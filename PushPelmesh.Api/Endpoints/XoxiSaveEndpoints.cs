using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PushPelmesh.Api.Data;
using PushPelmesh.Api.Models;

namespace PushPelmesh.Api.Endpoints;

public static class XoxiSaveEndpoints
{
    public static void MapXoxiSaveEndpoints(this WebApplication app)
    {
        app.MapGet("/api/xoxi/save", async (
            ClaimsPrincipal claimsUser,
            AppDbContext db) =>
        {
            if (!TryGetCurrentUserId(claimsUser, out var userId))
            {
                return Results.Unauthorized();
            }

            var saveData = await db.XoxiSaves
                .AsNoTracking()
                .Where(x => x.UserId == userId)
                .Select(x => x.SaveData)
                .FirstOrDefaultAsync();

            if (saveData == null)
            {
                return Results.NotFound();
            }

            return Results.Content(saveData, "application/json");
        })
        .RequireAuthorization();

        app.MapPut("/api/xoxi/save", async (
            JsonElement saveData,
            ClaimsPrincipal claimsUser,
            AppDbContext db) =>
        {
            if (!TryGetCurrentUserId(claimsUser, out var userId))
            {
                return Results.Unauthorized();
            }

            if (saveData.ValueKind != JsonValueKind.Object)
            {
                return Results.BadRequest(new
                {
                    message = "Save data must be a JSON object"
                });
            }

            var userExists = await db.Users.AnyAsync(x => x.Id == userId);

            if (!userExists)
            {
                return Results.NotFound(new
                {
                    message = "User not found"
                });
            }

            var now = DateTime.UtcNow;
            var rawSaveData = saveData.GetRawText();

            var existingSave = await db.XoxiSaves
                .FirstOrDefaultAsync(x => x.UserId == userId);

            if (existingSave == null)
            {
                db.XoxiSaves.Add(new XoxiSave
                {
                    UserId = userId,
                    SaveData = rawSaveData,
                    UpdatedAt = now
                });
            }
            else
            {
                existingSave.SaveData = rawSaveData;
                existingSave.UpdatedAt = now;
            }

            await db.SaveChangesAsync();

            return Results.Ok(new
            {
                message = "Save updated",
                updatedAt = now
            });
        })
        .RequireAuthorization();
    }

    private static bool TryGetCurrentUserId(ClaimsPrincipal claimsUser, out int userId)
    {
        var userIdText = claimsUser.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        return int.TryParse(userIdText, out userId);
    }
}
