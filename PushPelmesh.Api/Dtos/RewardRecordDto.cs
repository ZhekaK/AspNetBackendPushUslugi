namespace PushPelmesh.Api.Dtos;

public class RewardRecordDto
{
    public int Id { get; set; }

    public int Kind { get; set; }

    public string KindName { get; set; } = "";

    public string FullName { get; set; } = "";

    public string? EventType { get; set; }

    public string EventName { get; set; } = "";

    public string? Place { get; set; }

    public DateOnly Date { get; set; }

    public DateTime CreatedAt { get; set; }
}
