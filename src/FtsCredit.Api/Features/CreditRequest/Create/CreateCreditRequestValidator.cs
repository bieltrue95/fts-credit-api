using FluentValidation;

namespace FtsCredit.Api.Features.CreditRequest.Create;

public class CreateCreditRequestValidator : AbstractValidator<CreateCreditRequestCommand>
{
    public CreateCreditRequestValidator()
    {
        RuleFor(x => x.Document)
            .NotEmpty().WithMessage("Documento é obrigatório.")
            .Length(11, 14).WithMessage("Documento deve ter 11 (CPF) ou 14 (CNPJ) caracteres.");

        RuleFor(x => x.CustomerName)
            .NotEmpty().WithMessage("Nome do cliente é obrigatório.")
            .MaximumLength(200);

        RuleFor(x => x.MonthlyIncome)
            .GreaterThan(0).WithMessage("Renda mensal deve ser positiva.");

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Valor solicitado deve ser positivo.");

        RuleFor(x => x.Installments)
            .InclusiveBetween(1, 360).WithMessage("Parcelas devem estar entre 1 e 360.");

        RuleFor(x => x.ProductType)
            .IsInEnum().WithMessage("Tipo de produto inválido.");
    }
}
