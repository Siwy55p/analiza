namespace STSAnaliza.Services;

/// <summary>
/// Normalizacja identyfikatorów Sportradar (sr:...).
/// Jedno miejsce = zero duplikatów w serwisach.
/// </summary>
internal static class SportradarId
{
    public static string NormalizeRequired(string id, string paramName)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException($"{paramName} nie może być pusty.", paramName);

        return NormalizeOptional(id);
    }

    public static string NormalizeOptional(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return "";

        var s = id.Trim();
        try { s = Uri.UnescapeDataString(s); } catch { /* ignore */ }
        return s;
    }
}