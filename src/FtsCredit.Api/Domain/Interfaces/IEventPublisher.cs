namespace FtsCredit.Api.Domain.Interfaces;

public interface IEventPublisher
{
    Task PublishAsync(string exchange, string routingKey, string payload, CancellationToken ct = default);
}
