using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Responses;
using STSAnaliza.Interfejs;
using STSAnaliza.Options;
using STSAnaliza.Services;
using System.ClientModel;
using System.ClientModel.Primitives;
using STSAnaliza.Services.Stores;

namespace STSAnaliza;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        var appDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "STSAnaliza"
        );
        Directory.CreateDirectory(appDataDir);

        var stepsPath = Path.Combine(appDataDir, "pipeline_steps.json");
        var listStepsPath = Path.Combine(appDataDir, "pipeline_steps_list.json");
        var listTemplatePath = Path.Combine(appDataDir, "match_list_template.txt");

        var defaultListTemplate =
            @"1. Mecz: {PlayerA} vs {PlayerB}
2. Turniej: {Tournament}
3. Start: {Day} {Hour}

P_est: [DO UZUPEŁNIENIA]
Dane: [DO UZUPEŁNIENIA]
Typ: [DO UZUPEŁNIENIA]
Kursy -> P_imp -> Edge: [DO UZUPEŁNIENIA]
Podsumowanie: [DO UZUPEŁNIENIA]";

        var host = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration(cfg =>
            {
                cfg.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
                cfg.AddEnvironmentVariables();
            })
            .ConfigureServices((ctx, services) =>
            {
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
                        RetryPolicy = new ClientRetryPolicy(maxRetries: 0)
                    };

#pragma warning disable OPENAI001
                    return new ResponsesClient(opt.Model, new ApiKeyCredential(opt.ApiKey), clientOptions);
#pragma warning restore OPENAI001
                });

                services.AddSingleton<PlaywrightService>();

                services.AddSingleton<OpenAiService>();
                services.AddSingleton<IOpenAiService>(sp => sp.GetRequiredService<OpenAiService>());

                services.AddSingleton<StsMatchListScraper>();

                services.AddSingleton<IPipelineStepStore>(_ => new MatchPipelineStepStore(stepsPath));
                services.AddSingleton<IMatchListPipelineStepStore>(_ => new MatchListPipelineStepStore(listStepsPath));

                services.AddSingleton<IMatchListTemplateStore>(_ =>
                    new TextMatchListTemplateStore(listTemplatePath, defaultListTemplate));

                services.Configure<SportradarOptions>(ctx.Configuration.GetSection("Sportradar"));

                var srClientSection = ctx.Configuration.GetSection("Sportradar:Client");
                var srFallbackSection = ctx.Configuration.GetSection("Sportradar");
                services.Configure<SportradarClientOptions>(srClientSection.Exists() ? srClientSection : srFallbackSection);

                // metryki requestów do Sportradar (global + delta per mecz)
                services.AddSingleton<ISportradarRequestMeter, SportradarRequestMeter>();


                services.AddTransient<SportradarThrottlingHandler>();

                services.AddHttpClient<ISportradarTennisClient, SportradarTennisClient>(client =>
                {
                    client.BaseAddress = new Uri("https://api.sportradar.com/");
                    client.Timeout = TimeSpan.FromMinutes(5);
                })
                .AddHttpMessageHandler<SportradarThrottlingHandler>();

                services.AddSingleton<ITennisApiService, TennisApiService>();
                services.AddSingleton<ICompetitorIdResolver, SportradarCompetitorIdResolver>();
                services.AddSingleton<ISportradarDailyMatchResolver, SportradarDailyMatchResolver>();

                services.AddSingleton<IMatchBalanceFillBuilder, MatchBalanceFillBuilder>();
                services.AddSingleton<IRankService, SportradarRankService>();
                services.AddSingleton<IMatchRawJsonBuilder, MatchRawJsonBuilder>();

                // budowanie prefilled placeholderów (logika "auto" poza Form1)
                services.AddSingleton<IMatchPrefillBuilder, MatchPrefillBuilder>();

                // analiza jednego meczu z listy (pipeline + prefill + metryki)
                services.AddSingleton<IMatchListAnalyzer, MatchListAnalyzer>();

                services.AddSingleton<MatchListPipeline>();

                services.AddHttpClient<IOpenAiLogService, OpenAiLogService>();

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
