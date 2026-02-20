namespace STSAnaliza;

public partial class Form1
{
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
}
