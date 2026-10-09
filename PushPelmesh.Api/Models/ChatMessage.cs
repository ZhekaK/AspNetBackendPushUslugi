namespace PushPelmesh.Api.Models;

// ChatRoomId == null denotes the single global chat for all registered users.
public class ChatMessage
{
    public long Id { get; set; }
    public int? ChatRoomId { get; set; }
    public ChatRoom? ChatRoom { get; set; }
    public int SenderUserId { get; set; }
    public User Sender { get; set; } = null!;
    public string Text { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}
