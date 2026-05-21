namespace PushPelmesh.Api.Services;

public class CalendarEventNotificationBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CalendarEventNotificationBackgroundService> _logger;

    public CalendarEventNotificationBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<CalendarEventNotificationBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (CalendarNotificationTime.IsWithinRetryWindow(DateTimeOffset.UtcNow))
            await RunSendAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = CalendarNotificationTime.GetDelayUntilNextRetry(DateTimeOffset.UtcNow);

            _logger.LogInformation(
                "Next calendar event notification retry check in {Delay}",
                delay);

            await Task.Delay(delay, stoppingToken);

            if (CalendarNotificationTime.IsWithinRetryWindow(DateTimeOffset.UtcNow))
                await RunSendAsync(stoppingToken);
        }
    }

    private async Task RunSendAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();

            var service = scope.ServiceProvider
                .GetRequiredService<CalendarEventNotificationService>();

            var result = await service.SendTodayNotificationsAsync(stoppingToken);

            _logger.LogInformation(
                "Calendar event notification check finished. Events: {Events}, Subscriptions: {Subscriptions}, Sent: {Sent}, Failed: {Failed}, ModuleNotifications: {ModuleNotifications}",
                result.Events,
                result.Subscriptions,
                result.Sent,
                result.Failed,
                result.ModuleNotifications);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Calendar event notification check failed");
        }
    }
}
