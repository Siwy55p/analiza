namespace STSAnaliza.Services;

public interface IRankService
{
    Task<int?> GetSinglesRankAsync(string competitorId, CancellationToken ct);
}
