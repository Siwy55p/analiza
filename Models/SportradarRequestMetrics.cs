using System.Collections.Generic;
using System.Linq;

namespace STSAnaliza.Models;

public sealed record SportradarRequestSnapshot(
    long TotalRequests,
    long Total429,
    long TotalElapsedMs,
    IReadOnlyDictionary<string, long> ByEndpoint);

public sealed record SportradarRequestDelta(
    long Requests,
    long Status429,
    double AvgMs,
    IReadOnlyList<(string Endpoint, long Count)> TopEndpoints)
{
    public string ToLogLine()
    {
        var top = TopEndpoints is null || TopEndpoints.Count == 0
            ? "brak"
            : string.Join(", ", TopEndpoints.Select(x => $"{x.Endpoint}={x.Count}"));

        return $"req={Requests}, 429={Status429}, avgMs={AvgMs:0.0}, top=[{top}]";
    }
}
