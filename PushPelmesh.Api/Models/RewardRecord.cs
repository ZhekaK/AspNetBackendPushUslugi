namespace PushPelmesh.Api.Models;

public enum RewardKind
{
    Championship = 0,
    GovernmentAward = 1
}

public class RewardRecord
{
    public int Id { get; set; }

    public RewardKind Kind { get; set; }

    public int? UserId { get; set; }

    public User? User { get; set; }

    public string FullName { get; set; } = "";

    public string? EventType { get; set; }

    public string EventName { get; set; } = "";

    public string? Place { get; set; }

    public DateTime CreatedAt { get; set; }
}
