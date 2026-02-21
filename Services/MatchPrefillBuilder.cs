using Microsoft.Extensions.Logging;
using STSAnaliza.Interfejs;
using STSAnaliza.Models;
using System.Globalization;
using System.Text.RegularExpressions;

namespace STSAnaliza.Services;

public sealed class MatchPrefillBuilder : IMatchPrefillBuilder
{
    private readonly ITennisApiService _tennisApi;
    private readonly ISportradarDailyMatchResolver _dailyResolver;
    private readonly IMatchRawJsonBuilder _matchRawJsonBuilder;
    private readonly IMatchBalanceFillBuilder _balanceBuilder;
    private readonly IWtaEloService _eloService;
    private readonly ILogger<MatchPrefillBuilder> _logger;

    public MatchPrefillBuilder(
        ITennisApiService tennisApi,
        ISportradarDailyMatchResolver dailyResolver,
        IMatchRawJsonBuilder matchRawJsonBuilder,
        IMatchBalanceFillBuilder balanceBuilder,
        IWtaEloService eloService,
        ILogger<MatchPrefillBuilder> logger)
    {
        _tennisApi = tennisApi;
        _dailyResolver = dailyResolver;
        _matchRawJsonBuilder = matchRawJsonBuilder;
        _balanceBuilder = balanceBuilder;
        _eloService = eloService;
        _logger = logger;
    }

    public async Task<MatchPrefillResult> BuildAsync(MatchListItem m, CancellationToken ct, Action<string>? log = null)
    {
        ct.ThrowIfCancellationRequested();

        string? aId = null;
        string? bId = null;

        DateOnly? matchDate = null;
        var hasDate = TryParseDateOnly(m.Day, out var d);
        if (hasDate)
            matchDate = d;

        if (hasDate)
        {
            try
            {
                (aId, bId) = await _dailyResolver.TryResolveCompetitorIdsAsync(
                    d, m.PlayerA, m.PlayerB, ct);

                log?.Invoke($"  [AUTO] competitorId: A={aId ?? "brak"}, B={bId ?? "brak"}");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Daily resolver failed for {Day} {A} vs {B}", m.Day, m.PlayerA, m.PlayerB);
                log?.Invoke($"  [WARN] Nie udało się rozwiązać competitorId (daily): {ex.Message}");
            }
        }
        else
        {
            log?.Invoke($"  [WARN] Nie umiem sparsować daty: '{m.Day}' -> lecę fallback po nazwie.");
        }

        // ------- RAW (ostatnie mecze) -------
        var jsonA = aId is not null
            ? await _matchRawJsonBuilder.BuildByCompetitorIdAsync(m.PlayerA, aId, ct)
            : await _matchRawJsonBuilder.BuildAsync(m.PlayerA, ct);

        var jsonB = bId is not null
            ? await _matchRawJsonBuilder.BuildByCompetitorIdAsync(m.PlayerB, bId, ct)
            : await _matchRawJsonBuilder.BuildAsync(m.PlayerB, ct);

        // ------- META (surface/indoor/format) -------
        (string surface, string indoorOutdoor, string format) = ("brak", "brak", "brak");

        if (hasDate)
        {
            try
            {
                using var metaCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                metaCts.CancelAfter(TimeSpan.FromSeconds(20));

                (surface, indoorOutdoor, format) = await _tennisApi.GetWtaMatchMetaAsync(
                    m.PlayerA, aId,
                    m.PlayerB, bId,
                    d,
                    metaCts.Token);

                log?.Invoke($"  [AUTO] META: {surface}, {indoorOutdoor}, {format}");
            }
            catch (Exception ex)
            {
                log?.Invoke($"  [WARN] META niedostępne: {ex.Message}");
            }
        }

        // ------- Elo (TennisAbstract WTA) -------
        string fill_7;
        try
        {
            using var eloCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            eloCts.CancelAfter(TimeSpan.FromSeconds(20));

            log?.Invoke("  [AUTO] Pobieram Elo (WTA Elo: TA -> Kick-Serve fallback)");
            fill_7 = await _eloService.BuildFill7Async(m.PlayerA, m.PlayerB, surface, eloCts.Token, log);
            log?.Invoke("  [AUTO] Elo OK.");
        }
        catch (Exception ex)
        {
            log?.Invoke($"  [WARN] Elo niedostępne: {ex.Message}");
            fill_7 = string.Join(Environment.NewLine,
                "Elo_A (overall): n/a",
                "Elo_B (overall): n/a",
                "Elo_A (surface): n/a",
                "Elo_B (surface): n/a",
                "ΔElo (surface): n/a");
        }

        // ------- Bilans -------
        var fill11_2 = (aId is not null)
            ? await _balanceBuilder.BuildByCompetitorIdAsync(m.PlayerA, aId, ct)
            : "12M: brak danych\n10W: brak danych\nJakość bilansu: brak danych";

        var fill12_2 = (bId is not null)
            ? await _balanceBuilder.BuildByCompetitorIdAsync(m.PlayerB, bId, ct)
            : "12M: brak danych\n10W: brak danych\nJakość bilansu: brak danych";

        // ------- Rankingi -------
        string fill_6;
        try
        {
            using var rankCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            rankCts.CancelAfter(TimeSpan.FromSeconds(20));

            log?.Invoke("  [AUTO] Pobieram rankingi (World/Race)...");
            fill_6 = await _tennisApi.BuildFill6_WorldAndRaceAsync(
                m.PlayerA, aId,
                m.PlayerB, bId,
                rankCts.Token);

            log?.Invoke("  [AUTO] Rankingi OK.");
        }
        catch (Exception ex)
        {
            log?.Invoke($"  [WARN] Rankingi niedostępne: {ex.Message}");
            fill_6 = "Brak danych";
        }

        // ------- H2H -------
        string fill_13;
        try
        {
            using var h2hCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            h2hCts.CancelAfter(TimeSpan.FromSeconds(20));

            fill_13 = (aId is not null && bId is not null)
                ? await _tennisApi.BuildFill13_H2H_Last12MonthsAsync(m.PlayerA, aId, m.PlayerB, bId, h2hCts.Token)
                : "H2H (12M): brak danych";
        }
        catch (Exception ex)
        {
            log?.Invoke($"  [WARN] H2H niedostępne: {ex.Message}");
            fill_13 = "H2H (12M): brak danych";
        }

        // ------- Serve/Return -------
        string fill12_3 = "hold%: brak\n1st won%: brak\n2nd serve points won%: brak";
        string fill12_4 = "break%: brak";

        try
        {
            using var srCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            srCts.CancelAfter(TimeSpan.FromSeconds(120));

            log?.Invoke("  [AUTO] Serwis/Return B (last 52 weeks overall)...");
            (fill12_3, fill12_4) = await _tennisApi.BuildFill12_3_12_4_ServeReturn_Last52WeeksOverallAsync(
                m.PlayerB, bId, srCts.Token);

            log?.Invoke("  [AUTO] Serwis/Return B OK.");
        }
        catch (Exception ex)
        {
            log?.Invoke($"  [WARN] Serwis/Return B niedostępne: {ex.Message}");
        }

        string fill11_3 = "hold%: brak\n1st won%: brak\n2nd serve points won%: brak";
        string fill11_4 = "break%: brak";

        try
        {
            using var aSrvCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            aSrvCts.CancelAfter(TimeSpan.FromSeconds(120));

            log?.Invoke("  [AUTO] Serwis/Return A (last 52 weeks overall)...");
            (fill11_3, fill11_4) = await _tennisApi.BuildFill11_3_11_4_ServeReturn_Last52WeeksOverallAsync(
                m.PlayerA, aId, aSrvCts.Token);

            log?.Invoke("  [AUTO] Serwis/Return A OK.");
        }
        catch (Exception ex)
        {
            log?.Invoke($"  [WARN] Serwis/Return A niedostępne: {ex.Message}");
        }

        var prefilled = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["<<FILL_3>>"] = surface,
            ["<<FILL_4>>"] = indoorOutdoor,
            ["<<FILL_5>>"] = format,
            ["<<FILL_6>>"] = fill_6,
            ["<<FILL_7>>"] = fill_7,

            ["<<FILL_11_1>>"] = jsonA,
            ["<<FILL_11_2>>"] = fill11_2,
            ["<<FILL_11_3>>"] = fill11_3,
            ["<<FILL_11_4>>"] = fill11_4,

            ["<<FILL_12_1>>"] = jsonB,
            ["<<FILL_12_2>>"] = fill12_2,
            ["<<FILL_12_3>>"] = fill12_3,
            ["<<FILL_12_4>>"] = fill12_4,

            ["<<FILL_13>>"] = fill_13
        };

        return new MatchPrefillResult
        {
            Prefilled = prefilled,
            CompetitorIdA = aId,
            CompetitorIdB = bId,
            MatchDate = matchDate
        };
    }

    // Lokalnie (żeby serwis był samowystarczalny) – formaty jak w UI.
    private static bool TryParseDateOnly(string? s, out DateOnly date)
    {
        date = default;
        if (string.IsNullOrWhiteSpace(s)) return false;

        var formats = new[] { "yyyy-MM-dd", "dd/MM/yyyy", "dd.MM.yyyy", "dd-MM-yyyy" };
        if (DateTime.TryParseExact(s.Trim(), formats, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var dt))
        {
            date = DateOnly.FromDateTime(dt);
            return true;
        }

        if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
        {
            date = DateOnly.FromDateTime(dt);
            return true;
        }

        return false;
    }
}