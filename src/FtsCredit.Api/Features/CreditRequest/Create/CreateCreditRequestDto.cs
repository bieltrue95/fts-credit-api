using FtsCredit.Api.Domain.Enums;

namespace FtsCredit.Api.Features.CreditRequest.Create;

public record CreateCreditRequestCommand(
    string Document,
    string CustomerName,
    decimal MonthlyIncome,
    decimal Amount,
    int Installments,
    ProductType ProductType
);

public record CreateCreditRequestResponse(
    Guid RequestId,
    CreditStatus Status,
    decimal? ApprovedLimit,
    string? RejectionReason
);
