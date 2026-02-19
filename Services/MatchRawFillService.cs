using STSAnaliza.Models;
using System.Text;
using System.Text.Json;

namespace STSAnaliza.Services;

public interface IMatchRawFillService
{
    Task<string> BuildFillLineAsync(string fillTag, string playerName, CancellationToken ct);
}

public sealed class MatchRawFillService : IMatchRawFillService
{
    private readonly ITennisApiService _tennisApi;

    public MatchRawFillService(ITennisApiService tennisApi)
    {
        _tennisApi = tennisApi;
    }

    public async Task<string> BuildFillLineAsync(string fillTag, string playerName, CancellationToken ct)
    {
        // bierzemy ostatnie mecze (masz 10), ale do formatu kroku dajemy N=8 (lub 7 jeśli brak)
        var list = await _tennisApi.GetLast10MatchesByNameAsync(playerName, ct);

        var take = list.Count >= 8 ? 8 : (list.Count >= 7 ? 7 : list.Count);
        var picked = take > 0 ? list.Take(take).ToList() : new List<PlayerMatchSummary>();

        var json = BuildRawJson(playerName, picked);

        // UWAGA: format ma być dokładnie: <<FILL_..>>=JSON (jedna linia)
        return $"{fillTag}={json}";
    }

    private static string BuildRawJson(string playerName, IReadOnlyList<PlayerMatchSummary> matches)
    {
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms, new JsonWriterOptions { Indented = false }))
        {
            w.WriteStartObject();

            w.WriteString("player", playerName);
            w.WriteNumber("n", matches.Count);

            w.WritePropertyName("matches");
            w.WriteStartArray();

            foreach (var m in matches)
            {
                w.WriteStartObject();

                w.WriteString("date", m.StartTimeUtc.ToString("yyyy-MM-dd"));
                w.WriteString("wl", m.Result);

                // na teraz brak danych -> "brak"
                w.WritePropertyName("opp_rank");
                w.WriteStringValue("brak");

                w.WriteString("surface", "brak");

                var sets = ToSetsSummary(m.Score);
                w.WriteString("sets", sets);

                w.WriteEndObject();
            }

            w.WriteEndArray();
            w.WriteEndObject();
        }

        return Encoding.UTF8.GetString(ms.ToArray());
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
            if (paren > 0)
                token = token[..paren];

            var parts = token.Split('-', 2);
            if (parts.Length != 2) continue;

            if (!int.TryParse(parts[0], out var a)) continue;
            if (!int.TryParse(parts[1], out var b)) continue;

            if (a > b) my++;
            else if (b > a) opp++;
        }

        if (my == 0 && opp == 0) return "brak";

        // prompt dopuszcza tylko BO3: 2-0/2-1/1-2/0-2
        if (my > 2 || opp > 2) return "brak";

        return $"{my}-{opp}";
    }
}
