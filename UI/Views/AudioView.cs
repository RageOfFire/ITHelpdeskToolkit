using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using OrbitDesk.Models;
using OrbitDesk.Services;
using OrbitDesk.UI.Controls;
using OrbitDesk.UI.Theme;

namespace OrbitDesk.UI.Views
{
    public class AudioView : UserControl
    {
        private readonly DataGridView _grid;
        private readonly TextBox _txtDetails;
        private readonly ModernButton _btnRunAll;
        private readonly ModernButton _btnRunSelected;
        private readonly List<RepairActionItem> _actions;

        public AudioView()
        {
            DoubleBuffered = true;
            BackColor = DarkColors.Background;
            Dock = DockStyle.Fill;

            _actions = AudioService.GetAudioRepairActions();

            // Dock-based layout throughout (Dock=Top for everything, Dock=Fill for the details box
            // at the end) - the same approach as EventLogView, not the fixed Location/Size + Anchor
            // pattern that caused the earlier button-clipping bug.

            Panel header = new()
            {
                Dock = DockStyle.Top,
                Height = 82,
                BackColor = DarkColors.Background
            };
            Controls.Add(header);

            Label lblTitle = new()
            {
                BackColor = Color.Transparent,
                Text = "Audio / Microphone Fixes",
                Font = new Font("Segoe UI", 20F, FontStyle.Bold),
                ForeColor = DarkColors.TextMain,
                AutoSize = true,
                Location = new Point(0, 0)
            };
            header.Controls.Add(lblTitle);

            Label lblSub = new()
            {
                BackColor = Color.Transparent,
                Text = "Fix no-sound, no-mic, and \"works in one app but not another\" problems.",
                Font = new Font("Segoe UI", 10F),
                ForeColor = DarkColors.TextMuted,
                AutoSize = true,
                Location = new Point(4, 38)
            };
            header.Controls.Add(lblSub);

            // ---- Run All / Run Selected ----
            FlowLayoutPanel runButtons = new()
            {
                Dock = DockStyle.Top,
                Height = 44,
                WrapContents = false
            };
            Controls.Add(runButtons);

            _btnRunAll = new ModernButton { Text = "▶ Run All Fixes", Style = ButtonStyle.Primary, Width = 150, Margin = new Padding(0, 3, 10, 0) };
            _btnRunAll.Click += async (s, e) => await RunAllFixesAsync();
            runButtons.Controls.Add(_btnRunAll);

            _btnRunSelected = new ModernButton { Text = "▶ Run Selected", Style = ButtonStyle.Secondary, Width = 140, Margin = new Padding(0, 3, 0, 0) };
            _btnRunSelected.Click += async (s, e) => await RunSelectedFixAsync();
            runButtons.Controls.Add(_btnRunSelected);

            // ---- Fixes grid ----
            _grid = new DataGridView
            {
                Dock = DockStyle.Top,
                Height = 200,
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

            // ---- Interactive troubleshooters / quick settings ----
            Label lblQuickSection = new()
            {
                Dock = DockStyle.Top,
                Height = 28,
                BackColor = Color.Transparent,
                Text = "🔧 Interactive Troubleshooters & Settings",
                Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
                ForeColor = DarkColors.Primary,
                TextAlign = ContentAlignment.BottomLeft
            };
            Controls.Add(lblQuickSection);

            FlowLayoutPanel quickButtons = new()
            {
                Dock = DockStyle.Top,
                Height = 44,
                WrapContents = false
            };
            Controls.Add(quickButtons);

            ModernButton btnPlayback = new() { Text = "🎧 Playback Troubleshooter", Width = 195, Margin = new Padding(0, 3, 8, 0) };
            btnPlayback.Click += (s, e) => RunVisibleLaunch(AudioService.OpenPlaybackTroubleshooter);
            quickButtons.Controls.Add(btnPlayback);

            ModernButton btnRecording = new() { Text = "🎙 Recording Troubleshooter", Width = 205, Margin = new Padding(0, 3, 8, 0) };
            btnRecording.Click += (s, e) => RunVisibleLaunch(AudioService.OpenRecordingTroubleshooter);
            quickButtons.Controls.Add(btnRecording);

            ModernButton btnSoundSettings = new() { Text = "⚙ Open Sound Settings", Width = 180, Margin = new Padding(0, 3, 0, 0) };
            btnSoundSettings.Click += (s, e) => RunVisibleLaunch(AudioService.OpenSoundSettings);
            quickButtons.Controls.Add(btnSoundSettings);

            // ---- Details ----
            Label lblDetails = new()
            {
                Dock = DockStyle.Top,
                Height = 28,
                BackColor = Color.Transparent,
                Text = "Details",
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = DarkColors.TextMain,
                TextAlign = ContentAlignment.BottomLeft
            };
            Controls.Add(lblDetails);

            _txtDetails = new TextBox
            {
                Dock = DockStyle.Fill,
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
            foreach (RepairActionItem item in _actions)
            {
                _grid.Rows.Add(item.Title, "NOT RUN");
            }
        }

        private void RunVisibleLaunch(Func<(bool Success, string Output)> action)
        {
            var (success, output) = action();
            _txtDetails.Text = output;
            if (!success)
            {
                MessageBox.Show(output, "Could Not Open", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private async Task RunSelectedFixAsync()
        {
            if (_grid.SelectedRows.Count == 0) return;
            await ExecuteFixAsync(_grid.SelectedRows[0].Index);
        }

        private async Task RunAllFixesAsync()
        {
            DialogResult confirm = MessageBox.Show(
                "This runs all 4 checks/fixes in order, including restarting the audio services (your " +
                "sound will briefly cut out) and re-enabling any disabled audio device. Some steps need " +
                "Administrator.\r\n\r\nContinue?",
                "Run All Audio Fixes",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirm != DialogResult.Yes) return;

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
            RepairActionItem item = _actions[idx];
            DataGridViewRow row = _grid.Rows[idx];

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
            if (idx < 0 || idx >= _actions.Count) return;

            RepairActionItem item = _actions[idx];
            StringBuilder sb = new();
            sb.AppendLine(item.Title);
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
