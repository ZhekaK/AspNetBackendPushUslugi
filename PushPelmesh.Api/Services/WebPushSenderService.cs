using System.Text.Json;
using WebPush;

namespace PushPelmesh.Api.Services;

public class WebPushSenderService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<WebPushSenderService> _logger;

    public WebPushSenderService(
        IConfiguration configuration,
        ILogger<WebPushSenderService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<bool> SendAsync(
        Models.PushNotificationSubscription subscription,
        string title,
        string body,
        string url = "/")
    {
        var publicKey = _configuration["Vapid:PublicKey"];
        var privateKey = _configuration["Vapid:PrivateKey"];
        var subject = _configuration["Vapid:Subject"];

        if (string.IsNullOrWhiteSpace(publicKey) ||
            string.IsNullOrWhiteSpace(privateKey) ||
            string.IsNullOrWhiteSpace(subject))
        {
            _logger.LogError("VAPID settings are missing");
            return false;
        }

        var webPushSubscription = new WebPush.PushSubscription(
            subscription.Endpoint,
            subscription.P256dh,
            subscription.Auth);

        var vapidDetails = new VapidDetails(
            subject,
            publicKey,
            privateKey);

        var payload = JsonSerializer.Serialize(new
        {
            title,
            body,
            url,
            icon = "/favicon.png"
        });

        var webPushClient = new WebPushClient();

        try
        {
            await webPushClient.SendNotificationAsync(
                webPushSubscription,
                payload,
                vapidDetails);

            subscription.LastUsedAt = DateTime.UtcNow;

            return true;
        }
        catch (WebPushException exception)
        {
            _logger.LogError(
                exception,
                "Web push send failed. StatusCode: {StatusCode}",
                exception.StatusCode);

            return false;
        }
    }
}