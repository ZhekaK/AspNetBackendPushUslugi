using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using PushPelmesh.Api.Data;

namespace PushPelmesh.Api.Endpoints;

public static class ModuleNotificationEndpoints
{
    public static void MapModuleNotificationEndpoints(this WebApplication app)
    {
        app.MapGet("/api/module-notifications/unread", async (
            ClaimsPrincipal claimsUser,
            AppDbContext db) =>
        {
            if (!TryGetUserId(claimsUser, out var userId))
                return Results.Unauthorized();

            var notifications = await db.UserModuleNotifications
                .Where(x => x.UserId == userId && x.ReadAt == null)
                .GroupBy(x => x.ModuleKey)
                .Select(x => new
                {
                    moduleKey = x.Key,
                    count = x.Count(),
                    latestTitle = x.OrderByDescending(n => n.CreatedAt).Select(n => n.Title).FirstOrDefault(),
                    latestCreatedAt = x.Max(n => n.CreatedAt)
                })
                .OrderByDescending(x => x.latestCreatedAt)
                .ToListAsync();

            return Results.Ok(new
            {
                notifications
            });
        })
        .RequireAuthorization();

        app.MapPost("/api/module-notifications/{moduleKey}/read", async (
            string moduleKey,
            ClaimsPrincipal claimsUser,
            AppDbContext db) =>
        {
            if (!TryGetUserId(claimsUser, out var userId))
                return Results.Unauthorized();

            moduleKey = moduleKey.Trim();

            if (string.IsNullOrWhiteSpace(moduleKey))
            {
                return Results.BadRequest(new
                {
                    message = "Module key is required"
                });
            }

            var notifications = await db.UserModuleNotifications
                .Where(x => x.UserId == userId && x.ModuleKey == moduleKey && x.ReadAt == null)
                .ToListAsync();

            var readAt = DateTime.UtcNow;

            foreach (var notification in notifications)
                notification.ReadAt = readAt;

            await db.SaveChangesAsync();

            return Results.Ok(new
            {
                message = "Module notifications marked as read",
                moduleKey,
                read = notifications.Count
            });
        })
        .RequireAuthorization();
    }

    private static bool TryGetUserId(ClaimsPrincipal claimsUser, out int userId)
    {
        var userIdText = claimsUser.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(userIdText, out userId);
    }
}
