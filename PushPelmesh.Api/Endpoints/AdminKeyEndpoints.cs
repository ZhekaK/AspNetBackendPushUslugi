using Microsoft.EntityFrameworkCore;
using PushPelmesh.Api.Data;
using PushPelmesh.Api.Dtos;
using PushPelmesh.Api.Models;
using PushPelmesh.Api.Security;

namespace PushPelmesh.Api.Endpoints;

public static class AdminKeyEndpoints
{
    public static void MapAdminKeyEndpoints(this WebApplication app)
    {
        app.MapGet("/api/keys", async (
            HttpRequest httpRequest,
            IConfiguration configuration,
            AppDbContext db) =>
        {
            if (!AdminKeyValidator.IsValid(httpRequest, configuration))
            {
                return Results.Unauthorized();
            }

            var keys = await db.AccessKeys.ToListAsync();

            return Results.Ok(keys);
        });

        app.MapPost("/api/admin/keys/generate", async (
            CreateAccessKeyRequest request,
            HttpRequest httpRequest,
            IConfiguration configuration,
            AppDbContext db) =>
        {
            if (!AdminKeyValidator.IsValid(httpRequest, configuration))
            {
                return Results.Unauthorized();
            }
            if (string.IsNullOrWhiteSpace(request.Series))
            {
                return Results.BadRequest(new
                {
                    message = "Series is required"
                });
            }

            if (string.IsNullOrWhiteSpace(request.FirstName))
            {
                return Results.BadRequest(new
                {
                    message = "First name is required"
                });
            }

            if (request.BirthDate == null)
            {
                return Results.BadRequest(new
                {
                    message = "BirthDate is required"
                });
            }

            string number;

            do
            {
                number = Random.Shared.Next(100000, 999999).ToString();
            }
            while (await db.AccessKeys.AnyAsync(x =>
                x.Series == request.Series &&
                x.Number == number));

            var accessKey = new AccessKey
            {
                Series = request.Series,
                Number = number,
                AccountNameFirst = request.FirstName,
                AccountNameMiddle = request.MiddleName,
                AccountNameLast = request.LastName,
                BirthDate = request.BirthDate,
                IsActivated = false,
                CreatedAt = DateTime.UtcNow,
                Sex = request.Sex,
                GiveDate = request.GiveDate,
                GivePlace = request.GivePlace
            };

            db.AccessKeys.Add(accessKey);
            await db.SaveChangesAsync();

            return Results.Ok(new
            {
                id = accessKey.Id,
                series = accessKey.Series,
                number = accessKey.Number,
                accountNameFirst = accessKey.AccountNameFirst,
                accountNameMiddle = accessKey.AccountNameMiddle,
                accountNameLast = accessKey.AccountNameLast,
                birthDate = accessKey.BirthDate,
                isActivated = accessKey.IsActivated,
                createdAt = accessKey.CreatedAt,
                giveDate = accessKey.GiveDate,
                givePlace = accessKey.GivePlace,
                sex = accessKey.Sex.ToString()
            });
        });

        app.MapPost("/api/admin/keys/activate-all", async (
            HttpRequest httpRequest,
            IConfiguration configuration,
            AppDbContext db) =>
        {
            if (!AdminKeyValidator.IsValid(httpRequest, configuration))
            {
                return Results.Unauthorized();
            }

            var accessKeys = await db.AccessKeys
                .Include(x => x.ActivatedByUser)
                .OrderBy(x => x.Id)
                .ToListAsync();

            var existingUsers = await db.Users
                .Where(x => x.Type != UserType.Guest)
                .ToListAsync();

            var userByKey = existingUsers
                .GroupBy(x => (x.UserSeries, x.UserNumber))
                .ToDictionary(x => x.Key, x => x.First());

            var now = DateTime.UtcNow;
            var createdCount = 0;
            var linkedCount = 0;
            var skippedCount = 0;

            foreach (var accessKey in accessKeys)
            {
                if (string.IsNullOrWhiteSpace(accessKey.Series) ||
                    string.IsNullOrWhiteSpace(accessKey.Number))
                {
                    skippedCount++;
                    continue;
                }

                if (accessKey.IsActivated && accessKey.ActivatedByUser != null)
                {
                    skippedCount++;
                    continue;
                }

                var key = (accessKey.Series, accessKey.Number);

                if (accessKey.ActivatedByUser != null)
                {
                    accessKey.IsActivated = true;
                    accessKey.ActivatedAt ??= now;
                    userByKey.TryAdd(key, accessKey.ActivatedByUser);
                    linkedCount++;
                    continue;
                }

                if (userByKey.TryGetValue(key, out var existingUser))
                {
                    accessKey.IsActivated = true;
                    accessKey.ActivatedByUser = existingUser;
                    accessKey.ActivatedAt ??= now;
                    linkedCount++;
                    continue;
                }

                var user = CreateUserFromAccessKey(accessKey, now);

                db.Users.Add(user);

                accessKey.IsActivated = true;
                accessKey.ActivatedByUser = user;
                accessKey.ActivatedAt = now;

                userByKey[key] = user;
                createdCount++;
            }

            await db.SaveChangesAsync();

            return Results.Ok(new
            {
                message = "Access keys activation completed",
                created = createdCount,
                linked = linkedCount,
                skipped = skippedCount,
                activated = createdCount + linkedCount,
                total = accessKeys.Count
            });
        });
    }

    private static User CreateUserFromAccessKey(AccessKey accessKey, DateTime now)
    {
        return new User
        {
            UserSeries = accessKey.Series,
            UserNumber = accessKey.Number,
            Type = UserType.KeyUser,
            FirstName = accessKey.AccountNameFirst,
            MiddleName = accessKey.AccountNameMiddle,
            LastName = accessKey.AccountNameLast,
            BirthDate = accessKey.BirthDate,
            CreatedAt = now,
            Sex = accessKey.Sex,
            GiveDate = accessKey.GiveDate,
            GivePlace = accessKey.GivePlace
        };
    }
}
