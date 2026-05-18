namespace PushPelmesh.Api.Security;

public static class AdminKeyValidator
{
    public static bool IsValid(HttpRequest request, IConfiguration configuration)
    {
        var adminKeyFromConfig = configuration["Admin:Key"];

        if (string.IsNullOrWhiteSpace(adminKeyFromConfig))
        {
            return false;
        }

        if (!request.Headers.TryGetValue("X-Admin-Key", out var providedAdminKey))
        {
            return false;
        }

        return providedAdminKey == adminKeyFromConfig;
    }
}