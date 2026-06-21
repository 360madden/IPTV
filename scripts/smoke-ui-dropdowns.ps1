<#
.SYNOPSIS
Captures smoke-test screenshots for key IPTV Viewer dropdown states.

.DESCRIPTION
Launches the WPF app against the bundled sample playlist, expands representative dropdowns,
and saves full-screen PNG screenshots. This is intentionally a local/manual smoke test because
it needs an interactive Windows desktop.
#>
[CmdletBinding()]
param(
    [string]$Configuration = "Debug",
    [string]$OutputDirectory = "artifacts/ui-smoke/dropdowns",
    [switch]$NoBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$appProject = Join-Path $repoRoot "src/Iptv.App/Iptv.App.csproj"
$appExe = Join-Path $repoRoot "src/Iptv.App/bin/$Configuration/net10.0-windows/Iptv.App.exe"
$samplePlaylist = Join-Path $repoRoot "assets/sample-playlists/synthetic-news-sports.m3u"
$outputRoot = Join-Path $repoRoot $OutputDirectory
$appData = Join-Path ([IO.Path]::GetTempPath()) ("iptv-ui-smoke-" + [Guid]::NewGuid().ToString("N"))

if (-not $NoBuild) {
    dotnet build $appProject --configuration $Configuration --no-restore
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet build failed with exit code $LASTEXITCODE."
    }
}

if (-not (Test-Path -LiteralPath $appExe)) {
    throw "App executable not found: $appExe"
}

if (-not (Test-Path -LiteralPath $samplePlaylist)) {
    throw "Sample playlist not found: $samplePlaylist"
}

New-Item -ItemType Directory -Force -Path $outputRoot | Out-Null
New-Item -ItemType Directory -Force -Path $appData | Out-Null

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;

public static class NativeWindowMethods
{
    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
}
'@

function Activate-AppWindow {
    param(
        [Parameter(Mandatory)]$Process,
        [Parameter(Mandatory)]$Window
    )

    $Process.Refresh()
    if ($Process.MainWindowHandle -ne [IntPtr]::Zero) {
        [NativeWindowMethods]::ShowWindow($Process.MainWindowHandle, 9) | Out-Null
        [NativeWindowMethods]::SetForegroundWindow($Process.MainWindowHandle) | Out-Null
    }

    try {
        $Window.SetFocus()
    }
    catch {
        # Some automation providers reject focus while the window is still restoring.
    }

    Start-Sleep -Milliseconds 500
}

function Capture-AppWindow {
    param(
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)]$Window
    )

    $bounds = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
    $left = $bounds.Left
    $top = $bounds.Top
    $width = $bounds.Width
    $height = $bounds.Height
    $bitmap = [Drawing.Bitmap]::new($width, $height)
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.CopyFromScreen($left, $top, 0, 0, [Drawing.Size]::new($width, $height))
        $path = Join-Path $outputRoot ("$Name.png")
        $bitmap.Save($path, [Drawing.Imaging.ImageFormat]::Png)
        Write-Host "Captured $path"
    }
    finally {
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}

function Wait-ForWindow {
    param([int]$ProcessId)

    $condition = [Windows.Automation.PropertyCondition]::new(
        [Windows.Automation.AutomationElement]::ProcessIdProperty,
        $ProcessId)

    $deadline = [DateTimeOffset]::Now.AddSeconds(30)
    do {
        $window = [Windows.Automation.AutomationElement]::RootElement.FindFirst(
            [Windows.Automation.TreeScope]::Children,
            $condition)
        if ($null -ne $window) {
            return $window
        }

        Start-Sleep -Milliseconds 250
    } while ([DateTimeOffset]::Now -lt $deadline)

    throw "Timed out waiting for IPTV Viewer window."
}

function Find-ByName {
    param(
        [Parameter(Mandatory)]$Root,
        [Parameter(Mandatory)][string]$Name
    )

    $condition = [Windows.Automation.PropertyCondition]::new(
        [Windows.Automation.AutomationElement]::NameProperty,
        $Name)
    $element = $Root.FindFirst([Windows.Automation.TreeScope]::Descendants, $condition)
    if ($null -eq $element) {
        throw "Could not find UI element named '$Name'."
    }

    return $element
}

function Expand-Element {
    param([Parameter(Mandatory)]$Element)

    $pattern = $null
    if ($Element.TryGetCurrentPattern([Windows.Automation.ExpandCollapsePattern]::Pattern, [ref]$pattern)) {
        if ($pattern.Current.ExpandCollapseState -ne [Windows.Automation.ExpandCollapseState]::Expanded) {
            $pattern.Expand()
            Start-Sleep -Milliseconds 450
        }
        return
    }

    throw "Element '$($Element.Current.Name)' does not support ExpandCollapsePattern."
}

$oldAppData = [Environment]::GetEnvironmentVariable("IPTV_VIEWER_APPDATA_DIR", "Process")
[Environment]::SetEnvironmentVariable("IPTV_VIEWER_APPDATA_DIR", $appData, "Process")
$process = $null
try {
    $process = Start-Process -FilePath $appExe -ArgumentList "--playlist-file=`"$samplePlaylist`"" -PassThru
    $window = Wait-ForWindow -ProcessId $process.Id
    Start-Sleep -Seconds 4
    Activate-AppWindow -Process $process -Window $window
    Capture-AppWindow -Name "01-initial-no-channel-placeholder" -Window $window

    Activate-AppWindow -Process $process -Window $window
    Expand-Element (Find-ByName -Root $window -Name "Channel Sort Mode")
    Start-Sleep -Milliseconds 450
    Capture-AppWindow -Name "02-channel-sort-dropdown" -Window $window

    Activate-AppWindow -Process $process -Window $window
    Expand-Element (Find-ByName -Root $window -Name "UI Settings")
    Start-Sleep -Milliseconds 450
    Expand-Element (Find-ByName -Root $window -Name "Application Theme")
    Start-Sleep -Milliseconds 450
    Capture-AppWindow -Name "03-theme-dropdown" -Window $window

    Activate-AppWindow -Process $process -Window $window
    Expand-Element (Find-ByName -Root $window -Name "Application UI Scale")
    Start-Sleep -Milliseconds 450
    Capture-AppWindow -Name "04-ui-scale-dropdown" -Window $window

    Write-Host "UI dropdown smoke screenshots completed: $outputRoot"
}
finally {
    [Environment]::SetEnvironmentVariable("IPTV_VIEWER_APPDATA_DIR", $oldAppData, "Process")
    if ($null -ne $process -and -not $process.HasExited) {
        Stop-Process -Id $process.Id -Force
    }

    if (Test-Path -LiteralPath $appData) {
        Remove-Item -LiteralPath $appData -Recurse -Force
    }
}
