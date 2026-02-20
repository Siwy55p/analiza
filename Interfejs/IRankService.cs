namespace STSAnaliza.Interfejs;

public interface IRankService
{
    Task<int?> GetSinglesRankAsync(string competitorId, CancellationToken ct);
}