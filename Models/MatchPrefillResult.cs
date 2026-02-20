namespace STSAnaliza.Models;

public sealed class MatchPrefillResult
{
    public required Dictionary<string, string> Prefilled { get; init; }

    public string? CompetitorIdA { get; init; }
    public string? CompetitorIdB { get; init; }

    public DateOnly? MatchDate { get; init; }
}
