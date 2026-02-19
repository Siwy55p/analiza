using OpenAI.Responses;

namespace STSAnaliza;

public interface IOpenAiService
{
    // 1) Jednorazowe (bez pamięci)
    Task<string> SendPromptAsync(string prompt, CancellationToken cancellationToken = default);

    // 2) Rozmowa (z pamięcią)
    void StartChat(string? systemPrompt = null);
    Task<string> SendChatAsync(string userMessage, CancellationToken cancellationToken = default);
    void ClearChatKeepSystem();
}