using System.Text.Json.Serialization;

namespace STSAnaliza.Services.SportradarDtos;

public sealed class SeasonInfoDto
{
    [JsonPropertyName("generated_at")]
    public DateTimeOffset? GeneratedAt { get; set; }

    [JsonPropertyName("season")]
    public SeasonDto? Season { get; set; }
}

public sealed class SeasonDto
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("info")]
    public SeasonExtraInfoDto? Info { get; set; }
}

public sealed class SeasonExtraInfoDto
{
    [JsonPropertyName("surface")]
    public string? Surface { get; set; } // np. "hardcourt_outdoor" :contentReference[oaicite:5]{index=5}
}
