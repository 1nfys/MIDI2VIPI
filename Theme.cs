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

using System.Drawing.Text;
using System.Reflection;

namespace MIDI2VIPI
{
    internal static class Theme
    {
        [DllImport("gdi32.dll")]
        private static extern IntPtr AddFontMemResourceEx(IntPtr pbFont, uint cbFont, IntPtr pdv, [In] ref uint pcFonts);

        [DllImport("gdi32.dll", CharSet = CharSet.Auto)]
        private static extern int AddFontResourceEx(string lpszFilename, uint fl, IntPtr pdv);

        private static PrivateFontCollection _pfc;
        private static FontFamily _ithacaFamily;

        public static readonly Color BG = Color.Black;
        public static readonly Color Surface = C(14, 14, 16);
        public static readonly Color Surface2 = C(24, 24, 28);
        public static readonly Color Border = C(200, 200, 200);
        public static readonly Color Accent = Color.FromArgb(249, 255, 16);
        public static readonly Color AccentH = Color.FromArgb(255, 255, 100);
        public static readonly Color Ink = C(240, 240, 244);
        public static readonly Color InkDim = C(150, 150, 155);
        public static readonly Color Amber = Color.FromArgb(249, 255, 16);
        public static readonly Color Warn = C(255, 80, 80);

        public static readonly Font Title;
        public static readonly Font Main;
        public static readonly Font Bold;
        public static readonly Font Mono;
        public static readonly Font MonoSm;
        public static readonly Font MiniBold;
        public static readonly Font WarnFont;

        static Theme()
        {
            _pfc = new PrivateFontCollection();
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                using (Stream stream = assembly.GetManifestResourceStream("Ithaca-LVB75.ttf"))
                {
                    if (stream != null)
                    {
                        byte[] data = new byte[stream.Length];
                        stream.Read(data, 0, data.Length);
                        IntPtr ptr = Marshal.AllocCoTaskMem(data.Length);
                        Marshal.Copy(data, 0, ptr, data.Length);
                        _pfc.AddMemoryFont(ptr, data.Length);
                        
                        // Register with GDI for TextRenderer
                        uint dummy = 0;
                        AddFontMemResourceEx(ptr, (uint)data.Length, IntPtr.Zero, ref dummy);
                        
                        Marshal.FreeCoTaskMem(ptr);
                        _ithacaFamily = _pfc.Families[0];
                    }
                    else if (File.Exists("Ithaca-LVB75.ttf"))
                    {
                        // Register file with GDI for TextRenderer (FR_PRIVATE = 0x10)
                        AddFontResourceEx("Ithaca-LVB75.ttf", 0x10, IntPtr.Zero);
                        _pfc.AddFontFile("Ithaca-LVB75.ttf");
                        _ithacaFamily = _pfc.Families[0];
                    }
                }
            }
            catch { }

            if (_ithacaFamily != null)
            {
                Title = new Font(_ithacaFamily, 24f, FontStyle.Regular);
                Main = new Font(_ithacaFamily, 13f, FontStyle.Regular);
                Bold = new Font(_ithacaFamily, 13f, FontStyle.Regular);
                Mono = new Font(_ithacaFamily, 13f, FontStyle.Regular);
                MonoSm = new Font(_ithacaFamily, 12f, FontStyle.Regular);
                MiniBold = new Font(_ithacaFamily, 11f, FontStyle.Regular);
                WarnFont = new Font(_ithacaFamily, 13f, FontStyle.Regular);
            }
            else
            {
                Title = new Font("Consolas", 15f, FontStyle.Bold);
                Main = new Font("Consolas", 9f);
                Bold = new Font("Consolas", 9f, FontStyle.Bold);
                Mono = new Font("Consolas", 8.5f, FontStyle.Bold);
                MonoSm = new Font("Consolas", 8f);
                MiniBold = new Font("Consolas", 7f, FontStyle.Bold);
                WarnFont = new Font("Consolas", 8.5f, FontStyle.Bold);
            }
        }

        private static Color C(int r, int g, int b)
        {
            return Color.FromArgb(r, g, b);
        }
    }
}
