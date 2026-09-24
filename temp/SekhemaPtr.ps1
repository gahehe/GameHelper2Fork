$ErrorActionPreference = 'Stop'
Add-Type -TypeDefinition @"
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
public static class PtrScan {
  [DllImport("kernel32.dll", SetLastError=true)]
  public static extern IntPtr OpenProcess(uint access, bool inherit, int pid);
  [DllImport("kernel32.dll", SetLastError=true)]
  public static extern bool ReadProcessMemory(IntPtr h, IntPtr addr, byte[] buf, int size, out int read);
  [DllImport("kernel32.dll")]
  public static extern int VirtualQueryEx(IntPtr h, IntPtr addr, out MBI mbi, int len);
  [DllImport("kernel32.dll")]
  public static extern bool CloseHandle(IntPtr h);
  [StructLayout(LayoutKind.Sequential)]
  public struct MBI {
    public IntPtr BaseAddress;
    public IntPtr AllocationBase;
    public uint AllocationProtect;
    public IntPtr RegionSize;
    public uint State;
    public uint Protect;
    public uint Type;
  }
  public static IntPtr H;
  public static List<long> Find(byte[] needle, int maxHits) {
    var hits = new List<long>();
    long addr = 0x10000;
    var mbi = new MBI();
    int mbiLen = Marshal.SizeOf(typeof(MBI));
    while (hits.Count < maxHits && addr < 0x7FFFFFFFFFFF) {
      if (VirtualQueryEx(H, new IntPtr(addr), out mbi, mbiLen) == 0) break;
      long baseAddr = mbi.BaseAddress.ToInt64();
      long size = mbi.RegionSize.ToInt64();
      if (size <= 0) break;
      bool commit = mbi.State == 0x1000;
      uint p = mbi.Protect & 0xFF;
      bool readable = p == 0x02 || p == 0x04 || p == 0x08 || p == 0x20 || p == 0x40 || p == 0x80;
      if (commit && readable && size > 0 && size <= 32L * 1024 * 1024) {
        int chunk = 1 << 20;
        for (long off = 0; off < size && hits.Count < maxHits; off += chunk) {
          int take = (int)Math.Min(chunk, size - off);
          var buf = new byte[take];
          int n;
          if (!ReadProcessMemory(H, new IntPtr(baseAddr + off), buf, take, out n) || n < needle.Length) continue;
          for (int i = 0; i + needle.Length <= n; i++) {
            bool ok = true;
            for (int k = 0; k < needle.Length; k++) if (buf[i + k] != needle[k]) { ok = false; break; }
            if (!ok) continue;
            hits.Add(baseAddr + off + i);
            if (hits.Count >= maxHits) break;
          }
        }
      }
      long next = baseAddr + size;
      if (next <= addr) break;
      addr = next;
    }
    return hits;
  }
}
"@
$proc = Get-Process -Name PathOfExile | Select-Object -First 1
$h = [PtrScan]::OpenProcess(0x0410, $false, $proc.Id)
[PtrScan]::H = $h
$needle = [byte[]](0x90, 0x67, 0x53, 0x65, 0xE2, 0x48, 0x00, 0x00)
$hits = [PtrScan]::Find($needle, 12)
"PTR hits=$($hits.Count)"
foreach ($a in $hits) { "  $('{0:X}' -f $a)" }
[PtrScan]::CloseHandle($h) | Out-Null
'DONE'
