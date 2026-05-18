using PushPelmesh.Api.Models;

namespace PushPelmesh.Api.Dtos;

public class CalendarEventDto
{
    public int Id { get; set; }

    public CalendarEventType Type { get; set; }

    public string TypeName { get; set; } = "";

    public string Title { get; set; } = "";

    public string? Description { get; set; }

    public DateOnly Date { get; set; }

    public bool IsSystemEvent { get; set; }

    public string? CreatedByUser { get; set; }
    public int? CreatedByUserId { get; set; }
}