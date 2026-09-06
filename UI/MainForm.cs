using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using ITHelpdeskToolkit.Services;
using ITHelpdeskToolkit.UI.Controls;
using ITHelpdeskToolkit.UI.Theme;
using ITHelpdeskToolkit.UI.Views;

namespace ITHelpdeskToolkit.UI
{
    public class MainForm : Form
    {
        private Panel _sidebarPanel = null!;
        private Panel _contentPanel = null!;
        private Panel _contentTopBar = null!;
        private Panel _viewHost = null!;
        private Button _sidebarToggleBtn = null!;
        private Button _themeToggleBtn = null!;
        private StatusStrip _statusStrip = null!;
        private ToolStripStatusLabel _statusLabel = null!;
        private ToolStripStatusLabel _adminBadge = null!;
        private readonly Dictionary<string, NavButton> _navButtons = new();
        private UserControl? _currentView;
        private string _currentPageId = "dashboard";
        private bool _sidebarVisible = true;
        private const int SidebarWidth = 240;

        public MainForm()
        {
            Text = $"IT Helpdesk Toolkit v{AppInfo.Version}";
            Size = new Size(1220, 800);
            MinimumSize = new Size(1020, 680);
            StartPosition = FormStartPosition.CenterScreen;
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

            // Double buffering for smooth window resizing
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint, true);
            UpdateStyles();

            BuildShell();

            Resize += (s, e) => LayoutShell();
            DarkColors.Changed += OnThemeChanged;
            FormClosed += (s, e) => DarkColors.Changed -= OnThemeChanged;
        }

        /// <summary>
        /// Builds the entire window shell (status strip, sidebar, content panel,
        /// top bar, and the currently selected view) from scratch. Called once
        /// from the constructor, and again whenever the theme changes, since the
        /// colors used throughout are only read at control-construction time.
        /// </summary>
        private void BuildShell()
        {
            SuspendLayout();
            Controls.Clear();

            BackColor = DarkColors.Background;

            // Status Bar at Bottom
            _statusStrip = new StatusStrip
            {
                BackColor = DarkColors.Header,
                ForeColor = DarkColors.TextMuted,
                SizingGrip = true
            };

            _statusLabel = new ToolStripStatusLabel
            {
                Text = "Ready",
                Alignment = ToolStripItemAlignment.Left,
                Spring = true,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = DarkColors.TextMuted
            };

            _adminBadge = new ToolStripStatusLabel
            {
                Text = ShellService.IsAdmin() ? "🛡 ADMIN ELEVATED" : "USER MODE",
                Alignment = ToolStripItemAlignment.Right,
                ForeColor = ShellService.IsAdmin() ? DarkColors.Success : DarkColors.Warning,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };

            _statusStrip.Items.Add(_statusLabel);
            _statusStrip.Items.Add(_adminBadge);
            Controls.Add(_statusStrip);

            // Sidebar Panel
            _sidebarPanel = new Panel
            {
                Dock = DockStyle.Left,
                Width = SidebarWidth,
                BackColor = DarkColors.Sidebar,
                Padding = new Padding(0, 15, 0, 15),
                Visible = _sidebarVisible
            };
            Controls.Add(_sidebarPanel);
            _sidebarPanel.VisibleChanged += (s, e) => LayoutShell();

            // Main Content Panel (holds the fixed top bar + the swappable view host).
            // NOTE: deliberately NOT using Dock=Fill here. In this app's control
            // tree, Dock=Fill was resolving against the Form's full client area
            // instead of the space left after the sidebar, so content (and the
            // top bar) ended up positioned underneath the opaque sidebar panel
            // instead of beside it. LayoutShell() below sets exact bounds instead,
            // so this can never silently regress again.
            _contentPanel = new Panel
            {
                BackColor = DarkColors.Background,
                Padding = new Padding(0)
            };
            Controls.Add(_contentPanel);
            _contentPanel.BringToFront();

            // Top bar — always visible regardless of which view is showing,
            // so the sidebar/theme toggles are never hidden behind clipped content.
            _contentTopBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 36,
                BackColor = DarkColors.Header
            };

            _sidebarToggleBtn = new Button
            {
                Text = _sidebarVisible ? "☰" : "▶",
                Width = 40,
                Height = 30,
                Location = new Point(4, 3),
                FlatStyle = FlatStyle.Flat,
                BackColor = DarkColors.Header,
                ForeColor = DarkColors.TextMain,
                Font = new Font("Segoe UI", 11F),
                Cursor = Cursors.Hand
            };
            _sidebarToggleBtn.FlatAppearance.BorderSize = 0;
            _sidebarToggleBtn.Click += (s, e) => ToggleSidebar();
            _contentTopBar.Controls.Add(_sidebarToggleBtn);

            _themeToggleBtn = new Button
            {
                Text = DarkColors.Mode == ThemeMode.Dark ? "☀ Light" : "🌙 Dark",
                Width = 90,
                Height = 30,
                Location = new Point(48, 3),
                FlatStyle = FlatStyle.Flat,
                BackColor = DarkColors.Header,
                ForeColor = DarkColors.TextMain,
                Font = new Font("Segoe UI", 9.5F),
                Cursor = Cursors.Hand
            };
            _themeToggleBtn.FlatAppearance.BorderSize = 0;
            _themeToggleBtn.Click += (s, e) => ToggleTheme();
            _contentTopBar.Controls.Add(_themeToggleBtn);

            // View host — this is the panel NavigateTo swaps pages into.
            // Kept separate from _contentPanel so the top bar/toggles are never
            // removed or covered when views change. Padding here is what keeps
            // every view's content from sitting flush against the sidebar/edges.
            _viewHost = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = DarkColors.Background,
                Padding = new Padding(24, 32, 24, 20)
            };

            // Add the top-docked bar BEFORE the fill control so its 36px is
            // reserved first; _viewHost (Fill) then only ever gets what's left,
            // so it can never render underneath/behind the top bar.
            _contentPanel.Controls.Add(_contentTopBar);
            _contentPanel.Controls.Add(_viewHost);

            InitializeSidebar();

            ResumeLayout(true);

            NavigateTo(_currentPageId);
            LayoutShell();
        }

        private void OnThemeChanged()
        {
            _currentView = null; // being disposed as part of Controls.Clear() during rebuild
            BuildShell();
        }

        /// <summary>
        /// Explicitly positions/sizes _contentPanel to occupy exactly the space
        /// not used by the sidebar and status strip. Avoids relying on Dock=Fill
        /// resolving correctly against sibling docked controls (see note above).
        /// </summary>
        private void LayoutShell()
        {
            int left = _sidebarPanel.Visible ? _sidebarPanel.Width : 0;
            int bottom = _statusStrip.Visible ? _statusStrip.Height : 0;
            int width = Math.Max(0, ClientSize.Width - left);
            int height = Math.Max(0, ClientSize.Height - bottom);
            _contentPanel.Bounds = new Rectangle(left, 0, width, height);
        }

        private void ToggleSidebar()
        {
            _sidebarVisible = !_sidebarVisible;
            _sidebarPanel.Visible = _sidebarVisible;
            _sidebarToggleBtn.Text = _sidebarVisible ? "☰" : "▶";
        }

        private void ToggleTheme()
        {
            DarkColors.Mode = DarkColors.Mode == ThemeMode.Dark ? ThemeMode.Light : ThemeMode.Dark;
        }

        private void InitializeSidebar()
        {
            _navButtons.Clear();


            // headerBlock / navContainer / footer use EXPLICIT bounds, not
            // Dock, and are kept correct via LayoutSidebarChildren() below —
            // not by trusting Dock to resolve them against each other. This
            // is the exact same Fill-vs-edge-dock failure we already fixed
            // once at the form level (_contentPanel vs _sidebarPanel): the
            // edge-docked control (headerBlock, added/painted first) was
            // covering the top of the Fill-docked one (navContainer) instead
            // of being excluded from its bounds, which is why only a sliver
            // of the 2nd button ever peeked out from underneath it. Since
            // trusting Dock ordering for this relationship has now failed
            // three different ways in this app, we stop relying on it here
            // too, exactly as we already did once for the outer shell.
            const int headerHeight = 81;
            const int footerHeight = 40;

            Panel headerBlock = new()
            {
                BackColor = DarkColors.Sidebar
            };
            _sidebarPanel.Controls.Add(headerBlock);
            headerBlock.BringToFront();

            Label lblAppTitle = new()
            {
                BackColor = Color.Transparent,
                Text = "IT HELPDESK",
                Font = new Font("Segoe UI", 14F, FontStyle.Bold),
                ForeColor = DarkColors.TextMain,
                Dock = DockStyle.Top,
                Height = 30,
                TextAlign = ContentAlignment.MiddleCenter
            };
            headerBlock.Controls.Add(lblAppTitle);

            Label lblSubTitle = new()
            {
                BackColor = Color.Transparent,
                Text = $"TOOLKIT v{AppInfo.Version}",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = DarkColors.Primary,
                Dock = DockStyle.Top,
                Height = 25,
                TextAlign = ContentAlignment.MiddleCenter
            };
            headerBlock.Controls.Add(lblSubTitle);

            Panel gapAboveSeparator = new() { Dock = DockStyle.Top, Height = 10, BackColor = DarkColors.Sidebar };
            headerBlock.Controls.Add(gapAboveSeparator);

            Panel separator = new()
            {
                Dock = DockStyle.Top,
                Height = 1,
                BackColor = DarkColors.CardBorder
            };
            headerBlock.Controls.Add(separator);

            Panel gapBelowSeparator = new() { Dock = DockStyle.Top, Height = 15, BackColor = DarkColors.Sidebar };
            headerBlock.Controls.Add(gapBelowSeparator);

            // Navigation Buttons Container
            Panel navContainer = new()
            {
                AutoScroll = false,
                BackColor = DarkColors.Sidebar
            };
            _sidebarPanel.Controls.Add(navContainer);

            (string id, string text)[] navItems = new[]
            {
                ("dashboard", "🏠 Dashboard"),
                ("inventory", "🖥 Asset Inventory"),
                ("network", "🌐 Network Diagnostics"),
                ("network_repair", "🔧 Network Repair"),
                ("system_repair", "🛠 Windows / Repair"),
                ("cleanup", "🧹 System Cleanup"),
                ("excel", "📊 Excel Troubleshooter"),
                ("virus_scan", "🛡 Virus Scan"),
                ("apps", "📦 Application Fixes"),
                ("printer", "🖨 Printer Fixes"),
                ("password", "🔐 Password Generator")
            };

            // Positioned explicitly (Location/Size) rather than Dock=Top.
            // Stacking several Dock=Top siblings inside a Dock=Fill container
            // has repeatedly misbehaved in this app's WinForms Dock
            // resolution (see notes elsewhere in this file) — most recently
            // collapsing the first nav button to a sliver because its height
            // got computed before navContainer itself had a real size yet.
            // Explicit bounds can't be affected by that timing issue.
            const int navBtnHeight = 44;
            for (int i = 0; i < navItems.Length; i++)
            {
                var (id, text) = navItems[i];
                NavButton btn = new()
                {
                    Text = text,
                    Location = new Point(0, i * navBtnHeight),
                    Size = new Size(SidebarWidth, navBtnHeight)
                };
                btn.Click += (s, e) => NavigateTo(id);
                navContainer.Controls.Add(btn);
                _navButtons[id] = btn;
            }

            // Footer version text
            Label lblFooter = new()
            {
                BackColor = Color.Transparent,
                Text = "Windows Repair Edition",
                Font = new Font("Segoe UI", 8F),
                ForeColor = DarkColors.TextDim,
                TextAlign = ContentAlignment.MiddleCenter
            };
            _sidebarPanel.Controls.Add(lblFooter);

            void LayoutSidebarChildren()
            {
                int h = _sidebarPanel.ClientSize.Height;
                headerBlock.Bounds = new Rectangle(0, 0, SidebarWidth, headerHeight);
                lblFooter.Bounds = new Rectangle(0, Math.Max(headerHeight, h - footerHeight), SidebarWidth, footerHeight);
                int navTop = headerHeight;
                int navHeight = Math.Max(0, lblFooter.Top - navTop);
                navContainer.Bounds = new Rectangle(0, navTop, SidebarWidth, navHeight);
            }

            _sidebarPanel.Resize += (s, e) => LayoutSidebarChildren();
            LayoutSidebarChildren();
        }

        public void NavigateTo(string pageId)
        {
            _currentPageId = pageId;

            foreach (var kvp in _navButtons)
            {
                kvp.Value.IsActive = (kvp.Key == pageId);
            }

            UserControl newView = pageId switch
            {
                "inventory" => new InventoryView(),
                "network" => new NetworkView(),
                "network_repair" => new NetworkRepairView(),
                "system_repair" => new SystemRepairView(),
                "cleanup" => new CleanupView(),
                "excel" => new ExcelView(),
                "virus_scan" => new VirusScanView(),
                "apps" => new AppRepairView(),
                "printer" => new PrinterView(),
                "password" => new PasswordView(),
                _ => CreateDashboardView()
            };

            _viewHost.SuspendLayout();
            if (_currentView != null)
            {
                _viewHost.Controls.Remove(_currentView);
                _currentView.Dispose();
            }

            _currentView = newView;
            _viewHost.Controls.Add(_currentView);
            _viewHost.ResumeLayout(true);

            _statusLabel.Text = $"Page: {pageId.ToUpperInvariant()} — Ready";
        }

        private DashboardView CreateDashboardView()
        {
            DashboardView view = new();
            view.OnNavigateRequested = NavigateTo;
            return view;
        }

        public void SetStatus(string message)
        {
            _statusLabel.Text = message;
        }
    }
}
