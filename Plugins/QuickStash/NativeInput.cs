// <copyright file="NativeInput.cs" company="None">
// Copyright (c) None. All rights reserved.
// </copyright>

namespace QuickStash
{
    using System;
    using System.Runtime.InteropServices;
    using System.Threading;

    /// <summary>
    ///     Cursor move plus Ctrl+left-click. GameHelper core has no mouse helper.
    /// </summary>
    internal static class NativeInput
    {
        private const uint InputMouse = 0;
        private const uint InputKeyboard = 1;
        private const uint KeyeventfKeyup = 0x0002;
        private const uint KeyeventfScancode = 0x0008;
        private const uint MouseeventfLeftdown = 0x0002;
        private const uint MouseeventfLeftup = 0x0004;
        private const uint MapvkVkToVsc = 0;
        private const ushort VkControl = 0x11;
        private const int VkLButton = 0x01;

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public nint dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public nint dwExtraInfo;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion
        {
            [FieldOffset(0)] public MOUSEINPUT mi;
            [FieldOffset(0)] public KEYBDINPUT ki;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public uint type;
            public InputUnion u;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        private static extern uint MapVirtualKey(uint uCode, uint uMapType);

        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int x, int y);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        [StructLayout(LayoutKind.Sequential)]
        internal struct POINT
        {
            public int X;
            public int Y;
        }

        internal static bool TryGetCursor(out POINT point) => GetCursorPos(out point);

        internal static bool MoveCursor(int x, int y) => SetCursorPos(x, y);

        /// <summary>
        ///     Moves onto <paramref name="x"/>,<paramref name="y"/>, Ctrl+clicks, and only then waits
        ///     <paramref name="afterClickMs"/>. The cursor is not moved by this method after the click starts.
        /// </summary>
        internal static bool CtrlLeftClickAt(int x, int y, int afterClickMs)
        {
            var scan = (ushort)MapVirtualKey(VkControl, MapvkVkToVsc);
            if (scan == 0 || !WaitUntilCursorAt(x, y))
            {
                return false;
            }

            // One frame so the game sees the hover before the button goes down.
            Thread.Sleep(32);
            if (!CursorIsAt(x, y))
            {
                return false;
            }

            try
            {
                SendOne(Key(scan, up: false));
                SendOne(Mouse(MouseeventfLeftdown));
                Thread.Sleep(Random.Shared.Next(20, 41));
                SendOne(Mouse(MouseeventfLeftup));
                WaitUntil(static () => !LeftButtonDown(), 40);
            }
            finally
            {
                if (LeftButtonDown())
                {
                    SendOne(Mouse(MouseeventfLeftup));
                    WaitUntil(static () => !LeftButtonDown(), 40);
                }

                SendOne(Key(scan, up: true));
            }

            // Click is up. Stay on this cell until the gap elapses, then the caller may move.
            if (afterClickMs > 0)
            {
                Thread.Sleep(afterClickMs);
            }

            return true;
        }

        private static bool WaitUntilCursorAt(int x, int y)
        {
            if (!SetCursorPos(x, y))
            {
                return false;
            }

            var deadline = Environment.TickCount64 + 50;
            do
            {
                if (CursorIsAt(x, y))
                {
                    return true;
                }

                Thread.Sleep(1);
            }
            while (Environment.TickCount64 < deadline);

            return false;
        }

        private static bool CursorIsAt(int x, int y)
        {
            return GetCursorPos(out var point)
                && Math.Abs(point.X - x) <= 2
                && Math.Abs(point.Y - y) <= 2;
        }

        private static bool LeftButtonDown() => (GetAsyncKeyState(VkLButton) & 0x8000) != 0;

        private static void WaitUntil(Func<bool> done, int timeoutMs)
        {
            var deadline = Environment.TickCount64 + timeoutMs;
            while (!done() && Environment.TickCount64 < deadline)
            {
                Thread.Sleep(1);
            }
        }

        private static bool SendOne(INPUT input)
        {
            var packet = new[] { input };
            return SendInput(1, packet, Marshal.SizeOf<INPUT>()) == 1;
        }

        private static INPUT Key(ushort scan, bool up) => new()
        {
            type = InputKeyboard,
            u = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wScan = scan,
                    dwFlags = KeyeventfScancode | (up ? KeyeventfKeyup : 0),
                },
            },
        };

        private static INPUT Mouse(uint flags) => new()
        {
            type = InputMouse,
            u = new InputUnion { mi = new MOUSEINPUT { dwFlags = flags } },
        };
    }
}
