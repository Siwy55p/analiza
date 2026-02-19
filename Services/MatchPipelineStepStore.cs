using STSAnaliza.Interfejs;

namespace STSAnaliza.Services;

/// <summary>
/// Store kroków pipeline dla zakładki 1 (pojedynczy mecz).
/// Bez duplikowania logiki (dziedziczymy z JsonPipelineStepStoreBase).
/// </summary>
public sealed class MatchPipelineStepStore : JsonPipelineStepStoreBase
{
    public MatchPipelineStepStore(string filePath) : base(filePath) { }

    protected override List<StepDefinition> CreateDefaults()
        => MatchPipelineStepDefaults.Create();
}