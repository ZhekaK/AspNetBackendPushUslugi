using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using PushPelmesh.Api.Data;
using PushPelmesh.Api.Dtos;

namespace PushPelmesh.Api.Endpoints;

public static class UserEndpoints
{
    public static void MapUserEndpoints(this WebApplication app)
    {
        app.MapGet("/api/user/me", (ClaimsPrincipal user) =>
        {
            var id = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var name = user.FindFirst(ClaimTypes.Name)?.Value;
            var role = user.FindFirst(ClaimTypes.Role)?.Value;

            return Results.Ok(new
            {
                id,
                name,
                role
            });
        })
        .RequireAuthorization();

        app.MapGet("/api/user/profile", async (
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

            var user = await db.Users
                .FirstOrDefaultAsync(x => x.Id == userId);

            if (user == null)
            {
                return Results.NotFound(new
                {
                    message = "User not found"
                });
            }

            return Results.Ok(new
            {
                id = user.Id,
                type = user.Type.ToString(),

                firstName = user.FirstName,
                middleName = user.MiddleName,
                lastName = user.LastName,
                birthDate = user.BirthDate,

                sex = user.Sex.ToString(),
                giveDate = user.GiveDate,
                givePlace = user.GivePlace,

                createdAt = user.CreatedAt,
                lastLoginAt = user.LastLoginAt,

                weightKg = user.WeightKg,

                series = user.UserSeries,
                number = user.UserNumber
            });
        })
        .RequireAuthorization();

        app.MapPut("/api/user/profile/weight", async (
            UpdateWeightRequest request,
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

            if (request.WeightKg <= 0 || request.WeightKg > 500)
            {
                return Results.BadRequest(new
                {
                    message = "Weight must be between 1 and 500 kg"
                });
            }

            var user = await db.Users.FirstOrDefaultAsync(x => x.Id == userId);

            if (user == null)
            {
                return Results.NotFound(new
                {
                    message = "User not found"
                });
            }

            user.WeightKg = request.WeightKg;

            await db.SaveChangesAsync();

            return Results.Ok(new
            {
                message = "Weight updated",
                weightKg = user.WeightKg
            });
        })
        .RequireAuthorization();
    }
}