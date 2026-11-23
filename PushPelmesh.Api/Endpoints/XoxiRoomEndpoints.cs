using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PushPelmesh.Api.Data;
using PushPelmesh.Api.Models;

namespace PushPelmesh.Api.Endpoints;

public static class XoxiRoomEndpoints
{
    private const int MaxPasswordLength = 128;
    private static readonly PasswordHasher<XoxiRoom> PasswordHasher = new();

    public static void MapXoxiRoomEndpoints(this WebApplication app)
    {
        var rooms = app.MapGroup("/api/xoxi/rooms").RequireAuthorization();
        rooms.MapGet("", async (AppDbContext db, CancellationToken ct) =>
        {
            return await db.XoxiRooms.AsNoTracking()
                .Where(room => room.Winner == null)
                .OrderByDescending(room => room.UpdatedAt).Take(50)
                .Select(room => new { room.Id, room.Name, room.Started, Full = room.OpponentUserId != null, Locked = room.PasswordHash != null })
                .ToListAsync(ct);
        });

        rooms.MapPost("", async (CreateRoom request, ClaimsPrincipal claims, AppDbContext db, CancellationToken ct) =>
        {
            var userId = UserId(claims);
            if (!await db.Users.AnyAsync(user => user.Id == userId, ct)) return Results.Unauthorized();
            var name = request.Name?.Trim();
            if (string.IsNullOrWhiteSpace(name) || name.Length > 80)
                return Results.BadRequest(new { message = "Введите название комнаты до 80 символов." });
            if (request.Password?.Length > MaxPasswordLength)
                return Results.BadRequest(new { message = $"Пароль должен содержать не более {MaxPasswordLength} символов." });

            var room = new XoxiRoom { Name = name, OwnerUserId = userId, UpdatedAt = DateTime.UtcNow };
            if (!string.IsNullOrWhiteSpace(request.Password))
                room.PasswordHash = PasswordHasher.HashPassword(room, request.Password);
            db.XoxiRooms.Add(room);
            await db.SaveChangesAsync(ct);
            return Results.Ok(new { room.Id });
        });

        rooms.MapGet("/{id:int}", async (int id, ClaimsPrincipal claims, AppDbContext db, CancellationToken ct) =>
        {
            var room = await FindRoom(db, id, ct);
            if (room == null) return Results.NotFound();
            var userId = UserId(claims);
            if (!await HasAccess(db, room, userId, ct)) return PasswordRequired(room);
            return Results.Ok(State(room, userId));
        });

        rooms.MapPost("/{id:int}/access", async (int id, RoomAccess request, ClaimsPrincipal claims, AppDbContext db, CancellationToken ct) =>
        {
            var room = await FindRoom(db, id, ct);
            if (room == null) return Results.NotFound();

            var userId = UserId(claims);
            if (await HasAccess(db, room, userId, ct)) return Results.Ok(State(room, userId));
            if (request.Password is not { Length: > 0 and <= MaxPasswordLength } ||
                PasswordHasher.VerifyHashedPassword(room, room.PasswordHash!, request.Password) == PasswordVerificationResult.Failed)
                return Results.BadRequest(new { message = "Invalid room password" });
            if (!await db.Users.AnyAsync(user => user.Id == userId, ct)) return Results.Unauthorized();

            await db.Database.ExecuteSqlInterpolatedAsync(
                $"""INSERT INTO "XoxiRoomAdmissions" ("RoomId", "UserId") VALUES ({id}, {userId}) ON CONFLICT ("RoomId", "UserId") DO NOTHING""", ct);
            return Results.Ok(State(room, userId));
        }).RequireRateLimiting("xoxi-room-password");

        rooms.MapPost("/{id:int}/join", async (int id, ClaimsPrincipal claims, AppDbContext db, CancellationToken ct) =>
        {
            var room = await FindRoom(db, id, ct);
            if (room == null) return Results.NotFound();

            var userId = UserId(claims);
            if (!await HasAccess(db, room, userId, ct)) return PasswordRequired(room);
            if (room.OwnerUserId != userId && room.OpponentUserId == null)
            {
                var opponent = await db.Users.SingleOrDefaultAsync(user => user.Id == userId, ct);
                if (opponent == null) return Results.Unauthorized();
                room.Opponent = opponent;
                room.Touch();
                await db.SaveChangesAsync(ct);
            }
            return Results.Ok(State(room, userId));
        }).SerializeRoomChanges(LockRoom);

        rooms.MapPost("/{id:int}/swap", async (int id, HttpContext http, AppDbContext db, CancellationToken ct) =>
        {
            var room = await FindRoom(db, id, ct);
            if (room == null) return Results.NotFound();
            var userId = UserId(http.User);
            if (CheckLobbyOwner(room, userId, http) is { } error) return error;

            room.OwnerPlaysNoughts = !room.OwnerPlaysNoughts;
            room.Touch();
            await db.SaveChangesAsync(ct);
            return Results.Ok(State(room, userId));
        }).SerializeRoomChanges(LockRoom);

        rooms.MapPost("/{id:int}/start", async (int id, HttpContext http, AppDbContext db, CancellationToken ct) =>
        {
            var room = await FindRoom(db, id, ct);
            if (room == null) return Results.NotFound();
            var userId = UserId(http.User);
            if (CheckLobbyOwner(room, userId, http) is { } error) return error;
            if (room.OpponentUserId == null)
                return Results.Conflict(new { message = "Дождитесь второго игрока." });

            room.Started = true;
            room.Touch();
            await db.SaveChangesAsync(ct);
            return Results.Ok(State(room, userId));
        }).SerializeRoomChanges(LockRoom);

        rooms.MapPost("/{id:int}/move", async (int id, MoveRequest request, HttpContext http, AppDbContext db, CancellationToken ct) =>
        {
            var room = await FindRoom(db, id, ct);
            if (room == null) return Results.NotFound();

            var userId = UserId(http.User);
            var figure = Figure(room, userId);
            if (figure == 0) return Results.Forbid();
            if (!http.Request.Headers.ContainsKey("X-Game-Version"))
                return Results.BadRequest(new { message = "Обновите состояние комнаты." });
            if (room.Turn != figure) return Results.Conflict(new { message = "Сейчас не ваш ход." });
            if (!room.Drop(request.Column)) return Results.BadRequest(new { message = "Столбец недоступен." });

            room.Touch();
            await db.SaveChangesAsync(ct);
            return Results.Ok(State(room, userId));
        }).SerializeRoomChanges(LockRoom);
    }

    private static Task<XoxiRoom?> FindRoom(AppDbContext db, int id, CancellationToken ct) =>
        db.XoxiRooms.Include(x => x.Owner).Include(x => x.Opponent).SingleOrDefaultAsync(x => x.Id == id, ct);

    private static int UserId(ClaimsPrincipal claims) =>
        int.TryParse(claims.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    private static int Figure(XoxiRoom room, int userId) =>
        room.OwnerUserId == userId ? room.OwnerFigure : room.OpponentUserId == userId ? 3 - room.OwnerFigure : 0;

    private static Task<bool> HasAccess(AppDbContext db, XoxiRoom room, int userId, CancellationToken ct) =>
        room.PasswordHash == null || Figure(room, userId) != 0
            ? Task.FromResult(true)
            : db.XoxiRoomAdmissions.AnyAsync(entry => entry.RoomId == room.Id && entry.UserId == userId, ct);

    private static IResult PasswordRequired(XoxiRoom room) =>
        Results.Json(new { code = "xoxi_password_required", room.Name }, statusCode: StatusCodes.Status403Forbidden);

    private static IResult? CheckLobbyOwner(XoxiRoom room, int userId, HttpContext http)
    {
        if (room.OwnerUserId != userId) return Results.Forbid();
        if (!http.Request.Headers.ContainsKey("X-Game-Version"))
            return Results.BadRequest(new { message = "Обновите состояние комнаты." });
        if (room.Started) return Results.Conflict(new { message = "Игра уже началась." });
        return null;
    }

    private static FormattableString LockRoom(int id) =>
        $"""SELECT "UpdatedAt" FROM "XoxiRooms" WHERE "Id" = {id} FOR UPDATE""";

    private static object State(XoxiRoom room, int userId) => new
    {
        room.Id, room.Name, room.Cells, room.LastCell, room.Winner, room.Turn, room.Started, room.OwnerFigure,
        IsOwner = room.OwnerUserId == userId,
        Owner = room.Owner.FirstName,
        Opponent = room.Opponent?.FirstName,
        Figure = Figure(room, userId),
        Version = GameRoomMutation.VersionOf(room.UpdatedAt)
    };

    private sealed record CreateRoom(string? Name, string? Password);
    private sealed record RoomAccess(string? Password);
    private sealed record MoveRequest(int Column);
}
