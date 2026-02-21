namespace STSAnaliza;

public partial class MainForm
{
    private Task<string> WaitUserMsgAsync(CancellationToken ct)
        => _userInput.Reader.ReadAsync(ct).AsTask();

    private void btnWyslij_Click(object sender, EventArgs e)
    {
        var msg = (textBoxAnswer.Text ?? "").Trim();
        if (string.IsNullOrWhiteSpace(msg))
            return;

        AppendLineSafe(rtbdoc, $"Ty: {msg}");

        textBoxAnswer.Clear();
        textBoxAnswer.Focus();

        _userInput.Writer.TryWrite(msg);
    }
}