namespace PushPelmesh.Api.Models;

public enum ChatRoomKind
{
    Direct = 1,
    Group = 2
}

public class ChatRoom
{
    public int Id { get; set; }
    public ChatRoomKind Kind { get; set; }
    public string Title { get; set; } = "";
    public string? DirectKey { get; set; }
    public int CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public ICollection<ChatParticipant> Participants { get; set; } = new List<ChatParticipant>();
}
