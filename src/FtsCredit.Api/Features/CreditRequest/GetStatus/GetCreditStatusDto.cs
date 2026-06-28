using FtsCredit.Api.Domain.Enums;

namespace FtsCredit.Api.Features.CreditRequest.GetStatus;

public record GetCreditStatusQuery(Guid RequestId);

public record GetCreditStatusResponse(
    Guid RequestId,
    string CustomerName,
    string Document,
    decimal Amount,
    int Installments,
    CreditStatus Status,
    ProductType ProductType,
    decimal? ApprovedLimit,
    string? RejectionReason,
    RiskAnalysisResponse? RiskAnalysis,
    DateTime CreatedAt
);

public record RiskAnalysisResponse(
    int Score,
    RiskLevel RiskLevel,
    decimal ApprovedLimit,
    DateTime AnalysedAt,
    string EngineVersion
);
