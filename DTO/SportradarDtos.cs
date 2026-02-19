using System.Text.Json.Serialization;

namespace STSAnaliza.Services.SportradarDtos;

public sealed class CompetitorSummariesResponse
{
    [JsonPropertyName("generated_at")]
    public DateTimeOffset? GeneratedAt { get; set; }

    [JsonPropertyName("summaries")]
    public List<SummaryItem>? Summaries { get; set; }
}

public sealed class SummaryItem
{
    [JsonPropertyName("sport_event")]
    public SportEvent? SportEvent { get; set; }

    [JsonPropertyName("sport_event_status")]
    public SportEventStatus? SportEventStatus { get; set; }

    [JsonPropertyName("sport_event_context")]
    public SportEventContext? SportEventContext { get; set; }


}

public sealed class SportEvent
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("start_time")]
    public DateTimeOffset? StartTime { get; set; }

    [JsonPropertyName("competitors")]
    public List<Competitor>? Competitors { get; set; }

    // <-- NOWE: w sport_event_summary sport_event_context siedzi w sport_event
    [JsonPropertyName("sport_event_context")]
    public SportEventContext? SportEventContext { get; set; }
}

public sealed class Competitor
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    // "home" / "away"
    [JsonPropertyName("qualifier")]
    public string? Qualifier { get; set; }
}

public sealed class SportEventStatus
{
    [JsonPropertyName("status")]
    public string? Status { get; set; } // np. "closed"

    [JsonPropertyName("winner_id")]
    public string? WinnerId { get; set; }

    [JsonPropertyName("period_scores")]
    public List<PeriodScore>? PeriodScores { get; set; }
}

public sealed class PeriodScore
{
    [JsonPropertyName("home_score")]
    public int? HomeScore { get; set; }

    [JsonPropertyName("away_score")]
    public int? AwayScore { get; set; }

    // tie-break w przykładach też występuje (opcjonalnie)
    [JsonPropertyName("home_tiebreak_score")]
    public int? HomeTiebreakScore { get; set; }

    [JsonPropertyName("away_tiebreak_score")]
    public int? AwayTiebreakScore { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; } // "set"

    [JsonPropertyName("number")]
    public int? Number { get; set; }
}
public sealed class SportEventContext
{
    // NOWE:
    [JsonPropertyName("category")]
    public CategoryInfo? Category { get; set; }

    [JsonPropertyName("competition")]
    public CompetitionInfo? Competition { get; set; }

    [JsonPropertyName("season")]
    public SeasonShortInfo? Season { get; set; }

    [JsonPropertyName("mode")]
    public ModeInfo? Mode { get; set; }

    // było:
    [JsonPropertyName("sport_event_conditions")]
    public SportEventConditions? SportEventConditions { get; set; }
}

public sealed class CategoryInfo
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; } // "WTA", "ATP"
}

public sealed class CompetitionInfo
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; } // "singles" / "doubles"

    [JsonPropertyName("gender")]
    public string? Gender { get; set; } // "women" / "men" (często)
}

public sealed class SeasonShortInfo
{
    [JsonPropertyName("id")]
    public string? Id { get; set; } // sr:season:...
}

public sealed class ModeInfo
{
    [JsonPropertyName("best_of")]
    public int? BestOf { get; set; } // 3 lub 5 :contentReference[oaicite:3]{index=3}
}
public sealed class SportEventConditions
{
    [JsonPropertyName("surface")]
    public SurfaceInfo? Surface { get; set; }
}

public sealed class SurfaceInfo
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }
}