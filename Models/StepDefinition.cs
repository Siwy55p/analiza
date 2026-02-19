// <summary>
/// Konfiguracja pojedynczego kroku wykonywanego w pipeline.
/// </summary>
public class StepDefinition
{
    public int Order { get; set; }
    public string Title { get; set; } = "";
    public string Prompt { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public bool KursBuch { get; set; } = false;


    public bool? WebSearch { get; set; }
}
