using PushPelmesh.Api.Models;

namespace PushPelmesh.Api.Dtos;

public class CreateCalendarEventRequest
{
    public CalendarEventType Type { get; set; }

    public string Title { get; set; } = "";

    public string? Description { get; set; }

    public DateOnly Date { get; set; }
}