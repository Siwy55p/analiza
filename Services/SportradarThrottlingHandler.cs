using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;

namespace STSAnaliza.Services;

public sealed class SportradarThrottlingHandler : DelegatingHandler
{
    private readonly ILogger<SportradarThrottlingHandler> _logger;
    private readonly SemaphoreSlim _concurrency;
    private readonly int _max429Retries;

    // Token bucket (bez System.Threading.RateLimiting)
    private readonly object _tbLock = new();
    private readonly double _rps;
    private readonly double _burst;
    private double _tokens;
    private DateTime _lastRefillUtc;

    // jitter per wątek
    private static readonly ThreadLocal<Random> _rng = new(() => new Random());

    public SportradarThrottlingHandler(
        IOptions<SportradarClientOptions> opt,
        ILogger<SportradarThrottlingHandler> logger)
    {
        _logger = logger;

        var o = opt.Value;

        _max429Retries = Math.Max(0, o.Max429Retries);

        var maxConc = Math.Max(1, o.MaxConcurrency);
        _concurrency = new SemaphoreSlim(maxConc, maxConc);

        _rps = Math.Max(0.1, o.RequestsPerSecond);
        _burst = Math.Max(1, o.Burst);

        _tokens = _burst;
        _lastRefillUtc = DateTime.UtcNow;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        await _concurrency.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var original = request;
            // bezpiecznie retry tylko dla zapytań bez body (Sportradar to praktycznie zawsze GET)
            if (request.Method != HttpMethod.Get && request.Method != HttpMethod.Head)
            {
                return await base.SendAsync(request, ct).ConfigureAwait(false);
            }
            for (int attempt = 0; ; attempt++)
            {
                await AcquireTokenAsync(ct).ConfigureAwait(false);

                var reqToSend = attempt == 0 ? original : original.CloneShallow();
                var resp = await base.SendAsync(reqToSend, ct).ConfigureAwait(false);

                if (resp.StatusCode != (HttpStatusCode)429)
                    return resp;

                if (attempt >= _max429Retries)
                    return resp; // oddaj 429 dalej

                var retryAfter = GetRetryAfter(resp) ?? TimeSpan.FromSeconds(2);
                var jitterMs = _rng.Value!.Next(100, 400);
                var delay = retryAfter + TimeSpan.FromMilliseconds(jitterMs);

                _logger.LogWarning("Sportradar 429. Retry after {Delay}. Url: {Url}",
                    delay, reqToSend.RequestUri);

                resp.Dispose();
                await Task.Delay(delay, ct).ConfigureAwait(false);
            }
        }
        finally
        {
            _concurrency.Release();
        }
    }

    private async Task AcquireTokenAsync(CancellationToken ct)
    {
        while (true)
        {
            TimeSpan delay;

            lock (_tbLock)
            {
                var now = DateTime.UtcNow;
                var elapsed = (now - _lastRefillUtc).TotalSeconds;

                if (elapsed > 0)
                {
                    _tokens = Math.Min(_burst, _tokens + elapsed * _rps);
                    _lastRefillUtc = now;
                }

                if (_tokens >= 1d)
                {
                    _tokens -= 1d;
                    return;
                }

                var seconds = (1d - _tokens) / _rps;
                delay = TimeSpan.FromSeconds(seconds);
            }

            if (delay < TimeSpan.FromMilliseconds(5))
                delay = TimeSpan.FromMilliseconds(5);

            await Task.Delay(delay, ct).ConfigureAwait(false);
        }
    }

    private static TimeSpan? GetRetryAfter(HttpResponseMessage resp)
    {
        var ra = resp.Headers.RetryAfter;
        if (ra?.Delta is not null) return ra.Delta;

        if (ra?.Date is not null)
        {
            var d = ra.Date.Value - DateTimeOffset.UtcNow;
            return d > TimeSpan.Zero ? d : TimeSpan.Zero;
        }

        return null;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _concurrency.Dispose();

        base.Dispose(disposing);
    }
}
