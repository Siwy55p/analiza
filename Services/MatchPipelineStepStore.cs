using System.Text.Json;
using STSAnaliza.Interfejs;

namespace STSAnaliza.Services;

/// <summary>
/// Store konfiguracji kroków (<see cref="StepDefinition"/>) dla pipeline analizuj¹cego pojedynczy mecz (zak³adka 1).
/// </summary>
/// <remarks>
/// Odpowiedzialnoœæ:
/// - Odczyt/zapis listy kroków do pliku JSON.
/// - Utworzenie pliku z domyœln¹ konfiguracj¹ na pierwszym uruchomieniu.
/// - Sanity-check/normalizacja danych po wczytaniu.
/// </remarks>
public sealed class MatchPipelineStepStore : IPipelineStepStore
{
    /// <summary>
    /// Pe³na œcie¿ka do pliku JSON z konfiguracj¹ kroków.
    /// </summary>
    public string FilePath { get; }

    /// <summary>
    /// Opcje serializacji JSON (formatowanie + case-insensitive nazwy pól).
    /// </summary>
    private static readonly JsonSerializerOptions JsonOpt = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Tworzy store dla wskazanego pliku JSON.
    /// </summary>
    /// <param name="filePath">Œcie¿ka do pliku JSON.</param>
    public MatchPipelineStepStore(string filePath)
    {
        FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
    }

    /// <summary>
    /// Wczytuje kroki z pliku JSON. Jeœli plik nie istnieje lub jest uszkodzony, tworzy/zwraca kroki domyœlne.
    /// </summary>
    /// <param name="ct">Token anulowania dla operacji I/O.</param>
    /// <returns>Lista kroków (posortowana i znormalizowana).</returns>
    public async Task<List<StepDefinition>> LoadAsync(CancellationToken ct = default)
    {
        EnsureDirectory();

        if (!File.Exists(FilePath))
        {
            var defaults = MatchPipelineStepDefaults.Create();
            await SaveAsync(defaults, ct);
            return defaults;
        }

        try
        {
            var json = await File.ReadAllTextAsync(FilePath, ct);
            var steps = JsonSerializer.Deserialize<List<StepDefinition>>(json, JsonOpt) ?? new();

            NormalizeSteps(steps);
            return steps;
        }
        catch
        {
            // Uszkodzony JSON -> przywróæ defaulty
            var defaults = MatchPipelineStepDefaults.Create();
            await SaveAsync(defaults, ct);
            return defaults;
        }
    }

    /// <summary>
    /// Zapisuje kroki do pliku JSON.
    /// </summary>
    /// <param name="steps">Lista kroków do zapisu.</param>
    /// <param name="ct">Token anulowania dla operacji I/O.</param>
    public async Task SaveAsync(List<StepDefinition> steps, CancellationToken ct = default)
    {
        if (steps == null) throw new ArgumentNullException(nameof(steps));
        EnsureDirectory();

        var sorted = steps.OrderBy(s => s.Order).ToList();
        NormalizeSteps(sorted);

        var json = JsonSerializer.Serialize(sorted, JsonOpt);
        await File.WriteAllTextAsync(FilePath, json, ct);
    }

    /// <summary>
    /// Zapewnia istnienie katalogu docelowego dla <see cref="FilePath"/>.
    /// </summary>
    private void EnsureDirectory()
    {
        var dir = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);
    }

    /// <summary>
    /// Normalizuje listê kroków po wczytaniu (kolejnoœæ, brakuj¹ce pola, domyœlne timeouty).
    /// </summary>
    /// <param name="steps">Lista kroków do znormalizowania.</param>
    /// <remarks>
    /// Celowo nie rzuca wyj¹tków, aby plik edytowany rêcznie nie blokowa³ aplikacji.
    /// </remarks>
    private static void NormalizeSteps(List<StepDefinition> steps)
    {
        steps.Sort((a, b) => a.Order.CompareTo(b.Order));

        for (int i = 0; i < steps.Count; i++)
        {
            if (steps[i].Order <= 0) steps[i].Order = i + 1;
            if (string.IsNullOrWhiteSpace(steps[i].Title)) steps[i].Title = $"Krok {steps[i].Order}";
        }
    }
}