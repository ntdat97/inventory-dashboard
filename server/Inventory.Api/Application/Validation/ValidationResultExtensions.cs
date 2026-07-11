using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Inventory.Api.Application.Validation;

/// <summary>Copies FluentValidation failures into MVC's <see cref="ModelStateDictionary"/> so they surface as
/// a standard RFC 7807 validation ProblemDetails (400) via <c>ValidationProblem</c>.</summary>
public static class ValidationResultExtensions
{
    public static void CopyToModelState(this ValidationResult result, ModelStateDictionary modelState)
    {
        foreach (var error in result.Errors)
        {
            modelState.AddModelError(error.PropertyName, error.ErrorMessage);
        }
    }
}
