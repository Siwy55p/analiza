using Microsoft.Extensions.Logging;
using OpenAI.Responses;
using STSAnaliza.Interfejs;
using System.Text.Json;

#pragma warning disable OPENAI001

namespace STSAnaliza.Services;

public sealed class OpenAiService : IOpenAiService
{
    private readonly ResponsesClient _responsesClient;
    private readonly ILogger<OpenAiService> _logger; 

    // rozmowa “stateful” po stronie Responses API
    private readonly SemaphoreSlim _chatGate = new(1, 1);
    private string? _systemPrompt;
    private string? _previousResponseId;

    // ostatnie resp_... (dla UI)
    public string? LastResponseId { get; private set; }

    public sealed record OpenAiUsageSnapshot(
        int InputTokenCount,
        int OutputTokenCount,
        int TotalTokenCount,
        int CachedTokenCount,
        int ReasoningTokenCount
    );

    public OpenAiUsageSnapshot? LastUsage { get; private set; }

    // logi do pliku
    private readonly SemaphoreSlim _logGate = new(1, 1);
    private readonly string _logPath;

    // jeśli TRUE -> web_search poleci ZAWSZE (drożej/wolniej)
    private readonly bool _forceWebSearchEveryCall = false;

    // ustawiane StartChat(...)
    private bool _enableWebSearch;

    public OpenAiService(ResponsesClient responsesClient, ILogger<OpenAiService> logger) // <-- TU
    {
        _responsesClient = responsesClient;
        _logger = logger;

        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "STSAnaliza",
            "OpenAI"
        );

        Directory.CreateDirectory(dir);
        _logPath = Path.Combine(dir, "openai_calls.jsonl");
    }
    // -----------------------
    // 1) One-shot (bez pamięci)
    // -----------------------
    public Task<string> SendPromptAsync(string prompt, CancellationToken cancellationToken = default)
        => SendPromptAsync(prompt, enableWebSearch: true, cancellationToken);

    public async Task<string> SendPromptAsync(string prompt, bool enableWebSearch, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(prompt))
            throw new ArgumentException("Prompt nie może być pusty.", nameof(prompt));

        cancellationToken.ThrowIfCancellationRequested();

        var t0 = DateTimeOffset.UtcNow;

        try
        {
            var options = CreateWebOptions(
                instructions: null,
                previousResponseId: null,
                enableWebSearch: enableWebSearch
            );

            options.InputItems.Add(ResponseItem.CreateUserMessageItem(prompt));

            ResponseResult response = await _responsesClient.CreateResponseAsync(options, cancellationToken);

            LastResponseId = response.Id;
            LastUsage = ExtractUsage(response);

            var text = ExtractOutputText(response);

            await LogAsync(new
            {
                tsUtc = t0,
                mode = "one-shot",
                responseId = response.Id,
                previousResponseId = (string?)null,
                systemPrompt = (string?)null,
                userPreview = Trunc(prompt, 400),
                outputPreview = Trunc(text, 800),
                forceWebSearch = _forceWebSearchEveryCall,
                webSearchEnabled = enableWebSearch,
                usage = LastUsage,
            }, cancellationToken);

            return text;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OpenAI error (one-shot). Prompt: {Prompt}", prompt);

            await LogAsync(new
            {
                tsUtc = t0,
                mode = "one-shot",
                error = ex.GetType().Name,
                errorMessage = ex.Message,
                userPreview = Trunc(prompt, 400),
            }, cancellationToken);

            throw;
        }
    }

    // -----------------------
    // 2) Tryb rozmowy (z pamięcią)
    // -----------------------
    public void StartChat(string? systemPrompt = null)
       => StartChat(systemPrompt, enableWebSearch: true);

    public void StartChat(string? systemPrompt, bool enableWebSearch)
    {
        _systemPrompt = string.IsNullOrWhiteSpace(systemPrompt) ? null : systemPrompt;

        _chatGate.Wait();
        try
        {
            _previousResponseId = null;        // reset rozmowy
            _enableWebSearch = enableWebSearch; // ustawienie dla rozmowy
        }
        finally
        {
            _chatGate.Release();
        }
    }

    // kompatybilna metoda (używa _enableWebSearch ustawionego w StartChat)
    public Task<string> SendChatAsync(string userMessage, CancellationToken cancellationToken = default)
        => SendChatAsync(userMessage, enableWebSearch: _enableWebSearch, cancellationToken);

    // NOWE: per-call web_search (dla pojedynczego kroku)
    public async Task<string> SendChatAsync(string userMessage, bool enableWebSearch, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userMessage))
            throw new ArgumentException("Wiadomość nie może być pusta.", nameof(userMessage));

        var t0 = DateTimeOffset.UtcNow;

        await _chatGate.WaitAsync(cancellationToken);
        try
        {
            // instructions NIE “dziedziczą się” przy previous_response_id -> wysyłamy zawsze
            var prev = _previousResponseId;

            var options = CreateWebOptions(
                instructions: _systemPrompt,
                previousResponseId: prev,
                enableWebSearch: enableWebSearch
            );

            options.InputItems.Add(ResponseItem.CreateUserMessageItem(userMessage));

            ResponseResult response = await _responsesClient.CreateResponseAsync(options, cancellationToken);

            // zapamiętaj stan rozmowy po stronie API
            _previousResponseId = response.Id;

            LastResponseId = response.Id;
            LastUsage = ExtractUsage(response);

            var text = ExtractOutputText(response);

            await LogAsync(new
            {
                tsUtc = t0,
                mode = "chat",
                responseId = response.Id,
                previousResponseId = prev,
                systemPromptPreview = Trunc(_systemPrompt, 400),
                userPreview = Trunc(userMessage, 400),
                outputPreview = Trunc(text, 800),
                forceWebSearch = _forceWebSearchEveryCall,
                webSearchEnabled = enableWebSearch,
                usage = LastUsage,
            }, cancellationToken);

            return text;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OpenAI error (chat). UserMessage: {UserMessage}", userMessage);

            await LogAsync(new
            {
                tsUtc = t0,
                mode = "chat",
                error = ex.GetType().Name,
                errorMessage = ex.Message,
                userPreview = Trunc(userMessage, 400),
                previousResponseId = _previousResponseId
            }, cancellationToken);

            throw;
        }
        finally
        {
            _chatGate.Release();
        }
    }

    public void ClearChatKeepSystem()
    {
        _chatGate.Wait();
        try
        {
            _previousResponseId = null; // czyścimy kontekst rozmowy, systemPrompt zostaje
        }
        finally
        {
            _chatGate.Release();
        }
    }

    // -----------------------
    // Helpers
    // -----------------------
    private CreateResponseOptions CreateWebOptions(string? instructions, string? previousResponseId, bool enableWebSearch)
    {
        var options = new CreateResponseOptions();

        // web_search tylko gdy enableWebSearch==true (lub wymuszenie globalne)
        var webOn = enableWebSearch || _forceWebSearchEveryCall;
        if (webOn)
        {
            options.Tools.Add(ResponseTool.CreateWebSearchTool());

            // wymuszanie użycia toola tylko jeśli web włączony
            if (_forceWebSearchEveryCall)
                options.ToolChoice = ResponseToolChoice.CreateRequiredChoice();
        }

        if (!string.IsNullOrWhiteSpace(instructions))
            options.Instructions = instructions;

        if (!string.IsNullOrWhiteSpace(previousResponseId))
            options.PreviousResponseId = previousResponseId;

        return options;
    }

    private async Task LogAsync(object entry, CancellationToken ct)
    {
        try
        {
            var line = JsonSerializer.Serialize(entry);

            await _logGate.WaitAsync(ct);
            try
            {
                await File.AppendAllTextAsync(_logPath, line + Environment.NewLine, ct);
            }
            finally
            {
                _logGate.Release();
            }
        }
        catch (Exception ex)
        {
            // logowanie nie może psuć działania aplikacji
            _logger.LogWarning(ex, "Nie udało się dopisać wpisu do logów: {LogPath}", _logPath);
        }
    }

    private static string Trunc(string? s, int max)
        => string.IsNullOrEmpty(s) ? "" : (s.Length <= max ? s : s.Substring(0, max));

    private static string ExtractOutputText(ResponseResult response)
    {
        var parts = new List<string>();

        foreach (ResponseItem item in response.OutputItems)
        {
            if (item is MessageResponseItem message && message.Content is not null)
            {
                foreach (var c in message.Content)
                {
                    if (!string.IsNullOrWhiteSpace(c.Text))
                        parts.Add(c.Text);
                }
            }
        }

        return string.Join("", parts);
    }

    private static OpenAiUsageSnapshot? ExtractUsage(ResponseResult response)
    {
        var u = response.Usage;
        if (u is null)
            return null;

        var cached = u.InputTokenDetails?.CachedTokenCount ?? 0;
        var reasoning = u.OutputTokenDetails?.ReasoningTokenCount ?? 0;

        return new OpenAiUsageSnapshot(
            InputTokenCount: u.InputTokenCount,
            OutputTokenCount: u.OutputTokenCount,
            TotalTokenCount: u.TotalTokenCount,
            CachedTokenCount: cached,
            ReasoningTokenCount: reasoning
        );
    }
}
