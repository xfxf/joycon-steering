param([string]$TabName = "Pedals")

Add-Type -AssemblyName UIAutomationClient,UIAutomationTypes,WindowsBase

$proc = Get-Process -Name JoyconSteering -ErrorAction SilentlyContinue
if (-not $proc) { Write-Error "JoyconSteering not running."; exit 1 }

$root = [System.Windows.Automation.AutomationElement]::RootElement
$pidProp = [System.Windows.Automation.AutomationElement]::ProcessIdProperty
$nameProp = [System.Windows.Automation.AutomationElement]::NameProperty
$pidCond = New-Object System.Windows.Automation.PropertyCondition($pidProp, $proc.Id)

# Find ANY window of this process whose name contains "Settings" — title uses an em dash.
$wins = $root.FindAll([System.Windows.Automation.TreeScope]::Children, $pidCond)
$win = $null
foreach ($w in $wins) {
    $n = $w.Current.Name
    if ($n -and $n.IndexOf("Settings", [StringComparison]::OrdinalIgnoreCase) -ge 0) { $win = $w; break }
}
if (-not $win) {
    Write-Host "All windows for pid $($proc.Id):"
    foreach ($w in $wins) { Write-Host "  '$($w.Current.Name)' (class=$($w.Current.ClassName))" }
    Write-Error "Settings window not found."
    exit 1
}

$tabCond = New-Object System.Windows.Automation.PropertyCondition($nameProp, $TabName)
$tab = $win.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $tabCond)
if (-not $tab) { Write-Error "Tab '$TabName' not found."; exit 1 }
$sel = $tab.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern)
$sel.Select()
Write-Host "Switched to tab: $TabName"
