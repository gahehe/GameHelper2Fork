$ErrorActionPreference = 'Stop'
Add-Type -TypeDefinition @"
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
public static class Mem5 {
  [DllImport("kernel32.dll", SetLastError=true)]
  public static extern IntPtr OpenProcess(uint access, bool inherit, int pid);
  [DllImport("kernel32.dll", SetLastError=true)]
  public static extern bool ReadProcessMemory(IntPtr h, IntPtr addr, byte[] buf, int size, out int read);
  [DllImport("kernel32.dll")]
  public static extern bool CloseHandle(IntPtr h);
  public static IntPtr H;
  public static byte[] Read(long addr, int size) {
    var buf = new byte[size]; int n;
    if (addr < 0x10000) return null;
    if (!ReadProcessMemory(H, new IntPtr(addr), buf, size, out n) || n < size) return null;
    return buf;
  }
  public static long I64(long addr) { var b = Read(addr, 8); return b == null ? 0 : BitConverter.ToInt64(b, 0); }
  public static float F32(long addr) { var b = Read(addr, 4); return b == null ? 0 : BitConverter.ToSingle(b, 0); }
  public static long[] Kids(long el) {
    long first = I64(el + 0x10), last = I64(el + 0x18);
    if (first < 0x10000 || last < first) return new long[0];
    int n = (int)((last - first) / 8);
    if (n < 0 || n > 64) return new long[0];
    var a = new long[n];
    for (int i = 0; i < n; i++) a[i] = I64(first + 8L * i);
    return a;
  }
  public static string Hit(byte[] b, string needle) {
    var nb = Encoding.Unicode.GetBytes(needle);
    var hits = new StringBuilder();
    for (int i = 0; i + nb.Length <= b.Length; i += 2) {
      bool ok = true;
      for (int k = 0; k < nb.Length; k++) if (b[i + k] != nb[k]) { ok = false; break; }
      if (ok) hits.Append(" @+" + i.ToString("X"));
    }
    return hits.ToString();
  }
}
"@
$proc = Get-Process -Name PathOfExile | Select-Object -First 1
$h = [Mem5]::OpenProcess(0x0410, $false, $proc.Id)
if ($h -eq [IntPtr]::Zero) { 'OPEN_FAIL'; exit 1 }
[Mem5]::H = $h

$gameUi = [Mem5]::I64(0x48EDA990C10 + 0xB8)
"GAMEUI $('{0:X}' -f $gameUi) self=$('{0:X}' -f ([Mem5]::I64($gameUi + 8)))"
$gk = [Mem5]::Kids($gameUi)
"gameui kids $($gk.Length)"
if ($gk.Length -le 13) { 'NO_HUD'; exit 1 }
$hud = $gk[13]
"HUD $('{0:X}' -f $hud)"

$needles = @('29(+0)','29','1,846','1846','1,860','1860')
$q = New-Object System.Collections.Generic.Queue[object]
$q.Enqueue(@($hud, 'hud'))
$seen = 0
while ($q.Count -gt 0 -and $seen -lt 400) {
  $item = $q.Dequeue()
  $el = [int64]$item[0]
  $path = [string]$item[1]
  $seen++
  $blob = [Mem5]::Read($el, 0x500)
  if ($blob -ne $null) {
    foreach ($n in $needles) {
      $hit = [Mem5]::Hit($blob, $n)
      if ($hit) { "HIT '$n' $path $('{0:X}' -f $el)$hit" }
    }
  }
  $kids = [Mem5]::Kids($el)
  for ($i = 0; $i -lt $kids.Length; $i++) {
    $q.Enqueue(@($kids[$i], "$path[$i]"))
  }
}
"walked $seen"
# also print sizes of hud[5] children for honour fill
$hk = [Mem5]::Kids($hud)
"hud kids $($hk.Length)"
if ($hk.Length -gt 5) {
  $h5 = [Mem5]::Kids($hk[5])
  "honour slot size $([math]::Round([Mem5]::F32($hk[5] + 0x270),1))"
  for ($j = 0; $j -lt $h5.Length; $j++) {
    "  h5[$j] w=$([math]::Round([Mem5]::F32($h5[$j] + 0x270),1))"
  }
}
[Mem5]::CloseHandle($h) | Out-Null
'DONE'
