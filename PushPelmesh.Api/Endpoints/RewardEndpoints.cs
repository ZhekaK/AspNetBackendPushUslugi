using Microsoft.EntityFrameworkCore;
using PushPelmesh.Api.Data;
using PushPelmesh.Api.Dtos;
using PushPelmesh.Api.Models;
using PushPelmesh.Api.Security;

namespace PushPelmesh.Api.Endpoints;

public static class RewardEndpoints
{
    public static void MapRewardEndpoints(this WebApplication app)
    {
        app.MapGet("/api/rewards/championships", async (AppDbContext db) =>
        {
            var records = await GetRewardRecords(db, RewardKind.Championship);
            return Results.Ok(new { records });
        })
        .RequireAuthorization();

        app.MapGet("/api/rewards/government-awards", async (AppDbContext db) =>
        {
            var records = await GetRewardRecords(db, RewardKind.GovernmentAward);
            return Results.Ok(new { records });
        })
        .RequireAuthorization();

        app.MapPost("/api/admin/rewards", async (
            CreateRewardRecordRequest request,
            HttpRequest httpRequest,
            IConfiguration configuration,
            AppDbContext db) =>
        {
            if (!AdminKeyValidator.IsValid(httpRequest, configuration))
                return Results.Unauthorized();

            if (!Enum.IsDefined(typeof(RewardKind), request.Kind))
            {
                return Results.BadRequest(new
                {
                    message = "Unknown reward kind"
                });
            }

            if (string.IsNullOrWhiteSpace(request.EventName))
            {
                return Results.BadRequest(new
                {
                    message = "Event name is required"
                });
            }

            User? user = null;
            var userSeries = request.UserSeries?.Trim();
            var userNumbers = request.UserNumbers?.Trim();

            if (!string.IsNullOrWhiteSpace(userSeries) && !string.IsNullOrWhiteSpace(userNumbers))
            {
                user = await db.Users.FirstOrDefaultAsync(x =>
                    x.UserSeries == userSeries &&
                    x.UserNumber == userNumbers);

                if (user == null)
                {
                    return Results.NotFound(new
                    {
                        message = "User not found by series and number"
                    });
                }
            }

            var fullName = user != null
                ? BuildFullName(user)
                : request.FullName?.Trim();

            if (string.IsNullOrWhiteSpace(fullName))
            {
                return Results.BadRequest(new
                {
                    message = "Full name or user identity is required"
                });
            }

            var reward = new RewardRecord
            {
                Kind = request.Kind,
                UserId = user?.Id,
                FullName = fullName,
                EventType = string.IsNullOrWhiteSpace(request.EventType) ? null : request.EventType.Trim(),
                EventName = request.EventName.Trim(),
                Place = string.IsNullOrWhiteSpace(request.Place) ? null : request.Place.Trim(),
                CreatedAt = DateTime.UtcNow
            };

            db.RewardRecords.Add(reward);
            await db.SaveChangesAsync();

            return Results.Ok(ToDto(reward));
        });
    }

    private static Task<List<RewardRecordDto>> GetRewardRecords(AppDbContext db, RewardKind kind)
    {
        return db.RewardRecords
            .Where(x => x.Kind == kind)
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Select(x => new RewardRecordDto
            {
                Id = x.Id,
                Kind = (int)x.Kind,
                KindName = x.Kind.ToString(),
                FullName = x.FullName,
                EventType = x.EventType,
                EventName = x.EventName,
                Place = x.Place,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync();
    }

    private static RewardRecordDto ToDto(RewardRecord reward)
    {
        return new RewardRecordDto
        {
            Id = reward.Id,
            Kind = (int)reward.Kind,
            KindName = reward.Kind.ToString(),
            FullName = reward.FullName,
            EventType = reward.EventType,
            EventName = reward.EventName,
            Place = reward.Place,
            CreatedAt = reward.CreatedAt
        };
    }

    private static string BuildFullName(User user)
    {
        return string.Join(" ", new[]
        {
            user.MiddleName,
            user.FirstName,
            user.LastName
        }.Where(x => !string.IsNullOrWhiteSpace(x)));
    }
}
