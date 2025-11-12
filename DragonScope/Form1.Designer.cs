namespace DragonScope
{
    partial class Form1
    {
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.Button btnSaveOutput;

        private System.Windows.Forms.Button btnOpenCsv;
        private System.Windows.Forms.Button btnOpenXml;
        private System.Windows.Forms.Label lblCsvFile;
        private System.Windows.Forms.Label lblXmlFile;
        private System.Windows.Forms.Button btnDeleteLogs;

        private void InitializeComponent()
        {
            btnOpenCsv = new Button();
            btnOpenXml = new Button();
            lblCsvFile = new Label();
            lblXmlFile = new Label();
            progressBar1 = new ProgressBar();
            textBoxOutput = new RichTextBox();
            HootLoad = new Button();
            btnSaveOutput = new Button();
            btnDeleteLogs = new Button();
            SuspendLayout();
            // 
            // btnOpenCsv
            // 
            btnOpenCsv.Location = new Point(11, 49);
            btnOpenCsv.Margin = new Padding(3, 4, 3, 4);
            btnOpenCsv.Name = "btnOpenCsv";
            btnOpenCsv.Size = new Size(86, 31);
            btnOpenCsv.TabIndex = 1;
            btnOpenCsv.Text = "Open CSV";
            btnOpenCsv.UseVisualStyleBackColor = true;
            btnOpenCsv.Click += btnOpenCsv_Click;
            // 
            // btnOpenXml
            // 
            btnOpenXml.Location = new Point(11, 11);
            btnOpenXml.Margin = new Padding(3, 4, 3, 4);
            btnOpenXml.Name = "btnOpenXml";
            btnOpenXml.Size = new Size(86, 31);
            btnOpenXml.TabIndex = 0;
            btnOpenXml.Text = "Open XML";
            btnOpenXml.UseVisualStyleBackColor = true;
            btnOpenXml.Click += btnOpenXml_Click;
            // 
            // lblCsvFile
            // 
            lblCsvFile.AutoSize = true;
            lblCsvFile.Location = new Point(103, 56);
            lblCsvFile.Name = "lblCsvFile";
            lblCsvFile.Size = new Size(0, 20);
            lblCsvFile.TabIndex = 2;
            // 
            // lblXmlFile
            // 
            lblXmlFile.AutoSize = true;
            lblXmlFile.Location = new Point(103, 19);
            lblXmlFile.Name = "lblXmlFile";
            lblXmlFile.Size = new Size(0, 20);
            lblXmlFile.TabIndex = 3;
            // 
            // progressBar1
            // 
            progressBar1.Location = new Point(698, 11);
            progressBar1.Margin = new Padding(3, 4, 3, 4);
            progressBar1.Name = "progressBar1";
            progressBar1.Size = new Size(202, 31);
            progressBar1.TabIndex = 5;
            // 
            // textBoxOutput
            // 
            textBoxOutput.Location = new Point(11, 99);
            textBoxOutput.Name = "textBoxOutput";
            textBoxOutput.Size = new Size(890, 489);
            textBoxOutput.TabIndex = 6;
            textBoxOutput.Text = "";
            // 
            // HootLoad
            // 
            HootLoad.Location = new Point(806, 47);
            HootLoad.Name = "HootLoad";
            HootLoad.Size = new Size(94, 29);
            HootLoad.TabIndex = 7;
            HootLoad.Text = "HootLoad";
            HootLoad.UseVisualStyleBackColor = true;
            HootLoad.Click += HootLoad_Click;
            // 
            // btnSaveOutput
            // 
            btnSaveOutput.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnSaveOutput.AutoSize = true;
            btnSaveOutput.Location = new Point(673, 49);
            btnSaveOutput.Margin = new Padding(3, 4, 3, 4);
            btnSaveOutput.Name = "btnSaveOutput";
            btnSaveOutput.Size = new Size(126, 40);
            btnSaveOutput.TabIndex = 999;
            btnSaveOutput.Text = "Save Output...";
            btnSaveOutput.UseVisualStyleBackColor = true;
            btnSaveOutput.Click += SaveOutputToTextFile_Click;
            // 
            // btnDeleteLogs
            // 
            btnDeleteLogs.Location = new Point(565, 58);
            btnDeleteLogs.Margin = new Padding(3, 4, 3, 4);
            btnDeleteLogs.Name = "btnDeleteLogs";
            btnDeleteLogs.Size = new Size(102, 31);
            btnDeleteLogs.TabIndex = 0;
            btnDeleteLogs.Text = "Clean Logs";
            btnDeleteLogs.UseVisualStyleBackColor = true;
            btnDeleteLogs.Click += btnDeleteLogs_Click;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(912, 600);
            Controls.Add(btnDeleteLogs);
            Controls.Add(HootLoad);
            Controls.Add(btnOpenXml);
            Controls.Add(textBoxOutput);
            Controls.Add(progressBar1);
            Controls.Add(lblXmlFile);
            Controls.Add(lblCsvFile);
            Controls.Add(btnOpenCsv);
            Controls.Add(btnSaveOutput);
            Margin = new Padding(3, 4, 3, 4);
            Name = "Form1";
            Text = "DragonScope";
            ResumeLayout(false);
            PerformLayout();
        }
        private ProgressBar progressBar1;
        private RichTextBox textBoxOutput;
        private Button HootLoad;
    }
}