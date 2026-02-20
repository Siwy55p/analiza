namespace STSAnaliza.Interfejs;
using STSAnaliza.Models;

public interface IPipelineStepStore
{
    string FilePath { get; }

    Task<List<StepDefinition>> LoadAsync(CancellationToken ct = default);
    Task SaveAsync(List<StepDefinition> steps, CancellationToken ct = default);
}