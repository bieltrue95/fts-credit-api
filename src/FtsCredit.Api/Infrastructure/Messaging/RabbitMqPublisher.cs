using System.Text;
using FtsCredit.Api.Domain.Interfaces;
using RabbitMQ.Client;

namespace FtsCredit.Api.Infrastructure.Messaging;

public class RabbitMqPublisher : IEventPublisher
{
    private readonly IChannel _channel;

    public RabbitMqPublisher(IChannel channel) => _channel = channel;

    public async Task PublishAsync(string exchange, string routingKey, string payload, CancellationToken ct = default)
    {
        var body = Encoding.UTF8.GetBytes(payload);

        var props = new BasicProperties
        {
            ContentType = "application/json",
            DeliveryMode = DeliveryModes.Persistent
        };

        await _channel.BasicPublishAsync(
            exchange: exchange,
            routingKey: routingKey,
            mandatory: false,
            basicProperties: props,
            body: body,
            cancellationToken: ct);
    }
}
