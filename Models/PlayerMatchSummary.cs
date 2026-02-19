namespace STSAnaliza.Models;

public sealed record PlayerMatchSummary(
    DateTimeOffset StartTimeUtc,
    string OpponentId,
    string OpponentName,
    string Result,
    string Score,
    string Surface,
    string? SportEventId = null
);