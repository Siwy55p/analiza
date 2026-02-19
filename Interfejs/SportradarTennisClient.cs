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
    private static readonly TimeSpan TtlSchedule = TimeSpan.FromMinutes(2);
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

            // jak loader rzuci wyjątek => nic się nie zapisze do cache (dobrze)
            var value = await loader(ct).ConfigureAwait(false);

            // jeśli null => cache krótko, żeby nie kisić "braków"
            if (value is null)
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2);

            return value;
        }).ConfigureAwait(false);
    }

    // -------------------------
    // Public API
    // -------------------------

    public Task<string> GetSportEventSummaryJsonAsync(string sportEventId, CancellationToken ct)
    {
        var id = NormalizeSrId(sportEventId, nameof(sportEventId));
        var path = $"sport_events/{id}/summary.json";

        var cacheKey = $"{CachePrefix}:event-summary-json:{id}";
        return GetCachedAsync(
                cacheKey,
                TtlEventSummary,
                ct2 => GetStringAsync(path, ct2),
                ct
            )!;
    }

    public Task<RankingsResponseDto> GetRankingsAsync(CancellationToken ct)
    {
        var cacheKey = $"{CachePrefix}:rankings";
        return GetCachedAsync(
                cacheKey,
                TtlRankings,
                ct2 => GetAsync("rankings.json", ct2, () => new RankingsResponseDto(null, null)),
                ct
            )!;
    }

    public Task<RankingsResponseDto> GetRaceRankingsAsync(CancellationToken ct)
    {
        var cacheKey = $"{CachePrefix}:race-rankings";
        return GetCachedAsync(
                cacheKey,
                TtlRankings,
                ct2 => GetAsync("race_rankings.json", ct2, () => new RankingsResponseDto(null, null)),
                ct
            )!;
    }

    public Task<string> GetRankingsJsonAsync(CancellationToken ct)
    {
        var cacheKey = $"{CachePrefix}:rankings-json";
        return GetCachedAsync(
                cacheKey,
                TtlRankings,
                ct2 => GetStringAsync("rankings.json", ct2),
                ct
            )!;
    }

    public Task<string> GetRaceRankingsJsonAsync(CancellationToken ct)
    {
        var cacheKey = $"{CachePrefix}:race-rankings-json";
        return GetCachedAsync(
                cacheKey,
                TtlRankings,
                ct2 => GetStringAsync("race_rankings.json", ct2),
                ct
            )!;
    }

    public Task<CompetitorSummariesResponse> GetCompetitorVersusSummariesAsync(
        string competitorIdA,
        string competitorIdB,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(competitorIdA))
            throw new ArgumentException("competitorIdA nie może być pusty.", nameof(competitorIdA));
        if (string.IsNullOrWhiteSpace(competitorIdB))
            throw new ArgumentException("competitorIdB nie może być pusty.", nameof(competitorIdB));

        var a = NormalizeSrId(competitorIdA, nameof(competitorIdA));
        var b = NormalizeSrId(competitorIdB, nameof(competitorIdB));

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

    public Task<SportEventSummaryDto> GetSportEventSummaryAsync(string sportEventId, CancellationToken ct)
    {
        var id = NormalizeSrId(sportEventId, nameof(sportEventId));
        var path = $"sport_events/{id}/summary.json";

        var cacheKey = $"{CachePrefix}:event-summary:{id}";
        return GetCachedAsync(
                cacheKey,
                TtlEventSummary,
                ct2 => GetAsync(path, ct2, () => new SportEventSummaryDto()),
                ct
            )!;
    }

    public Task<CompetitorSummariesResponse> GetCompetitorSummariesAsync(string competitorId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(competitorId))
            throw new ArgumentException("competitorId nie może być pusty.", nameof(competitorId));

        var id = NormalizeSrId(competitorId, nameof(competitorId));
        var path = $"competitors/{id}/summaries.json";

        var cacheKey = $"{CachePrefix}:competitor-summaries:{id}";
        return GetCachedAsync(
                cacheKey,
                TtlCompetitorSummary,
                ct2 => GetAsync(path, ct2, () => new CompetitorSummariesResponse()),
                ct
            )!;
    }

    public Task<string> GetDailySummariesJsonAsync(DateOnly date, CancellationToken ct)
    {
        var dateStr = date.ToString("yyyy-MM-dd");
        var path = $"schedules/{dateStr}/summaries.json";

        var cacheKey = $"{CachePrefix}:schedule-json:{dateStr}";
        return GetCachedAsync(
                cacheKey,
                TtlSchedule,
                ct2 => GetStringAsync(path, ct2),
                ct
            )!;
    }

    public Task<CompetitorSummariesResponse> GetDailySummariesAsync(DateOnly date, CancellationToken ct)
    {
        var dateStr = date.ToString("yyyy-MM-dd");
        var path = $"schedules/{dateStr}/summaries.json";

        var cacheKey = $"{CachePrefix}:schedule:{dateStr}";
        return GetCachedAsync(
                cacheKey,
                TtlSchedule,
                ct2 => GetAsync(path, ct2, () => new CompetitorSummariesResponse()),
                ct
            )!;
    }

    public Task<SeasonInfoDto> GetSeasonInfoAsync(string seasonId, CancellationToken ct)
    {
        var id = NormalizeSrId(seasonId, nameof(seasonId));
        var path = $"seasons/{id}/info.json";

        var cacheKey = $"{CachePrefix}:season-info:{id}";
        return GetCachedAsync(
                cacheKey,
                TtlSeasonInfo,
                ct2 => GetAsync(path, ct2, () => new SeasonInfoDto()),
                ct
            )!;
    }

    // -------------------------
    // Private helpers
    // -------------------------

    private static string NormalizeSrId(string id, string paramName)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException($"{paramName} nie może być pusty.", paramName);

        return Uri.UnescapeDataString(id.Trim());
    }

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


        // 2) send
        using var req = CreateGetRequest(url);
        using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);

        // 3) 429 retry once (jeśli handler już retry'uje, to tu będzie rzadziej)
        if (resp.StatusCode == HttpStatusCode.TooManyRequests)
        {
            var retryAfter = resp.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(2);
            _logger.LogWarning("Sportradar 429 Too Many Requests. Retry after: {Delay}. Url: {Url}", retryAfter, url);

            await Task.Delay(retryAfter, ct);

            using var req2 = CreateGetRequest(url);
            using var resp2 = await _http.SendAsync(req2, HttpCompletionOption.ResponseHeadersRead, ct);
            return await ReadJsonOrThrowAsync(resp2, url, ct, emptyFactory);
        }

        return await ReadJsonOrThrowAsync(resp, url, ct, emptyFactory);
    }

    private async Task<string> GetStringAsync(string relativePath, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var url = BuildUrl(relativePath);

        //await _gate.WaitTurnAsync(ct);

        using var req = CreateGetRequest(url);
        using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);

        if (resp.StatusCode == HttpStatusCode.TooManyRequests)
        {
            var retryAfter = resp.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(2);
            _logger.LogWarning("Sportradar 429 Too Many Requests. Retry after: {Delay}. Url: {Url}", retryAfter, url);

            await Task.Delay(retryAfter, ct);
            //await _gate.WaitTurnAsync(ct);

            using var req2 = CreateGetRequest(url);
            using var resp2 = await _http.SendAsync(req2, HttpCompletionOption.ResponseHeadersRead, ct);
            return await ReadStringOrThrowAsync(resp2, url, ct);
        }

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
