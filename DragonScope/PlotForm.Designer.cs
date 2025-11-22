namespace DragonScope
{
    partial class PlotForm
    {
        private System.ComponentModel.IContainer components = null;
        private ScottPlot.WinForms.FormsPlot formsPlot;
        private System.Windows.Forms.CheckedListBox seriesList;
        private System.Windows.Forms.CheckBox chkShowErrors;
        private System.Windows.Forms.CheckBox chkGroupErrorSpans;
        private System.Windows.Forms.ComboBox cmbSeriesPicker;
        private System.Windows.Forms.Button btnPlotSelected;
        private System.Windows.Forms.Panel rightPanel;
        private System.Windows.Forms.Panel topPickerPanel;

        protected override void Dispose(bool disposing)
        {
            if (disposing && components != null)
                components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.formsPlot = new ScottPlot.WinForms.FormsPlot();
            this.seriesList = new System.Windows.Forms.CheckedListBox();
            this.chkShowErrors = new System.Windows.Forms.CheckBox();
            this.chkGroupErrorSpans = new System.Windows.Forms.CheckBox();
            this.cmbSeriesPicker = new System.Windows.Forms.ComboBox();
            this.btnPlotSelected = new System.Windows.Forms.Button();
            this.rightPanel = new System.Windows.Forms.Panel();
            this.topPickerPanel = new System.Windows.Forms.Panel();
            this.rightPanel.SuspendLayout();
            this.topPickerPanel.SuspendLayout();
            this.SuspendLayout();
            // 
            // formsPlot
            // 
            this.formsPlot.Dock = System.Windows.Forms.DockStyle.Fill;
            this.formsPlot.Location = new System.Drawing.Point(0, 0);
            this.formsPlot.Name = "formsPlot";
            this.formsPlot.Size = new System.Drawing.Size(1000, 560);
            this.formsPlot.TabIndex = 0;
            // 
            // seriesList
            // 
            this.seriesList.CheckOnClick = true;
            this.seriesList.Dock = System.Windows.Forms.DockStyle.Fill;
            this.seriesList.FormattingEnabled = true;
            this.seriesList.Location = new System.Drawing.Point(0, 124);
            this.seriesList.Name = "seriesList";
            this.seriesList.Size = new System.Drawing.Size(220, 436);
            this.seriesList.TabIndex = 4;
            this.seriesList.ItemCheck += new System.Windows.Forms.ItemCheckEventHandler(this.seriesList_ItemCheck);
            // 
            // chkShowErrors
            // 
            this.chkShowErrors.Dock = System.Windows.Forms.DockStyle.Top;
            this.chkShowErrors.Location = new System.Drawing.Point(0, 74);
            this.chkShowErrors.Name = "chkShowErrors";
            this.chkShowErrors.Size = new System.Drawing.Size(220, 25);
            this.chkShowErrors.TabIndex = 2;
            this.chkShowErrors.Text = "Show error intervals";
            this.chkShowErrors.Checked = true;
            this.chkShowErrors.UseVisualStyleBackColor = true;
            this.chkShowErrors.CheckedChanged += new System.EventHandler(this.RenderPlot);
            // 
            // chkGroupErrorSpans
            // 
            this.chkGroupErrorSpans.Dock = System.Windows.Forms.DockStyle.Top;
            this.chkGroupErrorSpans.Location = new System.Drawing.Point(0, 99);
            this.chkGroupErrorSpans.Name = "chkGroupErrorSpans";
            this.chkGroupErrorSpans.Size = new System.Drawing.Size(220, 25);
            this.chkGroupErrorSpans.TabIndex = 3;
            this.chkGroupErrorSpans.Text = "Group spans by priority";
            this.chkGroupErrorSpans.Checked = true;
            this.chkGroupErrorSpans.UseVisualStyleBackColor = true;
            this.chkGroupErrorSpans.CheckedChanged += new System.EventHandler(this.RenderPlot);
            // 
            // cmbSeriesPicker
            // 
            this.cmbSeriesPicker.Dock = System.Windows.Forms.DockStyle.Top;
            this.cmbSeriesPicker.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbSeriesPicker.FormattingEnabled = true;
            this.cmbSeriesPicker.Location = new System.Drawing.Point(0, 0);
            this.cmbSeriesPicker.Name = "cmbSeriesPicker";
            this.cmbSeriesPicker.Size = new System.Drawing.Size(220, 23);
            this.cmbSeriesPicker.TabIndex = 0;
            // 
            // btnPlotSelected
            // 
            this.btnPlotSelected.Dock = System.Windows.Forms.DockStyle.Top;
            this.btnPlotSelected.Location = new System.Drawing.Point(0, 23);
            this.btnPlotSelected.Name = "btnPlotSelected";
            this.btnPlotSelected.Size = new System.Drawing.Size(220, 28);
            this.btnPlotSelected.TabIndex = 1;
            this.btnPlotSelected.Text = "Plot Selected";
            this.btnPlotSelected.UseVisualStyleBackColor = true;
            this.btnPlotSelected.Click += new System.EventHandler(this.btnPlotSelected_Click);
            // 
            // rightPanel
            // 
            this.rightPanel.Controls.Add(this.seriesList);
            this.rightPanel.Controls.Add(this.chkGroupErrorSpans);
            this.rightPanel.Controls.Add(this.chkShowErrors);
            this.rightPanel.Controls.Add(this.topPickerPanel);
            this.rightPanel.Dock = System.Windows.Forms.DockStyle.Right;
            this.rightPanel.Location = new System.Drawing.Point(1000, 0);
            this.rightPanel.Name = "rightPanel";
            this.rightPanel.Padding = new System.Windows.Forms.Padding(0);
            this.rightPanel.Size = new System.Drawing.Size(220, 560);
            this.rightPanel.TabIndex = 5;
            // 
            // topPickerPanel
            // 
            this.topPickerPanel.Controls.Add(this.btnPlotSelected);
            this.topPickerPanel.Controls.Add(this.cmbSeriesPicker);
            this.topPickerPanel.Dock = System.Windows.Forms.DockStyle.Top;
            this.topPickerPanel.Location = new System.Drawing.Point(0, 0);
            this.topPickerPanel.Name = "topPickerPanel";
            this.topPickerPanel.Size = new System.Drawing.Size(220, 74);
            this.topPickerPanel.TabIndex = 1;
            // 
            // PlotForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1220, 560);
            this.Controls.Add(this.formsPlot);
            this.Controls.Add(this.rightPanel);
            this.Name = "PlotForm";
            this.Text = "Data Plot";
            this.rightPanel.ResumeLayout(false);
            this.topPickerPanel.ResumeLayout(false);
            this.ResumeLayout(false);
        }
    }
}