using Microsoft.EntityFrameworkCore;
using PushPelmesh.Api.Data;
using PushPelmesh.Api.Dtos;
using PushPelmesh.Api.Models;
using PushPelmesh.Api.Security;

namespace PushPelmesh.Api.Endpoints
{
    public static class AdminRolesEndpoint
    {
        public static void MapAdminRolesEndpoints(this WebApplication app)
        {
            app.MapGet("/api/admin/roles", async (
                string? userSeries,
                string? userNumbers,
                HttpRequest httpRequest,
                IConfiguration configuration,
                AppDbContext db) =>
            {
                if (!AdminKeyValidator.IsValid(httpRequest, configuration))
                {
                    return Results.Unauthorized();
                }

                userSeries = userSeries?.Trim();
                userNumbers = userNumbers?.Trim();

                if (string.IsNullOrWhiteSpace(userSeries) != string.IsNullOrWhiteSpace(userNumbers))
                {
                    return Results.BadRequest(new
                    {
                        message = "Pass both userSeries and userNumbers, or leave both empty"
                    });
                }

                var rolesQuery = db.UserRoles.AsNoTracking();

                if (!string.IsNullOrWhiteSpace(userSeries) && !string.IsNullOrWhiteSpace(userNumbers))
                {
                    rolesQuery = rolesQuery.Where(x =>
                        x.UserSeries == userSeries &&
                        x.UserNumbers == userNumbers);
                }

                var roles = await rolesQuery
                    .OrderByDescending(x => x.StartDate)
                    .ThenByDescending(x => x.Id)
                    .Select(x => new
                    {
                        id = x.Id,
                        number = x.Number,
                        post = x.Post.ToString(),
                        postValue = (int)x.Post,
                        postName = x.PostName,
                        givePlace = x.GivePlace,
                        startDate = x.StartDate,
                        userSeries = x.UserSeries,
                        userNumbers = x.UserNumbers
                    })
                    .ToListAsync();

                return Results.Ok(roles);
            });

            app.MapPost("/api/admin/roles/generate", async (
                CreateUserRoleRequest request,
                HttpRequest httpRequest,
                IConfiguration configuration,
                AppDbContext db) =>
            {
                if (!AdminKeyValidator.IsValid(httpRequest, configuration))
                {
                    return Results.Unauthorized();
                }

                var userSeries = request.UserSeries.Trim();
                var userNumbers = request.UserNumbers.Trim();

                if (string.IsNullOrWhiteSpace(userSeries) || string.IsNullOrWhiteSpace(userNumbers))
                {
                    return Results.BadRequest(new
                    {
                        message = "User series and numbers are required"
                    });
                }

                if (!Enum.IsDefined(request.Post))
                {
                    return Results.BadRequest(new
                    {
                        message = "Unknown user post"
                    });
                }

                if (request.StartDate == default)
                {
                    return Results.BadRequest(new
                    {
                        message = "Start date is required"
                    });
                }

                var user = await db.Users
                    .FirstOrDefaultAsync(x =>
                        x.UserSeries == userSeries &&
                        x.UserNumber == userNumbers);

                if (user == null)
                {
                    return Results.NotFound(new
                    {
                        message = "User not found by series and number"
                    });
                }

                string number;

                do
                {
                    number = Random.Shared.Next(1000000, 9999999).ToString();
                }
                while (await db.UserRoles.AnyAsync(x => x.Number == number));

                var userRole = new UserRole
                {
                    Number = number,
                    Post = request.Post,
                    PostName = string.IsNullOrWhiteSpace(request.PostName)
                        ? request.Post.ToString()
                        : request.PostName.Trim(),
                    GivePlace = string.IsNullOrWhiteSpace(request.GivePlace)
                        ? null
                        : request.GivePlace.Trim(),
                    StartDate = request.StartDate,
                    UserSeries = user.UserSeries,
                    UserNumbers = user.UserNumber
                };

                db.UserRoles.Add(userRole);
                await db.SaveChangesAsync();

                return Results.Ok(new
                {
                    id = userRole.Id,
                    number = userRole.Number,
                    post = userRole.Post.ToString(),
                    postName = userRole.PostName,
                    givePlace = userRole.GivePlace,
                    startDate = userRole.StartDate,

                    userSeries = userRole.UserSeries,
                    userNumbers = userRole.UserNumbers,
                    user = new
                    {
                        id = user.Id,
                        firstName = user.FirstName,
                        middleName = user.MiddleName,
                        lastName = user.LastName
                    }
                });
            });

            app.MapPost("/api/admin/roles/remove", async (
                CreateUserRoleRequest request,
                HttpRequest httpRequest,
                IConfiguration configuration,
                AppDbContext db) =>
            {
                if (!AdminKeyValidator.IsValid(httpRequest, configuration))
                {
                    return Results.Unauthorized();
                }

                var userSeries = request.UserSeries.Trim();
                var userNumbers = request.UserNumbers.Trim();

                if (string.IsNullOrWhiteSpace(userSeries) || string.IsNullOrWhiteSpace(userNumbers))
                {
                    return Results.BadRequest(new
                    {
                        message = "User series and numbers are required"
                    });
                }

                var userRoles = await db.UserRoles
                    .Where(x => x.UserSeries == userSeries && x.UserNumbers == userNumbers)
                    .ToListAsync();

                if (userRoles.Count == 0)
                {
                    return Results.Ok(new
                    {
                        message = "User roles not found",
                        deleted = 0,
                        userSeries,
                        userNumbers
                    });
                }

                var deleted = userRoles.Count;

                db.UserRoles.RemoveRange(userRoles);
                await db.SaveChangesAsync();

                return Results.Ok(new
                {
                    message = "User roles removed",
                    deleted,

                    userSeries,
                    userNumbers
                });
            });

            app.MapDelete("/api/admin/roles/{id:int}", async (
                int id,
                HttpRequest httpRequest,
                IConfiguration configuration,
                AppDbContext db) =>
            {
                if (!AdminKeyValidator.IsValid(httpRequest, configuration))
                {
                    return Results.Unauthorized();
                }

                var userRole = await db.UserRoles.FirstOrDefaultAsync(x => x.Id == id);

                if (userRole == null)
                {
                    return Results.NotFound(new
                    {
                        message = "User role not found"
                    });
                }

                db.UserRoles.Remove(userRole);
                await db.SaveChangesAsync();

                return Results.Ok(new
                {
                    message = "User role removed",
                    id,
                    userSeries = userRole.UserSeries,
                    userNumbers = userRole.UserNumbers
                });
            });
        }
    }
}

