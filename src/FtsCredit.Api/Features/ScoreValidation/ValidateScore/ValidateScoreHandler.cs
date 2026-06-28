using FtsCredit.Api.Domain.Interfaces;

namespace FtsCredit.Api.Features.ScoreValidation.ValidateScore;

public class ValidateScoreHandler
{
    private readonly ICustomerRepository _customers;
    private readonly ICacheService _cache;
    private readonly IScoreEngine _scoreEngine;

    public ValidateScoreHandler(ICustomerRepository customers, ICacheService cache, IScoreEngine scoreEngine)
    {
        _customers = customers;
        _cache = cache;
        _scoreEngine = scoreEngine;
    }

    public async Task<ValidateScoreResponse> HandleAsync(ValidateScoreCommand cmd, CancellationToken ct = default)
    {
        var customer = await _customers.GetByDocumentAsync(cmd.Document, ct);

        var income = customer?.MonthlyIncome ?? cmd.MonthlyIncome;
        var customerId = customer?.Id;

        if (customerId is not null)
        {
            var cached = await _cache.GetAsync<ScoreCache>($"score:{customerId}", ct);
            if (cached is not null)
                return BuildResponse(cmd.Document, cached.Score, cached.RiskLevel, cached.ApprovedLimit);
        }

        var score = _scoreEngine.Compute(income);
        var (riskLevel, approvedLimit) = _scoreEngine.Classify(score, income);

        if (customerId is not null)
        {
            await _cache.SetAsync(
                $"score:{customerId}",
                new ScoreCache(score, riskLevel, approvedLimit),
                TimeSpan.FromSeconds(300),
                ct);
        }

        return BuildResponse(cmd.Document, score, riskLevel, approvedLimit);
    }

    private ValidateScoreResponse BuildResponse(string doc, int score, Domain.Enums.RiskLevel risk, decimal limit) =>
        new(doc, score, risk, limit, _scoreEngine.IsEligible(score));

    private record ScoreCache(int Score, Domain.Enums.RiskLevel RiskLevel, decimal ApprovedLimit);
}
