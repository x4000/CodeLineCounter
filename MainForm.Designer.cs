namespace CodeLineCounter
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose( bool disposing )
        {
            if ( disposing && (components != null) )
            {
                components.Dispose();
            }
            base.Dispose( disposing );
        }

        #region Windows Form Designer generated code
        private void InitializeComponent()
        {
            this.btnCountLines = new System.Windows.Forms.Button();
            this.txtFolder = new System.Windows.Forms.TextBox();
            this.lblFolder = new System.Windows.Forms.Label();
            this.txtOutput = new System.Windows.Forms.TextBox();
            this.btnRecentFolders = new System.Windows.Forms.Button();
            this.SuspendLayout();
            //
            // btnRecentFolders
            //
            this.btnRecentFolders.Location = new System.Drawing.Point(158, 12);
            this.btnRecentFolders.Name = "btnRecentFolders";
            this.btnRecentFolders.Size = new System.Drawing.Size(110, 36);
            this.btnRecentFolders.TabIndex = 1;
            this.btnRecentFolders.Text = "Recent Folders ▼";
            this.btnRecentFolders.UseVisualStyleBackColor = true;
            this.btnRecentFolders.Click += new System.EventHandler(this.btnRecentFolders_Click);
            //
            // btnCountLines
            //
            this.btnCountLines.Location = new System.Drawing.Point(12, 12);
            this.btnCountLines.Name = "btnCountLines";
            this.btnCountLines.Size = new System.Drawing.Size(140, 36);
            this.btnCountLines.TabIndex = 0;
            this.btnCountLines.Text = "Count Lines";
            this.btnCountLines.UseVisualStyleBackColor = true;
            this.btnCountLines.Click += new System.EventHandler(this.btnCountLines_Click);
            //
            // txtFolder
            //
            this.txtFolder.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtFolder.Location = new System.Drawing.Point(57, 69);
            this.txtFolder.Name = "txtFolder";
            this.txtFolder.Size = new System.Drawing.Size(1055, 20);
            this.txtFolder.TabIndex = 7;
            //
            // lblFolder
            //
            this.lblFolder.AutoSize = true;
            this.lblFolder.Location = new System.Drawing.Point(12, 72);
            this.lblFolder.Name = "lblFolder";
            this.lblFolder.Size = new System.Drawing.Size(39, 13);
            this.lblFolder.TabIndex = 8;
            this.lblFolder.Text = "Folder:";
            //
            // txtOutput
            //
            this.txtOutput.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtOutput.Font = new System.Drawing.Font("Consolas", 9F);
            this.txtOutput.Location = new System.Drawing.Point(12, 95);
            this.txtOutput.MaxLength = 3276700;
            this.txtOutput.Multiline = true;
            this.txtOutput.Name = "txtOutput";
            this.txtOutput.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.txtOutput.Size = new System.Drawing.Size(1100, 422);
            this.txtOutput.TabIndex = 9;
            //
            // MainForm
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1128, 529);
            this.Controls.Add(this.txtOutput);
            this.Controls.Add(this.lblFolder);
            this.Controls.Add(this.txtFolder);
            this.Controls.Add(this.btnRecentFolders);
            this.Controls.Add(this.btnCountLines);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.Sizable;
            this.MinimumSize = new System.Drawing.Size(580, 580);
            this.Name = "MainForm";
            this.Text = "Code Line Counter";
            this.ResumeLayout(false);
            this.PerformLayout();

        }
        #endregion

        private System.Windows.Forms.Button btnCountLines;
        private System.Windows.Forms.TextBox txtFolder;
        private System.Windows.Forms.Label lblFolder;
        private System.Windows.Forms.TextBox txtOutput;
        private System.Windows.Forms.Button btnRecentFolders;
    }
}
