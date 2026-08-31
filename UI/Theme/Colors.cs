using System;
using System.Drawing;

namespace ITHelpdeskToolkit.UI.Theme
{
    public enum ThemeMode
    {
        Dark,
        Light
    }

    /// <summary>
    /// Kept the name "DarkColors" so every existing view/control in the app
    /// (which references DarkColors.XXX directly) keeps working unchanged.
    /// Internally these are now computed properties that switch on Mode,
    /// instead of fixed readonly fields.
    /// </summary>
    public static class DarkColors
    {
        private static ThemeMode _mode = ThemeMode.Light;

        public static ThemeMode Mode
        {
            get => _mode;
            set
            {
                if (_mode == value) return;
                _mode = value;
                Changed?.Invoke();
            }
        }

        /// <summary>Raised after Mode changes, so the shell can rebuild itself.</summary>
        public static event Action? Changed;

        private static bool IsDark => _mode == ThemeMode.Dark;

        public static Color Background => IsDark
            ? Color.FromArgb(15, 23, 42)      // #0F172A
            : Color.FromArgb(241, 245, 249);  // #F1F5F9

        public static Color Sidebar => IsDark
            ? Color.FromArgb(30, 41, 59)      // #1E293B
            : Color.FromArgb(255, 255, 255);  // #FFFFFF

        public static Color Header => IsDark
            ? Color.FromArgb(24, 34, 50)      // #182232
            : Color.FromArgb(255, 255, 255);  // #FFFFFF

        public static Color CardBg => IsDark
            ? Color.FromArgb(30, 41, 59)      // #1E293B
            : Color.FromArgb(255, 255, 255);  // #FFFFFF

        public static Color CardBorder => IsDark
            ? Color.FromArgb(51, 65, 85)      // #334155
            : Color.FromArgb(226, 232, 240);  // #E2E8F0

        public static Color InputBg => IsDark
            ? Color.FromArgb(15, 23, 42)      // #0F172A
            : Color.FromArgb(255, 255, 255);  // #FFFFFF

        public static Color InputBorder => IsDark
            ? Color.FromArgb(71, 85, 105)     // #475569
            : Color.FromArgb(203, 213, 225);  // #CBD5E1

        public static Color Primary => Color.FromArgb(59, 130, 246);       // #3B82F6 — works on both
        public static Color PrimaryHover => Color.FromArgb(37, 99, 235);   // #2563EB — works on both

        public static Color Secondary => IsDark
            ? Color.FromArgb(51, 65, 85)      // #334155
            : Color.FromArgb(226, 232, 240);  // #E2E8F0

        public static Color SecondaryHover => IsDark
            ? Color.FromArgb(71, 85, 105)     // #475569
            : Color.FromArgb(203, 213, 225);  // #CBD5E1

        public static Color Success => IsDark
            ? Color.FromArgb(16, 185, 129)    // #10B981
            : Color.FromArgb(5, 150, 105);    // #059669 (darker, for contrast on white)

        public static Color Danger => IsDark
            ? Color.FromArgb(239, 68, 68)     // #EF4444
            : Color.FromArgb(220, 38, 38);    // #DC2626

        public static Color Warning => IsDark
            ? Color.FromArgb(245, 158, 11)    // #F59E0B
            : Color.FromArgb(217, 119, 6);    // #D97706

        public static Color TextMain => IsDark
            ? Color.FromArgb(248, 250, 252)   // #F8FAFC
            : Color.FromArgb(15, 23, 42);     // #0F172A

        public static Color TextMuted => IsDark
            ? Color.FromArgb(148, 163, 184)   // #94A3B8
            : Color.FromArgb(71, 85, 105);    // #475569

        public static Color TextDim => IsDark
            ? Color.FromArgb(100, 116, 139)   // #64748B
            : Color.FromArgb(100, 116, 139);  // #64748B — legible on both
    }
}
