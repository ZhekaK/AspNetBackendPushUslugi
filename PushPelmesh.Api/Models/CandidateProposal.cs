namespace PushPelmesh.Api.Models;

public class CandidateProposal
{
    public int Id { get; set; }
    public string FullName { get; set; } = "";
    public string Reason { get; set; } = "";
    public int SubmittedByUserId { get; set; }
    public User SubmittedBy { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public bool? Approved { get; set; }
    public int? ReviewedByUserId { get; set; }
    public DateTime? ReviewedAt { get; set; }
}
