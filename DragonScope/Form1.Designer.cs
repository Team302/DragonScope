namespace DragonScope
{
    partial class Form1
    {
        private System.Windows.Forms.Button btnOpenCsv;
        private System.Windows.Forms.Button btnOpenXml;
        private System.Windows.Forms.Label lblCsvFile;
        private System.Windows.Forms.Label lblXmlFile;

        private void InitializeComponent()
        {
            btnOpenCsv = new Button();
            btnOpenXml = new Button();
            lblCsvFile = new Label();
            lblXmlFile = new Label();
            progressBar1 = new ProgressBar();
            textBoxOutput = new RichTextBox();
            SuspendLayout();
            // 
            // btnOpenCsv
            // 
            btnOpenCsv.Location = new Point(14, 16);
            btnOpenCsv.Margin = new Padding(3, 4, 3, 4);
            btnOpenCsv.Name = "btnOpenCsv";
            btnOpenCsv.Size = new Size(86, 31);
            btnOpenCsv.TabIndex = 0;
            btnOpenCsv.Text = "Open CSV";
            btnOpenCsv.UseVisualStyleBackColor = true;
            btnOpenCsv.Click += btnOpenCsv_Click;
            // 
            // btnOpenXml
            // 
            btnOpenXml.Location = new Point(14, 55);
            btnOpenXml.Margin = new Padding(3, 4, 3, 4);
            btnOpenXml.Name = "btnOpenXml";
            btnOpenXml.Size = new Size(86, 31);
            btnOpenXml.TabIndex = 1;
            btnOpenXml.Text = "Open XML";
            btnOpenXml.UseVisualStyleBackColor = true;
            btnOpenXml.Click += btnOpenXml_Click;
            // 
            // lblCsvFile
            // 
            lblCsvFile.AutoSize = true;
            lblCsvFile.Location = new Point(106, 23);
            lblCsvFile.Name = "lblCsvFile";
            lblCsvFile.Size = new Size(0, 20);
            lblCsvFile.TabIndex = 2;
            // 
            // lblXmlFile
            // 
            lblXmlFile.AutoSize = true;
            lblXmlFile.Location = new Point(106, 61);
            lblXmlFile.Name = "lblXmlFile";
            lblXmlFile.Size = new Size(0, 20);
            lblXmlFile.TabIndex = 3;
            // 
            // progressBar1
            // 
            progressBar1.Location = new Point(698, 61);
            progressBar1.Margin = new Padding(3, 4, 3, 4);
            progressBar1.Name = "progressBar1";
            progressBar1.Size = new Size(202, 31);
            progressBar1.TabIndex = 5;
            // 
            // textBoxOutput
            // 
            textBoxOutput.Location = new Point(12, 99);
            textBoxOutput.Name = "textBoxOutput";
            textBoxOutput.Size = new Size(890, 489);
            textBoxOutput.TabIndex = 6;
            textBoxOutput.Text = "";
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(914, 600);
            Controls.Add(textBoxOutput);
            Controls.Add(progressBar1);
            Controls.Add(lblXmlFile);
            Controls.Add(lblCsvFile);
            Controls.Add(btnOpenXml);
            Controls.Add(btnOpenCsv);
            Margin = new Padding(3, 4, 3, 4);
            Name = "Form1";
            Text = "Form1";
            ResumeLayout(false);
            PerformLayout();
        }
        private ProgressBar progressBar1;
        private RichTextBox textBoxOutput;
    }
}