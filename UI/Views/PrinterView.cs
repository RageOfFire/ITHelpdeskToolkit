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
    public class PrinterView : UserControl
    {
        // Printers tab
        private readonly DataGridView _grid;
        private readonly TextBox _txtDetails;
        private readonly ModernButton _btnRefresh;
        private readonly ModernButton _btnDiagnose;
        private readonly ModernButton _btnClearJobs;
        private readonly ModernButton _btnRestartSpooler;
        private readonly Label _lblSpoolerStatus;
        private List<PrinterInfo> _printers = new();
        private PrinterInfo? _selectedPrinter;

        // Driver Management tab
        private readonly DataGridView _driverGrid;
        private readonly TextBox _txtDriverDetails;
        private readonly ModernButton _btnScanDrivers;
        private readonly ModernButton _btnUninstallDriver;
        private readonly Label _lblDriverStatus;
        private List<PrinterDriverInfo> _drivers = new();
        private PrinterDriverInfo? _selectedDriver;

        public PrinterView()
        {
            DoubleBuffered = true;
            BackColor = DarkColors.Background;
            Dock = DockStyle.Fill;
            Padding = new Padding(25);

            // Title
            Label lblTitle = new()
            {
                BackColor = Color.Transparent,
                Text = "Printer Troubleshooter",
                Font = new Font("Segoe UI", 20F, FontStyle.Bold),
                ForeColor = DarkColors.TextMain,
                AutoSize = true,
                Location = new Point(0, 0)
            };
            Controls.Add(lblTitle);

            Label lblSub = new()
            {
                BackColor = Color.Transparent,
                Text = "Inspect installed printers, print jobs, driver packages, and the Windows Print Spooler service.",
                Font = new Font("Segoe UI", 10F),
                ForeColor = DarkColors.TextMuted,
                AutoSize = true,
                Location = new Point(4, 38)
            };
            Controls.Add(lblSub);

            TabControl tabs = new()
            {
                Location = new Point(0, 70),
                Size = new Size(950, 550),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold)
            };
            Controls.Add(tabs);

            TabPage tabPrinters = new("Printers") { BackColor = DarkColors.Background, Padding = new Padding(10) };
            TabPage tabDrivers = new("Driver Management") { BackColor = DarkColors.Background, Padding = new Padding(10) };
            tabs.TabPages.Add(tabPrinters);
            tabs.TabPages.Add(tabDrivers);

            // ================= Printers Tab =================

            CardPanel spoolerCard = new()
            {
                Location = new Point(0, 0),
                Size = new Size(900, 48),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Padding = new Padding(15, 10, 15, 10)
            };
            tabPrinters.Controls.Add(spoolerCard);

            Label lblSpoolerTitle = new()
            {
                BackColor = Color.Transparent,
                Text = "Print Spooler Service:",
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = DarkColors.TextMuted,
                AutoSize = true,
                Location = new Point(15, 14)
            };
            spoolerCard.Controls.Add(lblSpoolerTitle);

            _lblSpoolerStatus = new Label
            {
                BackColor = Color.Transparent,
                Text = "Checking...",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = DarkColors.Success,
                AutoSize = true,
                Location = new Point(165, 14)
            };
            spoolerCard.Controls.Add(_lblSpoolerStatus);

            FlowLayoutPanel actionPanel = new()
            {
                Location = new Point(0, 57),
                Size = new Size(900, 42),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            tabPrinters.Controls.Add(actionPanel);

            _btnRefresh = new ModernButton
            {
                Text = "🔄 Refresh Printers",
                Style = ButtonStyle.Primary,
                Width = 150,
                Margin = new Padding(0, 0, 10, 0)
            };
            _btnRefresh.Click += async (s, e) => await RefreshPrintersAsync();
            actionPanel.Controls.Add(_btnRefresh);

            _btnDiagnose = new ModernButton
            {
                Text = "▶ Diagnose Selected",
                Style = ButtonStyle.Secondary,
                Width = 165,
                Margin = new Padding(0, 0, 10, 0)
            };
            _btnDiagnose.Click += async (s, e) => await DiagnoseSelectedAsync();
            actionPanel.Controls.Add(_btnDiagnose);

            _btnClearJobs = new ModernButton
            {
                Text = "🧹 Clear Selected Jobs",
                Style = ButtonStyle.Secondary,
                Width = 175,
                Margin = new Padding(0, 0, 10, 0)
            };
            _btnClearJobs.Click += async (s, e) => await ClearSelectedJobsAsync();
            actionPanel.Controls.Add(_btnClearJobs);

            _btnRestartSpooler = new ModernButton
            {
                Text = "🔧 Restart Spooler",
                Style = ButtonStyle.Secondary,
                Width = 150
            };
            _btnRestartSpooler.Click += async (s, e) => await RestartSpoolerAsync();
            actionPanel.Controls.Add(_btnRestartSpooler);

            _grid = new DataGridView
            {
                Location = new Point(0, 107),
                Size = new Size(900, 210),
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

            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Name", HeaderText = "Printer Name", FillWeight = 30 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Default", HeaderText = "Default", FillWeight = 10 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Status", HeaderText = "Status", FillWeight = 15 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Driver", HeaderText = "Driver", FillWeight = 25 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Port", HeaderText = "Port", FillWeight = 20 });

            _grid.SelectionChanged += GridSelectionChanged;
            tabPrinters.Controls.Add(_grid);

            Label lblDetails = new()
            {
                BackColor = Color.Transparent,
                Text = "Troubleshooting Details",
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = DarkColors.TextMain,
                AutoSize = true,
                Location = new Point(0, 327)
            };
            tabPrinters.Controls.Add(lblDetails);

            _txtDetails = new TextBox
            {
                Location = new Point(0, 355),
                Size = new Size(900, 155),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                ReadOnly = true,
                BackColor = DarkColors.InputBg,
                ForeColor = DarkColors.TextMain,
                Font = new Font("Consolas", 9.5F),
                BorderStyle = BorderStyle.FixedSingle
            };
            tabPrinters.Controls.Add(_txtDetails);

            // ================= Driver Management Tab =================

            Label lblDriverIntro = new()
            {
                BackColor = Color.Transparent,
                Text = "Scans every printer driver package installed on this machine (Get-PrinterDriver) and lets you uninstall ones you no longer need.",
                Font = new Font("Segoe UI", 9.5F),
                ForeColor = DarkColors.TextMuted,
                AutoSize = true,
                MaximumSize = new Size(880, 0),
                Location = new Point(0, 0)
            };
            tabDrivers.Controls.Add(lblDriverIntro);

            FlowLayoutPanel driverActionPanel = new()
            {
                Location = new Point(0, 32),
                Size = new Size(900, 42),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            tabDrivers.Controls.Add(driverActionPanel);

            _btnScanDrivers = new ModernButton
            {
                Text = "🔍 Scan Installed Drivers",
                Style = ButtonStyle.Primary,
                Width = 190,
                Margin = new Padding(0, 0, 10, 0)
            };
            _btnScanDrivers.Click += async (s, e) => await ScanDriversAsync();
            driverActionPanel.Controls.Add(_btnScanDrivers);

            _btnUninstallDriver = new ModernButton
            {
                Text = "🗑 Uninstall Selected Driver",
                Style = ButtonStyle.Danger,
                Width = 205
            };
            _btnUninstallDriver.Click += async (s, e) => await UninstallSelectedDriverAsync();
            driverActionPanel.Controls.Add(_btnUninstallDriver);

            _lblDriverStatus = new Label
            {
                BackColor = Color.Transparent,
                Text = "Ready to scan.",
                Font = new Font("Segoe UI", 9.5F, FontStyle.Italic),
                ForeColor = DarkColors.TextMuted,
                AutoSize = true,
                Location = new Point(0, 79)
            };
            tabDrivers.Controls.Add(_lblDriverStatus);

            _driverGrid = new DataGridView
            {
                Location = new Point(0, 107),
                Size = new Size(900, 230),
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

            _driverGrid.EnableHeadersVisualStyles = false;
            _driverGrid.ColumnHeadersDefaultCellStyle.BackColor = DarkColors.Header;
            _driverGrid.ColumnHeadersDefaultCellStyle.ForeColor = DarkColors.TextMain;
            _driverGrid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            _driverGrid.ColumnHeadersHeight = 34;

            _driverGrid.DefaultCellStyle.BackColor = DarkColors.CardBg;
            _driverGrid.DefaultCellStyle.ForeColor = DarkColors.TextMain;
            _driverGrid.DefaultCellStyle.SelectionBackColor = DarkColors.Primary;
            _driverGrid.DefaultCellStyle.SelectionForeColor = Color.White;
            _driverGrid.DefaultCellStyle.Font = new Font("Segoe UI", 9.5F);
            _driverGrid.RowTemplate.Height = 30;

            _driverGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Name", HeaderText = "Driver Name", FillWeight = 35 });
            _driverGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Manufacturer", HeaderText = "Manufacturer", FillWeight = 20 });
            _driverGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Version", HeaderText = "Version", FillWeight = 15 });
            _driverGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "InUse", HeaderText = "In Use", FillWeight = 10 });
            _driverGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Path", HeaderText = "INF Path", FillWeight = 20 });

            _driverGrid.SelectionChanged += DriverGridSelectionChanged;
            tabDrivers.Controls.Add(_driverGrid);

            Label lblDriverDetails = new()
            {
                BackColor = Color.Transparent,
                Text = "Driver Details",
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = DarkColors.TextMain,
                AutoSize = true,
                Location = new Point(0, 347)
            };
            tabDrivers.Controls.Add(lblDriverDetails);

            _txtDriverDetails = new TextBox
            {
                Location = new Point(0, 375),
                Size = new Size(900, 135),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                ReadOnly = true,
                BackColor = DarkColors.InputBg,
                ForeColor = DarkColors.TextMain,
                Font = new Font("Consolas", 9.5F),
                BorderStyle = BorderStyle.FixedSingle,
                Text = "Select 'Scan Installed Drivers' to list every printer driver package on this machine."
            };
            tabDrivers.Controls.Add(_txtDriverDetails);

            Load += async (s, e) => await RefreshPrintersAsync();
        }

        private async Task RefreshPrintersAsync()
        {
            _btnRefresh.Enabled = false;
            _lblSpoolerStatus.Text = PrinterService.GetSpoolerStatus();
            _grid.Rows.Clear();

            try
            {
                _printers = await PrinterService.GetPrintersAsync();
                foreach (var p in _printers)
                {
                    _grid.Rows.Add(p.Name, p.IsDefault ? "Yes" : "No", p.Status, p.Driver, p.Port);
                }

                if (_printers.Count == 0)
                {
                    _txtDetails.Text = "No printers were returned by Windows.\r\n\r\nCheck Settings > Bluetooth & devices > Printers & scanners.";
                }
            }
            catch (Exception ex)
            {
                _txtDetails.Text = $"Failed to refresh printers: {ex.Message}";
            }
            finally
            {
                _btnRefresh.Enabled = true;
            }
        }

        private void GridSelectionChanged(object? sender, EventArgs e)
        {
            if (_grid.SelectedRows.Count == 0)
            {
                _selectedPrinter = null;
                return;
            }

            int idx = _grid.SelectedRows[0].Index;
            if (idx >= 0 && idx < _printers.Count)
            {
                _selectedPrinter = _printers[idx];
                StringBuilder sb = new();
                sb.AppendLine($"PRINTER: {_selectedPrinter.Name}");
                sb.AppendLine(new string('=', 65));
                sb.AppendLine($"\r\nDefault: {(_selectedPrinter.IsDefault ? "Yes" : "No")}");
                sb.AppendLine($"Status:  {_selectedPrinter.Status}");
                sb.AppendLine($"Driver:  {_selectedPrinter.Driver}");
                sb.AppendLine($"Port:    {_selectedPrinter.Port}\r\n");
                sb.AppendLine("Select 'Diagnose Selected' for port ping testing and print job queries.");
                _txtDetails.Text = sb.ToString();
            }
        }

        private async Task DiagnoseSelectedAsync()
        {
            if (_selectedPrinter == null)
            {
                MessageBox.Show("Select a printer first.", "Select Printer", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            _txtDetails.Text = $"Running diagnostics for {_selectedPrinter.Name}...\r\n\r\n";
            string spooler = PrinterService.GetSpoolerStatus();
            var (connOk, connDetail) = await PrinterService.CheckPrinterConnectivityAsync(_selectedPrinter.Port);
            string jobs = await PrinterService.GetPrintJobsAsync(_selectedPrinter.Name);

            StringBuilder sb = new();
            sb.AppendLine($"PRINTER DIAGNOSTIC: {_selectedPrinter.Name}");
            sb.AppendLine(new string('=', 70));
            sb.AppendLine($"\r\nSpooler Status: {spooler}");
            sb.AppendLine($"Printer Status: {_selectedPrinter.Status}");
            sb.AppendLine($"Default:        {(_selectedPrinter.IsDefault ? "Yes" : "No")}");
            sb.AppendLine($"Driver:         {_selectedPrinter.Driver}");
            sb.AppendLine($"Port:           {_selectedPrinter.Port}\r\n");

            sb.AppendLine($"Connectivity Check: {(connOk ? "PASS" : "FAIL")}");
            sb.AppendLine($"{connDetail}\r\n");

            sb.AppendLine("Print Job Query:");
            sb.AppendLine(!string.IsNullOrWhiteSpace(jobs) ? jobs : "No print jobs queued or query returned empty.");
            sb.AppendLine();

            if (!spooler.Equals("Running", StringComparison.OrdinalIgnoreCase))
            {
                sb.AppendLine("RECOMMENDATION: Restart the Print Spooler service.");
            }
            else if (!connOk)
            {
                sb.AppendLine("RECOMMENDATION: Check printer power, cable, IP address, and firewall.");
            }
            else if (!string.IsNullOrWhiteSpace(jobs))
            {
                sb.AppendLine("RECOMMENDATION: Clear stuck print jobs.");
            }
            else
            {
                sb.AppendLine("RESULT: No obvious issue detected by automated checks.");
            }

            _txtDetails.Text = sb.ToString();
        }

        private async Task ClearSelectedJobsAsync()
        {
            if (_selectedPrinter == null)
            {
                MessageBox.Show("Select a printer first.", "Select Printer", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            DialogResult dr = MessageBox.Show(
                $"Delete all queued print jobs for:\r\n\r\n{_selectedPrinter.Name}\r\n\r\nContinue?",
                "Clear Print Jobs",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question
            );

            if (dr == DialogResult.Yes)
            {
                var (success, output) = await PrinterService.ClearSelectedJobsAsync(_selectedPrinter.Name);
                if (success)
                {
                    MessageBox.Show("Queued print jobs cleared.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show($"Unable to clear print jobs:\r\n\r\n{output}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private async Task RestartSpoolerAsync()
        {
            DialogResult dr = MessageBox.Show(
                "Restart the Windows Print Spooler service?\r\nActive print jobs may be interrupted.",
                "Restart Print Spooler",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning
            );

            if (dr == DialogResult.Yes)
            {
                _btnRestartSpooler.Enabled = false;
                var (success, output) = await PrinterService.RestartSpoolerAsync();
                _lblSpoolerStatus.Text = PrinterService.GetSpoolerStatus();
                _btnRestartSpooler.Enabled = true;

                if (success)
                {
                    MessageBox.Show("Print Spooler restarted successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show($"Could not restart Print Spooler:\r\n\r\n{output}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        // ================= Driver Management =================

        private async Task ScanDriversAsync()
        {
            _btnScanDrivers.Enabled = false;
            _lblDriverStatus.Text = "Scanning installed printer drivers...";
            _driverGrid.Rows.Clear();
            _selectedDriver = null;

            try
            {
                _drivers = await PrinterService.GetPrinterDriversAsync();
                foreach (var d in _drivers)
                {
                    _driverGrid.Rows.Add(d.Name, d.Manufacturer, d.Version, d.InUse ? "Yes" : "No", d.DriverPath);
                }

                _lblDriverStatus.Text = _drivers.Count == 0
                    ? "No printer driver packages were returned by Windows."
                    : $"Scan complete — {_drivers.Count} driver package(s) found.";
            }
            catch (Exception ex)
            {
                _lblDriverStatus.Text = "Driver scan failed.";
                _txtDriverDetails.Text = $"Failed to scan printer drivers: {ex.Message}";
            }
            finally
            {
                _btnScanDrivers.Enabled = true;
            }
        }

        private void DriverGridSelectionChanged(object? sender, EventArgs e)
        {
            if (_driverGrid.SelectedRows.Count == 0)
            {
                _selectedDriver = null;
                return;
            }

            int idx = _driverGrid.SelectedRows[0].Index;
            if (idx >= 0 && idx < _drivers.Count)
            {
                _selectedDriver = _drivers[idx];
                StringBuilder sb = new();
                sb.AppendLine($"DRIVER: {_selectedDriver.Name}");
                sb.AppendLine(new string('=', 65));
                sb.AppendLine($"\r\nManufacturer: {_selectedDriver.Manufacturer}");
                sb.AppendLine($"Version:      {_selectedDriver.Version}");
                sb.AppendLine($"INF Path:     {_selectedDriver.DriverPath}");
                sb.AppendLine($"In Use:       {(_selectedDriver.InUse ? "Yes — bound to an installed printer" : "No")}\r\n");

                sb.AppendLine(_selectedDriver.InUse
                    ? "WARNING: This driver is currently bound to an installed printer. Uninstalling it may break that printer until a new driver is installed."
                    : "This driver does not appear to be bound to any currently installed printer.");

                _txtDriverDetails.Text = sb.ToString();
            }
        }

        private async Task UninstallSelectedDriverAsync()
        {
            if (_selectedDriver == null)
            {
                MessageBox.Show("Select a printer driver first.", "Select Driver", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string warning = _selectedDriver.InUse
                ? $"'{_selectedDriver.Name}' is currently bound to an installed printer.\r\n\r\nUninstalling it may break that printer until a replacement driver is installed.\r\n\r\nUninstall anyway?"
                : $"Uninstall printer driver package:\r\n\r\n{_selectedDriver.Name}\r\n\r\nThis requires administrator rights and may prompt for elevation. Continue?";

            DialogResult dr = MessageBox.Show(
                warning,
                "Uninstall Printer Driver",
                MessageBoxButtons.YesNo,
                _selectedDriver.InUse ? MessageBoxIcon.Warning : MessageBoxIcon.Question
            );

            if (dr != DialogResult.Yes) return;

            _btnUninstallDriver.Enabled = false;
            _lblDriverStatus.Text = $"Uninstalling {_selectedDriver.Name}...";

            var (success, output) = await PrinterService.UninstallPrinterDriverAsync(_selectedDriver.Name);

            _btnUninstallDriver.Enabled = true;

            if (success)
            {
                MessageBox.Show("Printer driver uninstalled successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                await ScanDriversAsync();
            }
            else
            {
                _lblDriverStatus.Text = "Driver uninstall failed.";
                MessageBox.Show($"Unable to uninstall printer driver:\r\n\r\n{output}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
