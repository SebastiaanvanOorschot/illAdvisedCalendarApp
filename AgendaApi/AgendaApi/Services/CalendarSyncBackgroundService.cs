using AgendaApi.Data;
using Microsoft.EntityFrameworkCore;

namespace AgendaApi.Services;

public class CalendarSyncBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<CalendarSyncBackgroundService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(5); // Check every 5 minutes

    public CalendarSyncBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<CalendarSyncBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Calendar Sync Background Service is starting");

        // Wait 30 seconds after startup before first run
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SyncAllSubscriptionsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while syncing calendar subscriptions");
            }

            // Wait for the next check interval
            await Task.Delay(_checkInterval, stoppingToken);
        }

        _logger.LogInformation("Calendar Sync Background Service is stopping");
    }

    private async Task SyncAllSubscriptionsAsync(CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AgendaDbContext>();
        var syncService = scope.ServiceProvider.GetRequiredService<ICalSyncService>();

        // Get all active subscriptions that need syncing
        var now = DateTime.UtcNow;
        var subscriptions = await context.CalendarSubscriptions
            .Where(s => s.IsActive)
            .Where(s => s.LastSyncedAt == null ||
                       s.LastSyncedAt.Value.AddMinutes(s.SyncIntervalMinutes) <= now)
            .ToListAsync(stoppingToken);

        if (subscriptions.Count == 0)
        {
            return;
        }

        _logger.LogInformation("Found {Count} subscription(s) to sync", subscriptions.Count);

        foreach (var subscription in subscriptions)
        {
            if (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                _logger.LogInformation("Syncing subscription {SubscriptionId}: {Name}",
                    subscription.Id, subscription.Name);

                var (success, error) = await syncService.SyncSubscriptionAsync(subscription);

                if (success)
                {
                    _logger.LogInformation("Successfully synced subscription {SubscriptionId}", subscription.Id);
                }
                else
                {
                    _logger.LogWarning("Failed to sync subscription {SubscriptionId}: {Error}",
                        subscription.Id, error);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing subscription {SubscriptionId}", subscription.Id);
            }

            // Small delay between subscriptions to avoid overwhelming the system
            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
        }
    }
}
