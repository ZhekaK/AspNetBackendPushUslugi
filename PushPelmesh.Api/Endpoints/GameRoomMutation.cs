using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PushPelmesh.Api.Data;

namespace PushPelmesh.Api.Endpoints;

internal static class GameRoomMutation
{
    public static RouteHandlerBuilder SerializeRoomChanges(
        this RouteHandlerBuilder route, Func<int, FormattableString> lockQuery)
    {
        return route.AddEndpointFilter(async (context, next) =>
        {
            var http = context.HttpContext;
            var id = int.Parse((string)http.Request.RouteValues["id"]!, CultureInfo.InvariantCulture);
            var db = http.RequestServices.GetRequiredService<AppDbContext>();
            await using var transaction = await db.Database.BeginTransactionAsync(http.RequestAborted);
            var versions = await db.Database.SqlQuery<DateTime>(lockQuery(id)).ToListAsync(http.RequestAborted);

            if (versions.Count > 0 && http.Request.Headers.TryGetValue("X-Game-Version", out var expected) &&
                expected.ToString() != VersionOf(versions[0]))
            {
                return Results.Conflict(new { message = "Game state changed. Refresh and try again." });
            }

            var result = await next(context);
            await transaction.CommitAsync(http.RequestAborted);
            return result;
        });
    }

    public static string VersionOf(DateTime updatedAt) =>
        (updatedAt.Ticks / 10).ToString(CultureInfo.InvariantCulture);
}
