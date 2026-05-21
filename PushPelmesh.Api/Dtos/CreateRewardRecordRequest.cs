using PushPelmesh.Api.Models;

namespace PushPelmesh.Api.Dtos;

public class CreateRewardRecordRequest
{
    public RewardKind Kind { get; set; }

    public string? UserSeries { get; set; }

    public string? UserNumbers { get; set; }

    public string? FullName { get; set; }

    public string? EventType { get; set; }

    public string EventName { get; set; } = "";

    public string? Place { get; set; }
}
