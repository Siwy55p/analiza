namespace STSAnaliza.Models;

/// <summary>
/// Jak wyznaczać nawierzchnię dla meczów z competitor summaries.
/// Używamy tego głównie po to, żeby ograniczyć requesty do seasons/{id}/info.json.
/// </summary>
public enum SurfaceResolutionMode
{
    /// <summary>Nie próbuj wyznaczać surface (zawsze "brak").</summary>
    None = 0,

    /// <summary>Użyj tylko surface z kontekstu meczu (bez dodatkowych requestów).</summary>
    CtxOnly = 1,

    /// <summary>Najpierw ctx surface, a jeśli brak -> fallback do seasons/{id}/info.json.</summary>
    CtxOrSeason = 2
}
