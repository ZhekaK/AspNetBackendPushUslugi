namespace PushPelmesh.Api.Models;

public class CalendarEventNotification
{
    public int Id { get; set; }

    public int CalendarEventId { get; set; }

    public CalendarEvent CalendarEvent { get; set; } = null!;

    public DateOnly SentForDate { get; set; }

    public DateTime SentAt { get; set; }
}
