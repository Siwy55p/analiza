namespace STSAnaliza.Options;

public sealed class SportradarOptions
{
    public string ApiKey { get; set; } = "";
    public string AccessLevel { get; set; } = "trial"; // trial | production
    public string Locale { get; set; } = "en";          // np. en
}