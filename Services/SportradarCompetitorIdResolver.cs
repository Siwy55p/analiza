using STSAnaliza.Interfejs;
using STSAnaliza.Services.SportradarDtos;
using System.Collections.Concurrent;

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

            // DTO (w środku klient cache'uje JSON i DTO)
            var dto = await _client.GetRankingsAsync(ct);
            FillMap(dto);

            _loadedAtUtc = DateTimeOffset.UtcNow;
        }
        finally
        {
            _loadGate.Release();
        }
    }

    private void FillMap(RankingsResponseDto dto)
    {
        foreach (var ranking in dto.Rankings ?? Array.Empty<RankingDto>())
        {
            foreach (var cr in ranking.CompetitorRankings ?? Array.Empty<CompetitorRankingDto>())
            {
                var id = cr.Competitor?.Id;
                var name = cr.Competitor?.Name;

                if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name))
                    continue;

                var key = SportradarName.NormalizeKey(name);
                _map.TryAdd(key, id);
            }
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
