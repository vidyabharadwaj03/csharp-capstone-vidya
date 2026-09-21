using FluentValidation;
using ReservationService.Dtos;

namespace ReservationService.Validation;

public class ReturnRequestValidator : AbstractValidator<ReturnRequest>
{
    private static readonly string[] AllowedConditions = { "GOOD", "FAIR", "POOR", "DAMAGED" };

    public ReturnRequestValidator()
    {
        RuleFor(x => x.Condition)
            .NotEmpty()
            .Must(c => AllowedConditions.Contains(c.ToUpperInvariant()))
            .WithMessage("Condition must be one of GOOD, FAIR, POOR, DAMAGED");
    }
}
