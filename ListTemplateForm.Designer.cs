namespace STSAnaliza
{
    partial class ListTemplateForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
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
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            btnReload = new Button();
            btnSave = new Button();
            btnHelp = new Button();
            rtbTemplate = new RichTextBox();
            lblPath = new Label();
            SuspendLayout();
            // 
            // btnReload
            // 
            btnReload.Location = new Point(18, 31);
            btnReload.Name = "btnReload";
            btnReload.Size = new Size(97, 63);
            btnReload.TabIndex = 0;
            btnReload.Text = "btnReload";
            btnReload.UseVisualStyleBackColor = true;
            btnReload.Click += btnReload_Click;
            // 
            // btnSave
            // 
            btnSave.Location = new Point(18, 100);
            btnSave.Name = "btnSave";
            btnSave.Size = new Size(97, 60);
            btnSave.TabIndex = 1;
            btnSave.Text = "btnSave";
            btnSave.UseVisualStyleBackColor = true;
            btnSave.Click += btnSave_Click;
            // 
            // btnHelp
            // 
            btnHelp.Location = new Point(18, 166);
            btnHelp.Name = "btnHelp";
            btnHelp.Size = new Size(97, 63);
            btnHelp.TabIndex = 2;
            btnHelp.Text = "btnHelp";
            btnHelp.UseVisualStyleBackColor = true;
            btnHelp.Click += btnHelp_Click;
            // 
            // rtbTemplate
            // 
            rtbTemplate.Font = new Font("Inter", 11.999999F, FontStyle.Regular, GraphicsUnit.Point, 238);
            rtbTemplate.Location = new Point(162, 31);
            rtbTemplate.Name = "rtbTemplate";
            rtbTemplate.Size = new Size(568, 342);
            rtbTemplate.TabIndex = 3;
            rtbTemplate.Text = "";
            // 
            // lblPath
            // 
            lblPath.AutoSize = true;
            lblPath.Location = new Point(35, 9);
            lblPath.Name = "lblPath";
            lblPath.Size = new Size(44, 15);
            lblPath.TabIndex = 4;
            lblPath.Text = "lblPath";
            // 
            // ListTemplateForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(800, 450);
            Controls.Add(lblPath);
            Controls.Add(rtbTemplate);
            Controls.Add(btnHelp);
            Controls.Add(btnSave);
            Controls.Add(btnReload);
            Name = "ListTemplateForm";
            Text = "ListTemplateForm";
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Button btnReload;
        private Button btnSave;
        private Button btnHelp;
        private RichTextBox rtbTemplate;
        private Label lblPath;
    }
}