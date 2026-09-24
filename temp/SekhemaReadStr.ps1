$ErrorActionPreference = 'Stop'
Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class Rd {
  [DllImport("kernel32.dll", SetLastError=true)]
  public static extern IntPtr OpenProcess(uint access, bool inherit, int pid);
  [DllImport("kernel32.dll", SetLastError=true)]
  public static extern bool ReadProcessMemory(IntPtr h, IntPtr addr, byte[] buf, int size, out int read);
  [DllImport("kernel32.dll")]
  public static extern bool CloseHandle(IntPtr h);
  public static IntPtr H;
  public static string U(long addr, int chars) {
    var buf = new byte[chars * 2];
    int n;
    if (!ReadProcessMemory(H, new IntPtr(addr), buf, buf.Length, out n) || n < 4) return "UNREAD";
    int z = 0;
    while (z + 1 < n && (buf[z] != 0 || buf[z + 1] != 0)) z += 2;
    return Encoding.Unicode.GetString(buf, 0, z);
  }
}
"@
$proc = Get-Process -Name PathOfExile | Select-Object -First 1
$h = [Rd]::OpenProcess(0x0410, $false, $proc.Id)
[Rd]::H = $h
foreach ($a in @(0x48E26536790, 0x48E265368C0, 0x48E26536968)) {
  $s = [Rd]::U($a, 80)
  "AT $('{0:X}' -f $a) [$s]"
}
[Rd]::CloseHandle($h) | Out-Null
'DONE'
