namespace DragonScope
{
    partial class MotorStatsForm
    {
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.TableLayoutPanel tableLayout;
        private System.Windows.Forms.Label titleLabel;
        private System.Windows.Forms.FlowLayoutPanel selectorPanel;
        private System.Windows.Forms.Label selectorLabel;
        private System.Windows.Forms.ComboBox seriesComboBox;
        private System.Windows.Forms.Button updateButton;
        private System.Windows.Forms.RichTextBox resultsTextBox;

        protected override void Dispose(bool disposing)
        {
            if (disposing && components != null)
                components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.tableLayout = new System.Windows.Forms.TableLayoutPanel();
            this.titleLabel = new System.Windows.Forms.Label();
            this.selectorPanel = new System.Windows.Forms.FlowLayoutPanel();
            this.selectorLabel = new System.Windows.Forms.Label();
            this.seriesComboBox = new System.Windows.Forms.ComboBox();
            this.updateButton = new System.Windows.Forms.Button();
            this.resultsTextBox = new System.Windows.Forms.RichTextBox();

            this.tableLayout.SuspendLayout();
            this.selectorPanel.SuspendLayout();
            this.SuspendLayout();

            // tableLayout
            this.tableLayout.ColumnCount = 1;
            this.tableLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayout.Controls.Add(this.titleLabel, 0, 0);
            this.tableLayout.Controls.Add(this.selectorPanel, 0, 1);
            this.tableLayout.Controls.Add(this.resultsTextBox, 0, 2);
            this.tableLayout.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayout.Name = "tableLayout";
            this.tableLayout.Padding = new System.Windows.Forms.Padding(10);
            this.tableLayout.RowCount = 3;
            this.tableLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tableLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tableLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));

            // titleLabel
            this.titleLabel.AutoSize = true;
            this.titleLabel.Font = new System.Drawing.Font(this.Font.FontFamily, 14F, System.Drawing.FontStyle.Bold);
            this.titleLabel.Location = new System.Drawing.Point(13, 10);
            this.titleLabel.Margin = new System.Windows.Forms.Padding(0, 0, 0, 10);
            this.titleLabel.Name = "titleLabel";
            this.titleLabel.Size = new System.Drawing.Size(286, 25);
            this.titleLabel.TabIndex = 0;
            this.titleLabel.Text = "Motor Stator Current Statistics";

            // selectorPanel
            this.selectorPanel.AutoSize = true;
            this.selectorPanel.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.selectorPanel.Controls.Add(this.selectorLabel);
            this.selectorPanel.Controls.Add(this.seriesComboBox);
            this.selectorPanel.Controls.Add(this.updateButton);
            this.selectorPanel.FlowDirection = System.Windows.Forms.FlowDirection.LeftToRight;
            this.selectorPanel.Location = new System.Drawing.Point(13, 45);
            this.selectorPanel.Margin = new System.Windows.Forms.Padding(0, 0, 0, 10);
            this.selectorPanel.Name = "selectorPanel";
            this.selectorPanel.Size = new System.Drawing.Size(364, 29);
            this.selectorPanel.TabIndex = 1;
            this.selectorPanel.WrapContents = false;

            // selectorLabel
            this.selectorLabel.AutoSize = true;
            this.selectorLabel.Location = new System.Drawing.Point(0, 0);
            this.selectorLabel.Margin = new System.Windows.Forms.Padding(0, 0, 5, 0);
            this.selectorLabel.Name = "selectorLabel";
            this.selectorLabel.Size = new System.Drawing.Size(89, 15);
            this.selectorLabel.TabIndex = 0;
            this.selectorLabel.Text = "Select Series:";
            this.selectorLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;

            // seriesComboBox
            this.seriesComboBox.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.SuggestAppend;
            this.seriesComboBox.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.ListItems;
            this.seriesComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.seriesComboBox.FormattingEnabled = true;
            this.seriesComboBox.Location = new System.Drawing.Point(94, 3);
            this.seriesComboBox.Margin = new System.Windows.Forms.Padding(0, 0, 10, 0);
            this.seriesComboBox.Name = "seriesComboBox";
            this.seriesComboBox.Size = new System.Drawing.Size(200, 23);
            this.seriesComboBox.TabIndex = 1;
            this.seriesComboBox.SelectedIndexChanged += new System.EventHandler(this.SeriesComboBox_SelectedIndexChanged);

            // updateButton
            this.updateButton.Location = new System.Drawing.Point(304, 3);
            this.updateButton.Margin = new System.Windows.Forms.Padding(0, 0, 0, 0);
            this.updateButton.Name = "updateButton";
            this.updateButton.Size = new System.Drawing.Size(150, 23);
            this.updateButton.TabIndex = 2;
            this.updateButton.Text = "Calculate Statistics";
            this.updateButton.UseVisualStyleBackColor = true;
            this.updateButton.Click += new System.EventHandler(this.UpdateButton_Click);

            // resultsTextBox
            this.resultsTextBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.resultsTextBox.Font = new System.Drawing.Font("Courier New", 10F);
            this.resultsTextBox.Location = new System.Drawing.Point(13, 84);
            this.resultsTextBox.Margin = new System.Windows.Forms.Padding(0, 10, 0, 0);
            this.resultsTextBox.Name = "resultsTextBox";
            this.resultsTextBox.ReadOnly = true;
            this.resultsTextBox.Size = new System.Drawing.Size(874, 493);
            this.resultsTextBox.TabIndex = 2;
            this.resultsTextBox.Text = "";

            // MotorStatsForm
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(900, 600);
            this.Controls.Add(this.tableLayout);
            this.Name = "MotorStatsForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Motor Statistics";

            this.tableLayout.ResumeLayout(false);
            this.tableLayout.PerformLayout();
            this.selectorPanel.ResumeLayout(false);
            this.selectorPanel.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();
        }
    }
}
