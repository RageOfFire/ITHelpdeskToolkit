using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using ITHelpdeskToolkit.Models;
using ITHelpdeskToolkit.Services;
using ITHelpdeskToolkit.UI.Controls;
using ITHelpdeskToolkit.UI.Theme;

namespace ITHelpdeskToolkit.UI.Views
{
    public class CleanupView : UserControl
    {
        private readonly DataGridView _grid;
        private readonly TextBox _txtDetails;
        private readonly ModernButton _btnRunAll;
        private readonly ModernButton _btnRunSelected;
        private readonly ModernButton _btnDiskCleanup;
        private readonly Label _lblDiskSpace;
        private readonly List<RepairActionItem> _actions;

        public CleanupView()
        {
            DoubleBuffered = true;
            BackColor = DarkColors.Background;
            Dock = DockStyle.Fill;
            Padding = new Padding(25);

            _actions = CleanupService.GetCleanupActions();

            // Title
            Label lblTitle = new()
            {
                BackColor = Color.Transparent,
                Text = "System Cleanup",
                Font = new Font("Segoe UI", 20F, FontStyle.Bold),
                ForeColor = DarkColors.TextMain,
                AutoSize = true,
                Location = new Point(0, 0)
            };
            Controls.Add(lblTitle);

            Label lblSub = new()
            {
                BackColor = Color.Transparent,
                Text = "Free up disk space by clearing temp files, browser caches, and Windows Update leftovers.",
                Font = new Font("Segoe UI", 10F),
                ForeColor = DarkColors.TextMuted,
                AutoSize = true,
                Location = new Point(4, 38)
            };
            Controls.Add(lblSub);

            // Disk Space Banner Card
            CardPanel spaceCard = new()
            {
                Location = new Point(0, 75),
                Size = new Size(950, 50),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Padding = new Padding(15, 10, 15, 10)
            };
            Controls.Add(spaceCard);

            Label lblSpaceTitle = new()
            {
                BackColor = Color.Transparent,
                Text = "Disk Space:",
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = DarkColors.TextMuted,
                AutoSize = true,
                Location = new Point(15, 15)
            };
            spaceCard.Controls.Add(lblSpaceTitle);

            _lblDiskSpace = new Label
            {
                BackColor = Color.Transparent,
                Text = "Checking...",
                Font = new Font("Consolas", 10F, FontStyle.Bold),
                ForeColor = DarkColors.Success,
                AutoSize = true,
                Location = new Point(100, 15)
            };
            spaceCard.Controls.Add(_lblDiskSpace);

            // Action Buttons Toolbar
            FlowLayoutPanel actionPanel = new()
            {
                Location = new Point(0, 135),
                Size = new Size(950, 45),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            Controls.Add(actionPanel);

            _btnRunAll = new ModernButton
            {
                Text = "▶ Run All Cleanup",
                Style = ButtonStyle.Primary,
                Width = 150,
                Margin = new Padding(0, 0, 10, 0)
            };
            _btnRunAll.Click += async (s, e) => await RunAllCleanupAsync();
            actionPanel.Controls.Add(_btnRunAll);

            _btnRunSelected = new ModernButton
            {
                Text = "▶ Run Selected",
                Style = ButtonStyle.Secondary,
                Width = 140,
                Margin = new Padding(0, 0, 10, 0)
            };
            _btnRunSelected.Click += async (s, e) => await RunSelectedCleanupAsync();
            actionPanel.Controls.Add(_btnRunSelected);

            _btnDiskCleanup = new ModernButton
            {
                Text = "🧰 Open Disk Cleanup",
                Style = ButtonStyle.Secondary,
                Width = 175
            };
            _btnDiskCleanup.Click += (s, e) => LaunchDiskCleanup();
            actionPanel.Controls.Add(_btnDiskCleanup);

            // Table Grid
            _grid = new DataGridView
            {
                Location = new Point(0, 190),
                Size = new Size(950, 210),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BackgroundColor = DarkColors.CardBg,
                BorderStyle = BorderStyle.None,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                GridColor = DarkColors.CardBorder,
                RowHeadersVisible = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };

            _grid.EnableHeadersVisualStyles = false;
            _grid.ColumnHeadersDefaultCellStyle.BackColor = DarkColors.Header;
            _grid.ColumnHeadersDefaultCellStyle.ForeColor = DarkColors.TextMain;
            _grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            _grid.ColumnHeadersHeight = 34;

            _grid.DefaultCellStyle.BackColor = DarkColors.CardBg;
            _grid.DefaultCellStyle.ForeColor = DarkColors.TextMain;
            _grid.DefaultCellStyle.SelectionBackColor = DarkColors.Primary;
            _grid.DefaultCellStyle.SelectionForeColor = Color.White;
            _grid.DefaultCellStyle.Font = new Font("Segoe UI", 9.5F);
            _grid.RowTemplate.Height = 30;

            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Cleanup", HeaderText = "Cleanup Action", FillWeight = 70 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Status", HeaderText = "Status", FillWeight = 30 });

            _grid.SelectionChanged += GridSelectionChanged;
            Controls.Add(_grid);

            // Details Log
            Label lblDetails = new()
            {
                BackColor = Color.Transparent,
                Text = "Details",
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = DarkColors.TextMain,
                AutoSize = true,
                Location = new Point(0, 412)
            };
            Controls.Add(lblDetails);

            _txtDetails = new TextBox
            {
                Location = new Point(0, 440),
                Size = new Size(950, 180),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                ReadOnly = true,
                BackColor = DarkColors.InputBg,
                ForeColor = DarkColors.TextMain,
                Font = new Font("Consolas", 9.5F),
                BorderStyle = BorderStyle.FixedSingle
            };
            Controls.Add(_txtDetails);

            PopulateGrid();
            RefreshDiskSpace();
        }

        private void PopulateGrid()
        {
            _grid.Rows.Clear();
            foreach (var item in _actions)
            {
                _grid.Rows.Add(item.Title, "NOT RUN");
            }
        }

        private void RefreshDiskSpace()
        {
            _lblDiskSpace.Text = CleanupService.GetDiskSpaceSummary();
        }

        private async Task RunSelectedCleanupAsync()
        {
            if (_grid.SelectedRows.Count == 0) return;
            int idx = _grid.SelectedRows[0].Index;
            await ExecuteCleanupAsync(idx);
            RefreshDiskSpace();
        }

        private async Task RunAllCleanupAsync()
        {
            DialogResult dr = MessageBox.Show(
                "This will clear Temp files, Prefetch, browser caches, Recycle Bin, and run Windows Update DISM cleanup.\r\n\r\nContinue?",
                "Run All Cleanup",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question
            );

            if (dr != DialogResult.Yes) return;

            _btnRunAll.Enabled = false;
            _btnRunSelected.Enabled = false;

            for (int i = 0; i < _actions.Count; i++)
            {
                await ExecuteCleanupAsync(i);
            }

            _btnRunAll.Enabled = true;
            _btnRunSelected.Enabled = true;
            RefreshDiskSpace();
        }

        private async Task ExecuteCleanupAsync(int idx)
        {
            if (idx < 0 || idx >= _actions.Count) return;
            var item = _actions[idx];
            var row = _grid.Rows[idx];

            row.Cells["Status"].Value = "RUNNING...";
            row.DefaultCellStyle.ForeColor = DarkColors.Warning;

            var (success, output) = await item.Action();

            item.Status = success ? RepairStatus.Done : RepairStatus.Failed;
            item.LastOutput = output;

            row.Cells["Status"].Value = success ? "✓ DONE" : "✗ FAILED";
            row.DefaultCellStyle.ForeColor = success ? DarkColors.Success : DarkColors.Danger;

            GridSelectionChanged(null, EventArgs.Empty);
        }

        private void LaunchDiskCleanup()
        {
            var (success, output) = CleanupService.OpenDiskCleanup();
            if (!success)
            {
                MessageBox.Show($"Could not launch Disk Cleanup: {output}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void GridSelectionChanged(object? sender, EventArgs e)
        {
            if (_grid.SelectedRows.Count == 0) return;
            int idx = _grid.SelectedRows[0].Index;
            if (idx >= 0 && idx < _actions.Count)
            {
                var item = _actions[idx];
                StringBuilder sb = new();
                sb.AppendLine($"{item.Title}");
                sb.AppendLine(new string('=', 60));
                sb.AppendLine($"\r\n{item.Description}\r\n");
                if (item.RequiresAdmin)
                {
                    sb.AppendLine("(Requires Administrator privileges — a UAC prompt may appear.)\r\n");
                }
                if (!string.IsNullOrWhiteSpace(item.LastOutput))
                {
                    sb.AppendLine(new string('-', 60));
                    sb.AppendLine($"Result: {(item.Status == RepairStatus.Done ? "SUCCESS" : "FAILED")}");
                    sb.AppendLine(item.LastOutput);
                }
                _txtDetails.Text = sb.ToString();
            }
        }
    }
}
