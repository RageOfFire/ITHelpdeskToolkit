using System;
using System.Drawing;
using System.Net;
using System.Windows.Forms;
using ITHelpdeskToolkit.Services;
using ITHelpdeskToolkit.UI.Controls;
using ITHelpdeskToolkit.UI.Theme;

namespace ITHelpdeskToolkit.UI.Views
{
    public class DashboardView : UserControl
    {
        public Action<string>? OnNavigateRequested;

        public DashboardView()
        {
            DoubleBuffered = true;
            BackColor = DarkColors.Background;
            Dock = DockStyle.Fill;
            Padding = new Padding(25);
            InitializeView();
        }

        private void InitializeView()
        {
            // Title Header
            Label lblTitle = new()
            {
                Text = "IT Helpdesk Toolkit",
                Font = new Font("Segoe UI", 22F, FontStyle.Bold),
                ForeColor = DarkColors.TextMain,
                AutoSize = true,
                Location = new Point(0, 0)
            };
            Controls.Add(lblTitle);

            Label lblSub = new()
            {
                Text = "A single application for common endpoint support and troubleshooting tasks.",
                Font = new Font("Segoe UI", 10F),
                ForeColor = DarkColors.TextMuted,
                AutoSize = true,
                Location = new Point(4, 42)
            };
            Controls.Add(lblSub);

            // Cards Container Panel
            FlowLayoutPanel cardLayout = new()
            {
                Location = new Point(0, 85),
                Size = new Size(950, 110),
                WrapContents = false,
                AutoScroll = false,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            Controls.Add(cardLayout);

            string hostName = Dns.GetHostName();
            string userName = Environment.UserName;
            string ipAddress = InventoryService.GetIpAddress();
            string osName = Environment.OSVersion.Platform.ToString();

            cardLayout.Controls.Add(CreateSummaryCard("Computer", hostName, DarkColors.Primary));
            cardLayout.Controls.Add(CreateSummaryCard("User", userName, DarkColors.Success));
            cardLayout.Controls.Add(CreateSummaryCard("IP Address", ipAddress, DarkColors.Warning));
            cardLayout.Controls.Add(CreateSummaryCard("OS", "Windows", DarkColors.TextMain));

            // Quick Actions Label
            Label lblQuickActions = new()
            {
                Text = "Quick Actions",
                Font = new Font("Segoe UI", 14F, FontStyle.Bold),
                ForeColor = DarkColors.TextMain,
                AutoSize = true,
                Location = new Point(0, 215)
            };
            Controls.Add(lblQuickActions);

            // Quick Actions Button Flow
            FlowLayoutPanel actionFlow = new()
            {
                Location = new Point(0, 250),
                Size = new Size(950, 90),
                WrapContents = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            Controls.Add(actionFlow);

            AddActionButton(actionFlow, "🖥 Scan This PC", "inventory");
            AddActionButton(actionFlow, "🌐 Run Network Checks", "network");
            AddActionButton(actionFlow, "🔧 Network Repair", "network_repair");
            AddActionButton(actionFlow, "🛠 System Repair", "system_repair");
            AddActionButton(actionFlow, "🧹 System Cleanup", "cleanup");
            AddActionButton(actionFlow, "📊 Excel Fixes", "excel");
            AddActionButton(actionFlow, "📦 App Fixes", "apps");
            AddActionButton(actionFlow, "🖨 Check Printers", "printer");
            AddActionButton(actionFlow, "🔐 Password Generator", "password");
            // Workflow Guide Card
            CardPanel guideCard = new()
            {
                Location = new Point(0, 355),
                Size = new Size(950, 130),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            Controls.Add(guideCard);

            Label lblGuideTitle = new()
            {
                Text = "Recommended Support Workflow",
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = DarkColors.Primary,
                AutoSize = true,
                Location = new Point(15, 12)
            };
            guideCard.Controls.Add(lblGuideTitle);

            Label lblWorkflow = new()
            {
                Text = "1. Scan Endpoint → Collect full hardware/OS specs and network configuration.\n" +
                       "2. Diagnostic Checks → Run Network & Printer connectivity diagnostics.\n" +
                       "3. Targeted Repairs → Apply Windows/File system repairs or Excel/App troubleshooting.\n" +
                       "4. Verification → Confirm resolution and export inventory or diagnostic logs for ticketing.",
                Font = new Font("Segoe UI", 9.5F),
                ForeColor = DarkColors.TextMuted,
                AutoSize = true,
                Location = new Point(15, 40)
            };
            guideCard.Controls.Add(lblWorkflow);

            // Anchor.Right's "distance from the right edge" is computed based
            // on this UserControl's size at the moment each panel is added —
            // which, since DashboardView is constructed standalone before
            // being placed into the real (properly-sized) view host, is
            // unreliable (same root cause as the inventory grid fix earlier).
            // That's why widening the window (e.g. maximizing) could leave
            // the Quick Actions row still using its old width, cutting off
            // the last button instead of it wrapping to a new row. Explicitly
            // re-syncing these widths on every resize sidesteps that entirely.
            void SyncWidths()
            {
                int w = Math.Max(0, ClientSize.Width - Padding.Left - Padding.Right);
                cardLayout.Width = w;
                actionFlow.Width = w;
                guideCard.Width = w;
            }

            Resize += (s, e) => SyncWidths();
            SyncWidths();
        }

        private static CardPanel CreateSummaryCard(string title, string value, Color accentColor)
        {
            CardPanel card = new()
            {
                Size = new Size(220, 95),
                Margin = new Padding(0, 0, 15, 0)
            };

            Label lblTitle = new()
            {
                Text = title.ToUpperInvariant(),
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = DarkColors.TextMuted,
                AutoSize = true,
                Location = new Point(15, 12)
            };
            card.Controls.Add(lblTitle);

            Label lblVal = new()
            {
                Text = value,
                Font = new Font("Segoe UI", 13F, FontStyle.Bold),
                ForeColor = accentColor,
                AutoSize = true,
                Location = new Point(14, 38)
            };
            card.Controls.Add(lblVal);

            return card;
        }

        private void AddActionButton(Control parent, string text, string targetPage)
        {
            ModernButton btn = new()
            {
                Text = text,
                Style = ButtonStyle.Secondary,
                Width = 195,
                Height = 38,
                Margin = new Padding(0, 0, 10, 10)
            };
            btn.Click += (s, e) => OnNavigateRequested?.Invoke(targetPage);
            parent.Controls.Add(btn);
        }
    }
}
