$ErrorActionPreference = 'Stop'
Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class Mem4 {
  [DllImport("kernel32.dll", SetLastError=true)]
  public static extern IntPtr OpenProcess(uint access, bool inherit, int pid);
  [DllImport("kernel32.dll", SetLastError=true)]
  public static extern bool ReadProcessMemory(IntPtr h, IntPtr addr, byte[] buf, int size, out int read);
  [DllImport("kernel32.dll")]
  public static extern bool CloseHandle(IntPtr h);
  public static IntPtr H;
  public static byte[] TryRead(long addr, int size) {
    if (addr < 0x10000) return null;
    var buf = new byte[size];
    int n;
    if (!ReadProcessMemory(H, new IntPtr(addr), buf, size, out n) || n != size) return null;
    return buf;
  }
  public static long I64(long addr) { var b = TryRead(addr, 8); return b == null ? 0 : BitConverter.ToInt64(b, 0); }
  public static int I32(long addr) { var b = TryRead(addr, 4); return b == null ? 0 : BitConverter.ToInt32(b, 0); }
  public static uint U32(long addr) { var b = TryRead(addr, 4); return b == null ? 0 : BitConverter.ToUInt32(b, 0); }
  public static float F32(long addr) { var b = TryRead(addr, 4); return b == null ? 0 : BitConverter.ToSingle(b, 0); }
  public static string Utf16(long addr, int maxChars) {
    if (addr < 0x10000) return "";
    var raw = TryRead(addr, maxChars * 2);
    if (raw == null) return "";
    int n = 0;
    while (n < maxChars && BitConverter.ToUInt16(raw, n * 2) != 0) n++;
    if (n == 0) return "";
    return Encoding.Unicode.GetString(raw, 0, n * 2);
  }
}
"@
$h = [Mem4]::OpenProcess(0x0410, $false, 27560)
if ($h -eq [IntPtr]::Zero) { Write-Output 'OPEN_FAIL'; exit 1 }
[Mem4]::H = $h

function Kids([long]$el) {
  $first = [Mem4]::I64($el + 0x10)
  $last = [Mem4]::I64($el + 0x18)
  if ($first -lt 0x10000 -or $last -lt $first) { return @() }
  $n = [int](($last - $first) / 8)
  if ($n -lt 0 -or $n -gt 32) { return @() }
  $list = @()
  for ($i = 0; $i -lt $n; $i++) { $list += [Mem4]::I64($first + 8L * $i) }
  return $list
}
function Fk([long]$w, [int]$off) {
  $row = [Mem4]::I64($w + $off)
  if ($row -lt 0x10000) { return '' }
  $id = [Mem4]::Utf16([Mem4]::I64($row), 48)
  if ($off -eq 0x520) {
    $name = [Mem4]::Utf16([Mem4]::I64($row + 0x28), 48)
    if ($name.Length -gt 0) { return $name }
  }
  return $id
}

$floor = 0x380D52C1020L
$layersEl = [Mem4]::I64($floor + 0x30)
$layerEls = Kids $layersEl
Write-Output ("layers n=$($layerEls.Count) floorKids=$((Kids $floor).Count)")
$centers = @()
for ($li = 0; $li -lt $layerEls.Count; $li++) {
  $layer = $layerEls[$li]
  $lx = [Mem4]::F32($layer + 0x100); $ly = [Mem4]::F32($layer + 0x104)
  $flags = [Mem4]::U32($layer + 0x168)
  $vis = ($flags -shr 11) -band 1
  $rooms = Kids $layer
  Write-Output ("L$li vis=$vis flags=$('{0:X}' -f $flags) rel=$([int]$lx),$([int]$ly) n=$($rooms.Count)")
  for ($ri = 0; $ri -lt $rooms.Count; $ri++) {
    $r = $rooms[$ri]
    $b = [Mem4]::TryRead($r + 0x4F0, 8)
    $lb = if ($b) { $b[4] } else { -1 }
    $ib = if ($b) { $b[5] } else { -1 }
    $rx = [Mem4]::F32($r + 0x100); $ry = [Mem4]::F32($r + 0x104)
    $rw = [Mem4]::F32($r + 0x270); $rh = [Mem4]::F32($r + 0x274)
    $rf = [Mem4]::U32($r + 0x168)
    $rv = ($rf -shr 11) -band 1
    $cx = $lx + $rx + $rw * 0.5
    $cy = $ly + $ry + $rh * 0.5
    $t = Fk $r 0x500
    $rew = Fk $r 0x510
    $a = Fk $r 0x520
    Write-Output ("  [$ri] u8=$lb,$ib vis=$rv flags=$('{0:X}' -f $rf) rel=$([int]$rx),$([int]$ry) size=$([int]$rw)x$([int]$rh) center=$([int]$cx),$([int]$cy) type='$t' rew='$rew' aff='$a'")
    $centers += [pscustomobject]@{ L=$li; I=$ri; X=$cx; Y=$cy; Type=$t }
  }
}

$link = (Kids $floor)[0]
$first = [Mem4]::I64($link + 0x2C8)
$last = [Mem4]::I64($link + 0x2D0)
$count = [int](($last - $first) / 24)
Write-Output ("LINKS count=$count link=$('{0:X}' -f $link)")
$states = @{}
for ($s = 0; $s -lt $count; $s++) {
  $at = $first + $s * 24
  $x1 = [Mem4]::F32($at); $y1 = [Mem4]::F32($at + 4)
  $x2 = [Mem4]::F32($at + 8); $y2 = [Mem4]::F32($at + 12)
  $st = [Mem4]::I32($at + 16); $fl = [Mem4]::I32($at + 20)
  if (-not $states.ContainsKey($st)) { $states[$st] = 0 }
  $states[$st]++
  function Near($x, $y) {
    $best = $null; $bestD = 1e9
    foreach ($c in $centers) {
      $d = [Math]::Sqrt(($c.X - $x)*($c.X - $x) + ($c.Y - $y)*($c.Y - $y))
      if ($d -lt $bestD) { $bestD = $d; $best = $c }
    }
    return "$($best.L).$($best.I):$($best.Type) d=$([int]$bestD)"
  }
  if ($st -eq 3 -or $st -eq 1 -or $s -lt 3) {
    Write-Output ("  seg $s ($([int]$x1),$([int]$y1))->($([int]$x2),$([int]$y2)) state=$st flags=$fl  A=$(Near $x1 $y1)  B=$(Near $x2 $y2)")
  }
}
$states.GetEnumerator() | Sort-Object Name | ForEach-Object { Write-Output ("state $($_.Key) n=$($_.Value)") }
[Mem4]::CloseHandle($h) | Out-Null
Write-Output 'DONE'
