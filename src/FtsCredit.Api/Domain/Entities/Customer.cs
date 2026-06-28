using FtsCredit.Api.Domain.Enums;

namespace FtsCredit.Api.Domain.Entities;

public class Customer
{
    public Guid Id { get; set; }
    public string Document { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal MonthlyIncome { get; set; }
    public RiskLevel RiskLevel { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<CreditRequest> CreditRequests { get; set; } = new List<CreditRequest>();
}
