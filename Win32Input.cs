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
    internal static class Win32Input
    {
        private struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Explicit, Size = 28)]
        private struct INPUTUNION
        {
            [FieldOffset(0)]
            public KEYBDINPUT ki;
        }

        private struct INPUT
        {
            public uint type;
            public INPUTUNION u;
        }

        private const uint INPUT_KEYBOARD = 1u;
        private const uint KEYEVENTF_SCANCODE = 8u;
        private const uint KEYEVENTF_KEYUP = 2u;

        [DllImport("user32.dll")]
        private static extern uint SendInput(uint n, INPUT[] inputs, int size);

        [DllImport("user32.dll")]
        private static extern uint MapVirtualKeyW(uint code, uint mapType);

        [DllImport("user32.dll")]
        private static extern short VkKeyScanW(char ch);

        [DllImport("user32.dll")]
        public static extern short GetAsyncKeyState(int vk);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr pid);

        [DllImport("user32.dll")]
        public static extern IntPtr GetKeyboardLayout(uint thread);

        private static void Send(ushort scan, bool press)
        {
            var inp = new INPUT { type = INPUT_KEYBOARD };
            inp.u.ki = new KEYBDINPUT
            {
                wVk = 0,
                wScan = scan,
                dwFlags = KEYEVENTF_SCANCODE | (press ? 0u : KEYEVENTF_KEYUP),
                time = 0,
                dwExtraInfo = IntPtr.Zero
            };
            SendInput(1u, new INPUT[] { inp }, Marshal.SizeOf(typeof(INPUT)));
        }

        public static void PressKeyVk(char ch)
        {
            short res = VkKeyScanW(ch);
            uint code = (uint)(res & 0xFF);
            bool shifted = ((res >> 8) & 1) != 0;

            ushort scan = (ushort)MapVirtualKeyW(code, 0u);
            ushort shiftScan = (ushort)MapVirtualKeyW(16u, 0u);

            if (shifted) Send(shiftScan, true);
            Send(scan, true);
            Send(scan, false);
            if (shifted) Send(shiftScan, false);
        }

        public static int GetVk(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return 189;

            string s = key.Trim().ToLowerInvariant();
            switch (s)
            {
                case "-": return 189;
                case "=": return 187;
                case "[": return 219;
                case "]": return 221;
                case ";": return 186;
                case "'": return 222;
                case ",": return 188;
                case ".": return 190;
                case "/": return 191;
                case "\\": return 220;
                case "`": return 192;
                case "space": return 32;
                case "enter": return 13;
                case "tab": return 9;
                case "escape": return 27;
                case "backspace": return 8;
                default:
                    int fn;
                    if (s.Length > 1 && s[0] == 'f' && int.TryParse(s.Substring(1), out fn) && fn >= 1 && fn <= 12)
                    {
                        return 111 + fn;
                    }
                    if (s.Length == 1 && char.IsLetterOrDigit(s[0]))
                    {
                        return char.ToUpperInvariant(s[0]);
                    }
                    return 189;
            }
        }
    }
}
