using System.Net.Http;

namespace STSAnaliza.Services;

internal static class HttpRequestMessageExtensions
{
    public static HttpRequestMessage CloneShallow(this HttpRequestMessage req)
    {
        var clone = new HttpRequestMessage(req.Method, req.RequestUri)
        {
            Version = req.Version,
            VersionPolicy = req.VersionPolicy
        };

        foreach (var h in req.Headers)
            clone.Headers.TryAddWithoutValidation(h.Key, h.Value);

        // Bezpieczne dla GET (bez Content). Jeśli kiedyś dodasz POST -> dopisz klonowanie Content.
        if (req.Content is not null)
            throw new NotSupportedException("CloneShallow obsługuje tylko requesty bez Content (np. GET).");

        return clone;
    }
}
