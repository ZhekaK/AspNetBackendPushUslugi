using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using PushPelmesh.Api.Data;
using PushPelmesh.Api.Dtos;
using PushPelmesh.Api.Models;

namespace PushPelmesh.Api.Endpoints;

public static class PushEndpoints
{
    public static void MapPushEndpoints(this WebApplication app)
    {
        app.MapPost("/api/push/subscribe", async (
            SubscribePushRequest request,
            ClaimsPrincipal claimsUser,
            AppDbContext db) =>
        {
            var userIdText = claimsUser.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (!int.TryParse(userIdText, out var userId))
            {
                return Results.Unauthorized();
            }

            if (string.IsNullOrWhiteSpace(request.Endpoint) ||
                string.IsNullOrWhiteSpace(request.P256dh) ||
                string.IsNullOrWhiteSpace(request.Auth))
            {
                return Results.BadRequest(new
                {
                    message = "Invalid push subscription"
                });
            }

            var existing = await db.PushNotificationSubscriptions
                .FirstOrDefaultAsync(x =>
                    x.UserId == userId &&
                    x.Endpoint == request.Endpoint);

            if (existing == null)
            {
                existing = new PushNotificationSubscription
                {
                    UserId = userId,
                    Platform = request.Platform,
                    Endpoint = request.Endpoint,
                    P256dh = request.P256dh,
                    Auth = request.Auth,
                    CreatedAt = DateTime.UtcNow
                };

                db.PushNotificationSubscriptions.Add(existing);
            }
            else
            {
                existing.P256dh = request.P256dh;
                existing.Auth = request.Auth;
                existing.Platform = request.Platform;
            }

            await db.SaveChangesAsync();

            return Results.Ok(new
            {
                message = "Push subscription saved"
            });
        })
        .RequireAuthorization();

        app.MapPost("/api/push/unsubscribe", async (
            SubscribePushRequest request,
            ClaimsPrincipal claimsUser,
            AppDbContext db) =>
        {
            var userIdText = claimsUser.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (!int.TryParse(userIdText, out var userId))
            {
                return Results.Unauthorized();
            }

            if (string.IsNullOrWhiteSpace(request.Endpoint))
            {
                return Results.BadRequest(new
                {
                    message = "Endpoint is required"
                });
            }

            var subscriptions = await db.PushNotificationSubscriptions
                .Where(x =>
                    x.UserId == userId &&
                    x.Endpoint == request.Endpoint)
                .ToListAsync();

            db.PushNotificationSubscriptions.RemoveRange(subscriptions);

            await db.SaveChangesAsync();

            return Results.Ok(new
            {
                message = "Push subscription removed",
                deleted = subscriptions.Count
            });
        })
        .RequireAuthorization();
    }
}
