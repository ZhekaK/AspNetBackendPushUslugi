using PushPelmesh.Api.Models;

namespace PushPelmesh.Api.Dtos;

public class SubscribePushRequest
{
    public PushPlatform Platform { get; set; } = PushPlatform.Web;

    public string Endpoint { get; set; } = "";

    public string P256dh { get; set; } = "";

    public string Auth { get; set; } = "";
}