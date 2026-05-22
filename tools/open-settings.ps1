# Programmatically clicks the "Settings…" button in the running JoyconSteering
# diagnostics window via UI Automation, so we can screenshot the settings form.

Add-Type -AssemblyName UIAutomationClient,UIAutomationTypes,WindowsBase

$proc = Get-Process -Name JoyconSteering -ErrorAction SilentlyContinue
if (-not $proc) { Write-Error "JoyconSteering not running."; exit 1 }

# Find the diagnostics window (root child by process ID).
$root = [System.Windows.Automation.AutomationElement]::RootElement
$pidProp = [System.Windows.Automation.AutomationElement]::ProcessIdProperty
$cond = New-Object System.Windows.Automation.PropertyCondition($pidProp, $proc.Id)
$win = $root.FindFirst([System.Windows.Automation.TreeScope]::Children, $cond)
if (-not $win) { Write-Error "Main window not found via UIA."; exit 1 }

# Find the Settings button by its caption (the "…" is a real Unicode ellipsis U+2026).
$nameProp = [System.Windows.Automation.AutomationElement]::NameProperty
$ellipsis = [char]0x2026
$btnCond = New-Object System.Windows.Automation.PropertyCondition($nameProp, "Settings$ellipsis")
$btn = $win.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $btnCond)
if (-not $btn) {
    # Fall back: list all buttons by name to help debug
    $allButtons = $win.FindAll([System.Windows.Automation.TreeScope]::Descendants,
        (New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
            [System.Windows.Automation.ControlType]::Button)))
    Write-Host "Buttons found:"
    foreach ($b in $allButtons) { Write-Host "  '$($b.Current.Name)'" }
    Write-Error "Settings button not found."
    exit 1
}

$invokePatternId = [System.Windows.Automation.InvokePattern]::Pattern
$pattern = $btn.GetCurrentPattern($invokePatternId)
$pattern.Invoke()
Write-Host "Clicked Settings…"
