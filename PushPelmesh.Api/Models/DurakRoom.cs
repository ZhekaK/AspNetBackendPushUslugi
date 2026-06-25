namespace PushPelmesh.Api.Models;

public enum DurakRoomStatus
{
    Waiting = 0,
    InProgress = 1,
    Finished = 2
}

public class DurakRoom
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public int MaxPlayers { get; set; }
    public int CardCount { get; set; }
    public DurakRoomStatus Status { get; set; }
    public int CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; } = null!;
    public int? AttackerPlayerId { get; set; }
    public DurakRoomPlayer? AttackerPlayer { get; set; }
    public int? DefenderPlayerId { get; set; }
    public DurakRoomPlayer? DefenderPlayer { get; set; }
    public int? WinnerPlayerId { get; set; }
    public DurakRoomPlayer? WinnerPlayer { get; set; }
    public string TrumpSuit { get; set; } = "";
    public string TrumpCardCode { get; set; } = "";
    public string DeckJson { get; set; } = "[]";
    public string TableJson { get; set; } = "[]";
    public string DiscardJson { get; set; } = "[]";
    public bool FirstBout { get; set; } = true;
    public string LastMessage { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<DurakRoomPlayer> Players { get; set; } = new();
}
