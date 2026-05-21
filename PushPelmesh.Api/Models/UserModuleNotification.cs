namespace PushPelmesh.Api.Models;

public class UserModuleNotification
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public User User { get; set; } = null!;

    public string ModuleKey { get; set; } = "";

    public string SourceKey { get; set; } = "";

    public string? Title { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ReadAt { get; set; }
}
