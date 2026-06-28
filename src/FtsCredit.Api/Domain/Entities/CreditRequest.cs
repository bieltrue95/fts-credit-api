using FtsCredit.Api.Domain.Enums;

namespace FtsCredit.Api.Domain.Entities;

public class CreditRequest
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public decimal Amount { get; set; }
    public int Installments { get; set; }
    public CreditStatus Status { get; set; }
    public ProductType ProductType { get; set; }
    public decimal? ApprovedLimit { get; set; }
    public string? RejectionReason { get; set; }
    public DateTime CreatedAt { get; set; }

    public Customer Customer { get; set; } = null!;
    public RiskAnalysis? RiskAnalysis { get; set; }
}
