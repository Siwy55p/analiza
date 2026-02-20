using STSAnaliza.Interfejs;
using STSAnaliza.Services.SportradarDtos;

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

        competitorId = SportradarId.NormalizeOptional(competitorId);
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

            var dto = await _client.GetRankingsAsync(ct);
            _rankByCompetitorId = BuildRankingsMap(dto);

            _loadedAtUtc = DateTimeOffset.UtcNow;
        }
        finally
        {
            _loadGate.Release();
        }
    }

    private static Dictionary<string, int> BuildRankingsMap(RankingsResponseDto dto)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var ranking in dto.Rankings ?? Array.Empty<RankingDto>())
        {
            foreach (var cr in ranking.CompetitorRankings ?? Array.Empty<CompetitorRankingDto>())
            {
                var id = cr.Competitor?.Id;
                var rank = cr.Rank;

                if (string.IsNullOrWhiteSpace(id) || rank is null || rank.Value <= 0)
                    continue;

                // jeśli duplikat – nie nadpisuj
                map.TryAdd(id, rank.Value);
            }
        }

        return map;
    }
}
