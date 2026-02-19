namespace STSAnaliza.Interfejs;

public interface IOpenAiService
{
    Task<string> SendPromptAsync(string prompt, CancellationToken cancellationToken = default);

    // Dotychczas
    void StartChat(string? systemPrompt = null);


    void StartChat(string? systemPrompt, bool enableWebSearch);

    Task<string> SendChatAsync(string userMessage, CancellationToken cancellationToken = default);

    void ClearChatKeepSystem();
}
