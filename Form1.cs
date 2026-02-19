
using Microsoft.Extensions.DependencyInjection;
using STSAnaliza.Interfejs;
using STSAnaliza.Services;
using System.Globalization;
using System.Reflection.Metadata;
using System.Text.RegularExpressions;
using System.Threading.Channels;

namespace STSAnaliza
{
    public partial class Form1 : Form
    {
        private readonly ITennisApiService _tennisApi;


        private readonly IMatchBalanceFillBuilder _balanceBuilder;


        ISportradarDailyMatchResolver _dailyResolver;
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

        public Form1(IMatchRawJsonBuilder matchRawJsonBuilder,
            //IMatchRawFillService matchRawFill,
            ISportradarDailyMatchResolver  dailyMatchResolver,
            IMatchBalanceFillBuilder balanceBuilder,
            ITennisApiService tennisApi, 
            IMatchListPipelineStepStore listStepStore, 
            IServiceProvider sp, 
            StsMatchListScraper listScraper, 
            MatchListPipeline listPipeline, 
            IOpenAiLogService logService)
        {
            InitializeComponent();
            _listScraper = listScraper;
            _listStepStore = listStepStore;
            _sp = sp;
            _listPipeline = listPipeline;
            _logService = logService;
            _tennisApi = tennisApi;
            _dailyResolver = dailyMatchResolver;
            //_matchRawFill = matchRawFill;
            _matchRawJsonBuilder = matchRawJsonBuilder;
            _balanceBuilder = balanceBuilder;
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
                    // Nie wywo³uj Dispose() tutaj – Form ju¿ siê zamyka i sam zwalnia zasoby.
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

        private async void tnListAnalyze_Click(object sender, EventArgs e)
        {
            if (_listMatches == null || _listMatches.Count == 0)
            {
                MessageBox.Show("Najpierw pobierz listê meczów.", "Info",
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


                    AppendLineSafe(txtListOutput, "");
                    AppendLineSafe(txtListOutput, $"[{i + 1}/{_listMatches.Count}] {m.Tournament}");
                    AppendLineSafe(txtListOutput, $"  {m.PlayerA} vs {m.PlayerB} | {m.Day} {m.Hour}");

                    // per-mecz timeout (np 3 min) + globalny Cancel
                    using var perMatchCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    perMatchCts.CancelAfter(TimeSpan.FromMinutes(60));

                    try
                    {


                        string? aId = null;
                        string? bId = null;

                        if (TryParseDateOnly(m.Day, out var matchDate))
                        {
                            (aId, bId) = await _dailyResolver.TryResolveCompetitorIdsAsync(
                                matchDate, m.PlayerA, m.PlayerB, perMatchCts.Token);

                            AppendLineSafe(txtListOutput, $"  [AUTO] competitorId: A={aId ?? "brak"}, B={bId ?? "brak"}");
                        }
                        else
                        {
                            AppendLineSafe(txtListOutput, $"  [WARN] Nie umiem sparsowaæ daty: '{m.Day}' -> lecê fallback po nazwie.");
                        }

                        var jsonA = aId is not null
                            ? await _matchRawJsonBuilder.BuildByCompetitorIdAsync(m.PlayerA, aId, perMatchCts.Token)
                            : await _matchRawJsonBuilder.BuildAsync(m.PlayerA, perMatchCts.Token);

                        var jsonB = bId is not null
                            ? await _matchRawJsonBuilder.BuildByCompetitorIdAsync(m.PlayerB, bId, perMatchCts.Token)
                            : await _matchRawJsonBuilder.BuildAsync(m.PlayerB, perMatchCts.Token);


                        (string surface, string indoorOutdoor, string format) = ("brak", "brak", "brak");

                        try
                        {
                            using var metaCts = CancellationTokenSource.CreateLinkedTokenSource(perMatchCts.Token);
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
                            AppendLineSafe(txtListOutput, $"  [WARN] META niedostêpne: {ex.Message}");
                        }


                        var fill11_2 = (aId is not null)
                            ? await _balanceBuilder.BuildByCompetitorIdAsync(m.PlayerA, aId, perMatchCts.Token)
                            : "12M: brak danych\n10W: brak danych\nJakoœæ bilansu: brak danych";

                        var fill12_2 = (bId is not null)
                            ? await _balanceBuilder.BuildByCompetitorIdAsync(m.PlayerB, bId, perMatchCts.Token)
                            : "12M: brak danych\n10W: brak danych\nJakoœæ bilansu: brak danych";

                        string fill_6;
                        try
                        {
                            // osobny krótszy timeout na rankingi (¿eby nigdy nie wisia³o 60 min)
                            using var rankCts = CancellationTokenSource.CreateLinkedTokenSource(perMatchCts.Token);
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
                            AppendLineSafe(txtListOutput, $"  [WARN] Rankingi niedostêpne: {ex.Message}");
                            fill_6 = "Brak danych";
                        }

                        string fill_13;
                        try
                        {
                            using var h2hCts = CancellationTokenSource.CreateLinkedTokenSource(perMatchCts.Token);
                            h2hCts.CancelAfter(TimeSpan.FromSeconds(20));

                            fill_13 = (aId is not null && bId is not null)
                                ? await _tennisApi.BuildFill13_H2H_Last12MonthsAsync(m.PlayerA, aId, m.PlayerB, bId, h2hCts.Token)
                                : "H2H (12M): brak danych";
                        }
                        catch (Exception ex)
                        {
                            AppendLineSafe(txtListOutput, $"  [WARN] H2H niedostêpne: {ex.Message}");
                            fill_13 = "H2H (12M): brak danych";
                        }


                        string fill12_3 = "hold%: brak\n1st won%: brak\n2nd serve points won%: brak";
                        string fill12_4 = "break%: brak";

                        try
                        {
                            using var srCts = CancellationTokenSource.CreateLinkedTokenSource(perMatchCts.Token);
                            srCts.CancelAfter(TimeSpan.FromSeconds(120)); // last52w = kilka/kilkanaœcie requestów

                            AppendLineSafe(txtListOutput, "  [AUTO] Serwis/Return B (last 52 weeks overall)...");
                            (fill12_3, fill12_4) = await _tennisApi.BuildFill12_3_12_4_ServeReturn_Last52WeeksOverallAsync(
                                m.PlayerB, bId,
                                srCts.Token);

                            AppendLineSafe(txtListOutput, "  [AUTO] Serwis/Return B OK.");
                        }
                        catch (Exception ex)
                        {
                            AppendLineSafe(txtListOutput, $"  [WARN] Serwis/Return B niedostêpne: {ex.Message}");
                        }

                        string fill11_3 = "hold%: brak\n1st won%: brak\n2nd serve points won%: brak";
                        string fill11_4 = "break%: brak";

                        try
                        {
                            using var aSrvCts = CancellationTokenSource.CreateLinkedTokenSource(perMatchCts.Token);
                            aSrvCts.CancelAfter(TimeSpan.FromSeconds(120));

                            AppendLineSafe(txtListOutput, "  [AUTO] Serwis/Return A (last 52 weeks overall)...");
                            (fill11_3, fill11_4) = await _tennisApi.BuildFill11_3_11_4_ServeReturn_Last52WeeksOverallAsync(
                                m.PlayerA, aId,
                                aSrvCts.Token);

                            AppendLineSafe(txtListOutput, "  [AUTO] Serwis/Return A OK.");
                        }
                        catch (Exception ex)
                        {
                            AppendLineSafe(txtListOutput, $"  [WARN] Serwis/Return A niedostêpne: {ex.Message}");
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
                            ct: perMatchCts.Token
                        );

                        AppendLineSafe(rtbdoc, res);



                        ////#################################### Jedno pytanie i odpowiedz

                        //var res = await _listPipeline.AnalyzeAsync(
                        //        m,
                        //        onStep: (stepNo, stepTotal, stepTitle) =>
                        //        {
                        //            AppendLineSafe(txtListOutput, $"   krok {stepNo}/{stepTotal}: {stepTitle}");

                        //        },
                        //        ct: perMatchCts.Token
                        //    );
                        //rtbdoc.Text += res.ToString();
                        ////############################### to powyzej jest dobrze


                    }
                    catch (OperationCanceledException)
                    {
                        AppendLineSafe(txtListOutput, "  [PRZERWANO] Timeout lub anulowano.");
                    }
                    catch (Exception ex)
                    {
                        AppendLineSafe(txtListOutput, $"  [B£¥D] {ex.Message}");
                    }

                    // ma³a pauza zmniejsza ryzyko rate-limitów / zrywania po³¹czeñ
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

            // poka¿ w rozmowie
            AppendChat($"Ty: {msg}");

            // wyczyœæ input
            textBoxAnswer.Clear();
            textBoxAnswer.Focus();

            // przeka¿ do pipeline (jak pipeline akurat czeka - dostanie od razu,
            // jak nie czeka - wiadomoœæ "poczeka" w kanale)
            _userInput.Writer.TryWrite(msg);
        }



        // Zast¹p MatchItem prawdziwym typem elementu z _listMatches (tym co ma Tournament/PlayerA/PlayerB/Day/Hour)
        private async Task AnalyzeSingleMatchAsync(MatchListItem m, int index1Based, int total, CancellationToken ct)
        {




            AppendLineSafe(txtListOutput, "");
            AppendLineSafe(txtListOutput, $"[{index1Based}/{total}] {m.Tournament}");
            AppendLineSafe(txtListOutput, $"  {m.PlayerA} vs {m.PlayerB} | {m.Day} {m.Hour}");

            using var perMatchCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            perMatchCts.CancelAfter(TimeSpan.FromMinutes(60));



            var jsonA = await _matchRawJsonBuilder.BuildAsync(m.PlayerA, perMatchCts.Token);
            var jsonB = await _matchRawJsonBuilder.BuildAsync(m.PlayerB, perMatchCts.Token);

            var prefilled = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["<<FILL_11_1>>"] = jsonA,
                ["<<FILL_12_1>>"] = jsonB
            };


            try
            {
                var res = await _listPipeline.AnalyzeAsyncInteractive(
                        m,
                        waitUserMessageAsync: WaitUserMsgAsync,
                        onChat: txt => AppendLineSafe(rtbdoc, txt),
                        onStep: (stepNo, stepTotal, stepTitle) =>
                        {
                            AppendLineSafe(txtListOutput, $"   krok {stepNo}/{stepTotal}: {stepTitle}");
                        },
                        prefilled: prefilled,
                        ct: perMatchCts.Token
                    );

                AppendLineSafe(rtbdoc, res);
            }
            catch (OperationCanceledException)
            {
                AppendLineSafe(txtListOutput, "  [PRZERWANO] Timeout lub anulowano.");
            }
            catch (Exception ex)
            {
                AppendLineSafe(txtListOutput, $"  [B£¥D] {ex.Message}");
            }
        }

        private async void dataGridMatchList_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            //if (e.RowIndex < 0) return; // klik w nag³ówek itp.

            //if (_listMatches == null || _listMatches.Count == 0)
            //{
            //    MessageBox.Show("Najpierw pobierz listê meczów.", "Info",
            //        MessageBoxButtons.OK, MessageBoxIcon.Information);
            //    return;
            //}

            //// Najproœciej: indeks wiersza == indeks w _listMatches (jeœli grid jest zasilany t¹ list¹ w tej kolejnoœci)
            //var m = _listMatches[e.RowIndex];

            //tnListAnalyze.Enabled = false;
            //btnDownloadList.Enabled = false;
            //btnCancelAnalyze.Enabled = true;
            //dataGridMatchList.Enabled = false;

            //_listAnalyzeCts?.Cancel();
            //_listAnalyzeCts = new CancellationTokenSource();
            //var ct = _listAnalyzeCts.Token;

            //AppendLineSafe(txtListOutput, "");
            //AppendLineSafe(txtListOutput, $"--- START ANALIZY: 1 mecz (wybrany z listy) ---");

            //try
            //{
            //    ct.ThrowIfCancellationRequested();

            //    // index1Based = e.RowIndex+1, total = _listMatches.Count (¿eby log wygl¹da³ podobnie jak w analizie listy)
            //    await AnalyzeSingleMatchAsync(m, e.RowIndex + 1, _listMatches.Count, ct);

            //    AppendLineSafe(txtListOutput, "");
            //    AppendLineSafe(txtListOutput, "--- KONIEC ANALIZY (1 mecz) ---");
            //}
            //catch (OperationCanceledException)
            //{
            //    AppendLineSafe(txtListOutput, "");
            //    AppendLineSafe(txtListOutput, "--- ANALIZA ANULOWANA ---");
            //}
            //finally
            //{
            //    tnListAnalyze.Enabled = true;
            //    btnDownloadList.Enabled = true;
            //    btnCancelAnalyze.Enabled = false;
            //    dataGridMatchList.Enabled = true;
            //}
        }

        private void panel3_Paint(object sender, PaintEventArgs e)
        {

        }

        private void Form1_Load(object sender, EventArgs e)
        {

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

                MessageBox.Show("Log pobrany. Skopiowa³em JSON do schowka (Ctrl+V).",
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
                MessageBox.Show("B³¹d pobierania logu:\n" + ex.Message,
                    "B³¹d", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                MessageBox.Show(ex.Message, "B³¹d", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                MessageBox.Show(ex.Message, "B³¹d", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnLoadLastMatchesBoth.Enabled = true;
            }
        }
    }
}
