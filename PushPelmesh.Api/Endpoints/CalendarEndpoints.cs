using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using PushPelmesh.Api.Data;
using PushPelmesh.Api.Dtos;
using PushPelmesh.Api.Models;
using PushPelmesh.Api.Security;
using PushPelmesh.Api.Services;

namespace PushPelmesh.Api.Endpoints;

public static class CalendarEndpoints
{
    public static void MapCalendarEndpoints(this WebApplication app)
    {
        app.MapGet("/api/calendar/events", async (
            DateOnly? from,
            DateOnly? to,
            AppDbContext db) =>
        {
            var start = from ?? DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-1));
            var end = to ?? DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(1));

            var events = await db.CalendarEvents
                .Where(x => x.Date >= start && x.Date <= end)
                .OrderBy(x => x.Date)
                .Select(x => new CalendarEventDto
                {
                    Id = x.Id,
                    Type = x.Type,
                    TypeName = x.Type.ToString(),
                    Title = x.Title,
                    Description = x.Description,
                    Date = x.Date,
                    IsSystemEvent = x.IsSystemEvent,
                    CreatedByUser = x.CreatedByUser,
                    CreatedByUserId = x.CreatedByUserId
                })
                .ToListAsync();

            return Results.Ok(new
            {
                events
            });
        })
        .RequireAuthorization();

        app.MapPost("/api/calendar/events", async (
            CreateCalendarEventRequest request,
            ClaimsPrincipal claimsUser,
            AppDbContext db) =>
        {
            var userIdText = claimsUser.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (!int.TryParse(userIdText, out var userId))
            {
                return Results.Unauthorized();
            }

            var user = await db.Users.FirstOrDefaultAsync(x => x.Id == userId);

            if (user == null)
            {
                return Results.NotFound(new
                {
                    message = "User not found"
                });
            }

            var roles = await db.UserRoles.Where(x => x.UserSeries == user.UserSeries && x.UserNumbers == user.UserNumber).ToListAsync();

            if (!CanCreateEvent(roles, request.Type))
            {
                return Results.Forbid();
            }

            if (string.IsNullOrWhiteSpace(request.Title))
            {
                return Results.BadRequest(new
                {
                    message = "Title is required"
                });
            }

            if (request.Date == default)
            {
                return Results.BadRequest(new
                {
                    message = "Date is required"
                });
            }

            var calendarEvent = new CalendarEvent
            {
                Type = request.Type,
                Title = request.Title,
                Description = request.Description,
                Date = request.Date,
                CreatedByUser = user.FirstName,
                CreatedByUserId = user.Id,
                IsSystemEvent = false,
                CreatedAt = DateTime.UtcNow
            };

            db.CalendarEvents.Add(calendarEvent);
            await db.SaveChangesAsync();

            return Results.Ok(new CalendarEventDto
            {
                Id = calendarEvent.Id,
                Type = calendarEvent.Type,
                TypeName = calendarEvent.Type.ToString(),
                Title = calendarEvent.Title,
                Description = calendarEvent.Description,
                Date = calendarEvent.Date,
                IsSystemEvent = calendarEvent.IsSystemEvent,
                CreatedByUser = calendarEvent.CreatedByUser,
                CreatedByUserId = calendarEvent.CreatedByUserId
            });
        })
        .RequireAuthorization();

        app.MapDelete("/api/calendar/events/{id:int}", async (
            int id,
            ClaimsPrincipal claimsUser,
            AppDbContext db) =>
        {
            var userIdText = claimsUser.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (!int.TryParse(userIdText, out var userId))
            {
                return Results.Unauthorized();
            }

            var user = await db.Users.FirstOrDefaultAsync(x => x.Id == userId);

            if (user == null)
            {
                return Results.NotFound(new
                {
                    message = "User not found"
                });
            }

            var calendarEvent = await db.CalendarEvents
                .FirstOrDefaultAsync(x => x.Id == id);

            if (calendarEvent == null)
            {
                return Results.NotFound(new
                {
                    message = "Event not found"
                });
            }

            if (calendarEvent.IsSystemEvent)
            {
                return Results.BadRequest(new
                {
                    message = "System event cannot be deleted"
                });
            }

            bool canDelete =
                calendarEvent.CreatedByUserId == user.Id ||
                user.Type == UserType.Admin || user.Role == UserPost.President;

            if (!canDelete)
            {
                return Results.Forbid();
            }

            db.CalendarEvents.Remove(calendarEvent);

            await db.SaveChangesAsync();

            return Results.Ok(new
            {
                message = "Event deleted",
                eventId = id
            });
        })
        .RequireAuthorization();

        app.MapPost("/api/admin/calendar/sync-birthdays", async (
            HttpRequest httpRequest,
            IConfiguration configuration,
            AppDbContext db) =>
        {
            if (!AdminKeyValidator.IsValid(httpRequest, configuration))
            {
                return Results.Unauthorized();
            }

            var users = await db.Users
                .Where(x =>
                    x.Type != UserType.Guest &&
                    x.BirthDate != null)
                .ToListAsync();

            int createdCount = 0;
            int skippedCount = 0;

            foreach (var user in users)
            {
                if (user.BirthDate == null)
                {
                    skippedCount++;
                    continue;
                }

                var nextBirthdayDate = GetNextBirthdayDate(user.BirthDate.Value);

                bool alreadyExists = await db.CalendarEvents.AnyAsync(x =>
                    x.Type == CalendarEventType.Birthday &&
                    x.CreatedByUserId == user.Id &&
                    x.Date == nextBirthdayDate);

                if (alreadyExists)
                {
                    skippedCount++;
                    continue;
                }

                var fullName = $"{user.MiddleName} {user.FirstName} {user.LastName}";

                var birthdayEvent = new CalendarEvent
                {
                    Type = CalendarEventType.Birthday,
                    Title = $"{user.FirstName}",
                    Description = $"У {fullName} сегодня день рождения! Дата рождения: {user.BirthDate.Value:yyyy-MM-dd}",
                    Date = nextBirthdayDate,
                    CreatedByUserId = user.Id,
                    IsSystemEvent = true,
                    CreatedAt = DateTime.UtcNow
                };

                db.CalendarEvents.Add(birthdayEvent);

                createdCount++;
            }

            await db.SaveChangesAsync();

            return Results.Ok(new
            {
                message = "Birthday sync completed",
                created = createdCount,
                skipped = skippedCount
            });
        });

        app.MapPost("/api/admin/calendar/refresh-birthdays", async (
            HttpRequest httpRequest,
            IConfiguration configuration,
            BirthdayCalendarService birthdayService) =>
        {
            if (!AdminKeyValidator.IsValid(httpRequest, configuration))
            {
                return Results.Unauthorized();
            }

            var result = await birthdayService.RefreshExpiredBirthdaysAsync();

            return Results.Ok(new
            {
                message = "Expired birthday events refreshed",
                removed = result.Removed,
                created = result.Created
            });
        });
    }

    private static bool CanCreateEvent(List<UserRole> roles, CalendarEventType eventType)
    {
        if (roles == null || roles.Count == 0) return false;

        foreach (var role in roles)
        {
            if (role.Post == UserPost.President)
            {
                return true;
            }

            if (role.Post == UserPost.Minister && eventType == CalendarEventType.Meeting)
            {
                return true;
            }
        }

        return false;
    }

    private static DateOnly GetNextBirthdayDate(DateOnly birthDate)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var birthdayThisYear = new DateOnly(
            today.Year,
            birthDate.Month,
            birthDate.Day);

        if (birthdayThisYear >= today)
        {
            return birthdayThisYear;
        }

        return new DateOnly(
            today.Year + 1,
            birthDate.Month,
            birthDate.Day);
    }
}