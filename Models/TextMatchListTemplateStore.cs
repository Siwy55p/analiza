using System.Text;

namespace STSAnaliza;

public sealed class TextMatchListTemplateStore : IMatchListTemplateStore
{
    public string FilePath { get; }
    private readonly string _defaultTemplate;

    public TextMatchListTemplateStore(string filePath, string defaultTemplate)
    {
        FilePath = filePath;
        _defaultTemplate = defaultTemplate ?? "";
    }

    public async Task<string> LoadAsync(CancellationToken ct = default)
    {
        var dir = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);

        if (!File.Exists(FilePath))
        {
            await File.WriteAllTextAsync(FilePath, _defaultTemplate, Encoding.UTF8, ct);
            return _defaultTemplate;
        }

        var txt = await File.ReadAllTextAsync(FilePath, Encoding.UTF8, ct);
        return string.IsNullOrWhiteSpace(txt) ? _defaultTemplate : txt;
    }

    public async Task SaveAsync(string template, CancellationToken ct = default)
    {
        var dir = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);

        await File.WriteAllTextAsync(FilePath, template ?? "", Encoding.UTF8, ct);
    }
}
