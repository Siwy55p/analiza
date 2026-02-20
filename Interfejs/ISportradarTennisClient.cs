using STSAnaliza.Services.SportradarDtos;

namespace STSAnaliza.Interfejs;

public interface ISportradarTennisClient
{
    Task<CompetitorSummariesResponse> GetCompetitorSummariesAsync(string competitorId, CancellationToken ct);

    Task<string> GetRankingsJsonAsync(CancellationToken ct);

    Task<string> GetDailySummariesJsonAsync(DateOnly date, CancellationToken ct);

    Task<CompetitorSummariesResponse> GetDailySummariesAsync(DateOnly date, CancellationToken ct);
    Task<SeasonInfoDto> GetSeasonInfoAsync(string seasonId, CancellationToken ct);

    // Rankings (DTO)
    Task<RankingsResponseDto> GetRankingsAsync(CancellationToken ct);
    Task<RankingsResponseDto> GetRaceRankingsAsync(CancellationToken ct);

    Task<CompetitorSummariesResponse> GetCompetitorVersusSummariesAsync(
    string competitorIdA,
    string competitorIdB,
    CancellationToken ct);

    Task<SportEventSummaryDto> GetSportEventSummaryAsync(string sportEventId, CancellationToken ct);

    Task<string> GetSportEventSummaryJsonAsync(string sportEventId, CancellationToken ct);


}