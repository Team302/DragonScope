using System.Drawing;
using System.Windows.Forms;
using ScottPlot.WinForms;

namespace DragonScope
{
    partial class MultiCacheMotorAnalysisForm
    {
        private System.ComponentModel.IContainer components = null;

        private Panel topPanel;
        private Label lblCaches;
        private CheckedListBox cacheListBox;
        private Label lblSeries;
        private ComboBox seriesCombo;
        private Button btnAnalyze;
        private Button btnExport;
        private Label lblColors;
        private FlowLayoutPanel colorPanel;
        private FormsPlot formsPlot;
        private RichTextBox statusTextBox;

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
            btnAnalyze = new Button();
            btnExport = new Button();
            colorPanel = new FlowLayoutPanel();
            cacheListBox = new CheckedListBox();
            seriesCombo = new ComboBox();
            formsPlot = new FormsPlot();
            statusTextBox = new RichTextBox();
            SuspendLayout();
            // 
            // btnAnalyze
            // 
            btnAnalyze.Location = new Point(12, 105);
            btnAnalyze.Name = "btnAnalyze";
            btnAnalyze.Size = new Size(75, 23);
            btnAnalyze.TabIndex = 4;
            btnAnalyze.Text = "Analyze";
            btnAnalyze.Click += BtnAnalyze_Click;
            // 
            // btnExport
            // 
            btnExport.Location = new Point(93, 105);
            btnExport.Name = "btnExport";
            btnExport.Size = new Size(75, 23);
            btnExport.TabIndex = 5;
            btnExport.Text = "Export";
            btnExport.Click += BtnExport_Click;
            // 
            // colorPanel
            // 
            colorPanel.Location = new Point(174, 41);
            colorPanel.Name = "colorPanel";
            colorPanel.Size = new Size(512, 123);
            colorPanel.TabIndex = 7;
            // 
            // cacheListBox
            // 
            cacheListBox.Location = new Point(12, 41);
            cacheListBox.Name = "cacheListBox";
            cacheListBox.Size = new Size(134, 58);
            cacheListBox.TabIndex = 1;
            cacheListBox.ItemCheck += CacheListBox_ItemCheck;
            // 
            // seriesCombo
            // 
            seriesCombo.Location = new Point(12, 12);
            seriesCombo.Name = "seriesCombo";
            seriesCombo.Size = new Size(674, 23);
            seriesCombo.TabIndex = 3;
            seriesCombo.SelectedIndexChanged += SeriesCombo_SelectedIndexChanged;
            // 
            // formsPlot
            // 
            formsPlot.DisplayScale = 1F;
            formsPlot.Location = new Point(0, 168);
            formsPlot.Name = "formsPlot";
            formsPlot.Size = new Size(686, 340);
            formsPlot.TabIndex = 1;
            // 
            // statusTextBox
            // 
            statusTextBox.Location = new Point(12, 514);
            statusTextBox.Name = "statusTextBox";
            statusTextBox.ReadOnly = true;
            statusTextBox.Size = new Size(674, 100);
            statusTextBox.TabIndex = 8;
            statusTextBox.Text = "";
            // 
            // MultiCacheMotorAnalysisForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(686, 626);
            Controls.Add(statusTextBox);
            Controls.Add(btnExport);
            Controls.Add(colorPanel);
            Controls.Add(btnAnalyze);
            Controls.Add(cacheListBox);
            Controls.Add(seriesCombo);
            Controls.Add(formsPlot);
            Name = "MultiCacheMotorAnalysisForm";
            StartPosition = FormStartPosition.CenterParent;
            Text = "Multi-Cache Motor Analysis";
            ResumeLayout(false);
        }

        #endregion
    }
}
