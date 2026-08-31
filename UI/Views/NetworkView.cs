using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using ITHelpdeskToolkit.Models;
using ITHelpdeskToolkit.Services;
using ITHelpdeskToolkit.UI.Controls;
using ITHelpdeskToolkit.UI.Theme;

namespace ITHelpdeskToolkit.UI.Views
{
    public class NetworkView : UserControl
    {
        private readonly DataGridView _grid;
        private readonly TextBox _txtDetails;
        private readonly ModernButton _btnRunAll;
        private readonly ModernButton _btnExport;
        private readonly NumericUpDown _numMaxHops;
        private readonly Dictionary<string, NetworkDiagnosticResult> _results = new();

        public NetworkView()
        {
            DoubleBuffered = true;
            BackColor = DarkColors.Background;
            Dock = DockStyle.Fill;
            Padding = new Padding(25);

            // Title
            Label lblTitle = new()
            {
                Text = "Network Diagnostics",
                Font = new Font("Segoe UI", 20F, FontStyle.Bold),
                ForeColor = DarkColors.TextMain,
                AutoSize = true,
                Location = new Point(0, 0)
            };
            Controls.Add(lblTitle);

            Label lblSub = new()
            {
                Text = "Run network connectivity tests and identify root cause connectivity breakdown.",
                Font = new Font("Segoe UI", 10F),
                ForeColor = DarkColors.TextMuted,
                AutoSize = true,
                Location = new Point(4, 38)
            };
            Controls.Add(lblSub);

            // Controls Toolbar
            FlowLayoutPanel toolBar = new()
            {
                Location = new Point(0, 75),
                Size = new Size(950, 45),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            Controls.Add(toolBar);

            _btnRunAll = new ModernButton
            {
                Text = "▶ RUN ALL CHECKS",
                Style = ButtonStyle.Primary,
                Width = 165,
                Margin = new Padding(0, 0, 15, 0)
            };
            _btnRunAll.Click += async (s, e) => await RunAllChecksAsync();
            toolBar.Controls.Add(_btnRunAll);

            _btnExport = new ModernButton
            {
                Text = "💾 Export Report",
                Style = ButtonStyle.Secondary,
                Width = 140,
                Margin = new Padding(0, 0, 20, 0)
            };
            _btnExport.Click += (s, e) => ExportReport();
            toolBar.Controls.Add(_btnExport);

            Label lblHops = new()
            {
                Text = "Tracert Hops:",
                Font = new Font("Segoe UI", 9.5F),
                ForeColor = DarkColors.TextMain,
                AutoSize = true,
                Margin = new Padding(0, 8, 5, 0)
            };
            toolBar.Controls.Add(lblHops);

            _numMaxHops = new NumericUpDown
            {
                Minimum = 1,
                Maximum = 255,
                Value = 30,
                Width = 60,
                Margin = new Padding(0, 5, 0, 0),
                BackColor = DarkColors.InputBg,
                ForeColor = DarkColors.TextMain
            };
            toolBar.Controls.Add(_numMaxHops);

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

            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "TestName", HeaderText = "Network Test", FillWeight = 70 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Status", HeaderText = "Status", FillWeight = 30 });

            _grid.SelectionChanged += GridSelectionChanged;
            Controls.Add(_grid);

            // Details Label
            Label lblDetails = new()
            {
                Text = "Diagnostic Details",
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = DarkColors.TextMain,
                AutoSize = true,
                Location = new Point(0, 382)
            };
            Controls.Add(lblDetails);

            // Details Log Box
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

            PopulateDefaultRows();
        }

        private void PopulateDefaultRows()
        {
            string[] testNames = { "Network Adapter", "Default Gateway", "Internet (8.8.8.8)", "DNS", "Internet + DNS", "Route", "TCP DNS" };
            foreach (string name in testNames)
            {
                _grid.Rows.Add(name, "NOT TESTED");
            }
        }

        private async Task RunAllChecksAsync()
        {
            _btnRunAll.Enabled = false;
            _results.Clear();
            _txtDetails.Text = "Running network diagnostics...\r\n\r\n";

            foreach (DataGridViewRow row in _grid.Rows)
            {
                row.Cells["Status"].Value = "TESTING...";
                row.DefaultCellStyle.ForeColor = DarkColors.TextMuted;
            }

            int hops = (int)_numMaxHops.Value;

            var tests = new Func<Task<NetworkDiagnosticResult>>[]
            {
                () => NetworkService.CheckAdapterAsync(),
                () => NetworkService.CheckGatewayAsync(),
                () => NetworkService.CheckInternetAsync(),
                () => NetworkService.CheckDnsAsync(),
                () => NetworkService.CheckGoogleAsync(),
                () => NetworkService.CheckRouteAsync(hops),
                () => NetworkService.CheckTcpDnsAsync()
            };

            for (int i = 0; i < tests.Length; i++)
            {
                var res = await tests[i]();
                _results[res.TestName] = res;

                if (i < _grid.Rows.Count)
                {
                    var row = _grid.Rows[i];
                    row.Cells["Status"].Value = res.Success ? "✓ WORKING" : "✗ FAILED";
                    row.DefaultCellStyle.ForeColor = res.Success ? DarkColors.Success : DarkColors.Danger;
                }
            }

            _btnRunAll.Enabled = true;
            DisplaySummary();
        }

        private void DisplaySummary()
        {
            int working = 0;
            int failed = 0;
            StringBuilder sb = new();
            sb.AppendLine("NETWORK DIAGNOSTIC COMPLETE");
            sb.AppendLine(new string('=', 50));

            foreach (var kvp in _results)
            {
                if (kvp.Value.Success) working++;
                else failed++;
            }

            sb.AppendLine($"\r\nWorking: {working}");
            sb.AppendLine($"Failed:  {failed}\r\n");

            if (failed > 0)
            {
                sb.AppendLine("LIKELY PROBLEMS");
                sb.AppendLine(new string('-', 50));
                foreach (var kvp in _results)
                {
                    if (!kvp.Value.Success)
                    {
                        sb.AppendLine($"• {kvp.Key}: {kvp.Value.LikelyProblem}");
                    }
                }
            }
            else
            {
                sb.AppendLine("✓ All network checks passed successfully.");
            }

            _txtDetails.Text = sb.ToString();
        }

        private void GridSelectionChanged(object? sender, EventArgs e)
        {
            if (_grid.SelectedRows.Count == 0) return;
            string testName = _grid.SelectedRows[0].Cells["TestName"].Value?.ToString() ?? "";
            if (_results.TryGetValue(testName, out var res))
            {
                StringBuilder sb = new();
                sb.AppendLine($"TEST: {res.TestName}");
                sb.AppendLine(new string('=', 60));
                sb.AppendLine($"\r\nStatus: {(res.Success ? "WORKING" : "FAILED")}\r\n");
                sb.AppendLine($"Likely problem:\r\n{res.LikelyProblem}\r\n");
                sb.AppendLine("Command output:");
                sb.AppendLine(new string('-', 60));
                sb.AppendLine(res.Output);
                _txtDetails.Text = sb.ToString();
            }
        }

        private void ExportReport()
        {
            if (_results.Count == 0)
            {
                MessageBox.Show("Run network checks first.", "No Data", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using SaveFileDialog sfd = new()
            {
                Filter = "Text Files (*.txt)|*.txt",
                FileName = $"network_report_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
            };

            if (sfd.ShowDialog() == DialogResult.OK)
            {
                StringBuilder sb = new();
                sb.AppendLine("IT Helpdesk Toolkit — Network Diagnostic Report");
                sb.AppendLine(new string('=', 65));
                sb.AppendLine($"Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\r\n");

                foreach (var kvp in _results)
                {
                    sb.AppendLine($"{kvp.Key}: {(kvp.Value.Success ? "WORKING" : "FAILED")}");
                    sb.AppendLine($"Likely problem: {kvp.Value.LikelyProblem}");
                    sb.AppendLine("Output:");
                    sb.AppendLine(kvp.Value.Output);
                    sb.AppendLine(new string('-', 65));
                }

                File.WriteAllText(sfd.FileName, sb.ToString(), Encoding.UTF8);
                MessageBox.Show($"Report saved to:\n{sfd.FileName}", "Report Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
    }
}
