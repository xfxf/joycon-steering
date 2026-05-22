param([string]$TabName = "Pedals")

# Send a keyboard shortcut to the focused Settings dialog to switch tabs.
# Ctrl+Tab navigates to the next tab in a WinForms TabControl. We send it as
# many times as needed to reach the named tab (in BuildLayout order:
# Steering, Pedals, Buttons, Advanced).

$tabIndex = @{
    "Steering" = 0
    "Pedals"   = 1
    "Buttons"  = 2
    "Advanced" = 3
}[$TabName]
if ($null -eq $tabIndex) { Write-Error "Unknown tab '$TabName'"; exit 1 }

Add-Type -AssemblyName System.Windows.Forms

# Bring the Settings window to the foreground first.
Add-Type @"
using System;
using System.Runtime.InteropServices;
using System.Text;
public class TabFocus {
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc cb, IntPtr p);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  public delegate bool EnumWindowsProc(IntPtr h, IntPtr p);
}
"@ | Out-Null

$proc = Get-Process -Name JoyconSteering -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $proc) { Write-Error "JoyconSteering not running"; exit 1 }
$targetPid = [uint32]$proc.Id

$settingsHandle = [IntPtr]::Zero
$cb = [TabFocus+EnumWindowsProc] {
    param($h, $lp)
    if (-not [TabFocus]::IsWindowVisible($h)) { return $true }
    $wpid = 0
    [TabFocus]::GetWindowThreadProcessId($h, [ref]$wpid) | Out-Null
    if ($wpid -ne $targetPid) { return $true }
    $sb = New-Object System.Text.StringBuilder 256
    [TabFocus]::GetWindowText($h, $sb, 256) | Out-Null
    if ($sb.ToString().Contains("Settings")) {
        $script:settingsHandle = $h
        return $false
    }
    return $true
}
[TabFocus]::EnumWindows($cb, [IntPtr]::Zero) | Out-Null

if ($settingsHandle -eq [IntPtr]::Zero) {
    Write-Error "Settings window not found via EnumWindows"
    exit 1
}

[TabFocus]::SetForegroundWindow($settingsHandle) | Out-Null
Start-Sleep -Milliseconds 200

# Ctrl+Home would go to the first tab; we just press Ctrl+Tab N times from there.
[System.Windows.Forms.SendKeys]::SendWait("^{HOME}")  # ensure on tab 0
Start-Sleep -Milliseconds 100
for ($i = 0; $i -lt $tabIndex; $i++) {
    [System.Windows.Forms.SendKeys]::SendWait("^{TAB}")
    Start-Sleep -Milliseconds 80
}
Write-Host "Switched to tab: $TabName (index $tabIndex)"
