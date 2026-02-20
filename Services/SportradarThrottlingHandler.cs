using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using STSAnaliza.Interfejs;
using STSAnaliza.Options;
using System.Diagnostics;
using System.Net;
using System.Threading;

namespace STSAnaliza.Services;

public sealed class SportradarThrottlingHandler : DelegatingHandler
{
    private readonly ILogger<SportradarThrottlingHandler> _logger;
    private readonly ISportradarRequestMeter _meter;

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

    // log co N requestów (żeby nie spamować)
    private const int LogEveryNRequests = 100;
    private long _logCounter;

    public SportradarThrottlingHandler(
        IOptions<SportradarClientOptions> opt,
        ILogger<SportradarThrottlingHandler> logger,
        ISportradarRequestMeter meter)
    {
        _logger = logger;
        _meter = meter;

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
                var swStart = Stopwatch.GetTimestamp();

                if (attempt == 0)
                {
                    resp = await base.SendAsync(original, ct).ConfigureAwait(false);
                }
                else
                {
                    using var clone = original.CloneShallow();
                    resp = await base.SendAsync(clone, ct).ConfigureAwait(false);
                }

                Track(original, resp, attempt, swStart);

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

    private void Track(HttpRequestMessage original, HttpResponseMessage resp, int attempt, long swStart)
    {
        var elapsedMs = (long)Stopwatch.GetElapsedTime(swStart).TotalMilliseconds;
        var endpoint = NormalizeEndpoint(original.RequestUri);

        _meter.Track(endpoint, (int)resp.StatusCode, elapsedMs);

        // retry -> tylko DEBUG
        if (attempt > 0)
        {
            _logger.LogDebug(
                "Sportradar retry attempt={Attempt} status={Status} endpoint={Endpoint} elapsedMs={ElapsedMs}",
                attempt,
                (int)resp.StatusCode,
                endpoint,
                elapsedMs);
        }

        // INFO co N requestów: szybki sanity check "czy nie poleciało za dużo"
        if (!_logger.IsEnabled(LogLevel.Information))
            return;

        var count = Interlocked.Increment(ref _logCounter);
        if (count % LogEveryNRequests != 0)
            return;

        var snap = _meter.Snapshot();
        var avg = snap.TotalRequests > 0 ? (double)snap.TotalElapsedMs / snap.TotalRequests : 0d;

        var top = snap.ByEndpoint
            .Select(kv => (Endpoint: kv.Key, Count: kv.Value))
            .OrderByDescending(x => x.Count)
            .Take(5)
            .Select(x => $"{x.Endpoint}={x.Count}")
            .ToArray();

        _logger.LogInformation(
            "Sportradar stats: total={Total}, 429={Total429}, avgMs={AvgMs:0.0}, pending={Pending}, top=[{Top}]",
            snap.TotalRequests,
            snap.Total429,
            avg,
            Volatile.Read(ref _pending),
            string.Join(", ", top));
    }

    private static string NormalizeEndpoint(Uri? uri)
    {
        if (uri is null) return "(null)";

        // /tennis/{access}/v3/{locale}/{relativePath}
        var path = uri.AbsolutePath ?? string.Empty;
        var idx = path.IndexOf("/v3/", StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
        {
            var rest = path[(idx + 4)..]; // {locale}/{relativePath}
            var slash = rest.IndexOf('/');
            if (slash >= 0 && slash + 1 < rest.Length)
                rest = rest[(slash + 1)..];

            return NormalizeRelative(rest);
        }

        return NormalizeRelative(path.Trim('/'));
    }

    private static string NormalizeRelative(string relative)
    {
        if (string.IsNullOrWhiteSpace(relative))
            return "(empty)";

        var parts = relative.Split('/', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < parts.Length; i++)
        {
            var p = parts[i];

            // schedules/2026-02-20/summaries.json
            if (p.Length == 10 && p[4] == '-' && p[7] == '-' &&
                char.IsDigit(p[0]) && char.IsDigit(p[1]) && char.IsDigit(p[2]) && char.IsDigit(p[3]))
            {
                parts[i] = "{date}";
                continue;
            }

            // sr:... albo zakodowane sr%3a...
            if (p.Contains("sr:", StringComparison.OrdinalIgnoreCase) ||
                p.Contains("sr%3a", StringComparison.OrdinalIgnoreCase))
            {
                parts[i] = "{id}";
                continue;
            }
        }

        return string.Join('/', parts);
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
