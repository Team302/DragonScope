namespace DragonScope
{
    partial class Form1
    {
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.Button btnOpenCsv;
        private System.Windows.Forms.Button btnOpenXml;
        private System.Windows.Forms.Button btnOpenPlot;
        private System.Windows.Forms.Button btnCacheBrowser;
        private System.Windows.Forms.Label lblCsvFile;
        private System.Windows.Forms.Label lblXmlFile;
        private System.Windows.Forms.Button btnDeleteLogs;
        private System.Windows.Forms.ProgressBar progressBar1;
        private System.Windows.Forms.RichTextBox textBoxOutput;
        private System.Windows.Forms.Button HootLoad;
        private System.Windows.Forms.Button btnMotorStats;
        private System.Windows.Forms.Button btnMultiCacheAnalysis;
        private System.Windows.Forms.Button btnBulkProcessCache;

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
            btnOpenPlot = new Button();
            btnCacheBrowser = new Button();
            lblCsvFile = new Label();
            lblXmlFile = new Label();
            progressBar1 = new ProgressBar();
            textBoxOutput = new RichTextBox();
            HootLoad = new Button();
            btnDeleteLogs = new Button();
            btnMotorStats = new Button();
            btnMultiCacheAnalysis = new Button();
            btnBulkProcessCache = new Button();
            SuspendLayout();
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
            // btnOpenCsv
            // 
            btnOpenCsv.Location = new Point(90, 8);
            btnOpenCsv.Name = "btnOpenCsv";
            btnOpenCsv.Size = new Size(75, 23);
            btnOpenCsv.TabIndex = 1;
            btnOpenCsv.Text = "Open CSV";
            btnOpenCsv.UseVisualStyleBackColor = true;
            btnOpenCsv.Click += btnOpenCsv_Click;
            // 
            // btnOpenPlot
            // 
            btnOpenPlot.Location = new Point(171, 8);
            btnOpenPlot.Name = "btnOpenPlot";
            btnOpenPlot.Size = new Size(96, 23);
            btnOpenPlot.TabIndex = 2;
            btnOpenPlot.Text = "Open Plot";
            btnOpenPlot.UseVisualStyleBackColor = true;
            btnOpenPlot.Click += btnOpenPlot_Click;
            // 
            // btnCacheBrowser
            // 
            btnCacheBrowser.Location = new Point(272, 8);
            btnCacheBrowser.Name = "btnCacheBrowser";
            btnCacheBrowser.Size = new Size(96, 23);
            btnCacheBrowser.TabIndex = 9;
            btnCacheBrowser.Text = "Cache Browser";
            btnCacheBrowser.UseVisualStyleBackColor = true;
            btnCacheBrowser.Click += BtnCacheBrowser_Click;
            // 
            // lblCsvFile
            // 
            lblCsvFile.AutoSize = true;
            lblCsvFile.Location = new Point(90, 37);
            lblCsvFile.Name = "lblCsvFile";
            lblCsvFile.Size = new Size(0, 15);
            lblCsvFile.TabIndex = 3;
            // 
            // lblXmlFile
            // 
            lblXmlFile.AutoSize = true;
            lblXmlFile.Location = new Point(10, 37);
            lblXmlFile.Name = "lblXmlFile";
            lblXmlFile.Size = new Size(0, 15);
            lblXmlFile.TabIndex = 4;
            // 
            // progressBar1
            // 
            progressBar1.Location = new Point(611, 8);
            progressBar1.Name = "progressBar1";
            progressBar1.Size = new Size(177, 23);
            progressBar1.TabIndex = 7;
            // 
            // textBoxOutput
            // 
            textBoxOutput.Location = new Point(10, 62);
            textBoxOutput.Margin = new Padding(3, 2, 3, 2);
            textBoxOutput.Name = "textBoxOutput";
            textBoxOutput.Size = new Size(779, 380);
            textBoxOutput.TabIndex = 8;
            textBoxOutput.Text = "";
            // 
            // HootLoad
            // 
            HootLoad.Location = new Point(707, 37);
            HootLoad.Margin = new Padding(3, 2, 3, 2);
            HootLoad.Name = "HootLoad";
            HootLoad.Size = new Size(82, 22);
            HootLoad.TabIndex = 6;
            HootLoad.Text = "HootLoad";
            HootLoad.UseVisualStyleBackColor = true;
            HootLoad.Click += HootLoad_Click;
            // 
            // btnDeleteLogs
            // 
            btnDeleteLogs.Location = new Point(611, 37);
            btnDeleteLogs.Name = "btnDeleteLogs";
            btnDeleteLogs.Size = new Size(89, 22);
            btnDeleteLogs.TabIndex = 4;
            btnDeleteLogs.Text = "Clean Logs";
            btnDeleteLogs.UseVisualStyleBackColor = true;
            btnDeleteLogs.Click += btnDeleteLogs_Click;
            // 
            // btnMotorStats
            // 
            btnMotorStats.Location = new Point(373, 8);
            btnMotorStats.Name = "btnMotorStats";
            btnMotorStats.Size = new Size(109, 23);
            btnMotorStats.TabIndex = 10;
            btnMotorStats.Text = "Motor Stats";
            btnMotorStats.UseVisualStyleBackColor = true;
            btnMotorStats.Click += btnMotorStats_Click;
            // 
            // btnMultiCacheAnalysis
            // 
            btnMultiCacheAnalysis.Location = new Point(490, 8);
            btnMultiCacheAnalysis.Name = "btnMultiCacheAnalysis";
            btnMultiCacheAnalysis.Size = new Size(114, 23);
            btnMultiCacheAnalysis.TabIndex = 11;
            btnMultiCacheAnalysis.Text = "Multi-Cache Analysis";
            btnMultiCacheAnalysis.UseVisualStyleBackColor = true;
            btnMultiCacheAnalysis.Click += btnMultiCacheAnalysis_Click;
            // 
            // btnBulkProcessCache
            // 
            btnBulkProcessCache.Location = new Point(610, 8);
            btnBulkProcessCache.Name = "btnBulkProcessCache";
            btnBulkProcessCache.Size = new Size(109, 23);
            btnBulkProcessCache.TabIndex = 12;
            btnBulkProcessCache.Text = "Bulk Process & Cache";
            btnBulkProcessCache.UseVisualStyleBackColor = true;
            btnBulkProcessCache.Click += btnBulkProcessCache_Click;
            // 
            // Form1
            //
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(798, 450);
            Controls.Add(btnBulkProcessCache);
            Controls.Add(btnMultiCacheAnalysis);
            Controls.Add(btnMotorStats);
            Controls.Add(btnCacheBrowser);
            Controls.Add(btnOpenPlot);
            Controls.Add(btnDeleteLogs);
            Controls.Add(HootLoad);
            Controls.Add(btnOpenXml);
            Controls.Add(textBoxOutput);
            Controls.Add(progressBar1);
            Controls.Add(lblXmlFile);
            Controls.Add(lblCsvFile);
            Controls.Add(btnOpenCsv);
            Name = "Form1";
            Text = "DragonScope";
            ResumeLayout(false);
            PerformLayout();
        }
    }
}