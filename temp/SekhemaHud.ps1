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
  public static byte[] Read(long addr, int size) {
    var buf = new byte[size]; int n;
    if (addr < 0x10000) return null;
    if (!ReadProcessMemory(H, new IntPtr(addr), buf, size, out n) || n < size) return null;
    return buf;
  }
  public static long I64(long addr) { var b = Read(addr, 8); return b == null ? 0 : BitConverter.ToInt64(b, 0); }
  public static float F32(long addr) { var b = Read(addr, 4); return b == null ? 0 : BitConverter.ToSingle(b, 0); }
  public static string Kids(long el) {
    long first = I64(el + 0x10), last = I64(el + 0x18);
    if (first < 0x10000 || last < first) return "";
    int n = (int)((last - first) / 8);
    if (n < 0 || n > 64) return "";
    var sb = new StringBuilder();
    for (int i = 0; i < n; i++) { if (i > 0) sb.Append(','); sb.Append(I64(first + 8L * i)); }
    return sb.ToString();
  }
  public static string Scan(long addr) {
    var b = Read(addr, 0x400);
    if (b == null) return "unreadable";
    var hits = new StringBuilder();
    for (int i = 0; i + 8 <= b.Length; i += 4) {
      int len = BitConverter.ToInt32(b, i);
      if (len < 1 || len > 12) continue;
      int capOff = i + 8;
      if (capOff + 4 > b.Length) continue;
      int cap = BitConverter.ToInt32(b, capOff);
      if (cap < len || cap > 32) continue;
      string s;
      if (cap <= 8 && i >= 16) {
        s = Encoding.Unicode.GetString(b, i - 16, Math.Min(16, len * 2));
      } else continue;
      s = s.Replace("\0", "");
      if (s.Length >= 1 && s.Length <= len + 1) hits.Append(" +" + (i - 16).ToString("X") + "='" + s + "'");
    }
    // also search raw utf16 digits
    var u = Encoding.Unicode.GetString(b);
    int z = u.IndexOf('\0');
    return hits.ToString();
  }
}
"@
$proc = Get-Process -Name PathOfExile | Select-Object -First 1
$h = [Mem4]::OpenProcess(0x0410, $false, $proc.Id)
[Mem4]::H = $h
function Kids([long]$el) {
  $s = [Mem4]::Kids($el)
  if ([string]::IsNullOrEmpty($s)) { return @() }
  return @($s.Split(',') | ForEach-Object { [long]$_ })
}
$gameUi = [Mem4]::I64(0x48EDA990C10 + 0xB8)
$gk = Kids $gameUi
$hud = $gk[13]
$hk = Kids $hud
"slot0 size $([Mem4]::F32($hk[0] + 0x270))"
$leaf = (Kids $hk[0])[1]
"water leaf $('{0:X}' -f $leaf) size $([Mem4]::F32($leaf + 0x270))"
$deeper = Kids $leaf
"water leaf kids $($deeper.Count)"
for ($j = 0; $j -lt $deeper.Count; $j++) {
  "  d[$j] $('{0:X}' -f $deeper[$j]) scan $([Mem4]::Scan($deeper[$j]))"
}
"water leaf scan $([Mem4]::Scan($leaf))"
$slot5 = $hk[5]
"honour slot size $([Mem4]::F32($slot5 + 0x270)),$([Mem4]::F32($slot5 + 0x274))"
$h5 = Kids $slot5
for ($j = 0; $j -lt $h5.Count; $j++) {
  "  h5[$j] size $([math]::Round([Mem4]::F32($h5[$j] + 0x270),1))"
}
# key slots 1..3 leaf 1
foreach ($si in 1,2,3) {
  $sk = Kids $hk[$si]
  $kleaf = $sk[1]
  "key slot $si leaf scan $([Mem4]::Scan($kleaf))"
  $dd = Kids $kleaf
  "  kids $($dd.Count)"
  for ($j = 0; $j -lt $dd.Count; $j++) { "  k[$j] scan $([Mem4]::Scan($dd[$j]))" }
}
[Mem4]::CloseHandle($h) | Out-Null
'DONE'
