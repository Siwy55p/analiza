using Microsoft.Extensions.Options;
using STSAnaliza.Options;
using System.Net.Http.Headers;
using System.Text.Json;

namespace STSAnaliza.Services;

public interface IOpenAiLogService
{
    Task<string> GetResponseLogAsync(string respId, CancellationToken ct = default);
}

public sealed class OpenAiLogService : IOpenAiLogService
{
    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly string? _projectId;

    public OpenAiLogService(HttpClient http, IOptions<OpenAiOptions> opt)
    {
        _http = http;

        var o = opt.Value;

        // ApiKey zwykle przyjdzie z appsettings / ENV provider (OpenAI__ApiKey)
        // ale zostawiam też fallback na OPENAI_API_KEY (Twoje wcześniejsze podejście)
        _apiKey =
            !string.IsNullOrWhiteSpace(o.ApiKey) ? o.ApiKey :
            Environment.GetEnvironmentVariable("OPENAI_API_KEY")
            ?? throw new InvalidOperationException("Brak OpenAI API Key. Ustaw OpenAI:ApiKey lub zmienną OPENAI_API_KEY.");

        _projectId =
            !string.IsNullOrWhiteSpace(o.ProjectId) ? o.ProjectId :
            Environment.GetEnvironmentVariable("OPENAI_PROJECT_ID");
    }

    public async Task<string> GetResponseLogAsync(string respId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(respId))
            throw new ArgumentException("respId jest pusty.", nameof(respId));

        respId = respId.Trim();

        var url = $"https://api.openai.com/v1/responses/{Uri.EscapeDataString(respId)}";

        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

        if (!string.IsNullOrWhiteSpace(_projectId))
            req.Headers.Add("OpenAI-Project", _projectId);

        using var res = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        var body = await res.Content.ReadAsStringAsync(ct);

        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException($"HTTP {(int)res.StatusCode} {res.ReasonPhrase}\n{body}");

        return PrettyJson(body);
    }

    private static string PrettyJson(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true });
    }
}
