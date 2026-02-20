using Microsoft.Extensions.Logging;
using STSAnaliza.Interfejs;
using STSAnaliza.Models;
using STSAnaliza.Services.SportradarDtos;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

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

    private const int MaxClosedMatchesToParse = 80;

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
        => SportradarId.NormalizeOptional(id);

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