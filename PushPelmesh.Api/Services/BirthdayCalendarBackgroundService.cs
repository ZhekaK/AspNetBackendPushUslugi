namespace PushPelmesh.Api.Services;

public class BirthdayCalendarBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BirthdayCalendarBackgroundService> _logger;

    public BirthdayCalendarBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<BirthdayCalendarBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunRefreshAsync(stoppingToken);
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Birthday calendar background refresh failed");
            }

            await Task.Delay(TimeSpan.FromDays(1), stoppingToken);
        }
    }

    private async Task RunRefreshAsync(
        CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();

        var service = scope.ServiceProvider
            .GetRequiredService<BirthdayCalendarService>();

        var result = await service.RefreshExpiredBirthdaysAsync();

        _logger.LogInformation(
            "Birthday refresh completed. Removed: {Removed}, Created: {Created}",
            result.Removed,
            result.Created);
    }
}