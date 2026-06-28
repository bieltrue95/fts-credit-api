using FtsCredit.Api.Domain.Enums;

namespace FtsCredit.Api.Domain.Interfaces;

public interface IScoreEngine
{
    int Compute(decimal monthlyIncome);
    (RiskLevel RiskLevel, decimal ApprovedLimit) Classify(int score, decimal monthlyIncome);
    bool IsEligible(int score);
}
