using System.Text.Json;
using FtsCredit.Api.Domain.Entities;
using FtsCredit.Api.Domain.Enums;
using FtsCredit.Api.Domain.Interfaces;

namespace FtsCredit.Api.Features.CreditRequest.Create;

public class CreateCreditRequestHandler
{
    private const string EngineVersion = "1.0.0";

    private readonly ICustomerRepository _customers;
    private readonly ICreditRequestRepository _creditRequests;
    private readonly IOutboxWriter _outbox;
    private readonly ICacheService _cache;
    private readonly IUnitOfWork _uow;
    private readonly IScoreEngine _scoreEngine;

    public CreateCreditRequestHandler(
        ICustomerRepository customers,
        ICreditRequestRepository creditRequests,
        IOutboxWriter outbox,
        ICacheService cache,
        IUnitOfWork uow,
        IScoreEngine scoreEngine)
    {
        _customers = customers;
        _creditRequests = creditRequests;
        _outbox = outbox;
        _cache = cache;
        _uow = uow;
        _scoreEngine = scoreEngine;
    }

    public async Task<CreateCreditRequestResponse> HandleAsync(
        CreateCreditRequestCommand cmd,
        CancellationToken ct = default)
    {
        var customer = await _customers.GetByDocumentAsync(cmd.Document, ct);
        if (customer is null)
        {
            customer = new Customer
            {
                Id = Guid.NewGuid(),
                Document = cmd.Document,
                Name = cmd.CustomerName,
                MonthlyIncome = cmd.MonthlyIncome,
                RiskLevel = RiskLevel.Medium,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            await _customers.AddAsync(customer, ct);
        }
        else
        {
            customer.MonthlyIncome = cmd.MonthlyIncome;
            customer.UpdatedAt = DateTime.UtcNow;
            _customers.Update(customer);
        }

        var (score, riskLevel, approvedLimit) = await GetOrComputeScoreAsync(customer, ct);

        var creditRequest = new Domain.Entities.CreditRequest
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            Amount = cmd.Amount,
            Installments = cmd.Installments,
            ProductType = cmd.ProductType,
            CreatedAt = DateTime.UtcNow
        };

        string? rejectionReason = null;

        if (_scoreEngine.IsEligible(score))
        {
            creditRequest.Status = CreditStatus.Approved;
            creditRequest.ApprovedLimit = approvedLimit;
            customer.RiskLevel = riskLevel;
        }
        else
        {
            creditRequest.Status = CreditStatus.Rejected;
            rejectionReason = $"Score {score} abaixo do mínimo exigido (500).";
            creditRequest.RejectionReason = rejectionReason;
            customer.RiskLevel = RiskLevel.High;
        }

        var riskAnalysis = new RiskAnalysis
        {
            Id = Guid.NewGuid(),
            RequestId = creditRequest.Id,
            Score = score,
            ApprovedLimit = approvedLimit,
            RiskLevel = riskLevel,
            AnalysedAt = DateTime.UtcNow,
            EngineVersion = EngineVersion
        };

        creditRequest.RiskAnalysis = riskAnalysis;
        await _creditRequests.AddAsync(creditRequest, ct);

        var payload = JsonSerializer.Serialize(new
        {
            creditRequest.Id,
            customer.Document,
            creditRequest.Status,
            creditRequest.ApprovedLimit,
            creditRequest.RejectionReason
        });
        await _outbox.EnqueueAsync(creditRequest.Id, creditRequest.Status == CreditStatus.Approved ? "credit.approved" : "credit.rejected", payload, ct);

        await _uow.CommitAsync(ct);

        return new CreateCreditRequestResponse(
            creditRequest.Id,
            creditRequest.Status,
            creditRequest.ApprovedLimit,
            rejectionReason);
    }

    private async Task<(int Score, RiskLevel RiskLevel, decimal ApprovedLimit)> GetOrComputeScoreAsync(
        Customer customer, CancellationToken ct)
    {
        var cacheKey = $"score:{customer.Id}";
        var cached = await _cache.GetAsync<ScoreCache>(cacheKey, ct);
        if (cached is not null)
            return (cached.Score, cached.RiskLevel, cached.ApprovedLimit);

        var score = _scoreEngine.Compute(customer.MonthlyIncome);
        var (riskLevel, approvedLimit) = _scoreEngine.Classify(score, customer.MonthlyIncome);

        await _cache.SetAsync(cacheKey, new ScoreCache(score, riskLevel, approvedLimit), TimeSpan.FromSeconds(300), ct);

        return (score, riskLevel, approvedLimit);
    }

    private record ScoreCache(int Score, RiskLevel RiskLevel, decimal ApprovedLimit);
}
