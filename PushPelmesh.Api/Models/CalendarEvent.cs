using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PushPelmesh.Api.Models
{
    public enum CalendarEventType
    {
        Birthday = 0,
        Meeting = 1
    }

    public class CalendarEvent
    {
        public int Id { get; set; }

        public CalendarEventType Type { get; set; }

        public string Title { get; set; } = "";

        public string? Description { get; set; }

        public DateOnly Date { get; set; }

        public string? CreatedByUser { get; set; }
        public int? CreatedByUserId { get; set; }

        public bool IsSystemEvent { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}