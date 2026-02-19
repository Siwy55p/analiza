public interface IPipelineStepStore
{
    Task<List<StepDefinition>> LoadAsync(CancellationToken ct = default);
    Task SaveAsync(List<StepDefinition> steps, CancellationToken ct = default);
    string FilePath { get; }
}