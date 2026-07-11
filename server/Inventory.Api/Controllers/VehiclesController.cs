using FluentValidation;
using Inventory.Api.Application.Dtos;
using Inventory.Api.Application.Recommendations;
using Inventory.Api.Application.Services;
using Inventory.Api.Application.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Inventory.Api.Controllers;

[ApiController]
[Route("api/vehicles")]
public class VehiclesController : ControllerBase
{
    private readonly InventoryService _inventory;
    private readonly ActionService _actions;
    private readonly RecommendationService _recommendations;
    private readonly IValidator<CreateActionRequest> _createActionValidator;

    public VehiclesController(
        InventoryService inventory,
        ActionService actions,
        RecommendationService recommendations,
        IValidator<CreateActionRequest> createActionValidator)
    {
        _inventory = inventory;
        _actions = actions;
        _recommendations = recommendations;
        _createActionValidator = createActionValidator;
    }

    /// <summary>Filterable, paginated, sortable inventory list with derived aging + carrying-cost fields.</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<VehicleListItemDto>>> List(
        [FromQuery] VehicleQuery query, CancellationToken ct)
    {
        return Ok(await _inventory.ListAsync(query, ct));
    }

    /// <summary>Vehicle detail plus its full action history.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<VehicleDetailDto>> GetById(Guid id, CancellationToken ct)
    {
        var vehicle = await _inventory.GetByIdAsync(id, ct);
        return vehicle is null ? NotFound() : Ok(vehicle);
    }

    /// <summary>Log a new action against a vehicle (starts as <c>Proposed</c>). Requires authentication (write).</summary>
    [HttpPost("{id:guid}/actions")]
    [Authorize]
    public async Task<ActionResult<ActionDto>> CreateAction(
        Guid id, [FromBody] CreateActionRequest request, CancellationToken ct)
    {
        var validation = await _createActionValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            validation.CopyToModelState(ModelState);
            return ValidationProblem(ModelState);
        }

        var result = await _actions.CreateAsync(id, request, ct);
        return result.Status switch
        {
            ServiceStatus.NotFound => Problem(result.Error, statusCode: StatusCodes.Status404NotFound),
            _ => CreatedAtAction(nameof(GetById), new { id }, result.Action),
        };
    }

    /// <summary>AI-assisted (or baseline) action recommendation for the vehicle.</summary>
    [HttpGet("{id:guid}/recommendation")]
    [EnableRateLimiting(RateLimitPolicies.VehicleRecommendation)]
    public async Task<ActionResult<RecommendationDto>> GetRecommendation(Guid id, CancellationToken ct)
    {
        var recommendation = await _recommendations.GetForVehicleAsync(id, ct);
        return recommendation is null ? NotFound() : Ok(recommendation);
    }
}
