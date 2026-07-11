using FluentValidation;
using Inventory.Api.Application.Dtos;
using Inventory.Api.Application.Services;
using Inventory.Api.Application.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inventory.Api.Controllers;

[ApiController]
[Route("api/actions")]
public class ActionsController : ControllerBase
{
    private readonly ActionService _actions;
    private readonly IValidator<UpdateActionRequest> _updateValidator;

    public ActionsController(ActionService actions, IValidator<UpdateActionRequest> updateValidator)
    {
        _actions = actions;
        _updateValidator = updateValidator;
    }

    /// <summary>Lifecycle transition / set outcome. Invalid transition -> 409 ProblemDetails (gated by ActionWorkflow). Requires authentication (write).</summary>
    [HttpPatch("{id:guid}")]
    [Authorize]
    public async Task<ActionResult<ActionDto>> Transition(
        Guid id, [FromBody] UpdateActionRequest request, CancellationToken ct)
    {
        var validation = await _updateValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            validation.CopyToModelState(ModelState);
            return ValidationProblem(ModelState);
        }

        var result = await _actions.TransitionAsync(id, request, ct);
        return result.Status switch
        {
            ServiceStatus.NotFound => Problem(result.Error, statusCode: StatusCodes.Status404NotFound),
            ServiceStatus.Conflict => Problem(result.Error, statusCode: StatusCodes.Status409Conflict),
            _ => Ok(result.Action),
        };
    }
}
