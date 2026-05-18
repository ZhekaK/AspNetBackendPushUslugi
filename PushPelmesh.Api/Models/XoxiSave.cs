namespace PushPelmesh.Api.Models;

public class XoxiSave
{
    public int UserId { get; set; }

    public User User { get; set; } = null!;

    public string SaveData { get; set; } = "{}";

    public DateTime UpdatedAt { get; set; }
}
