using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using STSAnaliza.Interfejs;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace STSAnaliza.Services;

public sealed class WtaEloService : IWtaEloService
{
    // =========================
    // Source #1 (Primary): TennisAbstract WTA Elo Ratings
    // =========================
    private const string TaWtaEloUrl = "reports/wta_elo_ratings.html";
    private const string CacheKey_TA = "TA_WTA_ELO_SNAPSHOT_V3";

    // =========================
    // Source #2 (Fallback): Kick-Serve WTA Elo
    // =========================
    private const string KsWtaEloUrl = "https://kick-serve.com/tdata/wta_elo.html";
    private const string CacheKey_KS = "KS_WTA_ELO_SNAPSHOT_V3";

    // TTL: 2h
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(2);

    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;
    private readonly ILogger<WtaEloService> _logger;

    private readonly SemaphoreSlim _taGate = new(1, 1);
    private readonly SemaphoreSlim _ksGate = new(1, 1);

    public WtaEloService(HttpClient http, IMemoryCache cache, ILogger<WtaEloService> logger)
    {
        _http = http;
        _cache = cache;
        _logger = logger;
    }

    public async Task<string> BuildFill7Async(
        string playerA_LastFirst,
        string playerB_LastFirst,
        string? surface,
        CancellationToken ct,
        Action<string>? log = null)
    {
        var surfaceKey = NormalizeSurface(surface);
        var na = BuildFill7_AllNa();

        log?.Invoke($"[ELO] START WTA | surface='{surface ?? "brak"}' -> key='{surfaceKey}'");
        log?.Invoke($"[ELO] A='{playerA_LastFirst}' | {DbgName(playerA_LastFirst)}");
        log?.Invoke($"[ELO] B='{playerB_LastFirst}' | {DbgName(playerB_LastFirst)}");

        // 1) TennisAbstract
        try
        {
            var (ta, taFromCache) = await GetTaSnapshotAsync(ct, log);
            log?.Invoke($"[ELO][TA] lastUpdate='{ta.LastUpdate}', rows={ta.Rows.Count}, cache={(taFromCache ? "HIT" : "MISS")}");

            if (!string.IsNullOrWhiteSpace(ta.LastUpdate))
            {
                var aTa = FindTaRow(ta.Rows, playerA_LastFirst);
                var bTa = FindTaRow(ta.Rows, playerB_LastFirst);

                log?.Invoke($"[ELO][TA] foundA={(aTa is not null)}, foundB={(bTa is not null)}");

                if (aTa is null)
                    log?.Invoke($"[ELO][TA] A not found. Suggestions: {SuggestTa(ta.Rows, playerA_LastFirst)}");
                if (bTa is null)
                    log?.Invoke($"[ELO][TA] B not found. Suggestions: {SuggestTa(ta.Rows, playerB_LastFirst)}");

                if (aTa is not null && bTa is not null)
                {
                    log?.Invoke($"[ELO] SOURCE=TA lastUpdate={ta.LastUpdate}");

                    return BuildFill7FromOverallAndSurface(
                        overallA: aTa.EloOverall,
                        overallB: bTa.EloOverall,
                        surfaceA: GetSurfaceTa(aTa, surfaceKey),
                        surfaceB: GetSurfaceTa(bTa, surfaceKey));
                }
            }
            else
            {
                log?.Invoke("[ELO][TA] Brak 'Last update' -> TA odrzucone.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "TA Elo failed.");
            log?.Invoke($"[ELO][TA] ERROR: {ex.Message}");
        }

        // 2) Kick-Serve
        try
        {
            var (ks, ksFromCache) = await GetKsSnapshotAsync(ct, log);
            log?.Invoke($"[ELO][KS] updated='{ks.Updated}', rows={ks.Rows.Count}, cache={(ksFromCache ? "HIT" : "MISS")}");

            if (!string.IsNullOrWhiteSpace(ks.Updated))
            {
                var aKs = FindKsRow(ks.Rows, playerA_LastFirst);
                var bKs = FindKsRow(ks.Rows, playerB_LastFirst);

                log?.Invoke($"[ELO][KS] foundA={(aKs is not null)}, foundB={(bKs is not null)}");

                if (aKs is null)
                    log?.Invoke($"[ELO][KS] A not found. Suggestions: {SuggestKs(ks.Rows, playerA_LastFirst)}");
                if (bKs is null)
                    log?.Invoke($"[ELO][KS] B not found. Suggestions: {SuggestKs(ks.Rows, playerB_LastFirst)}");

                if (aKs is not null && bKs is not null)
                {
                    log?.Invoke($"[ELO] SOURCE=Kick-Serve updated={ks.Updated}");

                    return BuildFill7FromOverallAndSurface(
                        overallA: aKs.Elo,
                        overallB: bKs.Elo,
                        surfaceA: GetSurfaceKs(aKs, surfaceKey),
                        surfaceB: GetSurfaceKs(bKs, surfaceKey));
                }
            }
            else
            {
                log?.Invoke("[ELO][KS] Brak 'Updated' -> Kick-Serve odrzucone.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Kick-Serve Elo failed.");
            log?.Invoke($"[ELO][KS] ERROR: {ex.Message}");
        }

        log?.Invoke("[ELO] RESULT = n/a (brak kompletu A+B z jednego źródła)");
        return na;
    }

    // =========================
    // OUTPUT (5 lines)
    // =========================

    private static string BuildFill7FromOverallAndSurface(double overallA, double overallB, double? surfaceA, double? surfaceB)
    {
        var s1 = $"Elo_A (overall): {Fmt1(overallA)}";
        var s2 = $"Elo_B (overall): {Fmt1(overallB)}";

        var s3 = $"Elo_A (surface): {(surfaceA is null ? "n/a" : Fmt1(surfaceA.Value))}";
        var s4 = $"Elo_B (surface): {(surfaceB is null ? "n/a" : Fmt1(surfaceB.Value))}";

        var s5 = (surfaceA is null || surfaceB is null)
            ? "ΔElo (surface): n/a"
            : $"ΔElo (surface): {Fmt1(surfaceA.Value - surfaceB.Value)}";

        return string.Join(Environment.NewLine, s1, s2, s3, s4, s5);
    }

    private static string BuildFill7_AllNa()
        => string.Join(Environment.NewLine,
            "Elo_A (overall): n/a",
            "Elo_B (overall): n/a",
            "Elo_A (surface): n/a",
            "Elo_B (surface): n/a",
            "ΔElo (surface): n/a");

    // =========================
    // SOURCE #1: TennisAbstract
    // =========================

    private async Task<(TaSnapshot snap, bool fromCache)> GetTaSnapshotAsync(CancellationToken ct, Action<string>? log)
    {
        if (_cache.TryGetValue(CacheKey_TA, out TaSnapshot? cached) && cached is not null)
            return (cached, true);

        await _taGate.WaitAsync(ct);
        try
        {
            if (_cache.TryGetValue(CacheKey_TA, out cached) && cached is not null)
                return (cached, true);

            log?.Invoke("[ELO][TA] Fetching TA wta_elo_ratings.html ...");

            using var req = new HttpRequestMessage(HttpMethod.Get, TaWtaEloUrl);
            req.Headers.TryAddWithoutValidation("User-Agent", "STSAnaliza/1.0 (+WinForms)");
            req.Headers.TryAddWithoutValidation("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");

            using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
            resp.EnsureSuccessStatusCode();

            var html = await resp.Content.ReadAsStringAsync(ct);
            var snap = ParseTaSnapshot(html);

            _cache.Set(CacheKey_TA, snap, CacheTtl);
            return (snap, false);
        }
        finally
        {
            _taGate.Release();
        }
    }

    private static readonly Regex TaLastUpdateRegex =
        new(@"(?i)Last update:\s*([0-9]{4}-[0-9]{2}-[0-9]{2})", RegexOptions.Compiled);

    private static readonly Regex TaTableRegex =
        new(@"(?is)<table[^>]*\bid\s*=\s*[""']reportable[""'][^>]*>(.*?)</table>", RegexOptions.Compiled);

    private static readonly Regex TaTheadRegex =
        new(@"(?is)<thead[^>]*>(.*?)</thead>", RegexOptions.Compiled);

    private static readonly Regex TaTbodyRegex =
        new(@"(?is)<tbody[^>]*>(.*?)</tbody>", RegexOptions.Compiled);

    private static readonly Regex TaThRegex =
        new(@"(?is)<th[^>]*>(.*?)</th>", RegexOptions.Compiled);

    private static readonly Regex TaTrRegex =
        new(@"(?is)<tr[^>]*>(.*?)</tr>", RegexOptions.Compiled);

    private static readonly Regex TaTdRegex =
        new(@"(?is)<t[dh][^>]*>(.*?)</t[dh]>", RegexOptions.Compiled);

    private static readonly Regex TagsRegex =
        new(@"(?is)<[^>]+>", RegexOptions.Compiled);

    private static TaSnapshot ParseTaSnapshot(string html)
    {
        html ??= "";

        var upd = "";
        var mUpd = TaLastUpdateRegex.Match(html);
        if (mUpd.Success) upd = mUpd.Groups[1].Value.Trim();

        var mTable = TaTableRegex.Match(html);
        if (!mTable.Success) return new TaSnapshot(upd, new List<TaRow>());

        var tableHtml = mTable.Groups[1].Value;

        var mHead = TaTheadRegex.Match(tableHtml);
        var mBody = TaTbodyRegex.Match(tableHtml);
        if (!mHead.Success || !mBody.Success) return new TaSnapshot(upd, new List<TaRow>());

        var headers = TaThRegex.Matches(mHead.Groups[1].Value)
            .Select(x => CleanCell(x.Groups[1].Value))
            .ToList();

        int idxPlayer = IndexOfHeader(headers, "Player");
        int idxElo = IndexOfHeader(headers, "Elo");
        int idxHElo = IndexOfHeader(headers, "hElo");
        int idxCElo = IndexOfHeader(headers, "cElo");
        int idxGElo = IndexOfHeader(headers, "gElo");

        if (idxPlayer < 0 || idxElo < 0)
            return new TaSnapshot(upd, new List<TaRow>());

        var rows = new List<TaRow>(4096);

        foreach (Match tr in TaTrRegex.Matches(mBody.Groups[1].Value))
        {
            var cells = TaTdRegex.Matches(tr.Groups[1].Value)
                .Select(x => CleanCell(x.Groups[1].Value))
                .ToList();

            if (cells.Count <= Math.Max(idxPlayer, idxElo)) continue;

            var player = cells[idxPlayer];
            if (string.IsNullOrWhiteSpace(player)) continue;

            if (!TryParseDouble(cells[idxElo], out var elo)) continue;

            double? h = (idxHElo >= 0 && idxHElo < cells.Count && TryParseDouble(cells[idxHElo], out var hv)) ? hv : null;
            double? c = (idxCElo >= 0 && idxCElo < cells.Count && TryParseDouble(cells[idxCElo], out var cv)) ? cv : null;
            double? g = (idxGElo >= 0 && idxGElo < cells.Count && TryParseDouble(cells[idxGElo], out var gv)) ? gv : null;

            var key = NormalizeName(player);
            var tokens = Tokenize(key);

            rows.Add(new TaRow(player, elo, h, c, g, key, tokens));
        }

        return new TaSnapshot(upd, rows);
    }

    private static int IndexOfHeader(List<string> headers, string header)
    {
        for (int i = 0; i < headers.Count; i++)
            if (string.Equals(headers[i], header, StringComparison.OrdinalIgnoreCase))
                return i;
        return -1;
    }

    private static string CleanCell(string htmlCell)
    {
        var s = TagsRegex.Replace(htmlCell ?? "", "");
        s = WebUtility.HtmlDecode(s);
        s = s.Replace("\u00A0", " ");
        return s.Trim();
    }

    private static TaRow? FindTaRow(List<TaRow> rows, string playerLastFirst)
    {
        if (string.IsNullOrWhiteSpace(playerLastFirst)) return null;

        var inputNorm = NormalizeName(playerLastFirst);
        var tokens = Tokenize(inputNorm);
        if (tokens.Length == 0) return null;

        var reversed = ReverseLastFirst(tokens);

        var exact = rows.FirstOrDefault(r =>
            r.PlayerKey.Equals(inputNorm, StringComparison.OrdinalIgnoreCase) ||
            r.PlayerKey.Equals(reversed, StringComparison.OrdinalIgnoreCase));
        if (exact is not null) return exact;

        // format wejścia: Nazwisko Imię
        if (tokens.Length >= 2)
        {
            var firstName = tokens[^1];
            var surnameTokens = tokens.Take(tokens.Length - 1).ToArray();

            foreach (var r in rows)
            {
                if (!r.Tokens.Contains(firstName)) continue;

                bool okSurname = true;
                foreach (var st in surnameTokens)
                {
                    if (!r.Tokens.Contains(st))
                    {
                        okSurname = false;
                        break;
                    }
                }
                if (okSurname) return r;
            }
        }

        return null;
    }

    private static double? GetSurfaceTa(TaRow row, string surfaceKey)
        => surfaceKey switch
        {
            "hard" => row.EloHard,
            "clay" => row.EloClay,
            "grass" => row.EloGrass,
            _ => null
        };

    // =========================
    // SOURCE #2: Kick-Serve (HTML table parsing)
    // =========================

    private async Task<(KsSnapshot snap, bool fromCache)> GetKsSnapshotAsync(CancellationToken ct, Action<string>? log)
    {
        if (_cache.TryGetValue(CacheKey_KS, out KsSnapshot? cached) && cached is not null)
            return (cached, true);

        await _ksGate.WaitAsync(ct);
        try
        {
            if (_cache.TryGetValue(CacheKey_KS, out cached) && cached is not null)
                return (cached, true);

            log?.Invoke("[ELO][KS] Fetching Kick-Serve wta_elo.html ...");

            using var req = new HttpRequestMessage(HttpMethod.Get, KsWtaEloUrl);
            req.Headers.TryAddWithoutValidation("User-Agent", "STSAnaliza/1.0 (+WinForms)");
            req.Headers.TryAddWithoutValidation("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");

            using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
            resp.EnsureSuccessStatusCode();

            var html = await resp.Content.ReadAsStringAsync(ct);
            var snap = ParseKsSnapshot(html, log);

            _cache.Set(CacheKey_KS, snap, CacheTtl);
            return (snap, false);
        }
        finally
        {
            _ksGate.Release();
        }
    }

    private static readonly Regex KsUpdatedRegex =
        new(@"(?i)\bUpdated:\s*([0-9]{4}-[0-9]{2}-[0-9]{2}[^\r\n<]*)", RegexOptions.Compiled);

    private static readonly Regex KsTableRegex =
        new(@"(?is)<table[^>]*class\s*=\s*[""'][^""']*rankings-table[^""']*[""'][^>]*>(.*?)</table>",
            RegexOptions.Compiled);

    private static readonly Regex KsTbodyRegex =
        new(@"(?is)<tbody[^>]*>(.*?)</tbody>", RegexOptions.Compiled);

    private static readonly Regex KsTrRegex =
        new(@"(?is)<tr[^>]*>(.*?)</tr>", RegexOptions.Compiled);

    private static readonly Regex KsPlayerTdRegex =
        new(@"(?is)<td[^>]*class\s*=\s*[""'][^""']*col-player[^""']*[""'][^>]*>(.*?)</td>",
            RegexOptions.Compiled);

    private static readonly Regex KsValTdRegex =
        new(@"(?is)<td[^>]*class\s*=\s*[""'][^""']*col-val[^""']*[""'][^>]*>(.*?)</td>",
            RegexOptions.Compiled);

    private static readonly Regex FirstNumberRegex =
        new(@"-?\d+(?:\.\d+)?", RegexOptions.Compiled);

    private static KsSnapshot ParseKsSnapshot(string html, Action<string>? log)
    {
        html ??= "";

        // updated: wyciągnij po zdjęciu tagów (najprostsze)
        var plainAll = TagsRegex.Replace(html, "");
        plainAll = WebUtility.HtmlDecode(plainAll);
        plainAll = plainAll.Replace("\u00A0", " ");
        plainAll = plainAll.Replace("\r\n", "\n").Replace("\r", "\n");

        var upd = "";
        var mUpd = KsUpdatedRegex.Match(plainAll);
        if (mUpd.Success) upd = Regex.Replace(mUpd.Groups[1].Value, @"\s+", " ").Trim();

        // tabela rankings-table
        var mTable = KsTableRegex.Match(html);
        if (!mTable.Success)
        {
            log?.Invoke("[ELO][KS] Nie znaleziono <table class='...rankings-table...'> w HTML.");
            return new KsSnapshot(upd, new List<KsRow>());
        }

        var tableHtml = mTable.Groups[1].Value;

        var mBody = KsTbodyRegex.Match(tableHtml);
        if (!mBody.Success)
        {
            log?.Invoke("[ELO][KS] Nie znaleziono <tbody> w tabeli Kick-Serve.");
            return new KsSnapshot(upd, new List<KsRow>());
        }

        var bodyHtml = mBody.Groups[1].Value;

        var rows = new List<KsRow>(4000);
        int parsed = 0, skipped = 0;

        foreach (Match tr in KsTrRegex.Matches(bodyHtml))
        {
            var trHtml = tr.Groups[1].Value;

            var mp = KsPlayerTdRegex.Match(trHtml);
            if (!mp.Success)
            {
                skipped++;
                continue;
            }

            var name = CleanCell(mp.Groups[1].Value);
            if (string.IsNullOrWhiteSpace(name))
            {
                skipped++;
                continue;
            }

            var vals = KsValTdRegex.Matches(trHtml).Select(x => x.Groups[1].Value).ToList();
            if (vals.Count < 5)
            {
                skipped++;
                continue;
            }

            if (!TryExtractFirstNumber(vals[0], out var elo) ||
                !TryExtractFirstNumber(vals[1], out var l52) ||
                !TryExtractFirstNumber(vals[2], out var hard) ||
                !TryExtractFirstNumber(vals[3], out var clay) ||
                !TryExtractFirstNumber(vals[4], out var grass))
            {
                skipped++;
                continue;
            }

            var key = NormalizeName(name);
            var tokens = Tokenize(key);

            rows.Add(new KsRow(name, elo, l52, hard, clay, grass, key, tokens));
            parsed++;
        }

        log?.Invoke($"[ELO][KS] Parsed rows={parsed}, skipped={skipped}");
        return new KsSnapshot(upd, rows);
    }

    private static bool TryExtractFirstNumber(string htmlFragment, out double value)
    {
        value = 0;
        var txt = CleanCell(htmlFragment);
        var m = FirstNumberRegex.Match(txt);
        if (!m.Success) return false;

        var s = m.Value.Replace(',', '.');
        return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private static KsRow? FindKsRow(List<KsRow> rows, string playerLastFirst)
    {
        if (string.IsNullOrWhiteSpace(playerLastFirst)) return null;

        var inputNorm = NormalizeName(playerLastFirst);
        var inputTokens = Tokenize(inputNorm);
        if (inputTokens.Length < 2) return null;

        // format wejścia: Nazwisko Imię
        var firstName = inputTokens[^1];
        var surnameTokens = inputTokens.Take(inputTokens.Length - 1).ToArray();
        var firstInitial = firstName.Length > 0 ? firstName[..1] : "";

        foreach (var r in rows)
        {
            // musi zawierać wszystkie tokeny nazwiska
            bool okSurname = true;
            foreach (var st in surnameTokens)
            {
                if (!r.Tokens.Contains(st))
                {
                    okSurname = false;
                    break;
                }
            }
            if (!okSurname) continue;

            // first name pełne lub inicjał (np. "M. Vondrousova" -> token "m")
            bool okFirst = r.Tokens.Contains(firstName) || (!string.IsNullOrEmpty(firstInitial) && r.Tokens.Contains(firstInitial));
            if (okFirst) return r;
        }

        // fallback: exact / reversed
        var reversed = ReverseLastFirst(inputTokens);
        return rows.FirstOrDefault(r =>
            r.PlayerKey.Equals(inputNorm, StringComparison.OrdinalIgnoreCase) ||
            r.PlayerKey.Equals(reversed, StringComparison.OrdinalIgnoreCase));
    }

    private static double? GetSurfaceKs(KsRow row, string surfaceKey)
        => surfaceKey switch
        {
            "hard" => row.Hard,
            "clay" => row.Clay,
            "grass" => row.Grass,
            _ => null
        };

    // =========================
    // Suggestions for logs
    // =========================

    private static string SuggestTa(List<TaRow> rows, string lastFirst)
    {
        var t = Tokenize(NormalizeName(lastFirst));
        if (t.Length < 2) return "n/a";
        var first = t[^1];
        var sur = t.Take(t.Length - 1).ToArray();

        var candidates = rows
            .Where(r => sur.All(st => r.Tokens.Contains(st)))
            .OrderByDescending(r => r.Tokens.Contains(first))
            .Take(5)
            .Select(r => r.PlayerRaw)
            .ToList();

        return candidates.Count == 0 ? "none" : string.Join(" | ", candidates);
    }

    private static string SuggestKs(List<KsRow> rows, string lastFirst)
    {
        var t = Tokenize(NormalizeName(lastFirst));
        if (t.Length < 2) return "n/a";
        var first = t[^1];
        var sur = t.Take(t.Length - 1).ToArray();

        var candidates = rows
            .Where(r => sur.All(st => r.Tokens.Contains(st)))
            .OrderByDescending(r => r.Tokens.Contains(first))
            .Take(5)
            .Select(r => r.PlayerRaw)
            .ToList();

        return candidates.Count == 0 ? "none" : string.Join(" | ", candidates);
    }

    private static string DbgName(string s)
    {
        var norm = NormalizeName(s);
        var tokens = Tokenize(norm);
        var reversed = ReverseLastFirst(tokens);
        return $"norm='{norm}', tokens=[{string.Join(",", tokens)}], reversed='{reversed}'";
    }

    // =========================
    // Shared helpers
    // =========================

    private static string NormalizeSurface(string? surface)
    {
        var s = (surface ?? "").Trim().ToLowerInvariant();
        if (s.Contains("hard")) return "hard";
        if (s.Contains("clay")) return "clay";
        if (s.Contains("grass")) return "grass";
        return "n/a";
    }

    private static bool TryParseDouble(string s, out double value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(s)) return false;
        s = s.Trim().Replace(',', '.');
        return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private static string Fmt1(double v) => v.ToString("0.0", CultureInfo.InvariantCulture);

    private static string NormalizeName(string s)
    {
        s = (s ?? "").Trim().ToLowerInvariant();
        s = RemoveDiacritics(s);

        var sb = new StringBuilder(s.Length);
        foreach (var ch in s)
        {
            // usuń kropki i inne znaki (Kick-Serve ma inicjały "M.")
            if (char.IsLetterOrDigit(ch) || char.IsWhiteSpace(ch))
                sb.Append(ch);
        }

        return Regex.Replace(sb.ToString(), @"\s+", " ").Trim();
    }

    private static string RemoveDiacritics(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;

        var normalized = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);

        foreach (var ch in normalized)
        {
            var cat = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (cat != UnicodeCategory.NonSpacingMark)
                sb.Append(ch);
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    private static string[] Tokenize(string normalizedName)
        => string.IsNullOrWhiteSpace(normalizedName)
            ? Array.Empty<string>()
            : normalizedName.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string ReverseLastFirst(string[] tokens)
    {
        if (tokens.Length <= 1) return string.Join(' ', tokens);
        var firstName = tokens[^1];
        var surname = string.Join(' ', tokens.Take(tokens.Length - 1));
        return $"{firstName} {surname}".Trim();
    }

    // =========================
    // Models
    // =========================

    private sealed record TaSnapshot(string LastUpdate, List<TaRow> Rows);

    private sealed record TaRow(
        string PlayerRaw,
        double EloOverall,
        double? EloHard,
        double? EloClay,
        double? EloGrass,
        string PlayerKey,
        string[] Tokens);

    private sealed record KsSnapshot(string Updated, List<KsRow> Rows);

    private sealed record KsRow(
        string PlayerRaw,
        double Elo,
        double L52,
        double Hard,
        double Clay,
        double Grass,
        string PlayerKey,
        string[] Tokens);
}