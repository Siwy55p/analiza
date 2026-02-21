namespace STSAnaliza.Interfejs;

public interface ITennisAbstractEloService
{
    /// <summary>
    /// Buduje treść placeholdera <<FILL_7>> (WTA Elo) programistycznie.
    /// Zwraca dokładnie 5 linii (bez prefixu <<FILL_7>>=).
    /// </summary>
    Task<string> BuildFill7Async(string playerA_LastFirst, string playerB_LastFirst, string? surface, CancellationToken ct);
}