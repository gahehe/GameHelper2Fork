$ErrorActionPreference = 'Stop'
Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class Near {
  [DllImport("kernel32.dll", SetLastError=true)]
  public static extern IntPtr OpenProcess(uint access, bool inherit, int pid);
  [DllImport("kernel32.dll", SetLastError=true)]
  public static extern bool ReadProcessMemory(IntPtr h, IntPtr addr, byte[] buf, int size, out int read);
  [DllImport("kernel32.dll")]
  public static extern int VirtualQueryEx(IntPtr h, IntPtr addr, out MBI mbi, int len);
  [DllImport("kernel32.dll")]
  public static extern bool CloseHandle(IntPtr h);
  [StructLayout(LayoutKind.Sequential)]
  public struct MBI {
    public IntPtr BaseAddress; public IntPtr AllocationBase; public uint AllocationProtect;
    public IntPtr RegionSize; public uint State; public uint Protect; public uint Type;
  }
  public static IntPtr H;
  public static string Region(long addr) {
    MBI m = new MBI();
    if (VirtualQueryEx(H, new IntPtr(addr), out m, Marshal.SizeOf(typeof(MBI))) == 0) return "query fail";
    return "base=" + m.BaseAddress.ToInt64().ToString("X") + " size=" + m.RegionSize.ToInt64().ToString("X") + " prot=" + m.Protect.ToString("X") + " type=" + m.Type.ToString("X");
  }
  public static string Around(long addr) {
    var buf = new byte[96];
    int n;
    ReadProcessMemory(H, new IntPtr(addr - 32), buf, 96, out n);
    var sb = new StringBuilder();
    for (int i = 0; i < n; i++) sb.Append(buf[i].ToString("X2"));
    return sb.ToString();
  }
  public static string FindIn(long baseAddr, long size, string text) {
    var needle = Encoding.Unicode.GetBytes(text);
    var hits = new StringBuilder();
    int chunk = 1 << 20;
    for (long off = 0; off < size; off += chunk) {
      int take = (int)Math.Min(chunk, size - off);
      var buf = new byte[take];
      int n;
      if (!ReadProcessMemory(H, new IntPtr(baseAddr + off), buf, take, out n)) continue;
      for (int i = 0; i + needle.Length <= n; i += 2) {
        bool ok = true;
        for (int k = 0; k < needle.Length; k++) if (buf[i + k] != needle[k]) { ok = false; break; }
        if (!ok) continue;
        int z = i;
        while (z + 1 < n && (buf[z] != 0 || buf[z + 1] != 0) && z - i < 80) z += 2;
        var s = Encoding.Unicode.GetString(buf, i, z - i);
        hits.AppendLine("  " + (baseAddr + off + i).ToString("X") + " [" + s + "]");
        i += needle.Length;
      }
    }
    return hits.ToString();
  }
}
"@
$proc = Get-Process -Name PathOfExile | Select-Object -First 1
$h = [Near]::OpenProcess(0x0410, $false, $proc.Id)
[Near]::H = $h
$a = [int64]0x48E26536790
"REGION $([Near]::Region($a))"
"AROUND $([Near]::Around($a))"
# parse region by querying again in powershell via the string
$reg = [Near]::Region($a)
if ($reg -match 'base=([0-9A-F]+) size=([0-9A-F]+)') {
  $base = [Convert]::ToInt64($Matches[1], 16)
  $size = [Convert]::ToInt64($Matches[2], 16)
  "scan region $('{0:X}' -f $base) size $('{0:X}' -f $size)"
  foreach ($t in @('29(+0)','Honour','1,846','1846','1,860')) {
    "---- $t"
    $found = [Near]::FindIn($base, $size, $t)
    if ([string]::IsNullOrWhiteSpace($found)) { '  none' } else { $found }
  }
}
[Near]::CloseHandle($h) | Out-Null
'DONE'
