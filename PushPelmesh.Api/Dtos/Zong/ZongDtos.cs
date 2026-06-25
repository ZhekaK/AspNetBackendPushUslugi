namespace PushPelmesh.Api.Dtos.Zong;

public class ZongRoomsResponse
{
    public List<ZongRoomListItemDto> Rooms { get; set; } = new();
}

public class ZongRoomResponse
{
    public ZongRoomDto? Room { get; set; }
}

public class ZongRoomListItemDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int MaxPlayers { get; set; }
    public int PlayerCount { get; set; }
    public int TargetScore { get; set; }
    public string Status { get; set; } = "";
    public bool HasPassword { get; set; }
}

public class ZongRoomDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int MaxPlayers { get; set; }
    public int TargetScore { get; set; }
    public string Status { get; set; } = "";
    public int CurrentTurnPlayerId { get; set; }
    public int WinnerPlayerId { get; set; }
    public int MyPlayerId { get; set; }
    public bool IsCreator { get; set; }
    public bool IsMyTurn { get; set; }
    public bool CanStart { get; set; }
    public bool CanRoll { get; set; }
    public bool CanBank { get; set; }
    public int CurrentTurnScore { get; set; }
    public int RemainingDice { get; set; }
    public string LastDice { get; set; } = "";
    public int LastRollScore { get; set; }
    public string LastRollMessage { get; set; } = "";
    public List<ZongPlayerDto> Players { get; set; } = new();
}

public class ZongPlayerDto
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string DisplayName { get; set; } = "";
    public int Score { get; set; }
    public int InitialRoll { get; set; }
    public int TurnOrder { get; set; }
    public bool IsCurrentTurn { get; set; }
    public bool IsWinner { get; set; }
}

public class CreateZongRoomRequest
{
    public string Name { get; set; } = "";
    public string Password { get; set; } = "";
    public int MaxPlayers { get; set; }
    public int TargetScore { get; set; }
}

public class JoinZongRoomRequest
{
    public string Password { get; set; } = "";
}