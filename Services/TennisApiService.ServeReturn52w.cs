using Microsoft.Extensions.Logging;
using STSAnaliza.Interfejs;
using System.Globalization;

namespace STSAnaliza.Services;

public sealed partial class TennisApiService
{
    private const string DefaultServeFill = "hold%: brak\n1st won%: brak\n2nd serve points won%: brak";
    private const string DefaultReturnFill = "break%: brak";

    /// <summary>
    /// Wspólny core dla LAST 52 WEEKS overall (A i B). Ujednolica format i minimalizuje duplikację kodu.
    /// </summary>
    private async Task<(string ServeFill, string ReturnFill)> BuildServeReturn_Last52WeeksOverallCoreAsync(
        string playerName,
        string? competitorId,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var id = !string.IsNullOrWhiteSpace(competitorId)
            ? NormalizeId(competitorId)
            : await _resolver.ResolveAsync(playerName, ct);

        if (string.IsNullOrWhiteSpace(id))
            return (DefaultServeFill, DefaultReturnFill);

        var since = DateTimeOffset.UtcNow.AddDays(-365);

        var metrics = await ComputeServeReturnSinceAsync(
            id, since,
            maxEventsToTry: 35,
            minMatchesWithStats: 8,
            ct);

        // jeśli w 52 tyg. nic nie wyszło – spróbuj najświeższego meczu ze statami (bez limitu daty)
        var hasAny =
            metrics.HoldPct is not null ||
            metrics.FirstWonPct is not null ||
            metrics.SecondWonPct is not null ||
            (metrics.BreakPct is not null && metrics.TotalBreakpoints > 0);

        if (!hasAny)
        {
            _logger.LogInformation("Serve/Return 52W: brak danych dla {Id} -> fallback do most recent match with stats.", id);
            metrics = await ComputeFromMostRecentMatchWithStatsAsync(id, ct);
        }

        static string Pct(double? x) => x is null
            ? "brak"
            : (x.Value * 100).ToString("0.0", CultureInfo.InvariantCulture) + "%";

        // Ujednolicony format (zgodny z fallbackami w Form1): hold / 1st / 2nd
        var serve =
            $"hold%: {Pct(metrics.HoldPct)}\n" +
            $"1st won%: {Pct(metrics.FirstWonPct)}\n" +
            $"2nd serve points won%: {Pct(metrics.SecondWonPct)}";

        var ret =
            metrics.BreakPct is null || metrics.TotalBreakpoints <= 0
                ? DefaultReturnFill
                : $"break%: {Pct(metrics.BreakPct)} ({metrics.BreakpointsWon}/{metrics.TotalBreakpoints})";

        return (serve, ret);
    }
}
