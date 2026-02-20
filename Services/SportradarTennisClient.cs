using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using STSAnaliza.Options;
using STSAnaliza.Services.SportradarDtos;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace STSAnaliza.Services;

public sealed class SportradarTennisClient : ISportradarTennisClient
{
    private readonly HttpClient _http;
    private readonly SportradarOptions _opt;
    private readonly IMemoryCache _cache;
    private readonly ILogger<SportradarTennisClient> _logger;

    private static readonly JsonSerializerOptions JsonOpt = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // TTL-e cache
    private static readonly TimeSpan TtlSeasonInfo = TimeSpan.FromDays(7);
    private static readonly TimeSpan TtlEventSummary = TimeSpan.FromHours(12);
    private static readonly TimeSpan TtlCompetitorSummary = TimeSpan.FromHours(12);
    private static readonly TimeSpan TtlRankings = TimeSpan.FromHours(12);

    // ważne: schedule to endpoint dzienny – spokojnie może mieć dłuższy TTL niż 2 min,
    // a i tak masz własny cache w DailyMatchResolver (15 min).
    private static readonly TimeSpan TtlSchedule = TimeSpan.FromMinutes(10);

    private static readonly TimeSpan TtlVersus = TimeSpan.FromHours(12);

    public SportradarTennisClient(
        HttpClient http,
        IMemoryCache cache,
        IOptions<SportradarOptions> opt,
        ILogger<SportradarTennisClient> logger)
    {
        _http = http;
        _opt = opt.Value;
        _logger = logger;
        _cache = cache;
    }

    private string CachePrefix => $"sr:{_opt.AccessLevel}:{_opt.Locale}";

    private async Task<T?> GetCachedAsync<T>(
        string key,
        TimeSpan ttl,
        Func<CancellationToken, Task<T?>> loader,
        CancellationToken ct)
    {
        if (_cache.TryGetValue(key, out T? cached) && cached is not null)
            return cached;

        return await _cache.GetOrCreateAsync(key, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = ttl;
            entry.Priority = CacheItemPriority.Normal;

            var value = await loader(ct).ConfigureAwait(false);

            // jeśli null => cache krótko, żeby nie kisić "braków"
            if (value is null)
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2);

            return value;
        }).ConfigureAwait(false);
    }

    private Task<string> GetJsonCachedAsync(string cacheKey, TimeSpan ttl, string relativePath, CancellationToken ct)
        => GetCachedAsync(cacheKey, ttl, ct2 => GetStringAsync(relativePath, ct2), ct)!;

    private static T DeserializeOrEmpty<T>(string json, Func<T> emptyFactory)
    {
        try
        {
            var obj = JsonSerializer.Deserialize<T>(json, JsonOpt);
            return obj ?? emptyFactory();
        }
        catch
        {
            return emptyFactory();
        }
    }

    // -------------------------
    // Public API (ISportradarTennisClient)
    // -------------------------

    public Task<CompetitorSummariesResponse> GetCompetitorSummariesAsync(string competitorId, CancellationToken ct)
    {
        var id = SportradarId.NormalizeRequired(competitorId, nameof(competitorId));
        var path = $"competitors/{id}/summaries.json";

        var cacheKey = $"{CachePrefix}:competitor-summaries:{id}";
        return GetCachedAsync(
                cacheKey,
                TtlCompetitorSummary,
                ct2 => GetAsync(path, ct2, () => new CompetitorSummariesResponse()),
                ct
            )!;
    }

    public Task<string> GetRankingsJsonAsync(CancellationToken ct)
    {
        var cacheKey = $"{CachePrefix}:rankings-json";
        return GetJsonCachedAsync(cacheKey, TtlRankings, "rankings.json", ct);
    }

    // DTO z JSON (bez dodatkowego requestu)
    public Task<RankingsResponseDto> GetRankingsAsync(CancellationToken ct)
    {
        var cacheKey = $"{CachePrefix}:rankings-dto";
        return GetCachedAsync(
                cacheKey,
                TtlRankings,
                async ct2 =>
                {
                    var json = await GetRankingsJsonAsync(ct2).ConfigureAwait(false);
                    return DeserializeOrEmpty(json, () => new RankingsResponseDto(null, null));
                },
                ct
            )!;
    }

    // Race rankings: też robimy JSON -> DTO (żeby kiedyś łatwo współdzielić)
    private Task<string> GetRaceRankingsJsonAsync(CancellationToken ct)
    {
        var cacheKey = $"{CachePrefix}:race-rankings-json";
        return GetJsonCachedAsync(cacheKey, TtlRankings, "race_rankings.json", ct);
    }

    public Task<RankingsResponseDto> GetRaceRankingsAsync(CancellationToken ct)
    {
        var cacheKey = $"{CachePrefix}:race-rankings-dto";
        return GetCachedAsync(
                cacheKey,
                TtlRankings,
                async ct2 =>
                {
                    var json = await GetRaceRankingsJsonAsync(ct2).ConfigureAwait(false);
                    return DeserializeOrEmpty(json, () => new RankingsResponseDto(null, null));
                },
                ct
            )!;
    }

    public Task<string> GetDailySummariesJsonAsync(DateOnly date, CancellationToken ct)
    {
        var dateStr = date.ToString("yyyy-MM-dd");
        var path = $"schedules/{dateStr}/summaries.json";

        var cacheKey = $"{CachePrefix}:schedule-json:{dateStr}";
        return GetJsonCachedAsync(cacheKey, TtlSchedule, path, ct);
    }

    // DTO z JSON (bez drugiego requestu)
    public Task<CompetitorSummariesResponse> GetDailySummariesAsync(DateOnly date, CancellationToken ct)
    {
        var dateStr = date.ToString("yyyy-MM-dd");
        var cacheKey = $"{CachePrefix}:schedule-dto:{dateStr}";

        return GetCachedAsync(
                cacheKey,
                TtlSchedule,
                async ct2 =>
                {
                    var json = await GetDailySummariesJsonAsync(date, ct2).ConfigureAwait(false);
                    return DeserializeOrEmpty(json, () => new CompetitorSummariesResponse());
                },
                ct
            )!;
    }

    public Task<SeasonInfoDto> GetSeasonInfoAsync(string seasonId, CancellationToken ct)
    {
        var id = SportradarId.NormalizeRequired(seasonId, nameof(seasonId));
        var path = $"seasons/{id}/info.json";

        var cacheKey = $"{CachePrefix}:season-info:{id}";
        return GetCachedAsync(
                cacheKey,
                TtlSeasonInfo,
                ct2 => GetAsync(path, ct2, () => new SeasonInfoDto()),
                ct
            )!;
    }

    public Task<CompetitorSummariesResponse> GetCompetitorVersusSummariesAsync(
        string competitorIdA,
        string competitorIdB,
        CancellationToken ct)
    {
        var a = SportradarId.NormalizeRequired(competitorIdA, nameof(competitorIdA));
        var b = SportradarId.NormalizeRequired(competitorIdB, nameof(competitorIdB));

        var path = $"competitors/{a}/versus/{b}/summaries.json";

        // uporządkuj klucz, żeby A vs B i B vs A nie cache'owały 2x
        var (x, y) = string.CompareOrdinal(a, b) <= 0 ? (a, b) : (b, a);
        var cacheKey = $"{CachePrefix}:versus:{x}:{y}";

        return GetCachedAsync(
                cacheKey,
                TtlVersus,
                ct2 => GetAsync(path, ct2, () => new CompetitorSummariesResponse()),
                ct
            )!;
    }

    public Task<string> GetSportEventSummaryJsonAsync(string sportEventId, CancellationToken ct)
    {
        var id = SportradarId.NormalizeRequired(sportEventId, nameof(sportEventId));
        var path = $"sport_events/{id}/summary.json";

        var cacheKey = $"{CachePrefix}:event-summary-json:{id}";
        return GetJsonCachedAsync(cacheKey, TtlEventSummary, path, ct);
    }

    // DTO z JSON (bez drugiego requestu)
    public Task<SportEventSummaryDto> GetSportEventSummaryAsync(string sportEventId, CancellationToken ct)
    {
        var id = SportradarId.NormalizeRequired(sportEventId, nameof(sportEventId));
        var cacheKey = $"{CachePrefix}:event-summary-dto:{id}";

        return GetCachedAsync(
                cacheKey,
                TtlEventSummary,
                async ct2 =>
                {
                    var json = await GetSportEventSummaryJsonAsync(id, ct2).ConfigureAwait(false);
                    return DeserializeOrEmpty(json, () => new SportEventSummaryDto());
                },
                ct
            )!;
    }

    // -------------------------
    // Private helpers (HTTP)
    // -------------------------

    private string BuildUrl(string relativePath)
    {
        relativePath = relativePath.TrimStart('/');
        return $"tennis/{_opt.AccessLevel}/v3/{_opt.Locale}/{relativePath}";
    }

    private HttpRequestMessage CreateGetRequest(string url)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.TryAddWithoutValidation("accept", "application/json");
        req.Headers.TryAddWithoutValidation("x-api-key", _opt.ApiKey);
        return req;
    }

    private async Task<T> GetAsync<T>(string relativePath, CancellationToken ct, Func<T> emptyFactory)
    {
        ct.ThrowIfCancellationRequested();

        var url = BuildUrl(relativePath);

        // retry 429 robi SportradarThrottlingHandler
        using var req = CreateGetRequest(url);
        using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);

        return await ReadJsonOrThrowAsync(resp, url, ct, emptyFactory);
    }

    private async Task<string> GetStringAsync(string relativePath, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var url = BuildUrl(relativePath);

        using var req = CreateGetRequest(url);
        using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);

        return await ReadStringOrThrowAsync(resp, url, ct);
    }

    private async Task<T> ReadJsonOrThrowAsync<T>(HttpResponseMessage resp, string url, CancellationToken ct, Func<T> emptyFactory)
    {
        if (resp.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            var body = await resp.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(
                $"Sportradar auth error ({(int)resp.StatusCode}). Sprawdź API key / uprawnienia. Url: {url}. Body: {body}"
            );
        }

        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync(ct);
            _logger.LogWarning("Sportradar HTTP {Status} {Reason}. Url: {Url}. Body: {Body}",
                (int)resp.StatusCode, resp.ReasonPhrase, url, body);

            throw new InvalidOperationException(
                $"Sportradar HTTP {(int)resp.StatusCode} {resp.ReasonPhrase}. Url: {url}. Body: {body}"
            );
        }

        var obj = await resp.Content.ReadFromJsonAsync<T>(JsonOpt, ct);
        return obj ?? emptyFactory();
    }

    private static async Task<string> ReadStringOrThrowAsync(HttpResponseMessage resp, string url, CancellationToken ct)
    {
        if (resp.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            var body = await resp.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(
                $"Sportradar auth error ({(int)resp.StatusCode}). Sprawdź API key / uprawnienia. Url: {url}. Body: {body}"
            );
        }

        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(
                $"Sportradar HTTP {(int)resp.StatusCode} {resp.ReasonPhrase}. Url: {url}. Body: {body}"
            );
        }

        return await resp.Content.ReadAsStringAsync(ct);
    }
}