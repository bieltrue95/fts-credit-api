using FtsCredit.Api.Domain.Interfaces;

namespace FtsCredit.Api.Infrastructure.Persistence;

public class OutboxService : IOutboxWriter
{
    private readonly AppDbContext _context;

    public OutboxService(AppDbContext context) => _context = context;

    public async Task EnqueueAsync(Guid aggregateId, string eventType, string payload, CancellationToken ct = default)
    {
        var message = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            AggregateId = aggregateId,
            EventType = eventType,
            Payload = payload,
            Status = OutboxStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        await _context.OutboxMessages.AddAsync(message, ct);
    }
}
