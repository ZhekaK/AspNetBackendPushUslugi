namespace PushPelmesh.Api.Models;

[Flags]
public enum VoteAudienceGroup
{
    RegularUsers = 1,
    Ministers = 2
}

public class VotePoll
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public DateOnly EndDate { get; set; }
    public VoteAudienceGroup AudienceGroups { get; set; }
    public int CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; } = null!;
    public string CreatedByUserName { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public List<VoteOption> Options { get; set; } = new();
    public List<VoteBallot> Ballots { get; set; } = new();
}