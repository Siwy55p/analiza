using System.Net.Http.Headers;

namespace STSAnaliza.Services;

internal static class HttpRequestMessageExtensions
{
    /// <summary>
    /// Tworzy płytką kopię requestu (metoda, URI, nagłówki, opcje).
    /// Uwaga: Content jest kopiowany jako referencja (nie zawsze jest re-sendable).
    /// Dla GET (typowe w Sportradar) to wystarcza.
    /// </summary>
    public static HttpRequestMessage CloneShallow(this HttpRequestMessage request)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri)
        {
            Version = request.Version,
            VersionPolicy = request.VersionPolicy,
            Content = request.Content
        };

        // nagłówki
        foreach (var header in request.Headers)
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);

        // options (w .NET 6+)
        foreach (var opt in request.Options)
            clone.Options.TryAdd(opt.Key, opt.Value);

        // properties (dla kompatybilności jeśli gdzieś używasz)
#pragma warning disable CS0618
        foreach (var prop in request.Properties)
            clone.Properties[prop.Key] = prop.Value;
#pragma warning restore CS0618

        return clone;
    }
}
