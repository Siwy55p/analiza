using System.ComponentModel;

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

            var list = _binding.Where(s => s != null).OrderBy(s => s.Order).ToList();

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Order <= 0) list[i].Order = i + 1;
                if (string.IsNullOrWhiteSpace(list[i].Title)) list[i].Title = $"Krok {list[i].Order}";

            }

            await _store.SaveAsync(list);
            MessageBox.Show("Zapisano kroki.", "OK", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private int? GetSelectedIndex()
        {
            if (dgvSteps.CurrentRow == null) return null;
            if (dgvSteps.CurrentRow.Index < 0) return null;
            if (dgvSteps.CurrentRow.Index >= _binding.Count) return null;
            return dgvSteps.CurrentRow.Index;
        }

        private void SelectRow(int index)
        {
            if (index < 0 || index >= dgvSteps.Rows.Count) return;

            dgvSteps.ClearSelection();
            dgvSteps.Rows[index].Selected = true;

            if (dgvSteps.Columns.Count > 0)
                dgvSteps.CurrentCell = dgvSteps.Rows[index].Cells[0];
        }

        private void ReorderOrders()
        {
            for (int i = 0; i < _binding.Count; i++)
                _binding[i].Order = i + 1;

            dgvSteps.Refresh();
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
            if (dgvSteps.Columns["TimeoutSeconds"] != null) dgvSteps.Columns["TimeoutSeconds"].Width = 90;
            if (dgvSteps.Columns["RequiresMarkets"] != null) dgvSteps.Columns["RequiresMarkets"].Width = 120;

            if (dgvSteps.Columns["KursBuch"] != null) dgvSteps.Columns["KursBuch"].Width = 95;
            if (dgvSteps.Columns["TargetSectionNumber"] != null) dgvSteps.Columns["TargetSectionNumber"].Width = 120;

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

        private void nudTargetSection_ValueChanged(object? sender, EventArgs e)
        {
            if (_syncingPrompt) return;

            var idx = dgvSteps.CurrentRow?.Index ?? -1;
            if (idx < 0 || idx >= _binding.Count) return;

            if (!_binding[idx].KursBuch)
                return;


            dgvSteps.Refresh();
        }

        private void RefreshPromptOnlyUiState()
        {
            var enabled = chkPromptOnly.Checked;
        }

        private void btnDelete_Click(object sender, EventArgs e)
        {
            dgvSteps.EndEdit();

            var idx = GetSelectedIndex();
            if (idx == null)
            {
                MessageBox.Show("Zaznacz krok do usunięcia.", "Brak zaznaczenia",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var step = _binding[idx.Value];
            var title = string.IsNullOrWhiteSpace(step.Title) ? $"Krok {step.Order}" : step.Title;

            var confirm = MessageBox.Show(
                $"Usunąć krok?\n\n{title}",
                "Potwierdź usunięcie",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning
            );

            if (confirm != DialogResult.Yes)
                return;

            int removeIndex = idx.Value;
            _binding.RemoveAt(removeIndex);

            ReorderOrders();
            SelectRowSafe(removeIndex);
        }

        private void SelectRowSafe(int index)
        {
            if (dgvSteps.Rows.Count == 0) return;

            if (index < 0) index = 0;
            if (index >= dgvSteps.Rows.Count) index = dgvSteps.Rows.Count - 1;

            dgvSteps.ClearSelection();
            dgvSteps.Rows[index].Selected = true;

            if (dgvSteps.Columns.Count > 0)
                dgvSteps.CurrentCell = dgvSteps.Rows[index].Cells[0];
        }

        private void dgvSteps_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Delete)
            {
                btnDelete_Click(sender!, EventArgs.Empty);
                e.Handled = true;
            }
        }
    }
}
