namespace STSAnaliza;

using STSAnaliza.Interfejs;
public partial class ListTemplateForm : Form
{
    private readonly IMatchListTemplateStore _store;

    public ListTemplateForm(IMatchListTemplateStore store)
    {
        InitializeComponent();
        _store = store;

        lblPath.Text = _store.FilePath;

        rtbTemplate.WordWrap = false;
        rtbTemplate.AcceptsTab = true;
        rtbTemplate.DetectUrls = false;
    }

    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);
        await ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        var txt = await _store.LoadAsync();
        rtbTemplate.Text = txt;
    }

    private async void btnReload_Click(object sender, EventArgs e)
    {
        await ReloadAsync();
    }

    private async void btnSave_Click(object sender, EventArgs e)
    {
        await _store.SaveAsync(rtbTemplate.Text);
        MessageBox.Show("Zapisano szablon.", "OK", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void btnHelp_Click(object sender, EventArgs e)
    {
        MessageBox.Show(
@"Placeholdery:
{PlayerA}, {PlayerB}, {Tournament}, {Day}, {Hour}",
            "Pomoc",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }
}
