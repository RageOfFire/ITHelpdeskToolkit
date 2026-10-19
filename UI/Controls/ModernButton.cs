using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ITHelpdeskToolkit.UI.Theme;

namespace ITHelpdeskToolkit.UI.Controls
{
    public enum ButtonStyle
    {
        Primary,
        Secondary,
        Danger,
        Success
    }

    public class ModernButton : Button
    {
        private bool _isHovered;
        private bool _isPressed;
        private ButtonStyle _style = ButtonStyle.Secondary;

        public ButtonStyle Style
        {
            get => _style;
            set
            {
                _style = value;
                Invalidate();
            }
        }

        public int BorderRadius { get; set; } = 6;

        public ModernButton()
        {
            DoubleBuffered = true;
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            Height = 36;
            Cursor = Cursors.Hand;
            Padding = new Padding(12, 0, 12, 0);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _isHovered = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _isHovered = false;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs mevent)
        {
            base.OnMouseDown(mevent);
            _isPressed = true;
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs mevent)
        {
            base.OnMouseUp(mevent);
            _isPressed = false;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            pevent.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            (Color baseBg, Color hoverBg, Color pressBg, Color textColor) = Style switch
            {
                ButtonStyle.Primary => (DarkColors.Primary, DarkColors.PrimaryHover, Color.FromArgb(29, 78, 216), Color.White),
                ButtonStyle.Danger => (DarkColors.Danger, Color.FromArgb(220, 38, 38), Color.FromArgb(185, 28, 28), Color.White),
                ButtonStyle.Success => (DarkColors.Success, Color.FromArgb(5, 150, 105), Color.FromArgb(4, 120, 87), Color.White),
                _ => (DarkColors.Secondary, DarkColors.SecondaryHover, Color.FromArgb(30, 41, 59), DarkColors.TextMain)
            };

            if (!Enabled)
            {
                baseBg = Color.FromArgb(40, 50, 65);
                textColor = DarkColors.TextDim;
            }

            Color currentBg = _isPressed ? pressBg : (_isHovered ? hoverBg : baseBg);

            Rectangle rect = new(0, 0, Width - 1, Height - 1);
            using GraphicsPath path = GetRoundedPath(rect, BorderRadius);

            using (SolidBrush bgBrush = new(currentBg))
            {
                pevent.Graphics.FillPath(bgBrush, path);
            }

            TextRenderer.DrawText(
                pevent.Graphics,
                Text,
                Font,
                ClientRectangle,
                textColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter
            );
        }

        private static GraphicsPath GetRoundedPath(Rectangle rect, int radius)
        {
            GraphicsPath path = new();
            if (radius <= 0)
            {
                path.AddRectangle(rect);
                return path;
            }

            int diameter = radius * 2;
            Rectangle arc = new(rect.X, rect.Y, diameter, diameter);

            path.AddArc(arc, 180, 90);
            arc.X = rect.Right - diameter;
            path.AddArc(arc, 270, 90);
            arc.Y = rect.Bottom - diameter;
            path.AddArc(arc, 0, 90);
            arc.X = rect.X;
            path.AddArc(arc, 90, 90);

            path.CloseFigure();
            return path;
        }
    }
}
