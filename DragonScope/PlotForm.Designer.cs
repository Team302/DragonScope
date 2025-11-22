namespace DragonScope
{
    partial class PlotForm
    {
        private System.ComponentModel.IContainer components = null;
        private ScottPlot.WinForms.FormsPlot formsPlot;
        private System.Windows.Forms.TextBox txtSearch;
        private System.Windows.Forms.ListBox lstSeries;
        private System.Windows.Forms.CheckBox chkShowErrors;
        private System.Windows.Forms.CheckBox chkGroupErrorSpans;
        private System.Windows.Forms.Button btnSelectAll;
        private System.Windows.Forms.Button btnClearSelection;

        protected override void Dispose(bool disposing)
        {
            if (disposing && components != null)
                components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            formsPlot = new ScottPlot.WinForms.FormsPlot();
            txtSearch = new System.Windows.Forms.TextBox();
            lstSeries = new System.Windows.Forms.ListBox();
            chkShowErrors = new System.Windows.Forms.CheckBox();
            chkGroupErrorSpans = new System.Windows.Forms.CheckBox();
            btnSelectAll = new System.Windows.Forms.Button();
            btnClearSelection = new System.Windows.Forms.Button();
            SuspendLayout();
            // 
            // formsPlot
            // 
            formsPlot.Location = new System.Drawing.Point(250, 12);
            formsPlot.Name = "formsPlot";
            formsPlot.Size = new System.Drawing.Size(900, 600);
            formsPlot.TabIndex = 0;
            // 
            // txtSearch
            // 
            txtSearch.Location = new System.Drawing.Point(12, 12);
            txtSearch.Name = "txtSearch";
            txtSearch.PlaceholderText = "Search series...";
            txtSearch.Size = new System.Drawing.Size(232, 27);
            txtSearch.TabIndex = 1;
            txtSearch.TextChanged += txtSearch_TextChanged;
            // 
            // lstSeries
            // 
            lstSeries.Location = new System.Drawing.Point(12, 45);
            lstSeries.Name = "lstSeries";
            lstSeries.SelectionMode = System.Windows.Forms.SelectionMode.MultiExtended;
            lstSeries.Size = new System.Drawing.Size(232, 324);
            lstSeries.TabIndex = 2;
            lstSeries.SelectedIndexChanged += lstSeries_SelectedIndexChanged;
            // 
            // chkShowErrors
            // 
            chkShowErrors.AutoSize = true;
            chkShowErrors.Location = new System.Drawing.Point(12, 380);
            chkShowErrors.Name = "chkShowErrors";
            chkShowErrors.Size = new System.Drawing.Size(107, 24);
            chkShowErrors.TabIndex = 3;
            chkShowErrors.Text = "Show Events";
            chkShowErrors.UseVisualStyleBackColor = true;
            // 
            // chkGroupErrorSpans
            // 
            chkGroupErrorSpans.AutoSize = true;
            chkGroupErrorSpans.Location = new System.Drawing.Point(12, 410);
            chkGroupErrorSpans.Name = "chkGroupErrorSpans";
            chkGroupErrorSpans.Size = new System.Drawing.Size(154, 24);
            chkGroupErrorSpans.TabIndex = 4;
            chkGroupErrorSpans.Text = "Group Intervals By Prio";
            chkGroupErrorSpans.UseVisualStyleBackColor = true;
            // 
            // btnSelectAll
            // 
            btnSelectAll.Location = new System.Drawing.Point(12, 450);
            btnSelectAll.Name = "btnSelectAll";
            btnSelectAll.Size = new System.Drawing.Size(110, 30);
            btnSelectAll.TabIndex = 5;
            btnSelectAll.Text = "Select All";
            btnSelectAll.UseVisualStyleBackColor = true;
            btnSelectAll.Click += btnSelectAll_Click;
            // 
            // btnClearSelection
            // 
            btnClearSelection.Location = new System.Drawing.Point(134, 450);
            btnClearSelection.Name = "btnClearSelection";
            btnClearSelection.Size = new System.Drawing.Size(110, 30);
            btnClearSelection.TabIndex = 6;
            btnClearSelection.Text = "Clear";
            btnClearSelection.UseVisualStyleBackColor = true;
            btnClearSelection.Click += btnClearSelection_Click;
            // 
            // PlotForm
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(8F, 20F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(1165, 626);
            Controls.Add(btnClearSelection);
            Controls.Add(btnSelectAll);
            Controls.Add(chkGroupErrorSpans);
            Controls.Add(chkShowErrors);
            Controls.Add(lstSeries);
            Controls.Add(txtSearch);
            Controls.Add(formsPlot);
            Name = "PlotForm";
            Text = "DragonScope Plot";
            ResumeLayout(false);
            PerformLayout();
        }
    }
}