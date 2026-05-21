using Microsoft.EntityFrameworkCore;
using PushPelmesh.Api.Data;
using PushPelmesh.Api.Dtos;
using PushPelmesh.Api.Models;
using PushPelmesh.Api.Security;
using PushPelmesh.Api.Services;

namespace PushPelmesh.Api.Endpoints;

public static class AdminPushEndpoints
{
    public static void MapAdminPushEndpoints(this WebApplication app)
    {
        app.MapPost("/api/admin/push/system", async (
            SendSystemPushRequest request,
            HttpRequest httpRequest,
            IConfiguration configuration,
            AppDbContext db,
            WebPushSenderService pushSender) =>
        {
            if (!AdminKeyValidator.IsValid(httpRequest, configuration))
            {
                return Results.Unauthorized();
            }

            if (string.IsNullOrWhiteSpace(request.Title))
            {
                return Results.BadRequest(new
                {
                    message = "Title is required"
                });
            }

            if (string.IsNullOrWhiteSpace(request.Description))
            {
                return Results.BadRequest(new
                {
                    message = "Description is required"
                });
            }

            List<int> userIds;

            if (request.SendToAll)
            {
                userIds = await db.Users
                    .Where(user => user.Type != UserType.Guest)
                    .Select(user => user.Id)
                    .ToListAsync();
            }
            else
            {
                var identities = request.Recipients
                    .Where(x => !string.IsNullOrWhiteSpace(x.UserSeries) && !string.IsNullOrWhiteSpace(x.UserNumbers))
                    .Select(x => new
                    {
                        UserSeries = x.UserSeries!.Trim(),
                        UserNumbers = x.UserNumbers!.Trim()
                    })
                    .Distinct()
                    .ToList();

                if (identities.Count == 0)
                {
                    return Results.BadRequest(new
                    {
                        message = "Recipients are required"
                    });
                }

                var users = await db.Users
                    .Where(user => user.Type != UserType.Guest)
                    .ToListAsync();

                userIds = users
                    .Where(user => identities.Any(identity =>
                        user.UserSeries == identity.UserSeries &&
                        user.UserNumber == identity.UserNumbers))
                    .Select(user => user.Id)
                    .Distinct()
                    .ToList();

                if (userIds.Count == 0)
                {
                    return Results.NotFound(new
                    {
                        message = "Selected users not found"
                    });
                }
            }

            var subscriptions = await db.PushNotificationSubscriptions
                .Where(subscription =>
                    subscription.Platform == PushPlatform.Web &&
                    userIds.Contains(subscription.UserId))
                .ToListAsync();

            int sent = 0;
            int failed = 0;

            foreach (var subscription in subscriptions)
            {
                bool success = await pushSender.SendAsync(
                    subscription,
                    request.Title.Trim(),
                    request.Description.Trim(),
                    "/");

                if (success)
                    sent++;
                else
                    failed++;
            }

            await db.SaveChangesAsync();

            return Results.Ok(new
            {
                message = "System push completed",
                targetUsers = userIds.Count,
                subscriptions = subscriptions.Count,
                sent,
                failed
            });
        });
    }
}
