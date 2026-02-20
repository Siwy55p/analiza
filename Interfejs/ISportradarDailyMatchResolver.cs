namespace STSAnaliza.Interfejs;

public interface ISportradarDailyMatchResolver
{
    Task<(string? PlayerAId, string? PlayerBId)> TryResolveCompetitorIdsAsync(
        DateOnly date,
        string playerAName,
        string playerBName,
        CancellationToken ct);
}