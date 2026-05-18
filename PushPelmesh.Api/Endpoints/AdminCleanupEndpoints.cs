using PushPelmesh.Api.Data;
using PushPelmesh.Api.Models;
using PushPelmesh.Api.Security;

namespace PushPelmesh.Api.Endpoints;

public static class AdminCleanupEndpoints
{
    public static void MapAdminCleanupEndpoints(this WebApplication app)
    {
        app.MapDelete("/api/admin/cleanup-guests", async (
            HttpRequest httpRequest,
            IConfiguration configuration,
            AppDbContext db) =>
        {
            if (!AdminKeyValidator.IsValid(httpRequest, configuration))
            {
                return Results.Unauthorized();
            }

            var cutoffDate = DateTime.UtcNow.AddDays(-7);

            var guests = db.Users.Where(x =>
                x.Type == UserType.Guest &&
                x.LastLoginAt < cutoffDate);

            db.Users.RemoveRange(guests);

            var deletedCount = await db.SaveChangesAsync();

            return Results.Ok(new
            {
                deleted = deletedCount
            });
        });
    }
}
