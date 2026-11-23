using Microsoft.EntityFrameworkCore;
using PushPelmesh.Api.Data;
using PushPelmesh.Api.Security;

namespace PushPelmesh.Api.Endpoints;

public static class AdminUserEndpoints
{
    public static void MapAdminUserEndpoints(this WebApplication app)
    {
        app.MapGet("/api/admin/access", (HttpContext context, IConfiguration configuration) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            return AdminKeyValidator.IsValid(context.Request, configuration)
                ? Results.NoContent()
                : Results.Unauthorized();
        });

        app.MapGet("/api/admin/users", async (
            HttpRequest httpRequest,
            IConfiguration configuration,
            AppDbContext db) =>
        {
            if (!AdminKeyValidator.IsValid(httpRequest, configuration))
            {
                return Results.Unauthorized();
            }

            var users = await db.Users
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => new
                {
                    id = x.Id,
                    series = x.UserSeries,
                    number = x.UserNumber,
                    type = x.Type.ToString(),

                    firstName = x.FirstName,
                    middleName = x.MiddleName,
                    lastName = x.LastName,
                    birthDate = x.BirthDate,
                    weightKg = x.WeightKg,

                    createdAt = x.CreatedAt,
                    lastLoginAt = x.LastLoginAt
                })
                .ToListAsync();

            return Results.Ok(users);
        });
    }
}
