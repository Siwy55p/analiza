using Microsoft.Playwright;

namespace STSAnaliza.Services;

/// <summary>
/// Utrzymuje jedną sesję Playwright (browser/context/page) na potrzeby scrapera STS.
/// </summary>
public sealed class PlaywrightService : IAsyncDisposable
{
    private IPlaywright? _pw;
    private IBrowser? _browser;
    private IBrowserContext? _ctx;

    public IPage? Page { get; private set; }

    public async Task<IPage> GetOrCreatePageAsync()
    {
        if (Page is not null)
            return Page;

        _pw = await Playwright.CreateAsync();
        _browser = await _pw.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = false
        });

        _ctx = await _browser.NewContextAsync();
        Page = await _ctx.NewPageAsync();

        await Page.GotoAsync("https://sts.pl");
        return Page;
    }

    public async ValueTask DisposeAsync()
    {
        if (_ctx is not null) await _ctx.CloseAsync();
        if (_browser is not null) await _browser.CloseAsync();
        _pw?.Dispose();
    }
}