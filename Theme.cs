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
    internal static class Theme
    {
        public static readonly Color BG = C(14, 14, 20);
        public static readonly Color Surface = C(23, 23, 31);
        public static readonly Color Surface2 = C(30, 30, 40);
        public static readonly Color Border = C(37, 37, 47);
        public static readonly Color Accent = C(224, 82, 82);
        public static readonly Color AccentH = C(192, 58, 58);
        public static readonly Color Ink = C(216, 221, 230);
        public static readonly Color InkDim = C(107, 114, 128);
        public static readonly Color Amber = C(217, 119, 6);
        public static readonly Color Warn = C(248, 113, 113);

        public static readonly Font Title = new Font("Segoe UI", 18f, FontStyle.Bold);
        public static readonly Font Main = new Font("Segoe UI", 9.5f);
        public static readonly Font Bold = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        public static readonly Font Mono = new Font("Cascadia Mono", 9f, FontStyle.Bold);
        public static readonly Font MonoSm = new Font("Cascadia Mono", 8.5f);

        private static Color C(int r, int g, int b)
        {
            return Color.FromArgb(r, g, b);
        }
    }
}
