using STSAnaliza.Interfejs;
using STSAnaliza.Models;
using System.Collections.Concurrent;

namespace STSAnaliza.Services;

public sealed class SportradarRequestMeter : ISportradarRequestMeter
{
    private sealed class Counter { public long Value; }

    private long _totalRequests;
    private long _total429;
    private long _totalElapsedMs;

    private readonly ConcurrentDictionary<string, Counter> _byEndpoint = new(StringComparer.OrdinalIgnoreCase);

    public SportradarRequestSnapshot Snapshot()
    {
        // Snapshot robimy rzadko (np. per mecz), więc kopiujemy mapę do prostego Dictionary.
        var copy = new Dictionary<string, long>(_byEndpoint.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var kv in _byEndpoint)
        {
            var v = Interlocked.Read(ref kv.Value.Value);
            copy[kv.Key] = v;
        }

        return new SportradarRequestSnapshot(
            TotalRequests: Interlocked.Read(ref _totalRequests),
            Total429: Interlocked.Read(ref _total429),
            TotalElapsedMs: Interlocked.Read(ref _totalElapsedMs),
            ByEndpoint: copy);
    }

    public SportradarRequestDelta DeltaSince(SportradarRequestSnapshot start, int topN = 5)
    {
        var end = Snapshot();

        var req = Math.Max(0, end.TotalRequests - start.TotalRequests);
        var s429 = Math.Max(0, end.Total429 - start.Total429);
        var elapsed = Math.Max(0, end.TotalElapsedMs - start.TotalElapsedMs);
        var avg = req > 0 ? (double)elapsed / req : 0d;

        var deltas = new List<(string Endpoint, long Count)>();
        foreach (var kv in end.ByEndpoint)
        {
            start.ByEndpoint.TryGetValue(kv.Key, out var startCount);
            var d = kv.Value - startCount;
            if (d > 0)
                deltas.Add((kv.Key, d));
        }

        var top = deltas
            .OrderByDescending(x => x.Count)
            .Take(Math.Max(0, topN))
            .ToList();

        return new SportradarRequestDelta(req, s429, avg, top);
    }

    public void Track(string endpoint, int statusCode, long elapsedMs)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
            endpoint = "(empty)";

        Interlocked.Increment(ref _totalRequests);
        Interlocked.Add(ref _totalElapsedMs, elapsedMs);

        if (statusCode == 429)
            Interlocked.Increment(ref _total429);

        var counter = _byEndpoint.GetOrAdd(endpoint, _ => new Counter());
        Interlocked.Increment(ref counter.Value);
    }
}
