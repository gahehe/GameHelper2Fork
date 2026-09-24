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
  public static byte[] Read(long addr, int size) {
    var buf = new byte[size];
    int n = 0;
    if (addr < 0x10000) return null;
    if (!ReadProcessMemory(H, new IntPtr(addr), buf, size, out n) || n != size) return null;
    return buf;
  }
  public static long I64(long addr) { var b = Read(addr, 8); return b == null ? 0 : BitConverter.ToInt64(b, 0); }
  public static int I32(long addr) { var b = Read(addr, 4); return b == null ? 0 : BitConverter.ToInt32(b, 0); }
  public static float F32(long addr) { var b = Read(addr, 4); return b == null ? 0 : BitConverter.ToSingle(b, 0); }
  public static string W(long addr) {
    var b = Read(addr, 32); if (b == null) return "";
    long buf = BitConverter.ToInt64(b, 0);
    int len = BitConverter.ToInt32(b, 16);
    int cap = BitConverter.ToInt32(b, 24);
    if (len <= 0 || len > 200 || cap <= 0 || cap > 200) return "";
    byte[] raw;
    if (cap <= 8) { raw = new byte[16]; Buffer.BlockCopy(b, 0, raw, 0, 16); }
    else { raw = Read(buf, len * 2); if (raw == null) return ""; }
    var s = Encoding.Unicode.GetString(raw);
    if (len < s.Length) s = s.Substring(0, len);
    return s.Replace("\0", "");
  }
  public static string Utf16(long addr, int maxChars) {
    var raw = Read(addr, maxChars * 2); if (raw == null) return "";
    int n = 0;
    while (n < maxChars && BitConverter.ToUInt16(raw, n * 2) != 0) n++;
    return Encoding.Unicode.GetString(raw, 0, n * 2);
  }
  public static string Kids(long el) {
    long first = I64(el + 0x10);
    long last = I64(el + 0x18);
    if (first < 0x10000 || last < first) return "";
    int n = (int)((last - first) / 8);
    if (n < 0 || n > 256) return "";
    var sb = new StringBuilder();
    for (int i = 0; i < n; i++) {
      if (i > 0) sb.Append(',');
      sb.Append(I64(first + 8L * i).ToString());
    }
    return sb.ToString();
  }
}
"@

function KidList([long]$el) {
  $s = [Mem3]::Kids($el)
  if ([string]::IsNullOrEmpty($s)) { return @() }
  return @($s.Split(',') | ForEach-Object { [long]$_ })
}
function Fk([long]$widget, [int]$off) {
  $row = [Mem3]::I64($widget + $off)
  $table = [Mem3]::I64($widget + $off + 8)
  $id = ''; $name = ''; $tpath = ''
  if ($row -gt 0x10000) {
    $id = [Mem3]::Utf16([Mem3]::I64($row), 80)
    $name = [Mem3]::Utf16([Mem3]::I64($row + 0x28), 80)
  }
  if ($table -gt 0x10000) {
    $tpath = [Mem3]::W($table)
    if ([string]::IsNullOrEmpty($tpath)) { $tpath = [Mem3]::Utf16([Mem3]::I64($table), 96) }
  }
  "id='$id' name='$name' table='$tpath'"
}

$proc = Get-Process -Name PathOfExile -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $proc) { 'NO_PROCESS'; exit 1 }
$h = [Mem3]::OpenProcess(0x0410, $false, $proc.Id)
if ($h -eq [IntPtr]::Zero) { 'OPEN_FAIL'; exit 1 }
[Mem3]::H = $h

$panel = [int64]0x48EDACE8650
$host84 = [Mem3]::I64($panel + 0xB8)
$gameUi = [Mem3]::I64($host84 + 0xB8)
$floor = [Mem3]::I64($host84 + 0x318)
"HOST84 $('{0:X}' -f $host84)"
"GAMEUI $('{0:X}' -f $gameUi)"
"FLOOR $('{0:X}' -f $floor)"

$pKids = KidList $panel
$c0kids = KidList $pKids[0]
"visual child0 kids=$($c0kids.Count)"
for ($i = 0; $i -lt $c0kids.Count; $i++) {
  $inner = KidList $c0kids[$i]
  "  visual[0][$i] kids=$($inner.Count)"
}

if ($floor -gt 0x10000) {
  $fk = KidList $floor
  "floor-obj kids=$($fk.Count) self=$('{0:X}' -f ([Mem3]::I64($floor + 8)))"
  $layers = [Mem3]::I64($floor + 0x30)
  "LAYERS $('{0:X}' -f $layers) self=$('{0:X}' -f ([Mem3]::I64($layers + 8)))"
  $lk = KidList $layers
  "LAYER_COUNT $($lk.Count)"
  for ($li = 0; $li -lt $lk.Count; $li++) {
    $rooms = KidList $lk[$li]
    "LAYER $li rooms=$($rooms.Count)"
    for ($ri = 0; $ri -lt $rooms.Count; $ri++) {
      $room = $rooms[$ri]
      $pair = [Mem3]::Read($room + 0x4F4, 2)
      $lb = 0; $ib = 0
      if ($pair -ne $null) { $lb = $pair[0]; $ib = $pair[1] }
      $rx = [Mem3]::F32($room + 0x100); $ry = [Mem3]::F32($room + 0x104)
      $rw = [Mem3]::F32($room + 0x270); $rh = [Mem3]::F32($room + 0x274)
      "  R$li.$ri u8=$lb,$ib rel=$([math]::Round($rx,1)),$([math]::Round($ry,1)) size=$([math]::Round($rw,1)),$([math]::Round($rh,1))"
      "    TYPE $(Fk $room 0x500)"
      "    REWARD $(Fk $room 0x510)"
      "    AFFLICT $(Fk $room 0x520)"
    }
  }
  if ($fk.Count -gt 0) {
    $link = $fk[0]
    $vecFirst = [Mem3]::I64($link + 0x2C8)
    $vecLast = [Mem3]::I64($link + 0x2D0)
    $count = 0
    if ($vecFirst -gt 0x10000 -and $vecLast -gt $vecFirst) { $count = [int](($vecLast - $vecFirst) / 24) }
    "LINKS count=$count"
    $show = [Math]::Min($count, 40)
    for ($s = 0; $s -lt $show; $s++) {
      $at = $vecFirst + (24 * $s)
      $x1 = [Mem3]::F32($at); $y1 = [Mem3]::F32($at + 4); $x2 = [Mem3]::F32($at + 8); $y2 = [Mem3]::F32($at + 12)
      $st = [Mem3]::I32($at + 16); $fl = [Mem3]::I32($at + 20)
      "  seg $s ($([math]::Round($x1,1)),$([math]::Round($y1,1)))->($([math]::Round($x2,1)),$([math]::Round($y2,1))) state=$st flags=$fl"
    }
  }
}

$gk = KidList $gameUi
"gameui kids=$($gk.Count)"
if ($gk.Count -gt 13) {
  $hud = $gk[13]
  $hk = KidList $hud
  "HUD kids=$($hk.Count) addr=$('{0:X}' -f $hud)"
  for ($i = 0; $i -lt $hk.Count; $i++) {
    $sk = KidList $hk[$i]
    $texts = @()
    for ($j = 0; $j -lt $sk.Count; $j++) {
      $leaf = $sk[$j]
      $t378 = [Mem3]::W($leaf + 0x378)
      $t128 = [Mem3]::W($leaf + 0x128)
      $ww = [Mem3]::F32($leaf + 0x270)
      $texts += "[$j]w=$([math]::Round($ww,1)) 378='$t378' 128='$t128'"
    }
    "  hud[$i] kids=$($sk.Count) $($texts -join ' | ')"
  }
}
[Mem3]::CloseHandle($h) | Out-Null
'DONE'
