namespace STSAnaliza.Interfejs;

public interface ICompetitorIdResolver
{
    Task<string?> ResolveAsync(string playerName, CancellationToken ct);
}