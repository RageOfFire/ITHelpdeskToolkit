using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ITHelpdeskToolkit.UI.Theme;

namespace ITHelpdeskToolkit.UI.Controls
{
    public class NavButton : Button
    {
        private bool _isActive;
        private bool _isHovered;

        public bool IsActive
        {
            get => _isActive;
            set
            {
                _isActive = value;
                Invalidate();
            }
        }

        public NavButton()
        {
            DoubleBuffered = true;
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Font = new Font("Segoe UI", 10F, FontStyle.Regular);
            ForeColor = DarkColors.TextMuted;
            BackColor = DarkColors.Sidebar;
            TextAlign = ContentAlignment.MiddleLeft;
            Padding = new Padding(16, 0, 0, 0);
            Height = 44;
            Cursor = Cursors.Hand;
        }

        protected override void OnMouseEnter(System.EventArgs e)
        {
            base.OnMouseEnter(e);
            _isHovered = true;
            Invalidate();
        }

        protected override void OnMouseLeave(System.EventArgs e)
        {
            base.OnMouseLeave(e);
            _isHovered = false;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            pevent.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            Color currentBg = IsActive
                ? DarkColors.Primary
                : (_isHovered ? DarkColors.SecondaryHover : DarkColors.Sidebar);

            Color currentText = IsActive ? Color.White : (_isHovered ? DarkColors.TextMain : DarkColors.TextMuted);
            Font currentFont = IsActive ? new Font(Font, FontStyle.Bold) : Font;

            using (SolidBrush bgBrush = new(currentBg))
            {
                pevent.Graphics.FillRectangle(bgBrush, ClientRectangle);
            }

            // Left Active Indicator Bar
            if (IsActive)
            {
                using SolidBrush accentBrush = new(DarkColors.PrimaryHover);
                pevent.Graphics.FillRectangle(accentBrush, new Rectangle(0, 6, 4, Height - 12));
            }

            TextRenderer.DrawText(
                pevent.Graphics,
                Text,
                currentFont,
                new Rectangle(20, 0, Width - 25, Height),
                currentText,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left
            );
        }
    }
}
