$ErrorActionPreference = 'Stop'
Add-Type -TypeDefinition @"
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
public static class DoorScan {
  [DllImport("kernel32.dll")] public static extern IntPtr OpenProcess(uint a, bool i, int pid);
  [DllImport("kernel32.dll")] public static extern bool ReadProcessMemory(IntPtr h, IntPtr addr, byte[] buf, int size, out int read);
  [DllImport("kernel32.dll")] public static extern int VirtualQueryEx(IntPtr h, IntPtr addr, out MBI m, int len);
  [DllImport("kernel32.dll")] public static extern bool CloseHandle(IntPtr h);
  public static IntPtr H;
  [StructLayout(LayoutKind.Sequential)]
  public struct MBI { public IntPtr Base; public IntPtr Alloc; public uint AllocProt; public IntPtr Size; public uint State; public uint Protect; public uint Type; }
  public static List<string> Find(string text, int max) {
    var needle = Encoding.Unicode.GetBytes(text);
    var hits = new List<string>();
    long addr = 0x10000;
    var m = new MBI();
    int ml = Marshal.SizeOf(typeof(MBI));
    while (hits.Count < max && addr < 0x7FFFFFFFFFFF) {
      if (VirtualQueryEx(H, new IntPtr(addr), out m, ml) == 0) break;
      long b = m.Base.ToInt64();
      long sz = m.Size.ToInt64();
      if (sz <= 0) break;
      uint p = m.Protect & 0xFF;
      bool ok = m.State == 0x1000 && (p == 4 || p == 2 || p == 8 || p == 0x40 || p == 0x20 || p == 0x80) && sz <= 32L * 1024 * 1024;
      if (ok) {
        int chunk = 1 << 20;
        for (long off = 0; off < sz && hits.Count < max; off += chunk) {
          int take = (int)Math.Min(chunk + needle.Length, sz - off);
          var buf = new byte[take];
          int n;
          if (!ReadProcessMemory(H, new IntPtr(b + off), buf, take, out n) || n < needle.Length) continue;
          for (int i = 0; i + needle.Length <= n; i += 2) {
            bool match = true;
            for (int k = 0; k < needle.Length; k++) if (buf[i + k] != needle[k]) { match = false; break; }
            if (!match) continue;
            int s = i;
            while (s > 1 && (buf[s - 2] != 0 || buf[s - 1] != 0) && i - s < 180) s -= 2;
            int e = i;
            while (e + 1 < n && (buf[e] != 0 || buf[e + 1] != 0) && e - i < 220) e += 2;
            hits.Add((b + off + i).ToString("X") + " " + Encoding.Unicode.GetString(buf, s, e - s));
            if (hits.Count >= max) break;
            i += needle.Length;
          }
        }
      }
      long next = b + sz;
      if (next <= addr) break;
      addr = next;
    }
    return hits;
  }
}
"@
$proc = Get-Process -Name PathOfExile -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $proc) { 'NO_PROCESS'; exit 1 }
$h = [DoorScan]::OpenProcess(0x0410, $false, $proc.Id)
if ($h -eq [IntPtr]::Zero) { 'OPEN_FAIL'; exit 1 }
[DoorScan]::H = $h
foreach ($n in @('SanctumLogicDoor','FloorTransitionDoor','SanctumRoomSelectorDoor','SanctumAirlock','KethDoor')) {
  "---- $n"
  $hits = [DoorScan]::Find($n, 4)
  if ($hits.Count -eq 0) { '  none' } else { $hits | ForEach-Object { "  $_" } }
}
[DoorScan]::CloseHandle($h) | Out-Null
'DONE'
