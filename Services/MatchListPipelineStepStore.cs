using STSAnaliza.Interfejs;

namespace STSAnaliza.Services;

/// <summary>
/// Store konfiguracji kroków (<see cref="StepDefinition"/>) dla pipeline analizującego listę meczów (zakładka 2).
/// </summary>
public sealed class MatchListPipelineStepStore : JsonPipelineStepStoreBase, IMatchListPipelineStepStore
{
    public MatchListPipelineStepStore(string filePath) : base(filePath) { }

    protected override List<StepDefinition> CreateDefaults() => MatchListStepDefaults.Create();
}
