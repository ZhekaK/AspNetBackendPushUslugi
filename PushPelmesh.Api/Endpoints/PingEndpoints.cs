namespace PushPelmesh.Api.Endpoints;

public static class PingEndpoints
{
    public static void MapPingEndpoints(this WebApplication app)
    {
        app.MapGet("/api/ping", () =>
        {
            return Results.Ok(new
            {
                message = "Server is alive",
                time = DateTime.UtcNow
            });
        });
    }
}