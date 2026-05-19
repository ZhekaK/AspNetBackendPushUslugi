using Microsoft.EntityFrameworkCore;
using PushPelmesh.Api.Data;
using PushPelmesh.Api.Security;
using PushPelmesh.Api.Services;

namespace PushPelmesh.Api.Endpoints;

public static class AdminPushEndpoints
{
    public static void MapAdminPushEndpoints(this WebApplication app)
    {
        app.MapPost("/api/admin/push/test", async (
            HttpRequest httpRequest,
            IConfiguration configuration,
            AppDbContext db,
            WebPushSenderService pushSender) =>
        {
            if (!AdminKeyValidator.IsValid(httpRequest, configuration))
            {
                return Results.Unauthorized();
            }

            var subscriptions = await db.PushNotificationSubscriptions
                .Where(x => x.Platform == Models.PushPlatform.Web)
                .ToListAsync();

            int sent = 0;
            int failed = 0;

            foreach (var subscription in subscriptions)
            {
                bool success = await pushSender.SendAsync(
                    subscription,
                    "Пыш-Услуги",
                    "Тестовое push-уведомление",
                    "/");

                if (success)
                    sent++;
                else
                    failed++;
            }

            await db.SaveChangesAsync();

            return Results.Ok(new
            {
                message = "Test push completed",
                sent,
                failed
            });
        });
    }
}