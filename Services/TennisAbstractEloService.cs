using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using STSAnaliza.Interfejs;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace STSAnaliza.Services;

public sealed class TennisAbstractEloService : ITennisAbstractEloService
{
    private const string Url = "reports/wta_elo_ratings.html";
    private const string CacheKey = "TA_WTA_ELO_SNAPSHOT_V1";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(2);

    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;
    private readonly ILogger<TennisAbstractEloService> _logger;

    private readonly SemaphoreSlim _gate = new(1, 1);

    public TennisAbstractEloService(HttpClient http, IMemoryCache cache, ILogger<TennisAbstractEloService> logger)
    {
        _http = http;
        _cache = cache;
        _logger = logger;
    }

    public async Task<string> BuildFill7Async(string playerA_LastFirst, string playerB_LastFirst, string? surface, CancellationToken ct)
    {
        var na = BuildFill7_AllNa();

        Snapshot snap;
        try
        {
            snap = await GetSnapshotAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "TennisAbstract snapshot failed");
            return na;
        }

        // Warunek: musi być widoczne "Last update"
        if (string.IsNullOrWhiteSpace(snap.LastUpdate))
            return na;

        var rowA = FindPlayerRow(snap.Rows, playerA_LastFirst);
        var rowB = FindPlayerRow(snap.Rows, playerB_LastFirst);

        // Warunek: Elo dla OBU z jednego źródła/aktualizacji -> jeśli brak kompletu -> n/a dla wszystkich
        if (rowA is null || rowB is null)
            return na;

        var overallA = rowA.EloOverall;
        var overallB = rowB.EloOverall;

        var surfaceKey = NormalizeSurface(surface);

        double? surfA = GetSurfaceElo(rowA, surfaceKey);
        double? surfB = GetSurfaceElo(rowB, surfaceKey);

        string s1 = $"Elo_A (overall): {Fmt1(overallA)}";
        string s2 = $"Elo_B (overall): {Fmt1(overallB)}";

        // surface n/a jeśli nieznane/inne
        string s3 = $"Elo_A (surface): {(surfA is null ? "n/a" : Fmt1(surfA.Value))}";
        string s4 = $"Elo_B (surface): {(surfB is null ? "n/a" : Fmt1(surfB.Value))}";

        string s5;
        if (surfA is null || surfB is null)
        {
            s5 = "ΔElo (surface): n/a";
        }
        else
        {
            var delta = surfA.Value - surfB.Value;
            s5 = $"ΔElo (surface): {Fmt1(delta)}";
        }

        return string.Join(Environment.NewLine, s1, s2, s3, s4, s5);
    }

    // -------------------------
    // Snapshot + cache (TTL 2h)
    // -------------------------

    private async Task<Snapshot> GetSnapshotAsync(CancellationToken ct)
    {
        if (_cache.TryGetValue(CacheKey, out Snapshot? cached) && cached is not null)
            return cached;

        await _gate.WaitAsync(ct);
        try
        {
            if (_cache.TryGetValue(CacheKey, out cached) && cached is not null)
                return cached;

            using var req = new HttpRequestMessage(HttpMethod.Get, Url);
            req.Headers.TryAddWithoutValidation("User-Agent", "STSAnaliza/1.0 (+WinForms)");

            using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
            resp.EnsureSuccessStatusCode();

            var html = await resp.Content.ReadAsStringAsync(ct);
            var snap = ParseSnapshot(html);

            _cache.Set(CacheKey, snap, CacheTtl);
            return snap;
        }
        finally
        {
            _gate.Release();
        }
    }

    // -------------------------
    // HTML parsing
    // -------------------------

    private static readonly Regex LastUpdateRegex =
        new(@"(?i)Last update:\s*([0-9]{4}-[0-9]{2}-[0-9]{2})", RegexOptions.Compiled);

    private static readonly Regex TableRegex =
        new(@"(?is)<table[^>]*\bid\s*=\s*[""']reportable[""'][^>]*>(.*?)</table>", RegexOptions.Compiled);

    private static readonly Regex TheadRegex =
        new(@"(?is)<thead[^>]*>(.*?)</thead>", RegexOptions.Compiled);

    private static readonly Regex TbodyRegex =
        new(@"(?is)<tbody[^>]*>(.*?)</tbody>", RegexOptions.Compiled);

    private static readonly Regex ThRegex =
        new(@"(?is)<th[^>]*>(.*?)</th>", RegexOptions.Compiled);

    private static readonly Regex TrRegex =
        new(@"(?is)<tr[^>]*>(.*?)</tr>", RegexOptions.Compiled);

    private static readonly Regex TdRegex =
        new(@"(?is)<t[dh][^>]*>(.*?)</t[dh]>", RegexOptions.Compiled);

    private static readonly Regex TagsRegex =
        new(@"(?is)<[^>]+>", RegexOptions.Compiled);

    private static Snapshot ParseSnapshot(string html)
    {
        html ??= "";

        var lastUpdate = "";
        var mUpd = LastUpdateRegex.Match(html);
        if (mUpd.Success)
            lastUpdate = mUpd.Groups[1].Value.Trim();

        var mTable = TableRegex.Match(html);
        if (!mTable.Success)
            return new Snapshot(lastUpdate, new List<EloRow>());

        var tableHtml = mTable.Groups[1].Value;

        var mThead = TheadRegex.Match(tableHtml);
        var mTbody = TbodyRegex.Match(tableHtml);

        if (!mThead.Success || !mTbody.Success)
            return new Snapshot(lastUpdate, new List<EloRow>());

        var headers = ThRegex.Matches(mThead.Groups[1].Value)
            .Select(x => CleanCell(x.Groups[1].Value))
            .ToList();

        int idxPlayer = IndexOfHeader(headers, "Player");
        int idxElo = IndexOfHeader(headers, "Elo");
        int idxHElo = IndexOfHeader(headers, "hElo");
        int idxCElo = IndexOfHeader(headers, "cElo");
        int idxGElo = IndexOfHeader(headers, "gElo");

        // minimalny zestaw, żeby działać
        if (idxPlayer < 0 || idxElo < 0)
            return new Snapshot(lastUpdate, new List<EloRow>());

        var rows = new List<EloRow>(4096);

        foreach (Match tr in TrRegex.Matches(mTbody.Groups[1].Value))
        {
            var cells = TdRegex.Matches(tr.Groups[1].Value)
                .Select(x => CleanCell(x.Groups[1].Value))
                .ToList();

            if (cells.Count <= Math.Max(idxPlayer, idxElo))
                continue;

            var player = cells[idxPlayer];
            if (string.IsNullOrWhiteSpace(player))
                continue;

            if (!TryParseDouble(cells[idxElo], out var eloOverall))
                continue;

            double? h = (idxHElo >= 0 && idxHElo < cells.Count && TryParseDouble(cells[idxHElo], out var hv)) ? hv : null;
            double? c = (idxCElo >= 0 && idxCElo < cells.Count && TryParseDouble(cells[idxCElo], out var cv)) ? cv : null;
            double? g = (idxGElo >= 0 && idxGElo < cells.Count && TryParseDouble(cells[idxGElo], out var gv)) ? gv : null;

            var key = NormalizeName(player);
            var tokens = Tokenize(key);

            rows.Add(new EloRow(player, eloOverall, h, c, g, key, tokens));
        }

        return new Snapshot(lastUpdate, rows);
    }

    private static int IndexOfHeader(List<string> headers, string header)
    {
        for (int i = 0; i < headers.Count; i++)
        {
            if (string.Equals(headers[i], header, StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return -1;
    }

    private static string CleanCell(string htmlCell)
    {
        var s = TagsRegex.Replace(htmlCell ?? "", "");
        s = WebUtility.HtmlDecode(s);
        s = s.Replace("\u00A0", " "); // nbsp
        return s.Trim();
    }

    private static bool TryParseDouble(string s, out double value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(s)) return false;
        s = s.Trim().Replace(',', '.');
        return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    // -------------------------
    // Matching names (Nazwisko Imię -> TennisAbstract: Imię Nazwisko)
    // -------------------------

    private static EloRow? FindPlayerRow(List<EloRow> rows, string playerLastFirst)
    {
        if (string.IsNullOrWhiteSpace(playerLastFirst)) return null;

        // wejście: "Appleton Emily" (Nazwisko Imię)
        var inputNorm = NormalizeName(playerLastFirst);
        var tokens = Tokenize(inputNorm);
        if (tokens.Length == 0) return null;

        // kandydaci: oryginalny + odwrócony
        var reversed = ReverseLastFirst(tokens); // "emily appleton" (albo złożone nazwiska)

        // 1) exact match (czasem wejście już jest w układzie TA)
        var exact = rows.FirstOrDefault(r =>
            r.PlayerKey.Equals(inputNorm, StringComparison.OrdinalIgnoreCase) ||
            r.PlayerKey.Equals(reversed, StringComparison.OrdinalIgnoreCase));

        if (exact is not null) return exact;

        // 2) token match: wymagamy FirstName + wszystkie tokeny nazwiska (dla złożonych nazwisk)
        // Zakładamy: ostatni token = imię, reszta = nazwisko (zgodnie z Twoim formatem)
        if (tokens.Length >= 2)
        {
            var firstName = tokens[^1];
            var surnameTokens = tokens.Take(tokens.Length - 1).ToArray();

            foreach (var r in rows)
            {
                if (!r.Tokens.Contains(firstName))
                    continue;

                bool okSurname = true;
                foreach (var st in surnameTokens)
                {
                    if (!r.Tokens.Contains(st))
                    {
                        okSurname = false;
                        break;
                    }
                }

                if (okSurname)
                    return r;
            }
        }

        return null;
    }

    private static string ReverseLastFirst(string[] tokens)
    {
        // tokens: [nazwisko..., imie]
        if (tokens.Length <= 1) return string.Join(' ', tokens);
        var firstName = tokens[^1];
        var surname = string.Join(' ', tokens.Take(tokens.Length - 1));
        return $"{firstName} {surname}".Trim();
    }

    private static string NormalizeSurface(string? surface)
    {
        var s = (surface ?? "").Trim().ToLowerInvariant();

        // czasem meta potrafi zwrócić np. "hardcourt_outdoor" – utnij do "hard"
        if (s.Contains("hard")) return "hard";
        if (s.Contains("clay")) return "clay";
        if (s.Contains("grass")) return "grass";

        if (s is "hard" or "clay" or "grass") return s;
        return "n/a";
    }

    private static double? GetSurfaceElo(EloRow row, string surfaceKey)
        => surfaceKey switch
        {
            "hard" => row.EloHard,
            "clay" => row.EloClay,
            "grass" => row.EloGrass,
            _ => null
        };

    private static string BuildFill7_AllNa()
        => string.Join(Environment.NewLine,
            "Elo_A (overall): n/a",
            "Elo_B (overall): n/a",
            "Elo_A (surface): n/a",
            "Elo_B (surface): n/a",
            "ΔElo (surface): n/a");

    private static string Fmt1(double v) => v.ToString("0.0", CultureInfo.InvariantCulture);

    private static string NormalizeName(string s)
    {
        s = (s ?? "").Trim().ToLowerInvariant();
        s = RemoveDiacritics(s);

        // usuń znaki poza literami/cyframi/spacjami
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s)
        {
            if (char.IsLetterOrDigit(ch) || char.IsWhiteSpace(ch))
                sb.Append(ch);
        }

        // wielokrotne spacje -> jedna
        var cleaned = Regex.Replace(sb.ToString(), @"\s+", " ").Trim();
        return cleaned;
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

    private sealed record Snapshot(string LastUpdate, List<EloRow> Rows);

    private sealed record EloRow(
        string PlayerRaw,
        double EloOverall,
        double? EloHard,
        double? EloClay,
        double? EloGrass,
        string PlayerKey,
        string[] Tokens);
}