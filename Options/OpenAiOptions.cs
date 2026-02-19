namespace STSAnaliza.Options;

public sealed class OpenAiOptions
{
    public string ApiKey { get; set; } = "";
    public string? ProjectId { get; set; } = null;

    public string Model { get; set; } = "gpt-5-mini";

    // timeout operacji HTTP do OpenAI (domyślnie 5 min)
    public int NetworkTimeoutSeconds { get; set; } = 300;
}
