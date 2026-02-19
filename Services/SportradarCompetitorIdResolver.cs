using System.Collections.Concurrent;
using System.Text.Json;

namespace STSAnaliza.Services;

public sealed class SportradarCompetitorIdResolver : ICompetitorIdResolver
{
    private readonly ISportradarTennisClient _client;

    private readonly ConcurrentDictionary<string, string> _map = new(StringComparer.OrdinalIgnoreCase);
    private DateTimeOffset _loadedAtUtc = DateTimeOffset.MinValue;
    private readonly SemaphoreSlim _loadGate = new(1, 1);

    public SportradarCompetitorIdResolver(ISportradarTennisClient client)
    {
        _client = client;
    }

    public async Task<string?> ResolveAsync(string playerName, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(playerName))
            return null;

        await EnsureLoadedAsync(ct);

        foreach (var key in BuildCandidateKeys(playerName))
        {
            if (_map.TryGetValue(key, out var id))
                return id;
        }

        // fallback: "contains" po znormalizowanym kluczu
        var norm = SportradarName.NormalizeKey(playerName);
        foreach (var kv in _map)
        {
            if (kv.Key.Contains(norm, StringComparison.OrdinalIgnoreCase))
                return kv.Value;
        }

        return null;
    }

    private async Task EnsureLoadedAsync(CancellationToken ct)
    {
        // odśwież co 12h
        if (_map.Count > 0 && (DateTimeOffset.UtcNow - _loadedAtUtc) < TimeSpan.FromHours(12))
            return;

        await _loadGate.WaitAsync(ct);
        try
        {
            if (_map.Count > 0 && (DateTimeOffset.UtcNow - _loadedAtUtc) < TimeSpan.FromHours(12))
                return;

            _map.Clear();

            var json = await _client.GetRankingsJsonAsync(ct);
            using var doc = JsonDocument.Parse(json);

            Walk(doc.RootElement);

            _loadedAtUtc = DateTimeOffset.UtcNow;
        }
        finally
        {
            _loadGate.Release();
        }
    }

    private void Walk(JsonElement el)
    {
        switch (el.ValueKind)
        {
            case JsonValueKind.Object:
                if (el.TryGetProperty("competitor", out var comp) && comp.ValueKind == JsonValueKind.Object)
                {
                    if (comp.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.String &&
                        comp.TryGetProperty("name", out var nameEl) && nameEl.ValueKind == JsonValueKind.String)
                    {
                        var id = idEl.GetString();
                        var name = nameEl.GetString();
                        if (!string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(name))
                        {
                            var key = SportradarName.NormalizeKey(name);
                            _map.TryAdd(key, id);
                        }
                    }
                }

                foreach (var p in el.EnumerateObject())
                    Walk(p.Value);
                break;

            case JsonValueKind.Array:
                foreach (var x in el.EnumerateArray())
                    Walk(x);
                break;
        }
    }

    private static IEnumerable<string> BuildCandidateKeys(string input)
    {
        yield return SportradarName.NormalizeKey(input);

        // "Imię Nazwisko" -> "Nazwisko, Imię"
        if (!input.Contains(',') && input.Contains(' '))
            yield return SportradarName.NormalizeKey(ToLastCommaFirst(input));

        // "Nazwisko, Imię" -> "Imię Nazwisko"
        if (input.Contains(','))
            yield return SportradarName.NormalizeKey(ToFirstLast(input));
    }

    private static string ToLastCommaFirst(string s)
    {
        s = s.Trim();
        var parts = s.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2) return s;

        var last = parts[^1];
        var first = string.Join(' ', parts[..^1]);
        return $"{last}, {first}";
    }

    private static string ToFirstLast(string s)
    {
        s = s.Trim();
        var parts = s.Split(',', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2) return s;

        var last = parts[0].Trim();
        var first = parts[1].Trim();
        return $"{first} {last}";
    }
}