using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using PushPelmesh.Api.Data;
using PushPelmesh.Api.Models;

namespace PushPelmesh.Api.Endpoints;

public static class IdentityPhotoEndpoints
{
    private const int MaxBytes = 512 * 1024;

    public static void MapIdentityPhotoEndpoints(this WebApplication app)
    {
        var photo = app.MapGroup("/api/user/profile/photo").RequireAuthorization();
        photo.MapGet("", async (HttpContext http, AppDbContext db, CancellationToken ct) =>
        {
            var userId = await GetHolderAsync(http.User, db, ct);
            if (userId == null) return Results.Forbid();

            http.Response.Headers.CacheControl = "private, no-store";
            http.Response.Headers.XContentTypeOptions = "nosniff";
            var jpeg = await db.IdentityPhotos.Where(x => x.UserId == userId)
                .Select(x => x.Jpeg).FirstOrDefaultAsync(ct);
            return jpeg == null ? Results.NotFound() : Results.File(jpeg, "image/jpeg");
        });

        photo.MapPut("", async (HttpRequest request, AppDbContext db, CancellationToken ct) =>
        {
            var userId = await GetHolderAsync(request.HttpContext.User, db, ct);
            if (userId == null) return Results.Forbid();
            if (!string.Equals(request.ContentType, "image/jpeg", StringComparison.OrdinalIgnoreCase))
                return Results.StatusCode(StatusCodes.Status415UnsupportedMediaType);
            if (request.ContentLength > MaxBytes)
                return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);

            var buffer = new byte[MaxBytes + 1];
            var length = await request.Body.ReadAtLeastAsync(buffer, buffer.Length, false, ct);
            if (length > MaxBytes)
                return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
            if (length < 5 || buffer[0] != 0xFF || buffer[1] != 0xD8 || buffer[2] != 0xFF ||
                buffer[length - 2] != 0xFF || buffer[length - 1] != 0xD9)
                return Results.BadRequest(new { message = "Нужна фотография в формате JPEG." });

            var jpeg = buffer[..length];
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "IdentityPhotos" ("UserId", "Jpeg") VALUES ({userId.Value}, {jpeg})
                ON CONFLICT ("UserId") DO UPDATE SET "Jpeg" = EXCLUDED."Jpeg"
                """, ct);
            return Results.NoContent();
        });

        photo.MapDelete("", async (ClaimsPrincipal claims, AppDbContext db, CancellationToken ct) =>
        {
            var userId = await GetHolderAsync(claims, db, ct);
            if (userId == null) return Results.Forbid();

            await db.IdentityPhotos.Where(x => x.UserId == userId).ExecuteDeleteAsync(ct);
            return Results.NoContent();
        });
    }

    private static async Task<int?> GetHolderAsync(ClaimsPrincipal claims, AppDbContext db, CancellationToken ct)
    {
        if (!int.TryParse(claims.FindFirstValue(ClaimTypes.NameIdentifier), out var id)) return null;
        return await db.Users.Where(x => x.Id == id && x.Type != UserType.Guest)
            .Select(x => (int?)x.Id).SingleOrDefaultAsync(ct);
    }
}
