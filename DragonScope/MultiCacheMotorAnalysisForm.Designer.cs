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

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            topPanel = new Panel();
            lblSeries = new Label();
            btnAnalyze = new Button();
            btnExport = new Button();
            lblColors = new Label();
            colorPanel = new FlowLayoutPanel();
            lblCaches = new Label();
            cacheListBox = new CheckedListBox();
            seriesCombo = new ComboBox();
            formsPlot = new FormsPlot();
            SuspendLayout();
            // 
            // topPanel
            // 
            topPanel.Location = new Point(12, 168);
            topPanel.Name = "topPanel";
            topPanel.Size = new Size(200, 123);
            topPanel.TabIndex = 0;
            // 
            // lblSeries
            // 
            lblSeries.Location = new Point(156, 125);
            lblSeries.Name = "lblSeries";
            lblSeries.Size = new Size(100, 23);
            lblSeries.TabIndex = 2;
            // 
            // btnAnalyze
            // 
            btnAnalyze.Location = new Point(313, 105);
            btnAnalyze.Name = "btnAnalyze";
            btnAnalyze.Size = new Size(75, 23);
            btnAnalyze.TabIndex = 4;
            btnAnalyze.Text = "Analyze";
            btnAnalyze.Click += BtnAnalyze_Click;
            // 
            // btnExport
            // 
            btnExport.Location = new Point(394, 105);
            btnExport.Name = "btnExport";
            btnExport.Size = new Size(75, 23);
            btnExport.TabIndex = 5;
            btnExport.Text = "Export";
            btnExport.Click += BtnExport_Click;
            // 
            // lblColors
            // 
            lblColors.Location = new Point(185, 102);
            lblColors.Name = "lblColors";
            lblColors.Size = new Size(100, 23);
            lblColors.TabIndex = 6;
            // 
            // colorPanel
            // 
            colorPanel.Location = new Point(227, 168);
            colorPanel.Name = "colorPanel";
            colorPanel.Size = new Size(200, 123);
            colorPanel.TabIndex = 7;
            // 
            // lblCaches
            // 
            lblCaches.Location = new Point(207, 102);
            lblCaches.Name = "lblCaches";
            lblCaches.Size = new Size(100, 23);
            lblCaches.TabIndex = 0;
            // 
            // cacheListBox
            // 
            cacheListBox.Location = new Point(317, 41);
            cacheListBox.Name = "cacheListBox";
            cacheListBox.Size = new Size(134, 58);
            cacheListBox.TabIndex = 1;
            cacheListBox.ItemCheck += CacheListBox_ItemCheck;
            // 
            // seriesCombo
            // 
            seriesCombo.Location = new Point(317, 12);
            seriesCombo.Name = "seriesCombo";
            seriesCombo.Size = new Size(121, 23);
            seriesCombo.TabIndex = 3;
            seriesCombo.SelectedIndexChanged += SeriesCombo_SelectedIndexChanged;
            // 
            // formsPlot
            // 
            formsPlot.DisplayScale = 1F;
            formsPlot.Location = new Point(0, 12);
            formsPlot.Name = "formsPlot";
            formsPlot.Size = new Size(150, 150);
            formsPlot.TabIndex = 1;
            // 
            // MultiCacheMotorAnalysisForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(512, 303);
            Controls.Add(lblColors);
            Controls.Add(btnExport);
            Controls.Add(colorPanel);
            Controls.Add(btnAnalyze);
            Controls.Add(lblSeries);
            Controls.Add(lblCaches);
            Controls.Add(topPanel);
            Controls.Add(cacheListBox);
            Controls.Add(seriesCombo);
            Controls.Add(formsPlot);
            Name = "MultiCacheMotorAnalysisForm";
            StartPosition = FormStartPosition.CenterParent;
            Text = "Multi-Cache Motor Analysis";
            ResumeLayout(false);
        }
    }
}
