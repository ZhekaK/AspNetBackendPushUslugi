namespace PushPelmesh.Api.Models;

public class IdentityPhoto
{
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public byte[] Jpeg { get; set; } = [];
}
