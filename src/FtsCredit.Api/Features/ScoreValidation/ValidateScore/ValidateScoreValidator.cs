using FluentValidation;

namespace FtsCredit.Api.Features.ScoreValidation.ValidateScore;

public class ValidateScoreValidator : AbstractValidator<ValidateScoreCommand>
{
    public ValidateScoreValidator()
    {
        RuleFor(x => x.Document)
            .NotEmpty().WithMessage("Documento é obrigatório.")
            .Length(11, 14);

        RuleFor(x => x.MonthlyIncome)
            .GreaterThan(0).WithMessage("Renda mensal deve ser positiva.");
    }
}
