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
    public class SystemRepairView : UserControl
    {
        private readonly DataGridView _grid;
        private readonly TextBox _txtDetails;
        private readonly ModernButton _btnRunAll;
        private readonly ModernButton _btnRunSelected;
        private readonly List<RepairActionItem> _actions;

        public SystemRepairView()
        {
            DoubleBuffered = true;
            BackColor = DarkColors.Background;
            Dock = DockStyle.Fill;
            Padding = new Padding(25);

            _actions = SystemRepairService.GetSystemRepairActions();

            // Title
            Label lblTitle = new()
            {
                BackColor = Color.Transparent,
                Text = "Windows / File System Repair",
                Font = new Font("Segoe UI", 20F, FontStyle.Bold),
                ForeColor = DarkColors.TextMain,
                AutoSize = true,
                Location = new Point(0, 0)
            };
            Controls.Add(lblTitle);

            Label lblSub = new()
            {
                BackColor = Color.Transparent,
                Text = "Repair corrupted system files, Windows component store, and NTFS file system errors.",
                Font = new Font("Segoe UI", 10F),
                ForeColor = DarkColors.TextMuted,
                AutoSize = true,
                Location = new Point(4, 38)
            };
            Controls.Add(lblSub);

            // Action Buttons
            FlowLayoutPanel actionPanel = new()
            {
                Location = new Point(0, 75),
                Size = new Size(950, 45),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            Controls.Add(actionPanel);

            _btnRunAll = new ModernButton
            {
                Text = "▶ Run All Repairs",
                Style = ButtonStyle.Primary,
                Width = 150,
                Margin = new Padding(0, 0, 10, 0)
            };
            _btnRunAll.Click += async (s, e) => await RunAllRepairsAsync();
            actionPanel.Controls.Add(_btnRunAll);

            _btnRunSelected = new ModernButton
            {
                Text = "▶ Run Selected",
                Style = ButtonStyle.Secondary,
                Width = 140
            };
            _btnRunSelected.Click += async (s, e) => await RunSelectedRepairAsync();
            actionPanel.Controls.Add(_btnRunSelected);

            // Table Grid
            _grid = new DataGridView
            {
                Location = new Point(0, 130),
                Size = new Size(950, 240),
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

            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Repair", HeaderText = "Repair", FillWeight = 70 });
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
                Location = new Point(0, 382)
            };
            Controls.Add(lblDetails);

            _txtDetails = new TextBox
            {
                Location = new Point(0, 410),
                Size = new Size(950, 210),
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
        }

        private void PopulateGrid()
        {
            _grid.Rows.Clear();
            foreach (var item in _actions)
            {
                _grid.Rows.Add(item.Title, "NOT RUN");
            }
        }

        private async Task RunSelectedRepairAsync()
        {
            if (_grid.SelectedRows.Count == 0) return;
            int idx = _grid.SelectedRows[0].Index;
            await ExecuteRepairAsync(idx);
        }

        private async Task RunAllRepairsAsync()
        {
            DialogResult dr = MessageBox.Show(
                "Running SFC, DISM, and CHKDSK in sequence can take 15-30+ minutes.\r\n" +
                "Some repair operations will require Administrator privileges.\r\n\r\nContinue?",
                "Run All System Repairs",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question
            );

            if (dr != DialogResult.Yes) return;

            _btnRunAll.Enabled = false;
            _btnRunSelected.Enabled = false;

            for (int i = 0; i < _actions.Count; i++)
            {
                await ExecuteRepairAsync(i);
            }

            _btnRunAll.Enabled = true;
            _btnRunSelected.Enabled = true;
        }

        private async Task ExecuteRepairAsync(int idx)
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
