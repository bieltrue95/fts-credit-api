using FtsCredit.Api.Domain.Enums;

namespace FtsCredit.Api.Features.ScoreValidation.ValidateScore;

public record ValidateScoreCommand(string Document, decimal MonthlyIncome);

public record ValidateScoreResponse(
    string Document,
    int Score,
    RiskLevel RiskLevel,
    decimal ApprovedLimit,
    bool IsEligible
);
