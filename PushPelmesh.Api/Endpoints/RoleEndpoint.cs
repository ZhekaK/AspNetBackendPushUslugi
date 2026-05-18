using Microsoft.EntityFrameworkCore;
using PushPelmesh.Api.Data;
using PushPelmesh.Api.Models;
using System.Security.Claims;

namespace PushPelmesh.Api.Endpoints
{
    public static class RoleEndpoint
    {
        public static void MapRolesEndpoints(this WebApplication app)
        {
            app.MapGet("/api/user/role", async (
            ClaimsPrincipal claimsUser,
            AppDbContext db) =>
            {
                var userIdText = claimsUser.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                if (string.IsNullOrWhiteSpace(userIdText))
                {
                    return Results.Unauthorized();
                }

                if (!int.TryParse(userIdText, out var userId))
                {
                    return Results.Unauthorized();
                }
                string? series = await db.Users
                    .Where(x => x.Id == userId)
                    .Select(x => x.UserSeries)
                    .FirstOrDefaultAsync();

                string? number = await db.Users
                    .Where(x => x.Id == userId)
                    .Select(x => x.UserNumber)
                    .FirstOrDefaultAsync();

                if (series == null || number == null)
                {
                    return Results.NotFound(new
                    {
                        message = "User not found"
                    });
                }

                var userRoles = await db.UserRoles
                    .Where(x => x.UserSeries == series && x.UserNumbers == number)
                    .OrderByDescending(x => x.StartDate)
                    .ToListAsync();

                if (userRoles == null)
                {
                    return Results.NotFound(new
                    {
                        message = "User not found"
                    });
                }

                return Results.Ok(new
                {
                    roles = userRoles.Select(x => new
                    {
                        number = x.Number,
                        roleType = x.Post.ToString(),
                        postName = x.PostName,
                        givePlace = x.GivePlace,
                        startDate = x.StartDate
                    })
                });
            })
        .RequireAuthorization();
        }
    }
}