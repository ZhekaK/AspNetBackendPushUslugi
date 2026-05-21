namespace PushPelmesh.Api.Models;

public enum UserType
{
    Guest = 0,
    KeyUser = 1,
    Admin = 2
}

public enum Sex
{
    Man = 0,
    Women = 1
}

public class User
{
    public int Id { get; set; }
    public string UserSeries { get; set; } = "";
    public string UserNumber { get; set; } = "";
    public UserType Type { get; set; }
    public string FirstName { get; set; } = "";
    public string? MiddleName { get; set; } = "";
    public string? LastName { get; set; } = "";
    public Sex? Sex { get; set; }
    public DateOnly? BirthDate { get; set; }
    public DateOnly? GiveDate { get; set; }
    public string? GivePlace { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? LastLoginAt { get; set; }

    public float? WeightKg { get; set; }
}
