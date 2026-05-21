namespace PushPelmesh.Api.Models;

public class CinemaMovieRating
{
    public int Id { get; set; }

    public string Title { get; set; } = "";

    public decimal Rating { get; set; }

    public DateOnly WatchedAt { get; set; }

    public string Url { get; set; } = "";

    public int? CreatedByUserId { get; set; }

    public User? CreatedByUser { get; set; }

    public DateTime CreatedAt { get; set; }
}
