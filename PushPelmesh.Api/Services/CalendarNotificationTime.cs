namespace PushPelmesh.Api.Services;

public static class CalendarNotificationTime
{
    public static readonly TimeOnly DailySendTime = new(9, 0);

    private static readonly Lazy<TimeZoneInfo> MoscowTimeZone = new(CreateMoscowTimeZone);

    public static DateTimeOffset GetMoscowNow(DateTimeOffset? utcNow = null)
    {
        return TimeZoneInfo.ConvertTime(utcNow ?? DateTimeOffset.UtcNow, MoscowTimeZone.Value);
    }

    public static bool HasReachedDailySendTime(DateTimeOffset utcNow)
    {
        var localNow = GetMoscowNow(utcNow);
        return TimeOnly.FromDateTime(localNow.DateTime) >= DailySendTime;
    }

    public static TimeSpan GetDelayUntilNextDailySend(DateTimeOffset utcNow)
    {
        var localNow = GetMoscowNow(utcNow);
        var localTarget = new DateTimeOffset(
            localNow.Year,
            localNow.Month,
            localNow.Day,
            DailySendTime.Hour,
            DailySendTime.Minute,
            0,
            localNow.Offset);

        if (localNow >= localTarget)
            localTarget = localTarget.AddDays(1);

        var utcTarget = TimeZoneInfo.ConvertTime(localTarget, TimeZoneInfo.Utc);
        return utcTarget - utcNow;
    }

    private static TimeZoneInfo CreateMoscowTimeZone()
    {
        foreach (var timeZoneId in new[] { "Europe/Moscow", "Russian Standard Time" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        return TimeZoneInfo.CreateCustomTimeZone(
            "Moscow",
            TimeSpan.FromHours(3),
            "Moscow",
            "Moscow");
    }
}
