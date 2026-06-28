using FtsCredit.Api.Domain.Interfaces;
using FtsCredit.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FtsCredit.Api.Infrastructure.Messaging;

public class OutboxPublisher : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxPublisher> _logger;
    private static readonly TimeSpan _interval = TimeSpan.FromSeconds(5);

    public OutboxPublisher(IServiceScopeFactory scopeFactory, ILogger<OutboxPublisher> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await ProcessPendingMessagesAsync(stoppingToken);
            await Task.Delay(_interval, stoppingToken);
        }
    }

    private async Task ProcessPendingMessagesAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<IEventPublisher>();

        var pending = await db.OutboxMessages
            .Where(m => m.Status == OutboxStatus.Pending)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(ct);

        foreach (var msg in pending)
        {
            try
            {
                await publisher.PublishAsync("credit", msg.EventType, msg.Payload, ct);
                msg.Status = OutboxStatus.Sent;
                _logger.LogInformation("Outbox message {Id} published as {EventType}", msg.Id, msg.EventType);
            }
            catch (Exception ex)
            {
                msg.Status = OutboxStatus.Failed;
                _logger.LogError(ex, "Failed to publish outbox message {Id}", msg.Id);
            }
        }

        await db.SaveChangesAsync(ct);
    }
}
