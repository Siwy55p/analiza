namespace STSAnaliza.Services;

public interface IMatchRawJsonBuilder
{
    Task<string> BuildAsync(string playerName, CancellationToken ct); // zwraca JSON (bez "<<FILL>>=")
    Task<string> BuildByCompetitorIdAsync(string playerName, string competitorId, CancellationToken ct);

}
