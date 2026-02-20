namespace STSAnaliza.Interfejs;

public interface IMatchBalanceFillBuilder
{
    Task<string> BuildByCompetitorIdAsync(string playerName, string competitorId, CancellationToken ct);
}