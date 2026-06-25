namespace PushPelmesh.Api.Models;

public class DurakRoomPlayer
{
    public int Id { get; set; }
    public int DurakRoomId { get; set; }
    public DurakRoom DurakRoom { get; set; } = null!;
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public string DisplayName { get; set; } = "";
    public int TurnOrder { get; set; }
    public string HandJson { get; set; } = "[]";
    public bool HasPassed { get; set; }
    public bool IsOut { get; set; }
    public DateTime JoinedAt { get; set; }
}
