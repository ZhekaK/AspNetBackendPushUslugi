using Microsoft.EntityFrameworkCore;
using PushPelmesh.Api.Data;
using PushPelmesh.Api.Models;

namespace PushPelmesh.Api.Services;

public class CalendarEventNotificationService
{
    private const string CalendarModuleKey = "CalendarScene";

    private readonly AppDbContext _db;
    private readonly WebPushSenderService _pushSender;
    private readonly ILogger<CalendarEventNotificationService> _logger;

    public CalendarEventNotificationService(
        AppDbContext db,
        WebPushSenderService pushSender,
        ILogger<CalendarEventNotificationService> logger)
    {
        _db = db;
        _pushSender = pushSender;
        _logger = logger;
    }

    public async Task<CalendarEventNotificationResult> SendTodayNotificationsAsync(
        CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(CalendarNotificationTime.GetMoscowNow().DateTime);

        var calendarEvents = await _db.CalendarEvents
            .Where(calendarEvent =>
                calendarEvent.Date == today &&
                !_db.CalendarEventNotifications.Any(notification =>
                    notification.CalendarEventId == calendarEvent.Id &&
                    notification.SentForDate == today))
            .OrderBy(calendarEvent => calendarEvent.Id)
            .ToListAsync(cancellationToken);

        if (calendarEvents.Count == 0)
        {
            return new CalendarEventNotificationResult(0, 0, 0, 0, 0);
        }

        int moduleNotifications = await CreateModuleNotificationsAsync(calendarEvents, today, cancellationToken);

        var subscriptions = await _db.PushNotificationSubscriptions
            .Where(subscription => subscription.Platform == PushPlatform.Web)
            .ToListAsync(cancellationToken);

        int sent = 0;
        int failed = 0;

        foreach (var calendarEvent in calendarEvents)
        {
            foreach (var subscription in subscriptions)
            {
                cancellationToken.ThrowIfCancellationRequested();

                bool success = await _pushSender.SendAsync(
                    subscription,
                    calendarEvent.Type == CalendarEventType.Birthday ? $"Сегодня день рождения: {calendarEvent.Title}!" : calendarEvent.Title,
                    GetNotificationBody(calendarEvent),
                    "/");

                if (success)
                    sent++;
                else
                    failed++;
            }

            _db.CalendarEventNotifications.Add(new CalendarEventNotification
            {
                CalendarEventId = calendarEvent.Id,
                SentForDate = today,
                SentAt = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Calendar event notifications completed. Events: {Events}, ModuleNotifications: {ModuleNotifications}, Subscriptions: {Subscriptions}, Sent: {Sent}, Failed: {Failed}",
            calendarEvents.Count,
            moduleNotifications,
            subscriptions.Count,
            sent,
            failed);

        return new CalendarEventNotificationResult(
            calendarEvents.Count,
            subscriptions.Count,
            sent,
            failed,
            moduleNotifications);
    }

    private async Task<int> CreateModuleNotificationsAsync(
        List<CalendarEvent> calendarEvents,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        var userIds = await _db.Users
            .Where(user => user.Type != UserType.Guest)
            .Select(user => user.Id)
            .ToListAsync(cancellationToken);

        if (userIds.Count == 0)
            return 0;

        int created = 0;
        var now = DateTime.UtcNow;

        foreach (var calendarEvent in calendarEvents)
        {
            var sourceKey = $"calendar:{calendarEvent.Id}:{today:yyyy-MM-dd}";

            var existingUserIds = await _db.UserModuleNotifications
                .Where(notification =>
                    notification.ModuleKey == CalendarModuleKey &&
                    notification.SourceKey == sourceKey)
                .Select(notification => notification.UserId)
                .ToListAsync(cancellationToken);

            var existing = existingUserIds.ToHashSet();

            foreach (int userId in userIds)
            {
                if (existing.Contains(userId))
                    continue;

                _db.UserModuleNotifications.Add(new UserModuleNotification
                {
                    UserId = userId,
                    ModuleKey = CalendarModuleKey,
                    SourceKey = sourceKey,
                    Title = calendarEvent.Title,
                    CreatedAt = now
                });

                created++;
            }
        }

        return created;
    }

    private static string GetNotificationBody(CalendarEvent calendarEvent)
    {
        if (!string.IsNullOrWhiteSpace(calendarEvent.Description))
            return calendarEvent.Description;

        return "Сегодня событие в календаре!";
    }
}

public record CalendarEventNotificationResult(
    int Events,
    int Subscriptions,
    int Sent,
    int Failed,
    int ModuleNotifications);
