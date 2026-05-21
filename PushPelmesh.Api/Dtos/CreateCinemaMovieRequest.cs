namespace PushPelmesh.Api.Dtos;

public class CreateCinemaMovieRequest
{
    public string Title { get; set; } = "";

    public decimal Rating { get; set; }

    public DateOnly WatchedAt { get; set; }

    public string Url { get; set; } = "";
}
