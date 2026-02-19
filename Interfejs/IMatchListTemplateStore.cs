namespace STSAnaliza;

public interface IMatchListTemplateStore
{
    string FilePath { get; }
    Task<string> LoadAsync(CancellationToken ct = default);
    Task SaveAsync(string template, CancellationToken ct = default);
}
