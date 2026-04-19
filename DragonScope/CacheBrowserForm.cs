using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace DragonScope
{
    public partial class CacheBrowserForm : Form
    {
        private readonly Form1 _parentForm;
        private readonly DataCacheManager _cacheManager;
        private List<CachedAnalysisMetadata> _currentResults = new();
        public CacheBrowserForm(Form1 parentForm)
        {
            _parentForm = parentForm;
            _cacheManager = ResolveSharedCacheManager(parentForm);
            InitializeComponent();
            LoadCacheList();
        }
        private DataCacheManager ResolveSharedCacheManager(Form1 parentForm)
        {
            var flags = System.Reflection.BindingFlags.Instance |
                        System.Reflection.BindingFlags.Public |
                        System.Reflection.BindingFlags.NonPublic;
            var parentType = parentForm.GetType();
            var property = parentType
                .GetProperties(flags)
                .FirstOrDefault(p => p.PropertyType == typeof(DataCacheManager) && p.GetIndexParameters().Length == 0);
            if (property?.GetValue(parentForm) is DataCacheManager propertyManager)
            {
                return propertyManager;
            }
            var field = parentType
                .GetFields(flags)
                .FirstOrDefault(f => f.FieldType == typeof(DataCacheManager));
            if (field?.GetValue(parentForm) is DataCacheManager fieldManager)
            {
                return fieldManager;
            }
            return new DataCacheManager();
        }

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 500);
            this.Text = "Cache Browser";
            this.Name = "CacheBrowserForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;

            var searchLabel = new Label
            {
                Text = "Search:",
                Location = new System.Drawing.Point(12, 12),
                AutoSize = true
            };
            this.Controls.Add(searchLabel);

            var searchBox = new TextBox
            {
                Name = "searchBox",
                Location = new System.Drawing.Point(70, 12),
                Width = 300,
                Height = 23
            };
            searchBox.TextChanged += SearchBox_TextChanged;
            this.Controls.Add(searchBox);

            var btnRefresh = new Button
            {
                Text = "Refresh",
                Location = new System.Drawing.Point(376, 12),
                Width = 75,
                Height = 23
            };
            btnRefresh.Click += BtnRefresh_Click;
            this.Controls.Add(btnRefresh);

            var btnClearCache = new Button
            {
                Text = "Clear All Cache",
                Location = new System.Drawing.Point(457, 12),
                Width = 100,
                Height = 23,
                BackColor = System.Drawing.Color.LightCoral
            };
            btnClearCache.Click += BtnClearCache_Click;
            this.Controls.Add(btnClearCache);

            var cacheListView = new ListViewEx
            {
                Name = "cacheListView",
                Location = new System.Drawing.Point(12, 45),
                Width = 776,
                Height = 380,
                View = System.Windows.Forms.View.Details,
                MultiSelect = false,
                FullRowSelect = true
            };
            cacheListView.Columns.Add("File Name", 250);
            cacheListView.Columns.Add("Cached Date", 150);
            cacheListView.Columns.Add("Lines Parsed", 80);
            cacheListView.Columns.Add("Data Hash", 180);
            cacheListView.Columns.Add("Cache ID", 100);
            cacheListView.ItemSelectionChanged += CacheListView_ItemSelectionChanged;
            cacheListView.DoubleClick += CacheListView_DoubleClick;
            this.Controls.Add(cacheListView);

            var btnLoad = new Button
            {
                Text = "Load Selected",
                Location = new System.Drawing.Point(12, 430),
                Width = 100,
                Height = 30,
                BackColor = System.Drawing.Color.LightGreen
            };
            btnLoad.Click += BtnLoad_Click;
            this.Controls.Add(btnLoad);

            var btnDelete = new Button
            {
                Text = "Delete Selected",
                Location = new System.Drawing.Point(118, 430),
                Width = 120,
                Height = 30,
                BackColor = System.Drawing.Color.LightCoral
            };
            btnDelete.Click += BtnDelete_Click;
            this.Controls.Add(btnDelete);

            var btnClose = new Button
            {
                Text = "Close",
                Location = new System.Drawing.Point(713, 430),
                Width = 75,
                Height = 30
            };
            btnClose.Click += (s, e) => this.Close();
            this.Controls.Add(btnClose);
        }

        private void LoadCacheList()
        {
            var listView = this.Controls.OfType<ListViewEx>().FirstOrDefault();
            if (listView == null) return;

            listView.Items.Clear();
            _currentResults = _cacheManager.GetAllCachedAnalyses();

            foreach (var metadata in _currentResults.OrderByDescending(m => m.CachedAt))
            {
                var item = new ListViewItem(metadata.FileName);
                item.SubItems.Add(metadata.CachedAt.ToString("yyyy-MM-dd HH:mm:ss"));
                item.SubItems.Add(metadata.LinesParsed.ToString());
                item.SubItems.Add(metadata.DataHash.Substring(0, Math.Min(16, metadata.DataHash.Length)) + "...");
                item.SubItems.Add(metadata.CacheId.Substring(0, 8) + "...");
                item.Tag = metadata.CacheId;
                listView.Items.Add(item);
            }
        }

        private void SearchBox_TextChanged(object? sender, EventArgs e)
        {
            var searchBox = this.Controls.OfType<TextBox>().FirstOrDefault(c => c.Name == "searchBox");
            var listView = this.Controls.OfType<ListViewEx>().FirstOrDefault();
            if (searchBox == null || listView == null) return;

            listView.Items.Clear();
            var searchTerm = searchBox.Text.Trim();
            var results = string.IsNullOrEmpty(searchTerm)
                ? _cacheManager.GetAllCachedAnalyses()
                : _cacheManager.SearchCachedAnalyses(searchTerm);

            foreach (var metadata in results.OrderByDescending(m => m.CachedAt))
            {
                var item = new ListViewItem(metadata.FileName);
                item.SubItems.Add(metadata.CachedAt.ToString("yyyy-MM-dd HH:mm:ss"));
                item.SubItems.Add(metadata.LinesParsed.ToString());
                item.SubItems.Add(metadata.DataHash.Substring(0, Math.Min(16, metadata.DataHash.Length)) + "...");
                item.SubItems.Add(metadata.CacheId.Substring(0, 8) + "...");
                item.Tag = metadata.CacheId;
                listView.Items.Add(item);
            }
        }

        private void BtnRefresh_Click(object? sender, EventArgs e)
        {
            LoadCacheList();
            MessageBox.Show("Cache list refreshed.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void BtnClearCache_Click(object? sender, EventArgs e)
        {
            var result = MessageBox.Show("Are you sure you want to clear all cached analyses?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (result == DialogResult.Yes)
            {
                _parentForm.ClearAllCache();
                LoadCacheList();
            }
        }

        private void CacheListView_ItemSelectionChanged(object? sender, ListViewItemSelectionChangedEventArgs e)
        {
        }

        private void CacheListView_DoubleClick(object? sender, EventArgs e)
        {
            BtnLoad_Click(null, EventArgs.Empty);
        }

        private async void BtnLoad_Click(object? sender, EventArgs e)
        {
            var listView = this.Controls.OfType<ListViewEx>().FirstOrDefault();
            if (listView?.SelectedItems.Count > 0)
            {
                var selectedItem = listView.SelectedItems[0];
                var cacheId = selectedItem.Tag?.ToString();
                if (cacheId != null)
                {
                    this.Enabled = false;
                    await _parentForm.LoadCachedAnalysisAsync(cacheId);
                    this.Enabled = true;
                    MessageBox.Show("Cached analysis loaded into RAM and displayed in the grapher.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            else
            {
                MessageBox.Show("Please select an analysis to load.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            this.Close();
        }

        private void BtnDelete_Click(object? sender, EventArgs e)
        {
            var listView = this.Controls.OfType<ListViewEx>().FirstOrDefault();
            if (listView?.SelectedItems.Count > 0)
            {
                var selectedItem = listView.SelectedItems[0];
                var cacheId = selectedItem.Tag?.ToString();
                if (cacheId != null)
                {
                    var result = MessageBox.Show($"Delete '{selectedItem.Text}'?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                    if (result == DialogResult.Yes)
                    {
                        _parentForm.DeleteCachedAnalysis(cacheId);
                        LoadCacheList();
                    }
                }
            }
            else
            {
                MessageBox.Show("Please select an analysis to delete.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private class ListViewEx : ListView
        {
            protected override void OnHandleCreated(EventArgs e)
            {
                base.OnHandleCreated(e);
                SendMessage(Handle, LVM_SETEXTENDEDLISTVIEWSTYLE, IntPtr.Zero, new IntPtr(LVS_EX_FULLROWSELECT | LVS_EX_GRIDLINES));
            }

            [System.Runtime.InteropServices.DllImport("user32.dll")]
            private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

            private const int LVM_SETEXTENDEDLISTVIEWSTYLE = 0x1000 + 54;
            private const int LVS_EX_FULLROWSELECT = 0x20;
            private const int LVS_EX_GRIDLINES = 0x1;
        }

        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
