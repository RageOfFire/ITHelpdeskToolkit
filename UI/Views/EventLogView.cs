using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using ITHelpdeskToolkit.Models;
using ITHelpdeskToolkit.Services;
using ITHelpdeskToolkit.UI.Controls;
using ITHelpdeskToolkit.UI.Theme;

namespace ITHelpdeskToolkit.UI.Views
{
    public class EventLogView : UserControl
    {
        private readonly DataGridView _appGrid;
        private readonly DataGridView _sysGrid;
        private readonly ModernButton _btnRefresh;
        private readonly Label _lblStatus;

        private List<EventLogEntryInfo> _appLogs = new();
        private List<EventLogEntryInfo> _sysLogs = new();

        public EventLogView()
        {
            DoubleBuffered = true;
            BackColor = DarkColors.Background;
            Dock = DockStyle.Fill;

            // Header block — fixed height, docked to the top, same reasoning as
            // InventoryView: everything below is Dock-based so it always gets
            // whatever real vertical space remains, regardless of window size.
            Panel header = new()
            {
                Dock = DockStyle.Top,
                Height = 90,
                BackColor = DarkColors.Background
            };
            Controls.Add(header);

            Label lblTitle = new()
            {
                BackColor = Color.Transparent,
                Text = "Event Viewer",
                Font = new Font("Segoe UI", 20F, FontStyle.Bold),
                ForeColor = DarkColors.TextMain,
                AutoSize = true,
                Location = new Point(0, 0)
            };
            header.Controls.Add(lblTitle);

            Label lblSub = new()
            {
                BackColor = Color.Transparent,
                Text = "Most recent Windows Application and System log entries, read straight from this endpoint.",
                Font = new Font("Segoe UI", 10F),
                ForeColor = DarkColors.TextMuted,
                AutoSize = true,
                Location = new Point(4, 32)
            };
            header.Controls.Add(lblSub);

            _btnRefresh = new ModernButton
            {
                Text = "🔄 Refresh",
                Style = ButtonStyle.Primary,
                Width = 120,
                Location = new Point(0, 58)
            };
            _btnRefresh.Click += async (s, e) => await RefreshLogsAsync();
            header.Controls.Add(_btnRefresh);

            _lblStatus = new Label
            {
                BackColor = Color.Transparent,
                Text = "Loading...",
                Font = new Font("Segoe UI", 9.5F, FontStyle.Italic),
                ForeColor = DarkColors.TextMuted,
                AutoSize = true,
                Location = new Point(135, 65)
            };
            header.Controls.Add(_lblStatus);

            // ---- Application log section ----
            Label lblAppSection = new()
            {
                Dock = DockStyle.Top,
                Height = 28,
                BackColor = Color.Transparent,
                Text = "📋 Application Log — Top 10 (all levels)",
                Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
                ForeColor = DarkColors.Primary,
                TextAlign = ContentAlignment.BottomLeft
            };
            Controls.Add(lblAppSection);

            _appGrid = BuildLogGrid();
            _appGrid.Dock = DockStyle.Top;
            _appGrid.Height = 240;
            _appGrid.CellDoubleClick += (s, e) => ShowRowDetail(e.RowIndex, _appLogs);
            Controls.Add(_appGrid);

            // ---- System error log section ----
            Label lblSysSection = new()
            {
                Dock = DockStyle.Top,
                Height = 28,
                BackColor = Color.Transparent,
                Text = "⚠ System Log — Top 10 Errors",
                Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
                ForeColor = DarkColors.Primary,
                TextAlign = ContentAlignment.BottomLeft
            };
            Controls.Add(lblSysSection);

            _sysGrid = BuildLogGrid();
            _sysGrid.Dock = DockStyle.Fill;
            _sysGrid.CellDoubleClick += (s, e) => ShowRowDetail(e.RowIndex, _sysLogs);
            Controls.Add(_sysGrid);

            // Dock order note (see InventoryView for the same convention): controls
            // sharing a Dock value stack in the order they were added to Controls,
            // so header -> lblAppSection -> _appGrid -> lblSysSection all claim their
            // strip from the top in that order, and _sysGrid (Dock=Fill) takes
            // whatever space is left at the bottom.

            Load += async (s, e) => await RefreshLogsAsync();
        }

        private static DataGridView BuildLogGrid()
        {
            DataGridView grid = new()
            {
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

            grid.EnableHeadersVisualStyles = false;
            grid.ColumnHeadersDefaultCellStyle.BackColor = DarkColors.Header;
            grid.ColumnHeadersDefaultCellStyle.ForeColor = DarkColors.TextMain;
            grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            grid.ColumnHeadersHeight = 30;

            grid.DefaultCellStyle.BackColor = DarkColors.CardBg;
            grid.DefaultCellStyle.ForeColor = DarkColors.TextMain;
            grid.DefaultCellStyle.SelectionBackColor = DarkColors.Primary;
            grid.DefaultCellStyle.SelectionForeColor = Color.White;
            grid.DefaultCellStyle.Font = new Font("Segoe UI", 9.5F);
            grid.RowTemplate.Height = 26;

            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Time", HeaderText = "Time", FillWeight = 16 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Level", HeaderText = "Level", FillWeight = 10 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Source", HeaderText = "Source", FillWeight = 22 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "EventId", HeaderText = "Event ID", FillWeight = 8 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Message", HeaderText = "Message", FillWeight = 44 });

            return grid;
        }

        private async Task RefreshLogsAsync()
        {
            _btnRefresh.Enabled = false;
            _lblStatus.Text = "Reading Application and System event logs...";
            _appGrid.Rows.Clear();
            _sysGrid.Rows.Clear();

            try
            {
                _appLogs = await EventLogService.GetTopApplicationLogsAsync(10);
                foreach (EventLogEntryInfo entry in _appLogs)
                {
                    _appGrid.Rows.Add(FormatTime(entry), entry.Level, entry.Source, entry.EventId, Truncate(entry.Message));
                }

                _sysLogs = await EventLogService.GetTopSystemErrorLogsAsync(10);
                foreach (EventLogEntryInfo entry in _sysLogs)
                {
                    _sysGrid.Rows.Add(FormatTime(entry), entry.Level, entry.Source, entry.EventId, Truncate(entry.Message));
                }

                _lblStatus.Text = $"Loaded {_appLogs.Count} Application / {_sysLogs.Count} System error entries at {DateTime.Now:HH:mm:ss}. Double-click a row for full detail.";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unable to read event logs:\r\n\r\n{ex.Message}", "Event Log Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                _lblStatus.Text = "Failed to load event logs.";
            }
            finally
            {
                _btnRefresh.Enabled = true;
            }
        }

        private static void ShowRowDetail(int rowIndex, List<EventLogEntryInfo> source)
        {
            if (rowIndex < 0 || rowIndex >= source.Count) return;

            EventLogEntryInfo entry = source[rowIndex];
            string details =
                $"Log: {entry.LogName}\r\n" +
                $"Time: {FormatTime(entry)}\r\n" +
                $"Level: {entry.Level}\r\n" +
                $"Source: {entry.Source}\r\n" +
                $"Event ID: {entry.EventId}\r\n\r\n" +
                entry.Message;

            MessageBox.Show(details, $"Event {entry.EventId} — {entry.Source}", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private static string FormatTime(EventLogEntryInfo entry) => entry.TimeCreated?.ToString("g") ?? "-";

        private static string Truncate(string text, int maxLen = 150)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            string oneLine = text.Replace("\r\n", " ").Replace("\n", " ").Trim();
            return oneLine.Length <= maxLen ? oneLine : oneLine[..maxLen] + "…";
        }
    }
}
