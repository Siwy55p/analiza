using Microsoft.Playwright;

public class PlaywrightService : IAsyncDisposable
{
    private IPlaywright? _pw;
    private IBrowser? _browser;
    private IBrowserContext? _ctx;
    //private IPage? _page;
    public IPage? _page { get; private set; }

    public async Task<IPage> GetOrCreatePageAsync()
    {
        if (_page != null) return _page;

        _pw = await Playwright.CreateAsync();
        _browser = await _pw.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = false
        });

        _ctx = await _browser.NewContextAsync();
        _page = await _ctx.NewPageAsync();

        // Startowo otwierasz sts.pl (user potem klika co chce)
        await _page.GotoAsync("https://sts.pl");

        return _page;
    }

    public async ValueTask DisposeAsync()
    {
        if (_ctx != null) await _ctx.CloseAsync();
        if (_browser != null) await _browser.CloseAsync();
        _pw?.Dispose();
    }
}
