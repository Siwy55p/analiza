using STSAnaliza.Services.SportradarDtos;
using System.Collections.Generic;
using System.Linq;

namespace STSAnaliza.Services;

public sealed partial class TennisApiService
{
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
}