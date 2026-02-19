using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OpenAI; // OpenAIClientOptions
using OpenAI.Responses;
using STSAnaliza.Interfejs;
using STSAnaliza.Options;
using STSAnaliza.Services;
using System.ClientModel;      // ApiKeyCredential
using System.ClientModel.Primitives;

namespace STSAnaliza
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            // =========================
            // Œcie¿ki / pliki aplikacji
            // =========================

            var appDataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "STSAnaliza"
            );
            Directory.CreateDirectory(appDataDir);

            // Zak³adka 1 (pojedynczy mecz)
            var stepsPath = Path.Combine(appDataDir, "pipeline_steps.json");

            // Zak³adka 2 (lista meczów)
            var listStepsPath = Path.Combine(appDataDir, "pipeline_steps_list.json");

            // Template dla listy meczów
            var listTemplatePath = Path.Combine(appDataDir, "match_list_template.txt");

            var defaultListTemplate =
                @"1. Mecz: {PlayerA} vs {PlayerB}
2. Turniej: {Tournament}
3. Start: {Day} {Hour}

P_est: [DO UZUPE£NIENIA]
Dane: [DO UZUPE£NIENIA]
Typ: [DO UZUPE£NIENIA]
Kursy -> P_imp -> Edge: [DO UZUPE£NIENIA]
Podsumowanie: [DO UZUPE£NIENIA]";

            // Folder z zapisanymi meczami (obok exe)
            var meczeFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Mecze");

            // =========================
            // Host + DI
            // =========================

            var host = Host.CreateDefaultBuilder()
                .ConfigureAppConfiguration(cfg =>
                {
                    cfg.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
                    cfg.AddEnvironmentVariables();
                })
                .ConfigureServices((ctx, services) =>
                {
                    // -------------------------
                    // Konfiguracja / infrastruktura
                    // -------------------------
                    services.AddMemoryCache();

                    services.Configure<OpenAiOptions>(ctx.Configuration.GetSection("OpenAI"));

                    services.AddSingleton(sp =>
                    {
                        var opt = sp.GetRequiredService<IOptions<OpenAiOptions>>().Value;

                        if (string.IsNullOrWhiteSpace(opt.ApiKey))
                            throw new InvalidOperationException("Brak konfiguracji OpenAI:ApiKey w appsettings.json lub ENV.");

                        if (string.IsNullOrWhiteSpace(opt.Model))
                            throw new InvalidOperationException("Brak konfiguracji OpenAI:Model w appsettings.json lub ENV.");

                        var clientOptions = new OpenAIClientOptions
                        {
                            NetworkTimeout = TimeSpan.FromSeconds(opt.NetworkTimeoutSeconds),
                            // ¿eby retry nie robi³y "lawiny" przy d³ugich requestach
                            RetryPolicy = new ClientRetryPolicy(maxRetries: 0)
                        };

#pragma warning disable OPENAI001
                        return new ResponsesClient(opt.Model, new ApiKeyCredential(opt.ApiKey), clientOptions);
#pragma warning restore OPENAI001
                    });

                    // -------------------------
                    // Core services
                    // -------------------------
                    services.AddSingleton<PlaywrightService>();

                    services.AddSingleton<OpenAiService>();
                    services.AddSingleton<IOpenAiService>(sp => sp.GetRequiredService<OpenAiService>());

                    // -------------------------
                    // Scrapers
                    // -------------------------
                    services.AddSingleton<StsMatchListScraper>();

                    // -------------------------
                    // Stores
                    // -------------------------
                    services.AddSingleton<IPipelineStepStore>(_ => new MatchPipelineStepStore(stepsPath));
                    services.AddSingleton<IMatchListPipelineStepStore>(_ => new MatchListPipelineStepStore(listStepsPath));

                    services.AddSingleton<IMatchListTemplateStore>(_ =>
                        new TextMatchListTemplateStore(listTemplatePath, defaultListTemplate));

                    // -------------------------
                    // Sportradar
                    // -------------------------
                    services.Configure<SportradarOptions>(ctx.Configuration.GetSection("Sportradar"));

                    // Preferuj sekcjê "Sportradar:Client" na limity (RPS/Burst/Concurrency),
                    // ale jeœli jej nie ma, u¿yj "Sportradar" (¿eby nie zmuszaæ Ciê do zmian w appsettings).
                    var srClientSection = ctx.Configuration.GetSection("Sportradar:Client");
                    var srFallbackSection = ctx.Configuration.GetSection("Sportradar");
                    services.Configure<SportradarClientOptions>(srClientSection.Exists() ? srClientSection : srFallbackSection);

                    services.AddTransient<SportradarThrottlingHandler>();

                    services.AddHttpClient<ISportradarTennisClient, SportradarTennisClient>(client =>
                    {
                        client.BaseAddress = new Uri("https://api.sportradar.com/");
                        // Uwaga: limiter/retry mog¹ dodaæ opóŸnienia -> 100s bywa za ma³o przy wiêkszych batchach
                        client.Timeout = TimeSpan.FromMinutes(5);
                    })
                    .AddHttpMessageHandler<SportradarThrottlingHandler>();

                    // Jeœli nadal u¿ywasz w kodzie SportradarRateGate, zostaw.
                    // Jeœli ju¿ nigdzie nie jest u¿ywany -> mo¿esz póŸniej usun¹æ rejestracjê.
                    //services.AddSingleton<SportradarRateGate>();

                    services.AddSingleton<ITennisApiService, TennisApiService>();
                    services.AddSingleton<ICompetitorIdResolver, SportradarCompetitorIdResolver>();
                    services.AddSingleton<ISportradarDailyMatchResolver, SportradarDailyMatchResolver>();

                    services.AddSingleton<IMatchRawFillService, MatchRawFillService>();
                    services.AddSingleton<IMatchBalanceFillBuilder, MatchBalanceFillBuilder>();
                    services.AddSingleton<IRankService, SportradarRankService>();
                    services.AddSingleton<IMatchRawJsonBuilder, MatchRawJsonBuilder>();

                    // -------------------------
                    // Pipelines
                    // -------------------------
                    services.AddSingleton<MatchListPipeline>();

                    services.AddHttpClient<IOpenAiLogService, OpenAiLogService>();

                    // -------------------------
                    // UI (Forms)
                    // -------------------------
                    services.AddTransient<OptionsForm>();
                    services.AddTransient<ListTemplateForm>();
                    services.AddSingleton<Form1>();
                })
                .Build();

            ApplicationConfiguration.Initialize();

            using (host)
            {
                var form = host.Services.GetRequiredService<Form1>();
                Application.Run(form);
            }
        }
    }
}
