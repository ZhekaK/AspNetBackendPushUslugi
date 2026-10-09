namespace PushPelmesh.Api.Dtos;

public class CreateDirectChatRequest
{
    public int UserId { get; set; }
}

public class CreateGroupChatRequest
{
    public string Title { get; set; } = "";
    public List<int> UserIds { get; set; } = new();
}

public class SendChatMessageRequest
{
    public string Text { get; set; } = "";
}

public class SetChatMutedRequest
{
    public bool Muted { get; set; }
}

public class MarkChatReadRequest
{
    public long LastMessageId { get; set; }
}
