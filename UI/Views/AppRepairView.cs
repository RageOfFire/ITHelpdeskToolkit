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
    public class AppRepairView : UserControl
    {
        private readonly DataGridView _grid;
        private readonly TextBox _txtDetails;
        private readonly TextBox _txtTargetApp;
        private readonly ModernButton _btnRunAll;
        private readonly ModernButton _btnRunSelected;
        private readonly List<RepairActionItem> _actions;

        public AppRepairView()
        {
            DoubleBuffered = true;
            BackColor = DarkColors.Background;
            Dock = DockStyle.Fill;
            Padding = new Padding(25);

            _actions = AppRepairService.GetGeneralAppFixes();

            // Title Header
            Label lblTitle = new()
            {
                BackColor = Color.Transparent,
                Text = "Application Fixes",
                Font = new Font("Segoe UI", 20F, FontStyle.Bold),
                ForeColor = DarkColors.TextMain,
                AutoSize = true,
                Location = new Point(0, 0)
            };
            Controls.Add(lblTitle);

            Label lblSub = new()
            {
                BackColor = Color.Transparent,
                Text = "Fix system-wide Windows app issues, or target one specific app by process/app name.",
                Font = new Font("Segoe UI", 10F),
                ForeColor = DarkColors.TextMuted,
                AutoSize = true,
                Location = new Point(4, 38)
            };
            Controls.Add(lblSub);

            // General Fixes Section Title
            Label lblGeneral = new()
            {
                BackColor = Color.Transparent,
                Text = "General Windows Fixes",
                Font = new Font("Segoe UI", 11.5F, FontStyle.Bold),
                ForeColor = DarkColors.TextMain,
                AutoSize = true,
                Location = new Point(0, 70)
            };
            Controls.Add(lblGeneral);

            // Toolbar
            FlowLayoutPanel toolBar = new()
            {
                Location = new Point(0, 98),
                Size = new Size(950, 40),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            Controls.Add(toolBar);

            _btnRunAll = new ModernButton
            {
                Text = "▶ Run All Fixes",
                Style = ButtonStyle.Primary,
                Width = 140,
                Margin = new Padding(0, 0, 10, 0)
            };
            _btnRunAll.Click += async (s, e) => await RunAllFixesAsync();
            toolBar.Controls.Add(_btnRunAll);

            _btnRunSelected = new ModernButton
            {
                Text = "▶ Run Selected",
                Style = ButtonStyle.Secondary,
                Width = 135
            };
            _btnRunSelected.Click += async (s, e) => await RunSelectedFixAsync();
            toolBar.Controls.Add(_btnRunSelected);

            // Table Grid
            _grid = new DataGridView
            {
                Location = new Point(0, 145),
                Size = new Size(950, 170),
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
            _grid.ColumnHeadersHeight = 32;

            _grid.DefaultCellStyle.BackColor = DarkColors.CardBg;
            _grid.DefaultCellStyle.ForeColor = DarkColors.TextMain;
            _grid.DefaultCellStyle.SelectionBackColor = DarkColors.Primary;
            _grid.DefaultCellStyle.SelectionForeColor = Color.White;
            _grid.DefaultCellStyle.Font = new Font("Segoe UI", 9.5F);
            _grid.RowTemplate.Height = 28;

            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Fix", HeaderText = "Fix", FillWeight = 70 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Status", HeaderText = "Status", FillWeight = 30 });

            _grid.SelectionChanged += GridSelectionChanged;
            Controls.Add(_grid);

            // Target Specific App Section
            CardPanel targetCard = new()
            {
                Location = new Point(0, 325),
                Size = new Size(950, 90),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Padding = new Padding(15, 10, 15, 10)
            };
            Controls.Add(targetCard);

            Label lblTargetTitle = new()
            {
                BackColor = Color.Transparent,
                Text = "Target a Specific Application",
                Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
                ForeColor = DarkColors.Primary,
                AutoSize = true,
                Location = new Point(12, 10)
            };
            targetCard.Controls.Add(lblTargetTitle);

            _txtTargetApp = new TextBox
            {
                Location = new Point(15, 40),
                Size = new Size(220, 26),
                Font = new Font("Segoe UI", 10F),
                BackColor = DarkColors.InputBg,
                ForeColor = DarkColors.TextMain,
                BorderStyle = BorderStyle.FixedSingle,
                PlaceholderText = "e.g. chrome.exe or Spotify"
            };
            targetCard.Controls.Add(_txtTargetApp);

            FlowLayoutPanel targetButtons = new()
            {
                Location = new Point(245, 36),
                Size = new Size(690, 40)
            };
            targetCard.Controls.Add(targetButtons);

            ModernButton btnForceClose = new() { Text = "🛑 Force Close", Width = 125, Margin = new Padding(0, 0, 8, 0) };
            btnForceClose.Click += async (s, e) => await ForceCloseAppAsync();
            targetButtons.Controls.Add(btnForceClose);

            ModernButton btnClearCache = new() { Text = "🧹 Clear Cache", Width = 125, Margin = new Padding(0, 0, 8, 0) };
            btnClearCache.Click += async (s, e) => await ClearAppCacheAsync();
            targetButtons.Controls.Add(btnClearCache);

            ModernButton btnOpenFolder = new() { Text = "📂 Open Data Folder", Width = 155, Margin = new Padding(0, 0, 8, 0) };
            btnOpenFolder.Click += (s, e) => OpenDataFolder();
            targetButtons.Controls.Add(btnOpenFolder);

            ModernButton btnFolderFix = new() { Text = "📁 Allow Without Admin (Folder Fix)", Width = 260, Margin = new Padding(0, 0, 8, 0) };
            btnFolderFix.Click += async (s, e) => await AllowAppToRunWithoutAdminAsync();
            targetButtons.Controls.Add(btnFolderFix);

            ModernButton btnGrantPermission = new() { Text = "🔑 Grant User Permission...", Width = 200, Margin = new Padding(0, 0, 8, 0) };
            btnGrantPermission.Click += async (s, e) => await GrantUserPermissionAsync();
            targetButtons.Controls.Add(btnGrantPermission);

            ModernButton btnRevokePermission = new() { Text = "🚫 Revoke Permission...", Width = 170 };
            btnRevokePermission.Click += async (s, e) => await RevokeUserPermissionAsync();
            targetButtons.Controls.Add(btnRevokePermission);

            // Details Log Console
            Label lblDetails = new()
            {
                BackColor = Color.Transparent,
                Text = "Details",
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = DarkColors.TextMain,
                AutoSize = true,
                Location = new Point(0, 425)
            };
            Controls.Add(lblDetails);

            _txtDetails = new TextBox
            {
                Location = new Point(0, 452),
                Size = new Size(950, 170),
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
                "This will restart Explorer, reset Store apps, and restart search services.\r\nSave any open work first.\r\n\r\nContinue?",
                "Run All App Fixes",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning
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

        private async Task ForceCloseAppAsync()
        {
            string name = _txtTargetApp.Text.Trim();
            var (success, output) = await AppRepairService.ForceCloseAppAsync(name);
            ShowTargetResult($"Force Close: {name}", success, output);
        }

        private async Task ClearAppCacheAsync()
        {
            string name = _txtTargetApp.Text.Trim();
            var (success, output) = await AppRepairService.ClearAppCacheAsync(name);
            ShowTargetResult($"Clear Cache: {name}", success, output);
        }

        private void OpenDataFolder()
        {
            string name = _txtTargetApp.Text.Trim();
            var (success, output) = AppRepairService.OpenAppDataFolder(name);
            ShowTargetResult($"Open Data Folder: {name}", success, output);
        }

        private async Task AllowAppToRunWithoutAdminAsync()
        {
            if (!ShellService.IsAdmin())
            {
                MessageBox.Show(
                    "This tool needs to be running as Administrator to change folder permissions (one-time setup).\r\n\r\nRestart it with 'Run as administrator' and try again.",
                    "Administrator Required",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            using OpenFileDialog ofd = new()
            {
                Title = "Choose the application (.exe) that shouldn't need admin to run",
                Filter = "Applications (*.exe)|*.exe|All files (*.*)|*.*"
            };

            if (ofd.ShowDialog() != DialogResult.OK) return;

            string? folder = Path.GetDirectoryName(ofd.FileName);
            DialogResult confirm = MessageBox.Show(
                $"This will grant '{Environment.UserName}' Modify access to the app's install folder:\r\n\r\n{folder}\r\n\r\n" +
                "(and everything inside it) so the app can read/write its own files there without needing " +
                "to run elevated.\r\n\r\nContinue?",
                "Allow App to Run Without Admin",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirm != DialogResult.Yes) return;

            var (success, output) = await AppRepairService.AllowAppToRunWithoutAdminAsync(ofd.FileName);
            ShowTargetResult($"Allow Without Admin: {ofd.FileName}", success, output);
        }

        private async Task GrantUserPermissionAsync()
        {
            if (!ShellService.IsAdmin())
            {
                MessageBox.Show(
                    "This tool needs to be running as Administrator to grant the permission (one-time setup).\r\n\r\nRestart it with 'Run as administrator' and try again.",
                    "Administrator Required",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            using OpenFileDialog ofd = new()
            {
                Title = "Choose the application (.exe) the user should be able to open elevated",
                Filter = "Applications (*.exe)|*.exe|All files (*.*)|*.*"
            };

            if (ofd.ShowDialog() != DialogResult.OK) return;

            DialogResult confirm = MessageBox.Show(
                $"This will let the current user ('{Environment.UserName}') open:\r\n\r\n{ofd.FileName}\r\n\r\n" +
                "...elevated, whenever they want, without typing an admin password or seeing a UAC prompt.\r\n\r\n" +
                "It does this via a Scheduled Task (runs as SYSTEM) — the user is NOT made an admin.\r\n\r\nContinue?",
                "Grant Launch Permission",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirm != DialogResult.Yes) return;

            var (success, output, taskName) = await AppRepairService.GrantUserLaunchPermissionAsync(ofd.FileName);
            ShowTargetResult($"Grant User Permission: {ofd.FileName}", success, output);

            if (success && !string.IsNullOrWhiteSpace(taskName))
            {
                DialogResult shortcutPrompt = MessageBox.Show(
                    "Create a desktop shortcut for the user so they can just double-click it instead of using the command line?",
                    "Create Shortcut",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (shortcutPrompt == DialogResult.Yes)
                {
                    var (shortcutSuccess, shortcutOutput) = AppRepairService.CreateLaunchShortcut(
                        taskName, Path.GetFileNameWithoutExtension(ofd.FileName));

                    _txtDetails.AppendText($"\r\n\r\n{new string('-', 60)}\r\n{shortcutOutput}");
                    if (!shortcutSuccess)
                    {
                        MessageBox.Show(shortcutOutput, "Shortcut Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
            }
        }

        private async Task RevokeUserPermissionAsync()
        {
            if (!ShellService.IsAdmin())
            {
                MessageBox.Show(
                    "This tool needs to be running as Administrator to revoke the permission.",
                    "Administrator Required",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            string? taskName = PromptForText(
                "Revoke Launch Permission",
                "Enter the exact Scheduled Task name to remove\r\n(shown in the result text after granting permission, e.g. 'IT-Helpdesk-Elevated-app-username'):");

            if (string.IsNullOrWhiteSpace(taskName)) return;

            var (success, output) = await AppRepairService.RevokeUserLaunchPermissionAsync(taskName.Trim());
            ShowTargetResult($"Revoke Permission: {taskName}", success, output);
        }

        private static string? PromptForText(string title, string prompt)
        {
            using Form dlg = new()
            {
                Text = title,
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MinimizeBox = false,
                MaximizeBox = false,
                ClientSize = new Size(420, 150),
                BackColor = DarkColors.Background
            };

            Label lbl = new()
            {
                Text = prompt,
                AutoSize = false,
                Size = new Size(390, 55),
                Location = new Point(15, 12),
                ForeColor = DarkColors.TextMain
            };
            dlg.Controls.Add(lbl);

            TextBox txt = new()
            {
                Location = new Point(15, 72),
                Size = new Size(390, 26),
                BackColor = DarkColors.InputBg,
                ForeColor = DarkColors.TextMain,
                BorderStyle = BorderStyle.FixedSingle
            };
            dlg.Controls.Add(txt);

            ModernButton btnOk = new() { Text = "OK", Width = 90, Location = new Point(225, 108) };
            btnOk.Click += (s, e) => { dlg.DialogResult = DialogResult.OK; dlg.Close(); };
            dlg.Controls.Add(btnOk);

            ModernButton btnCancel = new() { Text = "Cancel", Width = 90, Location = new Point(320, 108) };
            btnCancel.Click += (s, e) => { dlg.DialogResult = DialogResult.Cancel; dlg.Close(); };
            dlg.Controls.Add(btnCancel);

            dlg.AcceptButton = btnOk;
            dlg.CancelButton = btnCancel;

            return dlg.ShowDialog() == DialogResult.OK ? txt.Text : null;
        }

        private void ShowTargetResult(string title, bool success, string output)
        {
            StringBuilder sb = new();
            sb.AppendLine($"{title}");
            sb.AppendLine(new string('=', 60));
            sb.AppendLine($"\r\nResult: {(success ? "SUCCESS" : "FAILED")}\r\n");
            sb.AppendLine(output);
            _txtDetails.Text = sb.ToString();
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
                    sb.AppendLine("(May require Administrator privileges — a UAC prompt can appear.)\r\n");
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
