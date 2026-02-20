using System.Text.Json.Serialization;

namespace STSAnaliza.Services.SportradarDtos;

public sealed class SportEventSummaryDto
{
    [JsonPropertyName("generated_at")]
    public DateTimeOffset? GeneratedAt { get; set; }

    // <-- NOWE: potrzebne, żeby dostać season.id + mode.best_of
    [JsonPropertyName("sport_event")]
    public SportEvent? SportEvent { get; set; }

    [JsonPropertyName("sport_event_status")]
    public SportEventStatusDto? SportEventStatus { get; set; }
}

public sealed class SportEventStatusDto
{
    [JsonPropertyName("statistics")]
    public SportEventStatisticsDto? Statistics { get; set; }
}

public sealed class SportEventStatisticsDto
{
    [JsonPropertyName("totals")]
    public SportEventTotalsDto? Totals { get; set; }
}

public sealed class SportEventTotalsDto
{
    [JsonPropertyName("competitors")]
    public List<CompetitorStatsDto>? Competitors { get; set; }
}

public sealed class CompetitorStatsDto
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("statistics")]
    public CompetitorStatisticsDto? Statistics { get; set; }
}

public sealed class CompetitorStatisticsDto
{
    [JsonPropertyName("games_won")]
    public int? GamesWon { get; set; }

    [JsonPropertyName("service_games_won")]
    public int? ServiceGamesWon { get; set; }

    [JsonPropertyName("first_serve_points_won")]
    public int? FirstServePointsWon { get; set; }

    [JsonPropertyName("first_serve_successful")]
    public int? FirstServeSuccessful { get; set; }

    [JsonPropertyName("second_serve_points_won")]
    public int? SecondServePointsWon { get; set; }

    [JsonPropertyName("second_serve_successful")]
    public int? SecondServeSuccessful { get; set; }

    [JsonPropertyName("breakpoints_won")]
    public int? BreakpointsWon { get; set; }

    [JsonPropertyName("total_breakpoints")]
    public int? TotalBreakpoints { get; set; }
}
