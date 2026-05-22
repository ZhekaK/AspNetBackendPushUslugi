using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using PushPelmesh.Api.Data;
using PushPelmesh.Api.Dtos.Voting;
using PushPelmesh.Api.Models;
using PushPelmesh.Api.Services;

namespace PushPelmesh.Api.Endpoints;

public static class VoteEndpoints
{
    private const string ModuleKey = "VoteModule";

    public static void MapVoteEndpoints(this WebApplication app)
    {
        app.MapGet("/api/votes/polls", async (
            ClaimsPrincipal claimsUser,
            AppDbContext db) =>
        {
            var context = await GetUserContextAsync(claimsUser, db);

            if (context == null)
                return Results.Unauthorized();

            var polls = await db.VotePolls
                .Include(x => x.Options.OrderBy(option => option.SortOrder))
                    .ThenInclude(option => option.Ballots)
                .Include(x => x.Ballots)
                .OrderBy(x => x.EndDate < GetToday())
                .ThenByDescending(x => x.CreatedAt)
                .ToListAsync();

            var visiblePolls = polls
                .Where(poll => CanAccessPoll(context, poll))
                .Select(poll => ToDto(poll, context))
                .ToList();

            return Results.Ok(new VotePollsResponse
            {
                Polls = visiblePolls
            });
        })
        .RequireAuthorization();

        app.MapGet("/api/votes/polls/{id:int}", async (
            int id,
            ClaimsPrincipal claimsUser,
            AppDbContext db) =>
        {
            var context = await GetUserContextAsync(claimsUser, db);

            if (context == null)
                return Results.Unauthorized();

            var poll = await LoadPollAsync(db, id);

            if (poll == null)
            {
                return Results.NotFound(new
                {
                    message = "Poll not found"
                });
            }

            if (!CanAccessPoll(context, poll))
                return Results.Forbid();

            return Results.Ok(new VotePollResponse
            {
                Poll = ToDto(poll, context)
            });
        })
        .RequireAuthorization();

        app.MapPost("/api/votes/polls", async (
            CreateVotePollRequest request,
            ClaimsPrincipal claimsUser,
            AppDbContext db,
            WebPushSenderService pushSender) =>
        {
            var context = await GetUserContextAsync(claimsUser, db);

            if (context == null)
                return Results.Unauthorized();

            if (string.IsNullOrWhiteSpace(request.Title))
            {
                return Results.BadRequest(new
                {
                    message = "Title is required"
                });
            }

            if (request.EndDate == default)
            {
                return Results.BadRequest(new
                {
                    message = "End date is required"
                });
            }

            if (request.EndDate < GetToday())
            {
                return Results.BadRequest(new
                {
                    message = "End date cannot be in the past"
                });
            }

            var options = NormalizeOptions(request.Options);

            if (options.Count < 2)
            {
                return Results.BadRequest(new
                {
                    message = "At least two options are required"
                });
            }

            var audienceGroups = context.CanChooseAudienceGroups
                ? ParseAudienceGroups(request.AudienceGroups)
                : VoteAudienceGroup.RegularUsers | VoteAudienceGroup.Ministers;

            if (audienceGroups == 0)
                audienceGroups = VoteAudienceGroup.RegularUsers | VoteAudienceGroup.Ministers;

            var poll = new VotePoll
            {
                Title = request.Title.Trim(),
                Description = request.Description?.Trim() ?? "",
                EndDate = request.EndDate,
                AudienceGroups = audienceGroups,
                CreatedByUserId = context.User.Id,
                CreatedByUserName = BuildFullName(context.User),
                CreatedAt = DateTime.UtcNow
            };

            for (int i = 0; i < options.Count; i++)
            {
                poll.Options.Add(new VoteOption
                {
                    Text = options[i],
                    SortOrder = i
                });
            }

            db.VotePolls.Add(poll);
            await db.SaveChangesAsync();

            poll = (await LoadPollAsync(db, poll.Id))!;
            await CreateNotificationsAsync(db, pushSender, poll);

            return Results.Ok(new VotePollResponse
            {
                Poll = ToDto(poll, context)
            });
        })
        .RequireAuthorization();

        app.MapPost("/api/votes/polls/{id:int}/vote", async (
            int id,
            VotePollVoteRequest request,
            ClaimsPrincipal claimsUser,
            AppDbContext db) =>
        {
            var context = await GetUserContextAsync(claimsUser, db);

            if (context == null)
                return Results.Unauthorized();

            var poll = await LoadPollAsync(db, id);

            if (poll == null)
            {
                return Results.NotFound(new
                {
                    message = "Poll not found"
                });
            }

            if (!CanAccessPoll(context, poll))
                return Results.Forbid();

            if (IsClosed(poll))
            {
                return Results.BadRequest(new
                {
                    message = "Poll is closed"
                });
            }

            if (poll.Ballots.Any(x => x.UserId == context.User.Id))
            {
                return Results.BadRequest(new
                {
                    message = "User already voted"
                });
            }

            var option = poll.Options.FirstOrDefault(x => x.Id == request.OptionId);

            if (option == null)
            {
                return Results.BadRequest(new
                {
                    message = "Option not found"
                });
            }

            db.VoteBallots.Add(new VoteBallot
            {
                VotePollId = poll.Id,
                VoteOptionId = option.Id,
                UserId = context.User.Id,
                CreatedAt = DateTime.UtcNow
            });

            await db.SaveChangesAsync();

            poll = (await LoadPollAsync(db, id))!;

            return Results.Ok(new VotePollResponse
            {
                Poll = ToDto(poll, context)
            });
        })
        .RequireAuthorization();
    }

    private static async Task<VotePoll?> LoadPollAsync(AppDbContext db, int id)
    {
        return await db.VotePolls
            .Include(x => x.Options.OrderBy(option => option.SortOrder))
                .ThenInclude(option => option.Ballots)
            .Include(x => x.Ballots)
            .FirstOrDefaultAsync(x => x.Id == id);
    }

    private static VotePollDto ToDto(VotePoll poll, VoteUserContext context)
    {
        int selectedOptionId = poll.Ballots
            .Where(x => x.UserId == context.User.Id)
            .Select(x => x.VoteOptionId)
            .FirstOrDefault();

        int totalVotes = poll.Ballots.Count;

        return new VotePollDto
        {
            Id = poll.Id,
            Title = poll.Title,
            Description = poll.Description,
            EndDate = poll.EndDate,
            CreatedByUser = poll.CreatedByUserName,
            CreatedAt = poll.CreatedAt,
            IsClosed = IsClosed(poll),
            HasVoted = selectedOptionId > 0,
            CanVote = CanAccessPoll(context, poll) && !IsClosed(poll) && selectedOptionId == 0,
            SelectedOptionId = selectedOptionId,
            TotalVotes = totalVotes,
            AudienceGroups = ToAudienceGroupNames(poll.AudienceGroups),
            Options = poll.Options
                .OrderBy(x => x.SortOrder)
                .Select(option =>
                {
                    int votes = option.Ballots.Count;

                    return new VoteOptionDto
                    {
                        Id = option.Id,
                        Text = option.Text,
                        Votes = votes,
                        Percent = totalVotes == 0 ? 0f : (float)votes * 100f / totalVotes,
                        IsSelected = selectedOptionId == option.Id
                    };
                })
                .ToList()
        };
    }

    private static async Task CreateNotificationsAsync(
        AppDbContext db,
        WebPushSenderService pushSender,
        VotePoll poll)
    {
        var users = await db.Users
            .Where(x => x.Type != UserType.Guest)
            .ToListAsync();

        var roles = await db.UserRoles.ToListAsync();
        var targetUsers = users
            .Where(user => CanAccessPoll(new VoteUserContext(user, GetRolesForUser(user, roles)), poll))
            .ToList();

        string sourceKey = $"vote:{poll.Id}";
        DateTime createdAt = DateTime.UtcNow;

        foreach (var user in targetUsers)
        {
            bool exists = await db.UserModuleNotifications.AnyAsync(x =>
                x.UserId == user.Id &&
                x.ModuleKey == ModuleKey &&
                x.SourceKey == sourceKey);

            if (exists)
                continue;

            db.UserModuleNotifications.Add(new UserModuleNotification
            {
                UserId = user.Id,
                ModuleKey = ModuleKey,
                SourceKey = sourceKey,
                Title = poll.Title,
                CreatedAt = createdAt
            });
        }

        await db.SaveChangesAsync();

        var targetUserIds = targetUsers.Select(x => x.Id).ToList();
        var subscriptions = await db.PushNotificationSubscriptions
            .Where(x =>
                x.Platform == PushPlatform.Web &&
                targetUserIds.Contains(x.UserId))
            .ToListAsync();

        foreach (var subscription in subscriptions)
        {
            await pushSender.SendAsync(
                subscription,
                "Новое голосование",
                poll.Title,
                "/");
        }

        await db.SaveChangesAsync();
    }

    private static async Task<VoteUserContext?> GetUserContextAsync(ClaimsPrincipal claimsUser, AppDbContext db)
    {
        var userIdText = claimsUser.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!int.TryParse(userIdText, out var userId))
            return null;

        var user = await db.Users.FirstOrDefaultAsync(x => x.Id == userId);

        if (user == null)
            return null;

        var roles = await db.UserRoles
            .Where(x => x.UserSeries == user.UserSeries && x.UserNumbers == user.UserNumber)
            .ToListAsync();

        return new VoteUserContext(user, roles);
    }

    private static bool CanAccessPoll(VoteUserContext context, VotePoll poll)
    {
        if (context.User.Type == UserType.Admin || context.IsPresidentOrGovernor)
            return true;

        if (context.IsMinister && poll.AudienceGroups.HasFlag(VoteAudienceGroup.Ministers))
            return true;

        if (!context.IsMinister && poll.AudienceGroups.HasFlag(VoteAudienceGroup.RegularUsers))
            return true;

        return false;
    }

    private static bool IsClosed(VotePoll poll)
    {
        return poll.EndDate < GetToday();
    }

    private static DateOnly GetToday()
    {
        return DateOnly.FromDateTime(CalendarNotificationTime.GetMoscowNow().DateTime);
    }

    private static List<string> NormalizeOptions(List<string>? options)
    {
        List<string> result = new();

        if (options == null)
            return result;

        foreach (var option in options)
        {
            string trimmed = option?.Trim() ?? "";

            if (!string.IsNullOrWhiteSpace(trimmed) && !result.Contains(trimmed, StringComparer.OrdinalIgnoreCase))
                result.Add(trimmed);
        }

        return result;
    }

    private static VoteAudienceGroup ParseAudienceGroups(List<string>? groupNames)
    {
        if (groupNames == null || groupNames.Count == 0)
            return VoteAudienceGroup.RegularUsers | VoteAudienceGroup.Ministers;

        VoteAudienceGroup groups = 0;

        foreach (var groupName in groupNames)
        {
            if (string.Equals(groupName, nameof(VoteAudienceGroup.Ministers), StringComparison.OrdinalIgnoreCase))
                groups |= VoteAudienceGroup.Ministers;

            if (string.Equals(groupName, nameof(VoteAudienceGroup.RegularUsers), StringComparison.OrdinalIgnoreCase))
                groups |= VoteAudienceGroup.RegularUsers;
        }

        return groups;
    }

    private static List<string> ToAudienceGroupNames(VoteAudienceGroup groups)
    {
        List<string> result = new();

        if (groups.HasFlag(VoteAudienceGroup.RegularUsers))
            result.Add(nameof(VoteAudienceGroup.RegularUsers));

        if (groups.HasFlag(VoteAudienceGroup.Ministers))
            result.Add(nameof(VoteAudienceGroup.Ministers));

        return result;
    }

    private static List<UserRole> GetRolesForUser(User user, List<UserRole> roles)
    {
        return roles
            .Where(x => x.UserSeries == user.UserSeries && x.UserNumbers == user.UserNumber)
            .ToList();
    }

    private static string BuildFullName(User user)
    {
        return string.Join(" ", new[]
            {
                user.MiddleName,
                user.FirstName,
                user.LastName
            }
            .Where(x => !string.IsNullOrWhiteSpace(x)))
            .Trim();
    }

    private sealed class VoteUserContext
    {
        public VoteUserContext(User user, List<UserRole> roles)
        {
            User = user;
            Roles = roles;
        }

        public User User { get; }
        public List<UserRole> Roles { get; }
        public bool IsMinister => Roles.Any(x => x.Post == UserPost.Minister);
        public bool IsPresidentOrGovernor => Roles.Any(x => x.Post == UserPost.President || x.Post == UserPost.Governor);
        public bool CanChooseAudienceGroups => User.Type == UserType.Admin || IsPresidentOrGovernor;
    }
}