using FtsCredit.Api.Domain.Enums;
using FtsCredit.Api.Domain.Interfaces;
using FtsCredit.Api.Domain.Services;
using FtsCredit.Api.Features.ScoreValidation.ValidateScore;
using Moq;

namespace FtsCredit.Tests.Features.ScoreValidation;

public class ValidateScoreHandlerTests
{
    private readonly Mock<ICustomerRepository> _customerRepo = new();
    private readonly IScoreEngine _scoreEngine = new ScoreEngine();

    private ValidateScoreHandler CreateHandler() =>
        new(_customerRepo.Object, new NullCacheService(), _scoreEngine);

    [Theory]
    [InlineData(600, RiskLevel.Low, true)]    // score = 800 → LOW
    [InlineData(350, RiskLevel.Medium, true)] // score = 550 → MEDIUM
    [InlineData(250, RiskLevel.High, false)]  // score = 450 → HIGH → rejected
    public async Task HandleAsync_ReturnsCorrectRiskAndEligibility(
        decimal income, RiskLevel expectedRisk, bool expectedEligible)
    {
        var cmd = new ValidateScoreCommand("12345678901", income);

        _customerRepo.Setup(r => r.GetByDocumentAsync(cmd.Document, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Api.Domain.Entities.Customer?)null);

        var result = await CreateHandler().HandleAsync(cmd);

        Assert.Equal(expectedRisk, result.RiskLevel);
        Assert.Equal(expectedEligible, result.IsEligible);
    }

    [Fact]
    public async Task HandleAsync_LowRisk_ApprovedLimitIs80PercentOfIncome()
    {
        // income 600 → score 800 → LOW → limit = 600 * 0.8 = 480
        var cmd = new ValidateScoreCommand("11122233344", 600m);

        _customerRepo.Setup(r => r.GetByDocumentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Api.Domain.Entities.Customer?)null);

        var result = await CreateHandler().HandleAsync(cmd);

        Assert.Equal(480m, result.ApprovedLimit);
    }

    [Fact]
    public async Task HandleAsync_MediumRisk_ApprovedLimitIs50PercentOfIncome()
    {
        // income 350 → score 550 → MEDIUM → limit = 350 * 0.5 = 175
        var cmd = new ValidateScoreCommand("99988877766", 350m);

        _customerRepo.Setup(r => r.GetByDocumentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Api.Domain.Entities.Customer?)null);

        var result = await CreateHandler().HandleAsync(cmd);

        Assert.Equal(175m, result.ApprovedLimit);
    }

    [Fact]
    public async Task HandleAsync_HighRisk_ApprovedLimitIsZeroAndNotEligible()
    {
        // income 250 → score 450 → HIGH → limit = 0
        var cmd = new ValidateScoreCommand("55544433322", 250m);

        _customerRepo.Setup(r => r.GetByDocumentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Api.Domain.Entities.Customer?)null);

        var result = await CreateHandler().HandleAsync(cmd);

        Assert.Equal(0m, result.ApprovedLimit);
        Assert.False(result.IsEligible);
    }

    [Fact]
    public async Task HandleAsync_ReturnsDocumentInResponse()
    {
        var cmd = new ValidateScoreCommand("44455566677", 600m);

        _customerRepo.Setup(r => r.GetByDocumentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Api.Domain.Entities.Customer?)null);

        var result = await CreateHandler().HandleAsync(cmd);

        Assert.Equal(cmd.Document, result.Document);
    }

    private sealed class NullCacheService : ICacheService
    {
        public Task<T?> GetAsync<T>(string key, CancellationToken ct = default) =>
            Task.FromResult<T?>(default);

        public Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task RemoveAsync(string key, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task<bool> ExistsAsync(string key, CancellationToken ct = default) =>
            Task.FromResult(false);
    }
}
