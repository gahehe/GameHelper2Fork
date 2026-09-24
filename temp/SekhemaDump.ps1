$ErrorActionPreference = 'Stop'
Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class Mem {
  [DllImport("kernel32.dll", SetLastError=true)]
  public static extern IntPtr OpenProcess(uint access, bool inherit, int pid);
  [DllImport("kernel32.dll", SetLastError=true)]
  public static extern bool ReadProcessMemory(IntPtr h, IntPtr addr, byte[] buf, int size, out int read);
  [DllImport("kernel32.dll")]
  public static extern bool CloseHandle(IntPtr h);
  public static IntPtr H;
  public static byte[] Read(long addr, int size) {
    var buf = new byte[size];
    int n;
    if (!ReadProcessMemory(H, new IntPtr(addr), buf, size, out n) || n != size)
      throw new Exception("RPM fail 0x" + addr.ToString("X") + " size " + size + " got " + n + " err " + Marshal.GetLastWin32Error());
    return buf;
  }
  public static long I64(long addr) { return BitConverter.ToInt64(Read(addr, 8), 0); }
  public static int I32(long addr) { return BitConverter.ToInt32(Read(addr, 4), 0); }
  public static float F32(long addr) { return BitConverter.ToSingle(Read(addr, 4), 0); }
  public static uint U32(long addr) { return BitConverter.ToUInt32(Read(addr, 4), 0); }
  public static string W(long addr) {
    var b = Read(addr, 32);
    long buf = BitConverter.ToInt64(b, 0);
    long res = BitConverter.ToInt64(b, 8);
    int len = BitConverter.ToInt32(b, 16);
    int cap = BitConverter.ToInt32(b, 24);
    if (len <= 0 || len > 200 || cap <= 0 || cap > 200) return "";
    byte[] raw;
    if (cap <= 8) {
      raw = new byte[16];
      Buffer.BlockCopy(b, 0, raw, 0, 16);
    } else {
      raw = Read(buf, len * 2);
    }
    var s = Encoding.Unicode.GetString(raw);
    if (len < s.Length) s = s.Substring(0, len);
    return s.Replace("\0", "");
  }
  public static string Utf16Ptr(long addr, int maxChars) {
    if (addr == 0) return "";
    var raw = Read(addr, maxChars * 2);
    int n = 0;
    while (n < maxChars && BitConverter.ToUInt16(raw, n * 2) != 0) n++;
    return Encoding.Unicode.GetString(raw, 0, n * 2);
  }
}
"@

$names = @('PathOfExile','PathOfExile_KG','PathOfExileSteam','PathOfExile_x64','PathOfExileEGS')
$proc = Get-Process | Where-Object { $names -contains $_.ProcessName } | Select-Object -First 1
if (-not $proc) { Write-Output 'NO_PROCESS'; exit 1 }
Write-Output ("PROC " + $proc.ProcessName + " pid=" + $proc.Id)
$h = [Mem]::OpenProcess(0x0410, $false, $proc.Id)
if ($h -eq [IntPtr]::Zero) { Write-Output ("OPEN_FAIL " + [Runtime.InteropServices.Marshal]::GetLastWin32Error()); exit 1 }
[Mem]::H = $h

function Children([long]$el) {
  $first = [Mem]::I64($el + 0x10)
  $last = [Mem]::I64($el + 0x18)
  $n = [int](($last - $first) / 8)
  $list = @()
  if ($n -lt 0 -or $n -gt 64 -or $first -eq 0) { return ,@($n, $list) }
  for ($i = 0; $i -lt $n; $i++) { $list += [Mem]::I64($first + 8L * $i) }
  return ,@($n, $list)
}

function Describe([long]$el) {
  $self = [Mem]::I64($el + 0x08)
  $flags = [Mem]::U32($el + 0x168)
  $vis = (($flags -shr 0x0B) -band 1)
  $w = [Mem]::F32($el + 0x270)
  $hh = [Mem]::F32($el + 0x274)
  $sid = [Mem]::W($el + 0x128)
  $parent = [Mem]::I64($el + 0x0B8)
  return "self=$('{0:X}' -f $self) vis=$vis flags=$('{0:X}' -f $flags) size=$w,$hh id='$sid' parent=$('{0:X}' -f $parent)"
}

$panel = 0x48EDACE8650L
Write-Output 'PANEL'
Write-Output (Describe $panel)
$pc = Children $panel
Write-Output ("panel children " + $pc[0])
for ($i = 0; $i -lt $pc[1].Count; $i++) {
  $c = $pc[1][$i]
  $cc = Children $c
  Write-Output ("  [$i] $('{0:X}' -f $c) kids=$($cc[0]) " + (Describe $c))
}

$floor = [Mem]::I64($panel + 0x318)
Write-Output ("FLOOR_PTR $('{0:X}' -f $floor)")
if ($floor -gt 0x10000) {
  Write-Output ("floor-obj " + (Describe $floor))
  $fc = Children $floor
  Write-Output ("floor-obj children " + $fc[0])
  for ($i = 0; $i -lt [Math]::Min($fc[1].Count, 8); $i++) {
    $c = $fc[1][$i]
    $cc = Children $c
    Write-Output ("  floor[$i] $('{0:X}' -f $c) kids=$($cc[0]) " + (Describe $c))
  }
  $layers = [Mem]::I64($floor + 0x30)
  Write-Output ("LAYERS $('{0:X}' -f $layers)")
  if ($layers -gt 0x10000) {
    Write-Output ("layers " + (Describe $layers))
    $lc = Children $layers
    Write-Output ("LAYER_COUNT " + $lc[0])
    for ($li = 0; $li -lt $lc[1].Count; $li++) {
      $layer = $lc[1][$li]
      $rooms = Children $layer
      Write-Output ("LAYER $li rooms=$($rooms[0])")
      for ($ri = 0; $ri -lt $rooms[1].Count; $ri++) {
        $room = $rooms[1][$ri]
        $lx = [Mem]::F32($layer + 0x100); $ly = [Mem]::F32($layer + 0x104)
        $rx = [Mem]::F32($room + 0x100); $ry = [Mem]::F32($room + 0x104)
        $rw = [Mem]::F32($room + 0x270); $rh = [Mem]::F32($room + 0x274)
        $layerByte = [Mem]::Read($room + 0x4F4, 1)[0]
        $indexByte = [Mem]::Read($room + 0x4F5, 1)[0]
        function Fk([long]$widget, [int]$off) {
          $row = [Mem]::I64($widget + $off)
          $table = [Mem]::I64($widget + $off + 8)
          $id = ''
          $name = ''
          $tpath = ''
          if ($row -gt 0x10000) {
            $idPtr = [Mem]::I64($row)
            $namePtr = [Mem]::I64($row + 0x28)
            if ($idPtr -gt 0x10000) { $id = [Mem]::Utf16Ptr($idPtr, 96) }
            if ($namePtr -gt 0x10000) { $name = [Mem]::Utf16Ptr($namePtr, 96) }
          }
          if ($table -gt 0x10000) {
            $tpath = [Mem]::W($table)
            if ($tpath -eq '') {
              $p = [Mem]::I64($table)
              if ($p -gt 0x10000) { $tpath = [Mem]::Utf16Ptr($p, 96) }
            }
          }
          return "row=$('{0:X}' -f $row) id='$id' name='$name' table='$tpath'"
        }
        $t = Fk $room 0x500
        $r = Fk $room 0x510
        $a = Fk $room 0x520
        Write-Output ("  R$li.$ri addr=$('{0:X}' -f $room) u8=$layerByte,$indexByte rel=$rx,$ry size=$rw,$rh")
        Write-Output ("    TYPE $t")
        Write-Output ("    REWARD $r")
        Write-Output ("    AFFLICT $a")
      }
    }
  }
  if ($fc[1].Count -gt 0) {
    $link = $fc[1][0]
    $vecFirst = [Mem]::I64($link + 0x2C8)
    $vecLast = [Mem]::I64($link + 0x2D0)
    $count = [int](($vecLast - $vecFirst) / 24)
    Write-Output ("LINKS first=$('{0:X}' -f $vecFirst) count=$count")
    $show = [Math]::Min($count, 48)
    if ($count -gt 0 -and $count -lt 80) {
      for ($s = 0; $s -lt $show; $s++) {
        $at = $vecFirst + $s * 24
        $x1 = [Mem]::F32($at); $y1 = [Mem]::F32($at + 4); $x2 = [Mem]::F32($at + 8); $y2 = [Mem]::F32($at + 12)
        $st = [Mem]::I32($at + 16); $fl = [Mem]::I32($at + 20)
        Write-Output ("  seg $s ($x1,$y1)->($x2,$y2) state=$st flags=$fl")
      }
    }
  }
}

$parent = [Mem]::I64($panel + 0x0B8)
$gameUi = [Mem]::I64($parent + 0x0B8)
Write-Output ("GAMEUI $('{0:X}' -f $gameUi) via parent $('{0:X}' -f $parent)")
$gc = Children $gameUi
Write-Output ("gameui children " + $gc[0])
if ($gc[0] -gt 13) {
  $hud = $gc[1][13]
  Write-Output ("HUD $('{0:X}' -f $hud) " + (Describe $hud))
  $hc = Children $hud
  Write-Output ("hud children " + $hc[0])
  for ($i = 0; $i -lt $hc[1].Count; $i++) {
    $slot = $hc[1][$i]
    $sc = Children $slot
    $texts = @()
    foreach ($leaf in $sc[1]) {
      $texts += ("'" + [Mem]::W($leaf + 0x378) + "'|id='" + [Mem]::W($leaf + 0x128) + "'")
    }
    Write-Output ("  hud[$i] kids=$($sc[0]) " + ($texts -join ' ; '))
  }
}

[Mem]::CloseHandle($h) | Out-Null
Write-Output 'DONE'
