using STSAnaliza.Interfejs;
using STSAnaliza.Services.SportradarDtos;
using System.Collections.Generic;

namespace STSAnaliza.Services;

public sealed class SportradarDailyMatchResolver : ISportradarDailyMatchResolver
{
    private readonly ISportradarTennisClient _client;

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

        var aTokens = SportradarName.Tokens(playerAName);
        var bTokens = SportradarName.Tokens(playerBName);

        // ✅ cache surowego JSON / DTO jest w SportradarTennisClient (IMemoryCache)
        var daily = await _client.GetDailySummariesAsync(date, ct);

        foreach (var summary in daily.Summaries ?? new List<SummaryItem>())
        {
            ct.ThrowIfCancellationRequested();

            var ev = summary.SportEvent;
            if (ev is null) continue;

            // Preferuj kontekst z summary, fallback do tego w sport_event
            var ctx = summary.SportEventContext ?? ev.SportEventContext;
            var type = ctx?.Competition?.Type;

            // STS lista to single -> filtrujemy doubles, żeby nie złapać par
            if (!string.IsNullOrWhiteSpace(type) &&
                !string.Equals(type, "singles", StringComparison.OrdinalIgnoreCase))
                continue;

            var comps = ev.Competitors;
            if (comps is null || comps.Count != 2) continue;

            string? aId = null;
            string? bId = null;

            foreach (var c in comps)
            {
                if (string.IsNullOrWhiteSpace(c?.Id) || string.IsNullOrWhiteSpace(c.Name))
                    continue;

                var t = SportradarName.Tokens(c.Name);

                if (aId is null && IsSubset(aTokens, t))
                    aId = c.Id;

                if (bId is null && IsSubset(bTokens, t))
                    bId = c.Id;
            }

            if (aId is not null && bId is not null)
                return (aId, bId);
        }

        return (null, null);
    }

    private static bool IsSubset(HashSet<string> need, HashSet<string> have)
    {
        foreach (var x in need)
            if (!have.Contains(x))
                return false;

        return true;
    }
}