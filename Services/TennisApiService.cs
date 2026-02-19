using Microsoft.Extensions.Logging;
using STSAnaliza.Models;
using STSAnaliza.Services.SportradarDtos;
using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace STSAnaliza.Services;

public sealed partial class TennisApiService : ITennisApiService
{
    private readonly ISportradarTennisClient _client;
    private readonly ICompetitorIdResolver _resolver;
    private readonly ILogger<TennisApiService> _logger;

    // -------------------------
    // TTL / cache
    // -------------------------
    private static readonly TimeSpan RankingsTtl = TimeSpan.FromHours(12);
    private static readonly TimeSpan RecentCacheTtl = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan SeasonSurfaceTtl = TimeSpan.FromHours(24);
    private static readonly TimeSpan EventSummaryTtl = TimeSpan.FromHours(6);
    private static readonly TimeSpan H2HCacheTtl = TimeSpan.FromHours(6);

    // cache: seasonId -> season.info.surface (np. "hardcourt_outdoor")
    private readonly ConcurrentDictionary<string, (DateTimeOffset FetchedAtUtc, string? Surface)> _seasonSurfaceCache
        = new(StringComparer.OrdinalIgnoreCase);

    // cache: competitorId -> full closed matches (posortowane malejąco)
    private readonly ConcurrentDictionary<string, (DateTimeOffset FetchedAtUtc, IReadOnlyList<PlayerMatchSummary> Matches)> _recentCache
        = new(StringComparer.OrdinalIgnoreCase);

    // cache: sportEventId -> parsed DTO (opcjonalnie)
    private readonly ConcurrentDictionary<string, (DateTimeOffset FetchedAtUtc, SportEventSummaryDto Dto)> _eventSummaryCache
        = new(StringComparer.OrdinalIgnoreCase);

    // cache: sportEventId -> raw json (do szybkiej ekstrakcji statów)
    private readonly ConcurrentDictionary<string, (DateTimeOffset FetchedAtUtc, string Json)> _eventSummaryJsonCache
        = new(StringComparer.OrdinalIgnoreCase);

    // rank caches
    private readonly SemaphoreSlim _rankGate = new(1, 1);
    private (DateTimeOffset FetchedAtUtc, Dictionary<string, RankRow> Map)? _worldCache;
    private (DateTimeOffset FetchedAtUtc, Dictionary<string, RankRow> Map)? _raceCache;

    private sealed record RankRow(string Tour, int Rank, int Points, int Movement);

    // h2h cache
    private readonly ConcurrentDictionary<string, (DateTimeOffset FetchedAtUtc, string Text)> _h2hCache
        = new(StringComparer.OrdinalIgnoreCase);

    public TennisApiService(
        ISportradarTennisClient client,
        ICompetitorIdResolver resolver,
        ILogger<TennisApiService> logger)
    {
        _client = client;
        _resolver = resolver;
        _logger = logger;
    }

    // -------------------------
    // Helpers
    // -------------------------
    private static bool IsFinishedStatus(string? status)
        => status is not null &&
           (status.Equals("closed", StringComparison.OrdinalIgnoreCase) ||
            status.Equals("ended", StringComparison.OrdinalIgnoreCase));

    private static string NormalizeId(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return "";
        var s = id.Trim();
        try { s = Uri.UnescapeDataString(s); } catch { /* ignore */ }
        return s;
    }

    // normalizacja: tylko hard/clay/grass/brak
    private static string NormalizeSurface(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "brak";

        var s = raw.Trim().ToLowerInvariant();

        if (s.Contains("hard")) return "hard";
        if (s.Contains("clay")) return "clay";
        if (s.Contains("grass")) return "grass";

        return "brak";
    }

    // -------------------------
    // Season surface (cache)
    // -------------------------
    private async Task<string?> GetSeasonSurfaceCachedAsync(string seasonId, CancellationToken ct)
    {
        var sid = NormalizeId(seasonId);
        if (sid.Length == 0) return null;

        var now = DateTimeOffset.UtcNow;

        if (_seasonSurfaceCache.TryGetValue(sid, out var hit) &&
            (now - hit.FetchedAtUtc) < SeasonSurfaceTtl)
            return hit.Surface;

        try
        {
            var dto = await _client.GetSeasonInfoAsync(sid, ct);
            var surface = dto.Season?.Info?.Surface;

            _seasonSurfaceCache[sid] = (now, surface);
            return surface;
        }
        catch (Exception ex)
        {
            _seasonSurfaceCache[sid] = (now, null);
            _logger.LogWarning(ex, "Nie udało się pobrać SeasonInfo dla {SeasonId}", sid);
            return null;
        }
    }

    // -------------------------
    // Event summary JSON (cache)
    // -------------------------
    private async Task<string?> GetEventSummaryJsonCachedAsync(string sportEventId, CancellationToken ct)
    {
        var eid = NormalizeId(sportEventId);
        if (eid.Length == 0) return null;

        if (_eventSummaryJsonCache.TryGetValue(eid, out var cached) &&
            (DateTimeOffset.UtcNow - cached.FetchedAtUtc) < EventSummaryTtl)
        {
            return cached.Json;
        }

        var json = await _client.GetSportEventSummaryJsonAsync(eid, ct);
        _eventSummaryJsonCache[eid] = (DateTimeOffset.UtcNow, json);
        return json;
    }

    // -------------------------
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
            .ToList();

        // Prefetch season surfaces (żeby nie awaitować w pętli)
        var seasonIds = closed
            .Select(x => (x.SportEventContext ?? x.SportEvent?.SportEventContext)?.Season?.Id)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => NormalizeId(id))
            .Where(id => id.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var seasonSurfaceMap = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var sid in seasonIds)
        {
            ct.ThrowIfCancellationRequested();
            seasonSurfaceMap[sid] = await GetSeasonSurfaceCachedAsync(sid, ct); // np "hardcourt_outdoor"
        }

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

            // A) ctx surface (czasem null)
            var ctxSurfaceRaw = ctx?.SportEventConditions?.Surface?.Name;
            var surface = NormalizeSurface(ctxSurfaceRaw);

            // B) fallback: season.info.surface
            if (surface == "brak")
            {
                var sid = NormalizeId(ctx?.Season?.Id);
                if (sid.Length > 0 && seasonSurfaceMap.TryGetValue(sid, out var seasonSurfaceRaw))
                {
                    surface = NormalizeSurface(seasonSurfaceRaw);
                }
            }

            if (surface == "brak")
            {
                brakCount++;
                _logger.LogInformation(
                    "Surface=brak EV={EventId} ctxSurface={CtxSurface} seasonId={SeasonId}",
                    ev.Id ?? "null",
                    ctxSurfaceRaw ?? "null",
                    NormalizeId(ctx?.Season?.Id) is { Length: > 0 } x ? x : "null");
            }

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

    // -------------------------
    // WTA match meta (surface + indoor/outdoor + format)
    // -------------------------
    public async Task<(string Surface, string IndoorOutdoor, string Format)> GetWtaMatchMetaAsync(
        string playerAName, string? competitorIdA,
        string playerBName, string? competitorIdB,
        DateOnly matchDate,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(competitorIdA) || string.IsNullOrWhiteSpace(competitorIdB))
            return ("brak", "brak", "brak");

        var daily = await _client.GetDailySummariesAsync(matchDate, ct);
        var sportEventId = FindWtaSinglesSportEventId(daily, competitorIdA, competitorIdB);

        if (sportEventId is null)
            return ("brak", "brak", "brak");

        var summary = await _client.GetSportEventSummaryAsync(sportEventId, ct);

        var bestOf = summary.SportEvent?.SportEventContext?.Mode?.BestOf;
        var format = bestOf switch
        {
            3 => "BO3",
            5 => "BO5",
            _ => "brak"
        };

        var seasonId = summary.SportEvent?.SportEventContext?.Season?.Id;

        string? seasonSurface = null;
        if (!string.IsNullOrWhiteSpace(seasonId))
            seasonSurface = await GetSeasonSurfaceCachedAsync(seasonId!, ct);

        var fallbackSurfaceName = GetFallbackSurfaceName(daily, sportEventId);

        var (surface, inout) = ParseSurfaceAndInOut(seasonSurface, fallbackSurfaceName);
        return (surface, inout, format);
    }

    private string? FindWtaSinglesSportEventId(CompetitorSummariesResponse daily, string aId, string bId)
    {
        if (daily.Summaries is null) return null;

        foreach (var s in daily.Summaries)
        {
            var ev = s.SportEvent;
            if (ev?.Id is null) continue;

            var ids = ev.Competitors?
                .Select(c => c.Id)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (ids is null || !ids.Contains(aId) || !ids.Contains(bId))
                continue;

            var ctx = s.SportEventContext ?? ev.SportEventContext;

            var cat = ctx?.Category?.Name;
            var type = ctx?.Competition?.Type;

            if (!string.Equals(cat, "WTA", StringComparison.OrdinalIgnoreCase))
                continue;

            if (!string.Equals(type, "singles", StringComparison.OrdinalIgnoreCase))
                continue;

            return ev.Id;
        }

        return null;
    }

    private static string? GetFallbackSurfaceName(CompetitorSummariesResponse daily, string sportEventId)
    {
        var item = daily.Summaries?.FirstOrDefault(x =>
            string.Equals(x.SportEvent?.Id, sportEventId, StringComparison.OrdinalIgnoreCase));

        var ctx = item?.SportEventContext ?? item?.SportEvent?.SportEventContext;
        return ctx?.SportEventConditions?.Surface?.Name;
    }

    private static (string Surface, string IndoorOutdoor) ParseSurfaceAndInOut(string? seasonSurface, string? fallbackSurfaceName)
    {
        if (!string.IsNullOrWhiteSpace(seasonSurface))
        {
            var s = seasonSurface.Trim().ToLowerInvariant();

            var surf = s.Contains("grass") ? "grass"
                     : s.Contains("clay") ? "clay"
                     : s.Contains("hard") ? "hard"
                     : "brak";

            var io = s.Contains("indoor") ? "indoor"
                   : s.Contains("outdoor") ? "outdoor"
                   : "brak";

            return (surf, io);
        }

        if (!string.IsNullOrWhiteSpace(fallbackSurfaceName))
        {
            var f = fallbackSurfaceName.Trim().ToLowerInvariant();
            var surf = f.Contains("grass") ? "grass"
                     : f.Contains("clay") ? "clay"
                     : f.Contains("hard") ? "hard"
                     : "brak";
            return (surf, "brak");
        }

        return ("brak", "brak");
    }

    // -------------------------
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

    // -------------------------
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

    // -------------------------
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
        var matches = await GetRecentClosedSinglesMatchesAsync(competitorId, ct);

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
        var matches = await GetRecentClosedSinglesMatchesAsync(competitorId, ct);

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

    // -------------------------
    // Score helper
    // -------------------------
    private static string BuildScoreFromMyPerspective(SportEventStatus st, string? myQualifier)
    {
        var sets = st.PeriodScores ?? new List<PeriodScore>();
        if (sets.Count == 0) return "";

        var iAmHome = string.Equals(myQualifier, "home", StringComparison.OrdinalIgnoreCase);

        string FormatSet(PeriodScore p)
        {
            var h = p.HomeScore;
            var a = p.AwayScore;
            if (h is null || a is null) return "";

            int my = iAmHome ? h.Value : a.Value;
            int opp = iAmHome ? a.Value : h.Value;

            if (p.HomeTiebreakScore is not null && p.AwayTiebreakScore is not null)
            {
                int myTb = iAmHome ? p.HomeTiebreakScore.Value : p.AwayTiebreakScore.Value;
                int oppTb = iAmHome ? p.AwayTiebreakScore.Value : p.HomeTiebreakScore.Value;
                return $"{my}-{opp}({myTb}-{oppTb})";
            }

            return $"{my}-{opp}";
        }

        return string.Join(" ", sets.Select(FormatSet).Where(x => !string.IsNullOrWhiteSpace(x)));
    }
}
