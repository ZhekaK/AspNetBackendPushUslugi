using System.Security.Claims;
using System.Security.Cryptography;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PushPelmesh.Api.Data;
using PushPelmesh.Api.Models;
using PushPelmesh.Api.Security;

namespace PushPelmesh.Api.Endpoints;

public static class CandidateEndpoints
{
    public static void MapCandidateEndpoints(this WebApplication app)
    {
        var admin = app.MapGroup("/api/admin/candidates").AddEndpointFilter((context, next) =>
        {
            var http = context.HttpContext;
            http.Response.Headers.CacheControl = "no-store";
            var configuration = http.RequestServices.GetRequiredService<IConfiguration>();
            return AdminKeyValidator.IsValid(http.Request, configuration)
                ? next(context)
                : ValueTask.FromResult<object?>(Results.Unauthorized());
        });

        admin.MapGet("", async (AppDbContext db, CancellationToken ct) =>
            await db.AccessKeys.AsNoTracking().Where(key => key.IsCandidate)
                .OrderByDescending(key => key.Id)
                .Select(key => new
                {
                    key.Series, key.Number, key.IsActivated,
                    FirstName = key.AccountNameFirst, LastName = key.AccountNameLast, MiddleName = key.AccountNameMiddle,
                    Sex = key.Sex.ToString(), key.GiveDate, key.GivePlace
                }).ToListAsync(ct));
        admin.MapPost("", IssueCandidate);

        var candidates = app.MapGroup("/api/candidates").RequireAuthorization().AddEndpointFilter((context, next) =>
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            return next(context);
        });

        candidates.MapGet("", async (ClaimsPrincipal claims, AppDbContext db, CancellationToken ct) =>
        {
            if (await FindCitizen(claims, db, ct) == null) return Results.Forbid();
            var records = await db.AccessKeys.AsNoTracking().Where(key => key.IsCandidate)
                .OrderBy(key => key.AccountNameLast).ThenBy(key => key.AccountNameFirst).ThenBy(key => key.AccountNameMiddle)
                .Select(key => new
                {
                    FirstName = key.AccountNameFirst, LastName = key.AccountNameLast, MiddleName = key.AccountNameMiddle,
                    Sex = key.Sex.ToString(), key.GiveDate, key.GivePlace
                }).ToListAsync(ct);
            return Results.Ok(records);
        });

        candidates.MapPost("/proposals", async (ProposalRequest request, ClaimsPrincipal claims, AppDbContext db, CancellationToken ct) =>
        {
            var citizen = await FindCitizen(claims, db, ct);
            if (citizen == null) return Results.Forbid();
            var fullName = request.FullName?.Trim();
            var reason = request.Reason?.Trim();
            if (!HasText(fullName, 240) || !HasText(reason, 2000))
                return Results.BadRequest(new { message = "Укажите ФИО (до 240 символов) и причину (до 2000 символов)." });

            db.CandidateProposals.Add(new CandidateProposal
            {
                FullName = fullName!, Reason = reason!, SubmittedByUserId = citizen.Id, CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        var review = candidates.MapGroup("/proposals").AddEndpointFilter(async (context, next) =>
        {
            var http = context.HttpContext;
            var db = http.RequestServices.GetRequiredService<AppDbContext>();
            var citizen = await FindCitizen(http.User, db, http.RequestAborted);
            if (citizen == null || !await db.UserRoles.AnyAsync(role => role.Post == UserPost.President &&
                role.UserSeries == citizen.UserSeries && role.UserNumbers == citizen.UserNumber, http.RequestAborted))
                return Results.Forbid();

            return await next(context);
        });

        review.MapGet("", async (AppDbContext db, CancellationToken ct) =>
            await db.CandidateProposals.AsNoTracking().OrderBy(proposal => proposal.ReviewedAt != null)
                .ThenByDescending(proposal => proposal.Id)
                .Select(proposal => new
                {
                    proposal.Id, proposal.FullName, proposal.Reason, proposal.CreatedAt, proposal.Approved, proposal.ReviewedAt,
                    Author = new { proposal.SubmittedBy.FirstName, proposal.SubmittedBy.MiddleName, proposal.SubmittedBy.LastName }
                }).ToListAsync(ct));
        review.MapGet("/pending", async (AppDbContext db, CancellationToken ct) =>
        {
            var pending = await db.CandidateProposals.Where(proposal => proposal.Approved == null)
                .GroupBy(_ => 1).Select(group => new { Count = group.Count(), LatestId = group.Max(proposal => proposal.Id) })
                .SingleOrDefaultAsync(ct);
            return Results.Ok(pending ?? new { Count = 0, LatestId = 0 });
        });
        review.MapPost("/{id:int}/decision", async (int id, DecisionRequest request, ClaimsPrincipal claims, AppDbContext db, CancellationToken ct) =>
        {
            if (request.Approved is not bool approved)
                return Results.BadRequest(new { message = "Укажите решение по заявке." });

            var reviewer = UserId(claims);
            var now = DateTime.UtcNow;
            var updated = await db.CandidateProposals.Where(proposal => proposal.Id == id && proposal.Approved == null)
                .ExecuteUpdateAsync(setters => setters.SetProperty(proposal => proposal.Approved, approved)
                    .SetProperty(proposal => proposal.ReviewedAt, now)
                    .SetProperty(proposal => proposal.ReviewedByUserId, reviewer), ct);
            if (updated > 0) return Results.NoContent();
            if (!await db.CandidateProposals.AnyAsync(proposal => proposal.Id == id, ct)) return Results.NotFound();
            return Results.Conflict(new { message = "Заявка уже рассмотрена." });
        });
    }

    private static async Task<IResult> IssueCandidate(IssueRequest request, AppDbContext db, CancellationToken ct)
    {
        var series = request.Series?.Trim();
        var firstName = request.FirstName?.Trim();
        var lastName = request.LastName?.Trim();
        var middleName = request.MiddleName?.Trim();
        var givePlace = request.GivePlace?.Trim();
        if (!HasText(series, 16) || !HasText(firstName, 80) || !HasText(lastName, 80) || middleName?.Length > 80 ||
            !HasText(givePlace, 160) || request.GiveDate is not { } giveDate ||
            request.Sex is not { } sex || !Enum.IsDefined(sex))
        {
            return Results.BadRequest(new { message = "Укажите серию, фамилию, имя, пол, дату выдачи и кем выдано удостоверение." });
        }

        var number = await FindCandidateNumber(series!, db, ct);
        if (number == null)
            return Results.Conflict(new { message = "Не удалось подобрать свободный номер. Повторите попытку или укажите другую серию." });

        var now = DateTime.UtcNow;
        var key = new AccessKey
        {
            Series = series!, Number = number, IsCandidate = true,
            AccountNameFirst = firstName!, AccountNameLast = lastName, AccountNameMiddle = middleName,
            Sex = sex, GiveDate = giveDate, GivePlace = givePlace, CreatedAt = now,
            ActivatedByUser = new User
            {
                UserSeries = series!, UserNumber = number, Type = UserType.Candidate,
                FirstName = firstName!, LastName = lastName, MiddleName = middleName,
                Sex = sex, GiveDate = giveDate, GivePlace = givePlace, CreatedAt = now
            }
        };
        db.AccessKeys.Add(key);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException error) when (error.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "IX_AccessKeys_Series_Number" })
        {
            return Results.Conflict(new { message = "Номер уже занят. Повторите выпуск удостоверения." });
        }
        return Results.Ok(new { key.Series, key.Number });
    }

    private static async Task<string?> FindCandidateNumber(string series, AppDbContext db, CancellationToken ct)
    {
        for (var attempt = 0; attempt < 32; attempt++)
        {
            var number = RandomNumberGenerator.GetInt32(10000, 100000).ToString(CultureInfo.InvariantCulture);
            if (!await db.AccessKeys.AnyAsync(key => key.Series == series && key.Number == number, ct) &&
                !await db.Users.AnyAsync(user => user.UserSeries == series && user.UserNumber == number, ct))
                return number;
        }
        return null;
    }

    private static int UserId(ClaimsPrincipal claims) =>
        int.TryParse(claims.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    private static Task<User?> FindCitizen(ClaimsPrincipal claims, AppDbContext db, CancellationToken ct)
    {
        var id = UserId(claims);
        return db.Users.AsNoTracking().SingleOrDefaultAsync(user => user.Id == id &&
            (user.Type == UserType.KeyUser || user.Type == UserType.Admin) && user.UserSeries != "" && user.UserNumber != "", ct);
    }

    private static bool HasText(string? value, int maxLength) =>
        value is { Length: > 0 } && value.Length <= maxLength;

    private sealed record IssueRequest(string? Series, string? LastName, string? FirstName, string? MiddleName,
        Sex? Sex, DateOnly? GiveDate, string? GivePlace);
    private sealed record ProposalRequest(string? FullName, string? Reason);
    private sealed record DecisionRequest(bool? Approved);
}
