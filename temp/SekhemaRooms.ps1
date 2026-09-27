$ErrorActionPreference = 'Stop'
Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class Mem3 {
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
$h = [Mem3]::OpenProcess(0x0410, $false, 27560)
if ($h -eq [IntPtr]::Zero) { Write-Output 'OPEN_FAIL'; exit 1 }
[Mem3]::H = $h

function Kids([long]$el) {
  $first = [Mem3]::I64($el + 0x10)
  $last = [Mem3]::I64($el + 0x18)
  if ($first -lt 0x10000 -or $last -lt $first) { return @() }
  $n = [int](($last - $first) / 8)
  if ($n -lt 0 -or $n -gt 32) { return @() }
  $list = @()
  for ($i = 0; $i -lt $n; $i++) { $list += [Mem3]::I64($first + 8L * $i) }
  return $list
}

function Vis([long]$el) { return (([Mem3]::U32($el + 0x168) -shr 11) -band 1) }

function StringsNear([long]$el) {
  $hits = @()
  for ($off = 0x480; $off -le 0x560; $off += 8) {
    $p = [Mem3]::I64($el + $off)
    if ($p -lt 0x10000) { continue }
    $s1 = [Mem3]::Utf16([Mem3]::I64($p), 64)
    $s2 = [Mem3]::Utf16([Mem3]::I64($p + 0x28), 64)
    $direct = [Mem3]::Utf16($p, 48)
    $chunk = @()
    if ($s1 -match 'Ruins|Gauntlet|Arena|Explore|Boss|Treasure|Affliction|Sanctum|Ritual|Merchant') { $chunk += "row0='$s1'" }
    if ($s2 -match 'Ruins|Gauntlet|Arena|Explore|Boss|Treasure|Affliction|Sanctum|Ritual|Merchant') { $chunk += "row28='$s2'" }
    if ($direct -match 'Ruins|Gauntlet|Arena|Explore|Boss|Treasure|Affliction|Sanctum|Ritual') { $chunk += "direct='$direct'" }
    if ($chunk.Count -gt 0) { $hits += ("+$('{0:X}' -f $off) " + ($chunk -join ' ')) }
  }
  return ($hits -join ' ; ')
}

$floor = 0x380D52C1020L
$layers = [Mem3]::I64($floor + 0x30)
$layerEls = Kids $layers
Write-Output ("layers $('{0:X}' -f $layers) n=$($layerEls.Count)")
$fc = Kids $floor
Write-Output ("floor-obj children $($fc.Count)")
for ($i = 0; $i -lt $fc.Count; $i++) {
  Write-Output ("  child[$i] $('{0:X}' -f $fc[$i]) kids=$((Kids $fc[$i]).Count) sameAsLayers=$($fc[$i] -eq $layers)")
}

for ($li = 0; $li -lt $layerEls.Count; $li++) {
  $layer = $layerEls[$li]
  $lx = [Mem3]::F32($layer + 0x100); $ly = [Mem3]::F32($layer + 0x104)
  $rooms = Kids $layer
  Write-Output ("L$li layer=$('{0:X}' -f $layer) vis=$(Vis $layer) rel=$([int]$lx),$([int]$ly) rooms=$($rooms.Count)")
  for ($ri = 0; $ri -lt $rooms.Count; $ri++) {
    $r = $rooms[$ri]
    $rx = [Mem3]::F32($r + 0x100); $ry = [Mem3]::F32($r + 0x104)
    $rw = [Mem3]::F32($r + 0x270); $rh = [Mem3]::F32($r + 0x274)
    $rk = Kids $r
    $nested = @()
    foreach ($c in $rk) {
      $s = StringsNear $c
      if ($s.Length -gt 0) { $nested += $s }
    }
    $own = StringsNear $r
    Write-Output ("  [$ri] $('{0:X}' -f $r) vis=$(Vis $r) rel=$([int]$rx),$([int]$ry) size=$([int]$rw)x$([int]$rh) kids=$($rk.Count)")
    if ($own.Length -gt 0) { Write-Output "     own $own" }
    if ($nested.Count -gt 0) { Write-Output "     nested $($nested -join ' || ')" }
  }
}

[Mem3]::CloseHandle($h) | Out-Null
Write-Output 'DONE'
