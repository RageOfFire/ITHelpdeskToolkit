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
    public class InventoryView : UserControl
    {
        private readonly DataGridView _grid;
        private readonly ModernButton _btnScan;
        private readonly ModernButton _btnExportCsv;
        private readonly ModernButton _btnSaveReport;
        private readonly Label _lblStatus;
        private List<InventoryItem> _inventoryData = new();

        public InventoryView()
        {
            DoubleBuffered = true;
            BackColor = DarkColors.Background;
            Dock = DockStyle.Fill;

            // Header block (title, subtitle, action buttons, status) — fixed
            // height, docked to the top, so the grid below always gets
            // whatever real vertical space remains, regardless of window size.
            Panel header = new()
            {
                Dock = DockStyle.Top,
                Height = 130,
                BackColor = DarkColors.Background
            };
            Controls.Add(header);

            // Title
            Label lblTitle = new()
            {
                Text = "Asset Inventory",
                Font = new Font("Segoe UI", 20F, FontStyle.Bold),
                ForeColor = DarkColors.TextMain,
                AutoSize = true,
                Location = new Point(0, 0)
            };
            header.Controls.Add(lblTitle);

            Label lblSub = new()
            {
                Text = "Collect hardware, Windows OS, storage, and network specifications from this endpoint.",
                Font = new Font("Segoe UI", 10F),
                ForeColor = DarkColors.TextMuted,
                AutoSize = true,
                Location = new Point(4, 38)
            };
            header.Controls.Add(lblSub);

            // Action Buttons Panel
            FlowLayoutPanel actionPanel = new()
            {
                Location = new Point(0, 75),
                Size = new Size(950, 45),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            header.Controls.Add(actionPanel);

            _btnScan = new ModernButton
            {
                Text = "🔄 Scan This PC",
                Style = ButtonStyle.Primary,
                Width = 145,
                Margin = new Padding(0, 0, 10, 0)
            };
            _btnScan.Click += async (s, e) => await RunScanAsync();
            actionPanel.Controls.Add(_btnScan);

            _btnExportCsv = new ModernButton
            {
                Text = "💾 Export CSV",
                Style = ButtonStyle.Secondary,
                Width = 135,
                Margin = new Padding(0, 0, 10, 0)
            };
            _btnExportCsv.Click += (s, e) => ExportCsv();
            actionPanel.Controls.Add(_btnExportCsv);

            _btnSaveReport = new ModernButton
            {
                Text = "📄 Save TXT Report",
                Style = ButtonStyle.Secondary,
                Width = 150
            };
            _btnSaveReport.Click += (s, e) => SaveReport();
            actionPanel.Controls.Add(_btnSaveReport);

            // Status indicator
            _lblStatus = new Label
            {
                Text = "Ready to scan.",
                Font = new Font("Segoe UI", 9.5F, FontStyle.Italic),
                ForeColor = DarkColors.TextMuted,
                AutoSize = true,
                Location = new Point(450, 85)
            };
            header.Controls.Add(_lblStatus);

            // DataGridView Grid — Dock=Fill instead of a fixed Size + Anchor.
            // The old Anchor-based sizing was computed against this
            // UserControl's size at construction time (before it was ever
            // parented into the real, correctly-sized container), so the grid
            // ended up shorter than the actual available area and its own
            // scrollbar never had reason to appear. Dock=Fill always exactly
            // matches whatever space header didn't use, so the grid gets a
            // real internal scrollbar whenever rows overflow it.
            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
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
            _grid.ColumnHeadersHeight = 36;

            _grid.DefaultCellStyle.BackColor = DarkColors.CardBg;
            _grid.DefaultCellStyle.ForeColor = DarkColors.TextMain;
            _grid.DefaultCellStyle.SelectionBackColor = DarkColors.Primary;
            _grid.DefaultCellStyle.SelectionForeColor = Color.White;
            _grid.DefaultCellStyle.Font = new Font("Segoe UI", 9.5F);
            _grid.RowTemplate.Height = 32;

            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Property",
                HeaderText = "Property",
                FillWeight = 30
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Value",
                HeaderText = "Value",
                FillWeight = 70
            });

            Controls.Add(_grid);

            // Auto run scan on load
            Load += async (s, e) => await RunScanAsync();
        }

        private async Task RunScanAsync()
        {
            _btnScan.Enabled = false;
            _lblStatus.Text = "Scanning endpoint hardware and OS...";
            _grid.Rows.Clear();

            try
            {
                _inventoryData = await InventoryService.CollectInventoryAsync();
                foreach (var item in _inventoryData)
                {
                    _grid.Rows.Add(item.Property, item.Value);
                }
                _lblStatus.Text = $"Scan complete — {_inventoryData.Count} properties collected.";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Inventory scan error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                _lblStatus.Text = "Inventory scan failed.";
            }
            finally
            {
                _btnScan.Enabled = true;
            }
        }

        private void ExportCsv()
        {
            if (_inventoryData.Count == 0)
            {
                MessageBox.Show("Run an inventory scan first.", "No Data", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using SaveFileDialog sfd = new()
            {
                Filter = "CSV Files (*.csv)|*.csv",
                FileName = $"{Environment.MachineName}_inventory.csv"
            };

            if (sfd.ShowDialog() == DialogResult.OK)
            {
                StringBuilder sb = new();
                sb.AppendLine("Property,Value");
                foreach (var item in _inventoryData)
                {
                    string safeVal = item.Value.Contains(',') ? $"\"{item.Value}\"" : item.Value;
                    sb.AppendLine($"{item.Property},{safeVal}");
                }
                File.WriteAllText(sfd.FileName, sb.ToString(), Encoding.UTF8);
                MessageBox.Show($"Inventory exported to:\n{sfd.FileName}", "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void SaveReport()
        {
            if (_inventoryData.Count == 0)
            {
                MessageBox.Show("Run an inventory scan first.", "No Data", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using SaveFileDialog sfd = new()
            {
                Filter = "Text Files (*.txt)|*.txt",
                FileName = $"{Environment.MachineName}_inventory.txt"
            };

            if (sfd.ShowDialog() == DialogResult.OK)
            {
                StringBuilder sb = new();
                sb.AppendLine($"IT Helpdesk Toolkit v{AppInfo.Version} — Asset Inventory Report");
                sb.AppendLine(new string('=', 65));
                sb.AppendLine();

                foreach (var item in _inventoryData)
                {
                    sb.AppendLine($"{item.Property, -22}: {item.Value}");
                }

                File.WriteAllText(sfd.FileName, sb.ToString(), Encoding.UTF8);
                MessageBox.Show($"Report saved to:\n{sfd.FileName}", "Report Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
    }
}
