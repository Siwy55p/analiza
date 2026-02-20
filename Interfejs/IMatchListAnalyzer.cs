using STSAnaliza.Models;

namespace STSAnaliza.Interfejs;

/// <summary>
/// Uruchamia analizę jednego meczu z listy (pipeline + prefill + metryki Sportradar).
/// UI przekazuje callbacki do logowania i interakcji z użytkownikiem.
/// </summary>
public interface IMatchListAnalyzer
{
    /// <summary>
    /// Zwraca finalny dokument (tekst) jeśli pipeline go zwróci. W przypadku timeoutu/błędu może zwrócić null.
    /// Globalny cancel (ct) powinien przerwać całą analizę (throw).
    /// </summary>
    Task<string?> AnalyzeOneAsync(
        MatchListItem match,
        Func<CancellationToken, Task<string>> waitUserMessageAsync,
        Action<string> onChat,
        Action<int, int, string> onStep,
        Action<string> log,
        CancellationToken ct);
}
