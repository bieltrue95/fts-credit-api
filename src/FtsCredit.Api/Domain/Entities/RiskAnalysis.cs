using FtsCredit.Api.Domain.Enums;

namespace FtsCredit.Api.Domain.Entities;

public class RiskAnalysis
{
    public Guid Id { get; set; }
    public Guid RequestId { get; set; }
    public int Score { get; set; }
    public decimal ApprovedLimit { get; set; }
    public RiskLevel RiskLevel { get; set; }
    public DateTime AnalysedAt { get; set; }
    public string EngineVersion { get; set; } = string.Empty;
}
