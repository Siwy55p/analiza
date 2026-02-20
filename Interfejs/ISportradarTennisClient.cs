using STSAnaliza.Services.SportradarDtos;

namespace STSAnaliza.Interfejs;

public interface ISportradarTennisClient
{
    // Core
    Task<CompetitorSummariesResponse> GetCompetitorSummariesAsync(string competitorId, CancellationToken ct);
    Task<CompetitorSummariesResponse> GetDailySummariesAsync(DateOnly date, CancellationToken ct);
    Task<SeasonInfoDto> GetSeasonInfoAsync(string seasonId, CancellationToken ct);

    // Rankings
    Task<RankingsResponseDto> GetRankingsAsync(CancellationToken ct);
    Task<RankingsResponseDto> GetRaceRankingsAsync(CancellationToken ct);

    // Versus
    Task<CompetitorSummariesResponse> GetCompetitorVersusSummariesAsync(string competitorIdA, string competitorIdB, CancellationToken ct);

    // Sport event
    Task<SportEventSummaryDto> GetSportEventSummaryAsync(string sportEventId, CancellationToken ct);
    Task<string> GetSportEventSummaryJsonAsync(string sportEventId, CancellationToken ct);
}
