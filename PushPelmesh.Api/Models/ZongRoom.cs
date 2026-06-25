namespace PushPelmesh.Api.Models;

public enum ZongRoomStatus
{
    Waiting = 0,
    InProgress = 1,
    Finished = 2
}

public class ZongRoom
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public int MaxPlayers { get; set; }
    public int TargetScore { get; set; }
    public ZongRoomStatus Status { get; set; }
    public int CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; } = null!;
    public int? CurrentTurnPlayerId { get; set; }
    public ZongRoomPlayer? CurrentTurnPlayer { get; set; }
    public int? WinnerPlayerId { get; set; }
    public ZongRoomPlayer? WinnerPlayer { get; set; }
    public int CurrentTurnScore { get; set; }
    public int RemainingDice { get; set; } = 6;
    public string LastDice { get; set; } = "";
    public int LastRollScore { get; set; }
    public string LastRollMessage { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<ZongRoomPlayer> Players { get; set; } = new();
}