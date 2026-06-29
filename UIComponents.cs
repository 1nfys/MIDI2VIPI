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
        public int Radius = 0;
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
            e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            e.Graphics.Clear(UIHelper.GetSolidBg(Parent));
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var br = new SolidBrush(BackColor))
            {
                e.Graphics.FillRectangle(br, r);
            }
            if (ShowBorder)
            {
                using (var pen = new Pen(BorderCol, 2))
                {
                    e.Graphics.DrawRectangle(pen, 1, 1, Width - 3, Height - 3);
                }
            }
        }
    }

    internal class AccentButton : Control
    {
        public Color BaseColor = Theme.Accent;
        public Color HoverCol = Theme.AccentH;
        public int Radius = 0;
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
            e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            e.Graphics.Clear(UIHelper.GetSolidBg(Parent));

            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            
            Color bgCol = Theme.Surface2;
            Color borderCol = Theme.Border;
            Color textCol = Theme.Ink;

            if (!Enabled)
            {
                bgCol = Color.FromArgb(18, 18, 22);
                borderCol = Color.FromArgb(50, 50, 55);
                textCol = Color.FromArgb(90, 90, 95);
            }
            else if (_down)
            {
                bgCol = Color.FromArgb(12, 12, 14);
                borderCol = Theme.Accent;
                textCol = Theme.Accent;
                r.Offset(1, 1);
            }
            else if (_hover)
            {
                bgCol = Color.FromArgb(38, 38, 44);
                borderCol = Theme.Accent;
                textCol = Theme.Accent;
            }

            using (var br = new SolidBrush(bgCol))
            {
                e.Graphics.FillRectangle(br, r);
            }

            using (var pen = new Pen(borderCol, 2))
            {
                e.Graphics.DrawRectangle(pen, 1, 1, Width - 3, Height - 3);
            }

            if (IconImg != null)
            {
                e.Graphics.DrawImage(IconImg, new Rectangle((Width - IconImg.Width) / 2 + (r.X - 0), (Height - IconImg.Height) / 2 + (r.Y - 0), IconImg.Width, IconImg.Height));
            }
            else
            {
                TextRenderer.DrawText(e.Graphics, Text, Font, r, textCol, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }
    }

    internal class DarkTrackBar : Control
    {
        private double _val, _min, _max = 1;
        private bool _drag;
        public Color TrackCol = Color.FromArgb(40, 40, 45);
        public Color ProgCol = Theme.Accent;
        public Color ThumbCol = Color.White;
        public Color ThumbHover = Theme.Accent;

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
        protected override void OnMouseUp(MouseEventArgs e) { _drag = false; Capture = false; Invalidate(); }
        protected override void OnMouseEnter(EventArgs e) { Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            e.Graphics.Clear(UIHelper.GetSolidBg(Parent));
            int y = Height / 2, h = 6, pad = 8;
            double frac = (_max > _min) ? (_val - _min) / (_max - _min) : 0;
            int w = Width - pad * 2, px = pad + (int)(frac * w);

            using (var br = new SolidBrush(TrackCol))
            {
                e.Graphics.FillRectangle(br, pad, y - h / 2, w, h);
            }

            if (px - pad > 0)
            {
                using (var br = new SolidBrush(ProgCol))
                {
                    e.Graphics.FillRectangle(br, pad, y - h / 2, px - pad, h);
                }
            }

            bool isHover = ClientRectangle.Contains(PointToClient(Cursor.Position)) || _drag;
            Color thumbC = isHover ? ThumbHover : ThumbCol;
            Color borderC = isHover ? Theme.Accent : Theme.Border;
            
            int thumbW = 10;
            int thumbH = 16;
            using (var br = new SolidBrush(thumbC))
            {
                e.Graphics.FillRectangle(br, px - thumbW / 2, y - thumbH / 2, thumbW, thumbH);
            }
            using (var pen = new Pen(borderC, 1.5f))
            {
                e.Graphics.DrawRectangle(pen, px - thumbW / 2, y - thumbH / 2, thumbW - 1, thumbH - 1);
            }
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
            e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            e.Graphics.Clear(UIHelper.GetSolidBg(Parent));

            var box = new Rectangle(2, (Height - 16) / 2, 16, 16);
            
            Color bgC = Checked ? Theme.Accent : Theme.Surface2;
            Color borderC = Checked ? Theme.Accent : Theme.Border;

            using (var br = new SolidBrush(bgC))
            {
                e.Graphics.FillRectangle(br, box.X + 1, box.Y + 1, box.Width - 2, box.Height - 2);
            }

            using (var pen = new Pen(borderC, 2))
            {
                e.Graphics.DrawRectangle(pen, box.X + 1, box.Y + 1, box.Width - 2, box.Height - 2);
            }

            if (Checked)
            {
                using (var br = new SolidBrush(Color.Black))
                {
                    e.Graphics.FillRectangle(br, box.X + 5, box.Y + 5, box.Width - 10, box.Height - 10);
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
