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
            btnOpenCsv.Location = new Point(10, 37);
            btnOpenCsv.Name = "btnOpenCsv";
            btnOpenCsv.Size = new Size(75, 23);
            btnOpenCsv.TabIndex = 1;
            btnOpenCsv.Text = "Open CSV";
            btnOpenCsv.UseVisualStyleBackColor = true;
            btnOpenCsv.Click += btnOpenCsv_Click;
            // 
            // btnOpenXml
            // 
            btnOpenXml.Location = new Point(10, 8);
            btnOpenXml.Name = "btnOpenXml";
            btnOpenXml.Size = new Size(75, 23);
            btnOpenXml.TabIndex = 0;
            btnOpenXml.Text = "Open XML";
            btnOpenXml.UseVisualStyleBackColor = true;
            btnOpenXml.Click += btnOpenXml_Click;
            // 
            // lblCsvFile
            // 
            lblCsvFile.AutoSize = true;
            lblCsvFile.Location = new Point(90, 42);
            lblCsvFile.Name = "lblCsvFile";
            lblCsvFile.Size = new Size(0, 15);
            lblCsvFile.TabIndex = 2;
            // 
            // lblXmlFile
            // 
            lblXmlFile.AutoSize = true;
            lblXmlFile.Location = new Point(90, 14);
            lblXmlFile.Name = "lblXmlFile";
            lblXmlFile.Size = new Size(0, 15);
            lblXmlFile.TabIndex = 3;
            // 
            // progressBar1
            // 
            progressBar1.Location = new Point(611, 8);
            progressBar1.Name = "progressBar1";
            progressBar1.Size = new Size(177, 23);
            progressBar1.TabIndex = 5;
            // 
            // textBoxOutput
            // 
            textBoxOutput.Location = new Point(10, 74);
            textBoxOutput.Margin = new Padding(3, 2, 3, 2);
            textBoxOutput.Name = "textBoxOutput";
            textBoxOutput.Size = new Size(779, 368);
            textBoxOutput.TabIndex = 6;
            textBoxOutput.Text = "";
            // 
            // HootLoad
            // 
            HootLoad.Location = new Point(705, 35);
            HootLoad.Margin = new Padding(3, 2, 3, 2);
            HootLoad.Name = "HootLoad";
            HootLoad.Size = new Size(82, 22);
            HootLoad.TabIndex = 7;
            HootLoad.Text = "HootLoad";
            HootLoad.UseVisualStyleBackColor = true;
            HootLoad.Click += HootLoad_Click;
            // 
            // btnSaveOutput
            // 
            btnSaveOutput.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnSaveOutput.AutoSize = true;
            btnSaveOutput.Location = new Point(589, 37);
            btnSaveOutput.Name = "btnSaveOutput";
            btnSaveOutput.Size = new Size(110, 30);
            btnSaveOutput.TabIndex = 999;
            btnSaveOutput.Text = "Save Output...";
            btnSaveOutput.UseVisualStyleBackColor = true;
            btnSaveOutput.Click += SaveOutputToTextFile_Click;
            // 
            // btnDeleteLogs
            // 
            btnDeleteLogs.Location = new Point(10, 450);
            btnDeleteLogs.Name = "btnDeleteLogs";
            btnDeleteLogs.Size = new Size(75, 23);
            btnDeleteLogs.TabIndex = 8;
            btnDeleteLogs.Text = "Delete Logs";
            btnDeleteLogs.UseVisualStyleBackColor = true;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(798, 450);
            Controls.Add(btnDeleteLogs);
            Controls.Add(HootLoad);
            Controls.Add(btnOpenXml);
            Controls.Add(textBoxOutput);
            Controls.Add(progressBar1);
            Controls.Add(lblXmlFile);
            Controls.Add(lblCsvFile);
            Controls.Add(btnOpenCsv);
            Controls.Add(btnSaveOutput);
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