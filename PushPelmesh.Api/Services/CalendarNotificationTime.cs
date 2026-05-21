namespace PushPelmesh.Api.Services;

public static class CalendarNotificationTime
{
    public static readonly TimeOnly DailySendTime = new(9, 0);
    public static readonly TimeOnly LastRetryTime = new(23, 0);
    public static readonly TimeSpan RetryInterval = TimeSpan.FromHours(1);

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

    public static bool IsWithinRetryWindow(DateTimeOffset utcNow)
    {
        var localNow = GetMoscowNow(utcNow);
        var localTime = TimeOnly.FromDateTime(localNow.DateTime);
        return localTime >= DailySendTime && localTime <= LastRetryTime;
    }

    public static TimeSpan GetDelayUntilNextRetry(DateTimeOffset utcNow)
    {
        var localNow = GetMoscowNow(utcNow);
        var localStart = CreateLocalDateTimeOffset(localNow, DailySendTime);
        var localEnd = CreateLocalDateTimeOffset(localNow, LastRetryTime);

        DateTimeOffset localTarget;

        if (localNow < localStart)
        {
            localTarget = localStart;
        }
        else if (localNow >= localEnd)
        {
            localTarget = localStart.AddDays(1);
        }
        else
        {
            localTarget = new DateTimeOffset(
                localNow.Year,
                localNow.Month,
                localNow.Day,
                localNow.Hour,
                0,
                0,
                localNow.Offset)
                .Add(RetryInterval);

            if (localTarget > localEnd)
                localTarget = localEnd;
        }

        var utcTarget = TimeZoneInfo.ConvertTime(localTarget, TimeZoneInfo.Utc);
        var delay = utcTarget - utcNow;
        return delay <= TimeSpan.Zero ? TimeSpan.FromSeconds(1) : delay;
    }

    public static TimeSpan GetDelayUntilNextDailySend(DateTimeOffset utcNow)
    {
        var localNow = GetMoscowNow(utcNow);
        var localTarget = CreateLocalDateTimeOffset(localNow, DailySendTime);

        if (localNow >= localTarget)
            localTarget = localTarget.AddDays(1);

        var utcTarget = TimeZoneInfo.ConvertTime(localTarget, TimeZoneInfo.Utc);
        return utcTarget - utcNow;
    }

    private static DateTimeOffset CreateLocalDateTimeOffset(DateTimeOffset localNow, TimeOnly time)
    {
        return new DateTimeOffset(
            localNow.Year,
            localNow.Month,
            localNow.Day,
            time.Hour,
            time.Minute,
            0,
            localNow.Offset);
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
