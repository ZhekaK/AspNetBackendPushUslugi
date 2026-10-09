using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using PushPelmesh.Api.Data;
using PushPelmesh.Api.Dtos;
using PushPelmesh.Api.Models;
using PushPelmesh.Api.Services;

namespace PushPelmesh.Api.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        app.MapPost("/api/auth/login-by-key", async (
            LoginByKeyRequest request,
            AppDbContext db,
            JwtTokenGenerator jwtTokenGenerator) =>
        {
            if (string.IsNullOrWhiteSpace(request.Series))
            {
                return Results.BadRequest(new
                {
                    message = "Series is required"
                });
            }

            if (string.IsNullOrWhiteSpace(request.Number))
            {
                return Results.BadRequest(new
                {
                    message = "Number is required"
                });
            }

            var accessKey = await db.AccessKeys
                .Include(x => x.ActivatedByUser)
                .FirstOrDefaultAsync(x =>
                    x.Series == request.Series &&
                    x.Number == request.Number);

            if (accessKey == null)
            {
                return Results.NotFound(new
                {
                    message = "Access key not found"
                });
            }

            User user;

            if (accessKey.IsActivated && accessKey.ActivatedByUser != null)
            {
                user = accessKey.ActivatedByUser;
                user.LastLoginAt = DateTime.UtcNow;
            }
            else
            {
                user = new User
                {
                    UserSeries = accessKey.Series,
                    UserNumber = accessKey.Number,
                    Type = UserType.KeyUser,
                    FirstName = accessKey.AccountNameFirst,
                    MiddleName = accessKey.AccountNameMiddle,
                    LastName = accessKey.AccountNameLast,
                    BirthDate = accessKey.BirthDate,
                    CreatedAt = DateTime.UtcNow,
                    LastLoginAt = DateTime.UtcNow,
                    Sex = accessKey.Sex,
                    GiveDate = accessKey.GiveDate,
                    GivePlace = accessKey.GivePlace
                };

                db.Users.Add(user);

                accessKey.IsActivated = true;
                accessKey.ActivatedByUser = user;
                accessKey.ActivatedAt = DateTime.UtcNow;
            }

            await db.SaveChangesAsync();

            var token = jwtTokenGenerator.Generate(user);
            var rememberToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            db.RememberedLogins.Add(new RememberedLogin
            {
                UserId = user.Id,
                TokenHash = HashRememberToken(rememberToken),
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();

            return Results.Ok(new
            {
                message = "Login successful",

                token = token,
                rememberToken,

                user = new
                {
                    id = user.Id,
                    type = user.Type.ToString(),
                    displayName = user.FirstName
                },

                accessKey = new
                {
                    series = accessKey.Series,
                    number = accessKey.Number
                }
            });
        });

        app.MapPost("/api/auth/login-by-name", async (
            LoginByNameRequest request,
            AppDbContext db,
            JwtTokenGenerator jwtTokenGenerator) =>
        {
            if (string.IsNullOrWhiteSpace(request.RememberToken) || request.RememberToken.Length != 64)
                return Results.Json(new { message = "Full login required", requiresKeyLogin = true }, statusCode: 401);

            var tokenHash = HashRememberToken(request.RememberToken);
            var rememberedLogin = await db.RememberedLogins
                .Include(x => x.User)
                .FirstOrDefaultAsync(x => x.TokenHash == tokenHash);

            if (rememberedLogin == null || rememberedLogin.User.Type == UserType.Guest ||
                string.IsNullOrWhiteSpace(rememberedLogin.User.UserSeries) ||
                string.IsNullOrWhiteSpace(rememberedLogin.User.UserNumber))
            {
                return Results.Json(new { message = "Full login required", requiresKeyLogin = true }, statusCode: 401);
            }

            var enteredName = request.FirstName?.Trim() ?? "";
            if (!string.Equals(enteredName, rememberedLogin.User.FirstName.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                rememberedLogin.FailedNameAttempts++;
                var attemptsRemaining = Math.Max(0, 3 - rememberedLogin.FailedNameAttempts);
                if (attemptsRemaining == 0)
                    db.RememberedLogins.Remove(rememberedLogin);

                await db.SaveChangesAsync();
                return Results.Json(new
                {
                    message = "Incorrect name",
                    attemptsRemaining,
                    requiresKeyLogin = attemptsRemaining == 0
                }, statusCode: 401);
            }

            rememberedLogin.FailedNameAttempts = 0;
            rememberedLogin.LastUsedAt = DateTime.UtcNow;
            rememberedLogin.User.LastLoginAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            var user = rememberedLogin.User;
            return Results.Ok(new
            {
                message = "Login successful",
                token = jwtTokenGenerator.Generate(user),
                rememberToken = request.RememberToken,
                user = new
                {
                    id = user.Id,
                    type = user.Type.ToString(),
                    displayName = user.FirstName
                }
            });
        });

        app.MapPost("/api/auth/guest", async (
            AppDbContext db,
            JwtTokenGenerator jwtTokenGenerator) =>
        {
            var user = new User
            {
                Type = UserType.Guest,
                FirstName = "Guest_" + Random.Shared.Next(1000, 9999),
                CreatedAt = DateTime.UtcNow,
                LastLoginAt = DateTime.UtcNow
            };

            db.Users.Add(user);
            await db.SaveChangesAsync();

            var token = jwtTokenGenerator.Generate(user);
            return Results.Ok(new
            {
                message = "Guest login successful",

                token = token,

                user = new
                {
                    id = user.Id,
                    type = user.Type.ToString(),
                    displayName = user.FirstName
                }
            });
        });
    }

    private static string HashRememberToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
