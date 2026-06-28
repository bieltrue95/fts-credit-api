namespace FtsCredit.Api.Domain.Interfaces;

public interface IOutboxWriter
{
    Task EnqueueAsync(Guid aggregateId, string eventType, string payload, CancellationToken ct = default);
}
