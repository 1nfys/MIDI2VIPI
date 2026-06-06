using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System;

namespace MIDI2VIPI
{
    public static class UIHelper
    {
        [DllImport("dwmapi.dll", PreserveSig = true)]
        public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        public static Color GetSolidBg(Control c)
        {
            while (c != null)
            {
                if (c.BackColor != Color.Transparent && c.BackColor.A == 255) return c.BackColor;
                c = c.Parent;
            }
            return Theme.BG;
        }

        public static GraphicsPath RoundRect(Rectangle r, int rad)
        {
            var gp = new GraphicsPath();
            if (rad <= 0) { gp.AddRectangle(r); return gp; }
            int d = rad * 2;
            if (d > r.Width) d = r.Width;
            if (d > r.Height) d = r.Height;
            gp.AddArc(r.X, r.Y, d, d, 180, 90);
            gp.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            gp.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            gp.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            gp.CloseFigure();
            return gp;
        }
    }

    internal class DarkPanel : Panel
    {
        public int Radius = 12;
        public Color BorderCol = Theme.Border;
        public bool ShowBorder = true;

        public DarkPanel()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaintBackground(PaintEventArgs e) { }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.Clear(UIHelper.GetSolidBg(Parent));
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = UIHelper.RoundRect(r, Radius))
            {
                using (var br = new SolidBrush(BackColor)) e.Graphics.FillPath(br, path);
                if (ShowBorder) using (var pen = new Pen(BorderCol)) e.Graphics.DrawPath(pen, path);
            }
        }
    }

    internal class AccentButton : Control
    {
        public Color BaseColor = Theme.Accent;
        public Color HoverCol = Theme.AccentH;
        public int Radius = 6;
        public Image IconImg;
        private bool _hover, _down;

        public event EventHandler ClickEvent;

        public AccentButton()
        {
            Font = Theme.Bold;
            ForeColor = Color.White;
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; _down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { _down = true; Invalidate(); } base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _down = false;
                Invalidate();
                if (ClientRectangle.Contains(e.Location) && ClickEvent != null) ClickEvent(this, EventArgs.Empty);
            }
            base.OnMouseUp(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.Clear(UIHelper.GetSolidBg(Parent));

            var col = Enabled ? (_down ? HoverCol : (_hover ? HoverCol : BaseColor)) : Theme.Surface2;
            using (var path = UIHelper.RoundRect(new Rectangle(0, 0, Width - 1, Height - 1), Radius))
            using (var br = new SolidBrush(col))
            {
                e.Graphics.FillPath(br, path);
                if (IconImg != null)
                {
                    e.Graphics.DrawImage(IconImg, new Rectangle((Width - IconImg.Width) / 2, (Height - IconImg.Height) / 2, IconImg.Width, IconImg.Height));
                }
                else
                {
                    TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, ForeColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
            }
        }
    }

    internal class DarkTrackBar : Control
    {
        private double _val, _min, _max = 1;
        private bool _drag;
        public Color TrackCol = Theme.Surface2;
        public Color ProgCol = Theme.Accent;
        public Color ThumbCol = Theme.Accent;
        public Color ThumbHover = Theme.AccentH;

        public event Action<double> ValueChanged;

        public double Value
        {
            get { return _val; }
            set { _val = Math.Max(_min, Math.Min(_max, value)); Invalidate(); }
        }
        public double Minimum { get { return _min; } set { _min = value; } }
        public double Maximum { get { return _max; } set { _max = value; } }

        public DarkTrackBar()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Height = 20;
            Cursor = Cursors.Hand;
        }

        private double PosToVal(int x) { return _min + (_max - _min) * Math.Max(0, Math.Min(1.0, (x - 8.0) / (Width - 16.0))); }

        protected override void OnMouseDown(MouseEventArgs e) { _drag = true; Value = PosToVal(e.X); if (ValueChanged != null) ValueChanged(Value); Capture = true; }
        protected override void OnMouseMove(MouseEventArgs e) { if (_drag) { Value = PosToVal(e.X); if (ValueChanged != null) ValueChanged(Value); } }
        protected override void OnMouseUp(MouseEventArgs e) { _drag = false; Capture = false; }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.Clear(UIHelper.GetSolidBg(Parent));
            int y = Height / 2, h = 4, pad = 8;
            double frac = (_max > _min) ? (_val - _min) / (_max - _min) : 0;
            int w = Width - pad * 2, px = pad + (int)(frac * w);

            using (var path = UIHelper.RoundRect(new Rectangle(pad, y - h / 2, w, h), h / 2))
            using (var br = new SolidBrush(TrackCol)) e.Graphics.FillPath(br, path);

            if (px - pad > 0)
            {
                using (var path = UIHelper.RoundRect(new Rectangle(pad, y - h / 2, px - pad, h), h / 2))
                using (var br = new SolidBrush(ProgCol)) e.Graphics.FillPath(br, path);
            }
            using (var br = new SolidBrush(_drag ? ThumbHover : ThumbCol)) e.Graphics.FillEllipse(br, px - 6, y - 6, 12, 12);
        }
    }

    internal class DarkCheckBox : CheckBox
    {
        public DarkCheckBox()
        {
            FlatStyle = FlatStyle.Flat; Appearance = Appearance.Normal;
            ForeColor = Theme.Ink; Font = Theme.Main; BackColor = Color.Transparent;
            FlatAppearance.BorderSize = 0; FlatAppearance.CheckedBackColor = Color.Transparent;
            FlatAppearance.MouseDownBackColor = Color.Transparent; FlatAppearance.MouseOverBackColor = Color.Transparent;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.Clear(UIHelper.GetSolidBg(Parent));

            var box = new Rectangle(2, (Height - 16) / 2, 16, 16);
            using (var path = UIHelper.RoundRect(box, 3))
            {
                using (var br = new SolidBrush(Checked ? Theme.Accent : Theme.Surface2)) e.Graphics.FillPath(br, path);
                using (var pen = new Pen(Checked ? Theme.AccentH : Theme.Border)) e.Graphics.DrawPath(pen, path);
            }

            if (Checked)
            {
                using (var pen = new Pen(Color.White, 2))
                {
                    e.Graphics.DrawLine(pen, box.X + 4, box.Y + 8, box.X + 7, box.Y + 11);
                    e.Graphics.DrawLine(pen, box.X + 7, box.Y + 11, box.X + 12, box.Y + 5);
                }
            }
            TextRenderer.DrawText(e.Graphics, Text, Font, new Rectangle(26, 0, Width - 28, Height), ForeColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
        }

        public override Size GetPreferredSize(Size proposedSize)
        {
            var s = TextRenderer.MeasureText(Text, Font);
            return new Size(s.Width + 30, Math.Max(24, s.Height + 8));
        }
    }
}
