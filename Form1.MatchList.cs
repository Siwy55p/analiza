using Microsoft.Extensions.DependencyInjection;
using STSAnaliza.Models;

namespace STSAnaliza;

public partial class Form1
{
    private async void btnDownloadList_Click(object sender, EventArgs e)
    {
        _listMatches = await _listScraper.ExtractMatchListAsync();
        txtListOutput.Text = Services.StsMatchListScraper.RenderForUi(_listMatches);
        dataGridMatchList.DataSource = _listMatches;
    }

    private void button4_Click(object sender, EventArgs e)
    {
        ShowNonModal(
            ref _listTemplateForm,
            () => _sp.GetRequiredService<ListTemplateForm>(),
            onClosed: () => _listTemplateForm = null);
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
            onClosed: () => _optionsListStepsForm = null);
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
        => _listAnalyzeCts?.Cancel();

    private MatchListItem? GetMatchFromRowIndex(int rowIndex)
    {
        if (rowIndex < 0) return null;

        // Najbezpieczniej: DataBoundItem (działa też przy sortowaniu w gridzie)
        var row = dataGridMatchList.Rows[rowIndex];
        if (row?.DataBoundItem is MatchListItem mi)
            return mi;

        // Fallback
        if (_listMatches != null && rowIndex < _listMatches.Count)
            return _listMatches[rowIndex];

        return null;
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

    private async Task AnalyzeMatchListItemAsync(MatchListItem m, int index1Based, int total, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var srStart = _srMeter?.Snapshot();

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
                ct: perCt);

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
        finally
        {
            // ---- Sportradar: ile requestów poszło na ten mecz? ----
            if (_srMeter is not null && srStart is not null)
            {
                var delta = _srMeter.DeltaSince(srStart, topN: 5);
                AppendLineSafe(txtListOutput, $"  [SR] {delta.ToLogLine()}");
            }
        }
    }
}
