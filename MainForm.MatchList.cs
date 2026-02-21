using Microsoft.Extensions.DependencyInjection;
using STSAnaliza.Models;
using System.ComponentModel;

namespace STSAnaliza;

public partial class MainForm
{
    private async void btnDownloadList_Click(object sender, EventArgs e)
    {
        btnDownloadList.Enabled = false;

        try
        {
            var list = await _listScraper.ExtractMatchListAsync();
            _listMatches = new BindingList<MatchListItem>(list);

            dataGridMatchList.DataSource = _listMatches;

            txtListOutput.Text = Services.StsMatchListScraper.RenderForUi(_listMatches);
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
                // IPipelineStepStore jest u Ciebie wielokrotnie rejestrowany (match + lista),
                // więc tutaj świadomie wymuszamy store listowy.
                var f = ActivatorUtilities.CreateInstance<OptionsForm>(_sp, _listStepStore);
                f.Text = "Opcje – analiza listy meczów (Tab2)";
                return f;
            },
            onClosed: () => _optionsListStepsForm = null);
    }

    private async void tnListAnalyze_Click(object sender, EventArgs e)
    {
        if (_listMatches.Count == 0)
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

        var row = dataGridMatchList.Rows[rowIndex];
        if (row?.DataBoundItem is MatchListItem mi)
            return mi;

        if (rowIndex < _listMatches.Count)
            return _listMatches[rowIndex];

        return null;
    }

    private async void dataGridMatchList_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0) return;

        if (_listMatches.Count == 0)
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

        AppendLineSafe(txtListOutput, "");
        AppendLineSafe(txtListOutput, $"[{index1Based}/{total}] {m.Tournament}");
        AppendLineSafe(txtListOutput, $"  {m.PlayerA} vs {m.PlayerB} | {m.Day} {m.Hour}");

        var res = await _matchListAnalyzer.AnalyzeOneAsync(
            match: m,
            waitUserMessageAsync: WaitUserMsgAsync,
            onChat: txt => AppendLineSafe(rtbOmijaj, txt), // <-- GPT/AUTO logi tutaj
            onStep: (stepNo, stepTotal, stepTitle) =>
            {
                AppendLineSafe(txtListOutput, $"   krok {stepNo}/{stepTotal}: {stepTitle}");
            },
            log: msg => AppendLineSafe(txtListOutput, msg),
            ct: ct);

        if (!string.IsNullOrWhiteSpace(res))
            AppendLineSafe(rtbdoc, res);
    }
}