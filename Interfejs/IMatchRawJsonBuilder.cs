namespace STSAnaliza.Interfejs;

public interface IMatchRawJsonBuilder
{
    Task<string> BuildAsync(string playerName, CancellationToken ct);
    Task<string> BuildByCompetitorIdAsync(string playerName, string competitorId, CancellationToken ct);
}