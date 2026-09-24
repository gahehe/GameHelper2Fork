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
  public static bool Ok(long addr, int size, out byte[] buf) {
    buf = new byte[size];
    int n;
    if (addr < 0x10000) return false;
    return ReadProcessMemory(H, new IntPtr(addr), buf, size, out n) && n == size;
  }
  public static long I64(long addr) { byte[] b; if (!Ok(addr, 8, out b)) return 0; return BitConverter.ToInt64(b, 0); }
  public static int I32(long addr) { byte[] b; if (!Ok(addr, 4, out b)) return 0; return BitConverter.ToInt32(b, 0); }
  public static float F32(long addr) { byte[] b; if (!Ok(addr, 4, out b)) return 0; return BitConverter.ToSingle(b, 0); }
  public static string W(long addr) {
    byte[] b; if (!Ok(addr, 32, out b)) return "";
    long buf = BitConverter.ToInt64(b, 0);
    int len = BitConverter.ToInt32(b, 16);
    int cap = BitConverter.ToInt32(b, 24);
    if (len <= 0 || len > 200 || cap <= 0 || cap > 200) return "";
    byte[] raw;
    if (cap <= 8) { raw = new byte[16]; Buffer.BlockCopy(b, 0, raw, 0, 16); }
    else { if (!Ok(buf, len * 2, out raw)) return ""; }
    var s = Encoding.Unicode.GetString(raw);
    if (len < s.Length) s = s.Substring(0, len);
    return s.Replace("\0", "");
  }
  public static string Utf16(long addr, int maxChars) {
    if (addr < 0x10000) return "";
    byte[] raw; if (!Ok(addr, maxChars * 2, out raw)) return "";
    int n = 0;
    while (n < maxChars && BitConverter.ToUInt16(raw, n * 2) != 0) n++;
    return Encoding.Unicode.GetString(raw, 0, n * 2);
  }
  public static long[] Kids(long el, out int n) {
    n = -1;
    long first = I64(el + 0x10);
    long last = I64(el + 0x18);
    if (first < 0x10000 || last < first) return new long[0];
    n = (int)((last - first) / 8);
    if (n < 0 || n > 256) { n = -2; return new long[0]; }
    var list = new long[n];
    for (int i = 0; i < n; i++) list[i] = I64(first + 8L * i);
    return list;
  }
}
"@

$proc = Get-Process -Name PathOfExile -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $proc) { 'NO_PROCESS'; exit 1 }
$h = [Mem2]::OpenProcess(0x0410, $false, $proc.Id)
if ($h -eq [IntPtr]::Zero) { "OPEN_FAIL"; exit 1 }
[Mem2]::H = $h

function Fk([long]$widget, [int]$off) {
  $row = [Mem2]::I64($widget + $off)
  $table = [Mem2]::I64($widget + $off + 8)
  $id = ''; $name = ''; $tpath = ''
  if ($row -gt 0x10000) {
    $id = [Mem2]::Utf16([Mem2]::I64($row), 80)
    $name = [Mem2]::Utf16([Mem2]::I64($row + 0x28), 80)
  }
  if ($table -gt 0x10000) {
    $tpath = [Mem2]::W($table)
    if ([string]::IsNullOrEmpty($tpath)) { $tpath = [Mem2]::Utf16([Mem2]::I64($table), 80) }
  }
  "id='$id' name='$name' table='$tpath'"
}

$panel = 0x48EDACE8650L
$host84 = [Mem2]::I64($panel + 0x0B8)
$gameUi = [Mem2]::I64($host84 + 0x0B8)
"HOST84 $('{0:X}' -f $host84)"
"GAMEUI $('{0:X}' -f $gameUi)"
$floor = [Mem2]::I64($host84 + 0x318)
"FLOOR_ON_HOST84 $('{0:X}' -f $floor)"

# visual path: panel child 0 child 1
$pKids = [Mem2]::Kids($panel, [ref]$null)
# Kids out param via dummy - fix by calling differently
$n = 0
$pKids = [Mem2]::Kids($panel, [ref]$n)
"panel child0"
$c0 = $pKids[0]
$n0 = 0
$c0kids = [Mem2]::Kids($c0, [ref]$n0)
"  child0 kids=$n0"
for ($i = 0; $i -lt $n0; $i++) {
  $n1 = 0
  $k = [Mem2]::Kids($c0kids[$i], [ref]$n1)
  "  visual[0][$i] kids=$n1 addr=$('{0:X}' -f $c0kids[$i])"
  if ($i -eq 1) {
    for ($li = 0; $li -lt $n1; $li++) {
      $nr = 0
      $rooms = [Mem2]::Kids($k[$li], [ref]$nr)
      "    VIS_LAYER $li rooms=$nr"
    }
  }
}

if ($floor -gt 0x10000) {
  $nf = 0
  $fk = [Mem2]::Kids($floor, [ref]$nf)
  "floor-obj kids=$nf self=$('{0:X}' -f ([Mem2]::I64($floor + 8)))"
  $layers = [Mem2]::I64($floor + 0x30)
  "LAYERS $('{0:X}' -f $layers) self=$('{0:X}' -f ([Mem2]::I64($layers + 8)))"
  $nl = 0
  $lk = [Mem2]::Kids($layers, [ref]$nl)
  "LAYER_COUNT $nl"
  for ($li = 0; $li -lt $nl; $li++) {
    $nr = 0
    $rooms = [Mem2]::Kids($lk[$li], [ref]$nr)
    "LAYER $li rooms=$nr"
    for ($ri = 0; $ri -lt $nr; $ri++) {
      $room = $rooms[$ri]
      $b = [Mem2]::Ok($room + 0x4F4, 2, [ref]$null)
      $bytes = New-Object byte[] 2
      # read two bytes
      $lb = 0; $ib = 0
      $rawOk = $false
      $buf = New-Object byte[] 2
      $read = 0
      $rawOk = [Mem2]::ReadProcessMemory([Mem2]::H, [IntPtr]($room + 0x4F4), $buf, 2, [ref]$read)
      if ($rawOk) { $lb = $buf[0]; $ib = $buf[1] }
      $rx = [Mem2]::F32($room + 0x100); $ry = [Mem2]::F32($room + 0x104)
      $rw = [Mem2]::F32($room + 0x270); $rh = [Mem2]::F32($room + 0x274)
      "  R$li.$ri u8=$lb,$ib rel=$rx,$ry size=$rw,$rh"
      "    TYPE $(Fk $room 0x500)"
      "    REWARD $(Fk $room 0x510)"
      "    AFFLICT $(Fk $room 0x520)"
    }
  }
  if ($nf -gt 0) {
    $link = $fk[0]
    $vecFirst = [Mem2]::I64($link + 0x2C8)
    $vecLast = [Mem2]::I64($link + 0x2D0)
    $count = 0
    if ($vecFirst -gt 0x10000 -and $vecLast -gt $vecFirst) { $count = [int](($vecLast - $vecFirst) / 24) }
    "LINKS count=$count first=$('{0:X}' -f $vecFirst)"
    $show = [Math]::Min($count, 48)
    for ($s = 0; $s -lt $show; $s++) {
      $at = $vecFirst + $s * 24
      $x1 = [Mem2]::F32($at); $y1 = [Mem2]::F32($at + 4); $x2 = [Mem2]::F32($at + 8); $y2 = [Mem2]::F32($at + 12)
      $st = [Mem2]::I32($at + 16); $fl = [Mem2]::I32($at + 20)
      "  seg $s ($([math]::Round($x1,1)),$([math]::Round($y1,1)))->($([math]::Round($x2,1)),$([math]::Round($y2,1))) state=$st flags=$fl"
    }
  }
}

$ng = 0
$gk = [Mem2]::Kids($gameUi, [ref]$ng)
"gameui kids=$ng"
if ($ng -gt 13) {
  $hud = $gk[13]
  $nh = 0
  $hk = [Mem2]::Kids($hud, [ref]$nh)
  "HUD kids=$nh addr=$('{0:X}' -f $hud)"
  for ($i = 0; $i -lt $nh; $i++) {
    $ns = 0
    $sk = [Mem2]::Kids($hk[$i], [ref]$ns)
    $texts = @()
    for ($j = 0; $j -lt $ns; $j++) {
      $leaf = $sk[$j]
      $t378 = [Mem2]::W($leaf + 0x378)
      $t128 = [Mem2]::W($leaf + 0x128)
      $texts += "[$j]378='$t378' 128='$t128'"
    }
    "  hud[$i] kids=$ns $($texts -join ' | ')"
  }
}

[Mem2]::CloseHandle($h) | Out-Null
'DONE'
