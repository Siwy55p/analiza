using Microsoft.Extensions.DependencyInjection;
using STSAnaliza.Interfejs;
using STSAnaliza.Models;
using STSAnaliza.Services;
using System.ComponentModel;
using System.Threading.Channels;

namespace STSAnaliza;

public partial class MainForm : Form
{
    private readonly ITennisApiService _tennisApi;
    private readonly IOpenAiService _openAiService;
    private readonly IMatchPrefillBuilder _prefillBuilder;
    private readonly IServiceProvider _sp;
    private readonly IOpenAiLogService _logService;
    private readonly IMatchListPipelineStepStore _listStepStore;
    private readonly MatchListPipeline _listPipeline;
    private readonly StsMatchListScraper _listScraper;
    private readonly ISportradarRequestMeter? _srMeter;

    private CancellationTokenSource? _logCts;
    private CancellationTokenSource? _listAnalyzeCts;

    private BindingList<MatchListItem> _listMatches = new();

    private ListTemplateForm? _listTemplateForm;
    private OptionsForm? _optionsListStepsForm;

    // kanał do interaktywnego pipeline (gdy GPT pyta użytkownika)
    private readonly Channel<string> _userInput = Channel.CreateUnbounded<string>(
        new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        });

    public MainForm()
    {
        InitializeComponent();

        _tennisApi = null!;
        _openAiService = null!;
        _prefillBuilder = null!;
        _sp = null!;
        _logService = null!;
        _listStepStore = null!;
        _listPipeline = null!;
        _listScraper = null!;
        _srMeter = null;
    }

    [ActivatorUtilitiesConstructor]
    public MainForm(
        ITennisApiService tennisApi,
        IMatchPrefillBuilder prefillBuilder,
        IMatchListPipelineStepStore listStepStore,
        IServiceProvider sp,
        StsMatchListScraper listScraper,
        MatchListPipeline listPipeline,
        IOpenAiLogService logService,
        IOpenAiService openAiService,
        ISportradarRequestMeter srMeter
    ) : this()
    {
        _listScraper = listScraper;
        _listStepStore = listStepStore;
        _sp = sp;
        _listPipeline = listPipeline;
        _logService = logService;

        _tennisApi = tennisApi;
        _prefillBuilder = prefillBuilder;

        _openAiService = openAiService;
        _srMeter = srMeter;
    }
}
