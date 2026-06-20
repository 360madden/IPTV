[CmdletBinding()]
param(
    [string]$PlaylistUrl = "https://www.apsattv.com/xumo.m3u",
    [string]$ChannelSearch = "LiveNOW",
    [string]$ChannelName = "LiveNOW",
    [int]$TimeoutSeconds = 60,
    [int]$PlaybackTimeoutSeconds = 35,
    [switch]$SkipBuild,
    [switch]$RequirePlayback,
    [switch]$UseDialogImport
)

$ErrorActionPreference = "Stop"

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Windows.Forms
Add-Type @"
using System;
using System.Runtime.InteropServices;

public static class NativeMouse
{
    [DllImport("user32.dll")]
    public static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    public static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extraInfo);
}
"@

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$appProject = Join-Path $repoRoot "src\Iptv.App\Iptv.App.csproj"
$appExe = Join-Path $repoRoot "src\Iptv.App\bin\Debug\net10.0-windows\Iptv.App.exe"
$process = $null

function Wait-Until {
    param(
        [scriptblock]$Condition,
        [int]$Seconds,
        [string]$Message
    )

    $deadline = [DateTimeOffset]::Now.AddSeconds($Seconds)
    $lastError = $null
    do {
        try {
            $value = & $Condition
            $lastError = $null
        }
        catch {
            $value = $false
            $lastError = $_
        }

        if ($null -ne $value -and $value -ne $false) {
            return $value
        }

        Start-Sleep -Milliseconds 250
    } while ([DateTimeOffset]::Now -lt $deadline)

    if ($null -ne $lastError) {
        throw "Timed out waiting for $Message. Last error: $lastError"
    }

    throw "Timed out waiting for $Message"
}

function Find-ByName {
    param(
        [System.Windows.Automation.AutomationElement]$Root,
        [string]$Name,
        [System.Windows.Automation.TreeScope]$Scope = [System.Windows.Automation.TreeScope]::Descendants
    )

    $condition = [System.Windows.Automation.PropertyCondition]::new(
        [System.Windows.Automation.AutomationElement]::NameProperty,
        $Name)
    return $Root.FindFirst($Scope, $condition)
}

function Find-ByNameContains {
    param(
        [System.Windows.Automation.AutomationElement]$Root,
        [string]$Text,
        [System.Windows.Automation.TreeScope]$Scope = [System.Windows.Automation.TreeScope]::Descendants
    )

    $elements = $Root.FindAll($Scope, [System.Windows.Automation.Condition]::TrueCondition)
    for ($i = 0; $i -lt $elements.Count; $i++) {
        $element = $elements.Item($i)
        $name = $element.Current.Name
        if (-not [string]::IsNullOrWhiteSpace($name) -and
            $name.IndexOf($Text, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
            return $element
        }
    }

    return $null
}

function Invoke-Element {
    param([System.Windows.Automation.AutomationElement]$Element)

    $rect = $Element.Current.BoundingRectangle
    if (-not $rect.IsEmpty -and $rect.Width -gt 0 -and $rect.Height -gt 0) {
        $x = [int]($rect.X + ($rect.Width / 2))
        $y = [int]($rect.Y + ($rect.Height / 2))
        try {
            $Element.SetFocus()
        }
        catch {
            # Some WPF elements report bounding rectangles but cannot receive UIA focus.
            # Mouse invocation remains valid for visible controls.
        }
        [NativeMouse]::SetCursorPos($x, $y) | Out-Null
        [NativeMouse]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero)
        [NativeMouse]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero)
        Start-Sleep -Milliseconds 250
        return
    }

    $pattern = $Element.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
    $pattern.Invoke()
}

function Set-ElementValue {
    param(
        [System.Windows.Automation.AutomationElement]$Element,
        [string]$Value
    )

    try {
        $pattern = $Element.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)
        $pattern.SetValue($Value)
    }
    catch {
        $Element.SetFocus()
        [System.Windows.Forms.SendKeys]::SendWait("^a")
        [System.Windows.Forms.SendKeys]::SendWait($Value)
    }
}

function Set-CheckboxOn {
    param([System.Windows.Automation.AutomationElement]$Element)

    $pattern = $Element.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern)
    if ($pattern.Current.ToggleState -ne [System.Windows.Automation.ToggleState]::On) {
        $pattern.Toggle()
    }
}

function Select-Element {
    param([System.Windows.Automation.AutomationElement]$Element)

    try {
        $pattern = $Element.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern)
        $pattern.Select()
    }
    catch {
        $Element.SetFocus()
    }
}

function Get-AppWindow {
    param([int]$ProcessId)

    $condition = [System.Windows.Automation.PropertyCondition]::new(
        [System.Windows.Automation.AutomationElement]::ProcessIdProperty,
        $ProcessId)
    $windows = [System.Windows.Automation.AutomationElement]::RootElement.FindAll(
        [System.Windows.Automation.TreeScope]::Children,
        $condition)
    $best = $null
    $bestArea = -1
    for ($i = 0; $i -lt $windows.Count; $i++) {
        $candidate = $windows.Item($i)
        $rect = $candidate.Current.BoundingRectangle
        $area = if ($rect.IsEmpty) { 0 } else { [double]$rect.Width * [double]$rect.Height }
        if ($area -gt $bestArea) {
            $best = $candidate
            $bestArea = $area
        }
    }

    return $best
}

function Assert-Fullscreen {
    param(
        [System.Windows.Automation.AutomationElement]$Window,
        [System.Diagnostics.Process]$Process
    )

    $rect = $Window.Current.BoundingRectangle
    $screen = [System.Windows.Forms.Screen]::FromHandle($Process.MainWindowHandle).Bounds
    $tolerance = 6

    $matches =
        [Math]::Abs($rect.X - $screen.X) -le $tolerance -and
        [Math]::Abs($rect.Y - $screen.Y) -le $tolerance -and
        [Math]::Abs($rect.Width - $screen.Width) -le $tolerance -and
        [Math]::Abs($rect.Height - $screen.Height) -le $tolerance

    if (-not $matches) {
        throw "Window is not true fullscreen. Window=$($rect.Width)x$($rect.Height)+$($rect.X)+$($rect.Y), Screen=$($screen.Width)x$($screen.Height)+$($screen.X)+$($screen.Y)"
    }
}

try {
    if (-not $SkipBuild) {
        Write-Host "Building app..."
        & dotnet build $appProject --no-restore
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet build failed"
        }
    }

    if (-not (Test-Path $appExe)) {
        throw "App executable not found: $appExe"
    }

    Write-Host "Launching IPTV app..."
    $startArguments = if ($UseDialogImport) { @() } else { @("--playlist-url", $PlaylistUrl) }
    $process = Start-Process -FilePath $appExe -ArgumentList $startArguments -PassThru
    $main = Wait-Until { Get-AppWindow -ProcessId $process.Id } $TimeoutSeconds "main app window"

    if ($UseDialogImport) {
        Write-Host "Importing playlist URL through dialog..."
        Invoke-Element (Wait-Until { Find-ByName $main "Import URL" } $TimeoutSeconds "Import URL button")
        $prompt = Wait-Until {
            Find-ByName `
                -Root ([System.Windows.Automation.AutomationElement]::RootElement) `
                -Name "Import Playlist URL" `
                -Scope ([System.Windows.Automation.TreeScope]::Children)
        } $TimeoutSeconds "playlist URL dialog"
        Set-ElementValue (Wait-Until { Find-ByName $prompt "Playlist URL" } $TimeoutSeconds "playlist URL field") $PlaylistUrl
        Invoke-Element (Wait-Until { Find-ByName $prompt "Import" } $TimeoutSeconds "dialog Import button")
    }
    else {
        Write-Host "Importing playlist URL through startup argument..."
        Wait-Until { Find-ByNameContains $main "Imported " } $TimeoutSeconds "startup playlist import" | Out-Null
    }

    Write-Host "Searching and selecting channel..."
    $main = Wait-Until { Get-AppWindow -ProcessId $process.Id } $TimeoutSeconds "main app window after import"
    $searchBox = Wait-Until { Find-ByName $main "Channel Search" } $TimeoutSeconds "channel search box"
    Set-ElementValue $searchBox $ChannelSearch
    $channel = Wait-Until { Find-ByNameContains $main $ChannelName } $TimeoutSeconds "channel containing '$ChannelName'"
    Select-Element $channel

    Write-Host "Starting playback..."
    Invoke-Element (Wait-Until { Find-ByName $main "Play" } $TimeoutSeconds "Play button")
    try {
        Wait-Until { Find-ByNameContains $main "Playing:" } $PlaybackTimeoutSeconds "Playing playback status" | Out-Null
        Write-Host "Playback reached Playing."
    }
    catch {
        if ($RequirePlayback) {
            throw
        }

        Write-Warning "Playback did not reach Playing before timeout; continuing UI regression checks because live streams can be transient."
    }

    Write-Host "Enabling and verifying clock overlay..."
    Set-CheckboxOn (Wait-Until { Find-ByName $main "Clock" } $TimeoutSeconds "Clock checkbox")
    Wait-Until {
        $main = Get-AppWindow -ProcessId $process.Id
        if ($null -eq $main) { return $false }
        Find-ByNameContains $main "Clock Overlay"
    } $TimeoutSeconds "clock overlay" | Out-Null

    Write-Host "Entering fullscreen with F11..."
    $main.SetFocus()
    [System.Windows.Forms.SendKeys]::SendWait("{F11}")
    Start-Sleep -Milliseconds 750
    $main = Wait-Until { Get-AppWindow -ProcessId $process.Id } $TimeoutSeconds "main app window in fullscreen"
    Wait-Until { Assert-Fullscreen $main $process; $true } $TimeoutSeconds "true fullscreen bounds" | Out-Null
    Wait-Until { Find-ByNameContains $main "Clock Overlay" } $TimeoutSeconds "clock overlay in fullscreen" | Out-Null
    Wait-Until { Find-ByNameContains $main "double-click exits fullscreen" } $TimeoutSeconds "fullscreen mini HUD" | Out-Null

    Write-Host "Checking auto-hide HUD behavior..."
    Start-Sleep -Seconds 4
    if (Find-ByNameContains $main "double-click exits fullscreen") {
        Write-Warning "Fullscreen HUD was still visible after idle; auto-hide may be disabled in saved preferences."
    }

    Write-Host "Restoring window with Escape..."
    $main.SetFocus()
    [System.Windows.Forms.SendKeys]::SendWait("{ESC}")
    Start-Sleep -Milliseconds 750
    $main = Wait-Until { Get-AppWindow -ProcessId $process.Id } $TimeoutSeconds "main app window after Escape"
    Invoke-Element (Wait-Until { Find-ByName $main "Stop" } $TimeoutSeconds "Stop button")

    Write-Host "GUI smoke completed successfully."
}
finally {
    if ($null -ne $process -and -not $process.HasExited) {
        $process.CloseMainWindow() | Out-Null
        if (-not $process.WaitForExit(3000)) {
            Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
        }
    }
}
