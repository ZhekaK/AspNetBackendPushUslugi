namespace PushPelmesh.Api.Dtos;

public class SendSystemPushRequest
{
    public string Title { get; set; } = "";

    public string Description { get; set; } = "";

    public bool SendToAll { get; set; }

    public List<SystemPushRecipientRequest> Recipients { get; set; } = new();
}

public class SystemPushRecipientRequest
{
    public string? UserSeries { get; set; }

    public string? UserNumbers { get; set; }
}
