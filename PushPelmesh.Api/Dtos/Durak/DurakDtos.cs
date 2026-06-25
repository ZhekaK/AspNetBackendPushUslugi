namespace PushPelmesh.Api.Dtos.Durak;

public class DurakRoomsResponse
{
    public List<DurakRoomListItemDto> Rooms { get; set; } = new();
}

public class DurakRoomResponse
{
    public DurakRoomDto? Room { get; set; }
}

public class DurakRoomListItemDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int MaxPlayers { get; set; }
    public int PlayerCount { get; set; }
    public int CardCount { get; set; }
    public string Status { get; set; } = "";
    public bool HasPassword { get; set; }
}

public class DurakRoomDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int MaxPlayers { get; set; }
    public int CardCount { get; set; }
    public string Status { get; set; } = "";
    public string TrumpSuit { get; set; } = "";
    public string TrumpCardCode { get; set; } = "";
    public int DeckCount { get; set; }
    public int MyPlayerId { get; set; }
    public int AttackerPlayerId { get; set; }
    public int DefenderPlayerId { get; set; }
    public bool IsCreator { get; set; }
    public bool CanStart { get; set; }
    public bool CanAttack { get; set; }
    public bool CanDefend { get; set; }
    public bool CanTransfer { get; set; }
    public bool CanTake { get; set; }
    public bool CanPass { get; set; }
    public string Message { get; set; } = "";
    public List<DurakPlayerDto> Players { get; set; } = new();
    public List<DurakCardDto> MyHand { get; set; } = new();
    public List<DurakTableCardDto> Table { get; set; } = new();
}

public class DurakPlayerDto
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string DisplayName { get; set; } = "";
    public int HandCount { get; set; }
    public int TurnOrder { get; set; }
    public bool IsBot { get; set; }
    public bool IsAttacker { get; set; }
    public bool IsDefender { get; set; }
    public bool IsOut { get; set; }
}

public class DurakCardDto
{
    public string Code { get; set; } = "";
    public string Rank { get; set; } = "";
    public string Suit { get; set; } = "";
    public int Value { get; set; }
}

public class DurakTableCardDto
{
    public DurakCardDto? Attack { get; set; }
    public DurakCardDto? Defense { get; set; }
}

public class CreateDurakRoomRequest
{
    public string Name { get; set; } = "";
    public string Password { get; set; } = "";
    public int MaxPlayers { get; set; }
    public int CardCount { get; set; }
}

public class JoinDurakRoomRequest
{
    public string Password { get; set; } = "";
}

public class DurakCardActionRequest
{
    public string CardCode { get; set; } = "";
    public string AttackCardCode { get; set; } = "";
}
