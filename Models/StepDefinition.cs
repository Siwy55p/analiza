namespace STSAnaliza.Models;

/// <summary>
/// Konfiguracja pojedynczego kroku wykonywanego w pipeline.
/// </summary>
public sealed class StepDefinition
{
    public int Order { get; set; }
    public string Title { get; set; } = "";
    public string Prompt { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public bool KursBuch { get; set; } = false;

    /// <summary>
    /// Nadpisuje globalne ustawienie web_search dla tego kroku.
    /// null = użyj globalnego.
    /// </summary>
    public bool? WebSearch { get; set; }
}