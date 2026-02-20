
using Microsoft.Extensions.DependencyInjection;
using STSAnaliza.Interfejs;
using STSAnaliza.Services;
using System.Globalization;
using System.Threading.Channels;

namespace STSAnaliza
{
    public partial class Form1 : Form
    {
        private readonly ITennisApiService _tennisApi;
        private readonly IOpenAiService _openAiService;

        private readonly IMatchBalanceFillBuilder _balanceBuilder;


        private readonly ISportradarDailyMatchResolver _dailyResolver;
        //private readonly IMatchRawFillService _matchRawFill;
        private readonly IMatchRawJsonBuilder _matchRawJsonBuilder;
        private readonly IServiceProvider _sp;

        private readonly IOpenAiLogService _logService;
        private CancellationTokenSource? _logCts;

        private List<MatchListItem> _listMatches = new();

        private readonly IMatchListPipelineStepStore _listStepStore;

        private CancellationTokenSource? _listAnalyzeCts;

        private readonly MatchListPipeline _listPipeline;

        private ListTemplateForm? _listTemplateForm;
        private OptionsForm? _optionsListStepsForm;

        private readonly StsMatchListScraper _listScraper;

        public Form1()
        {
            InitializeComponent();

            _tennisApi = null!;
            _openAiService = null!;
            _balanceBuilder = null!;
            _dailyResolver = null!;
            _matchRawJsonBuilder = null!;
            _sp = null!;
            _logService = null!;
            _listStepStore = null!;
            _listPipeline = null!;
            _listScraper = null!;
        }

        [ActivatorUtilitiesConstructor]
        public Form1(
            IMatchRawJsonBuilder matchRawJsonBuilder,
            ISportradarDailyMatchResolver dailyMatchResolver,
            IMatchBalanceFillBuilder balanceBuilder,
            ITennisApiService tennisApi,
            IMatchListPipelineStepStore listStepStore,
            IServiceProvider sp,
            StsMatchListScraper listScraper,
            MatchListPipeline listPipeline,
            IOpenAiLogService logService,
            IOpenAiService openAiService
        ) : this()
        {
            _listScraper = listScraper;
            _listStepStore = listStepStore;
            _sp = sp;
            _listPipeline = listPipeline;
            _logService = logService;

            _tennisApi = tennisApi;
            _dailyResolver = dailyMatchResolver;
            _matchRawJsonBuilder = matchRawJsonBuilder;
            _balanceBuilder = balanceBuilder;

            _openAiService = openAiService;
        }

        private void ShowNonModal<TForm>(ref TForm? instance, Func<TForm> factory, Action onClosed)
     where TForm : Form
        {
            if (instance == null || instance.IsDisposed)
            {
                instance = factory();
                instance.StartPosition = FormStartPosition.CenterParent;

                instance.FormClosed += (_, __) =>
                {
                    // Nie wywołuj Dispose() tutaj – Form już się zamyka i sam zwalnia zasoby.
                    onClosed();
                };
            }

            if (!instance.Visible)
                instance.Show(this);
            else
                instance.Activate();
        }


        private async void btnDownloadList_Click(object sender, EventArgs e)
        {

            _listMatches = await _listScraper.ExtractMatchListAsync();
            txtListOutput.Text = StsMatchListScraper.RenderForUi(_listMatches);

            dataGridMatchList.DataSource = _listMatches;
        }

        private void button4_Click(object sender, EventArgs e)
        {
            ShowNonModal(
                        ref _listTemplateForm,
                        () => _sp.GetRequiredService<ListTemplateForm>(),
                        onClosed: () => _listTemplateForm = null
                    );
        }

        private void button3_Click(object sender, EventArgs e)
        {
            ShowNonModal(
                ref _optionsListStepsForm,
                () =>
                {
                    var f = new OptionsForm(_listStepStore);
                    f.Text = "Opcje – analiza listy meczów (Tab2)";
                    return f;
                },
                onClosed: () => _optionsListStepsForm = null
            );
        }

        private async Task AnalyzeMatchListItemAsync(MatchListItem m, int index1Based, int total, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            AppendLineSafe(txtListOutput, "");
            AppendLineSafe(txtListOutput, $"[{index1Based}/{total}] {m.Tournament}");
            AppendLineSafe(txtListOutput, $"  {m.PlayerA} vs {m.PlayerB} | {m.Day} {m.Hour}");

            using var perMatchCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            perMatchCts.CancelAfter(TimeSpan.FromMinutes(60));
            var perCt = perMatchCts.Token;

            try
            {
                string? aId = null;
                string? bId = null;

                var hasDate = TryParseDateOnly(m.Day, out var matchDate);

                if (hasDate)
                {
                    (aId, bId) = await _dailyResolver.TryResolveCompetitorIdsAsync(
                        matchDate, m.PlayerA, m.PlayerB, perCt);

                    AppendLineSafe(txtListOutput, $"  [AUTO] competitorId: A={aId ?? "brak"}, B={bId ?? "brak"}");
                }
                else
                {
                    AppendLineSafe(txtListOutput, $"  [WARN] Nie umiem sparsować daty: '{m.Day}' -> lecę fallback po nazwie.");
                }

                var jsonA = aId is not null
                    ? await _matchRawJsonBuilder.BuildByCompetitorIdAsync(m.PlayerA, aId, perCt)
                    : await _matchRawJsonBuilder.BuildAsync(m.PlayerA, perCt);

                var jsonB = bId is not null
                    ? await _matchRawJsonBuilder.BuildByCompetitorIdAsync(m.PlayerB, bId, perCt)
                    : await _matchRawJsonBuilder.BuildAsync(m.PlayerB, perCt);

                (string surface, string indoorOutdoor, string format) = ("brak", "brak", "brak");

                if (hasDate)
                {
                    try
                    {
                        using var metaCts = CancellationTokenSource.CreateLinkedTokenSource(perCt);
                        metaCts.CancelAfter(TimeSpan.FromSeconds(20));

                        (surface, indoorOutdoor, format) = await _tennisApi.GetWtaMatchMetaAsync(
                            m.PlayerA, aId,
                            m.PlayerB, bId,
                            matchDate,
                            metaCts.Token);

                        AppendLineSafe(txtListOutput, $"  [AUTO] META: {surface}, {indoorOutdoor}, {format}");
                    }
                    catch (Exception ex)
                    {
                        AppendLineSafe(txtListOutput, $"  [WARN] META niedostępne: {ex.Message}");
                    }
                }

                var fill11_2 = (aId is not null)
                    ? await _balanceBuilder.BuildByCompetitorIdAsync(m.PlayerA, aId, perCt)
                    : "12M: brak danych\n10W: brak danych\nJakość bilansu: brak danych";

                var fill12_2 = (bId is not null)
                    ? await _balanceBuilder.BuildByCompetitorIdAsync(m.PlayerB, bId, perCt)
                    : "12M: brak danych\n10W: brak danych\nJakość bilansu: brak danych";

                string fill_6;
                try
                {
                    using var rankCts = CancellationTokenSource.CreateLinkedTokenSource(perCt);
                    rankCts.CancelAfter(TimeSpan.FromSeconds(20));

                    AppendLineSafe(txtListOutput, "  [AUTO] Pobieram rankingi (World/Race)...");
                    fill_6 = await _tennisApi.BuildFill6_WorldAndRaceAsync(
                        m.PlayerA, aId,
                        m.PlayerB, bId,
                        rankCts.Token);

                    AppendLineSafe(txtListOutput, "  [AUTO] Rankingi OK.");
                }
                catch (Exception ex)
                {
                    AppendLineSafe(txtListOutput, $"  [WARN] Rankingi niedostępne: {ex.Message}");
                    fill_6 = "Brak danych";
                }

                string fill_13;
                try
                {
                    using var h2hCts = CancellationTokenSource.CreateLinkedTokenSource(perCt);
                    h2hCts.CancelAfter(TimeSpan.FromSeconds(20));

                    fill_13 = (aId is not null && bId is not null)
                        ? await _tennisApi.BuildFill13_H2H_Last12MonthsAsync(m.PlayerA, aId, m.PlayerB, bId, h2hCts.Token)
                        : "H2H (12M): brak danych";
                }
                catch (Exception ex)
                {
                    AppendLineSafe(txtListOutput, $"  [WARN] H2H niedostępne: {ex.Message}");
                    fill_13 = "H2H (12M): brak danych";
                }

                string fill12_3 = "hold%: brak\n1st won%: brak\n2nd serve points won%: brak";
                string fill12_4 = "break%: brak";

                try
                {
                    using var srCts = CancellationTokenSource.CreateLinkedTokenSource(perCt);
                    srCts.CancelAfter(TimeSpan.FromSeconds(120));

                    AppendLineSafe(txtListOutput, "  [AUTO] Serwis/Return B (last 52 weeks overall)...");
                    (fill12_3, fill12_4) = await _tennisApi.BuildFill12_3_12_4_ServeReturn_Last52WeeksOverallAsync(
                        m.PlayerB, bId, srCts.Token);

                    AppendLineSafe(txtListOutput, "  [AUTO] Serwis/Return B OK.");
                }
                catch (Exception ex)
                {
                    AppendLineSafe(txtListOutput, $"  [WARN] Serwis/Return B niedostępne: {ex.Message}");
                }

                string fill11_3 = "hold%: brak\n1st won%: brak\n2nd serve points won%: brak";
                string fill11_4 = "break%: brak";

                try
                {
                    using var aSrvCts = CancellationTokenSource.CreateLinkedTokenSource(perCt);
                    aSrvCts.CancelAfter(TimeSpan.FromSeconds(120));

                    AppendLineSafe(txtListOutput, "  [AUTO] Serwis/Return A (last 52 weeks overall)...");
                    (fill11_3, fill11_4) = await _tennisApi.BuildFill11_3_11_4_ServeReturn_Last52WeeksOverallAsync(
                        m.PlayerA, aId, aSrvCts.Token);

                    AppendLineSafe(txtListOutput, "  [AUTO] Serwis/Return A OK.");
                }
                catch (Exception ex)
                {
                    AppendLineSafe(txtListOutput, $"  [WARN] Serwis/Return A niedostępne: {ex.Message}");
                }

                var prefilled = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["<<FILL_3>>"] = surface,
                    ["<<FILL_4>>"] = indoorOutdoor,
                    ["<<FILL_5>>"] = format,
                    ["<<FILL_6>>"] = fill_6,
                    ["<<FILL_11_1>>"] = jsonA,
                    ["<<FILL_11_2>>"] = fill11_2,
                    ["<<FILL_11_3>>"] = fill11_3,
                    ["<<FILL_11_4>>"] = fill11_4,
                    ["<<FILL_12_1>>"] = jsonB,
                    ["<<FILL_12_2>>"] = fill12_2,
                    ["<<FILL_12_3>>"] = fill12_3,
                    ["<<FILL_12_4>>"] = fill12_4,
                    ["<<FILL_13>>"] = fill_13
                };

                var res = await _listPipeline.AnalyzeAsyncInteractive(
                    m,
                    waitUserMessageAsync: WaitUserMsgAsync,
                    onChat: txt => AppendLineSafe(rtbdoc, txt),
                    onStep: (stepNo, stepTotal, stepTitle) =>
                    {
                        AppendLineSafe(txtListOutput, $"   krok {stepNo}/{stepTotal}: {stepTitle}");
                    },
                    prefilled: prefilled,
                    ct: perCt
                );

                AppendLineSafe(rtbdoc, res);
            }
            catch (OperationCanceledException)
            {
                AppendLineSafe(txtListOutput, "  [PRZERWANO] Timeout lub anulowano.");
            }
            catch (Exception ex)
            {
                AppendLineSafe(txtListOutput, $"  [BŁĄD] {ex.Message}");
            }
        }

        private async void tnListAnalyze_Click(object sender, EventArgs e)
        {
            if (_openAiService is null)
            {
                MessageBox.Show("OpenAI service nie jest zainicjalizowany.", "Błąd",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (_listMatches == null || _listMatches.Count == 0)
            {
                MessageBox.Show("Najpierw pobierz listę meczów.", "Info",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            tnListAnalyze.Enabled = false;
            btnDownloadList.Enabled = false;
            btnCancelAnalyze.Enabled = true;

            _listAnalyzeCts?.Cancel();
            _listAnalyzeCts = new CancellationTokenSource();
            var ct = _listAnalyzeCts.Token;

            AppendLineSafe(txtListOutput, "");
            AppendLineSafe(txtListOutput, $"--- START ANALIZY: {_listMatches.Count} meczów ---");

            try
            {

                for (int i = 0; i < _listMatches.Count; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    var m = _listMatches[i];

                    await AnalyzeMatchListItemAsync(m, i + 1, _listMatches.Count, ct);

                    // mała pauza zmniejsza ryzyko rate-limitów / zrywania połączeń
                    await Task.Delay(250, ct);
                }


                AppendLineSafe(txtListOutput, "");
                AppendLineSafe(txtListOutput, "--- KONIEC ANALIZY ---");
            }
            catch (OperationCanceledException)
            {
                AppendLineSafe(txtListOutput, "");
                AppendLineSafe(txtListOutput, "--- ANALIZA ANULOWANA ---");
            }
            finally
            {
                tnListAnalyze.Enabled = true;
                btnDownloadList.Enabled = true;
                btnCancelAnalyze.Enabled = false;
            }

        }

        private void btnCancelAnalyze_Click(object sender, EventArgs e)
        {
            _listAnalyzeCts?.Cancel();
        }



        private static void AppendLineSafe(RichTextBox rtb, string line)
        {
            if (rtb.IsDisposed) return;

            if (rtb.InvokeRequired)
            {
                rtb.BeginInvoke(new Action(() => AppendLineSafe(rtb, line)));
                return;
            }

            rtb.AppendText(line + Environment.NewLine);
            rtb.ScrollToCaret();
        }
        private MatchListItem? GetMatchFromRowIndex(int rowIndex)
        {
            if (rowIndex < 0) return null;

            // Najbezpieczniej: DataBoundItem (działa też przy sortowaniu w gridzie)
            var row = dataGridMatchList.Rows[rowIndex];
            if (row?.DataBoundItem is MatchListItem mi)
                return mi;

            // Fallback (gdyby DataSource było inne)
            if (_listMatches != null && rowIndex < _listMatches.Count)
                return _listMatches[rowIndex];

            return null;
        }

        private readonly Channel<string> _userInput = Channel.CreateUnbounded<string>(
    new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = false,
        AllowSynchronousContinuations = false
    });

        private Task<string> WaitUserMsgAsync(CancellationToken ct)
            => _userInput.Reader.ReadAsync(ct).AsTask();

        private void AppendChat(string line)
        {
            if (rtbdoc.InvokeRequired)
            {
                rtbdoc.BeginInvoke(new Action<string>(AppendChat), line);
                return;
            }

            if (rtbdoc.TextLength > 0 && !rtbdoc.Text.EndsWith(Environment.NewLine))
                rtbdoc.AppendText(Environment.NewLine);

            rtbdoc.AppendText(line + Environment.NewLine);
            rtbdoc.SelectionStart = rtbdoc.TextLength;
            rtbdoc.ScrollToCaret();
        }

        private void btnWyslij_Click(object sender, EventArgs e)
        {
            var msg = (textBoxAnswer.Text ?? "").Trim();
            if (string.IsNullOrWhiteSpace(msg))
                return;

            // pokaż w rozmowie
            AppendChat($"Ty: {msg}");

            // wyczyść input
            textBoxAnswer.Clear();
            textBoxAnswer.Focus();

            // przekaż do pipeline (jak pipeline akurat czeka - dostanie od razu,
            // jak nie czeka - wiadomość "poczeka" w kanale)
            _userInput.Writer.TryWrite(msg);
        }

        private async void dataGridMatchList_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;

            if (_openAiService is null)
            {
                MessageBox.Show("OpenAI service nie jest zainicjalizowany.", "Błąd",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (_listMatches == null || _listMatches.Count == 0)
            {
                MessageBox.Show("Najpierw pobierz listę meczów.", "Info",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var m = GetMatchFromRowIndex(e.RowIndex);
            if (m is null)
            {
                MessageBox.Show("Nie mogę pobrać meczu z zaznaczonego wiersza.", "Błąd",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            tnListAnalyze.Enabled = false;
            btnDownloadList.Enabled = false;
            btnCancelAnalyze.Enabled = true;
            dataGridMatchList.Enabled = false;

            _listAnalyzeCts?.Cancel();
            _listAnalyzeCts = new CancellationTokenSource();
            var ct = _listAnalyzeCts.Token;

            AppendLineSafe(txtListOutput, "");
            AppendLineSafe(txtListOutput, "--- START ANALIZY: 1 mecz (dwuklik) ---");

            try
            {
                // index “ładny” do logu – przy sortowaniu może nie odpowiadać pozycji w _listMatches i to OK
                await AnalyzeMatchListItemAsync(m, index1Based: e.RowIndex + 1, total: _listMatches.Count, ct);
                AppendLineSafe(txtListOutput, "--- KONIEC ANALIZY (1 mecz) ---");
            }
            catch (OperationCanceledException)
            {
                AppendLineSafe(txtListOutput, "--- ANALIZA ANULOWANA ---");
            }
            finally
            {
                tnListAnalyze.Enabled = true;
                btnDownloadList.Enabled = true;
                btnCancelAnalyze.Enabled = false;
                dataGridMatchList.Enabled = true;
            }
        }


        private async void btnPobierzLog_Click(object sender, EventArgs e)
        {
            var id = (respId.Text ?? "").Trim();
            if (string.IsNullOrWhiteSpace(id))
            {
                MessageBox.Show("Wpisz resp_... w polu respId.", "Brak ID",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            btnPobierzLog.Enabled = false;
            WynikLog.Clear();

            _logCts?.Cancel();
            _logCts = new CancellationTokenSource();
            var ct = _logCts.Token;

            try
            {
                WynikLog.AppendText("Pobieram log...\n");

                var json = await _logService.GetResponseLogAsync(id, ct);

                WynikLog.Clear();
                WynikLog.Text = json;

                // opcjonalnie: od razu na schowek
                Clipboard.SetText(json);

                MessageBox.Show("Log pobrany. Skopiowałem JSON do schowka (Ctrl+V).",
                    "OK", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (OperationCanceledException)
            {
                WynikLog.AppendText("\nAnulowano.");
            }
            catch (Exception ex)
            {
                WynikLog.Clear();
                WynikLog.Text = ex.ToString();
                MessageBox.Show("Błąd pobierania logu:\n" + ex.Message,
                    "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnPobierzLog.Enabled = true;
            }
        }

        private async void btnLoadLastMatchesA_Click(object sender, EventArgs e)
        {
            try
            {
                var id = txtCompetitorIdA.Text.Trim();
                var matches = await _tennisApi.GetLast10MatchesAsync(id, CancellationToken.None);

                txtOutput.Clear();
                foreach (var m in matches)
                {
                    txtOutput.AppendText($"{m.StartTimeUtc:yyyy-MM-dd} | {m.Result} | vs {m.OpponentName} | {m.Score}{Environment.NewLine}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// //HELPER
        /// 
        /// 
        private static bool TryParseDateOnly(string? s, out DateOnly date)
        {
            date = default;
            if (string.IsNullOrWhiteSpace(s)) return false;

            // dopasuj do Twojego formatu z STS
            var formats = new[] { "yyyy-MM-dd", "dd/MM/yyyy", "dd.MM.yyyy", "dd-MM-yyyy" };
            if (DateTime.TryParseExact(s.Trim(), formats, CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var dt))
            {
                date = DateOnly.FromDateTime(dt);
                return true;
            }

            // fallback
            if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
            {
                date = DateOnly.FromDateTime(dt);
                return true;
            }

        return false;
        }

        private async void btnLoadLastMatchesBoth_Click(object sender, EventArgs e)
        {
            try
            {
                btnLoadLastMatchesBoth.Enabled = false;

                // TODO: podstaw z Twojego obiektu meczu:
               // var playerAId = match.PlayerACompetitorId; // "sr:competitor:..."
                //var playerBId = match.PlayerBCompetitorId;

                //await LoadLastMatchesForBothAsync(playerAId, match.PlayerAName, playerBId, match.PlayerBName, CancellationToken.None);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnLoadLastMatchesBoth.Enabled = true;
            }
        }
    }
}
