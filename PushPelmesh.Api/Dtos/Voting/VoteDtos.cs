using PushPelmesh.Api.Models;

namespace PushPelmesh.Api.Dtos.Voting;

public class VotePollsResponse
{
    public List<VotePollDto> Polls { get; set; } = new();
}

public class VotePollResponse
{
    public VotePollDto? Poll { get; set; }
}

public class VotePollDto
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public DateOnly EndDate { get; set; }
    public string CreatedByUser { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public bool IsClosed { get; set; }
    public bool HasVoted { get; set; }
    public bool CanVote { get; set; }
    public int SelectedOptionId { get; set; }
    public int TotalVotes { get; set; }
    public List<string> AudienceGroups { get; set; } = new();
    public List<VoteOptionDto> Options { get; set; } = new();
}

public class VoteOptionDto
{
    public int Id { get; set; }
    public string Text { get; set; } = "";
    public int Votes { get; set; }
    public float Percent { get; set; }
    public bool IsSelected { get; set; }
}

public class CreateVotePollRequest
{
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public DateOnly EndDate { get; set; }
    public List<string> Options { get; set; } = new();
    public List<string> AudienceGroups { get; set; } = new();
}

public class VotePollVoteRequest
{
    public int OptionId { get; set; }
}