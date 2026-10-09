namespace PushPelmesh.Api.Models;

// A device-specific credential that allows a registered user to renew an expired JWT
// after entering their first name. Only the SHA-256 hash is stored on the server.
public class RememberedLogin
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public string TokenHash { get; set; } = "";
    public int FailedNameAttempts { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
}
