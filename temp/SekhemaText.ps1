$ErrorActionPreference = 'Stop'
Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class Tx {
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
  public static string W(long addr) {
    var b = new byte[32]; int n;
    if (!ReadProcessMemory(H, new IntPtr(addr), b, 32, out n) || n < 32) return "";
    int len = BitConverter.ToInt32(b, 16);
    int cap = BitConverter.ToInt32(b, 24);
    if (len <= 0 || len > 80 || cap < len || cap > 200) return "";
    byte[] raw;
    if (cap <= 8) raw = b;
    else {
      long p = BitConverter.ToInt64(b, 0);
      raw = new byte[len * 2];
      if (!ReadProcessMemory(H, new IntPtr(p), raw, raw.Length, out n)) return "";
    }
    var s = Encoding.Unicode.GetString(raw, 0, Math.Min(len, raw.Length / 2) * 2);
    return s;
  }
  public static long[] Kids(long el) {
    long first = I64(el + 0x10), last = I64(el + 0x18);
    if (first < 0x10000 || last < first) return new long[0];
    int n = (int)((last - first) / 8);
    if (n < 0 || n > 32) return new long[0];
    var a = new long[n];
    for (int i = 0; i < n; i++) a[i] = I64(first + 8L * i);
    return a;
  }
}
"@
$proc = Get-Process -Name PathOfExile | Select-Object -First 1
[Tx]::H = [Tx]::OpenProcess(0x0410, $false, $proc.Id)
$leaf = [int64]0x48E26536430
"water leaf text360='$([Tx]::W($leaf + 0x360))' text490='$([Tx]::W($leaf + 0x490))' w=$([math]::Round([Tx]::F32($leaf + 0x270),1))"
$slot = [Tx]::I64($leaf + 0xB8)
$hud = [Tx]::I64($slot + 0xB8)
"slot $('{0:X}' -f $slot) hud $('{0:X}' -f $hud)"
$hk = [Tx]::Kids($hud)
"hud kids $($hk.Length)"
for ($i = 0; $i -lt $hk.Length; $i++) {
  $sk = [Tx]::Kids($hk[$i])
  $parts = @()
  for ($j = 0; $j -lt $sk.Length; $j++) {
    $t = [Tx]::W($sk[$j] + 0x360)
    $ww = [math]::Round([Tx]::F32($sk[$j] + 0x270), 1)
    $parts += "[$j]w=$ww '$t'"
  }
  $sw = [math]::Round([Tx]::F32($hk[$i] + 0x270), 1)
  "hud[$i] slotW=$sw $($parts -join ' | ')"
}
[Tx]::CloseHandle([Tx]::H) | Out-Null
'DONE'
