using System.Diagnostics;
using Inventory.Api.Application.Dtos;
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

    public async Task<RecommendationDto?> GetForVehicleAsync(Guid vehicleId, CancellationToken ct = default)
    {
        var cacheKey = CacheKey(vehicleId);
        if (_cache.TryGetValue(cacheKey, out RecommendationDto? cached) && cached is not null)
        {
            return cached;
        }

        var vehicle = await _db.Vehicles.AsNoTracking().FirstOrDefaultAsync(v => v.Id == vehicleId, ct);
        if (vehicle is null)
        {
            return null;
        }

        var aging = _aging.Calculate(vehicle.AcquisitionDate);
        var carryingCost = _carryingCost.CalculateToDate(vehicle.AcquisitionCost, vehicle.AcquisitionDate);
        var context = new RecommendationContext(
            aging.DaysInInventory, aging.Tier, vehicle.ListPrice, vehicle.AcquisitionCost, carryingCost);

        var baseline = _baseline.Recommend(context);
        var dto = await EnrichOrBaselineAsync(vehicleId, context, baseline, ct);

        _cache.Set(cacheKey, dto, TimeSpan.FromMinutes(_options.CacheMinutes));
        return dto;
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
                    RecommendationSource.Ai, baseline.GroundingFacts);
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
}
