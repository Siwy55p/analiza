namespace STSAnaliza.Services;

public sealed class SportradarClientOptions
{
    // ile requestów na sekundę (konserwatywnie dla trial)
    public int RequestsPerSecond { get; init; } = 2;

    // maksymalny burst (ile naraz może "wylecieć")
    public int Burst { get; init; } = 2;

    // ile requestów może lecieć równolegle
    public int MaxConcurrency { get; init; } = 2;

    // ile razy retry dla 429
    public int Max429Retries { get; init; } = 3;

    // limit kolejki oczekujących (żeby nie wywalić pamięci)
    public int QueueLimit { get; init; } = 200;
}
