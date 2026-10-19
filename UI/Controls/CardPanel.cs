using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ITHelpdeskToolkit.UI.Theme;

namespace ITHelpdeskToolkit.UI.Controls
{
    public class CardPanel : Panel
    {
        public int BorderRadius { get; set; } = 10;
        public Color BorderColor { get; set; } = DarkColors.CardBorder;
        public int BorderWidth { get; set; } = 1;

        public CardPanel()
        {
            DoubleBuffered = true;
            BackColor = DarkColors.CardBg;
            ForeColor = DarkColors.TextMain;
            Padding = new Padding(15);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle rect = new(0, 0, Width - 1, Height - 1);
            using GraphicsPath path = GetRoundedPath(rect, BorderRadius);
            
            using SolidBrush bgBrush = new(BackColor);
            e.Graphics.FillPath(bgBrush, path);

            if (BorderWidth > 0)
            {
                using Pen pen = new(BorderColor, BorderWidth);
                e.Graphics.DrawPath(pen, path);
            }
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

            // Top-left arc
            path.AddArc(arc, 180, 90);

            // Top-right arc
            arc.X = rect.Right - diameter;
            path.AddArc(arc, 270, 90);

            // Bottom-right arc
            arc.Y = rect.Bottom - diameter;
            path.AddArc(arc, 0, 90);

            // Bottom-left arc
            arc.X = rect.X;
            path.AddArc(arc, 90, 90);

            path.CloseFigure();
            return path;
        }
    }
}
