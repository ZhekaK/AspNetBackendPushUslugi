namespace PushPelmesh.Api.Models;

public enum PushPlatform
{
    Web = 0,
    Android = 1,
    IOS = 2
}

public class PushSubscription
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public User User { get; set; } = null!;

    public PushPlatform Platform { get; set; }

    public string Endpoint { get; set; } = "";

    public string P256dh { get; set; } = "";

    public string Auth { get; set; } = "";

    public DateTime CreatedAt { get; set; }

    public DateTime? LastUsedAt { get; set; }
}