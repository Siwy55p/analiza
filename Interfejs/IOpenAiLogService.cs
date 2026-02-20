namespace STSAnaliza.Interfejs;

public interface IOpenAiLogService
{
    Task<string> GetResponseLogAsync(string respId, CancellationToken ct = default);
}