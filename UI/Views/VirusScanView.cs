using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using ITHelpdeskToolkit.Services;
using ITHelpdeskToolkit.UI.Controls;
using ITHelpdeskToolkit.UI.Theme;

namespace ITHelpdeskToolkit.UI.Views
{
    public class VirusScanView : UserControl
    {
        private readonly TextBox _txtPath;
        private readonly TextBox _txtApiKey;
        private readonly CheckBox _chkRememberKey;
        private readonly ModernButton _btnBrowseFile;
        private readonly ModernButton _btnBrowseFolder;
        private readonly ModernButton _btnScanOffline;
        private readonly ModernButton _btnScanOnline;
        private readonly ModernButton _btnUploadOnline;
        private readonly TextBox _txtDetails;
        private readonly Label _lblStatus;

        public VirusScanView()
        {
            DoubleBuffered = true;
            BackColor = DarkColors.Background;
            Dock = DockStyle.Fill;
            Padding = new Padding(25);

            Label lblTitle = new()
            {
                BackColor = Color.Transparent,
                Text = "Virus Scan",
                Font = new Font("Segoe UI", 20F, FontStyle.Bold),
                ForeColor = DarkColors.TextMain,
                AutoSize = true,
                Location = new Point(0, 0)
            };
            Controls.Add(lblTitle);

            Label lblSub = new()
            {
                BackColor = Color.Transparent,
                Text = "Scan a file or folder offline with Windows Defender, or check it online against VirusTotal.",
                Font = new Font("Segoe UI", 10F),
                ForeColor = DarkColors.TextMuted,
                AutoSize = true,
                Location = new Point(4, 38)
            };
            Controls.Add(lblSub);

            // Path picker row — using the same FlowLayoutPanel pattern already proven to
            // render correctly elsewhere in this app (see ExcelView's action row). The
            // earlier TableLayoutPanel with auto-size columns was mis-measuring the custom
            // ModernButton control and collapsing the button columns to zero width.
            FlowLayoutPanel pathRow = new()
            {
                Location = new Point(0, 80),
                Size = new Size(950, 40),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                WrapContents = false
            };
            Controls.Add(pathRow);

            _txtPath = new TextBox
            {
                Width = 620,
                Height = 30,
                Margin = new Padding(0, 3, 10, 0),
                ReadOnly = true,
                BackColor = DarkColors.InputBg,
                ForeColor = DarkColors.TextMain,
                Font = new Font("Segoe UI", 9.5F),
                BorderStyle = BorderStyle.FixedSingle,
                PlaceholderText = "No file or folder selected..."
            };
            pathRow.Controls.Add(_txtPath);

            _btnBrowseFile = new ModernButton
            {
                Text = "📄 Choose File",
                Style = ButtonStyle.Secondary,
                Width = 130,
                Margin = new Padding(0, 0, 10, 0)
            };
            _btnBrowseFile.Click += (s, e) => BrowseFile();
            pathRow.Controls.Add(_btnBrowseFile);

            _btnBrowseFolder = new ModernButton
            {
                Text = "📁 Choose Folder",
                Style = ButtonStyle.Secondary,
                Width = 140
            };
            _btnBrowseFolder.Click += (s, e) => BrowseFolder();
            pathRow.Controls.Add(_btnBrowseFolder);

            // Scan action row
            FlowLayoutPanel actionPanel = new()
            {
                Location = new Point(0, 122),
                Size = new Size(950, 45),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            Controls.Add(actionPanel);

            _btnScanOffline = new ModernButton
            {
                Text = "🛡 Scan Offline (Windows Defender)",
                Style = ButtonStyle.Primary,
                Width = 250,
                Margin = new Padding(0, 0, 10, 0)
            };
            _btnScanOffline.Click += async (s, e) => await ScanOfflineAsync();
            actionPanel.Controls.Add(_btnScanOffline);

            _btnScanOnline = new ModernButton
            {
                Text = "🌐 Check Online (VirusTotal)",
                Style = ButtonStyle.Secondary,
                Width = 220,
                Margin = new Padding(0, 0, 10, 0)
            };
            _btnScanOnline.Click += async (s, e) => await ScanOnlineAsync();
            actionPanel.Controls.Add(_btnScanOnline);

            _btnUploadOnline = new ModernButton
            {
                Text = "⬆ Upload for Fresh Scan",
                Style = ButtonStyle.Secondary,
                Width = 200
            };
            _btnUploadOnline.Click += async (s, e) => await UploadOnlineAsync();
            actionPanel.Controls.Add(_btnUploadOnline);

            // VirusTotal API key row
            Label lblApiKey = new()
            {
                BackColor = Color.Transparent,
                Text = "VirusTotal API Key:",
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = DarkColors.TextMain,
                AutoSize = true,
                Location = new Point(0, 178)
            };
            Controls.Add(lblApiKey);

            _txtApiKey = new TextBox
            {
                Location = new Point(0, 200),
                Size = new Size(420, 28),
                Anchor = AnchorStyles.Top | AnchorStyles.Left,
                UseSystemPasswordChar = true,
                BackColor = DarkColors.InputBg,
                ForeColor = DarkColors.TextMain,
                Font = new Font("Segoe UI", 9.5F),
                BorderStyle = BorderStyle.FixedSingle,
                Text = VirusScanService.LoadSavedApiKey()
            };
            Controls.Add(_txtApiKey);

            _chkRememberKey = new CheckBox
            {
                BackColor = Color.Transparent,
                Text = "Remember key on this PC",
                ForeColor = DarkColors.TextMain,
                Font = new Font("Segoe UI", 9F),
                AutoSize = true,
                Location = new Point(430, 205),
                Checked = !string.IsNullOrWhiteSpace(VirusScanService.LoadSavedApiKey())
            };
            _chkRememberKey.CheckedChanged += (s, e) =>
            {
                if (!_chkRememberKey.Checked) VirusScanService.SaveApiKey("");
            };
            Controls.Add(_chkRememberKey);

            Label lblKeyHint = new()
            {
                BackColor = Color.Transparent,
                Text = "Free key: virustotal.com/gui/join-us · stored as plain text under your AppData folder, not this app's servers.",
                Font = new Font("Segoe UI", 8F),
                ForeColor = DarkColors.TextDim,
                AutoSize = true,
                Location = new Point(0, 232)
            };
            Controls.Add(lblKeyHint);

            // Status + details
            _lblStatus = new Label
            {
                BackColor = Color.Transparent,
                Text = "Ready.",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = DarkColors.TextMuted,
                AutoSize = true,
                Location = new Point(0, 264)
            };
            Controls.Add(_lblStatus);

            Label lblDetails = new()
            {
                BackColor = Color.Transparent,
                Text = "Details",
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = DarkColors.TextMain,
                AutoSize = true,
                Location = new Point(0, 292)
            };
            Controls.Add(lblDetails);

            _txtDetails = new TextBox
            {
                Location = new Point(0, 320),
                Size = new Size(950, 300),
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
        }

        private void BrowseFile()
        {
            using OpenFileDialog ofd = new() { Title = "Select a file to scan" };
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                _txtPath.Text = ofd.FileName;
            }
        }

        private void BrowseFolder()
        {
            using FolderBrowserDialog fbd = new() { Description = "Select a folder to scan" };
            if (fbd.ShowDialog() == DialogResult.OK)
            {
                _txtPath.Text = fbd.SelectedPath;
            }
        }

        private bool EnsurePathSelected()
        {
            if (string.IsNullOrWhiteSpace(_txtPath.Text) ||
                (!File.Exists(_txtPath.Text) && !Directory.Exists(_txtPath.Text)))
            {
                MessageBox.Show("Choose a file or folder first.", "Nothing Selected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            return true;
        }

        private string GetApiKey()
        {
            string key = _txtApiKey.Text.Trim();
            if (_chkRememberKey.Checked && !string.IsNullOrWhiteSpace(key))
            {
                VirusScanService.SaveApiKey(key);
            }
            return key;
        }

        private void SetBusy(bool busy, string status)
        {
            _btnScanOffline.Enabled = !busy;
            _btnScanOnline.Enabled = !busy;
            _btnUploadOnline.Enabled = !busy;
            _btnBrowseFile.Enabled = !busy;
            _btnBrowseFolder.Enabled = !busy;
            _lblStatus.Text = status;
            _lblStatus.ForeColor = busy ? DarkColors.Warning : DarkColors.TextMuted;
        }

        private async Task ScanOfflineAsync()
        {
            if (!EnsurePathSelected()) return;

            SetBusy(true, "Scanning with Windows Defender...");
            _txtDetails.Text = $"Running offline scan on:\n{_txtPath.Text}\n\n(This can take a while for large folders.)";

            var (success, threatFound, output) = await VirusScanService.OfflineScanAsync(_txtPath.Text);

            _txtDetails.Text = "Offline Scan Result (Windows Defender)\n" + new string('=', 60) + "\n" + output;
            SetBusy(false, success
                ? (threatFound ? "⚠ Threat detected!" : "✓ Clean — no threats found.")
                : "Scan could not run — see details.");

            if (success && threatFound)
            {
                MessageBox.Show("Windows Defender flagged a threat in the scanned item. See Details.",
                    "Threat Detected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            else if (!success)
            {
                MessageBox.Show(output, "Scan Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task ScanOnlineAsync()
        {
            if (!EnsurePathSelected()) return;
            if (Directory.Exists(_txtPath.Text))
            {
                MessageBox.Show("Online lookup works on a single file, not a folder. Choose a file instead.",
                    "File Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string apiKey = GetApiKey();
            SetBusy(true, "Checking VirusTotal by file hash...");
            _txtDetails.Text = $"Hashing and checking:\n{_txtPath.Text}";

            var (success, found, malicious, output) = await VirusScanService.OnlineScanByHashAsync(_txtPath.Text, apiKey);

            _txtDetails.Text = "Online Lookup Result (VirusTotal)\n" + new string('=', 60) + "\n" + output;
            SetBusy(false, !success
                ? "Lookup failed — see details."
                : !found
                    ? "Not previously seen by VirusTotal."
                    : malicious
                        ? "⚠ Flagged as malicious!"
                        : "✓ Clean — no engines flagged it.");

            if (success && found && malicious)
            {
                MessageBox.Show("VirusTotal engines flagged this file as malicious. See Details.",
                    "Threat Detected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private async Task UploadOnlineAsync()
        {
            if (!EnsurePathSelected()) return;
            if (Directory.Exists(_txtPath.Text))
            {
                MessageBox.Show("Upload works on a single file, not a folder. Choose a file instead.",
                    "File Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string apiKey = GetApiKey();

            DialogResult confirm = MessageBox.Show(
                "This will upload the selected file to VirusTotal's public service for analysis.\r\n" +
                "Only do this for files that don't contain sensitive/private data.\r\n\r\nContinue?",
                "Upload Confirmation", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes) return;

            SetBusy(true, "Uploading and waiting for analysis (this can take a minute or two)...");
            _txtDetails.Text = $"Uploading to VirusTotal:\n{_txtPath.Text}\n\nWaiting for engines to finish scanning...";

            var (success, malicious, output) = await VirusScanService.UploadForOnlineScanAsync(_txtPath.Text, apiKey);

            _txtDetails.Text = "Online Upload Scan Result (VirusTotal)\n" + new string('=', 60) + "\n" + output;
            SetBusy(false, !success
                ? "Upload/analysis failed — see details."
                : malicious
                    ? "⚠ Flagged as malicious!"
                    : "✓ Clean — no engines flagged it.");

            if (success && malicious)
            {
                MessageBox.Show("VirusTotal engines flagged this file as malicious. See Details.",
                    "Threat Detected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}
