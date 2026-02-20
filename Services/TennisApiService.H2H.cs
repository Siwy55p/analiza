using STSAnaliza.Services.SportradarDtos;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace STSAnaliza.Services;

public sealed partial class TennisApiService
{
    // H2H last 12 months
    // -------------------------
    public async Task<string> BuildFill13_H2H_Last12MonthsAsync(
        string playerAName, string? competitorIdA,
        string playerBName, string? competitorIdB,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(competitorIdA) || string.IsNullOrWhiteSpace(competitorIdB))
            return "H2H (12M): brak danych (brak competitorId)";

        var idA = NormalizeId(competitorIdA);
        var idB = NormalizeId(competitorIdB);

        var cacheKey = $"{idA}|{idB}";
        if (_h2hCache.TryGetValue(cacheKey, out var cached) &&
            (DateTimeOffset.UtcNow - cached.FetchedAtUtc) < H2HCacheTtl)
        {
            return cached.Text;
        }

        var dto = await _client.GetCompetitorVersusSummariesAsync(idA, idB, ct);
        var since = DateTimeOffset.UtcNow.AddMonths(-12);

        var rows = new List<(DateTimeOffset Start, bool IsWinA, string ScoreA)>();

        foreach (var s in dto.Summaries ?? new List<SummaryItem>())
        {
            ct.ThrowIfCancellationRequested();

            if (!IsFinishedStatus(s.SportEventStatus?.Status)) continue;

            var ev = s.SportEvent;
            var st = s.SportEventStatus;
            if (ev?.StartTime is null || st is null) continue;

            var start = ev.StartTime.Value;
            if (start < since) continue;

            var comps = ev.Competitors ?? new List<Competitor>();
            if (comps.Count < 2) continue;

            var hasA = comps.Any(c => string.Equals(c.Id, idA, StringComparison.OrdinalIgnoreCase));
            var hasB = comps.Any(c => string.Equals(c.Id, idB, StringComparison.OrdinalIgnoreCase));
            if (!hasA || !hasB) continue;

            if (string.IsNullOrWhiteSpace(st.WinnerId)) continue;

            var isWinA = string.Equals(st.WinnerId, idA, StringComparison.OrdinalIgnoreCase);

            var meA = comps.FirstOrDefault(c => string.Equals(c.Id, idA, StringComparison.OrdinalIgnoreCase));
            var scoreA = BuildScoreFromMyPerspective(st, myQualifier: meA?.Qualifier);

            rows.Add((start, isWinA, scoreA));
        }

        rows = rows.OrderByDescending(x => x.Start).ToList();

        string result;
        if (rows.Count == 0)
        {
            result = "H2H (12M): brak meczów";
        }
        else
        {
            var sb = new StringBuilder();
            sb.AppendLine("H2H (12M):");
            foreach (var r in rows)
            {
                var wl = r.IsWinA ? "W" : "L";
                sb.AppendLine($"{r.Start:yyyy-MM-dd}: {wl} {r.ScoreA}");
            }
            result = sb.ToString().TrimEnd();
        }

        _h2hCache[cacheKey] = (DateTimeOffset.UtcNow, result);
        return result;
    }
}