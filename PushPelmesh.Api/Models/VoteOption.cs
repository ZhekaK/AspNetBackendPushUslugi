namespace PushPelmesh.Api.Models;

public class VoteOption
{
    public int Id { get; set; }
    public int VotePollId { get; set; }
    public VotePoll VotePoll { get; set; } = null!;
    public string Text { get; set; } = "";
    public int SortOrder { get; set; }
    public List<VoteBallot> Ballots { get; set; } = new();
}