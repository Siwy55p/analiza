using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;
using System.Threading;

namespace STSAnaliza.Services;

public sealed class SportradarThrottlingHandler : DelegatingHandler
{
    private readonly ILogger<SportradarThrottlingHandler> _logger;
    private readonly SemaphoreSlim _concurrency;
    private readonly int _max429Retries;

    private readonly int _queueLimit;
    private int _pending; // queued + in-flight

    // Token bucket
    private readonly object _tbLock = new();
    private readonly double _rps;
    private readonly double _burst;
    private double _tokens;
    private DateTime _lastRefillUtc;

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

        _queueLimit = Math.Max(10, o.QueueLimit);

        _rps = Math.Max(0.1, o.RequestsPerSecond);
        _burst = Math.Max(1, o.Burst);

        _tokens = _burst;
        _lastRefillUtc = DateTime.UtcNow;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        if (Interlocked.Increment(ref _pending) > _queueLimit)
        {
            Interlocked.Decrement(ref _pending);
            throw new InvalidOperationException(
                $"Sportradar queue limit exceeded ({_queueLimit}). Zmniejsz analizę równoległą albo zwiększ Sportradar:Client:QueueLimit.");
        }

        var acquired = false;

        try
        {
            await _concurrency.WaitAsync(ct).ConfigureAwait(false);
            acquired = true;

            // retry tylko dla zapytań bez body (Sportradar: GET/HEAD)
            if (request.Method != HttpMethod.Get && request.Method != HttpMethod.Head)
                return await base.SendAsync(request, ct).ConfigureAwait(false);

            var original = request;

            for (int attempt = 0; ; attempt++)
            {
                await AcquireTokenAsync(ct).ConfigureAwait(false);

                HttpResponseMessage resp;

                if (attempt == 0)
                {
                    resp = await base.SendAsync(original, ct).ConfigureAwait(false);
                }
                else
                {
                    using var clone = original.CloneShallow();
                    resp = await base.SendAsync(clone, ct).ConfigureAwait(false);
                }

                if (resp.StatusCode != (HttpStatusCode)429)
                    return resp;

                if (attempt >= _max429Retries)
                    return resp; // oddaj 429 dalej

                var retryAfter = GetRetryAfter(resp) ?? TimeSpan.FromSeconds(2);
                var jitterMs = _rng.Value!.Next(100, 400);
                var delay = retryAfter + TimeSpan.FromMilliseconds(jitterMs);

                _logger.LogWarning("Sportradar 429. Retry after {Delay}. Url: {Url}",
                    delay, original.RequestUri);

                resp.Dispose();
                await Task.Delay(delay, ct).ConfigureAwait(false);
            }
        }
        finally
        {
            if (acquired)
                _concurrency.Release();

            Interlocked.Decrement(ref _pending);
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