$ErrorActionPreference = 'Stop'
Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class Mem2 {
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
  public static long I64(long addr) {
    var b = TryRead(addr, 8);
    return b == null ? 0 : BitConverter.ToInt64(b, 0);
  }
  public static int I32(long addr) {
    var b = TryRead(addr, 4);
    return b == null ? 0 : BitConverter.ToInt32(b, 0);
  }
  public static uint U32(long addr) {
    var b = TryRead(addr, 4);
    return b == null ? 0 : BitConverter.ToUInt32(b, 0);
  }
  public static float F32(long addr) {
    var b = TryRead(addr, 4);
    return b == null ? 0 : BitConverter.ToSingle(b, 0);
  }
  public static string W(long addr) {
    var b = TryRead(addr, 32);
    if (b == null) return "";
    int len = BitConverter.ToInt32(b, 16);
    int cap = BitConverter.ToInt32(b, 24);
    if (len <= 0 || len > 120 || cap < len || cap > 200) return "";
    byte[] raw;
    if (cap <= 8) raw = b;
    else {
      long ptr = BitConverter.ToInt64(b, 0);
      raw = TryRead(ptr, len * 2);
      if (raw == null) return "";
    }
    var s = Encoding.Unicode.GetString(raw, 0, Math.Min(raw.Length, len * 2));
    return s.Replace("\0", "");
  }
  public static string Utf16(long addr, int maxChars) {
    if (addr < 0x10000) return "";
    var raw = TryRead(addr, maxChars * 2);
    if (raw == null) return "";
    int n = 0;
    while (n < maxChars && BitConverter.ToUInt16(raw, n * 2) != 0) n++;
    return Encoding.Unicode.GetString(raw, 0, n * 2);
  }
}
"@

$proc = Get-Process -Id 27560 -ErrorAction SilentlyContinue
if (-not $proc) { Write-Output 'NO_PROCESS'; exit 1 }
$h = [Mem2]::OpenProcess(0x0410, $false, 27560)
if ($h -eq [IntPtr]::Zero) { Write-Output 'OPEN_FAIL'; exit 1 }
[Mem2]::H = $h

function Kids([long]$el) {
  $first = [Mem2]::I64($el + 0x10)
  $last = [Mem2]::I64($el + 0x18)
  if ($first -lt 0x10000 -or $last -lt $first) { return @() }
  $n = [int](($last - $first) / 8)
  if ($n -lt 0 -or $n -gt 64) { return @() }
  $list = New-Object System.Collections.Generic.List[long]
  for ($i = 0; $i -lt $n; $i++) { $list.Add([Mem2]::I64($first + 8L * $i)) }
  return $list
}

function Vis([long]$el) {
  $flags = [Mem2]::U32($el + 0x168)
  return (($flags -shr 11) -band 1)
}

function FkId([long]$widget, [int]$off) {
  $row = [Mem2]::I64($widget + $off)
  if ($row -lt 0x10000) { return '' }
  $idPtr = [Mem2]::I64($row)
  if ($idPtr -lt 0x10000) { return '' }
  return [Mem2]::Utf16($idPtr, 80)
}

function Describe([long]$el) {
  $id = [Mem2]::W($el + 0x128)
  $txt = [Mem2]::W($el + 0x378)
  $w = [Mem2]::F32($el + 0x270)
  $hh = [Mem2]::F32($el + 0x274)
  $v = Vis $el
  $n = (Kids $el).Count
  return "vis=$v kids=$n size=$([int]$w)x$([int]$hh) id='$id' text='$txt'"
}

$panel = 0x380D52C0C10L
Write-Output ("PANEL $('{0:X}' -f $panel) " + (Describe $panel))
$pc = Kids $panel
Write-Output ("panel children $($pc.Count)")
for ($i = 0; $i -lt $pc.Count; $i++) {
  $c = $pc[$i]
  Write-Output ("  [$i] $('{0:X}' -f $c) " + (Describe $c))
  $cc = Kids $c
  for ($j = 0; $j -lt $cc.Count; $j++) {
    $d = $cc[$j]
    $dd = Kids $d
    $counts = @()
    foreach ($g in $dd) { $counts += (Kids $g).Count }
    Write-Output ("    [$i][$j] $('{0:X}' -f $d) " + (Describe $d) + " grandchildRoomCounts=$($counts -join ',')")
  }
}

$floor = [Mem2]::I64($panel + 0x318)
Write-Output ("FLOOR_OBJ $('{0:X}' -f $floor)")
if ($floor -gt 0x10000) {
  Write-Output ("floor-obj " + (Describe $floor))
  Write-Output 'floor-obj qwords +0x00..+0x180'
  for ($off = 0; $off -le 0x180; $off += 8) {
    $p = [Mem2]::I64($floor + $off)
    if ($p -gt 0x10000) {
      $kn = (Kids $p).Count
      Write-Output ("  +$('{0:X3}' -f $off) -> $('{0:X}' -f $p) kids=$kn vis=$(Vis $p)")
    }
  }
  $layers = [Mem2]::I64($floor + 0x30)
  Write-Output ("LAYERS +0x30 $('{0:X}' -f $layers)")
  if ($layers -gt 0x10000) {
    $lc = Kids $layers
    Write-Output ("layer count $($lc.Count) " + (Describe $layers))
    for ($li = 0; $li -lt $lc.Count; $li++) {
      $rooms = Kids $lc[$li]
      $ids = @()
      foreach ($r in $rooms) {
        $t = FkId $r 0x500
        if ($t.Length -gt 40) { $t = $t.Substring(0, 40) }
        $ids += $t
      }
      Write-Output ("  L$li n=$($rooms.Count) vis=$(Vis $lc[$li]) types=$($ids -join ' | ')")
    }
  }
}

function DumpColumns([string]$label, [long]$el) {
  if ($el -lt 0x10000) { Write-Output "$label NULL"; return }
  $layers = Kids $el
  Write-Output ("$label $('{0:X}' -f $el) layers=$($layers.Count) " + (Describe $el))
  for ($li = 0; $li -lt $layers.Count; $li++) {
    $rooms = Kids $layers[$li]
    $ids = @()
    foreach ($r in $rooms) {
      $t = FkId $r 0x500
      $rew = FkId $r 0x510
      if ($t.Length -gt 36) { $t = $t.Substring(0, 36) }
      if ($rew.Length -gt 24) { $rew = $rew.Substring(0, 24) }
      $ids += "$t/$rew"
    }
    Write-Output ("  L$li n=$($rooms.Count) vis=$(Vis $layers[$li]) $($ids -join ' | ')")
  }
}

# Plugin draw path: visual panel[0][1], visual is panel child 0
if ($pc.Count -gt 0) {
  $visual = $pc[0]
  $v0 = Kids $visual
  Write-Output '--- plugin draw paths ---'
  if ($v0.Count -gt 1) { DumpColumns 'visual[0][1]' $v0[1] }
  if ($v0.Count -gt 0) { DumpColumns 'visual[0][0]' $v0[0] }
}

# Walk every panel child looking for a 1..1 layer shape within 3 levels
Write-Output '--- shape hunt under each panel child ---'
function Hunt([long]$el, [string]$path, [int]$depth) {
  if ($depth -gt 3 -or $el -lt 0x10000) { return }
  $ch = Kids $el
  if ($ch.Count -ge 2 -and $ch.Count -le 16) {
    $counts = @()
    $ok = $true
    foreach ($c in $ch) {
      $n = (Kids $c).Count
      if ($n -lt 1 -or $n -gt 16) { $ok = $false }
      $counts += $n
    }
    if ($ok -and $counts[0] -eq 1 -and $counts[$counts.Count - 1] -eq 1) {
      Write-Output ("SHAPE $path counts=$($counts -join ',') vis=$(Vis $el)")
    }
  }
  if ($depth -eq 3) { return }
  for ($i = 0; $i -lt $ch.Count; $i++) {
    Hunt $ch[$i] "$path[$i]" ($depth + 1)
  }
}
for ($i = 0; $i -lt $pc.Count; $i++) { Hunt $pc[$i] "panel[$i]" 0 }

[Mem2]::CloseHandle($h) | Out-Null
Write-Output 'DONE'
