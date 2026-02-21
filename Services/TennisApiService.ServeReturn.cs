using STSAnaliza.Models;
using STSAnaliza.Services.SportradarDtos;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace STSAnaliza.Services;

public sealed partial class TennisApiService
{
    // Serve / return (raw JSON extract)
    // -------------------------
    private sealed record SummaryStats(
        string Id,
        int? GamesWon,
        int? ServiceGamesWon,
        int? FirstServePointsWon,
        int? FirstServeSuccessful,
        int? SecondServePointsWon,
        int? SecondServeSuccessful,
        int? BreakpointsWon,
        int? TotalBreakpoints);

    private static CompetitorStatisticsDto ToDto(SummaryStats s) => new()
    {
        GamesWon = s.GamesWon,
        ServiceGamesWon = s.ServiceGamesWon,
        FirstServePointsWon = s.FirstServePointsWon,
        FirstServeSuccessful = s.FirstServeSuccessful,
        SecondServePointsWon = s.SecondServePointsWon,
        SecondServeSuccessful = s.SecondServeSuccessful,
        BreakpointsWon = s.BreakpointsWon,
        TotalBreakpoints = s.TotalBreakpoints
    };

    private static int? GetInt(JsonElement obj, params string[] keys)
    {
        foreach (var k in keys)
        {
            if (obj.TryGetProperty(k, out var el) &&
                el.ValueKind == JsonValueKind.Number &&
                el.TryGetInt32(out var v))
                return v;
        }
        return null;
    }

    private static bool TryGetByPath(JsonElement el, out JsonElement value, params string[] path)
    {
        value = el;
        foreach (var p in path)
        {
            if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(p, out value))
                return false;
        }
        return true;
    }

    private static bool TryFindCompetitorsArrayRecursive(JsonElement el, out JsonElement comps)
    {
        comps = default;

        if (el.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in el.EnumerateObject())
            {
                if (prop.NameEquals("competitors") && prop.Value.ValueKind == JsonValueKind.Array)
                {
                    comps = prop.Value;
                    return true;
                }

                if (TryFindCompetitorsArrayRecursive(prop.Value, out comps))
                    return true;
            }
        }
        else if (el.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in el.EnumerateArray())
            {
                if (TryFindCompetitorsArrayRecursive(item, out comps))
                    return true;
            }
        }

        return false;
    }

    private static bool TryFindCompetitorsArray(JsonElement root, out JsonElement comps)
    {
        if (TryGetByPath(root, out comps, "sport_event_status", "statistics", "totals", "competitors")) return true;
        if (TryGetByPath(root, out comps, "statistics", "totals", "competitors")) return true;
        if (TryGetByPath(root, out comps, "sport_event_status", "statistics", "competitors")) return true;
        if (TryGetByPath(root, out comps, "statistics", "competitors")) return true;

        return TryFindCompetitorsArrayRecursive(root, out comps);
    }

    private static bool TryExtractServeReturnPair(string json, string competitorId, out CompetitorStatisticsDto me, out CompetitorStatisticsDto opp)
    {
        me = new CompetitorStatisticsDto();
        opp = new CompetitorStatisticsDto();

        competitorId = NormalizeId(competitorId);

        List<SummaryStats> list;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!TryFindCompetitorsArray(doc.RootElement, out var comps) || comps.ValueKind != JsonValueKind.Array)
                return false;

            list = new List<SummaryStats>(capacity: 4);

            foreach (var c in comps.EnumerateArray())
            {
                if (!c.TryGetProperty("id", out var idEl)) continue;
                var id = idEl.GetString();
                if (string.IsNullOrWhiteSpace(id)) continue;

                if (!c.TryGetProperty("statistics", out var st) || st.ValueKind != JsonValueKind.Object)
                    continue;

                var stats = new SummaryStats(
                    Id: NormalizeId(id),
                    GamesWon: GetInt(st, "games_won"),
                    ServiceGamesWon: GetInt(st, "service_games_won"),
                    FirstServePointsWon: GetInt(st, "first_serve_points_won"),
                    FirstServeSuccessful: GetInt(st, "first_serve_successful"),
                    SecondServePointsWon: GetInt(st, "second_serve_points_won"),
                    SecondServeSuccessful: GetInt(st, "second_serve_successful"),
                    BreakpointsWon: GetInt(st, "breakpoints_won", "break_points_won"),
                    TotalBreakpoints: GetInt(st, "total_breakpoints", "total_break_points")
                );

                list.Add(stats);
            }
        }
        catch
        {
            return false;
        }

        var meStats = list.FirstOrDefault(x => string.Equals(x.Id, competitorId, StringComparison.OrdinalIgnoreCase));
        if (meStats is null) return false;

        var oppStats = list.FirstOrDefault(x => !string.Equals(x.Id, competitorId, StringComparison.OrdinalIgnoreCase));
        if (oppStats is null) return false;

        me = ToDto(meStats);
        opp = ToDto(oppStats);
        return true;
    }

    private async Task<ServeReturnMetrics> ComputeServeReturnSinceAsync(
        string competitorId,
        DateTimeOffset sinceUtc,
        int maxEventsToTry,
        int minMatchesWithStats,
        CancellationToken ct)
    {
        var dto = await _client.GetCompetitorSummariesAsync(competitorId, ct);
        var items = dto.Summaries ?? new List<SummaryItem>();

        var ordered = items
            .Where(x => IsFinishedStatus(x.SportEventStatus?.Status))
            .Where(x => x.SportEvent?.StartTime != null && x.SportEvent.StartTime.Value >= sinceUtc)
            .OrderByDescending(x => x.SportEvent!.StartTime!.Value)
            .ToList();

        var agg = new ServeReturnCalculator.Agg();
        int tried = 0;

        foreach (var s in ordered)
        {
            ct.ThrowIfCancellationRequested();
            if (tried >= maxEventsToTry) break;

            var eventId = s.SportEvent?.Id;
            if (string.IsNullOrWhiteSpace(eventId))
                continue;

            tried++;

            var json = await GetEventSummaryJsonCachedAsync(eventId, ct);
            if (string.IsNullOrWhiteSpace(json)) continue;

            if (!TryExtractServeReturnPair(json, competitorId, out var me, out var opp))
                continue;

            ServeReturnCalculator.AddMatch(ref agg, me, opp);

            if (agg.MatchesUsed >= minMatchesWithStats)
                break;
        }

        return ServeReturnCalculator.Finalize(agg);
    }

    private async Task<ServeReturnMetrics> ComputeServeReturnLastNAsync(string competitorId, int n, CancellationToken ct)
    {
        var matches = await GetRecentClosedSinglesMatchesAsync(competitorId, ct, SurfaceResolutionMode.None, Math.Max(n, 20));

        var agg = new ServeReturnCalculator.Agg();

        foreach (var m in matches.Where(x => !string.IsNullOrWhiteSpace(x.SportEventId)).Take(n))
        {
            ct.ThrowIfCancellationRequested();

            var json = await GetEventSummaryJsonCachedAsync(m.SportEventId!, ct);
            if (string.IsNullOrWhiteSpace(json)) continue;

            if (!TryExtractServeReturnPair(json, competitorId, out var me, out var opp))
                continue;

            ServeReturnCalculator.AddMatch(ref agg, me, opp);
        }

        return ServeReturnCalculator.Finalize(agg);
    }

    private async Task<ServeReturnMetrics> ComputeFromMostRecentMatchWithStatsAsync(string competitorId, CancellationToken ct)
    {
        var matches = await GetRecentClosedSinglesMatchesAsync(competitorId, ct, SurfaceResolutionMode.None, 25);

        foreach (var m in matches)
        {
            ct.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(m.SportEventId))
                continue;

            var json = await GetEventSummaryJsonCachedAsync(m.SportEventId!, ct);
            if (string.IsNullOrWhiteSpace(json)) continue;

            if (!TryExtractServeReturnPair(json, competitorId, out var me, out var opp))
                continue;

            var agg = new ServeReturnCalculator.Agg();
            ServeReturnCalculator.AddMatch(ref agg, me, opp);
            return ServeReturnCalculator.Finalize(agg);
        }

        return new ServeReturnMetrics(null, null, null, null, 0, 0);
    }

    // LAST 52 WEEKS overall (B)
    public async Task<(string Fill12_3, string Fill12_4)> BuildFill12_3_12_4_ServeReturn_Last52WeeksOverallAsync(
        string playerBName, string? competitorIdB,
        CancellationToken ct)
    {
        var (serve, ret) = await BuildServeReturn_Last52WeeksOverallCoreAsync(playerBName, competitorIdB, ct);
        return (serve, ret);
    }

    // LAST 52 WEEKS overall (A)
    public async Task<(string Fill11_3, string Fill11_4)> BuildFill11_3_11_4_ServeReturn_Last52WeeksOverallAsync(
        string playerAName, string? competitorIdA,
        CancellationToken ct)
    {
        var (serve, ret) = await BuildServeReturn_Last52WeeksOverallCoreAsync(playerAName, competitorIdA, ct);
        return (serve, ret);
    }

    public async Task<(string Fill11_3, string Fill11_4)> BuildFill11_3_11_4_ServeReturn_Last10Async(
        string playerAName, string? competitorIdA,
        string playerBName, string? competitorIdB,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var idA = !string.IsNullOrWhiteSpace(competitorIdA) ? NormalizeId(competitorIdA) : await _resolver.ResolveAsync(playerAName, ct);
        var idB = !string.IsNullOrWhiteSpace(competitorIdB) ? NormalizeId(competitorIdB) : await _resolver.ResolveAsync(playerBName, ct);

        if (string.IsNullOrWhiteSpace(idA) || string.IsNullOrWhiteSpace(idB))
            return ("brak", "brak");

        var a = await ComputeServeReturnLastNAsync(idA, n: 10, ct);
        var b = await ComputeServeReturnLastNAsync(idB, n: 10, ct);

        return ServeReturnCalculator.BuildPlaceholders(playerAName, playerBName, a, b);
    }

    public async Task<(string Fill11_3, string Fill11_4)> BuildFill11_3_11_4_ServeReturn_MostRecentWithStatsAsync(
        string playerAName, string? competitorIdA,
        string playerBName, string? competitorIdB,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var idA = !string.IsNullOrWhiteSpace(competitorIdA) ? NormalizeId(competitorIdA) : await _resolver.ResolveAsync(playerAName, ct);
        var idB = !string.IsNullOrWhiteSpace(competitorIdB) ? NormalizeId(competitorIdB) : await _resolver.ResolveAsync(playerBName, ct);

        if (string.IsNullOrWhiteSpace(idA) || string.IsNullOrWhiteSpace(idB))
            return ("brak", "brak");

        var taskA = ComputeFromMostRecentMatchWithStatsAsync(idA, ct);
        var taskB = ComputeFromMostRecentMatchWithStatsAsync(idB, ct);
        await Task.WhenAll(taskA, taskB);

        return ServeReturnCalculator.BuildPlaceholders(playerAName, playerBName, await taskA, await taskB);
    }
}