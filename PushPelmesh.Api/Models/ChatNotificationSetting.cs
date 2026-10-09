namespace PushPelmesh.Api.Models;

public class ChatNotificationSetting
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    // The global chat has ID 0; other values identify ChatRooms.
    public int ChatId { get; set; }
    public bool Muted { get; set; }
    public long LastReadMessageId { get; set; }
}
