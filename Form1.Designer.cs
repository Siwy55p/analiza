namespace STSAnaliza
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            panel1 = new Panel();
            tlpLeft = new TableLayoutPanel();
            btnDownloadList = new Button();
            tnListAnalyze = new Button();
            btnCancelAnalyze = new Button();
            button3 = new Button();
            button4 = new Button();
            grpOmijaj = new GroupBox();
            rtbOmijaj = new RichTextBox();
            panel2 = new Panel();
            txtListOutput = new RichTextBox();
            panel3 = new Panel();
            tableLayoutPanel1 = new TableLayoutPanel();
            dataGridMatchList = new DataGridView();
            panel4 = new Panel();
            textBoxAnswer = new TextBox();
            btnWyslij = new Button();
            rtbdoc = new RichTextBox();
            tabControl1 = new TabControl();
            tabPage1 = new TabPage();
            tabPage2 = new TabPage();
            tlpTab2 = new TableLayoutPanel();
            pnlTab2Top = new Panel();
            respId = new TextBox();
            btnPobierzLog = new Button();
            tlpTab2Body = new TableLayoutPanel();
            WynikLog = new RichTextBox();
            textOutLog = new RichTextBox();
            rtbPrice = new RichTextBox();
            tabPage3 = new TabPage();
            splitTab3 = new SplitContainer();
            tlpTab3Left = new TableLayoutPanel();
            lblCompetitorIdA = new Label();
            txtCompetitorIdA = new TextBox();
            btnLoadLastMatchesA = new Button();
            btnLoadLastMatchesBoth = new Button();
            txtOutput = new RichTextBox();
            panel1.SuspendLayout();
            tlpLeft.SuspendLayout();
            grpOmijaj.SuspendLayout();
            panel2.SuspendLayout();
            panel3.SuspendLayout();
            tableLayoutPanel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dataGridMatchList).BeginInit();
            panel4.SuspendLayout();
            tabControl1.SuspendLayout();
            tabPage1.SuspendLayout();
            tabPage2.SuspendLayout();
            tlpTab2.SuspendLayout();
            pnlTab2Top.SuspendLayout();
            tlpTab2Body.SuspendLayout();
            tabPage3.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)splitTab3).BeginInit();
            splitTab3.Panel1.SuspendLayout();
            splitTab3.Panel2.SuspendLayout();
            splitTab3.SuspendLayout();
            tlpTab3Left.SuspendLayout();
            SuspendLayout();
            // 
            // panel1
            // 
            panel1.Controls.Add(tlpLeft);
            panel1.Dock = DockStyle.Left;
            panel1.Location = new Point(8, 8);
            panel1.Name = "panel1";
            panel1.Padding = new Padding(10);
            panel1.Size = new Size(190, 748);
            panel1.TabIndex = 2;
            // 
            // tlpLeft
            // 
            tlpLeft.ColumnCount = 1;
            tlpLeft.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tlpLeft.Controls.Add(btnDownloadList, 0, 0);
            tlpLeft.Controls.Add(tnListAnalyze, 0, 1);
            tlpLeft.Controls.Add(btnCancelAnalyze, 0, 2);
            tlpLeft.Controls.Add(button3, 0, 3);
            tlpLeft.Controls.Add(button4, 0, 4);
            tlpLeft.Controls.Add(grpOmijaj, 0, 5);
            tlpLeft.Dock = DockStyle.Fill;
            tlpLeft.Location = new Point(10, 10);
            tlpLeft.Name = "tlpLeft";
            tlpLeft.RowCount = 6;
            tlpLeft.RowStyles.Add(new RowStyle(SizeType.Absolute, 56F));
            tlpLeft.RowStyles.Add(new RowStyle(SizeType.Absolute, 56F));
            tlpLeft.RowStyles.Add(new RowStyle(SizeType.Absolute, 56F));
            tlpLeft.RowStyles.Add(new RowStyle(SizeType.Absolute, 56F));
            tlpLeft.RowStyles.Add(new RowStyle(SizeType.Absolute, 56F));
            tlpLeft.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            tlpLeft.Size = new Size(170, 728);
            tlpLeft.TabIndex = 0;
            // 
            // btnDownloadList
            // 
            btnDownloadList.Dock = DockStyle.Fill;
            btnDownloadList.Font = new Font("Inter", 12F);
            btnDownloadList.Location = new Point(3, 3);
            btnDownloadList.Name = "btnDownloadList";
            btnDownloadList.Size = new Size(164, 50);
            btnDownloadList.TabIndex = 0;
            btnDownloadList.Text = "Pobierz listę";
            btnDownloadList.Click += btnDownloadList_Click;
            // 
            // tnListAnalyze
            // 
            tnListAnalyze.Dock = DockStyle.Fill;
            tnListAnalyze.Font = new Font("Inter", 12F);
            tnListAnalyze.Location = new Point(3, 59);
            tnListAnalyze.Name = "tnListAnalyze";
            tnListAnalyze.Size = new Size(164, 50);
            tnListAnalyze.TabIndex = 1;
            tnListAnalyze.Text = "Analizuj";
            tnListAnalyze.Click += tnListAnalyze_Click;
            // 
            // btnCancelAnalyze
            // 
            btnCancelAnalyze.Dock = DockStyle.Fill;
            btnCancelAnalyze.Font = new Font("Inter", 12F);
            btnCancelAnalyze.Location = new Point(3, 115);
            btnCancelAnalyze.Name = "btnCancelAnalyze";
            btnCancelAnalyze.Size = new Size(164, 50);
            btnCancelAnalyze.TabIndex = 2;
            btnCancelAnalyze.Text = "Anuluj";
            btnCancelAnalyze.Click += btnCancelAnalyze_Click;
            // 
            // button3
            // 
            button3.Dock = DockStyle.Fill;
            button3.Font = new Font("Inter", 12F);
            button3.Location = new Point(3, 171);
            button3.Name = "button3";
            button3.Size = new Size(164, 50);
            button3.TabIndex = 3;
            button3.Text = "Komendy";
            button3.Click += button3_Click;
            // 
            // button4
            // 
            button4.Dock = DockStyle.Fill;
            button4.Font = new Font("Inter", 12F);
            button4.Location = new Point(3, 227);
            button4.Name = "button4";
            button4.Size = new Size(164, 50);
            button4.TabIndex = 4;
            button4.Text = "Szablon";
            button4.Click += button4_Click;
            // 
            // grpOmijaj
            // 
            grpOmijaj.Controls.Add(rtbOmijaj);
            grpOmijaj.Dock = DockStyle.Fill;
            grpOmijaj.Location = new Point(3, 283);
            grpOmijaj.Name = "grpOmijaj";
            grpOmijaj.Padding = new Padding(8);
            grpOmijaj.Size = new Size(164, 442);
            grpOmijaj.TabIndex = 5;
            grpOmijaj.TabStop = false;
            grpOmijaj.Text = "Omijaj (frazy)";
            // 
            // rtbOmijaj
            // 
            rtbOmijaj.BorderStyle = BorderStyle.FixedSingle;
            rtbOmijaj.Dock = DockStyle.Fill;
            rtbOmijaj.Location = new Point(8, 24);
            rtbOmijaj.Name = "rtbOmijaj";
            rtbOmijaj.Size = new Size(148, 410);
            rtbOmijaj.TabIndex = 0;
            rtbOmijaj.Text = "";
            // 
            // panel2
            // 
            panel2.Controls.Add(txtListOutput);
            panel2.Dock = DockStyle.Right;
            panel2.Location = new Point(1438, 8);
            panel2.Name = "panel2";
            panel2.Padding = new Padding(10);
            panel2.Size = new Size(260, 748);
            panel2.TabIndex = 1;
            // 
            // txtListOutput
            // 
            txtListOutput.BorderStyle = BorderStyle.FixedSingle;
            txtListOutput.Dock = DockStyle.Fill;
            txtListOutput.Location = new Point(10, 10);
            txtListOutput.Name = "txtListOutput";
            txtListOutput.Size = new Size(240, 728);
            txtListOutput.TabIndex = 0;
            txtListOutput.Text = "";
            // 
            // panel3
            // 
            panel3.Controls.Add(tableLayoutPanel1);
            panel3.Dock = DockStyle.Fill;
            panel3.Location = new Point(198, 8);
            panel3.Name = "panel3";
            panel3.Padding = new Padding(10);
            panel3.Size = new Size(1240, 748);
            panel3.TabIndex = 0;
            // 
            // tableLayoutPanel1
            // 
            tableLayoutPanel1.ColumnCount = 1;
            tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tableLayoutPanel1.Controls.Add(dataGridMatchList, 0, 0);
            tableLayoutPanel1.Controls.Add(panel4, 0, 1);
            tableLayoutPanel1.Controls.Add(rtbdoc, 0, 2);
            tableLayoutPanel1.Dock = DockStyle.Fill;
            tableLayoutPanel1.Location = new Point(10, 10);
            tableLayoutPanel1.Name = "tableLayoutPanel1";
            tableLayoutPanel1.Padding = new Padding(5);
            tableLayoutPanel1.RowCount = 3;
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 35F));
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 65F));
            tableLayoutPanel1.Size = new Size(1220, 728);
            tableLayoutPanel1.TabIndex = 0;
            // 
            // dataGridMatchList
            // 
            dataGridMatchList.AllowUserToAddRows = false;
            dataGridMatchList.AllowUserToDeleteRows = false;
            dataGridMatchList.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dataGridMatchList.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridMatchList.Dock = DockStyle.Fill;
            dataGridMatchList.Location = new Point(8, 8);
            dataGridMatchList.MultiSelect = false;
            dataGridMatchList.Name = "dataGridMatchList";
            dataGridMatchList.RowHeadersVisible = false;
            dataGridMatchList.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dataGridMatchList.Size = new Size(1204, 229);
            dataGridMatchList.TabIndex = 0;
            dataGridMatchList.CellDoubleClick += dataGridMatchList_CellDoubleClick;
            // 
            // panel4
            // 
            panel4.Controls.Add(textBoxAnswer);
            panel4.Controls.Add(btnWyslij);
            panel4.Dock = DockStyle.Fill;
            panel4.Location = new Point(8, 243);
            panel4.Name = "panel4";
            panel4.Size = new Size(1204, 38);
            panel4.TabIndex = 1;
            // 
            // textBoxAnswer
            // 
            textBoxAnswer.Dock = DockStyle.Fill;
            textBoxAnswer.Location = new Point(0, 0);
            textBoxAnswer.Margin = new Padding(0, 8, 8, 8);
            textBoxAnswer.Name = "textBoxAnswer";
            textBoxAnswer.Size = new Size(1034, 23);
            textBoxAnswer.TabIndex = 0;
            // 
            // btnWyslij
            // 
            btnWyslij.Dock = DockStyle.Right;
            btnWyslij.Location = new Point(1034, 0);
            btnWyslij.Name = "btnWyslij";
            btnWyslij.Size = new Size(170, 38);
            btnWyslij.TabIndex = 1;
            btnWyslij.Text = "Wyślij odpowiedź";
            btnWyslij.Click += btnWyslij_Click;
            // 
            // rtbdoc
            // 
            rtbdoc.BorderStyle = BorderStyle.FixedSingle;
            rtbdoc.Dock = DockStyle.Fill;
            rtbdoc.Font = new Font("Inter", 12F, FontStyle.Regular, GraphicsUnit.Point, 238);
            rtbdoc.Location = new Point(8, 287);
            rtbdoc.Name = "rtbdoc";
            rtbdoc.Size = new Size(1204, 433);
            rtbdoc.TabIndex = 2;
            rtbdoc.Text = "";
            // 
            // tabControl1
            // 
            tabControl1.Controls.Add(tabPage1);
            tabControl1.Controls.Add(tabPage2);
            tabControl1.Controls.Add(tabPage3);
            tabControl1.Dock = DockStyle.Fill;
            tabControl1.Location = new Point(0, 0);
            tabControl1.Name = "tabControl1";
            tabControl1.SelectedIndex = 0;
            tabControl1.Size = new Size(1714, 792);
            tabControl1.TabIndex = 0;
            // 
            // tabPage1
            // 
            tabPage1.Controls.Add(panel3);
            tabPage1.Controls.Add(panel2);
            tabPage1.Controls.Add(panel1);
            tabPage1.Location = new Point(4, 24);
            tabPage1.Name = "tabPage1";
            tabPage1.Padding = new Padding(8);
            tabPage1.Size = new Size(1706, 764);
            tabPage1.TabIndex = 0;
            tabPage1.Text = "Analiza";
            // 
            // tabPage2
            // 
            tabPage2.Controls.Add(tlpTab2);
            tabPage2.Location = new Point(4, 24);
            tabPage2.Name = "tabPage2";
            tabPage2.Padding = new Padding(8);
            tabPage2.Size = new Size(1706, 764);
            tabPage2.TabIndex = 1;
            tabPage2.Text = "Logi";
            // 
            // tlpTab2
            // 
            tlpTab2.ColumnCount = 1;
            tlpTab2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tlpTab2.Controls.Add(pnlTab2Top, 0, 0);
            tlpTab2.Controls.Add(tlpTab2Body, 0, 1);
            tlpTab2.Dock = DockStyle.Fill;
            tlpTab2.Location = new Point(8, 8);
            tlpTab2.Name = "tlpTab2";
            tlpTab2.Padding = new Padding(10);
            tlpTab2.RowCount = 2;
            tlpTab2.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
            tlpTab2.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            tlpTab2.Size = new Size(1690, 748);
            tlpTab2.TabIndex = 0;
            // 
            // pnlTab2Top
            // 
            pnlTab2Top.Controls.Add(respId);
            pnlTab2Top.Controls.Add(btnPobierzLog);
            pnlTab2Top.Dock = DockStyle.Fill;
            pnlTab2Top.Location = new Point(13, 13);
            pnlTab2Top.Name = "pnlTab2Top";
            pnlTab2Top.Size = new Size(1664, 38);
            pnlTab2Top.TabIndex = 0;
            // 
            // respId
            // 
            respId.Dock = DockStyle.Fill;
            respId.Location = new Point(140, 0);
            respId.Margin = new Padding(10, 8, 0, 8);
            respId.Name = "respId";
            respId.Size = new Size(1524, 23);
            respId.TabIndex = 0;
            // 
            // btnPobierzLog
            // 
            btnPobierzLog.Dock = DockStyle.Left;
            btnPobierzLog.Location = new Point(0, 0);
            btnPobierzLog.Name = "btnPobierzLog";
            btnPobierzLog.Size = new Size(140, 38);
            btnPobierzLog.TabIndex = 1;
            btnPobierzLog.Text = "Pobierz log";
            btnPobierzLog.Click += btnPobierzLog_Click;
            // 
            // tlpTab2Body
            // 
            tlpTab2Body.ColumnCount = 3;
            tlpTab2Body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
            tlpTab2Body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
            tlpTab2Body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34F));
            tlpTab2Body.Controls.Add(WynikLog, 0, 0);
            tlpTab2Body.Controls.Add(textOutLog, 1, 0);
            tlpTab2Body.Controls.Add(rtbPrice, 2, 0);
            tlpTab2Body.Dock = DockStyle.Fill;
            tlpTab2Body.Location = new Point(13, 57);
            tlpTab2Body.Name = "tlpTab2Body";
            tlpTab2Body.RowCount = 1;
            tlpTab2Body.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            tlpTab2Body.Size = new Size(1664, 678);
            tlpTab2Body.TabIndex = 1;
            // 
            // WynikLog
            // 
            WynikLog.BorderStyle = BorderStyle.FixedSingle;
            WynikLog.Dock = DockStyle.Fill;
            WynikLog.Location = new Point(3, 3);
            WynikLog.Name = "WynikLog";
            WynikLog.Size = new Size(548, 672);
            WynikLog.TabIndex = 0;
            WynikLog.Text = "";
            // 
            // textOutLog
            // 
            textOutLog.BorderStyle = BorderStyle.FixedSingle;
            textOutLog.Dock = DockStyle.Fill;
            textOutLog.Location = new Point(557, 3);
            textOutLog.Name = "textOutLog";
            textOutLog.Size = new Size(548, 672);
            textOutLog.TabIndex = 1;
            textOutLog.Text = "";
            // 
            // rtbPrice
            // 
            rtbPrice.BorderStyle = BorderStyle.FixedSingle;
            rtbPrice.Dock = DockStyle.Fill;
            rtbPrice.Location = new Point(1111, 3);
            rtbPrice.Name = "rtbPrice";
            rtbPrice.Size = new Size(550, 672);
            rtbPrice.TabIndex = 2;
            rtbPrice.Text = "";
            // 
            // tabPage3
            // 
            tabPage3.Controls.Add(splitTab3);
            tabPage3.Location = new Point(4, 24);
            tabPage3.Name = "tabPage3";
            tabPage3.Padding = new Padding(8);
            tabPage3.Size = new Size(1706, 764);
            tabPage3.TabIndex = 2;
            tabPage3.Text = "Mecze";
            // 
            // splitTab3
            // 
            splitTab3.Dock = DockStyle.Fill;
            splitTab3.Location = new Point(8, 8);
            splitTab3.Name = "splitTab3";
            // 
            // splitTab3.Panel1
            // 
            splitTab3.Panel1.Controls.Add(tlpTab3Left);
            splitTab3.Panel1.Padding = new Padding(10);
            splitTab3.Panel1MinSize = 260;
            // 
            // splitTab3.Panel2
            // 
            splitTab3.Panel2.Controls.Add(txtOutput);
            splitTab3.Panel2.Padding = new Padding(10);
            splitTab3.Panel2MinSize = 400;
            splitTab3.Size = new Size(1690, 748);
            splitTab3.SplitterDistance = 1286;
            splitTab3.TabIndex = 0;
            // 
            // tlpTab3Left
            // 
            tlpTab3Left.ColumnCount = 1;
            tlpTab3Left.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tlpTab3Left.Controls.Add(lblCompetitorIdA, 0, 0);
            tlpTab3Left.Controls.Add(txtCompetitorIdA, 0, 1);
            tlpTab3Left.Controls.Add(btnLoadLastMatchesA, 0, 2);
            tlpTab3Left.Controls.Add(btnLoadLastMatchesBoth, 0, 3);
            tlpTab3Left.Dock = DockStyle.Fill;
            tlpTab3Left.Location = new Point(10, 10);
            tlpTab3Left.Name = "tlpTab3Left";
            tlpTab3Left.RowCount = 6;
            tlpTab3Left.RowStyles.Add(new RowStyle(SizeType.Absolute, 20F));
            tlpTab3Left.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
            tlpTab3Left.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
            tlpTab3Left.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
            tlpTab3Left.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            tlpTab3Left.RowStyles.Add(new RowStyle(SizeType.Absolute, 10F));
            tlpTab3Left.Size = new Size(1266, 728);
            tlpTab3Left.TabIndex = 0;
            // 
            // lblCompetitorIdA
            // 
            lblCompetitorIdA.Dock = DockStyle.Fill;
            lblCompetitorIdA.Location = new Point(3, 0);
            lblCompetitorIdA.Name = "lblCompetitorIdA";
            lblCompetitorIdA.Size = new Size(1260, 20);
            lblCompetitorIdA.TabIndex = 0;
            lblCompetitorIdA.Text = "Competitor ID (A):";
            // 
            // txtCompetitorIdA
            // 
            txtCompetitorIdA.Dock = DockStyle.Fill;
            txtCompetitorIdA.Location = new Point(3, 23);
            txtCompetitorIdA.Name = "txtCompetitorIdA";
            txtCompetitorIdA.Size = new Size(1260, 23);
            txtCompetitorIdA.TabIndex = 1;
            // 
            // btnLoadLastMatchesA
            // 
            btnLoadLastMatchesA.Dock = DockStyle.Fill;
            btnLoadLastMatchesA.Location = new Point(3, 67);
            btnLoadLastMatchesA.Name = "btnLoadLastMatchesA";
            btnLoadLastMatchesA.Size = new Size(1260, 38);
            btnLoadLastMatchesA.TabIndex = 2;
            btnLoadLastMatchesA.Text = "Pobierz A";
            btnLoadLastMatchesA.Click += btnLoadLastMatchesA_Click;
            // 
            // btnLoadLastMatchesBoth
            // 
            btnLoadLastMatchesBoth.Dock = DockStyle.Fill;
            btnLoadLastMatchesBoth.Location = new Point(3, 111);
            btnLoadLastMatchesBoth.Name = "btnLoadLastMatchesBoth";
            btnLoadLastMatchesBoth.Size = new Size(1260, 38);
            btnLoadLastMatchesBoth.TabIndex = 3;
            btnLoadLastMatchesBoth.Text = "Pobierz mecze";
            btnLoadLastMatchesBoth.Click += btnLoadLastMatchesBoth_Click;
            // 
            // txtOutput
            // 
            txtOutput.BorderStyle = BorderStyle.FixedSingle;
            txtOutput.Dock = DockStyle.Fill;
            txtOutput.Location = new Point(10, 10);
            txtOutput.Name = "txtOutput";
            txtOutput.Size = new Size(380, 728);
            txtOutput.TabIndex = 0;
            txtOutput.Text = "";
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1714, 792);
            Controls.Add(tabControl1);
            Name = "Form1";
            Text = "Form1";
            panel1.ResumeLayout(false);
            tlpLeft.ResumeLayout(false);
            grpOmijaj.ResumeLayout(false);
            panel2.ResumeLayout(false);
            panel3.ResumeLayout(false);
            tableLayoutPanel1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)dataGridMatchList).EndInit();
            panel4.ResumeLayout(false);
            panel4.PerformLayout();
            tabControl1.ResumeLayout(false);
            tabPage1.ResumeLayout(false);
            tabPage2.ResumeLayout(false);
            tlpTab2.ResumeLayout(false);
            pnlTab2Top.ResumeLayout(false);
            pnlTab2Top.PerformLayout();
            tlpTab2Body.ResumeLayout(false);
            tabPage3.ResumeLayout(false);
            splitTab3.Panel1.ResumeLayout(false);
            splitTab3.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)splitTab3).EndInit();
            splitTab3.ResumeLayout(false);
            tlpTab3Left.ResumeLayout(false);
            tlpTab3Left.PerformLayout();
            ResumeLayout(false);
        }


        #endregion
        private TableLayoutPanel tlpLeft;
        private GroupBox grpOmijaj;

        private TableLayoutPanel tlpTab2;
        private Panel pnlTab2Top;
        private TableLayoutPanel tlpTab2Body;

        private SplitContainer splitTab3;
        private TableLayoutPanel tlpTab3Left;
        private Label lblCompetitorIdA;
        private Panel panel1;
        private Panel panel2;
        private Panel panel3;
        private Button button4;
        private Button button3;
        private Button tnListAnalyze;
        private Button btnDownloadList;
        private DataGridView dataGridMatchList;
        private RichTextBox rtbdoc;
        private RichTextBox txtListOutput;
        private Button btnCancelAnalyze;
        private Button btnWyslij;
        private TextBox textBoxAnswer;
        private TableLayoutPanel tableLayoutPanel1;
        private Panel panel4;
        private TabControl tabControl1;
        private TabPage tabPage1;
        private TabPage tabPage2;
        private Button btnPobierzLog;
        private TextBox respId;
        private RichTextBox WynikLog;
        private RichTextBox textOutLog;
        private RichTextBox rtbOmijaj;
        private RichTextBox rtbPrice;
        private TabPage tabPage3;
        private RichTextBox txtOutput;
        private Button btnLoadLastMatchesA;
        private TextBox txtCompetitorIdA;
        private Button btnLoadLastMatchesBoth;
    }
}
