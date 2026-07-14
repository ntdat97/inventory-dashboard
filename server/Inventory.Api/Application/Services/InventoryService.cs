using Inventory.Api.Application.Auth;
using Inventory.Api.Application.Dtos;
using Inventory.Api.Domain.Configuration;
using Inventory.Api.Domain.Entities;
using Inventory.Api.Domain.Enums;
using Inventory.Api.Domain.Services;
using Inventory.Api.Infrastructure;
using Inventory.Api.Infrastructure.Observability;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Inventory.Api.Application.Services;

/// <summary>
/// Orchestrates the read path: listing/filtering vehicles and assembling summary KPIs. Aging tier and
/// carrying cost are derived on read via the pure calculators (single source of truth = AcquisitionDate + config),
/// so derived-field filters (tier, min/max days) are applied in memory to stay exactly consistent with them.
/// </summary>
public class InventoryService
{
    private const int MaxPageSize = 100;

    // "Aged/distressed" for the capital-at-risk KPIs means past the aging threshold => Critical tier (A3).
    private const AgingTier AgedTier = AgingTier.Critical;

    private readonly AppDbContext _db;
    private readonly AgingCalculator _aging;
    private readonly CarryingCostCalculator _carryingCost;
    private readonly AgingConfig _agingConfig;
    private readonly AppMetrics _metrics;
    private readonly ICurrentUserService _currentUser;

    public InventoryService(
        AppDbContext db,
        AgingCalculator aging,
        CarryingCostCalculator carryingCost,
        IOptions<AgingConfig> agingConfig,
        AppMetrics metrics,
        ICurrentUserService currentUser)
    {
        _db = db;
        _aging = aging;
        _carryingCost = carryingCost;
        _agingConfig = agingConfig.Value;
        _metrics = metrics;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Resolves the dealership scope from the current user's JWT claim.
    /// When the claim is present the user sees only their dealership's vehicles.
    /// When absent (platform admin / unconfigured demo token) no restriction is applied.
    /// </summary>
    private Guid? ScopedDealershipId =>
        _currentUser.DealershipId is { } s && Guid.TryParse(s, out var id) ? id : null;

    public async Task<PagedResult<VehicleListItemDto>> ListAsync(VehicleQuery query, CancellationToken ct = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);

        // Enforce dealership scope from the token — the caller cannot override this.
        // If the token carries a dealershipId it always wins; otherwise fall back to the query param (platform admin).
        var effectiveDealershipId = ScopedDealershipId ?? query.DealershipId;

        // DB-side filters on persisted columns; derived filters (tier, days) applied after in-memory derivation.
        var vehicles = await FilteredByColumns(query, effectiveDealershipId).AsNoTracking().ToListAsync(ct);

        var items = vehicles
            .Select(ToListItem)
            .Where(v => query.Tier.Count == 0 || query.Tier.Contains(v.Tier))
            .Where(v => query.MinDays is null || v.DaysInInventory >= query.MinDays)
            .Where(v => query.MaxDays is null || v.DaysInInventory <= query.MaxDays)
            .ToList();

        var sorted = ApplySort(items, query.Sort).ToList();
        var totalCount = sorted.Count;
        var pageItems = sorted.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return new PagedResult<VehicleListItemDto>(pageItems, page, pageSize, totalCount);
    }

    public async Task<VehicleDetailDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var scopedDealershipId = ScopedDealershipId;
        var vehicle = await _db.Vehicles
            .AsNoTracking()
            .Include(v => v.Dealership)
            .Include(v => v.Actions)
            // When the user is scoped to a dealership, treat cross-dealership access as not-found
            // (404 rather than 403 — avoids leaking that the vehicle ID exists).
            .FirstOrDefaultAsync(v => v.Id == id
                && (scopedDealershipId == null || v.DealershipId == scopedDealershipId), ct);

        if (vehicle is null)
        {
            return null;
        }

        return DtoMapper.ToVehicleDetailDto(vehicle, _aging, _carryingCost);
    }

    public async Task<InventorySummaryDto> GetSummaryAsync(CancellationToken ct = default)
    {
        var dealershipId = ScopedDealershipId;
        var query = _db.Vehicles.AsNoTracking().AsQueryable();
        if (dealershipId is not null)
        {
            query = query.Where(v => v.DealershipId == dealershipId);
        }

        // KPIs describe capital currently held; sold/transferred/auction units have left the active risk ledger.
        query = ApplyScope(query, VehicleScope.Active);

        var vehicles = await query.ToListAsync(ct);
        var items = vehicles.Select(ToListItem).ToList();

        var totalUnits = items.Count;
        var agedItems = items.Where(v => v.Tier == AgedTier).ToList();

        var tierBreakdown = Enum.GetValues<AgingTier>()
            .ToDictionary(tier => tier, tier => items.Count(v => v.Tier == tier));

        // Business signal for /metrics: the aged-units count served on the latest summary (design §9).
        _metrics.SetAgedUnits(agedItems.Count);

        return new InventorySummaryDto(
            TotalUnits: totalUnits,
            TotalInventoryValue: items.Sum(v => v.ListPrice),
            AgedUnits: agedItems.Count,
            AgedPercent: totalUnits == 0 ? 0m : Math.Round(agedItems.Count * 100m / totalUnits, 2),
            CapitalTiedInAged: agedItems.Sum(v => v.AcquisitionCost),
            AvgDaysInInventory: totalUnits == 0 ? 0m : Math.Round((decimal)items.Average(v => v.DaysInInventory), 2),
            TotalCarryingCostToDate: items.Sum(v => v.CarryingCostToDate),
            TierBreakdown: tierBreakdown);
    }

    /// <summary>Aging/Critical subset — a convenience view over the list filter for the dashboard's aging spectrum.</summary>
    public async Task<IReadOnlyList<VehicleListItemDto>> GetAgingAsync(CancellationToken ct = default)
    {
        var dealershipId = ScopedDealershipId;
        var query = _db.Vehicles.AsNoTracking().AsQueryable();
        if (dealershipId is not null)
        {
            query = query.Where(v => v.DealershipId == dealershipId);
        }

        var vehicles = await ApplyScope(query, VehicleScope.Active).ToListAsync(ct);

        return vehicles
            .Select(ToListItem)
            .Where(v => v.Tier is AgingTier.Aging or AgingTier.Critical)
            .OrderByDescending(v => v.DaysInInventory)
            .ToList();
    }

    private IQueryable<Vehicle> FilteredByColumns(VehicleQuery query, Guid? dealershipId = null)
    {
        var q = _db.Vehicles.AsQueryable();

        // dealershipId arg wins over query.DealershipId (it carries the token-enforced scope).
        var effectiveDealershipId = dealershipId ?? query.DealershipId;
        if (effectiveDealershipId is not null)
        {
            q = q.Where(v => v.DealershipId == effectiveDealershipId);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            // Single free-text box: case-insensitive substring across the row's identifying columns.
            // ToLower().Contains (not Postgres ILike) keeps the query provider-agnostic — the design's
            // "swap the DB provider in one line" claim only holds if we don't reach for pg-specific SQL.
            var term = query.Search.Trim().ToLower();
            q = q.Where(v =>
                v.Make.ToLower().Contains(term)
                || v.Model.ToLower().Contains(term)
                || (v.Trim != null && v.Trim.ToLower().Contains(term))
                || (v.Color != null && v.Color.ToLower().Contains(term))
                || v.Vin.ToLower().Contains(term)
                || v.Year.ToString().Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(query.Make))
        {
            q = q.Where(v => v.Make == query.Make);
        }

        if (!string.IsNullOrWhiteSpace(query.Model))
        {
            q = q.Where(v => v.Model == query.Model);
        }

        if (query.Status.Count > 0)
        {
            q = q.Where(v => query.Status.Contains(v.Status));
        }

        return ApplyScope(q, query.Scope);
    }

    private static IQueryable<Vehicle> ApplyScope(IQueryable<Vehicle> q, VehicleScope scope)
    {
        // Active = still capital-at-risk (InStock/Reserved); Closed = left the ledger (Sold/Transferred/AtAuction).
        // Kept as an inline predicate (not the VehicleStatusExtensions helper) so EF can translate it to SQL.
        return scope switch
        {
            VehicleScope.Active => q.Where(v => v.Status == VehicleStatus.InStock || v.Status == VehicleStatus.Reserved),
            VehicleScope.Closed => q.Where(v => v.Status != VehicleStatus.InStock && v.Status != VehicleStatus.Reserved),
            VehicleScope.All => q,
            _ => q.Where(v => v.Status == VehicleStatus.InStock || v.Status == VehicleStatus.Reserved),
        };
    }

    private static IEnumerable<VehicleListItemDto> ApplySort(IEnumerable<VehicleListItemDto> items, string? sort)
    {
        var descending = false;
        var key = sort?.Trim();
        if (!string.IsNullOrEmpty(key) && key.StartsWith('-'))
        {
            descending = true;
            key = key[1..];
        }

        Func<VehicleListItemDto, IComparable> selector = (key ?? string.Empty).ToLowerInvariant() switch
        {
            "listprice" => v => v.ListPrice,
            "make" => v => v.Make,
            "year" => v => v.Year,
            "carryingcost" => v => v.CarryingCostToDate,
            "daysininventory" => v => v.DaysInInventory,
            _ => v => v.DaysInInventory, // default key
        };

        // Default direction is most-aged-first (descending days) when no sort is supplied.
        if (string.IsNullOrEmpty(sort))
        {
            descending = true;
        }

        return descending ? items.OrderByDescending(selector) : items.OrderBy(selector);
    }

    private VehicleListItemDto ToListItem(Vehicle v) =>
        DtoMapper.ToVehicleListItemDto(v, _aging, _carryingCost);
}
