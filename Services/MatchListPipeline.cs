using STSAnaliza.Interfejs;
using System.Text;
using System.Text.RegularExpressions;

namespace STSAnaliza.Services;

public sealed class MatchListPipeline
{
    private readonly IOpenAiService _ai;
    private readonly IMatchListPipelineStepStore _store;
    private readonly IMatchListTemplateStore _templateStore;

    public MatchListPipeline(
        IOpenAiService ai,
        IMatchListPipelineStepStore store,
        IMatchListTemplateStore templateStore)
    {
        _ai = ai;
        _store = store;
        _templateStore = templateStore;
    }

    private static readonly Regex FillTokenRegex =
        new(@"<<FILL_[A-Za-z0-9_]+>>", RegexOptions.Compiled);

    private static readonly Regex CurlyFillRefRegex =
        new(@"\{(FILL_[A-Za-z0-9_]+)\}", RegexOptions.Compiled);

    // Kroki, które naprawdę potrzebują pełnego dokumentu jako kontekstu (na start tylko FILL_16)
    private static readonly HashSet<string> StepsThatNeedFullDoc =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "<<FILL_16>>"
        };

    private static string BuildStepContextMessage(
        MatchListItem match,
        string doc,
        string stepPrompt,
        IReadOnlyList<string> stepFillPlaceholders,
        Dictionary<string, string> filled)
    {
        // 1) pełny dokument tylko dla wybranych kroków (start: FILL_16)
        if (stepFillPlaceholders.Any(p => StepsThatNeedFullDoc.Contains(p)))
            return "Kontekst (aktualny dokument):\n\n" + doc;

        // 2) skrócony kontekst
        var sb = new StringBuilder();
        sb.AppendLine("Kontekst (skrócony):");
        sb.AppendLine($"Mecz: {match.PlayerA} vs {match.PlayerB}");
        sb.AppendLine($"Turniej: {match.Tournament}");
        sb.AppendLine($"Start: {match.Day} {match.Hour}");

        if (!string.IsNullOrWhiteSpace(match.Surface)) sb.AppendLine($"Surface: {match.Surface}");
        if (!string.IsNullOrWhiteSpace(match.FormatMeczu)) sb.AppendLine($"Format: {match.FormatMeczu}");

        if (match.OddA > 0 && match.OddB > 0)
            sb.AppendLine($"Odds(A)={match.OddA} Odds(B)={match.OddB}");

        // 3) dołącz TYLKO te wcześniejsze wyniki, które krok referuje w promptach jako {FILL_xxx}
        var needed = ExtractNeededFilledKeyValues(stepPrompt, filled);
        if (needed.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Dane z poprzednich kroków:");

            foreach (var (key, value) in needed)
            {
                sb.Append(key).Append('=').AppendLine(value);
                sb.AppendLine(); // separator
            }
        }

        return sb.ToString().TrimEnd();
    }

    private static List<(string Key, string Value)> ExtractNeededFilledKeyValues(
        string stepPrompt,
        Dictionary<string, string> filled)
    {
        var result = new List<(string Key, string Value)>();
        if (string.IsNullOrWhiteSpace(stepPrompt)) return result;

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match m in CurlyFillRefRegex.Matches(stepPrompt))
        {
            // np. FILL_11_1_JSON_BEZ_PREFIXU
            var token = m.Groups[1].Value;
            var baseToken = token;

            const string suffix = "_JSON_BEZ_PREFIXU";
            var wantsJsonNoPrefix = false;

            if (baseToken.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                wantsJsonNoPrefix = true;
                baseToken = baseToken[..^suffix.Length]; // np. FILL_11_1
            }

            var filledKey = $"<<{baseToken}>>"; // np. <<FILL_11_1>>
            if (!filled.TryGetValue(filledKey, out var value) || string.IsNullOrWhiteSpace(value))
                continue;

            // jeśli krok chce *_JSON_BEZ_PREFIXU, mapujemy na A_JSON/B_JSON (prompt 15 tego oczekuje)
            var displayKey = filledKey;

            if (wantsJsonNoPrefix)
            {
                if (baseToken.Equals("FILL_11_1", StringComparison.OrdinalIgnoreCase))
                    displayKey = "A_JSON";
                else if (baseToken.Equals("FILL_12_1", StringComparison.OrdinalIgnoreCase))
                    displayKey = "B_JSON";
            }

            if (seen.Add(displayKey))
                result.Add((displayKey, value));
        }

        return result;
    }

    public async Task<string> AnalyzeAsyncInteractive(
        MatchListItem match,
        Func<CancellationToken, Task<string>> waitUserMessageAsync,
        Action<string>? onChat = null,
        Action<int, int, string>? onStep = null,
        bool enableWebSearch = false,
        IReadOnlyDictionary<string, string>? prefilled = null,
        CancellationToken ct = default)
    {
        if (waitUserMessageAsync is null)
            throw new ArgumentNullException(nameof(waitUserMessageAsync));

        var template = await _templateStore.LoadAsync(ct);
        var doc = ApplyTemplate(template, match);

        var filled = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Prefill (AUTO dane z API) – wypełnij doc i lokalny słownik
        if (prefilled is not null && prefilled.Count > 0)
        {
            var pre = new Dictionary<string, string>(prefilled, StringComparer.OrdinalIgnoreCase);

            doc = ApplyFillMap(doc, pre);

            foreach (var kv in pre)
                filled[kv.Key] = kv.Value;

            ApplyFillMapToMatch(match, pre);
            doc = ReplaceTokens(doc, match);

            onChat?.Invoke("AUTO: (wypełniono prefilled placeholdery)");
        }

        var steps = await _store.LoadAsync(ct) ?? new List<StepDefinition>();

        var active = steps
            .Where(s => s.Enabled)
            .OrderBy(s => s.Order)
            .ToList();

        int total = active.Count;

        for (int i = 0; i < total; i++)
        {
            ct.ThrowIfCancellationRequested();

            var step = active[i];
            onStep?.Invoke(i + 1, total, step.Title);

            using var perStepCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

            var stepPrompt = ReplaceTokens(step.Prompt, match);

            // Kursy z bukmachera – dokładamy jako "Odds" żeby model nie mieszał z P_imp
            if (step.KursBuch && match.OddA > 0 && match.OddB > 0)
            {
                stepPrompt += $"\nOdds(A)={match.OddA} Odds(B)={match.OddB}";
            }

            var placeholders = ExtractPlaceholdersFromStepPrompt(stepPrompt);

            // Jeśli krok nie operuje na placeholderach -> tryb "pełny dokument"
            if (placeholders.Count == 0)
            {
                bool useWebSearch = step.WebSearch ?? enableWebSearch; // krok ma priorytet, null => global
                _ai.StartChat(stepPrompt, enableWebSearch: useWebSearch);

                var firstUserDoc = BuildInitialUserMessage(doc);
                var assistantDoc = await _ai.SendChatAsync(firstUserDoc, perStepCts.Token);

                while (TryParseNeedUser(assistantDoc, out var questionDoc))
                {
                    onChat?.Invoke($"GPT: {questionDoc}");
                    var userMsg = await waitUserMessageAsync(perStepCts.Token);
                    assistantDoc = await _ai.SendChatAsync(userMsg, perStepCts.Token);
                }

                assistantDoc = NormalizeAssistant(assistantDoc);
                if (!string.IsNullOrWhiteSpace(assistantDoc))
                {
                    doc = assistantDoc;
                    onChat?.Invoke("GPT: (zaktualizowano dokument) " + step.Title);
                }

                continue;
            }

            // Jeśli wszystkie placeholdery już są, pomijamy krok
            if (placeholders.All(p => filled.ContainsKey(p)))
            {
                onChat?.Invoke($"AUTO: pominięto krok '{step.Title}' (placeholdery już wypełnione).");
                continue;
            }

            var expectedFormat = BuildExpectedKvFormat(placeholders);

            var systemPrompt =
                stepPrompt + "\n\n" +
                "Zwróć bloki tylko dla tych placeholderów (każdy dokładnie raz):\n" +
                BuildExpectedKeysList(placeholders) + "\n\n" +
                "Nie zwracaj całego dokumentu. Nie dodawaj komentarzy ani innego tekstu poza blokami.\n" +
                "Jeśli musisz zapytać użytkownika o dane, zwróć WYŁĄCZNIE:\n" +
                "NEED_USER: <pytanie>";

            bool useWebSearch2 = step.WebSearch ?? enableWebSearch; // krok ma priorytet, null => global
            _ai.StartChat(systemPrompt, enableWebSearch: useWebSearch2);

            var firstUser = BuildStepContextMessage(match, doc, stepPrompt, placeholders, filled);

            var assistant = await _ai.SendChatAsync(firstUser, perStepCts.Token);

            // jeśli GPT chce danych -> pytamy usera w UI
            while (TryParseNeedUser(assistant, out var question))
            {
                onChat?.Invoke($"GPT: {question}");
                var userMsg = await waitUserMessageAsync(perStepCts.Token);
                assistant = await _ai.SendChatAsync(userMsg, perStepCts.Token);
            }

            // parse KV i podmień placeholdery w doc
            int tries = 0;
            while (tries < 2)
            {
                if (TryParseFillKv(assistant, placeholders, out var map, out var parseErr))
                {
                    // 1) wypełnij doc (<<FILL_X>> -> wartości)
                    doc = ApplyFillMap(doc, map);

                    // zapamiętaj wartości (żeby nie wysyłać całego doc w kolejnych krokach)
                    foreach (var kv in map)
                        filled[kv.Key] = kv.Value;

                    // 2) zapisz do match (<<FILL_X>> -> match.Surface / match.FormatMeczu / ...)
                    ApplyFillMapToMatch(match, map);

                    // 3) uzupełnij tokeny semantyczne {Surface}/{FormatMeczu} jeśli już znamy
                    doc = ReplaceTokens(doc, match);

                    onChat?.Invoke("GPT: (wypełniono pola w szablonie) " + step.Title);
                    break;
                }

                tries++;

                assistant = await _ai.SendChatAsync(
                    "Popraw format.\n" +
                    "Zwróć WYŁĄCZNIE surowy tekst (bez Markdown, bez ``` i bez ` ).\n" +
                    "Zwróć TYLKO bloki placeholderów w formacie:\n" +
                    expectedFormat + "\n" +
                    "Każdy placeholder dokładnie raz. Nic więcej.\n" +
                    "Błąd: " + parseErr,
                    perStepCts.Token);
            }
        }

        onStep?.Invoke(total, total, "Gotowe");
        return doc;
    }

    private static string BuildExpectedKeysList(IReadOnlyList<string> placeholders)
        => string.Join("\n", placeholders.Select(p => "- " + p));

    private static bool TryParseNeedUser(string text, out string question)
    {
        question = "";
        if (string.IsNullOrWhiteSpace(text)) return false;

        var t = text.Trim();
        if (!t.StartsWith("NEED_USER:", StringComparison.OrdinalIgnoreCase))
            return false;

        question = t["NEED_USER:".Length..].Trim();
        return !string.IsNullOrWhiteSpace(question);
    }

    private static string ReplaceIfKnown(string text, string token, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return text; // zostaw {Surface}/{FormatMeczu} jeśli jeszcze nie znane

        return text.Replace(token, value);
    }

    // ---------- Helpers ----------

    private static string ApplyTemplate(string template, MatchListItem m)
    {
        template ??= BuildBaseDoc(m);

        return template
            .Replace("{PlayerA}", m.PlayerA ?? "")
            .Replace("{PlayerB}", m.PlayerB ?? "")
            .Replace("{Tournament}", m.Tournament ?? "")
            .Replace("{Day}", m.Day ?? "")
            .Replace("{Hour}", m.Hour ?? "");
    }

    // NIE MUTUJEMY StepDefinition – zwracamy string
    private static string ReplaceTokens(string text, MatchListItem m)
    {
        text ??= "";

        var result = text
            .Replace("{PlayerA}", m.PlayerA ?? "")
            .Replace("{PlayerB}", m.PlayerB ?? "")
            .Replace("{Tournament}", m.Tournament ?? "")
            .Replace("{Day}", m.Day ?? "")
            .Replace("{Hour}", m.Hour ?? "");

        // tokeny semantyczne
        result = ReplaceIfKnown(result, "{Surface}", m.Surface);
        result = ReplaceIfKnown(result, "{FormatMeczu}", m.FormatMeczu);

        return result;
    }

    private static readonly Dictionary<string, Action<MatchListItem, string>> FillToMatchMap
        = new(StringComparer.OrdinalIgnoreCase)
        {
            ["<<FILL_3>>"] = (m, v) => m.Surface = v,
            ["<<FILL_5>>"] = (m, v) => m.FormatMeczu = v,
        };

    private static void ApplyFillMapToMatch(MatchListItem match, Dictionary<string, string> map)
    {
        foreach (var (key, val) in map)
        {
            if (FillToMatchMap.TryGetValue(key, out var setter))
                setter(match, val);
        }
    }

    private static IReadOnlyList<string> ExtractPlaceholdersFromStepPrompt(string stepPrompt)
    {
        if (string.IsNullOrWhiteSpace(stepPrompt))
            return Array.Empty<string>();

        var matches = FillTokenRegex.Matches(stepPrompt);

        var list = new List<string>();
        foreach (Match m in matches)
        {
            var token = m.Value;
            if (!list.Contains(token))
                list.Add(token); // zachowaj kolejność
        }
        return list;
    }

    private static string BuildExpectedKvFormat(IReadOnlyList<string> placeholders)
        => string.Join("\n", placeholders.Select(p => $"{p}=..."));

    private static readonly Regex CitationRegex = new(
        @"【\d+†[^】]+】|〖\d+:\d+†[^〗]+〗|\[\d+\]",
        RegexOptions.Compiled);

    private static readonly Regex MarkdownLinkRegex =
        new(@"\[[^\]]*\]\([^)]+\)", RegexOptions.Compiled);

    // Toleruje ozdobniki typu **<<FILL_X>>**=... lub `<<FILL_X>>`=...
    private static readonly Regex FillKvStartRegex =
        new(@"^\s*(?:[*_`]+)?(<<FILL_[A-Za-z0-9_]+>>)(?:[*_`]+)?\s*=\s*(.*)$",
            RegexOptions.Compiled);

    private static bool TryParseFillKv(
        string assistant,
        IReadOnlyList<string> placeholders,
        out Dictionary<string, string> map,
        out string error)
    {
        error = "";
        var localMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        map = localMap;

        if (string.IsNullOrWhiteSpace(assistant))
        {
            error = "Pusta odpowiedź.";
            return false;
        }

        assistant = NormalizeAssistant(assistant);

        var expected = new HashSet<string>(placeholders, StringComparer.OrdinalIgnoreCase);

        string? currentKey = null;
        var sb = new StringBuilder();

        void CommitCurrent()
        {
            if (currentKey is null) return;

            var val = sb.ToString().Trim();
            val = CitationRegex.Replace(val, "").Trim();
            val = MarkdownLinkRegex.Replace(val, "").Trim();

            localMap[currentKey] = val;

            sb.Clear();
            currentKey = null;
        }

        var lines = assistant.Replace("\r\n", "\n").Split('\n');

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd();

            // usuń prefixy list / cytatów: "- ", "* ", "> ", "• "
            var l = Regex.Replace(line, @"^\s*([-*•>]+)\s+", "");

            var m = FillKvStartRegex.Match(l.Trim());
            if (m.Success)
            {
                CommitCurrent();

                var key = m.Groups[1].Value.Trim();
                var firstLineValue = m.Groups[2].Value;

                if (!expected.Contains(key))
                {
                    currentKey = null;
                    sb.Clear();
                    continue;
                }

                currentKey = key;
                sb.Append(firstLineValue);
                continue;
            }

            if (currentKey != null)
            {
                if (sb.Length > 0) sb.AppendLine();
                sb.Append(l);
            }
        }

        CommitCurrent();

        var missing = placeholders.Where(p => !localMap.ContainsKey(p)).ToList();
        if (missing.Count > 0)
        {
            error = "Brakuje: " + string.Join(", ", missing);
            return false;
        }

        return true;
    }

    private static string ApplyFillMap(string doc, Dictionary<string, string> map)
    {
        var updated = doc;
        foreach (var kv in map)
            updated = updated.Replace(kv.Key, kv.Value);

        return updated;
    }

    private static string NormalizeAssistant(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return s ?? "";

        // usuń same linie code-fence (zostawiając treść w środku)
        s = Regex.Replace(s, @"^\s*```[a-zA-Z0-9_-]*\s*$", "", RegexOptions.Multiline);
        s = Regex.Replace(s, @"^\s*```\s*$", "", RegexOptions.Multiline);

        // napraw: <<FILL_18>=...  -> <<FILL_18>>=...
        s = Regex.Replace(
            s,
            @"(?m)^\s*(<<FILL_[A-Za-z0-9_]+>)\s*=",
            "$1>=");

        return s;
    }

    private static string BuildInitialUserMessage(string doc) =>
        "Oto aktualny dokument. Pracuj na nim zgodnie z instrukcją kroku i ZWRÓĆ PEŁNY, ZAKTUALIZOWANY DOKUMENT (bez gadania wokół):\n\n"
        + doc;

    private static string BuildBaseDoc(MatchListItem m) =>
$@"1. Mecz: {m.PlayerA} vs {m.PlayerB}
2. Turniej: {m.Tournament}
3. Start: {m.Day} {m.Hour}

P_est: [DO UZUPEŁNIENIA]
Dane: [DO UZUPEŁNIENIA]
Typ: [DO UZUPEŁNIENIA]
Kursy -> P_imp -> Edge: [DO UZUPEŁNIENIA]
Podsumowanie: [DO UZUPEŁNIENIA]";
}
