using Microsoft.EntityFrameworkCore;
using PushPelmesh.Api.Data;
using PushPelmesh.Api.Models;

namespace PushPelmesh.Api.Services;

public class BirthdayCalendarService
{
    private readonly AppDbContext _db;

    public BirthdayCalendarService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<RefreshBirthdaysResult> RefreshExpiredBirthdaysAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var expiredBirthdayEvents = await _db.CalendarEvents
            .Where(x =>
                x.Type == CalendarEventType.Birthday &&
                x.IsSystemEvent &&
                x.Date < today)
            .ToListAsync();

        if (expiredBirthdayEvents.Count == 0)
        {
            return new RefreshBirthdaysResult(0, 0);
        }

        var userIds = expiredBirthdayEvents
            .Where(x => x.CreatedByUserId != null)
            .Select(x => x.CreatedByUserId!.Value)
            .Distinct()
            .ToList();

        _db.CalendarEvents.RemoveRange(expiredBirthdayEvents);

        var users = await _db.Users
            .Where(x =>
                userIds.Contains(x.Id) &&
                x.Type != UserType.Guest &&
                x.BirthDate != null)
            .ToListAsync();

        int createdCount = 0;

        foreach (var user in users)
        {
            var nextBirthdayDate = GetNextBirthdayDate(user.BirthDate!.Value);

            bool alreadyExists = await _db.CalendarEvents.AnyAsync(x =>
                x.Type == CalendarEventType.Birthday &&
                x.IsSystemEvent &&
                x.CreatedByUserId == user.Id &&
                x.Date == nextBirthdayDate);

            if (alreadyExists)
                continue;

            var birthdayEvent = new CalendarEvent
            {
                Type = CalendarEventType.Birthday,
                Title = $"{user.FirstName}",
                Description = $"У {user.MiddleName} {user.FirstName} {user.LastName} сегодня день рождения! Дата рождения: {user.BirthDate.Value:yyyy-MM-dd}",
                Date = nextBirthdayDate,
                CreatedByUserId = user.Id,
                IsSystemEvent = true,
                CreatedAt = DateTime.UtcNow
            };

            _db.CalendarEvents.Add(birthdayEvent);
            createdCount++;
        }

        await _db.SaveChangesAsync();

        return new RefreshBirthdaysResult(
            expiredBirthdayEvents.Count,
            createdCount);
    }

    private static DateOnly GetNextBirthdayDate(DateOnly birthDate)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var birthdayThisYear = new DateOnly(
            today.Year,
            birthDate.Month,
            birthDate.Day);

        if (birthdayThisYear >= today)
            return birthdayThisYear;

        return new DateOnly(
            today.Year + 1,
            birthDate.Month,
            birthDate.Day);
    }
}

public record RefreshBirthdaysResult(
    int Removed,
    int Created);