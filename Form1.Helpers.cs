namespace STSAnaliza;

public partial class Form1
{
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

}
