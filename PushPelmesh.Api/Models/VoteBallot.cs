namespace PushPelmesh.Api.Models;

public class VoteBallot
{
    public int Id { get; set; }
    public int VotePollId { get; set; }
    public VotePoll VotePoll { get; set; } = null!;
    public int VoteOptionId { get; set; }
    public VoteOption VoteOption { get; set; } = null!;
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
}