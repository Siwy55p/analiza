using System.ComponentModel;
using STSAnaliza.Interfejs;

namespace STSAnaliza
{
    /// <summary>
    /// Okno opcji służące do edycji i zapisu kroków pipeline (konfiguracja <see cref="StepDefinition"/>).
    /// </summary>
    public partial class OptionsForm : Form
    {
        private readonly IPipelineStepStore _store;
        private BindingList<StepDefinition> _binding = new();

        public OptionsForm(IPipelineStepStore store)
        {
            InitializeComponent();

            _store = store;
            lblPathBox.Text = _store.FilePath;
            lblPath.Text = _store.FilePath;

            dgvSteps.AutoGenerateColumns = true;
            dgvSteps.AllowUserToAddRows = true;
            dgvSteps.AllowUserToDeleteRows = true;
            dgvSteps.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvSteps.MultiSelect = false;

            dgvSteps.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
            dgvSteps.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            dgvSteps.AllowUserToResizeRows = true;

            dgvSteps.SelectionChanged += dgvSteps_SelectionChanged;
            txtPrompt.TextChanged += txtPrompt_TextChanged;
            dgvSteps.KeyDown += dgvSteps_KeyDown;

            // klucz: NIE autosizuj wysokości wierszy do całej treści
            dgvSteps.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;

            // ustaw sensowną stałą wysokość (np. 2–3 linie)
            dgvSteps.RowTemplate.Height = 60;

            // możesz zostawić zawijanie, ale wysokość i tak stała
            dgvSteps.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            dgvSteps.RowsDefaultCellStyle.Alignment = DataGridViewContentAlignment.TopLeft;

            RefreshPromptOnlyUiState();
        }

        //ttrzy stany
        private void EnsureWebSearchCheckboxColumn(DataGridView grid)
        {
            const string colName = "WebSearch";

            // jeśli istnieje auto-wygenerowana kolumna i nie jest checkboxem -> podmień
            if (grid.Columns.Contains(colName) && grid.Columns[colName] is not DataGridViewCheckBoxColumn)
            {
                int idx = grid.Columns[colName].Index;
                grid.Columns.Remove(colName);

                var col = new DataGridViewCheckBoxColumn
                {
                    Name = colName,
                    HeaderText = colName,
                    DataPropertyName = nameof(StepDefinition.WebSearch), // MUSI być nazwa właściwości
                    ThreeState = true,         // bo bool?
                    TrueValue = true,
                    FalseValue = false,
                    IndeterminateValue = null, // null = stan "dziedzicz"
                    Width = 70
                };

                grid.Columns.Insert(idx, col);
            }
            else if (!grid.Columns.Contains(colName))
            {
                // jeśli w ogóle nie ma kolumny, dodaj na koniec
                grid.Columns.Add(new DataGridViewCheckBoxColumn
                {
                    Name = colName,
                    HeaderText = colName,
                    DataPropertyName = nameof(StepDefinition.WebSearch),
                    ThreeState = true,
                    TrueValue = true,
                    FalseValue = false,
                    IndeterminateValue = null,
                    Width = 70
                });
            }

            // żeby klik checkboxa od razu commitował wartość do obiektu (BindingSource / lista)
            grid.CurrentCellDirtyStateChanged -= Grid_CurrentCellDirtyStateChanged;
            grid.CurrentCellDirtyStateChanged += Grid_CurrentCellDirtyStateChanged;

            // zabezpieczenie przed DataError (np. null/indeterminate)
            grid.DataError -= Grid_DataError;
            grid.DataError += Grid_DataError;
        }

        private void Grid_CurrentCellDirtyStateChanged(object? sender, EventArgs e)
        {
            var grid = (DataGridView)sender!;
            if (grid.IsCurrentCellDirty)
                grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        }

        private void Grid_DataError(object? sender, DataGridViewDataErrorEventArgs e)
        {
            e.ThrowException = false;
        }
        //koniec trzech stanow

        protected override async void OnShown(EventArgs e)
        {
            base.OnShown(e);
            await ReloadAsync();
        }

        private async Task ReloadAsync()
        {
            var steps = await _store.LoadAsync();

            _binding = new BindingList<StepDefinition>(
                steps.OrderBy(s => s.Order).ToList()
            );

            dgvSteps.DataSource = _binding;

            ConfigureGridColumns();

            EnsureWebSearchCheckboxColumn(dgvSteps);

            foreach (DataGridViewColumn col in dgvSteps.Columns)
                col.SortMode = DataGridViewColumnSortMode.NotSortable;
        }

        private async void btnReload_Click(object sender, EventArgs e) => await ReloadAsync();

        private async void btnSave_Click(object sender, EventArgs e)
        {
            dgvSteps.EndEdit();

            // usuń puste wiersze dodane przez grid
            var list = _binding
                .Where(s => !(s.Order == 0 && string.IsNullOrWhiteSpace(s.Title) && string.IsNullOrWhiteSpace(s.Prompt)))
                .ToList();

            ReorderOrders(list);

            await _store.SaveAsync(list);

            MessageBox.Show("Zapisano.", "OK", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private static void ReorderOrders(List<StepDefinition> steps)
        {
            for (int i = 0; i < steps.Count; i++)
                steps[i].Order = i + 1;
        }

        private void ReorderOrders()
        {
            ReorderOrders(_binding.ToList());
            dgvSteps.Refresh();
        }

        private int? GetSelectedIndex()
        {
            var idx = dgvSteps.CurrentRow?.Index ?? -1;
            if (idx < 0 || idx >= _binding.Count) return null;
            return idx;
        }

        private void SelectRow(int index)
        {
            if (index < 0 || index >= dgvSteps.Rows.Count) return;
            dgvSteps.ClearSelection();
            dgvSteps.Rows[index].Selected = true;
            dgvSteps.CurrentCell = dgvSteps.Rows[index].Cells[0];
        }

        private void MoveRow(int delta)
        {
            dgvSteps.EndEdit();

            var idx = GetSelectedIndex();
            if (idx == null) return;

            int from = idx.Value;
            int to = from + delta;
            if (to < 0 || to >= _binding.Count) return;

            var item = _binding[from];
            _binding.RemoveAt(from);
            _binding.Insert(to, item);

            ReorderOrders();
            SelectRow(to);
        }

        private void btnUp_Click(object sender, EventArgs e) => MoveRow(-1);

        private void btnDown_Click(object sender, EventArgs e) => MoveRow(+1);

        private void btnDuplicate_Click(object sender, EventArgs e)
        {
            dgvSteps.EndEdit();

            var idx = GetSelectedIndex();
            if (idx == null) return;

            int i = idx.Value;
            var src = _binding[i];

            var copy = new StepDefinition
            {
                Order = src.Order,
                Title = string.IsNullOrWhiteSpace(src.Title) ? "Kopia" : $"{src.Title} (copy)",
                Prompt = src.Prompt,
                Enabled = src.Enabled,
                KursBuch = src.KursBuch,
                WebSearch = src.WebSearch,
            };

            _binding.Insert(i + 1, copy);

            ReorderOrders();
            SelectRow(i + 1);
        }

        private void ConfigureGridColumns()
        {
            if (dgvSteps.Columns.Count == 0) return;

            dgvSteps.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;

            if (dgvSteps.Columns["Order"] != null) dgvSteps.Columns["Order"].Width = 20;
            if (dgvSteps.Columns["Enabled"] != null) dgvSteps.Columns["Enabled"].Width = 30;

            if (dgvSteps.Columns["KursBuch"] != null) dgvSteps.Columns["KursBuch"].Width = 95;

            if (dgvSteps.Columns["Title"] != null)
                dgvSteps.Columns["Title"].Width = 50;

            if (dgvSteps.Columns["Prompt"] != null)
            {
                dgvSteps.Columns["Prompt"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                dgvSteps.Columns["Prompt"].DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            }

            dgvSteps.RowTemplate.Height = 60;
        }

        private bool _syncingPrompt;

        private void dgvSteps_SelectionChanged(object? sender, EventArgs e)
        {
            var idx = dgvSteps.CurrentRow?.Index ?? -1;
            if (idx < 0 || idx >= _binding.Count) return;

            _syncingPrompt = true;

            txtPrompt.Text = _binding[idx].Prompt ?? "";

            chkPromptOnly.Checked = _binding[idx].KursBuch;

            _syncingPrompt = false;

            RefreshPromptOnlyUiState();
        }

        private void txtPrompt_TextChanged(object? sender, EventArgs e)
        {
            if (_syncingPrompt) return;

            var idx = dgvSteps.CurrentRow?.Index ?? -1;
            if (idx < 0 || idx >= _binding.Count) return;

            _binding[idx].Prompt = txtPrompt.Text;

            var col = dgvSteps.Columns["Prompt"]?.Index ?? -1;
            if (col >= 0)
                dgvSteps.InvalidateCell(col, idx);
        }

        private void chkPromptOnly_CheckedChanged(object? sender, EventArgs e)
        {
            if (_syncingPrompt) return;

            var idx = dgvSteps.CurrentRow?.Index ?? -1;
            if (idx < 0 || idx >= _binding.Count) return;

            _binding[idx].KursBuch = chkPromptOnly.Checked;

            dgvSteps.Refresh();
            RefreshPromptOnlyUiState();
        }

        private void RefreshPromptOnlyUiState()
        {
            // jeżeli masz dodatkowe sterowanie UI zależne od KursBuch – zostaw tutaj
        }

        private void dgvSteps_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.D)
            {
                btnDuplicate_Click(sender!, EventArgs.Empty);
                e.Handled = true;
            }
        }
    }
}
