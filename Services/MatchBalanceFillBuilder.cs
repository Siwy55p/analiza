using STSAnaliza.Interfejs;
using STSAnaliza.Models;

namespace STSAnaliza.Services;

public sealed class MatchBalanceFillBuilder : IMatchBalanceFillBuilder
{
    private readonly ITennisApiService _tennisApi;
    private readonly IRankService _rank;

    public MatchBalanceFillBuilder(ITennisApiService tennisApi, IRankService rank)
    {
        _tennisApi = tennisApi;
        _rank = rank;
    }

    public async Task<string> BuildByCompetitorIdAsync(string playerName, string competitorId, CancellationToken ct)
    {
        var all = await _tennisApi.GetRecentClosedSinglesMatchesAsync(competitorId, ct, SurfaceResolutionMode.None);

        var now = DateTimeOffset.UtcNow;
        var start10w = now.AddDays(-70);
        var start12m = now.AddMonths(-12);

        var valid = all.Where(m => !IsWalkoverOrEmpty(m.Score)).ToList();

        var m12 = valid.Where(m => m.StartTimeUtc >= start12m).ToList();
        var m10 = valid.Where(m => m.StartTimeUtc >= start10w).ToList();

        string wl12m = m12.Count > 0 ? $"{CountWL(m12).W}-{CountWL(m12).L}" : "0-0";
        string wl10w = m10.Count > 0 ? $"{CountWL(m10).W}-{CountWL(m10).L}" : "0-0";

        var aor12 = await AvgOppRankAsync(m12, ct);
        var aor10 = await AvgOppRankAsync(m10, ct);

        string quality =
    (m12.Count == 0 && m10.Count == 0)
        ? "brak meczów w okresie (12M i 10W)."
        : $"{QualityPart(m12, aor12, "12M")} {QualityPart(m10, aor10, "10W")}".Trim();

        return Format(wl12m, wl10w, quality);
    }

    private static (int W, int L) CountWL(List<PlayerMatchSummary> list)
    {
        int w = 0, l = 0;
        foreach (var m in list)
        {
            if (string.Equals(m.Result, "W", StringComparison.OrdinalIgnoreCase)) w++;
            else if (string.Equals(m.Result, "L", StringComparison.OrdinalIgnoreCase)) l++;
        }
        return (w, l);
    }

    private static string QualityPart(List<PlayerMatchSummary> list, int? aor, string label)
    {
        if (list.Count == 0) return $"{label}: brak meczów.";

        var (w, l) = CountWL(list);
        var total = w + l;
        var wr = total > 0 ? (double)w / total : 0;

        var form =
            wr >= 0.65 ? "bardzo dobry" :
            wr >= 0.55 ? "solidny" :
            wr >= 0.45 ? "mieszany" : "słaby";

        var opp = aor.HasValue ? $" średni ranking ~#{aor.Value}" : "";
        return $"{label}: {form}{opp} (n={total}).";
    }

    private async Task<int?> AvgOppRankAsync(List<PlayerMatchSummary> list, CancellationToken ct)
    {
        var ranks = new List<int>();
        var localCache = new Dictionary<string, int?>(StringComparer.OrdinalIgnoreCase);

        foreach (var m in list)
        {
            if (string.IsNullOrWhiteSpace(m.OpponentId))
                continue;

            if (!localCache.TryGetValue(m.OpponentId, out var r))
            {
                r = await _rank.GetSinglesRankAsync(m.OpponentId, ct);
                localCache[m.OpponentId] = r;
            }

            if (r.HasValue) ranks.Add(r.Value);
        }

        if (ranks.Count < 4) return null;
        return (int)Math.Round(ranks.Average(), 0);
    }

    private static bool IsWalkoverOrEmpty(string score)
    {
        if (string.IsNullOrWhiteSpace(score)) return true;

        var s = score.Trim();
        return s.Contains("WO", StringComparison.OrdinalIgnoreCase)
            || s.Contains("W/O", StringComparison.OrdinalIgnoreCase)
            || s.Contains("WALKOVER", StringComparison.OrdinalIgnoreCase)
            || s.Contains("DEFAULT", StringComparison.OrdinalIgnoreCase);
    }

    private static string Format(string wl12m, string wl10w, string quality)
        => $"12M: {wl12m}{Environment.NewLine}10W: {wl10w}{Environment.NewLine}Jakość bilansu: {quality}";
}