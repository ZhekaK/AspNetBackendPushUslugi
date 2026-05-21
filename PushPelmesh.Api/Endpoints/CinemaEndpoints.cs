using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using PushPelmesh.Api.Data;
using PushPelmesh.Api.Dtos;
using PushPelmesh.Api.Models;

namespace PushPelmesh.Api.Endpoints;

public static class CinemaEndpoints
{
    public static void MapCinemaEndpoints(this WebApplication app)
    {
        app.MapGet("/api/cinema/movies", async (AppDbContext db) =>
        {
            var movies = await db.CinemaMovieRatings
                .OrderByDescending(x => x.WatchedAt)
                .ThenByDescending(x => x.Id)
                .Select(x => new
                {
                    id = x.Id,
                    title = x.Title,
                    rating = x.Rating,
                    watchedAt = x.WatchedAt,
                    url = x.Url,
                    createdAt = x.CreatedAt
                })
                .ToListAsync();

            return Results.Ok(new
            {
                movies
            });
        })
        .RequireAuthorization();

        app.MapPost("/api/cinema/movies", async (
            CreateCinemaMovieRequest request,
            ClaimsPrincipal claimsUser,
            AppDbContext db) =>
        {
            if (!TryGetUserId(claimsUser, out var userId))
                return Results.Unauthorized();

            var user = await db.Users.FirstOrDefaultAsync(x => x.Id == userId);

            if (user == null)
            {
                return Results.NotFound(new
                {
                    message = "User not found"
                });
            }

            bool isPresident = await db.UserRoles.AnyAsync(x =>
                x.UserSeries == user.UserSeries &&
                x.UserNumbers == user.UserNumber &&
                x.Post == UserPost.President);

            if (!isPresident)
                return Results.Forbid();

            if (string.IsNullOrWhiteSpace(request.Title))
            {
                return Results.BadRequest(new
                {
                    message = "Title is required"
                });
            }

            if (request.Rating < 0 || request.Rating > 10)
            {
                return Results.BadRequest(new
                {
                    message = "Rating must be between 0 and 10"
                });
            }

            if (request.WatchedAt == default)
            {
                return Results.BadRequest(new
                {
                    message = "Watch date is required"
                });
            }

            var movie = new CinemaMovieRating
            {
                Title = request.Title.Trim(),
                Rating = request.Rating,
                WatchedAt = request.WatchedAt,
                Url = request.Url?.Trim() ?? "",
                CreatedByUserId = user.Id,
                CreatedAt = DateTime.UtcNow
            };

            db.CinemaMovieRatings.Add(movie);
            await db.SaveChangesAsync();

            return Results.Ok(new
            {
                id = movie.Id,
                title = movie.Title,
                rating = movie.Rating,
                watchedAt = movie.WatchedAt,
                url = movie.Url,
                createdAt = movie.CreatedAt
            });
        })
        .RequireAuthorization();
    }

    private static bool TryGetUserId(ClaimsPrincipal claimsUser, out int userId)
    {
        var userIdText = claimsUser.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(userIdText, out userId);
    }
}
