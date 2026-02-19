namespace STSAnaliza.Services;

public sealed class SportradarRateGate
{
    private readonly SemaphoreSlim _mutex = new(1, 1);
    private DateTimeOffset _nextAllowedUtc = DateTimeOffset.MinValue;

    private static readonly TimeSpan MinInterval = TimeSpan.FromSeconds(1); // trial: 1 QPS

    public async Task WaitTurnAsync(CancellationToken ct)
    {
        await _mutex.WaitAsync(ct);
        try
        {
            var now = DateTimeOffset.UtcNow;
            if (now < _nextAllowedUtc)
                await Task.Delay(_nextAllowedUtc - now, ct);

            _nextAllowedUtc = DateTimeOffset.UtcNow + MinInterval;
        }
        finally
        {
            _mutex.Release();
        }
    }
}
