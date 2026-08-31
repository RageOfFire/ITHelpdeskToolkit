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
    public class ExcelView : UserControl
    {
        private readonly DataGridView _grid;
        private readonly TextBox _txtDetails;
        private readonly ModernButton _btnRunAll;
        private readonly ModernButton _btnRunSelected;
        private readonly ModernButton _btnConvertXls;
        private readonly ModernButton _btnSafeMode;
        private readonly List<RepairActionItem> _actions;

        public ExcelView()
        {
            DoubleBuffered = true;
            BackColor = DarkColors.Background;
            Dock = DockStyle.Fill;
            Padding = new Padding(25);

            _actions = ExcelRepairService.GetExcelFixes();

            // Title
            Label lblTitle = new()
            {
                Text = "Excel Troubleshooter",
                Font = new Font("Segoe UI", 20F, FontStyle.Bold),
                ForeColor = DarkColors.TextMain,
                AutoSize = true,
                Location = new Point(0, 0)
            };
            Controls.Add(lblTitle);

            Label lblSub = new()
            {
                Text = "Quick fixes for Excel crashes, slow startup, stuck add-ins, corrupted toolbars, and file conversion.",
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
                Text = "▶ Run All Fixes",
                Style = ButtonStyle.Primary,
                Width = 140,
                Margin = new Padding(0, 0, 10, 0)
            };
            _btnRunAll.Click += async (s, e) => await RunAllFixesAsync();
            actionPanel.Controls.Add(_btnRunAll);

            _btnRunSelected = new ModernButton
            {
                Text = "▶ Run Selected",
                Style = ButtonStyle.Secondary,
                Width = 135,
                Margin = new Padding(0, 0, 10, 0)
            };
            _btnRunSelected.Click += async (s, e) => await RunSelectedFixAsync();
            actionPanel.Controls.Add(_btnRunSelected);

            _btnConvertXls = new ModernButton
            {
                Text = "📄 Convert .xls to .xlsx",
                Style = ButtonStyle.Secondary,
                Width = 180,
                Margin = new Padding(0, 0, 10, 0)
            };
            _btnConvertXls.Click += async (s, e) => await ConvertXlsDialogAsync();
            actionPanel.Controls.Add(_btnConvertXls);

            _btnSafeMode = new ModernButton
            {
                Text = "🛟 Open Safe Mode",
                Style = ButtonStyle.Secondary,
                Width = 155
            };
            _btnSafeMode.Click += (s, e) => OpenSafeMode();
            actionPanel.Controls.Add(_btnSafeMode);

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

            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Fix", HeaderText = "Fix", FillWeight = 70 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Status", HeaderText = "Status", FillWeight = 30 });

            _grid.SelectionChanged += GridSelectionChanged;
            Controls.Add(_grid);

            // Details Log
            Label lblDetails = new()
            {
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

        private async Task RunSelectedFixAsync()
        {
            if (_grid.SelectedRows.Count == 0) return;
            int idx = _grid.SelectedRows[0].Index;
            await ExecuteFixAsync(idx);
        }

        private async Task RunAllFixesAsync()
        {
            DialogResult dr = MessageBox.Show(
                "This will apply all Excel fixes including disabling COM add-ins and resetting toolbars.\r\nPlease close Microsoft Excel before continuing.\r\n\r\nContinue?",
                "Run All Excel Fixes",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question
            );

            if (dr != DialogResult.Yes) return;

            _btnRunAll.Enabled = false;
            _btnRunSelected.Enabled = false;

            for (int i = 0; i < _actions.Count; i++)
            {
                await ExecuteFixAsync(i);
            }

            _btnRunAll.Enabled = true;
            _btnRunSelected.Enabled = true;
        }

        private async Task ExecuteFixAsync(int idx)
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

        private void OpenSafeMode()
        {
            var (success, output) = ExcelRepairService.OpenExcelSafeMode();
            if (!success)
            {
                MessageBox.Show($"Could not launch Excel Safe Mode: {output}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task ConvertXlsDialogAsync()
        {
            using OpenFileDialog ofd = new()
            {
                Title = "Select .xls file(s) to convert to .xlsx",
                Filter = "Excel 97-2003 Workbook (*.xls)|*.xls|All Files (*.*)|*.*",
                Multiselect = true
            };

            if (ofd.ShowDialog() == DialogResult.OK && ofd.FileNames.Length > 0)
            {
                _txtDetails.Text = $"Starting conversion of {ofd.FileNames.Length} .xls file(s)...\r\n\r\n";
                var (ok, summary, details) = await ExcelRepairService.ConvertXlsToXlsxBatchAsync(ofd.FileNames);

                StringBuilder sb = new();
                sb.AppendLine("File Conversion Results");
                sb.AppendLine(new string('=', 60));
                sb.AppendLine($"{summary}\r\n");
                sb.AppendLine(details);
                _txtDetails.Text = sb.ToString();

                MessageBox.Show(summary, ok ? "Conversion Complete" : "Conversion Finished with Issues", MessageBoxButtons.OK, ok ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
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
                    sb.AppendLine("(May require Administrator privileges — a UAC prompt may appear.)\r\n");
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
