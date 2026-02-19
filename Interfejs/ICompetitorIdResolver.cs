namespace STSAnaliza.Services;

public interface ICompetitorIdResolver
{
    Task<string?> ResolveAsync(string playerName, CancellationToken ct);
}
