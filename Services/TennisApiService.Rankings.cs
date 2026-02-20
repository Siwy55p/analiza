using STSAnaliza.Services.SportradarDtos;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace STSAnaliza.Services;

public sealed partial class TennisApiService
{
    // Rankings
    // -------------------------
    private async Task<Dictionary<string, RankRow>> GetWorldMapAsync(CancellationToken ct)
        => await GetRankMapCachedAsync(isRace: false, ct);

    private async Task<Dictionary<string, RankRow>> GetRaceMapAsync(CancellationToken ct)
        => await GetRankMapCachedAsync(isRace: true, ct);

    private async Task<Dictionary<string, RankRow>> GetRankMapCachedAsync(bool isRace, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var cache = isRace ? _raceCache : _worldCache;

        if (cache is not null && (now - cache.Value.FetchedAtUtc) < RankingsTtl)
            return cache.Value.Map;

        await _rankGate.WaitAsync(ct);
        try
        {
            cache = isRace ? _raceCache : _worldCache;
            if (cache is not null && (now - cache.Value.FetchedAtUtc) < RankingsTtl)
                return cache.Value.Map;

            RankingsResponseDto dto = isRace
                ? await _client.GetRaceRankingsAsync(ct)
                : await _client.GetRankingsAsync(ct);

            var map = BuildRankMap(dto);

            if (isRace) _raceCache = (now, map);
            else _worldCache = (now, map);

            return map;
        }
        finally
        {
            _rankGate.Release();
        }
    }

    private static Dictionary<string, RankRow> BuildRankMap(RankingsResponseDto dto)
    {
        var map = new Dictionary<string, RankRow>(StringComparer.OrdinalIgnoreCase);

        foreach (var ranking in dto.Rankings ?? Array.Empty<RankingDto>())
        {
            var tour = string.IsNullOrWhiteSpace(ranking.Name) ? "?" : ranking.Name.Trim();

            foreach (var cr in ranking.CompetitorRankings ?? Array.Empty<CompetitorRankingDto>())
            {
                var id = cr.Competitor?.Id;
                if (string.IsNullOrWhiteSpace(id)) continue;

                if (map.ContainsKey(id)) continue;

                var rank = cr.Rank ?? 0;
                var points = cr.Points ?? 0;
                var movement = cr.Movement ?? 0;

                if (rank > 0)
                    map[id] = new RankRow(tour, rank, points, movement);
            }
        }

        return map;
    }

    // wrapper (stara sygnatura)
    public Task<string> BuildFill6_WorldAndRaceAsync(string playerAName, string playerBName, CancellationToken ct)
        => BuildFill6_WorldAndRaceAsync(playerAName, null, playerBName, null, ct);

    public async Task<string> BuildFill6_WorldAndRaceAsync(
        string playerAName, string? competitorIdA,
        string playerBName, string? competitorIdB,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var idA = !string.IsNullOrWhiteSpace(competitorIdA) ? NormalizeId(competitorIdA) : await _resolver.ResolveAsync(playerAName, ct);
        var idB = !string.IsNullOrWhiteSpace(competitorIdB) ? NormalizeId(competitorIdB) : await _resolver.ResolveAsync(playerBName, ct);

        var world = await GetWorldMapAsync(ct);
        var race = await GetRaceMapAsync(ct);

        world.TryGetValue(idA, out var wA);
        world.TryGetValue(idB, out var wB);
        race.TryGetValue(idA, out var rA);
        race.TryGetValue(idB, out var rB);

        var sb = new StringBuilder();
        sb.AppendLine($"{playerAName}: {FormatRow("World", wA)} | {FormatRow("Race", rA)}");
        sb.AppendLine($"{playerBName}: {FormatRow("World", wB)} | {FormatRow("Race", rB)}");
        sb.AppendLine($"Porównanie World: {CompareRanks(wA?.Rank, wB?.Rank, playerAName, playerBName)}");
        sb.AppendLine($"Porównanie Race: {CompareRanks(rA?.Rank, rB?.Rank, playerAName, playerBName)}");
        return sb.ToString().TrimEnd();
    }

    private static string FormatRow(string label, RankRow? row)
    {
        if (row is null) return $"{label} brak";
        return $"{label} {row.Tour} #{row.Rank} ({row.Points} pkt, mv {row.Movement:+#;-#;0})";
    }

    private static string CompareRanks(int? a, int? b, string aName, string bName)
    {
        if (a is null && b is null) return "brak danych";
        if (a is not null && b is null) return $"lepszy {aName} (#{a} vs brak)";
        if (a is null && b is not null) return $"lepszy {bName} (brak vs #{b})";
        if (a == b) return $"remis (#{a})";

        var better = a < b ? aName : bName;
        var diff = Math.Abs(a!.Value - b!.Value);
        return $"lepszy {better} (+{diff} pozycji)";
    }
}