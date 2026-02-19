using System.Text.Json;

namespace STSAnaliza.Services;

public sealed class SportradarRankService : IRankService
{
    private readonly ISportradarTennisClient _client;

    private readonly SemaphoreSlim _loadGate = new(1, 1);
    private DateTimeOffset _loadedAtUtc = DateTimeOffset.MinValue;

    private Dictionary<string, int> _rankByCompetitorId = new(StringComparer.OrdinalIgnoreCase);

    public SportradarRankService(ISportradarTennisClient client)
    {
        _client = client;
    }

    public async Task<int?> GetSinglesRankAsync(string competitorId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(competitorId))
            return null;

        await EnsureLoadedAsync(ct);

        return _rankByCompetitorId.TryGetValue(competitorId, out var r) ? r : null;
    }

    private async Task EnsureLoadedAsync(CancellationToken ct)
    {
        // odśwież co 6h (wystarczy)
        if (_rankByCompetitorId.Count > 0 && (DateTimeOffset.UtcNow - _loadedAtUtc) < TimeSpan.FromHours(6))
            return;

        await _loadGate.WaitAsync(ct);
        try
        {
            if (_rankByCompetitorId.Count > 0 && (DateTimeOffset.UtcNow - _loadedAtUtc) < TimeSpan.FromHours(6))
                return;

            var json = await _client.GetRankingsJsonAsync(ct);
            _rankByCompetitorId = ParseRankings(json);

            _loadedAtUtc = DateTimeOffset.UtcNow;
        }
        finally
        {
            _loadGate.Release();
        }
    }

    private static Dictionary<string, int> ParseRankings(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        void Walk(JsonElement el)
        {
            if (el.ValueKind == JsonValueKind.Object)
            {
                // Szukamy wzorca: { rank: <int>, competitor: { id: "sr:competitor:..." } }
                if (el.TryGetProperty("competitor", out var comp) &&
                    comp.ValueKind == JsonValueKind.Object &&
                    comp.TryGetProperty("id", out var idEl) &&
                    idEl.ValueKind == JsonValueKind.String)
                {
                    var id = idEl.GetString();

                    if (!string.IsNullOrWhiteSpace(id) &&
                        el.TryGetProperty("rank", out var rankEl))
                    {
                        if (rankEl.ValueKind == JsonValueKind.Number && rankEl.TryGetInt32(out var r))
                            map[id] = r;
                        else if (rankEl.ValueKind == JsonValueKind.String && int.TryParse(rankEl.GetString(), out r))
                            map[id] = r;
                    }
                }

                foreach (var p in el.EnumerateObject())
                    Walk(p.Value);
            }
            else if (el.ValueKind == JsonValueKind.Array)
            {
                foreach (var x in el.EnumerateArray())
                    Walk(x);
            }
        }

        Walk(doc.RootElement);
        return map;
    }
}
