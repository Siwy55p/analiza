namespace STSAnaliza;

public partial class Form1
{
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

    private async void btnLoadLastMatchesBoth_Click(object sender, EventArgs e)
    {
        try
        {
            btnLoadLastMatchesBoth.Enabled = false;

            // TODO: podstaw z Twojego obiektu meczu:
            // var playerAId = match.PlayerACompetitorId; // "sr:competitor:..."
            // var playerBId = match.PlayerBCompetitorId;
            // await LoadLastMatchesForBothAsync(playerAId, match.PlayerAName, playerBId, match.PlayerBName, CancellationToken.None);
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
