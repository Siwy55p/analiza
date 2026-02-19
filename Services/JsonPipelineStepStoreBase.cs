using System.Text.Json;
using STSAnaliza.Interfejs;

namespace STSAnaliza.Services;

/// <summary>
/// Wspólna baza dla store'ów kroków pipeline (JSON na dysku).
/// Minimalizuje duplikację między zakładką 1 (pojedynczy mecz) i zakładką 2 (lista meczów).
/// </summary>
public abstract class JsonPipelineStepStoreBase : IPipelineStepStore
{
    public string FilePath { get; }

    protected JsonPipelineStepStoreBase(string filePath)
    {
        FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
    }

    protected abstract List<StepDefinition> CreateDefaults();

    protected virtual void NormalizeSteps(List<StepDefinition> steps)
    {
        steps.Sort((a, b) => a.Order.CompareTo(b.Order));

        for (int i = 0; i < steps.Count; i++)
        {
            if (steps[i].Order <= 0) steps[i].Order = i + 1;
            if (string.IsNullOrWhiteSpace(steps[i].Title)) steps[i].Title = $"Krok {steps[i].Order}";
        }
    }

    private static readonly JsonSerializerOptions JsonOpt = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public async Task<List<StepDefinition>> LoadAsync(CancellationToken ct = default)
    {
        EnsureDirectory();

        if (!File.Exists(FilePath))
        {
            var defaults = CreateDefaults();
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
            // Uszkodzony JSON -> przywróć defaulty
            var defaults = CreateDefaults();
            await SaveAsync(defaults, ct);
            return defaults;
        }
    }

    public async Task SaveAsync(List<StepDefinition> steps, CancellationToken ct = default)
    {
        if (steps is null) throw new ArgumentNullException(nameof(steps));

        EnsureDirectory();

        var sorted = steps.OrderBy(s => s.Order).ToList();
        NormalizeSteps(sorted);

        var json = JsonSerializer.Serialize(sorted, JsonOpt);
        await File.WriteAllTextAsync(FilePath, json, ct);
    }

    private void EnsureDirectory()
    {
        var dir = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);
    }
}
