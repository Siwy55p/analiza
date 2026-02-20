using Microsoft.Playwright;
using System.Globalization;
using System.Text;
using STSAnaliza.Models;

namespace STSAnaliza.Services
{
    public class StsMatchListScraper
    {
        private readonly PlaywrightService _pw;

        public StsMatchListScraper(PlaywrightService pw) => _pw = pw;

        /// <summary>
        /// Pobiera listę meczów z listy prematch.
        /// - klika "Wyświetl kolejne" kilka razy (opcjonalnie)
        /// - omija debel/dubel oraz turnieje z ban-listy
        /// - zwraca dane: turniej, home/away, day/time, kurs 1 i 2 (ML)
        /// </summary>
        public async Task<List<MatchListItem>> ExtractMatchListAsync(
            int expandClicks = 3,
            bool sortByTournament = true,
            CancellationToken ct = default)
        {
            var page = await _pw.GetOrCreatePageAsync();

            // Czekamy aż lista kafelków w ogóle istnieje
            await page.Locator("bo-one-ticket-match-tile").First.WaitForAsync(new() { Timeout = 15000 });

            // Rozwijamy listę: "Wyświetl kolejne"
            await ExpandListAsync(page, expandClicks, ct);

            // Pobieramy raw dane z DOM
            var raw = await GetRawTilesAsync(page);

            var list = new List<MatchListItem>(raw.Length);

            for (int i = 0; i < raw.Length; i++)
            {
                var r = raw[i];

                var tournament = (r.Tournament ?? "").Trim();
                var home = (r.Home ?? "").Trim();
                var away = (r.Away ?? "").Trim();
                var dayRaw = (r.Day ?? "").Trim();
                var hourRaw = (r.Hour ?? "").Trim();

                // jeśli nie mamy podstawowych danych — pomijamy
                if (string.IsNullOrWhiteSpace(tournament) ||
                    string.IsNullOrWhiteSpace(home) ||
                    string.IsNullOrWhiteSpace(away))
                    continue;

                // FILTR: omijamy niechciane typy turniejów/meczy
                if (ShouldSkipTournament(tournament))
                    continue;

                var oddA = ParseOdd(r.Odd1);
                var oddB = ParseOdd(r.Odd2);

                // NEW: normalizacja "dzisiaj/jutro" + format dd/MM/yyyy + TZ (Europe/Warsaw, CET/CEST)
                var tz = GetPolandTimeZone();
                var nowPl = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tz);

                var dayResolved = ResolvePolishRelativeDay(dayRaw, nowPl.Date);
                var tzAbbrev = GetTimeZoneAbbreviation(tz, nowPl.DateTime);

                var dayOut = dayResolved.HasValue
                    ? dayResolved.Value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)
                    : dayRaw; // fallback: zostaw jak przyszło, nic nie psujemy

                var hourOut = string.IsNullOrWhiteSpace(hourRaw)
                    ? hourRaw
                    : $"{hourRaw} ({tz.Id}, {tzAbbrev})";

                list.Add(new MatchListItem
                {
                    Tournament = tournament,
                    PlayerA = home,
                    PlayerB = away,
                    Day = dayOut,
                    Hour = hourOut,
                    OddA = oddA,
                    OddB = oddB,
                    SourceIndex = i
                });
            }

            if (sortByTournament)
            {
                list = list
                    .OrderBy(x => x.Tournament, StringComparer.CurrentCultureIgnoreCase)
                    .ThenBy(x => x.Day, StringComparer.CurrentCultureIgnoreCase)
                    .ThenBy(x => x.Hour, StringComparer.CurrentCultureIgnoreCase)
                    .ThenBy(x => x.PlayerA, StringComparer.CurrentCultureIgnoreCase)
                    .ToList();
            }

            return list;
        }

        private static TimeZoneInfo GetPolandTimeZone()
        {
            // Windows: "Central European Standard Time"
            // Linux/macOS: "Europe/Warsaw"
            try { return TimeZoneInfo.FindSystemTimeZoneById("Europe/Warsaw"); }
            catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("Central European Standard Time"); }
        }

        private static DateTime? ResolvePolishRelativeDay(string? dayRaw, DateTime today)
        {
            if (string.IsNullOrWhiteSpace(dayRaw))
                return null;

            var s = dayRaw.Trim().ToLowerInvariant();

            // spotykane warianty
            if (s.Contains("dzisiaj")) return today;
            if (s.Contains("jutro")) return today.AddDays(1);

            // jeśli kiedyś pojawi się "pojutrze"
            if (s.Contains("pojutrze")) return today.AddDays(2);

            // jeśli to już konkretnA data, nie dotykamy (można rozszerzyć o parsing jeśli chcesz)
            return null;
        }

        private static string GetTimeZoneAbbreviation(TimeZoneInfo tz, DateTime localDateTime)
        {
            // DST -> CEST, inaczej CET (dla Polski)
            return tz.IsDaylightSavingTime(localDateTime) ? "CEST" : "CET";
        }

        // =========================
        // DOM extraction (JS)
        // =========================

        private sealed class RawTile
        {
            public string? Tournament { get; set; }
            public string? Home { get; set; }
            public string? Away { get; set; }
            public string? Day { get; set; }
            public string? Hour { get; set; }
            public string? Odd1 { get; set; }
            public string? Odd2 { get; set; }
        }

        private static async Task<RawTile[]> GetRawTilesAsync(IPage page)
        {
            // Fallback selektory: desktop + wersje bez "desktop" (STS często miesza)
            const string script = @"
() => {
  const txt = (root, sel) => {
    const el = root.querySelector(sel);
    return el ? (el.textContent || '').trim() : '';
  };

  const tiles = Array.from(document.querySelectorAll('bo-one-ticket-match-tile'));
  return tiles.map(t => {
    const tournament = txt(t, '.one-ticket-region-info__text, .one-ticket-region-info__text span');

    // ważne: bierzemy osobno home i away, NIE splitujemy po '-'
    const home = txt(t, '.one-ticket-match-tile-event-details-desktop__team-home span, .one-ticket-match-tile-event-details__team-home span, .one-ticket-match-tile-event-details__team-home');
    const away = txt(t, '.one-ticket-match-tile-event-details-desktop__team-away span, .one-ticket-match-tile-event-details__team-away span, .one-ticket-match-tile-event-details__team-away');

    const day  = txt(t, '.match-tile-start-time__date, .match-tile-start-time__date span');
    const hour = txt(t, '.match-tile-start-time__time, .match-tile-start-time__time span');

    // odds: szukamy konkretnie przycisków label 1 i 2 w ramach kafelka
    const btn1 = t.querySelector('button.odds-button__container[aria-label^=""1 ""] span[data-testid=""odds-value""]');
    const btn2 = t.querySelector('button.odds-button__container[aria-label^=""2 ""] span[data-testid=""odds-value""]');

    const odd1 = btn1 ? (btn1.textContent || '').trim() : '';
    const odd2 = btn2 ? (btn2.textContent || '').trim() : '';

    return { tournament, home, away, day, hour, odd1, odd2 };
  });
}";

            var raw = await page.EvaluateAsync<RawTile[]>(script);
            return raw ?? Array.Empty<RawTile>();
        }

        // =========================
        // Expand list ("Wyświetl kolejne")
        // =========================

        private static async Task ExpandListAsync(IPage page, int expandClicks, CancellationToken ct)
        {
            if (expandClicks <= 0) return;

            var tiles = page.Locator("bo-one-ticket-match-tile");
            int lastCount = await tiles.CountAsync();

            for (int i = 0; i < expandClicks; i++)
            {
                ct.ThrowIfCancellationRequested();

                // Spróbuj znaleźć przycisk "Wyświetl kolejne" (STS ma różne klasy, ale tekst zwykle stały)
                var showMore = page.Locator("button:has-text(\"Wyświetl kolejne\")");
                if (await showMore.CountAsync() == 0)
                    break;

                // Scroll pod przycisk (czasem bez tego nie klika)
                await showMore.First.ScrollIntoViewIfNeededAsync();
                await page.WaitForTimeoutAsync(150);

                // Klik i poczekaj aż przybędzie kafelków (albo choć DOM się uspokoi)
                await showMore.First.ClickAsync(new() { Timeout = 5000 });

                // Czekamy aż liczba kafelków wzrośnie albo timeout
                bool grown = false;
                for (int t = 0; t < 20; t++)
                {
                    ct.ThrowIfCancellationRequested();
                    await page.WaitForTimeoutAsync(250);

                    int count = await tiles.CountAsync();
                    if (count > lastCount)
                    {
                        lastCount = count;
                        grown = true;
                        break;
                    }
                }

                // jeśli nie rośnie — nie ma sensu dalej klikać
                if (!grown) break;
            }
        }

        // =========================
        // Filtering + parsing
        // =========================

        private static bool ShouldSkipTournament(string tournament)
        {
            var t = NormalizeKey(tournament);


            // UWAGA: wszystko małymi, bo NormalizeKey zwraca lower + bez polskich znaków
            string[] banned =
            {
                "debel", "dubel", "debl", "doubles",
                "itf", "utr",
                "kwal", "kwalifikacje", "kwalifikacja", "qualification", "qualifying"
            };

            return banned.Any(t.Contains);
        }

        private static string NormalizeKey(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "";

            // whitespace + lower
            s = string.Join(" ", s.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
                .ToLowerInvariant();

            // usuń polskie znaki (mężczyźni -> mezczyzni)
            var formD = s.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(formD.Length);

            foreach (var ch in formD)
            {
                var uc = CharUnicodeInfo.GetUnicodeCategory(ch);
                if (uc != UnicodeCategory.NonSpacingMark)
                    sb.Append(ch);
            }

            return sb.ToString().Normalize(NormalizationForm.FormC);
        }

        private static decimal? ParseOdd(string? odd)
        {
            if (string.IsNullOrWhiteSpace(odd)) return null;

            odd = odd.Trim().Replace(',', '.'); // na wszelki wypadek
            if (decimal.TryParse(odd, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
                return d;

            return null;
        }

        public static string RenderForUi(IEnumerable<MatchListItem> matches)
        {
            if (matches == null) return "";

            var list = matches as IList<MatchListItem> ?? matches.ToList();
            if (list.Count == 0) return "Mecze: 0";

            var sb = new StringBuilder();
            int n = 1;

            foreach (var grp in list
                .GroupBy(m => m.Tournament)
                .OrderBy(g => g.Key, StringComparer.CurrentCultureIgnoreCase))
            {
                sb.AppendLine($"=== {grp.Key} ===");

                foreach (var m in grp
                    .OrderBy(x => x.Day, StringComparer.CurrentCultureIgnoreCase)
                    .ThenBy(x => x.Hour, StringComparer.CurrentCultureIgnoreCase)
                    .ThenBy(x => x.PlayerA, StringComparer.CurrentCultureIgnoreCase))
                {
                    var oddA = m.OddA?.ToString("0.00", CultureInfo.InvariantCulture) ?? "-";
                    var oddB = m.OddB?.ToString("0.00", CultureInfo.InvariantCulture) ?? "-";

                    sb.AppendLine($"{n,2}. {m.PlayerA} vs {m.PlayerB} | {m.Day} {m.Hour} | 1 @{oddA} | 2 @{oddB}");
                    n++;
                }

                sb.AppendLine();
            }

            sb.Insert(0, $"Mecze: {list.Count}\r\n\r\n");
            return sb.ToString().TrimEnd();
        }

    }
}
