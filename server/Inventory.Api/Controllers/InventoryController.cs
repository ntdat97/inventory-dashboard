using Inventory.Api.Application.Dtos;
using Inventory.Api.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace Inventory.Api.Controllers;

[ApiController]
[Route("api/inventory")]
public class InventoryController : ControllerBase
{
    private readonly InventoryService _inventory;

    public InventoryController(InventoryService inventory)
    {
        _inventory = inventory;
    }

    /// <summary>Capital-at-risk KPI payload for the dashboard.</summary>
    [HttpGet("summary")]
    public async Task<ActionResult<InventorySummaryDto>> Summary([FromQuery] Guid? dealershipId, CancellationToken ct)
    {
        return Ok(await _inventory.GetSummaryAsync(dealershipId, ct));
    }

    /// <summary>Aging/Critical subset — convenience view for the aging spectrum.</summary>
    [HttpGet("aging")]
    public async Task<ActionResult<IReadOnlyList<VehicleListItemDto>>> Aging(
        [FromQuery] Guid? dealershipId, CancellationToken ct)
    {
        return Ok(await _inventory.GetAgingAsync(dealershipId, ct));
    }
}
