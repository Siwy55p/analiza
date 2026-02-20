using System.Text.Json.Serialization;

namespace STSAnaliza.Services.SportradarDtos;

public sealed record RankingsResponseDto(
    [property: JsonPropertyName("generated_at")] DateTimeOffset? GeneratedAt,
    [property: JsonPropertyName("rankings")] IReadOnlyList<RankingDto>? Rankings
);

public sealed record RankingDto(
    [property: JsonPropertyName("type_id")] int? TypeId,
    [property: JsonPropertyName("name")] string? Name,          // "WTA" / "ATP"
    [property: JsonPropertyName("year")] int? Year,
    [property: JsonPropertyName("week")] int? Week,
    [property: JsonPropertyName("gender")] string? Gender,      // "women"/"men" (czasem null)
    [property: JsonPropertyName("competitor_rankings")] IReadOnlyList<CompetitorRankingDto>? CompetitorRankings
);

public sealed record CompetitorRankingDto(
    [property: JsonPropertyName("rank")] int? Rank,
    [property: JsonPropertyName("movement")] int? Movement,
    [property: JsonPropertyName("points")] int? Points,
    [property: JsonPropertyName("competitions_played")] int? CompetitionsPlayed,
    [property: JsonPropertyName("competitor")] CompetitorDto? Competitor
);

public sealed record CompetitorDto(
    [property: JsonPropertyName("id")] string? Id,              // "sr:competitor:..."
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("country")] string? Country,
    [property: JsonPropertyName("country_code")] string? CountryCode,
    [property: JsonPropertyName("abbreviation")] string? Abbreviation
);
