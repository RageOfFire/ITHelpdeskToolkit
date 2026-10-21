using System;
using System.Drawing;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using ITHelpdeskToolkit.Services;
using ITHelpdeskToolkit.UI.Controls;
using ITHelpdeskToolkit.UI.Theme;

namespace ITHelpdeskToolkit.UI.Views
{
    public class LicenseCheckerView : UserControl
    {
        private readonly ModernButton _btnCheckAll;
        private readonly ModernButton _btnCheckWindows;
        private readonly ModernButton _btnCheckOffice;
        private readonly Panel       _windowsCard;
        private readonly Panel       _officeCard;
        private readonly TextBox     _txtLog;

        public LicenseCheckerView()
        {
            DoubleBuffered = true;
            BackColor = DarkColors.Background;
            Dock      = DockStyle.Fill;
            Padding   = new Padding(25);

            // ── Title ────────────────────────────────────────────────────────
            Label lblTitle = new()
            {
                BackColor = Color.Transparent,
                Text      = "License Checker",
                Font      = new Font("Segoe UI", 20F, FontStyle.Bold),
                ForeColor = DarkColors.TextMain,
                AutoSize  = true,
                Location  = new Point(0, 0)
            };
            Controls.Add(lblTitle);

            Label lblSub = new()
            {
                BackColor = Color.Transparent,
                Text      = "Check activation status and license details for Windows and Microsoft Office.",
                Font      = new Font("Segoe UI", 10F),
                ForeColor = DarkColors.TextMuted,
                AutoSize  = true,
                Location  = new Point(4, 38)
            };
            Controls.Add(lblSub);

            // ── Toolbar ──────────────────────────────────────────────────────
            FlowLayoutPanel toolbar = new()
            {
                Location = new Point(0, 75),
                Size     = new Size(950, 45),
                Anchor   = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            Controls.Add(toolbar);

            _btnCheckAll = new ModernButton
            {
                Text   = "▶ CHECK ALL",
                Style  = ButtonStyle.Primary,
                Width  = 140,
                Margin = new Padding(0, 0, 10, 0)
            };
            _btnCheckAll.Click += async (s, e) => await RunChecksAsync(true, true);
            toolbar.Controls.Add(_btnCheckAll);

            _btnCheckWindows = new ModernButton
            {
                Text   = "🪟 Windows Only",
                Style  = ButtonStyle.Secondary,
                Width  = 145,
                Margin = new Padding(0, 0, 10, 0)
            };
            _btnCheckWindows.Click += async (s, e) => await RunChecksAsync(true, false);
            toolbar.Controls.Add(_btnCheckWindows);

            _btnCheckOffice = new ModernButton
            {
                Text   = "📦 Office Only",
                Style  = ButtonStyle.Secondary,
                Width  = 135,
                Margin = new Padding(0, 0, 0, 0)
            };
            _btnCheckOffice.Click += async (s, e) => await RunChecksAsync(false, true);
            toolbar.Controls.Add(_btnCheckOffice);

            // ── Result cards ─────────────────────────────────────────────────
            _windowsCard = BuildResultCard("🪟  Windows", new Point(0, 130));
            Controls.Add(_windowsCard);

            _officeCard = BuildResultCard("📦  Microsoft Office", new Point(480, 130));
            Controls.Add(_officeCard);

            // ── Raw log ──────────────────────────────────────────────────────
            Label lblLog = new()
            {
                BackColor = Color.Transparent,
                Text      = "Raw Output",
                Font      = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = DarkColors.TextMain,
                AutoSize  = true,
                Location  = new Point(0, 330)
            };
            Controls.Add(lblLog);

            _txtLog = new TextBox
            {
                Location    = new Point(0, 356),
                Size        = new Size(950, 230),
                Anchor      = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Multiline   = true,
                ScrollBars  = ScrollBars.Vertical,
                ReadOnly    = true,
                BackColor   = DarkColors.InputBg,
                ForeColor   = DarkColors.TextMain,
                Font        = new Font("Consolas", 9F),
                BorderStyle = BorderStyle.FixedSingle
            };
            Controls.Add(_txtLog);

            ResetCards();
        }

        // ── Card builder ─────────────────────────────────────────────────────

        private static Panel BuildResultCard(string title, Point location)
        {
            Panel card = new()
            {
                Location    = location,
                Size        = new Size(465, 185),
                Anchor      = AnchorStyles.Top | AnchorStyles.Left,
                BackColor   = DarkColors.CardBg,
                BorderStyle = BorderStyle.FixedSingle,
                Padding     = new Padding(14)
            };

            Label lblTitle = new()
            {
                Name      = "lblTitle",
                Text      = title,
                Font      = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = DarkColors.TextMain,
                BackColor = Color.Transparent,
                AutoSize  = true,
                Location  = new Point(14, 12)
            };
            card.Controls.Add(lblTitle);

            // Status badge
            Label lblStatus = new()
            {
                Name      = "lblStatus",
                Text      = "NOT CHECKED",
                Font      = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = DarkColors.TextMuted,
                BackColor = DarkColors.InputBg,
                AutoSize  = false,
                Size      = new Size(130, 24),
                Location  = new Point(14, 44),
                TextAlign = ContentAlignment.MiddleCenter
            };
            card.Controls.Add(lblStatus);

            string[] rowNames  = { "Version", "Key", "Type", "Channel" };
            int[]    rowTops   = { 78, 104, 130, 156 };
            for (int i = 0; i < rowNames.Length; i++)
            {
                Label lbl = new()
                {
                    Text      = $"{rowNames[i]}:",
                    Font      = new Font("Segoe UI", 9F, FontStyle.Bold),
                    ForeColor = DarkColors.TextMuted,
                    BackColor = Color.Transparent,
                    AutoSize  = true,
                    Location  = new Point(14, rowTops[i])
                };
                card.Controls.Add(lbl);

                Label val = new()
                {
                    Name      = $"lbl{rowNames[i]}",
                    Text      = "—",
                    Font      = new Font("Segoe UI", 9F),
                    ForeColor = DarkColors.TextMain,
                    BackColor = Color.Transparent,
                    AutoSize  = false,
                    Size      = new Size(290, 18),
                    Location  = new Point(80, rowTops[i])
                };
                card.Controls.Add(val);
            }

            return card;
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private void SetAllButtonsEnabled(bool enabled)
        {
            _btnCheckAll.Enabled     = enabled;
            _btnCheckWindows.Enabled = enabled;
            _btnCheckOffice.Enabled  = enabled;
        }

        private void ResetCards()
        {
            ResetCard(_windowsCard);
            ResetCard(_officeCard);
            _txtLog.Text = "Press ▶ CHECK ALL or select an individual check to begin.";
        }

        private static void ResetCard(Panel card)
        {
            SetCardField(card, "lblStatus",  "NOT CHECKED");
            SetCardField(card, "lblVersion", "—");
            SetCardField(card, "lblKey",     "—");
            SetCardField(card, "lblType",    "—");
            SetCardField(card, "lblChannel", "—");
            SetStatusBadge(card, LicenseStatus.Unknown);
        }

        private static void SetCardField(Panel card, string name, string value)
        {
            if (card.Controls[name] is Label lbl)
                lbl.Text = value;
        }

        private static void SetStatusBadge(Panel card, LicenseStatus status)
        {
            if (card.Controls["lblStatus"] is not Label badge) return;

            (badge.Text, badge.ForeColor, badge.BackColor) = status switch
            {
                LicenseStatus.Licensed     => ("✔ ACTIVATED",      Color.White,            Color.FromArgb(34, 139, 34)),
                LicenseStatus.Unlicensed   => ("✘ NOT ACTIVATED",  Color.White,            DarkColors.Danger),
                LicenseStatus.Notification => ("⚠ NOTIFICATION",   Color.Black,            Color.FromArgb(255, 193, 7)),
                LicenseStatus.Trial        => ("⏳ TRIAL",          Color.White,            Color.FromArgb(100, 100, 200)),
                LicenseStatus.Expired      => ("✘ EXPIRED",         Color.White,            DarkColors.Danger),
                _                          => ("— UNKNOWN",         DarkColors.TextMuted,   DarkColors.InputBg)
            };
        }

        private void PopulateCard(Panel card, LicenseResult r)
        {
            SetStatusBadge(card, r.Status);
            SetCardField(card, "lblStatus",  r.StatusDetail);
            SetCardField(card, "lblVersion", string.IsNullOrEmpty(r.Version)     ? "—" : r.Version);
            SetCardField(card, "lblKey",     string.IsNullOrEmpty(r.ProductKey)  ? "—" : r.ProductKey);
            SetCardField(card, "lblType",    string.IsNullOrEmpty(r.LicenseType) ? "—" : r.LicenseType);
            SetCardField(card, "lblChannel", string.IsNullOrEmpty(r.Channel)     ? "—" : r.Channel);
            SetStatusBadge(card, r.Status);
        }

        // ── Main logic ───────────────────────────────────────────────────────

        private async Task RunChecksAsync(bool checkWindows, bool checkOffice)
        {
            SetAllButtonsEnabled(false);
            _txtLog.Text = "Running license checks...\r\n";
            var logSb = new StringBuilder("Running license checks...\r\n\r\n");

            try
            {
                if (checkWindows)
                {
                    _txtLog.AppendText("Checking Windows...\r\n");
                    var winResult = await LicenseService.CheckWindowsAsync();
                    PopulateCard(_windowsCard, winResult);

                    logSb.AppendLine("══════════════════════════════════════════════════");
                    logSb.AppendLine("  WINDOWS");
                    logSb.AppendLine("══════════════════════════════════════════════════");
                    logSb.AppendLine($"  Status:  {winResult.StatusDetail}");
                    logSb.AppendLine($"  Version: {winResult.Version}");
                    logSb.AppendLine($"  Key:     {winResult.ProductKey}");
                    logSb.AppendLine($"  Type:    {winResult.LicenseType}");
                    logSb.AppendLine();
                    logSb.AppendLine("── Raw ──");
                    logSb.AppendLine(winResult.RawOutput);
                    logSb.AppendLine();
                }

                if (checkOffice)
                {
                    _txtLog.AppendText("Checking Microsoft Office...\r\n");
                    var offResult = await LicenseService.CheckOfficeAsync();
                    PopulateCard(_officeCard, offResult);

                    logSb.AppendLine("══════════════════════════════════════════════════");
                    logSb.AppendLine("  MICROSOFT OFFICE");
                    logSb.AppendLine("══════════════════════════════════════════════════");
                    logSb.AppendLine($"  Status:  {offResult.StatusDetail}");
                    logSb.AppendLine($"  Version: {offResult.Version}");
                    logSb.AppendLine($"  Key:     {offResult.ProductKey}");
                    logSb.AppendLine($"  Type:    {offResult.LicenseType}");
                    logSb.AppendLine($"  Channel: {offResult.Channel}");
                    logSb.AppendLine();
                    logSb.AppendLine("── Raw ──");
                    logSb.AppendLine(offResult.RawOutput);
                    logSb.AppendLine();
                }

                logSb.AppendLine($"Completed at {DateTime.Now:HH:mm:ss}");
                _txtLog.Text = logSb.ToString();
            }
            catch (Exception ex)
            {
                _txtLog.AppendText($"\r\nError: {ex.Message}");
            }
            finally
            {
                SetAllButtonsEnabled(true);
            }
        }
    }
}
