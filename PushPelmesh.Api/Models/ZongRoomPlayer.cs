namespace PushPelmesh.Api.Models;

public class ZongRoomPlayer
{
    public int Id { get; set; }
    public int ZongRoomId { get; set; }
    public ZongRoom ZongRoom { get; set; } = null!;
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public string DisplayName { get; set; } = "";
    public int Score { get; set; }
    public int InitialRoll { get; set; }
    public int TurnOrder { get; set; }
    public DateTime JoinedAt { get; set; }
}