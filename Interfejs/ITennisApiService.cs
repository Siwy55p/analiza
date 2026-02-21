using STSAnaliza.Models;

namespace STSAnaliza.Interfejs;

public interface ITennisApiService
{
    Task<IReadOnlyList<PlayerMatchSummary>> GetLast10MatchesAsync(string competitorId, CancellationToken ct);

    Task<(IReadOnlyList<PlayerMatchSummary> A, IReadOnlyList<PlayerMatchSummary> B)>
    GetLast10MatchesForBothAsync(string competitorIdA, string competitorIdB, CancellationToken ct);

    Task<IReadOnlyList<PlayerMatchSummary>> GetLast10MatchesByNameAsync(string playerName, CancellationToken ct);
    Task<IReadOnlyList<PlayerMatchSummary>> GetRecentClosedSinglesMatchesAsync(string competitorId, CancellationToken ct);

    Task<string> BuildFill6_WorldAndRaceAsync(string playerAName, string playerBName, CancellationToken ct);

    Task<string> BuildFill6_WorldAndRaceAsync(
    string playerAName, string? competitorIdA,
    string playerBName, string? competitorIdB,
    CancellationToken ct);

    Task<string> BuildFill13_H2H_Last12MonthsAsync(
    string playerAName, string? competitorIdA,
    string playerBName, string? competitorIdB,
    CancellationToken ct);


    Task<(string Fill11_3, string Fill11_4)> BuildFill11_3_11_4_ServeReturn_Last10Async(
    string playerAName, string? competitorIdA,
    string playerBName, string? competitorIdB,
    CancellationToken ct);


    Task<(string Fill11_3, string Fill11_4)> BuildFill11_3_11_4_ServeReturn_MostRecentWithStatsAsync(
    string playerAName, string? competitorIdA,
    string playerBName, string? competitorIdB,
    CancellationToken ct);

    Task<(string Fill12_3, string Fill12_4)> BuildFill12_3_12_4_ServeReturn_Last52WeeksOverallAsync(
    string playerBName, string? competitorIdB,
    CancellationToken ct);

    Task<(string Surface, string IndoorOutdoor, string Format)> GetWtaMatchMetaAsync(
    string playerAName, string? competitorIdA,
    string playerBName, string? competitorIdB,
    DateOnly matchDate,
    CancellationToken ct);

    Task<(string Fill11_3, string Fill11_4)> BuildFill11_3_11_4_ServeReturn_Last52WeeksOverallAsync(
    string playerAName, string? competitorIdA,
    CancellationToken ct);
}
