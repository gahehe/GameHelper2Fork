$ErrorActionPreference = 'Stop'
Add-Type -TypeDefinition @"
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
public static class Scan {
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
    long addr = 0;
    var mbi = new MBI();
    int mbiLen = Marshal.SizeOf(typeof(MBI));
    while (hits.Count < maxHits) {
      int q = VirtualQueryEx(H, new IntPtr(addr), out mbi, mbiLen);
      if (q == 0) break;
      long baseAddr = mbi.BaseAddress.ToInt64();
      long size = mbi.RegionSize.ToInt64();
      if (size <= 0) break;
      bool commit = mbi.State == 0x1000;
      bool readable = (mbi.Protect & 0xEE) != 0; // not noaccess/guard
      if (commit && readable && size <= 64L * 1024 * 1024) {
        var buf = new byte[size];
        int n;
        if (ReadProcessMemory(H, mbi.BaseAddress, buf, (int)size, out n) && n > needle.Length) {
          for (int i = 0; i + needle.Length <= n; i++) {
            bool ok = true;
            for (int k = 0; k < needle.Length; k++) if (buf[i + k] != needle[k]) { ok = false; break; }
            if (ok) {
              hits.Add(baseAddr + i);
              if (hits.Count >= maxHits) break;
              i += needle.Length - 1;
            }
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
$h = [Scan]::OpenProcess(0x0410, $false, $proc.Id)
if ($h -eq [IntPtr]::Zero) { 'OPEN_FAIL'; exit 1 }
[Scan]::H = $h
function Needle([string]$s) { [Text.Encoding]::Unicode.GetBytes($s) }
foreach ($s in @('29(+0)','Honour:')) {
  $hits = [Scan]::Find((Needle $s), 8)
  "NEEDLE '$s' hits=$($hits.Count)"
  foreach ($a in $hits) { "  $('{0:X}' -f $a)" }
}
[Scan]::CloseHandle($h) | Out-Null
'DONE'
