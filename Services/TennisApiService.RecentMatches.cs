using Microsoft.Extensions.Logging;
using STSAnaliza.Models;
using STSAnaliza.Services.SportradarDtos;
using System.Collections.Generic;
using System.Linq;

namespace STSAnaliza.Services;

public sealed partial class TennisApiService
{
    // Recent matches (closed singles) + surface
    // -------------------------
    public async Task<IReadOnlyList<PlayerMatchSummary>> GetRecentClosedSinglesMatchesAsync(string competitorId, CancellationToken ct)
    {
        var cid = NormalizeId(competitorId);
        if (cid.Length == 0)
            return Array.Empty<PlayerMatchSummary>();

        if (_recentCache.TryGetValue(cid, out var cached) &&
            (DateTimeOffset.UtcNow - cached.FetchedAtUtc) < RecentCacheTtl)
        {
            return cached.Matches;
        }

        var dto = await _client.GetCompetitorSummariesAsync(cid, ct);

        var all = await ParseClosedSinglesMatchesAsync(dto, cid, ct);

        _recentCache[cid] = (DateTimeOffset.UtcNow, all);

        _logger.LogInformation("Sportradar: pobrano {Count} meczów (recent) dla {CompetitorId}",
            all.Count, cid);

        var surfaceStats = all.GroupBy(x => x.Surface ?? "brak")
                              .ToDictionary(g => g.Key, g => g.Count());
        _logger.LogInformation("Surface stats for {CompetitorId}: {Stats}",
            cid, string.Join(", ", surfaceStats.Select(kv => $"{kv.Key}:{kv.Value}")));

        return all;
    }

    public async Task<IReadOnlyList<PlayerMatchSummary>> GetLast10MatchesAsync(string competitorId, CancellationToken ct)
    {
        var all = await GetRecentClosedSinglesMatchesAsync(competitorId, ct);
        var last10 = all.Take(10).ToList();

        _logger.LogInformation("Sportradar: pobrano {Count} ostatnich meczów dla {CompetitorId}",
            last10.Count, NormalizeId(competitorId));

        return last10;
    }

    public async Task<IReadOnlyList<PlayerMatchSummary>> GetLast10MatchesByNameAsync(string playerName, CancellationToken ct)
    {
        var id = await _resolver.ResolveAsync(playerName, ct);
        if (string.IsNullOrWhiteSpace(id))
        {
            _logger.LogWarning("Sportradar: nie znaleziono competitorId dla '{Name}'", playerName);
            return Array.Empty<PlayerMatchSummary>();
        }

        return await GetLast10MatchesAsync(id, ct);
    }

    public async Task<(IReadOnlyList<PlayerMatchSummary> A, IReadOnlyList<PlayerMatchSummary> B)> GetLast10MatchesForBothAsync(string competitorIdA, string competitorIdB, CancellationToken ct)
    {
        var taskA = GetLast10MatchesAsync(competitorIdA, ct);
        var taskB = GetLast10MatchesAsync(competitorIdB, ct);

        await Task.WhenAll(taskA, taskB);
        return (await taskA, await taskB);
    }

    private async Task<List<PlayerMatchSummary>> ParseClosedSinglesMatchesAsync(
        CompetitorSummariesResponse dto,
        string competitorId,
        CancellationToken ct)
    {
        var items = dto.Summaries ?? new List<SummaryItem>();

        var closed = items
            .Where(x => IsFinishedStatus(x.SportEventStatus?.Status))
            .Where(x => x.SportEvent?.StartTime != null)
            .OrderByDescending(x => x.SportEvent!.StartTime!.Value)
            .Take(MaxClosedMatchesToParse) // ✅ limit = mniej sezonów = mniej requestów
            .ToList();

        var seasonSurfaceMap = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        var result = new List<PlayerMatchSummary>(capacity: closed.Count);
        int brakCount = 0;

        foreach (var s in closed)
        {
            ct.ThrowIfCancellationRequested();

            var ev = s.SportEvent;
            var st = s.SportEventStatus;
            if (ev?.StartTime is null || st is null) continue;

            var competitors = ev.Competitors ?? new List<Competitor>();

            // SINGLES: musi być dokładnie 2 competitorów
            if (competitors.Count != 2) continue;

            var me = competitors.FirstOrDefault(c => string.Equals(c.Id, competitorId, StringComparison.OrdinalIgnoreCase));
            var opp = competitors.FirstOrDefault(c => !string.Equals(c.Id, competitorId, StringComparison.OrdinalIgnoreCase));
            if (opp is null) continue;

            if (string.IsNullOrWhiteSpace(st.WinnerId))
                continue;

            var opponentName = opp.Name ?? "Unknown";
            var opponentId = opp.Id ?? "";

            var isWin = string.Equals(st.WinnerId, competitorId, StringComparison.OrdinalIgnoreCase);
            var score = BuildScoreFromMyPerspective(st, myQualifier: me?.Qualifier);

            var ctx = s.SportEventContext ?? ev.SportEventContext;

            // A) ctx surface (jeśli jest)
            var ctxSurfaceRaw = ctx?.SportEventConditions?.Surface?.Name;
            var surface = NormalizeSurface(ctxSurfaceRaw);

            // B) fallback: season.info.surface — tylko jeśli surface brak (lazy)
            if (surface == "brak")
            {
                var sid = NormalizeId(ctx?.Season?.Id);
                if (sid.Length > 0)
                {
                    if (!seasonSurfaceMap.TryGetValue(sid, out var seasonSurfaceRaw))
                    {
                        seasonSurfaceRaw = await GetSeasonSurfaceCachedAsync(sid, ct);
                        seasonSurfaceMap[sid] = seasonSurfaceRaw;
                    }

                    surface = NormalizeSurface(seasonSurfaceRaw);
                }
            }

            if (surface == "brak")
                brakCount++;

            result.Add(new PlayerMatchSummary(
                StartTimeUtc: ev.StartTime.Value,
                OpponentId: opponentId,
                OpponentName: opponentName,
                Result: isWin ? "W" : "L",
                Score: score,
                Surface: surface,
                SportEventId: ev.Id
            ));
        }

        _logger.LogInformation("ParseClosedSinglesMatchesAsync: surface 'brak' = {Missing}/{Total} dla {CompetitorId}",
            brakCount, result.Count, competitorId);

        return result;
    }
}