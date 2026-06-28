using FtsCredit.Api.Domain.Enums;
using FtsCredit.Api.Domain.Interfaces;

namespace FtsCredit.Api.Domain.Services;

public class ScoreEngine : IScoreEngine
{
    private const int MinScore = 500;

    public int Compute(decimal monthlyIncome)
    {
        var baseScore = (int)(monthlyIncome % 1000);
        return Math.Clamp(baseScore + 200, 100, 900);
    }

    public (RiskLevel RiskLevel, decimal ApprovedLimit) Classify(int score, decimal monthlyIncome) => score switch
    {
        >= 750 => (RiskLevel.Low, monthlyIncome * 0.8m),
        >= 500 => (RiskLevel.Medium, monthlyIncome * 0.5m),
        _ => (RiskLevel.High, 0m)
    };

    public bool IsEligible(int score) => score >= MinScore;
}
