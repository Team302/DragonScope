namespace DragonScope
{
    partial class Form1
    {
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.Button btnSaveOutput;
        private System.Windows.Forms.Button btnOpenCsv;
        private System.Windows.Forms.Button btnOpenXml;
        private System.Windows.Forms.Button btnOpenPlot; // NEW: Open Plot button
        private System.Windows.Forms.Label lblCsvFile;
        private System.Windows.Forms.Label lblXmlFile;
        private System.Windows.Forms.Button btnDeleteLogs;
        private System.Windows.Forms.ProgressBar progressBar1;
        private System.Windows.Forms.RichTextBox textBoxOutput;
        private System.Windows.Forms.Button HootLoad;

        protected override void Dispose(bool disposing)
        {
            if (disposing && components != null)
                components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            btnOpenXml = new Button();
            btnOpenCsv = new Button();
            btnOpenPlot = new Button(); // instantiate
            lblCsvFile = new Label();
            lblXmlFile = new Label();
            progressBar1 = new ProgressBar();
            textBoxOutput = new RichTextBox();
            HootLoad = new Button();
            btnSaveOutput = new Button();
            btnDeleteLogs = new Button();
            SuspendLayout();
            // 
            // btnOpenXml
            // 
            btnOpenXml.Location = new System.Drawing.Point(11, 11);
            btnOpenXml.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            btnOpenXml.Name = "btnOpenXml";
            btnOpenXml.Size = new System.Drawing.Size(86, 31);
            btnOpenXml.TabIndex = 0;
            btnOpenXml.Text = "Open XML";
            btnOpenXml.UseVisualStyleBackColor = true;
            btnOpenXml.Click += btnOpenXml_Click;
            // 
            // btnOpenCsv
            // 
            btnOpenCsv.Location = new System.Drawing.Point(103, 11);
            btnOpenCsv.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            btnOpenCsv.Name = "btnOpenCsv";
            btnOpenCsv.Size = new System.Drawing.Size(86, 31);
            btnOpenCsv.TabIndex = 1;
            btnOpenCsv.Text = "Open CSV";
            btnOpenCsv.UseVisualStyleBackColor = true;
            btnOpenCsv.Click += btnOpenCsv_Click;
            // 
            // btnOpenPlot
            // 
            btnOpenPlot.Location = new System.Drawing.Point(195, 11);
            btnOpenPlot.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            btnOpenPlot.Name = "btnOpenPlot";
            btnOpenPlot.Size = new System.Drawing.Size(110, 31);
            btnOpenPlot.TabIndex = 2;
            btnOpenPlot.Text = "Open Plot";
            btnOpenPlot.UseVisualStyleBackColor = true;
            btnOpenPlot.Click += btnOpenPlot_Click;
            // 
            // lblCsvFile
            // 
            lblCsvFile.AutoSize = true;
            lblCsvFile.Location = new System.Drawing.Point(103, 49);
            lblCsvFile.Name = "lblCsvFile";
            lblCsvFile.Size = new System.Drawing.Size(0, 20);
            lblCsvFile.TabIndex = 3;
            // 
            // lblXmlFile
            // 
            lblXmlFile.AutoSize = true;
            lblXmlFile.Location = new System.Drawing.Point(11, 49);
            lblXmlFile.Name = "lblXmlFile";
            lblXmlFile.Size = new System.Drawing.Size(0, 20);
            lblXmlFile.TabIndex = 4;
            // 
            // progressBar1
            // 
            progressBar1.Location = new System.Drawing.Point(698, 11);
            progressBar1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            progressBar1.Name = "progressBar1";
            progressBar1.Size = new System.Drawing.Size(202, 31);
            progressBar1.TabIndex = 7;
            // 
            // textBoxOutput
            // 
            textBoxOutput.Location = new System.Drawing.Point(11, 83);
            textBoxOutput.Name = "textBoxOutput";
            textBoxOutput.Size = new System.Drawing.Size(890, 505);
            textBoxOutput.TabIndex = 8;
            textBoxOutput.Text = "";
            // 
            // HootLoad
            // 
            HootLoad.Location = new System.Drawing.Point(806, 49);
            HootLoad.Name = "HootLoad";
            HootLoad.Size = new System.Drawing.Size(94, 29);
            HootLoad.TabIndex = 6;
            HootLoad.Text = "HootLoad";
            HootLoad.UseVisualStyleBackColor = true;
            HootLoad.Click += HootLoad_Click;
            // 
            // btnSaveOutput
            // 
            btnSaveOutput.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            btnSaveOutput.AutoSize = true;
            btnSaveOutput.Location = new System.Drawing.Point(673, 49);
            btnSaveOutput.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            btnSaveOutput.Name = "btnSaveOutput";
            btnSaveOutput.Size = new System.Drawing.Size(126, 29);
            btnSaveOutput.TabIndex = 5;
            btnSaveOutput.Text = "Save Output...";
            btnSaveOutput.UseVisualStyleBackColor = true;
            btnSaveOutput.Click += SaveOutputToTextFile_Click;
            // 
            // btnDeleteLogs
            // 
            btnDeleteLogs.Location = new System.Drawing.Point(565, 49);
            btnDeleteLogs.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            btnDeleteLogs.Name = "btnDeleteLogs";
            btnDeleteLogs.Size = new System.Drawing.Size(102, 29);
            btnDeleteLogs.TabIndex = 4;
            btnDeleteLogs.Text = "Clean Logs";
            btnDeleteLogs.UseVisualStyleBackColor = true;
            btnDeleteLogs.Click += btnDeleteLogs_Click;
            // 
            // Form1
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(8F, 20F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(912, 600);
            Controls.Add(btnOpenPlot);
            Controls.Add(btnDeleteLogs);
            Controls.Add(HootLoad);
            Controls.Add(btnOpenXml);
            Controls.Add(textBoxOutput);
            Controls.Add(progressBar1);
            Controls.Add(btnSaveOutput);
            Controls.Add(lblXmlFile);
            Controls.Add(lblCsvFile);
            Controls.Add(btnOpenCsv);
            Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            Name = "Form1";
            Text = "DragonScope";
            ResumeLayout(false);
            PerformLayout();
        }
    }
}