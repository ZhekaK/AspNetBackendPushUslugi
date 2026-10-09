using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using PushPelmesh.Api.Data;
using PushPelmesh.Api.Dtos;
using PushPelmesh.Api.Models;
using PushPelmesh.Api.Services;

namespace PushPelmesh.Api.Endpoints;

public static class MessengerEndpoints
{
    public static void MapMessengerEndpoints(this WebApplication app)
    {
        app.MapGet("/api/messenger/users", async (ClaimsPrincipal claims, AppDbContext db) =>
        {
            var currentUser = await GetRegisteredUserAsync(claims, db);
            if (currentUser == null)
                return Results.Forbid();

            var users = await db.Users.AsNoTracking()
                .Where(IsRegisteredUser())
                .OrderBy(x => x.LastName).ThenBy(x => x.FirstName).ThenBy(x => x.Id)
                .Select(x => new { x.Id, x.FirstName, x.MiddleName, x.LastName })
                .ToListAsync();

            return Results.Ok(new
            {
                users = users.Select(x => new { id = x.Id, displayName = DisplayName(x.FirstName, x.MiddleName, x.LastName) })
            });
        }).RequireAuthorization();

        app.MapGet("/api/messenger/chats", async (ClaimsPrincipal claims, AppDbContext db) =>
        {
            var currentUser = await GetRegisteredUserAsync(claims, db);
            if (currentUser == null)
                return Results.Forbid();

            var rooms = await db.ChatRooms.AsNoTracking()
                .Where(x => x.Participants.Any(p => p.UserId == currentUser.Id))
                .Include(x => x.Participants).ThenInclude(x => x.User)
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();

            var settings = await db.ChatNotificationSettings.AsNoTracking()
                .Where(x => x.UserId == currentUser.Id)
                .ToDictionaryAsync(x => x.ChatId);
            var unreadChatIds = (await db.ChatMessages.AsNoTracking()
                .Where(x => x.SenderUserId != currentUser.Id)
                .Join(db.ChatNotificationSettings.AsNoTracking()
                        .Where(x => x.UserId == currentUser.Id && !x.Muted),
                    message => message.ChatRoomId ?? 0, setting => setting.ChatId,
                    (message, setting) => new { message, setting })
                .Where(x => x.message.Id > x.setting.LastReadMessageId)
                .Select(x => x.setting.ChatId).Distinct().ToListAsync()).ToHashSet();

            var chats = new List<object>
            {
                new { id = 0, type = "global", title = "Общий чат",
                    participantIds = Array.Empty<int>(),
                    muted = settings.TryGetValue(0, out var globalSetting) && globalSetting.Muted,
                    unread = unreadChatIds.Contains(0) }
            };

            foreach (var room in rooms)
            {
                var title = room.Kind == ChatRoomKind.Direct
                    ? room.Participants.Where(x => x.UserId != currentUser.Id)
                        .Select(x => DisplayName(x.User.FirstName, x.User.MiddleName, x.User.LastName))
                        .FirstOrDefault() ?? "Личный чат"
                    : room.Title;

                chats.Add(new
                {
                    id = room.Id,
                    type = room.Kind == ChatRoomKind.Direct ? "direct" : "group",
                    title,
                    participantIds = room.Participants.Select(x => x.UserId).ToArray(),
                    muted = settings.TryGetValue(room.Id, out var setting) && setting.Muted,
                    unread = unreadChatIds.Contains(room.Id)
                });
            }

            return Results.Ok(new { chats });
        }).RequireAuthorization();

        app.MapPost("/api/messenger/chats/{id:int}/mute", async (
            int id, SetChatMutedRequest request, ClaimsPrincipal claims, AppDbContext db) =>
        {
            var currentUser = await GetRegisteredUserAsync(claims, db);
            if (currentUser == null)
                return Results.Forbid();
            if (!await CanAccessChatAsync(id, currentUser.Id, db))
                return Results.NotFound(new { message = "Chat not found" });

            var setting = await db.ChatNotificationSettings
                .FirstOrDefaultAsync(x => x.UserId == currentUser.Id && x.ChatId == id);
            var latestId = await db.ChatMessages
                .Where(x => x.ChatRoomId == (id == 0 ? null : id))
                .MaxAsync(x => (long?)x.Id) ?? 0;
            if (setting == null)
            {
                setting = new ChatNotificationSetting
                {
                    UserId = currentUser.Id, ChatId = id,
                    LastReadMessageId = latestId
                };
                db.ChatNotificationSettings.Add(setting);
            }
            setting.Muted = request.Muted;
            setting.LastReadMessageId = Math.Max(setting.LastReadMessageId, latestId);
            await db.SaveChangesAsync();
            return Results.Ok(new { muted = setting.Muted });
        }).RequireAuthorization();

        app.MapPost("/api/messenger/chats/{id:int}/read", async (
            int id, MarkChatReadRequest request, ClaimsPrincipal claims, AppDbContext db) =>
        {
            var currentUser = await GetRegisteredUserAsync(claims, db);
            if (currentUser == null)
                return Results.Forbid();
            if (!await CanAccessChatAsync(id, currentUser.Id, db))
                return Results.NotFound(new { message = "Chat not found" });
            if (request.LastMessageId <= 0 || !await db.ChatMessages.AnyAsync(x =>
                    x.Id == request.LastMessageId && x.ChatRoomId == (id == 0 ? null : id)))
                return Results.BadRequest(new { message = "Message not found in chat" });

            var setting = await db.ChatNotificationSettings
                .FirstOrDefaultAsync(x => x.UserId == currentUser.Id && x.ChatId == id);
            if (setting == null)
            {
                setting = new ChatNotificationSetting
                {
                    UserId = currentUser.Id, ChatId = id
                };
                db.ChatNotificationSettings.Add(setting);
            }
            setting.LastReadMessageId = Math.Max(setting.LastReadMessageId, request.LastMessageId);
            await db.SaveChangesAsync();
            return Results.Ok(new { lastReadMessageId = setting.LastReadMessageId });
        }).RequireAuthorization();

        app.MapPost("/api/messenger/chats/direct", async (
            CreateDirectChatRequest request, ClaimsPrincipal claims, AppDbContext db) =>
        {
            var currentUser = await GetRegisteredUserAsync(claims, db);
            if (currentUser == null)
                return Results.Forbid();

            if (request.UserId <= 0 || request.UserId == currentUser.Id)
                return Results.BadRequest(new { message = "Choose another registered user" });

            var otherUser = await db.Users.FirstOrDefaultAsync(x => x.Id == request.UserId &&
                x.Type != UserType.Guest && x.UserSeries != "" && x.UserNumber != "");
            if (otherUser == null)
                return Results.BadRequest(new { message = "Registered user not found" });

            var directKey = $"{Math.Min(currentUser.Id, otherUser.Id)}:{Math.Max(currentUser.Id, otherUser.Id)}";
            var room = await db.ChatRooms.FirstOrDefaultAsync(x => x.DirectKey == directKey);
            if (room == null)
            {
                room = new ChatRoom
                {
                    Kind = ChatRoomKind.Direct,
                    DirectKey = directKey,
                    CreatedByUserId = currentUser.Id,
                    CreatedAt = DateTime.UtcNow,
                    Participants = new List<ChatParticipant>
                    {
                        new() { UserId = currentUser.Id, JoinedAt = DateTime.UtcNow },
                        new() { UserId = otherUser.Id, JoinedAt = DateTime.UtcNow }
                    }
                };
                db.ChatRooms.Add(room);
                try
                {
                    await db.SaveChangesAsync();
                }
                catch (DbUpdateException)
                {
                    // A concurrent request may have created the same pair.
                    room = await db.ChatRooms.AsNoTracking().FirstOrDefaultAsync(x => x.DirectKey == directKey);
                    if (room == null)
                        throw;
                }
            }

            return Results.Ok(new { chat = new
            {
                id = room.Id,
                type = "direct",
                title = DisplayName(otherUser.FirstName, otherUser.MiddleName, otherUser.LastName),
                participantIds = new[] { currentUser.Id, otherUser.Id }
            }});
        }).RequireAuthorization();

        app.MapPost("/api/messenger/chats/group", async (
            CreateGroupChatRequest request, ClaimsPrincipal claims, AppDbContext db) =>
        {
            var currentUser = await GetRegisteredUserAsync(claims, db);
            if (currentUser == null)
                return Results.Forbid();

            var title = request.Title?.Trim() ?? "";
            if (title.Length is < 1 or > 120)
                return Results.BadRequest(new { message = "Chat title must be 1-120 characters" });

            var selectedIds = (request.UserIds ?? new List<int>())
                .Where(x => x > 0 && x != currentUser.Id).Distinct().ToArray();
            if (selectedIds.Length == 0 || selectedIds.Length > 50)
                return Results.BadRequest(new { message = "Choose 1-50 other registered users" });

            var validCount = await db.Users.CountAsync(x => selectedIds.Contains(x.Id) &&
                x.Type != UserType.Guest && x.UserSeries != "" && x.UserNumber != "");
            if (validCount != selectedIds.Length)
                return Results.BadRequest(new { message = "One or more users are unavailable" });

            var now = DateTime.UtcNow;
            var room = new ChatRoom
            {
                Kind = ChatRoomKind.Group,
                Title = title,
                CreatedByUserId = currentUser.Id,
                CreatedAt = now,
                Participants = selectedIds.Append(currentUser.Id)
                    .Select(id => new ChatParticipant { UserId = id, JoinedAt = now }).ToList()
            };
            db.ChatRooms.Add(room);
            await db.SaveChangesAsync();

            return Results.Ok(new { chat = new
            {
                id = room.Id,
                type = "group",
                title = room.Title,
                participantIds = room.Participants.Select(x => x.UserId).ToArray()
            }});
        }).RequireAuthorization();

        app.MapGet("/api/messenger/chats/{id:int}/messages", async (
            int id, long afterId, ClaimsPrincipal claims, AppDbContext db) =>
        {
            var currentUser = await GetRegisteredUserAsync(claims, db);
            if (currentUser == null)
                return Results.Forbid();

            if (!await CanAccessChatAsync(id, currentUser.Id, db))
                return Results.NotFound(new { message = "Chat not found" });

            var query = db.ChatMessages.AsNoTracking()
                .Where(x => x.ChatRoomId == (id == 0 ? null : id));
            var messages = afterId > 0
                ? await query.Where(x => x.Id > afterId).OrderBy(x => x.Id)
                    .Take(100).Include(x => x.Sender).ToListAsync()
                : (await query.OrderByDescending(x => x.Id).Take(50)
                    .Include(x => x.Sender).ToListAsync()).OrderBy(x => x.Id).ToList();

            return Results.Ok(new { messages = messages.Select(x => ToMessage(x, id)) });
        }).RequireAuthorization();

        app.MapPost("/api/messenger/chats/{id:int}/messages", async (
            int id, SendChatMessageRequest request, ClaimsPrincipal claims, AppDbContext db,
            WebPushSenderService pushSender, ILoggerFactory loggerFactory) =>
        {
            var currentUser = await GetRegisteredUserAsync(claims, db);
            if (currentUser == null)
                return Results.Forbid();

            if (!await CanAccessChatAsync(id, currentUser.Id, db))
                return Results.NotFound(new { message = "Chat not found" });

            var text = request.Text?.Trim() ?? "";
            if (text.Length is < 1 or > 2000)
                return Results.BadRequest(new { message = "Message must be 1-2000 characters" });

            var recipientIds = id == 0
                ? await db.Users.AsNoTracking().Where(IsRegisteredUser())
                    .Where(x => x.Id != currentUser.Id).Select(x => x.Id).ToListAsync()
                : await db.ChatParticipants.AsNoTracking()
                    .Where(x => x.ChatRoomId == id && x.UserId != currentUser.Id)
                    .Select(x => x.UserId).ToListAsync();
            var previousId = await db.ChatMessages.AsNoTracking()
                .Where(x => x.ChatRoomId == (id == 0 ? null : id))
                .MaxAsync(x => (long?)x.Id) ?? 0;
            var message = new ChatMessage
            {
                ChatRoomId = id == 0 ? null : id,
                SenderUserId = currentUser.Id,
                Sender = currentUser,
                Text = text,
                CreatedAt = DateTime.UtcNow
            };
            db.ChatMessages.Add(message);
            await db.SaveChangesAsync();

            if (recipientIds.Count > 0)
            {
                // Concurrent first messages can target the same chat. Upsert keeps
                // both sends valid while preserving the first unread position.
                await db.Database.ExecuteSqlInterpolatedAsync($@"
                    INSERT INTO ""ChatNotificationSettings"" (""UserId"", ""ChatId"", ""Muted"", ""LastReadMessageId"")
                    SELECT unnest({recipientIds.ToArray()}), {id}, FALSE, {previousId}
                    ON CONFLICT (""UserId"", ""ChatId"") DO NOTHING");
            }
            var settings = await db.ChatNotificationSettings.AsNoTracking()
                .Where(x => x.ChatId == id && recipientIds.Contains(x.UserId))
                .ToDictionaryAsync(x => x.UserId);

            var notifiedIds = settings.Values.Where(x => !x.Muted).Select(x => x.UserId).ToList();
            if (notifiedIds.Count > 0)
            {
                var subscriptions = await db.PushNotificationSubscriptions
                    .Where(x => x.Platform == PushPlatform.Web && notifiedIds.Contains(x.UserId))
                    .ToListAsync();
                var chatTitle = id == 0 ? "Общий чат" : await db.ChatRooms.AsNoTracking()
                    .Where(x => x.Id == id).Select(x => x.Title).FirstOrDefaultAsync();
                var senderName = DisplayName(currentUser.FirstName, currentUser.MiddleName, currentUser.LastName);
                var title = id == 0 ? chatTitle! : string.IsNullOrWhiteSpace(chatTitle)
                    ? senderName : chatTitle;
                var body = senderName + ": " + (text.Length > 120 ? text[..120] + "…" : text);
                foreach (var subscription in subscriptions)
                {
                    try
                    {
                        // Empty URL lets the service worker open its own scope, including
                        // deployments hosted below the domain root.
                        await pushSender.SendAsync(subscription, title, body, "");
                    }
                    catch (Exception exception)
                    {
                        loggerFactory.CreateLogger("MessengerEndpoints")
                            .LogWarning(exception, "Messenger push failed for subscription {Id}", subscription.Id);
                    }
                }
                await db.SaveChangesAsync();
            }
            return Results.Ok(new { message = ToMessage(message, id) });
        }).RequireAuthorization();
    }

    private static async Task<User?> GetRegisteredUserAsync(ClaimsPrincipal claims, AppDbContext db)
    {
        if (!int.TryParse(claims.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id))
            return null;

        return await db.Users.FirstOrDefaultAsync(x => x.Id == id && x.Type != UserType.Guest &&
            x.UserSeries != "" && x.UserNumber != "");
    }

    private static System.Linq.Expressions.Expression<Func<User, bool>> IsRegisteredUser() =>
        x => x.Type != UserType.Guest && x.UserSeries != "" && x.UserNumber != "";

    private static Task<bool> CanAccessChatAsync(int chatId, int userId, AppDbContext db) =>
        chatId == 0 ? Task.FromResult(true) :
        chatId < 0 ? Task.FromResult(false) :
        db.ChatParticipants.AnyAsync(x => x.ChatRoomId == chatId && x.UserId == userId);

    private static string DisplayName(string first, string? middle, string? last) =>
        string.Join(" ", new[] { last, first, middle }.Where(x => !string.IsNullOrWhiteSpace(x)));

    private static string MessageSenderName(string first, string? middle) =>
        string.Join(" ", new[] { first, middle }.Where(x => !string.IsNullOrWhiteSpace(x)));

    private static object ToMessage(ChatMessage message, int chatId) => new
    {
        id = message.Id,
        chatId,
        senderUserId = message.SenderUserId,
        senderName = MessageSenderName(message.Sender.FirstName, message.Sender.MiddleName),
        text = message.Text,
        createdAt = message.CreatedAt
    };
}
