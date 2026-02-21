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
    private readonly IServiceProvider _sp;
    private readonly IOpenAiLogService _logService;

    private readonly IMatchListPipelineStepStore _listStepStore;
    private readonly StsMatchListScraper _listScraper;
    private readonly IMatchListAnalyzer _matchListAnalyzer;

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

        // konstruktor dla designera WinForms
        _tennisApi = null!;
        _sp = null!;
        _logService = null!;

        _listStepStore = null!;
        _listScraper = null!;
        _matchListAnalyzer = null!;
    }

    [ActivatorUtilitiesConstructor]
    public MainForm(
        ITennisApiService tennisApi,
        IMatchListPipelineStepStore listStepStore,
        StsMatchListScraper listScraper,
        IMatchListAnalyzer matchListAnalyzer,
        IOpenAiLogService logService,
        IServiceProvider sp
    ) : this()
    {
        _tennisApi = tennisApi;
        _listStepStore = listStepStore;
        _listScraper = listScraper;
        _matchListAnalyzer = matchListAnalyzer;
        _logService = logService;
        _sp = sp;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        // przerwij ewentualne trwające operacje
        try { _logCts?.Cancel(); } catch { }
        try { _listAnalyzeCts?.Cancel(); } catch { }

        _logCts?.Dispose();
        _logCts = null;

        _listAnalyzeCts?.Dispose();
        _listAnalyzeCts = null;

        // zamknij kanał wejścia użytkownika (jeśli ktoś czeka na ReadAsync)
        try { _userInput.Writer.TryComplete(); } catch { }

        base.OnFormClosing(e);
    }
}