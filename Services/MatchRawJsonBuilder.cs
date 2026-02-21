using STSAnaliza.Models;
using System.Text;
using System.Text.Json;
using STSAnaliza.Interfejs;

namespace STSAnaliza.Services;

public sealed class MatchRawJsonBuilder : IMatchRawJsonBuilder
{
    private readonly ITennisApiService _tennisApi;
    private readonly IRankService _rank;

    // "Forma" ma bazować na świeżych danych.
    // Jeśli zawodnik nie grał długo (np. kontuzja), wolimy zwrócić n=0 niż brać mecze sprzed lat.
    private static readonly TimeSpan MaxAgeForForm = TimeSpan.FromDays(365);

    public MatchRawJsonBuilder(ITennisApiService tennisApi, IRankService rank)
    {
        _tennisApi = tennisApi;
        _rank = rank;
    }
    public async Task<string> BuildByCompetitorIdAsync(string playerName, string competitorId, CancellationToken ct)
    {
        var list = await _tennisApi.GetLast10MatchesAsync(competitorId, ct);

        return await BuildJsonFromMatchesAsync(playerName, list, ct);
    }

    public async Task<string> BuildAsync(string playerName, CancellationToken ct)
    {
        var list = await _tennisApi.GetLast10MatchesByNameAsync(playerName, ct);
        return await BuildJsonFromMatchesAsync(playerName, list, ct);
    }

    private async Task<string> BuildJsonFromMatchesAsync(string playerName, IReadOnlyList<PlayerMatchSummary> list, CancellationToken ct)
    {
        // Filtr świeżości: tylko ostatnie 12 miesięcy.
        // Uwaga: list jest już posortowana malejąco po dacie (z TennisApiService).
        var cutoffUtc = DateTimeOffset.UtcNow - MaxAgeForForm;
        var recent = list.Where(m => m.StartTimeUtc >= cutoffUtc).ToList();

        // Bierzemy max 8, ale jeśli jest mniej, nie "dobieramy" starszych.
        var n = recent.Count >= 8 ? 8 : (recent.Count >= 7 ? 7 : recent.Count);
        var picked = n > 0 ? recent.Take(n).ToList() : new List<PlayerMatchSummary>();


        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms, new JsonWriterOptions { Indented = false }))
        {
            w.WriteStartObject();
            w.WriteString("player", playerName);
            w.WriteNumber("n", picked.Count);

            w.WritePropertyName("matches");
            w.WriteStartArray();

            foreach (var m in picked)
            {
                w.WriteStartObject();
                w.WriteString("date", m.StartTimeUtc.ToString("yyyy-MM-dd"));
                w.WriteString("wl", m.Result);

                var r = await _rank.GetSinglesRankAsync(m.OpponentId, ct);
                w.WritePropertyName("opp_rank");
                if (r.HasValue) w.WriteNumberValue(r.Value);
                else w.WriteStringValue("brak");

                w.WriteString("surface", SurfaceOut(m.Surface));

                w.WriteString("sets", ToSetsSummary(m.Score));
                w.WriteEndObject();
            }

            w.WriteEndArray();
            w.WriteEndObject();
        }

        return Encoding.UTF8.GetString(ms.ToArray());
    }
    static string SurfaceOut(string? s)
    {
        s = (s ?? "").Trim().ToLowerInvariant();
        return s is "hard" or "clay" or "grass" ? s : "brak";
    }
    private static string ToSetsSummary(string score)
    {
        if (string.IsNullOrWhiteSpace(score))
            return "brak";

        int my = 0, opp = 0;

        foreach (var token0 in score.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var token = token0;
            var paren = token.IndexOf('(');
            if (paren > 0) token = token[..paren];

            var parts = token.Split('-', 2);
            if (parts.Length != 2) continue;

            if (!int.TryParse(parts[0], out var a)) continue;
            if (!int.TryParse(parts[1], out var b)) continue;

            if (a > b) my++;
            else if (b > a) opp++;
        }

        if (my == 0 && opp == 0) return "brak";
        if (my > 2 || opp > 2) return "brak";

        return $"{my}-{opp}";
    }
}
