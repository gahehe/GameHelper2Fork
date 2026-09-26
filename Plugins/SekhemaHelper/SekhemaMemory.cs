// <copyright file="SekhemaMemory.cs" company="None">
// Copyright (c) None. All rights reserved.
// </copyright>

namespace SekhemaHelper
{
    using System;
    using System.Runtime.InteropServices;
    using System.Text;

    /// <summary>Reads the game process. GameHelper's memory helpers are internal to that assembly.</summary>
    internal static class SekhemaMemory
    {
        private const uint Access = 0x0410;

        private static IntPtr handle;
        private static uint pid;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint access, bool inherit, uint pid);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool ReadProcessMemory(IntPtr h, IntPtr addr, byte[] buf, int size, out int read);

        [DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr h);

        public static void Bind(uint processId)
        {
            if (processId == pid && handle != IntPtr.Zero)
            {
                return;
            }

            if (handle != IntPtr.Zero)
            {
                CloseHandle(handle);
                handle = IntPtr.Zero;
            }

            pid = processId;
            if (processId != 0)
            {
                handle = OpenProcess(Access, false, processId);
            }
        }

        public static void Close()
        {
            if (handle != IntPtr.Zero)
            {
                CloseHandle(handle);
                handle = IntPtr.Zero;
            }

            pid = 0;
        }

        public static long I64(long address)
        {
            var b = Read(address, 8);
            return b == null ? 0 : BitConverter.ToInt64(b, 0);
        }

        public static int I32(long address)
        {
            var b = Read(address, 4);
            return b == null ? 0 : BitConverter.ToInt32(b, 0);
        }

        public static float F32(long address)
        {
            var b = Read(address, 4);
            return b == null ? 0 : BitConverter.ToSingle(b, 0);
        }

        public static long[] Children(long element)
        {
            var first = I64(element + 0x10);
            var last = I64(element + 0x18);
            if (first < 0x10000 || last < first)
            {
                return [];
            }

            var n = (int)((last - first) / 8);
            if (n is < 0 or > 256)
            {
                return [];
            }

            var list = new long[n];
            for (var i = 0; i < n; i++)
            {
                list[i] = I64(first + (8L * i));
            }

            return list;
        }

        /// <summary>MSVC std::wstring. Short strings are stored in the object.</summary>
        public static string WString(long address)
        {
            var b = Read(address, 32);
            if (b == null)
            {
                return string.Empty;
            }

            var len = BitConverter.ToInt32(b, 16);
            var cap = BitConverter.ToInt32(b, 24);
            if (len <= 0 || len > 200 || cap < len || cap > 200)
            {
                return string.Empty;
            }

            byte[] raw;
            if (cap <= 8)
            {
                raw = b;
            }
            else
            {
                var ptr = BitConverter.ToInt64(b, 0);
                var heap = Read(ptr, len * 2);
                if (heap == null)
                {
                    return string.Empty;
                }

                raw = heap;
            }

            return Encoding.Unicode.GetString(raw, 0, len * 2);
        }

        public static string Utf16(long address, int maxChars = 96)
        {
            if (address < 0x10000)
            {
                return string.Empty;
            }

            var raw = Read(address, maxChars * 2);
            if (raw == null)
            {
                return string.Empty;
            }

            var n = 0;
            while (n < maxChars && BitConverter.ToUInt16(raw, n * 2) != 0)
            {
                n++;
            }

            return Encoding.Unicode.GetString(raw, 0, n * 2);
        }

        private static byte[]? Read(long address, int size)
        {
            if (handle == IntPtr.Zero || address < 0x10000 || size <= 0)
            {
                return null;
            }

            var buf = new byte[size];
            if (!ReadProcessMemory(handle, new IntPtr(address), buf, size, out var n) || n != size)
            {
                return null;
            }

            return buf;
        }
    }
}
