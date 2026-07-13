using System.Diagnostics;
using Inventory.Api.Application.Dtos;
using Inventory.Api.Domain.Enums;
using Inventory.Api.Domain.Services;
using Inventory.Api.Infrastructure;
using Inventory.Api.Infrastructure.Observability;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Inventory.Api.Application.Recommendations;

/// <summary>
/// Produces an action recommendation for a vehicle. Baseline-first (always available), AI-enriched when the LLM is
/// reachable and its output validates, and degrading to baseline on any failure — the feature never 5xxs (design §8).
/// Results are cached per vehicle as a cost/abuse guard on the public demo.
/// </summary>
public class RecommendationService
{
    private readonly AppDbContext _db;
    private readonly BaselineRecommender _baseline;
    private readonly IAiClient _aiClient;
    private readonly AgingCalculator _aging;
    private readonly CarryingCostCalculator _carryingCost;
    private readonly IMemoryCache _cache;
    private readonly AiOptions _options;
    private readonly AppMetrics _metrics;
    private readonly ILogger<RecommendationService> _logger;

    public RecommendationService(
        AppDbContext db,
        BaselineRecommender baseline,
        IAiClient aiClient,
        AgingCalculator aging,
        CarryingCostCalculator carryingCost,
        IMemoryCache cache,
        IOptions<AiOptions> options,
        AppMetrics metrics,
        ILogger<RecommendationService> logger)
    {
        _db = db;
        _baseline = baseline;
        _aiClient = aiClient;
        _aging = aging;
        _carryingCost = carryingCost;
        _cache = cache;
        _options = options.Value;
        _metrics = metrics;
        _logger = logger;
    }

    public async Task<RecommendationResultOf> GetForVehicleAsync(Guid vehicleId, CancellationToken ct = default)
    {
        var cacheKey = CacheKey(vehicleId);
        if (_cache.TryGetValue(cacheKey, out RecommendationDto? cached) && cached is not null)
        {
            return RecommendationResultOf.Ok(cached);
        }

        var vehicle = await _db.Vehicles.AsNoTracking().FirstOrDefaultAsync(v => v.Id == vehicleId, ct);
        if (vehicle is null)
        {
            return RecommendationResultOf.NotFound($"Vehicle {vehicleId} was not found.");
        }

        // No AI/baseline recommendation for a closed unit: it has left the risk ledger, so there's no action to take —
        // and short-circuiting here means a sold vehicle never spends an LLM call (part of the public-demo cost guard).
        if (vehicle.Status.IsClosed())
        {
            return RecommendationResultOf.Conflict(
                $"Vehicle {vehicleId} is {vehicle.Status}; recommendations apply to active inventory only.");
        }

        var aging = _aging.Calculate(vehicle.AcquisitionDate, vehicle.ClosedDate);
        var carryingCost = _carryingCost.CalculateToDate(vehicle.AcquisitionCost, vehicle.AcquisitionDate, vehicle.ClosedDate);
        var benchmarks = await GetActiveFleetBenchmarksAsync(ct);
        var context = new RecommendationContext(
            aging.DaysInInventory,
            aging.Tier,
            vehicle.ListPrice,
            vehicle.AcquisitionCost,
            carryingCost,
            vehicle.Year,
            vehicle.Make,
            vehicle.Model,
            vehicle.Trim,
            vehicle.Mileage,
            benchmarks.AverageDaysInInventory,
            benchmarks.AverageCarryingCostToDate);

        var baseline = _baseline.Recommend(context);
        var dto = await EnrichOrBaselineAsync(vehicleId, context, baseline, ct);

        _cache.Set(cacheKey, dto, TimeSpan.FromMinutes(_options.CacheMinutes));
        return RecommendationResultOf.Ok(dto);
    }

    private async Task<FleetBenchmarks> GetActiveFleetBenchmarksAsync(CancellationToken ct)
    {
        var vehicles = await _db.Vehicles
            .AsNoTracking()
            .Where(v => v.Status == VehicleStatus.InStock || v.Status == VehicleStatus.Reserved)
            .ToListAsync(ct);

        if (vehicles.Count == 0)
        {
            return new FleetBenchmarks(null, null);
        }

        var signals = vehicles
            .Select(v =>
            {
                var aging = _aging.Calculate(v.AcquisitionDate, v.ClosedDate);
                var carryingCost = _carryingCost.CalculateToDate(v.AcquisitionCost, v.AcquisitionDate, v.ClosedDate);
                return new { aging.DaysInInventory, CarryingCost = carryingCost };
            })
            .ToList();

        return new FleetBenchmarks(
            Math.Round((decimal)signals.Average(v => v.DaysInInventory), 0),
            Math.Round(signals.Average(v => v.CarryingCost), 0));
    }

    private async Task<RecommendationDto> EnrichOrBaselineAsync(
        Guid vehicleId, RecommendationContext context, RecommendationResult baseline, CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var enriched = await _aiClient.EnrichAsync(context, baseline, ct);
            _metrics.RecordAiCall("success", stopwatch.Elapsed.TotalSeconds);

            if (enriched is not null && AiRecommendationValidator.IsValid(enriched, context))
            {
                _metrics.RecordRecommendation("ai");
                return new RecommendationDto(
                    vehicleId, enriched.Action, enriched.ProposedValue, enriched.Rationale,
                    RecommendationSource.Ai, baseline.GroundingFacts, enriched.MarketRead);
            }

            if (enriched is not null)
            {
                _logger.LogWarning("AI recommendation for vehicle {VehicleId} failed validation; using baseline.", vehicleId);
            }
        }
        catch (Exception ex)
        {
            // Graceful degradation: the LLM being down/slow/erroring never breaks the feature.
            _metrics.RecordAiCall("failure", stopwatch.Elapsed.TotalSeconds);
            _logger.LogWarning(ex, "AI recommendation for vehicle {VehicleId} failed; using baseline.", vehicleId);
        }

        _metrics.RecordRecommendation("baseline");
        return new RecommendationDto(
            vehicleId, baseline.Action, baseline.ProposedValue, baseline.Rationale,
            RecommendationSource.Baseline, baseline.GroundingFacts);
    }

    private static string CacheKey(Guid vehicleId) => $"recommendation:{vehicleId}";

    private sealed record FleetBenchmarks(decimal? AverageDaysInInventory, decimal? AverageCarryingCostToDate);
}
