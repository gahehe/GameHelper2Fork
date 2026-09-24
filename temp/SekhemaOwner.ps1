$ErrorActionPreference = 'Stop'
Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public static class Own {
  [DllImport("kernel32.dll")] public static extern IntPtr OpenProcess(uint a, bool i, int pid);
  [DllImport("kernel32.dll")] public static extern bool ReadProcessMemory(IntPtr h, IntPtr addr, byte[] buf, int size, out int read);
  [DllImport("kernel32.dll")] public static extern bool CloseHandle(IntPtr h);
  public static IntPtr H;
  public static long I64(long addr) {
    var b = new byte[8]; int n;
    if (!ReadProcessMemory(H, new IntPtr(addr), b, 8, out n) || n != 8) return 0;
    return BitConverter.ToInt64(b, 0);
  }
  public static float F32(long addr) {
    var b = new byte[4]; int n;
    if (!ReadProcessMemory(H, new IntPtr(addr), b, 4, out n) || n != 4) return 0;
    return BitConverter.ToSingle(b, 0);
  }
}
"@
$proc = Get-Process -Name PathOfExile | Select-Object -First 1
[Own]::H = [Own]::OpenProcess(0x0410, $false, $proc.Id)
foreach ($s in @(0x48E26536790, 0x48E265368C0, 0x48E26536968)) {
  "STRING $('{0:X}' -f $s) len=$([Own]::I64($s + 16)) cap=$([Own]::I64($s + 24))"
  $found = $false
  for ($off = 0x20; $off -le 0x500; $off += 8) {
    $el = $s - $off
    $self = [Own]::I64($el + 8)
    if ($self -ne $el) { continue }
    $first = [Own]::I64($el + 0x10)
    $last = [Own]::I64($el + 0x18)
    $n = -1
    if ($first -gt 0x10000 -and $last -ge $first) { $n = [int](($last - $first) / 8) }
    $w = [Own]::F32($el + 0x270)
    "  ELEMENT off=0x$('{0:X}' -f $off) el=$('{0:X}' -f $el) kids=$n w=$([math]::Round($w,1))"
    $found = $true
  }
  if (-not $found) { '  no self-pointer owner in 0x500' }
}
[Own]::CloseHandle([Own]::H) | Out-Null
'DONE'
