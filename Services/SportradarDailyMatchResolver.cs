using System.Collections.Concurrent;
using System.Text.Json;

namespace STSAnaliza.Services;

public interface ISportradarDailyMatchResolver
{
    Task<(string? PlayerAId, string? PlayerBId)> TryResolveCompetitorIdsAsync(
        DateOnly date,
        string playerAName,
        string playerBName,
        CancellationToken ct);
}

public sealed class SportradarDailyMatchResolver : ISportradarDailyMatchResolver
{
    private readonly ISportradarTennisClient _client;

    // cache per dzień (żeby nie walić schedules X razy w tej samej analizie)
    private readonly ConcurrentDictionary<DateOnly, string> _dailyCache = new();

    public SportradarDailyMatchResolver(ISportradarTennisClient client)
    {
        _client = client;
    }

    public async Task<(string? PlayerAId, string? PlayerBId)> TryResolveCompetitorIdsAsync(
        DateOnly date,
        string playerAName,
        string playerBName,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(playerAName) || string.IsNullOrWhiteSpace(playerBName))
            return (null, null);

        var json = await GetDailyJsonCachedAsync(date, ct);

        var aTokens = SportradarName.Tokens(playerAName);
        var bTokens = SportradarName.Tokens(playerBName);

        using var doc = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty("summaries", out var summaries) ||
            summaries.ValueKind != JsonValueKind.Array)
            return (null, null);

        foreach (var summary in summaries.EnumerateArray())
        {
            if (!summary.TryGetProperty("sport_event", out var se) || se.ValueKind != JsonValueKind.Object)
                continue;

            if (!se.TryGetProperty("competitors", out var comps) || comps.ValueKind != JsonValueKind.Array)
                continue;

            // zbierz competitorów
            var list = new List<(string? Id, string? Name)>();
            foreach (var c in comps.EnumerateArray())
            {
                if (c.ValueKind != JsonValueKind.Object) continue;

                c.TryGetProperty("id", out var idEl);
                c.TryGetProperty("name", out var nameEl);

                var id = idEl.ValueKind == JsonValueKind.String ? idEl.GetString() : null;
                var name = nameEl.ValueKind == JsonValueKind.String ? nameEl.GetString() : null;

                list.Add((id, name));
            }

            if (list.Count < 2) continue;

            // dopasuj po tokenach (ignoruje kolejność imię/nazwisko)
            string? aId = null;
            string? bId = null;

            foreach (var (id, name) in list)
            {
                if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name)) continue;

                var t = SportradarName.Tokens(name);

                if (aId is null && IsSubset(aTokens, t))
                    aId = id;

                if (bId is null && IsSubset(bTokens, t))
                    bId = id;
            }

            if (aId is not null && bId is not null)
                return (aId, bId);
        }

        return (null, null);
    }

    private async Task<string> GetDailyJsonCachedAsync(DateOnly date, CancellationToken ct)
    {
        if (_dailyCache.TryGetValue(date, out var cached))
            return cached;

        var json = await _client.GetDailySummariesJsonAsync(date, ct);
        _dailyCache.TryAdd(date, json);
        return json;
    }

    private static bool IsSubset(HashSet<string> need, HashSet<string> have)
    {
        foreach (var x in need)
            if (!have.Contains(x))
                return false;

        return true;
    }
}