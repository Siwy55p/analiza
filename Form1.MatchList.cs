using Microsoft.Extensions.DependencyInjection;
using STSAnaliza.Models;

namespace STSAnaliza;

public partial class Form1
{
    private async void btnDownloadList_Click(object sender, EventArgs e)
    {
        btnDownloadList.Enabled = false;

        try
        {
            _listMatches = await _listScraper.ExtractMatchListAsync();
            txtListOutput.Text = Services.StsMatchListScraper.RenderForUi(_listMatches);
            dataGridMatchList.DataSource = _listMatches;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Błąd pobierania listy: {ex.Message}", "Błąd",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            btnDownloadList.Enabled = true;
        }
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
            // logika "AUTO" (Sportradar + wyliczenia) jest w osobnym serwisie
            var prefill = await _prefillBuilder.BuildAsync(
                m,
                perCt,
                log: msg => AppendLineSafe(txtListOutput, msg));

            var res = await _listPipeline.AnalyzeAsyncInteractive(
                m,
                waitUserMessageAsync: WaitUserMsgAsync,
                onChat: txt => AppendLineSafe(rtbdoc, txt),
                onStep: (stepNo, stepTotal, stepTitle) =>
                {
                    AppendLineSafe(txtListOutput, $"   krok {stepNo}/{stepTotal}: {stepTitle}");
                },
                prefilled: prefill.Prefilled,
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
