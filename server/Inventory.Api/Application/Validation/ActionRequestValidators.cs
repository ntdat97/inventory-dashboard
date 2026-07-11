using FluentValidation;
using Inventory.Api.Application.Dtos;

namespace Inventory.Api.Application.Validation;

/// <summary>Validates a new-action request: known type, non-empty bounded note, non-negative proposed value.</summary>
public class CreateActionRequestValidator : AbstractValidator<CreateActionRequest>
{
    public CreateActionRequestValidator()
    {
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Note)
            .NotEmpty()
            .MaximumLength(2000);
        RuleFor(x => x.ProposedValue)
            .GreaterThanOrEqualTo(0)
            .When(x => x.ProposedValue.HasValue);
    }
}

/// <summary>Validates a lifecycle-transition request: known target status and outcome. Legality of the transition
/// itself is the domain's job (<c>ActionWorkflow</c>), so it is deliberately not re-checked here.</summary>
public class UpdateActionRequestValidator : AbstractValidator<UpdateActionRequest>
{
    public UpdateActionRequestValidator()
    {
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.Outcome).IsInEnum().When(x => x.Outcome.HasValue);
    }
}
