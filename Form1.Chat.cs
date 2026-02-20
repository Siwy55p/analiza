namespace STSAnaliza;

public partial class Form1
{
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

        AppendChat($"Ty: {msg}");

        textBoxAnswer.Clear();
        textBoxAnswer.Focus();

        _userInput.Writer.TryWrite(msg);
    }
}
