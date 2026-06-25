using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PushPelmesh.Api.Data;
using PushPelmesh.Api.Dtos.Durak;
using PushPelmesh.Api.Models;

namespace PushPelmesh.Api.Endpoints;

public static class DurakEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static void MapDurakEndpoints(this WebApplication app)
    {
        app.MapGet("/api/durak/rooms", async (ClaimsPrincipal claimsUser, AppDbContext db) =>
        {
            var user = await GetUserAsync(claimsUser, db);

            if (user == null)
                return Results.Unauthorized();

            var rooms = await db.DurakRooms
                .Include(x => x.Players)
                .Where(x => x.Status != DurakRoomStatus.Finished)
                .OrderByDescending(x => x.UpdatedAt)
                .Take(100)
                .ToListAsync();

            return Results.Ok(new DurakRoomsResponse
            {
                Rooms = rooms.Select(ToListItemDto).ToList()
            });
        })
        .RequireAuthorization();

        app.MapPost("/api/durak/rooms", async (
            CreateDurakRoomRequest request,
            ClaimsPrincipal claimsUser,
            AppDbContext db) =>
        {
            var user = await GetUserAsync(claimsUser, db);

            if (user == null)
                return Results.Unauthorized();

            string name = request.Name?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(name))
                return Results.BadRequest(new { message = "Room name is required" });

            int cardCount = NormalizeCardCount(request.CardCount);
            int maxPlayers = Math.Clamp(request.MaxPlayers <= 0 ? 2 : request.MaxPlayers, 2, MaxPlayers(cardCount));
            DateTime now = DateTime.UtcNow;

            var room = new DurakRoom
            {
                Name = name,
                PasswordHash = HashPassword(request.Password),
                MaxPlayers = maxPlayers,
                CardCount = cardCount,
                Status = DurakRoomStatus.Waiting,
                CreatedByUserId = user.Id,
                CreatedAt = now,
                UpdatedAt = now,
                LastMessage = "Ожидание игроков"
            };

            room.Players.Add(new DurakRoomPlayer
            {
                UserId = user.Id,
                DisplayName = BuildFullName(user),
                TurnOrder = 1,
                JoinedAt = now
            });

            db.DurakRooms.Add(room);
            await db.SaveChangesAsync();

            room = (await LoadRoomAsync(db, room.Id))!;
            return Results.Ok(new DurakRoomResponse { Room = ToDto(room, user.Id) });
        })
        .RequireAuthorization();

        app.MapPost("/api/durak/rooms/{id:int}/join", async (
            int id,
            JoinDurakRoomRequest request,
            ClaimsPrincipal claimsUser,
            AppDbContext db) =>
        {
            var user = await GetUserAsync(claimsUser, db);

            if (user == null)
                return Results.Unauthorized();

            var room = await LoadRoomAsync(db, id);

            if (room == null)
                return Results.NotFound(new { message = "Room not found" });

            if (room.Status != DurakRoomStatus.Waiting)
                return Results.BadRequest(new { message = "Game already started" });

            var existing = room.Players.FirstOrDefault(x => x.UserId == user.Id);

            if (existing != null)
                return Results.Ok(new DurakRoomResponse { Room = ToDto(room, user.Id) });

            if (room.Players.Count >= room.MaxPlayers)
                return Results.BadRequest(new { message = "Room is full" });

            if (!PasswordMatches(room, request.Password))
                return Results.BadRequest(new { message = "Invalid room password" });

            room.Players.Add(new DurakRoomPlayer
            {
                UserId = user.Id,
                DisplayName = BuildFullName(user),
                TurnOrder = room.Players.Count + 1,
                JoinedAt = DateTime.UtcNow
            });

            room.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            room = (await LoadRoomAsync(db, id))!;
            return Results.Ok(new DurakRoomResponse { Room = ToDto(room, user.Id) });
        })
        .RequireAuthorization();

        app.MapGet("/api/durak/rooms/{id:int}", async (
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

            return Results.Ok(new DurakRoomResponse { Room = ToDto(room, user.Id) });
        })
        .RequireAuthorization();

        app.MapPost("/api/durak/rooms/{id:int}/start", async (
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

            if (room.Status != DurakRoomStatus.Waiting)
                return Results.BadRequest(new { message = "Game already started" });

            if (room.Players.Count < 2)
                return Results.BadRequest(new { message = "At least two players are required" });

            StartGame(room);
            await db.SaveChangesAsync();

            room = (await LoadRoomAsync(db, id))!;
            return Results.Ok(new DurakRoomResponse { Room = ToDto(room, user.Id) });
        })
        .RequireAuthorization();

        app.MapPost("/api/durak/rooms/{id:int}/attack", async (
            int id,
            DurakCardActionRequest request,
            ClaimsPrincipal claimsUser,
            AppDbContext db) =>
        {
            var result = await RunGameAction(id, claimsUser, db, (room, player) => Attack(room, player, request.CardCode));
            return result;
        })
        .RequireAuthorization();

        app.MapPost("/api/durak/rooms/{id:int}/defend", async (
            int id,
            DurakCardActionRequest request,
            ClaimsPrincipal claimsUser,
            AppDbContext db) =>
        {
            var result = await RunGameAction(id, claimsUser, db, (room, player) => Defend(room, player, request.AttackCardCode, request.CardCode));
            return result;
        })
        .RequireAuthorization();

        app.MapPost("/api/durak/rooms/{id:int}/transfer", async (
            int id,
            DurakCardActionRequest request,
            ClaimsPrincipal claimsUser,
            AppDbContext db) =>
        {
            var result = await RunGameAction(id, claimsUser, db, (room, player) => Transfer(room, player, request.CardCode));
            return result;
        })
        .RequireAuthorization();

        app.MapPost("/api/durak/rooms/{id:int}/take", async (
            int id,
            ClaimsPrincipal claimsUser,
            AppDbContext db) =>
        {
            var result = await RunGameAction(id, claimsUser, db, Take);
            return result;
        })
        .RequireAuthorization();

        app.MapPost("/api/durak/rooms/{id:int}/pass", async (
            int id,
            ClaimsPrincipal claimsUser,
            AppDbContext db) =>
        {
            var result = await RunGameAction(id, claimsUser, db, Pass);
            return result;
        })
        .RequireAuthorization();
    }

    private static async Task<IResult> RunGameAction(int id, ClaimsPrincipal claimsUser, AppDbContext db, Func<DurakRoom, DurakRoomPlayer, IResult?> action)
    {
        var user = await GetUserAsync(claimsUser, db);

        if (user == null)
            return Results.Unauthorized();

        var room = await LoadRoomAsync(db, id);

        if (room == null)
            return Results.NotFound(new { message = "Room not found" });

        if (room.Status != DurakRoomStatus.InProgress)
            return Results.BadRequest(new { message = "Game is not in progress" });

        var player = room.Players.FirstOrDefault(x => x.UserId == user.Id);

        if (player == null)
            return Results.Forbid();

        var validation = action(room, player);

        if (validation != null)
            return validation;

        room.UpdatedAt = DateTime.UtcNow;
        CheckFinish(room);
        await db.SaveChangesAsync();

        room = (await LoadRoomAsync(db, id))!;
        return Results.Ok(new DurakRoomResponse { Room = ToDto(room, user.Id) });
    }

    private static IResult? Attack(DurakRoom room, DurakRoomPlayer player, string cardCode)
    {
        if (room.AttackerPlayerId != player.Id)
            return Results.BadRequest(new { message = "It is not your attack" });

        var table = ReadTable(room);

        var hand = ReadHand(player);
        var card = RemoveCard(hand, cardCode);

        if (card == null)
            return Results.BadRequest(new { message = "Card not found in hand" });

        if (!CanAddAttack(room, table, card))
            return Results.BadRequest(new { message = "This card cannot be added" });

        table.Add(new DurakTableCardState { Attack = card });
        WriteHand(player, hand);
        WriteTable(room, table);
        room.LastMessage = $"{player.DisplayName} атакует {Format(card)}";
        return null;
    }

    private static IResult? Defend(DurakRoom room, DurakRoomPlayer player, string attackCardCode, string defenseCardCode)
    {
        if (room.DefenderPlayerId != player.Id)
            return Results.BadRequest(new { message = "It is not your defense" });

        var table = ReadTable(room);
        var pair = table.FirstOrDefault(x => x.Attack.Code == attackCardCode && x.Defense == null) ?? table.FirstOrDefault(x => x.Defense == null);

        if (pair == null)
            return Results.BadRequest(new { message = "Nothing to defend" });

        var hand = ReadHand(player);
        var defense = RemoveCard(hand, defenseCardCode);

        if (defense == null)
            return Results.BadRequest(new { message = "Card not found in hand" });

        if (!Beats(defense, pair.Attack, room.TrumpSuit))
            return Results.BadRequest(new { message = "Card does not beat attack" });

        pair.Defense = defense;
        WriteHand(player, hand);
        WriteTable(room, table);
        room.LastMessage = $"{player.DisplayName} бьет {Format(pair.Attack)} картой {Format(defense)}";
        return null;
    }

    private static IResult? Transfer(DurakRoom room, DurakRoomPlayer player, string cardCode)
    {
        if (room.DefenderPlayerId != player.Id)
            return Results.BadRequest(new { message = "It is not your defense" });

        var table = ReadTable(room);

        if (table.Count == 0 || table.Any(x => x.Defense != null))
            return Results.BadRequest(new { message = "Only open attack can be transferred" });

        var hand = ReadHand(player);
        var card = RemoveCard(hand, cardCode);

        if (card == null)
            return Results.BadRequest(new { message = "Card not found in hand" });

        if (table.All(x => x.Attack.Rank != card.Rank))
            return Results.BadRequest(new { message = "Transfer requires same rank" });

        var nextDefender = NextActivePlayer(room, player.Id);

        if (nextDefender == null)
            return Results.BadRequest(new { message = "No next defender" });

        if (table.Count + 1 > ReadHand(nextDefender).Count)
            return Results.BadRequest(new { message = "Next defender has too few cards" });

        table.Add(new DurakTableCardState { Attack = card });
        WriteHand(player, hand);
        WriteTable(room, table);
        room.AttackerPlayerId = player.Id;
        room.DefenderPlayerId = nextDefender.Id;
        room.LastMessage = $"{player.DisplayName} переводит на {nextDefender.DisplayName}";
        return null;
    }

    private static IResult? Take(DurakRoom room, DurakRoomPlayer player)
    {
        if (room.DefenderPlayerId != player.Id)
            return Results.BadRequest(new { message = "It is not your defense" });

        var table = ReadTable(room);
        var hand = ReadHand(player);

        foreach (var pair in table)
        {
            hand.Add(pair.Attack);

            if (pair.Defense != null)
                hand.Add(pair.Defense);
        }

        WriteHand(player, hand);
        WriteTable(room, new List<DurakTableCardState>());
        room.LastMessage = $"{player.DisplayName} берет карты";
        FinishBout(room, NextActivePlayer(room, player.Id)?.Id ?? player.Id);
        return null;
    }

    private static IResult? Pass(DurakRoom room, DurakRoomPlayer player)
    {
        if (room.AttackerPlayerId != player.Id)
            return Results.BadRequest(new { message = "It is not your attack" });

        var table = ReadTable(room);

        if (table.Count == 0 || table.Any(x => x.Defense == null))
            return Results.BadRequest(new { message = "Cards are not defended" });

        WriteTable(room, new List<DurakTableCardState>());
        room.LastMessage = "Бито";
        FinishBout(room, room.DefenderPlayerId ?? player.Id);
        return null;
    }

    private static void StartGame(DurakRoom room)
    {
        var deck = CreateDeck(room.CardCount).OrderBy(_ => RandomNumberGenerator.GetInt32(int.MaxValue)).ToList();
        room.TrumpCardCode = deck.Last().Code;
        room.TrumpSuit = deck.Last().Suit;

        foreach (var player in room.Players.OrderBy(x => x.JoinedAt).Select((player, index) => new { player, index }))
        {
            player.player.TurnOrder = player.index + 1;
            WriteHand(player.player, Draw(deck, 6));
            player.player.IsOut = false;
            player.player.HasPassed = false;
        }

        WriteDeck(room, deck);
        WriteTable(room, new List<DurakTableCardState>());
        room.Status = DurakRoomStatus.InProgress;
        room.FirstBout = true;

        var attacker = FindFirstAttacker(room) ?? room.Players.OrderBy(x => x.TurnOrder).First();
        var defender = NextActivePlayer(room, attacker.Id) ?? attacker;
        room.AttackerPlayerId = attacker.Id;
        room.DefenderPlayerId = defender.Id;
        room.LastMessage = $"Игра началась. Ходит {attacker.DisplayName}";
        room.UpdatedAt = DateTime.UtcNow;
    }

    private static void FinishBout(DurakRoom room, int nextAttackerId)
    {
        DrawAfterBout(room);
        MarkOutPlayers(room);
        room.AttackerPlayerId = nextAttackerId;
        room.DefenderPlayerId = NextActivePlayer(room, nextAttackerId)?.Id;
        room.FirstBout = false;
    }

    private static void DrawAfterBout(DurakRoom room)
    {
        var deck = ReadDeck(room);
        var ordered = OrderedFrom(room, room.AttackerPlayerId ?? room.Players.OrderBy(x => x.TurnOrder).First().Id).ToList();

        foreach (var player in ordered)
        {
            var hand = ReadHand(player);

            while (hand.Count < 6 && deck.Count > 0)
            {
                hand.Add(deck[0]);
                deck.RemoveAt(0);
            }

            WriteHand(player, hand);
        }

        WriteDeck(room, deck);
    }

    private static void MarkOutPlayers(DurakRoom room)
    {
        if (ReadDeck(room).Count > 0)
            return;

        foreach (var player in room.Players)
            player.IsOut = ReadHand(player).Count == 0;
    }

    private static void CheckFinish(DurakRoom room)
    {
        MarkOutPlayers(room);
        var active = room.Players.Where(x => !x.IsOut).ToList();

        if (room.Status == DurakRoomStatus.InProgress && ReadDeck(room).Count == 0 && active.Count <= 1)
        {
            room.Status = DurakRoomStatus.Finished;
            room.WinnerPlayerId = room.Players.FirstOrDefault(x => x.IsOut)?.Id;
            room.LastMessage = active.Count == 1 ? $"{active[0].DisplayName} остался дураком" : "Партия завершена";
        }
    }

    private static DurakRoomDto ToDto(DurakRoom room, int userId)
    {
        var myPlayer = room.Players.FirstOrDefault(x => x.UserId == userId);
        var table = ReadTable(room);
        bool allDefended = table.Count > 0 && table.All(x => x.Defense != null);
        bool isAttacker = myPlayer != null && room.AttackerPlayerId == myPlayer.Id;
        bool isDefender = myPlayer != null && room.DefenderPlayerId == myPlayer.Id;

        return new DurakRoomDto
        {
            Id = room.Id,
            Name = room.Name,
            MaxPlayers = room.MaxPlayers,
            CardCount = room.CardCount,
            Status = room.Status.ToString(),
            TrumpSuit = room.TrumpSuit,
            TrumpCardCode = room.TrumpCardCode,
            DeckCount = ReadDeck(room).Count,
            MyPlayerId = myPlayer?.Id ?? 0,
            AttackerPlayerId = room.AttackerPlayerId ?? 0,
            DefenderPlayerId = room.DefenderPlayerId ?? 0,
            IsCreator = room.CreatedByUserId == userId,
            CanStart = room.CreatedByUserId == userId && room.Status == DurakRoomStatus.Waiting && room.Players.Count >= 2,
            CanAttack = room.Status == DurakRoomStatus.InProgress && isAttacker && table.Count < (room.FirstBout ? 5 : 6),
            CanDefend = room.Status == DurakRoomStatus.InProgress && isDefender && table.Any(x => x.Defense == null),
            CanTransfer = room.Status == DurakRoomStatus.InProgress && isDefender && table.Count > 0 && table.All(x => x.Defense == null),
            CanTake = room.Status == DurakRoomStatus.InProgress && isDefender && table.Any(x => x.Defense == null),
            CanPass = room.Status == DurakRoomStatus.InProgress && isAttacker && allDefended,
            Message = room.LastMessage,
            MyHand = myPlayer == null ? new List<DurakCardDto>() : ReadHand(myPlayer).Select(ToDto).ToList(),
            Table = table.Select(x => new DurakTableCardDto
            {
                Attack = ToDto(x.Attack),
                Defense = x.Defense == null ? null : ToDto(x.Defense)
            }).ToList(),
            Players = room.Players
                .OrderBy(x => x.TurnOrder)
                .Select(player => new DurakPlayerDto
                {
                    Id = player.Id,
                    UserId = player.UserId,
                    DisplayName = player.DisplayName,
                    HandCount = ReadHand(player).Count,
                    TurnOrder = player.TurnOrder,
                    IsAttacker = room.AttackerPlayerId == player.Id,
                    IsDefender = room.DefenderPlayerId == player.Id,
                    IsOut = player.IsOut
                })
                .ToList()
        };
    }

    private static DurakRoomListItemDto ToListItemDto(DurakRoom room)
    {
        return new DurakRoomListItemDto
        {
            Id = room.Id,
            Name = room.Name,
            MaxPlayers = room.MaxPlayers,
            PlayerCount = room.Players.Count,
            CardCount = room.CardCount,
            Status = room.Status.ToString(),
            HasPassword = !string.IsNullOrWhiteSpace(room.PasswordHash)
        };
    }

    private static bool CanAddAttack(DurakRoom room, List<DurakTableCardState> table, DurakCardState card)
    {
        int limit = room.FirstBout ? 5 : 6;

        if (table.Count >= limit)
            return false;

        return table.Count == 0 || table.Any(x => x.Attack.Rank == card.Rank || (x.Defense != null && x.Defense.Rank == card.Rank));
    }

    private static bool Beats(DurakCardState defense, DurakCardState attack, string trumpSuit)
    {
        if (defense.Suit == attack.Suit && defense.Value > attack.Value)
            return true;

        return defense.Suit == trumpSuit && attack.Suit != trumpSuit;
    }

    private static DurakCardState? RemoveCard(List<DurakCardState> cards, string code)
    {
        var card = cards.FirstOrDefault(x => string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase));

        if (card != null)
            cards.Remove(card);

        return card;
    }

    private static DurakRoomPlayer? FindFirstAttacker(DurakRoom room)
    {
        return room.Players
            .Select(player => new { Player = player, Trump = ReadHand(player).Where(x => x.Suit == room.TrumpSuit).OrderBy(x => x.Value).FirstOrDefault() })
            .Where(x => x.Trump != null)
            .OrderBy(x => x.Trump!.Value)
            .Select(x => x.Player)
            .FirstOrDefault();
    }

    private static DurakRoomPlayer? NextActivePlayer(DurakRoom room, int fromPlayerId)
    {
        var ordered = room.Players.OrderBy(x => x.TurnOrder).ToList();
        int start = ordered.FindIndex(x => x.Id == fromPlayerId);

        if (start < 0)
            start = 0;

        for (int offset = 1; offset <= ordered.Count; offset++)
        {
            var player = ordered[(start + offset) % ordered.Count];

            if (!player.IsOut)
                return player;
        }

        return null;
    }

    private static IEnumerable<DurakRoomPlayer> OrderedFrom(DurakRoom room, int fromPlayerId)
    {
        var ordered = room.Players.OrderBy(x => x.TurnOrder).ToList();
        int start = ordered.FindIndex(x => x.Id == fromPlayerId);

        if (start < 0)
            start = 0;

        for (int offset = 0; offset < ordered.Count; offset++)
            yield return ordered[(start + offset) % ordered.Count];
    }

    private static List<DurakCardState> Draw(List<DurakCardState> deck, int count)
    {
        var result = deck.Take(count).ToList();
        deck.RemoveRange(0, result.Count);
        return result;
    }

    private static List<DurakCardState> CreateDeck(int cardCount)
    {
        string[] suits = { "Clubs", "Diamonds", "Hearts", "Spades" };
        string[] ranks = cardCount == 24
            ? new[] { "9", "10", "J", "Q", "K", "A" }
            : cardCount == 36
                ? new[] { "6", "7", "8", "9", "10", "J", "Q", "K", "A" }
                : new[] { "2", "3", "4", "5", "6", "7", "8", "9", "10", "J", "Q", "K", "A" };

        var deck = new List<DurakCardState>();

        foreach (string suit in suits)
        {
            for (int i = 0; i < ranks.Length; i++)
            {
                deck.Add(new DurakCardState
                {
                    Code = ranks[i] + suit[0],
                    Rank = ranks[i],
                    Suit = suit,
                    Value = i + 2
                });
            }
        }

        return deck;
    }

    private static int NormalizeCardCount(int value)
    {
        if (value <= 24)
            return 24;

        return value <= 36 ? 36 : 52;
    }

    private static int MaxPlayers(int cardCount)
    {
        return cardCount == 24 ? 4 : 6;
    }

    private static DurakCardDto ToDto(DurakCardState card)
    {
        return new DurakCardDto
        {
            Code = card.Code,
            Rank = card.Rank,
            Suit = card.Suit,
            Value = card.Value
        };
    }

    private static string Format(DurakCardState card)
    {
        return card.Rank + card.Suit[0];
    }

    private static List<DurakCardState> ReadDeck(DurakRoom room) => ReadCards(room.DeckJson);
    private static void WriteDeck(DurakRoom room, List<DurakCardState> cards) => room.DeckJson = Write(cards);
    private static List<DurakTableCardState> ReadTable(DurakRoom room) => Read<List<DurakTableCardState>>(room.TableJson) ?? new List<DurakTableCardState>();
    private static void WriteTable(DurakRoom room, List<DurakTableCardState> table) => room.TableJson = Write(table);
    private static List<DurakCardState> ReadHand(DurakRoomPlayer player) => ReadCards(player.HandJson);
    private static void WriteHand(DurakRoomPlayer player, List<DurakCardState> cards) => player.HandJson = Write(cards);
    private static List<DurakCardState> ReadCards(string json) => Read<List<DurakCardState>>(json) ?? new List<DurakCardState>();
    private static T? Read<T>(string json) => string.IsNullOrWhiteSpace(json) ? default : JsonSerializer.Deserialize<T>(json, JsonOptions);
    private static string Write<T>(T value) => JsonSerializer.Serialize(value, JsonOptions);

    private static async Task<User?> GetUserAsync(ClaimsPrincipal claimsUser, AppDbContext db)
    {
        var userIdText = claimsUser.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!int.TryParse(userIdText, out int userId))
            return null;

        return await db.Users.FirstOrDefaultAsync(x => x.Id == userId);
    }

    private static async Task<DurakRoom?> LoadRoomAsync(AppDbContext db, int roomId)
    {
        return await db.DurakRooms
            .Include(x => x.Players)
            .FirstOrDefaultAsync(x => x.Id == roomId);
    }

    private static string HashPassword(string? password)
    {
        if (string.IsNullOrWhiteSpace(password))
            return "";

        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password.Trim()));
        return Convert.ToHexString(bytes);
    }

    private static bool PasswordMatches(DurakRoom room, string? password)
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

    private sealed class DurakCardState
    {
        public string Code { get; set; } = "";
        public string Rank { get; set; } = "";
        public string Suit { get; set; } = "";
        public int Value { get; set; }
    }

    private sealed class DurakTableCardState
    {
        public DurakCardState Attack { get; set; } = new();
        public DurakCardState? Defense { get; set; }
    }
}
