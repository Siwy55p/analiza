namespace STSAnaliza.Services;

/// <summary>
/// Fabryka domyślnej konfiguracji kroków dla pipeline analizującego pojedynczy mecz (zakładka 1).
/// </summary>
/// <remarks>
/// Zwracana konfiguracja jest zapisywana do JSON przy pierwszym uruchomieniu (gdy brak pliku),
/// aby użytkownik mógł następnie edytować kroki w UI.
/// </remarks>
public static class MatchPipelineStepDefaults
{
    /// <summary>
    /// Tworzy listę domyślnych kroków pipeline.
    /// </summary>
    /// <returns>Lista kroków w kolejności wykonywania (wg <see cref="StepDefinition.Order"/>).</returns>
    public static List<StepDefinition> Create()
    {
        return new()
        {
            new() { Order = 1, Title = "Format", Prompt = "Uzupełnij wyłącznie pole 4. Format: {BO3/BO5}.", Enabled = true },
            new() { Order = 2, Title = "Nawierzchnia", Prompt = "Uzupełnij wyłącznie pole 5. Nawierzchnia: {hard/clay/grass}.", Enabled = true},
            new() { Order = 3, Title = "Pogoda", Prompt = "Uzupełnij wyłącznie pole 6. Pogoda: {temperatura}.", Enabled = true},
            new() { Order = 4, Title = "Istotne info", Prompt = "Uzupełnij wyłącznie pole 7. Istotne informacje: 1 zdanie.", Enabled = true }
        };
    }
}