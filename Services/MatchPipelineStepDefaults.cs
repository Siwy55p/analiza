namespace STSAnaliza.Services;

/// <summary>
/// Fabryka domyœlnej konfiguracji kroków dla pipeline analizuj¹cego pojedynczy mecz (zak³adka 1).
/// </summary>
/// <remarks>
/// Zwracana konfiguracja jest zapisywana do JSON przy pierwszym uruchomieniu (gdy brak pliku),
/// aby u¿ytkownik móg³ nastêpnie edytowaæ kroki w UI.
/// </remarks>
public static class MatchPipelineStepDefaults
{
    /// <summary>
    /// Tworzy listê domyœlnych kroków pipeline.
    /// </summary>
    /// <returns>Lista kroków w kolejnoœci wykonywania (wg <see cref="StepDefinition.Order"/>).</returns>
    public static List<StepDefinition> Create()
    {
        return new()
        {
            new() { Order = 1, Title = "Format", Prompt = "Uzupe³nij wy³¹cznie pole 4. Format: {BO3/BO5}.", Enabled = true },
            new() { Order = 2, Title = "Nawierzchnia", Prompt = "Uzupe³nij wy³¹cznie pole 5. Nawierzchnia: {hard/clay/grass}.", Enabled = true},
            new() { Order = 3, Title = "Pogoda", Prompt = "Uzupe³nij wy³¹cznie pole 6. Pogoda: {temperatura}.", Enabled = true},
            new() { Order = 4, Title = "Istotne info", Prompt = "Uzupe³nij wy³¹cznie pole 7. Istotne informacje: 1 zdanie.", Enabled = true }
        };
    }
}