namespace PushPelmesh.Api.Models;

public class AccessKey
{
    public int Id { get; set; }

    public string Series { get; set; } = "";

    public string Number { get; set; } = "";

    public string AccountNameFirst { get; set; } = "";
    public string? AccountNameMiddle { get; set; } = "";
    public string? AccountNameLast { get; set; } = "";
    public DateOnly? BirthDate { get; set; }
    public DateOnly? GiveDate { get; set; }
    public string? GivePlace { get; set; }
    public Sex? Sex { get; set; }
    public bool IsActivated { get; set; }

    public User? ActivatedByUser { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ActivatedAt { get; set; }
}