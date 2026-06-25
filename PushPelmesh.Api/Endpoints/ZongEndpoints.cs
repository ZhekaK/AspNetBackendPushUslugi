using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using PushPelmesh.Api.Data;
using PushPelmesh.Api.Dtos.Zong;
using PushPelmesh.Api.Models;

namespace PushPelmesh.Api.Endpoints;

public static class ZongEndpoints
{
    public static void MapZongEndpoints(this WebApplication app)
    {
        app.MapGet("/api/zong/rooms", async (ClaimsPrincipal claimsUser, AppDbContext db) =>
        {
            var user = await GetUserAsync(claimsUser, db);

            if (user == null)
                return Results.Unauthorized();

            var rooms = await db.ZongRooms
                .Include(x => x.Players)
                .Where(x => x.Status != ZongRoomStatus.Finished)
                .OrderByDescending(x => x.UpdatedAt)
                .Take(100)
                .ToListAsync();

            return Results.Ok(new ZongRoomsResponse
            {
                Rooms = rooms.Select(ToListItemDto).ToList()
            });
        })
        .RequireAuthorization();

        app.MapPost("/api/zong/rooms", async (
            CreateZongRoomRequest request,
            ClaimsPrincipal claimsUser,
            AppDbContext db) =>
        {
            var user = await GetUserAsync(claimsUser, db);

            if (user == null)
                return Results.Unauthorized();

            string name = request.Name?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(name))
                return Results.BadRequest(new { message = "Room name is required" });

            int maxPlayers = Math.Clamp(request.MaxPlayers <= 0 ? 2 : request.MaxPlayers, 2, 8);
            int targetScore = NormalizeTargetScore(request.TargetScore);
            DateTime now = DateTime.UtcNow;

            var room = new ZongRoom
            {
                Name = name,
                PasswordHash = HashPassword(request.Password),
                MaxPlayers = maxPlayers,
                TargetScore = targetScore,
                Status = ZongRoomStatus.Waiting,
                CreatedByUserId = user.Id,
                CreatedAt = now,
                UpdatedAt = now,
                RemainingDice = 6
            };

            room.Players.Add(new ZongRoomPlayer
            {
                UserId = user.Id,
                DisplayName = BuildFullName(user),
                InitialRoll = RollDie(),
                JoinedAt = now
            });

            db.ZongRooms.Add(room);
            await db.SaveChangesAsync();

            room = (await LoadRoomAsync(db, room.Id))!;
            return Results.Ok(new ZongRoomResponse { Room = ToDto(room, user.Id) });
        })
        .RequireAuthorization();

        app.MapPost("/api/zong/rooms/{id:int}/join", async (
            int id,
            JoinZongRoomRequest request,
            ClaimsPrincipal claimsUser,
            AppDbContext db) =>
        {
            var user = await GetUserAsync(claimsUser, db);

            if (user == null)
                return Results.Unauthorized();

            var room = await LoadRoomAsync(db, id);

            if (room == null)
                return Results.NotFound(new { message = "Room not found" });

            if (room.Status != ZongRoomStatus.Waiting)
                return Results.BadRequest(new { message = "Game already started" });

            var existing = room.Players.FirstOrDefault(x => x.UserId == user.Id);

            if (existing != null)
                return Results.Ok(new ZongRoomResponse { Room = ToDto(room, user.Id) });

            if (room.Players.Count >= room.MaxPlayers)
                return Results.BadRequest(new { message = "Room is full" });

            if (!PasswordMatches(room, request.Password))
                return Results.BadRequest(new { message = "Invalid room password" });

            room.Players.Add(new ZongRoomPlayer
            {
                UserId = user.Id,
                DisplayName = BuildFullName(user),
                InitialRoll = RollDie(),
                JoinedAt = DateTime.UtcNow
            });

            room.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            room = (await LoadRoomAsync(db, id))!;
            return Results.Ok(new ZongRoomResponse { Room = ToDto(room, user.Id) });
        })
        .RequireAuthorization();

        app.MapGet("/api/zong/rooms/{id:int}", async (
            int id,
            ClaimsPrincipal claimsUser,
            AppDbContext db) =>
        {
            var user = await GetUserAsync(claimsUser, db);

            if (user == null)
                return Results.Unauthorized();

            var room = await LoadRoomAsync(db, id);

            if (room == null)
                return Results.NotFound(new { message = "Room not found" });

            if (room.Players.All(x => x.UserId != user.Id))
                return Results.Forbid();

            return Results.Ok(new ZongRoomResponse { Room = ToDto(room, user.Id) });
        })
        .RequireAuthorization();

        app.MapPost("/api/zong/rooms/{id:int}/start", async (
            int id,
            ClaimsPrincipal claimsUser,
            AppDbContext db) =>
        {
            var user = await GetUserAsync(claimsUser, db);

            if (user == null)
                return Results.Unauthorized();

            var room = await LoadRoomAsync(db, id);

            if (room == null)
                return Results.NotFound(new { message = "Room not found" });

            if (room.CreatedByUserId != user.Id)
                return Results.Forbid();

            if (room.Status != ZongRoomStatus.Waiting)
                return Results.BadRequest(new { message = "Game already started" });

            if (room.Players.Count < 2)
                return Results.BadRequest(new { message = "At least two players are required" });

            var orderedPlayers = room.Players
                .OrderByDescending(x => x.InitialRoll)
                .ThenBy(x => x.JoinedAt)
                .ToList();

            for (int i = 0; i < orderedPlayers.Count; i++)
                orderedPlayers[i].TurnOrder = i + 1;

            room.Status = ZongRoomStatus.InProgress;
            room.CurrentTurnPlayerId = orderedPlayers[0].Id;
            room.CurrentTurnScore = 0;
            room.RemainingDice = 6;
            room.LastDice = "";
            room.LastRollScore = 0;
            room.LastRollMessage = "Game started";
            room.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync();

            room = (await LoadRoomAsync(db, id))!;
            return Results.Ok(new ZongRoomResponse { Room = ToDto(room, user.Id) });
        })
        .RequireAuthorization();

        app.MapPost("/api/zong/rooms/{id:int}/roll", async (
            int id,
            ClaimsPrincipal claimsUser,
            AppDbContext db) =>
        {
            var user = await GetUserAsync(claimsUser, db);

            if (user == null)
                return Results.Unauthorized();

            var room = await LoadRoomAsync(db, id);
            var validation = ValidateTurn(room, user.Id);

            if (validation != null)
                return validation;

            int diceCount = room!.RemainingDice <= 0 ? 6 : room.RemainingDice;
            var dice = RollDice(diceCount);
            var score = CalculateScore(dice);

            room.LastDice = string.Join(",", dice);
            room.LastRollScore = score.Score;

            if (score.Score <= 0)
            {
                room.CurrentTurnScore = 0;
                room.RemainingDice = 6;
                room.LastRollMessage = "Зонг. Очки за ход сгорели.";
                AdvanceTurn(room);
            }
            else
            {
                room.CurrentTurnScore += score.Score;
                room.RemainingDice = diceCount - score.UsedDice;

                if (room.RemainingDice <= 0)
                    room.RemainingDice = 6;

                room.LastRollMessage = $"+{score.Score}. Можно продолжить или забрать очки.";
            }

            room.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            room = (await LoadRoomAsync(db, id))!;
            return Results.Ok(new ZongRoomResponse { Room = ToDto(room, user.Id) });
        })
        .RequireAuthorization();

        app.MapPost("/api/zong/rooms/{id:int}/bank", async (
            int id,
            ClaimsPrincipal claimsUser,
            AppDbContext db) =>
        {
            var user = await GetUserAsync(claimsUser, db);

            if (user == null)
                return Results.Unauthorized();

            var room = await LoadRoomAsync(db, id);
            var validation = ValidateTurn(room, user.Id);

            if (validation != null)
                return validation;

            if (room!.CurrentTurnScore <= 0)
                return Results.BadRequest(new { message = "No points to bank" });

            var currentPlayer = room.Players.First(x => x.Id == room.CurrentTurnPlayerId);
            currentPlayer.Score += room.CurrentTurnScore;
            room.LastRollMessage = $"{currentPlayer.DisplayName} забрал {room.CurrentTurnScore} очков.";
            room.CurrentTurnScore = 0;
            room.RemainingDice = 6;

            if (currentPlayer.Score >= room.TargetScore)
            {
                room.Status = ZongRoomStatus.Finished;
                room.WinnerPlayerId = currentPlayer.Id;
                room.CurrentTurnPlayerId = currentPlayer.Id;
                room.LastRollMessage = $"{currentPlayer.DisplayName} победил.";
            }
            else
            {
                AdvanceTurn(room);
            }

            room.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            room = (await LoadRoomAsync(db, id))!;
            return Results.Ok(new ZongRoomResponse { Room = ToDto(room, user.Id) });
        })
        .RequireAuthorization();
    }

    private static async Task<User?> GetUserAsync(ClaimsPrincipal claimsUser, AppDbContext db)
    {
        var userIdText = claimsUser.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!int.TryParse(userIdText, out int userId))
            return null;

        return await db.Users.FirstOrDefaultAsync(x => x.Id == userId);
    }

    private static async Task<ZongRoom?> LoadRoomAsync(AppDbContext db, int roomId)
    {
        return await db.ZongRooms
            .Include(x => x.Players)
            .FirstOrDefaultAsync(x => x.Id == roomId);
    }

    private static IResult? ValidateTurn(ZongRoom? room, int userId)
    {
        if (room == null)
            return Results.NotFound(new { message = "Room not found" });

        if (room.Status != ZongRoomStatus.InProgress)
            return Results.BadRequest(new { message = "Game is not in progress" });

        var myPlayer = room.Players.FirstOrDefault(x => x.UserId == userId);

        if (myPlayer == null)
            return Results.Forbid();

        if (room.CurrentTurnPlayerId != myPlayer.Id)
            return Results.BadRequest(new { message = "It is not your turn" });

        return null;
    }

    private static ZongRoomListItemDto ToListItemDto(ZongRoom room)
    {
        return new ZongRoomListItemDto
        {
            Id = room.Id,
            Name = room.Name,
            MaxPlayers = room.MaxPlayers,
            PlayerCount = room.Players.Count,
            TargetScore = room.TargetScore,
            Status = room.Status.ToString(),
            HasPassword = !string.IsNullOrWhiteSpace(room.PasswordHash)
        };
    }

    private static ZongRoomDto ToDto(ZongRoom room, int userId)
    {
        var myPlayer = room.Players.FirstOrDefault(x => x.UserId == userId);
        bool isMyTurn = myPlayer != null && room.CurrentTurnPlayerId == myPlayer.Id;

        return new ZongRoomDto
        {
            Id = room.Id,
            Name = room.Name,
            MaxPlayers = room.MaxPlayers,
            TargetScore = room.TargetScore,
            Status = room.Status.ToString(),
            CurrentTurnPlayerId = room.CurrentTurnPlayerId ?? 0,
            WinnerPlayerId = room.WinnerPlayerId ?? 0,
            MyPlayerId = myPlayer?.Id ?? 0,
            IsCreator = room.CreatedByUserId == userId,
            IsMyTurn = isMyTurn,
            CanStart = room.CreatedByUserId == userId && room.Status == ZongRoomStatus.Waiting && room.Players.Count >= 2,
            CanRoll = isMyTurn && room.Status == ZongRoomStatus.InProgress,
            CanBank = isMyTurn && room.Status == ZongRoomStatus.InProgress && room.CurrentTurnScore > 0,
            CurrentTurnScore = room.CurrentTurnScore,
            RemainingDice = room.RemainingDice <= 0 ? 6 : room.RemainingDice,
            LastDice = room.LastDice,
            LastRollScore = room.LastRollScore,
            LastRollMessage = room.LastRollMessage,
            Players = room.Players
                .OrderBy(x => x.TurnOrder == 0 ? int.MaxValue : x.TurnOrder)
                .ThenByDescending(x => x.InitialRoll)
                .ThenBy(x => x.JoinedAt)
                .Select(player => new ZongPlayerDto
                {
                    Id = player.Id,
                    UserId = player.UserId,
                    DisplayName = player.DisplayName,
                    Score = player.Score,
                    InitialRoll = player.InitialRoll,
                    TurnOrder = player.TurnOrder,
                    IsCurrentTurn = room.CurrentTurnPlayerId == player.Id,
                    IsWinner = room.WinnerPlayerId == player.Id
                })
                .ToList()
        };
    }

    private static void AdvanceTurn(ZongRoom room)
    {
        var orderedPlayers = room.Players
            .Where(x => x.TurnOrder > 0)
            .OrderBy(x => x.TurnOrder)
            .ToList();

        if (orderedPlayers.Count == 0)
            orderedPlayers = room.Players.OrderBy(x => x.JoinedAt).ToList();

        int currentIndex = orderedPlayers.FindIndex(x => x.Id == room.CurrentTurnPlayerId);
        int nextIndex = currentIndex < 0 ? 0 : (currentIndex + 1) % orderedPlayers.Count;
        room.CurrentTurnPlayerId = orderedPlayers[nextIndex].Id;
        room.CurrentTurnScore = 0;
        room.RemainingDice = 6;
    }

    private static ZongRollScore CalculateScore(List<int> dice)
    {
        if (dice.Count == 0)
            return new ZongRollScore(0, 0);

        int score = 0;
        int used = 0;
        var counts = Enumerable.Range(1, 6).ToDictionary(x => x, x => dice.Count(value => value == x));

        if (dice.Count == 6 && counts.All(x => x.Value == 1))
            return new ZongRollScore(1500, 6);

        if (dice.Count == 6 && counts.Count(x => x.Value == 2) == 3)
            return new ZongRollScore(1500, 6);

        for (int face = 1; face <= 6; face++)
        {
            int count = counts[face];

            if (count < 3)
                continue;

            int baseScore = face == 1 ? 1000 : face * 100;
            int multiplier = count switch
            {
                3 => 1,
                4 => 2,
                5 => 4,
                _ => 8
            };

            score += baseScore * multiplier;
            used += count;
            counts[face] = 0;
        }

        if (counts[1] > 0)
        {
            score += counts[1] * 100;
            used += counts[1];
        }

        if (counts[5] > 0)
        {
            score += counts[5] * 50;
            used += counts[5];
        }

        return new ZongRollScore(score, used);
    }

    private static List<int> RollDice(int count)
    {
        List<int> dice = new();

        for (int i = 0; i < count; i++)
            dice.Add(RollDie());

        return dice;
    }

    private static int RollDie()
    {
        return RandomNumberGenerator.GetInt32(1, 7);
    }

    private static int NormalizeTargetScore(int targetScore)
    {
        if (targetScore <= 0)
            return 5000;

        return Math.Clamp(targetScore, 1000, 50000);
    }

    private static string HashPassword(string? password)
    {
        if (string.IsNullOrWhiteSpace(password))
            return "";

        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password.Trim()));
        return Convert.ToHexString(bytes);
    }

    private static bool PasswordMatches(ZongRoom room, string? password)
    {
        if (string.IsNullOrWhiteSpace(room.PasswordHash))
            return true;

        return string.Equals(room.PasswordHash, HashPassword(password), StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildFullName(User user)
    {
        string fullName = string.Join(" ", new[]
            {
                user.MiddleName,
                user.FirstName,
                user.LastName
            }
            .Where(x => !string.IsNullOrWhiteSpace(x)))
            .Trim();

        return string.IsNullOrWhiteSpace(fullName) ? $"User {user.Id}" : fullName;
    }

    private sealed record ZongRollScore(int Score, int UsedDice);
}