using FtsCredit.Api.Domain.Entities;
using FtsCredit.Api.Domain.Enums;
using FtsCredit.Api.Domain.Interfaces;
using FtsCredit.Api.Domain.Services;
using FtsCredit.Api.Features.CreditRequest.Create;
using Moq;

namespace FtsCredit.Tests.Features.CreditRequest;

public class CreateCreditRequestHandlerTests
{
    private readonly Mock<ICustomerRepository> _customerRepo = new();
    private readonly Mock<ICreditRequestRepository> _creditRepo = new();
    private readonly Mock<IOutboxWriter> _outboxWriter = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly IScoreEngine _scoreEngine = new ScoreEngine();

    private CreateCreditRequestHandler CreateHandler(ICacheService? cache = null) =>
        new(_customerRepo.Object, _creditRepo.Object, _outboxWriter.Object,
            cache ?? new NullCacheService(), _uow.Object, _scoreEngine);

    [Fact]
    public async Task HandleAsync_NewCustomer_ApprovedWhenScoreAbove500()
    {
        // income 600 → score = (600 % 1000) + 200 = 800 → Approved (LOW risk)
        var cmd = new CreateCreditRequestCommand(
            Document: "12345678901",
            CustomerName: "João Silva",
            MonthlyIncome: 600m,
            Amount: 5000m,
            Installments: 12,
            ProductType: ProductType.PersonalLoan);

        _customerRepo.Setup(r => r.GetByDocumentAsync(cmd.Document, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Customer?)null);
        _uow.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var result = await CreateHandler().HandleAsync(cmd);

        Assert.Equal(CreditStatus.Approved, result.Status);
        Assert.NotNull(result.ApprovedLimit);
        Assert.Null(result.RejectionReason);
    }

    [Fact]
    public async Task HandleAsync_NewCustomer_RejectedWhenScoreBelow500()
    {
        // income 250 → score = (250 % 1000) + 200 = 450 → Rejected (HIGH risk)
        var cmd = new CreateCreditRequestCommand(
            Document: "98765432100",
            CustomerName: "Maria Santos",
            MonthlyIncome: 250m,
            Amount: 10000m,
            Installments: 24,
            ProductType: ProductType.PersonalLoan);

        _customerRepo.Setup(r => r.GetByDocumentAsync(cmd.Document, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Customer?)null);
        _uow.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var result = await CreateHandler().HandleAsync(cmd);

        Assert.Equal(CreditStatus.Rejected, result.Status);
        Assert.Null(result.ApprovedLimit);
        Assert.NotNull(result.RejectionReason);
    }

    [Fact]
    public async Task HandleAsync_ExistingCustomer_UpdatesIncomeAndProcesses()
    {
        var existingCustomer = new Customer
        {
            Id = Guid.NewGuid(),
            Document = "11122233344",
            Name = "Carlos Oliveira",
            MonthlyIncome = 500m,
            RiskLevel = RiskLevel.Medium,
            CreatedAt = DateTime.UtcNow.AddDays(-10),
            UpdatedAt = DateTime.UtcNow.AddDays(-10)
        };

        var cmd = new CreateCreditRequestCommand(
            Document: existingCustomer.Document,
            CustomerName: existingCustomer.Name,
            MonthlyIncome: 800m,
            Amount: 3000m,
            Installments: 6,
            ProductType: ProductType.ConsignedLoan);

        _customerRepo.Setup(r => r.GetByDocumentAsync(cmd.Document, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingCustomer);
        _uow.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var result = await CreateHandler().HandleAsync(cmd);

        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.RequestId);
        _customerRepo.Verify(r => r.Update(It.IsAny<Customer>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_RejectedRequest_ContainsRejectionReason()
    {
        var cmd = new CreateCreditRequestCommand(
            Document: "55566677788",
            CustomerName: "Ana Lima",
            MonthlyIncome: 100m, // score = (100 % 1000) + 200 = 300 → HIGH → rejected
            Amount: 2000m,
            Installments: 3,
            ProductType: ProductType.ReceivablesAnticipation);

        _customerRepo.Setup(r => r.GetByDocumentAsync(cmd.Document, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Customer?)null);
        _uow.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var result = await CreateHandler().HandleAsync(cmd);

        Assert.Equal(CreditStatus.Rejected, result.Status);
        Assert.Contains("500", result.RejectionReason);
    }

    [Fact]
    public async Task HandleAsync_Approved_EnqueuesApprovedEvent()
    {
        // income 600 → score 800 → Approved
        var cmd = new CreateCreditRequestCommand(
            Document: "77788899900",
            CustomerName: "Pedro Costa",
            MonthlyIncome: 600m,
            Amount: 1000m,
            Installments: 6,
            ProductType: ProductType.PersonalLoan);

        _customerRepo.Setup(r => r.GetByDocumentAsync(cmd.Document, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Customer?)null);
        _uow.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        await CreateHandler().HandleAsync(cmd);

        _outboxWriter.Verify(o => o.EnqueueAsync(
            It.IsAny<Guid>(),
            "credit.approved",
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Rejected_EnqueuesRejectedEvent()
    {
        // income 100 → score 300 → Rejected
        var cmd = new CreateCreditRequestCommand(
            Document: "11100011100",
            CustomerName: "Joana Melo",
            MonthlyIncome: 100m,
            Amount: 5000m,
            Installments: 12,
            ProductType: ProductType.PersonalLoan);

        _customerRepo.Setup(r => r.GetByDocumentAsync(cmd.Document, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Customer?)null);
        _uow.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        await CreateHandler().HandleAsync(cmd);

        _outboxWriter.Verify(o => o.EnqueueAsync(
            It.IsAny<Guid>(),
            "credit.rejected",
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
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
