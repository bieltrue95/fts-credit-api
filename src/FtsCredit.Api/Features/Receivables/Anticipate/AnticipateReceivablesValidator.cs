using FluentValidation;

namespace FtsCredit.Api.Features.Receivables.Anticipate;

public class AnticipateReceivablesValidator : AbstractValidator<AnticipateReceivablesCommand>
{
    public AnticipateReceivablesValidator()
    {
        RuleFor(x => x.Document)
            .NotEmpty().WithMessage("Documento é obrigatório.")
            .Length(11, 14);

        RuleFor(x => x.TotalReceivables)
            .GreaterThan(0).WithMessage("Total de recebíveis deve ser positivo.");

        RuleFor(x => x.AnticipationDays)
            .InclusiveBetween(1, 365).WithMessage("Dias de antecipação devem estar entre 1 e 365.");
    }
}
