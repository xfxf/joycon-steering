param(
    [string]$ProcessName = "JoyconSteering",
    [string]$WindowTitle = "",
    [string]$Out         = "docs/screenshots/diagnostics.png"
)

Add-Type @"
using System;
using System.Runtime.InteropServices;
using System.Text;
public class Win {
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int c);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc cb, IntPtr p);
  public delegate bool EnumWindowsProc(IntPtr h, IntPtr p);
  [StructLayout(LayoutKind.Sequential)]
  public struct RECT { public int Left,Top,Right,Bottom; }
}
"@ | Out-Null
Add-Type -AssemblyName System.Windows.Forms,System.Drawing

$proc = Get-Process -Name $ProcessName -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $proc) { Write-Error "No process named '$ProcessName' running."; exit 1 }
$targetPid = [uint32]$proc.Id

# Enumerate all top-level windows for this process; pick by title match if provided,
# else the first visible one with a non-empty title.
$candidates = New-Object System.Collections.ArrayList
$cb = [Win+EnumWindowsProc] {
    param($h, $lp)
    if (-not [Win]::IsWindowVisible($h)) { return $true }
    $wpid = 0
    [Win]::GetWindowThreadProcessId($h, [ref]$wpid) | Out-Null
    if ($wpid -ne $targetPid) { return $true }
    $sb = New-Object System.Text.StringBuilder 256
    [Win]::GetWindowText($h, $sb, 256) | Out-Null
    $title = $sb.ToString()
    if ([string]::IsNullOrWhiteSpace($title)) { return $true }
    $null = $candidates.Add([pscustomobject]@{ Handle = $h; Title = $title })
    return $true
}
[Win]::EnumWindows($cb, [IntPtr]::Zero) | Out-Null

if ($candidates.Count -eq 0) { Write-Error "No visible windows for $ProcessName"; exit 1 }

if ($WindowTitle) {
    $chosen = $candidates | Where-Object { $_.Title -like "*$WindowTitle*" } | Select-Object -First 1
} else {
    $chosen = $candidates | Select-Object -First 1
}

if (-not $chosen) {
    Write-Host "Available windows:"
    foreach ($c in $candidates) { Write-Host "  $($c.Title)" }
    Write-Error "No window matched '$WindowTitle'."
    exit 1
}

Write-Host "Capturing: '$($chosen.Title)'"
$h = $chosen.Handle
[Win]::ShowWindow($h, 9) | Out-Null
[Win]::SetForegroundWindow($h) | Out-Null
Start-Sleep -Milliseconds 700

$r = New-Object Win+RECT
[Win]::GetWindowRect($h, [ref]$r) | Out-Null
$w  = $r.Right  - $r.Left
$ht = $r.Bottom - $r.Top
if ($w -le 0 -or $ht -le 0) { Write-Error "Bad rect: $w x $ht"; exit 1 }

$bmp = New-Object System.Drawing.Bitmap $w, $ht
$g   = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($r.Left, $r.Top, 0, 0, ([System.Drawing.Size]::new($w, $ht)))
New-Item -ItemType Directory -Force (Split-Path -Parent $Out) | Out-Null
$bmp.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $bmp.Dispose()
Write-Host "Saved $Out ($w x $ht)"
