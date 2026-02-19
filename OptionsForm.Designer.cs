namespace STSAnaliza
{
    partial class OptionsForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            DataGridViewCellStyle dataGridViewCellStyle1 = new DataGridViewCellStyle();
            btnSave = new Button();
            btnReload = new Button();
            lblPath = new Label();
            panel2 = new Panel();
            lblPathBox = new TextBox();
            btnDelete = new Button();
            btnDuplicate = new Button();
            btnUp = new Button();
            btnDown = new Button();
            dgvSteps = new DataGridView();
            txtPrompt = new TextBox();
            panel4 = new Panel();
            panel6 = new Panel();
            panel5 = new Panel();
            panelPromptTools = new Panel();
            chkPromptOnly = new CheckBox();
            panel2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dgvSteps).BeginInit();
            panel4.SuspendLayout();
            panel6.SuspendLayout();
            panel5.SuspendLayout();
            panelPromptTools.SuspendLayout();
            SuspendLayout();
            // 
            // btnSave
            // 
            btnSave.Font = new Font("Segoe UI", 12F, FontStyle.Regular, GraphicsUnit.Point, 238);
            btnSave.Location = new Point(12, 40);
            btnSave.Name = "btnSave";
            btnSave.Size = new Size(110, 43);
            btnSave.TabIndex = 0;
            btnSave.Text = "Zapisz";
            btnSave.UseVisualStyleBackColor = true;
            btnSave.Click += btnSave_Click;
            // 
            // btnReload
            // 
            btnReload.Font = new Font("Segoe UI", 12F, FontStyle.Regular, GraphicsUnit.Point, 238);
            btnReload.Location = new Point(12, 89);
            btnReload.Name = "btnReload";
            btnReload.Size = new Size(110, 40);
            btnReload.TabIndex = 1;
            btnReload.Text = "Odśwież";
            btnReload.UseVisualStyleBackColor = true;
            btnReload.Click += btnReload_Click;
            // 
            // lblPath
            // 
            lblPath.AutoSize = true;
            lblPath.Location = new Point(12, 396);
            lblPath.Name = "lblPath";
            lblPath.Size = new Size(44, 15);
            lblPath.TabIndex = 2;
            lblPath.Text = "lblPath";
            // 
            // panel2
            // 
            panel2.Controls.Add(lblPathBox);
            panel2.Controls.Add(btnDelete);
            panel2.Controls.Add(lblPath);
            panel2.Controls.Add(btnDuplicate);
            panel2.Controls.Add(btnReload);
            panel2.Controls.Add(btnUp);
            panel2.Controls.Add(btnDown);
            panel2.Controls.Add(btnSave);
            panel2.Dock = DockStyle.Left;
            panel2.Location = new Point(0, 0);
            panel2.Name = "panel2";
            panel2.Size = new Size(146, 663);
            panel2.TabIndex = 5;
            // 
            // lblPathBox
            // 
            lblPathBox.Location = new Point(12, 437);
            lblPathBox.Name = "lblPathBox";
            lblPathBox.Size = new Size(100, 23);
            lblPathBox.TabIndex = 7;
            // 
            // btnDelete
            // 
            btnDelete.Location = new Point(12, 304);
            btnDelete.Name = "btnDelete";
            btnDelete.Size = new Size(103, 51);
            btnDelete.TabIndex = 6;
            btnDelete.Text = "Usuń";
            btnDelete.UseVisualStyleBackColor = true;
            // 
            // btnDuplicate
            // 
            btnDuplicate.Font = new Font("Segoe UI", 11.25F);
            btnDuplicate.Location = new Point(12, 252);
            btnDuplicate.Name = "btnDuplicate";
            btnDuplicate.Size = new Size(103, 46);
            btnDuplicate.TabIndex = 5;
            btnDuplicate.Text = "Duplikuj";
            btnDuplicate.UseVisualStyleBackColor = true;
            btnDuplicate.Click += btnDuplicate_Click;
            // 
            // btnUp
            // 
            btnUp.Font = new Font("Segoe UI", 11.25F);
            btnUp.Location = new Point(12, 148);
            btnUp.Name = "btnUp";
            btnUp.Size = new Size(103, 46);
            btnUp.TabIndex = 3;
            btnUp.Text = "Do góry ";
            btnUp.UseVisualStyleBackColor = true;
            btnUp.Click += btnUp_Click;
            // 
            // btnDown
            // 
            btnDown.Font = new Font("Segoe UI", 11.25F);
            btnDown.Location = new Point(12, 200);
            btnDown.Name = "btnDown";
            btnDown.Size = new Size(103, 46);
            btnDown.TabIndex = 4;
            btnDown.Text = "W dół";
            btnDown.UseVisualStyleBackColor = true;
            btnDown.Click += btnDown_Click;
            // 
            // dgvSteps
            // 
            dataGridViewCellStyle1.Alignment = DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle1.BackColor = SystemColors.Control;
            dataGridViewCellStyle1.Font = new Font("Inter", 11.249999F, FontStyle.Regular, GraphicsUnit.Point, 238);
            dataGridViewCellStyle1.ForeColor = SystemColors.WindowText;
            dataGridViewCellStyle1.SelectionBackColor = SystemColors.Highlight;
            dataGridViewCellStyle1.SelectionForeColor = SystemColors.HighlightText;
            dataGridViewCellStyle1.WrapMode = DataGridViewTriState.True;
            dgvSteps.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle1;
            dgvSteps.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dgvSteps.Dock = DockStyle.Fill;
            dgvSteps.Location = new Point(0, 0);
            dgvSteps.Name = "dgvSteps";
            dgvSteps.Size = new Size(1261, 396);
            dgvSteps.TabIndex = 3;
            // 
            // txtPrompt
            // 
            txtPrompt.Dock = DockStyle.Fill;
            txtPrompt.Font = new Font("Inter", 11.999999F, FontStyle.Regular, GraphicsUnit.Point, 238);
            txtPrompt.Location = new Point(0, 28);
            txtPrompt.Multiline = true;
            txtPrompt.Name = "txtPrompt";
            txtPrompt.ScrollBars = ScrollBars.Vertical;
            txtPrompt.Size = new Size(1261, 239);
            txtPrompt.TabIndex = 4;
            // 
            // panel4
            // 
            panel4.Controls.Add(panel6);
            panel4.Controls.Add(panel5);
            panel4.Dock = DockStyle.Fill;
            panel4.Location = new Point(146, 0);
            panel4.Name = "panel4";
            panel4.Size = new Size(1261, 663);
            panel4.TabIndex = 7;
            // 
            // panel6
            // 
            panel6.Controls.Add(dgvSteps);
            panel6.Dock = DockStyle.Fill;
            panel6.Location = new Point(0, 0);
            panel6.Name = "panel6";
            panel6.Size = new Size(1261, 396);
            panel6.TabIndex = 7;
            // 
            // panel5
            // 
            panel5.Controls.Add(txtPrompt);
            panel5.Controls.Add(panelPromptTools);
            panel5.Dock = DockStyle.Bottom;
            panel5.Location = new Point(0, 396);
            panel5.Name = "panel5";
            panel5.Size = new Size(1261, 267);
            panel5.TabIndex = 6;
            // 
            // panelPromptTools
            // 
            panelPromptTools.Controls.Add(chkPromptOnly);
            panelPromptTools.Dock = DockStyle.Top;
            panelPromptTools.Location = new Point(0, 0);
            panelPromptTools.Name = "panelPromptTools";
            panelPromptTools.Size = new Size(1261, 28);
            panelPromptTools.TabIndex = 5;
            // 
            // chkPromptOnly
            // 
            chkPromptOnly.AutoSize = true;
            chkPromptOnly.Location = new Point(8, 5);
            chkPromptOnly.Name = "chkPromptOnly";
            chkPromptOnly.Size = new Size(191, 19);
            chkPromptOnly.TabIndex = 0;
            chkPromptOnly.Text = "Tylko prompt (bez dokumentu)";
            chkPromptOnly.UseVisualStyleBackColor = true;
            chkPromptOnly.CheckedChanged += chkPromptOnly_CheckedChanged;
            // 
            // OptionsForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1407, 663);
            Controls.Add(panel4);
            Controls.Add(panel2);
            Name = "OptionsForm";
            Text = "OptionsForm";
            panel2.ResumeLayout(false);
            panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)dgvSteps).EndInit();
            panel4.ResumeLayout(false);
            panel6.ResumeLayout(false);
            panel5.ResumeLayout(false);
            panel5.PerformLayout();
            panelPromptTools.ResumeLayout(false);
            panelPromptTools.PerformLayout();
            ResumeLayout(false);
        }

        #endregion

        private Button btnSave;
        private Button btnReload;
        private Label lblPath;
        private Panel panel2;
        private Button btnDown;
        private Button btnUp;
        private Button btnDuplicate;
        private TextBox txtPrompt;
        private DataGridView dgvSteps;
        private Panel panel4;
        private Panel panel5;
        private Panel panel6;
        private Button btnDelete;

        private Panel panelPromptTools;
        private CheckBox chkPromptOnly;
        private TextBox lblPathBox;
    }
}