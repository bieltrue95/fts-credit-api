namespace FtsCredit.Api.Infrastructure.Persistence;

public class OutboxMessage
{
    public Guid Id { get; set; }
    public Guid AggregateId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public OutboxStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
}

public enum OutboxStatus
{
    Pending,
    Sent,
    Failed
}
