using Microsoft.EntityFrameworkCore;
using PushPelmesh.Api.Data;
using PushPelmesh.Api.Models;
using PushPelmesh.Api.Security;

namespace PushPelmesh.Api.Endpoints;

public static class AdminCleanupEndpoints
{
    public static void MapAdminCleanupEndpoints(this WebApplication app)
    {
        app.MapDelete("/api/admin/cleanup-guests", async (
            HttpRequest httpRequest,
            IConfiguration configuration,
            AppDbContext db,
            CancellationToken cancellationToken) =>
        {
            if (!AdminKeyValidator.IsValid(httpRequest, configuration))
            {
                return Results.Unauthorized();
            }

            var cutoffDate = DateTime.UtcNow.AddDays(-7);

            var guests = db.Users.Where(x =>
                x.Type == UserType.Guest &&
                (x.LastLoginAt ?? x.CreatedAt) < cutoffDate);

            // Keep guest records that anchor game, vote, or chat history.
            var removableGuests = guests.Where(user =>
                !db.DurakRooms.Any(room => room.CreatedByUserId == user.Id) &&
                !db.ZongRooms.Any(room => room.CreatedByUserId == user.Id) &&
                !db.XoxiRooms.Any(room => room.OwnerUserId == user.Id || room.OpponentUserId == user.Id) &&
                !db.VotePolls.Any(poll => poll.CreatedByUserId == user.Id) &&
                !db.ChatRooms.Any(chat => chat.CreatedByUserId == user.Id) &&
                !db.ChatParticipants.Any(participant => participant.UserId == user.Id) &&
                !db.ChatMessages.Any(message => message.SenderUserId == user.Id) &&
                !db.AccessKeys.Any(key => key.ActivatedByUser != null && key.ActivatedByUser.Id == user.Id) &&
                !db.DurakRoomPlayers.Any(player =>
                    player.UserId == user.Id && player.DurakRoom.Status == DurakRoomStatus.InProgress) &&
                !db.ZongRoomPlayers.Any(player =>
                    player.UserId == user.Id && player.ZongRoom.Status == ZongRoomStatus.InProgress));

            var deleted = await removableGuests.ExecuteDeleteAsync(cancellationToken);
            var skipped = await guests.CountAsync(cancellationToken);

            return Results.Ok(new { deleted, skipped });
        });
    }
}
