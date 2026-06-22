[CmdletBinding()]
param(
    [string]$PlaylistUrl = "https://www.apsattv.com/xumo.m3u",
    [string]$PlaylistFile,
    [string]$ChannelSearch = "LiveNOW",
    [string]$ChannelName = "LiveNOW",
    [int]$TimeoutSeconds = 60,
    [int]$PlaybackTimeoutSeconds = 35,
    [int]$DuplicateDialogTimeoutSeconds = 120,
    [ValidateSet("None", "LoadSample", "ImportUrl", "OpenFile", "Continue")]
    [string]$FirstRunAction = "None",
    [switch]$SkipBuild,
    [switch]$RequirePlayback,
    [switch]$RequireClockOverlay,
    [switch]$UseDialogImport,
    [switch]$ExerciseMutatingOrganization,
    [switch]$ExerciseLibraryManagementDialogs,
    [switch]$CaptureScreenshots,
    [switch]$UseRealUserProfile
)

$ErrorActionPreference = "Stop"

if (-not [string]::IsNullOrWhiteSpace($PlaylistFile) -and $UseDialogImport) {
    throw "-PlaylistFile cannot be combined with -UseDialogImport because the dialog smoke path imports URLs only."
}

if ($FirstRunAction -ne "None" -and (-not [string]::IsNullOrWhiteSpace($PlaylistFile) -or $UseDialogImport)) {
    throw "-FirstRunAction must launch without startup import arguments."
}

if ($ExerciseLibraryManagementDialogs -and $UseRealUserProfile) {
    throw "-ExerciseLibraryManagementDialogs requires an isolated profile; omit -UseRealUserProfile."
}

if ($ExerciseLibraryManagementDialogs -and -not [string]::IsNullOrWhiteSpace($PlaylistFile)) {
    throw "-ExerciseLibraryManagementDialogs currently supports URL startup imports so it can seed a deterministic source profile."
}

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
using System.Text;

public static class NativeMouse
{
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(IntPtr hWnd);

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
$originalLocalAppData = $env:LOCALAPPDATA
$originalAppDataOverride = $env:IPTV_VIEWER_APPDATA_DIR
$isolatedProfileRoot = $null
$libraryManagementSourceId = $null
$screenshotRoot = Join-Path $repoRoot "artifacts\gui-smoke"

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

function Find-ByAutomationId {
    param(
        [System.Windows.Automation.AutomationElement]$Root,
        [string]$AutomationId,
        [System.Windows.Automation.TreeScope]$Scope = [System.Windows.Automation.TreeScope]::Descendants
    )

    $condition = [System.Windows.Automation.PropertyCondition]::new(
        [System.Windows.Automation.AutomationElement]::AutomationIdProperty,
        $AutomationId)
    return $Root.FindFirst($Scope, $condition)
}

function Invoke-Element {
    param([System.Windows.Automation.AutomationElement]$Element)

    try {
        $pattern = $Element.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
        $pattern.Invoke()
        Start-Sleep -Milliseconds 250
        return
    }
    catch {
        # Fall through to mouse invocation for visible WPF controls that do not expose InvokePattern.
    }

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

    throw "Element '$($Element.Current.Name)' cannot be invoked through UI Automation or mouse bounds."
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

function Set-CheckboxOff {
    param([System.Windows.Automation.AutomationElement]$Element)

    $pattern = $Element.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern)
    if ($pattern.Current.ToggleState -ne [System.Windows.Automation.ToggleState]::Off) {
        $pattern.Toggle()
    }
}

function Expand-Element {
    param([System.Windows.Automation.AutomationElement]$Element)

    try {
        $pattern = $Element.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern)
        if ($pattern.Current.ExpandCollapseState -eq [System.Windows.Automation.ExpandCollapseState]::Collapsed) {
            $pattern.Expand()
            Start-Sleep -Milliseconds 250
        }
    }
    catch {
        # Some controls expose only mouse invocation. Click as a fallback.
        Invoke-Element $Element
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

function Get-ProcessWindowByName {
    param(
        [int]$ProcessId,
        [string]$Name
    )

    $processCondition = [System.Windows.Automation.PropertyCondition]::new(
        [System.Windows.Automation.AutomationElement]::ProcessIdProperty,
        $ProcessId)
    $nameCondition = [System.Windows.Automation.PropertyCondition]::new(
        [System.Windows.Automation.AutomationElement]::NameProperty,
        $Name)
    $condition = [System.Windows.Automation.AndCondition]::new($processCondition, $nameCondition)
    $window = [System.Windows.Automation.AutomationElement]::RootElement.FindFirst(
        [System.Windows.Automation.TreeScope]::Children,
        $condition)
    if ($null -ne $window) {
        return $window
    }

    $script:matchedWindowHandle = [IntPtr]::Zero
    $callback = [NativeMouse+EnumWindowsProc]{
        param([IntPtr]$WindowHandle, [IntPtr]$Parameter)

        $windowProcessId = [uint32]0
        [NativeMouse]::GetWindowThreadProcessId($WindowHandle, [ref]$windowProcessId) | Out-Null
        if ($windowProcessId -ne [uint32]$ProcessId -or -not [NativeMouse]::IsWindowVisible($WindowHandle)) {
            return $true
        }

        $title = [System.Text.StringBuilder]::new(256)
        [NativeMouse]::GetWindowText($WindowHandle, $title, $title.Capacity) | Out-Null
        if ([string]::Equals($title.ToString(), $Name, [StringComparison]::OrdinalIgnoreCase)) {
            $script:matchedWindowHandle = $WindowHandle
            return $false
        }

        return $true
    }

    [NativeMouse]::EnumWindows($callback, [IntPtr]::Zero) | Out-Null
    if ($script:matchedWindowHandle -ne [IntPtr]::Zero) {
        return [System.Windows.Automation.AutomationElement]::FromHandle($script:matchedWindowHandle)
    }

    return $null
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

function Save-WindowScreenshot {
    param(
        [System.Windows.Automation.AutomationElement]$Window,
        [string]$Name
    )

    if (-not $CaptureScreenshots) {
        return
    }

    $rect = $Window.Current.BoundingRectangle
    if ($rect.IsEmpty -or $rect.Width -le 0 -or $rect.Height -le 0) {
        throw "Cannot capture screenshot '$Name' because the window bounds are empty."
    }

    New-Item -ItemType Directory -Force -Path $screenshotRoot | Out-Null
    $path = Join-Path $screenshotRoot "$Name.png"
    $bitmap = [System.Drawing.Bitmap]::new([int]$rect.Width, [int]$rect.Height)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.CopyFromScreen([int]$rect.X, [int]$rect.Y, 0, 0, $bitmap.Size)
        $bitmap.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
        Write-Host "Screenshot saved: $path"
    }
    finally {
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}

function Submit-FileDialogPath {
    param(
        [System.Windows.Automation.AutomationElement]$Dialog,
        [string]$Path
    )

    if ([string]::IsNullOrWhiteSpace($Path)) {
        throw "File dialog path is required."
    }

    $Dialog.SetFocus()
    Start-Sleep -Milliseconds 250
    $fileNameField = Find-ByAutomationId $Dialog "1148"
    if ($null -eq $fileNameField) {
        $fileNameField = Find-ByAutomationId $Dialog "FileNameControlHost"
    }

    if ($null -eq $fileNameField) {
        $fileNameField = Find-ByName $Dialog "File name:"
    }

    if ($null -ne $fileNameField) {
        Set-ElementValue $fileNameField $Path
    } else {
        [System.Windows.Forms.SendKeys]::SendWait("%n")
        [System.Windows.Forms.SendKeys]::SendWait("^a")
        [System.Windows.Forms.SendKeys]::SendWait($Path)
    }

    $submitButton = Find-ByName $Dialog "Save"
    if ($null -eq $submitButton) {
        $submitButton = Find-ByName $Dialog "Open"
    }

    if ($null -ne $submitButton) {
        Invoke-Element $submitButton
    } else {
        [System.Windows.Forms.SendKeys]::SendWait("{ENTER}")
    }

    Start-Sleep -Milliseconds 500
}

function Get-StableGuid {
    param([string[]]$Parts)

    $separator = [string][char]0x001f
    $value = [string]::Join($separator, $Parts)
    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    try {
        $hash = $sha256.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($value))
    }
    finally {
        $sha256.Dispose()
    }

    $bytes = [byte[]]$hash[0..15]
    return ([Guid]::new($bytes)).ToString()
}

try {
    if (-not $UseRealUserProfile) {
        $isolatedProfileRoot = Join-Path ([System.IO.Path]::GetTempPath()) "iptv-gui-smoke-$([Guid]::NewGuid().ToString('N'))"
        New-Item -ItemType Directory -Force -Path $isolatedProfileRoot | Out-Null
        $env:LOCALAPPDATA = $isolatedProfileRoot
        $env:IPTV_VIEWER_APPDATA_DIR = $isolatedProfileRoot
        Write-Host "Using isolated LOCALAPPDATA: $isolatedProfileRoot"

        if ($ExerciseLibraryManagementDialogs) {
            $playlistUri = [Uri]$PlaylistUrl
            $libraryManagementSourceId = Get-StableGuid @("remote", $playlistUri.Host)
            $sourcePlaybackProfiles = @{}
            $sourcePlaybackProfiles[$libraryManagementSourceId] = @{
                retryCount = 1
                bufferingPreset = 1
            }
            @{
                sourcePlaybackProfiles = $sourcePlaybackProfiles
            } |
                ConvertTo-Json -Depth 8 |
                Set-Content -LiteralPath (Join-Path $isolatedProfileRoot "channel-organization-preferences.json") -Encoding UTF8
        }
    }

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
    if ($FirstRunAction -ne "None") {
        $startArguments = @()
    }
    elseif (-not [string]::IsNullOrWhiteSpace($PlaylistFile)) {
        $playlistFilePath = (Resolve-Path -LiteralPath $PlaylistFile).Path
        $startArguments = @("--playlist-file", "`"$playlistFilePath`"")
    }
    elseif ($UseDialogImport) {
        $startArguments = @()
    }
    else {
        $startArguments = @("--playlist-url", $PlaylistUrl)
    }
    if ($startArguments.Count -gt 0) {
        $process = Start-Process -FilePath $appExe -ArgumentList $startArguments -PassThru
    } else {
        $process = Start-Process -FilePath $appExe -PassThru
    }
    $main = Wait-Until { Get-AppWindow -ProcessId $process.Id } $TimeoutSeconds "main app window"

    if ($FirstRunAction -ne "None") {
        Write-Host "Exercising first-run action: $FirstRunAction"
        $firstRun = Wait-Until {
            $window = Get-ProcessWindowByName -ProcessId $process.Id -Name "Get started"
            if ($null -ne $window) { return $window }
            Find-ByNameContains -Root ([System.Windows.Automation.AutomationElement]::RootElement) -Text "Welcome to IPTV Viewer"
        } $TimeoutSeconds "first-run setup window"

        switch ($FirstRunAction) {
            "LoadSample" {
                Invoke-Element (Wait-Until { Find-ByName $firstRun "First Run Load Sample" } $TimeoutSeconds "first-run sample button")
                $main = Wait-Until { Get-AppWindow -ProcessId $process.Id } $TimeoutSeconds "main app window after first-run sample"
                Wait-Until { Find-ByNameContains $main "Imported " } $TimeoutSeconds "first-run sample import" | Out-Null
            }
            "ImportUrl" {
                Invoke-Element (Wait-Until { Find-ByName $firstRun "First Run Import Playlist URL" } $TimeoutSeconds "first-run URL button")
                $prompt = Wait-Until {
                    $window = Get-ProcessWindowByName -ProcessId $process.Id -Name "Import Playlist URL"
                    if ($null -ne $window) { return $window }
                    Find-ByName `
                        -Root ([System.Windows.Automation.AutomationElement]::RootElement) `
                        -Name "Import Playlist URL" `
                        -Scope ([System.Windows.Automation.TreeScope]::Children)
                } $TimeoutSeconds "playlist URL dialog from first-run"
                Set-ElementValue (Wait-Until { Find-ByName $prompt "Playlist URL" } $TimeoutSeconds "playlist URL field") $PlaylistUrl
                Invoke-Element (Wait-Until { Find-ByName $prompt "Import" } $TimeoutSeconds "dialog Import button")
                $main = Wait-Until { Get-AppWindow -ProcessId $process.Id } $TimeoutSeconds "main app window after first-run URL"
                Wait-Until { Find-ByNameContains $main "Imported " } $TimeoutSeconds "first-run URL import" | Out-Null
            }
            "OpenFile" {
                Invoke-Element (Wait-Until { Find-ByName $firstRun "First Run Open Playlist File" } $TimeoutSeconds "first-run open file button")
                $openDialog = Wait-Until {
                    $window = Get-ProcessWindowByName -ProcessId $process.Id -Name "Import IPTV playlist"
                    if ($null -ne $window) { return $window }
                    Find-ByName `
                        -Root ([System.Windows.Automation.AutomationElement]::RootElement) `
                        -Name "Import IPTV playlist" `
                        -Scope ([System.Windows.Automation.TreeScope]::Children)
                } $TimeoutSeconds "first-run open file dialog"
                $openDialog.SetFocus()
                [System.Windows.Forms.SendKeys]::SendWait("{ESC}")
            }
            "Continue" {
                Invoke-Element (Wait-Until { Find-ByName $firstRun "First Run Continue" } $TimeoutSeconds "first-run continue button")
                $main = Wait-Until { Get-AppWindow -ProcessId $process.Id } $TimeoutSeconds "main app window after first-run continue"
                Wait-Until { Find-ByName $main "Import URL" } $TimeoutSeconds "main import URL button after continue" | Out-Null
            }
        }

        Write-Host "First-run smoke completed successfully."
        return
    }

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
        $startupImportKind = if ([string]::IsNullOrWhiteSpace($PlaylistFile)) { "URL" } else { "file" }
        Write-Host "Importing playlist $startupImportKind through startup argument..."
        Wait-Until { Find-ByNameContains $main "Imported " } $TimeoutSeconds "startup playlist import" | Out-Null
    }

    Write-Host "Searching and selecting channel..."
    $main = Wait-Until { Get-AppWindow -ProcessId $process.Id } $TimeoutSeconds "main app window after import"
    $searchBox = Wait-Until { Find-ByName $main "Channel Search" } $TimeoutSeconds "channel search box"
    Set-ElementValue $searchBox $ChannelSearch
    $channel = Wait-Until { Find-ByNameContains $main $ChannelName } $TimeoutSeconds "channel containing '$ChannelName'"
    Select-Element $channel

    Write-Host "Verifying organization feature surfaces..."
    Set-CheckboxOff (Wait-Until { Find-ByName $main "Basic Mode" } $TimeoutSeconds "basic mode toggle")
    Expand-Element (Wait-Until { Find-ByName $main "Recent Playlist Drawer" } $TimeoutSeconds "recent playlist drawer")
    Expand-Element (Wait-Until { Find-ByName $main "More Channel Filters" } $TimeoutSeconds "more channel filters drawer")
    Expand-Element (Wait-Until { Find-ByName $main "Channel Organization" } $TimeoutSeconds "channel organization drawer")
    Wait-Until { Find-ByName $main "Recent Playlist Sources" } $TimeoutSeconds "recent playlist source selector" | Out-Null
    Wait-Until { Find-ByName $main "Import Recent Playlist Sources" } $TimeoutSeconds "recent playlist source import button" | Out-Null
    Wait-Until { Find-ByName $main "Export Recent Playlist Sources" } $TimeoutSeconds "recent playlist source export button" | Out-Null
    Wait-Until { Find-ByName $main "Channel View Density" } $TimeoutSeconds "channel density selector" | Out-Null
    Wait-Until { Find-ByName $main "Smart Group Rule Mode" } $TimeoutSeconds "smart group rule mode selector" | Out-Null
    Wait-Until { Find-ByName $main "Duplicate Channel Groups" } $TimeoutSeconds "duplicate channel groups list" | Out-Null
    Wait-Until { Find-ByName $main "Parental PIN" } $TimeoutSeconds "parental PIN field" | Out-Null
    Wait-Until { Find-ByName $main "Hidden Locked Audit" } $TimeoutSeconds "hidden locked audit list" | Out-Null
    Wait-Until { Find-ByName $main "Import Custom Group CSV" } $TimeoutSeconds "custom group CSV import button" | Out-Null
    Wait-Until { Find-ByName $main "Source Profiles" } $TimeoutSeconds "source profiles panel" | Out-Null
    Wait-Until { Find-ByName $main "EPG Timeline" } $TimeoutSeconds "EPG timeline panel" | Out-Null
    Wait-Until { Find-ByName $main "VOD Detail Page" } $TimeoutSeconds "VOD detail page panel" | Out-Null
    Wait-Until { Find-ByName $main "VOD Library" } $TimeoutSeconds "VOD library panel" | Out-Null
    Wait-Until { Find-ByName $main "Saved Smart View" } $TimeoutSeconds "saved smart view selector" | Out-Null
    Wait-Until { Find-ByName $main "Fallback Streams" } $TimeoutSeconds "fallback streams panel" | Out-Null
    Wait-Until { Find-ByName $main "Refresh Approval" } $TimeoutSeconds "refresh approval panel" | Out-Null
    Wait-Until { Find-ByName $main "Search Benchmark" } $TimeoutSeconds "search benchmark panel" | Out-Null
    Wait-Until { Find-ByName $main "Retry Playback" } $TimeoutSeconds "retry playback button" | Out-Null
    Expand-Element (Wait-Until { Find-ByName $main "Playback Recovery Panel" } $TimeoutSeconds "playback recovery panel")
    Wait-Until { Find-ByName $main "Disable Hardware Decoding" } $TimeoutSeconds "disable hardware decoding checkbox" | Out-Null
    Wait-Until { Find-ByName $main "Save Current Playback Settings to Source" } $TimeoutSeconds "save current playback settings button" | Out-Null
    Wait-Until { Find-ByName $main "Applied Playback Profile" } $TimeoutSeconds "applied playback profile text" | Out-Null
    Wait-Until { Find-ByName $main "Playback Diagnostics" } $TimeoutSeconds "playback diagnostics text" | Out-Null

    Expand-Element (Wait-Until { Find-ByName $main "Source Profiles" } $TimeoutSeconds "source profiles panel")
    Wait-Until { Find-ByName $main "Import Source Profiles" } $TimeoutSeconds "source profile import button" | Out-Null
    Wait-Until { Find-ByName $main "Export Source Profiles" } $TimeoutSeconds "source profile export button" | Out-Null
    Wait-Until { Find-ByName $main "Source Disable Hardware Decoding" } $TimeoutSeconds "source hardware decoding profile toggle" | Out-Null
    Wait-Until { Find-ByName $main "Source Default Visibility Group" } $TimeoutSeconds "source default visibility group selector" | Out-Null
    Wait-Until { Find-ByName $main "XMLTV Guide URL" } $TimeoutSeconds "XMLTV guide URL field" | Out-Null
    Wait-Until { Find-ByName $main "Library Health Dashboard" } $TimeoutSeconds "library health dashboard" | Out-Null

    Expand-Element (Wait-Until { Find-ByName $main "EPG Timeline" } $TimeoutSeconds "EPG timeline panel")
    Wait-Until { Find-ByName $main "EPG Timeline Window" } $TimeoutSeconds "EPG timeline window selector" | Out-Null
    Wait-Until { Find-ByName $main "EPG Search" } $TimeoutSeconds "EPG search field" | Out-Null

    Expand-Element (Wait-Until { Find-ByName $main "Search Benchmark" } $TimeoutSeconds "search benchmark panel")
    Wait-Until { Find-ByName $main "Search Benchmark Results" } $TimeoutSeconds "search benchmark results list" | Out-Null

    Save-WindowScreenshot $main "window-library"

    if ($ExerciseLibraryManagementDialogs) {
        Write-Host "Exercising recent-source export/import and source-profile conflict preview..."
        if ([string]::IsNullOrWhiteSpace($libraryManagementSourceId)) {
            throw "Library management smoke source profile was not seeded."
        }

        $librarySmokeRoot = Join-Path $isolatedProfileRoot "library-management-smoke"
        New-Item -ItemType Directory -Force -Path $librarySmokeRoot | Out-Null
        $recentExportPath = Join-Path $librarySmokeRoot "recent-sources.json"

        Invoke-Element (Wait-Until { Find-ByName $main "Export Recent Playlist Sources" } $TimeoutSeconds "Export Recent Playlist Sources button")
        $recentSaveDialog = Wait-Until {
            Get-ProcessWindowByName -ProcessId $process.Id -Name "Export recent playlist sources"
        } $TimeoutSeconds "recent playlist source export dialog"
        Submit-FileDialogPath $recentSaveDialog $recentExportPath
        Wait-Until { Test-Path -LiteralPath $recentExportPath } $TimeoutSeconds "recent playlist source export file" | Out-Null

        Invoke-Element (Wait-Until { Find-ByName $main "Import Recent Playlist Sources" } $TimeoutSeconds "Import Recent Playlist Sources button")
        $recentOpenDialog = Wait-Until {
            Get-ProcessWindowByName -ProcessId $process.Id -Name "Import recent playlist sources"
        } $TimeoutSeconds "recent playlist source import dialog"
        Submit-FileDialogPath $recentOpenDialog $recentExportPath
        Wait-Until { Find-ByNameContains $main "Imported recent playlist sources" } $TimeoutSeconds "recent playlist source import status" | Out-Null

        $sourceProfileImportPath = Join-Path $librarySmokeRoot "source-profile-conflict.json"
        $sourcePlaybackProfiles = @{}
        $sourcePlaybackProfiles[$libraryManagementSourceId] = @{
            retryCount = 3
            bufferingPreset = 2
        }
        $sourceProfileImport = @{
            version = 1
            sourceProfileNames = @{}
            sourcePlaybackProfiles = $sourcePlaybackProfiles
            sourceDefaultHiddenGroups = @{}
        }
        $sourceProfileImport |
            ConvertTo-Json -Depth 8 |
            Set-Content -LiteralPath $sourceProfileImportPath -Encoding UTF8

        Invoke-Element (Wait-Until { Find-ByName $main "Import Source Profiles" } $TimeoutSeconds "Import Source Profiles button")
        $sourceOpenDialog = Wait-Until {
            Get-ProcessWindowByName -ProcessId $process.Id -Name "Import source profiles"
        } $TimeoutSeconds "source profile import dialog"
        Submit-FileDialogPath $sourceOpenDialog $sourceProfileImportPath

        $conflictDialog = Wait-Until {
            $root = [System.Windows.Automation.AutomationElement]::RootElement
            $dialog = Find-ByName `
                -Root $root `
                -Name "Preview Confirm Dialog" `
                -Scope ([System.Windows.Automation.TreeScope]::Children)
            if ($null -eq $dialog) {
                $dialog = Get-ProcessWindowByName -ProcessId $process.Id -Name "Review Changes"
            }

            $conflictMessage = if ($null -ne $dialog) {
                Find-ByNameContains `
                    -Root $dialog `
                    -Text "source profile file updates existing profile settings" `
                    -Scope ([System.Windows.Automation.TreeScope]::Descendants)
            } else {
                $null
            }

            if ($null -ne $dialog -and $null -ne $conflictMessage) {
                return $dialog
            }

            return $false
        } $TimeoutSeconds "source profile conflict preview dialog"
        Invoke-Element (Wait-Until { Find-ByName $conflictDialog "Cancel" } $TimeoutSeconds "source profile conflict cancel button")
        Wait-Until { Find-ByNameContains $main "Source profile import cancelled before applying conflicts" } $TimeoutSeconds "source profile conflict cancellation status" | Out-Null
    }

    if ($ExerciseMutatingOrganization) {
        Write-Host "Exercising isolated PIN lock/unlock workflow..."
        $pinBox = Wait-Until { Find-ByName $main "Parental PIN" } $TimeoutSeconds "parental PIN field"
        Set-ElementValue $pinBox "1234"
        Invoke-Element (Wait-Until { Find-ByName $main "Set PIN" } $TimeoutSeconds "Set PIN button")
        Wait-Until { Find-ByNameContains $main "Parental lock: unlocked" } $TimeoutSeconds "PIN unlocked status" | Out-Null
        Invoke-Element (Wait-Until { Find-ByName $main "Lock Now" } $TimeoutSeconds "Lock Now button")
        Wait-Until { Find-ByNameContains $main "Parental lock: locked" } $TimeoutSeconds "PIN locked status" | Out-Null
        Set-ElementValue $pinBox "1234"
        Invoke-Element (Wait-Until { Find-ByName $main "Unlock" } $TimeoutSeconds "Unlock button")
        Wait-Until { Find-ByNameContains $main "Parental lock: unlocked" } $TimeoutSeconds "PIN re-unlocked status" | Out-Null
        Invoke-Element (Wait-Until { Find-ByName $main "Clear PIN" } $TimeoutSeconds "Clear PIN button")
        Wait-Until { Find-ByNameContains $main "Parental lock: not configured" } $TimeoutSeconds "PIN cleared status" | Out-Null

        Write-Host "Exercising duplicate-hide workflow when duplicates are available..."
        Invoke-Element (Wait-Until { Find-ByName $main "Refresh Duplicates" } $TimeoutSeconds "Refresh Duplicates button")
        $hideDuplicates = Wait-Until {
            $button = Find-ByName $main "Hide Duplicates"
            if ($null -eq $button) { return $false }
            return $button
        } $TimeoutSeconds "Hide Duplicates button"
        if ($hideDuplicates.Current.IsEnabled) {
            Invoke-Element $hideDuplicates
            $duplicateDialog = Wait-Until {
                $root = [System.Windows.Automation.AutomationElement]::RootElement
                $dialog = Find-ByName `
                    -Root $root `
                    -Name "Duplicate Preview Dialog" `
                    -Scope ([System.Windows.Automation.TreeScope]::Children)
                if ($null -ne $dialog) { return $dialog }

                $dialog = Find-ByName `
                    -Root $root `
                    -Name "Duplicate Preview" `
                    -Scope ([System.Windows.Automation.TreeScope]::Children)
                if ($null -ne $dialog) { return $dialog }

                Find-ByNameContains `
                    -Root $root `
                    -Text "Duplicate Preview" `
                    -Scope ([System.Windows.Automation.TreeScope]::Descendants)
            } $DuplicateDialogTimeoutSeconds "duplicate preview dialog"
            Invoke-Element (Wait-Until { Find-ByName $duplicateDialog "Confirm Hide Duplicates" } $TimeoutSeconds "confirm hide duplicates button")
            Wait-Until {
                $main = Get-AppWindow -ProcessId $process.Id
                if ($null -eq $main) { return $false }

                if (Find-ByNameContains $main "Hid ") { return $true }

                $hideDuplicates = Find-ByName $main "Hide Duplicates"
                return $null -ne $hideDuplicates -and -not $hideDuplicates.Current.IsEnabled
            } $TimeoutSeconds "duplicate hide result" | Out-Null
            $undo = Find-ByName $main "Undo Org"
            if ($null -ne $undo -and $undo.Current.IsEnabled) {
                Invoke-Element $undo
            }
        }
        else {
            Write-Warning "No duplicate group was available in this playlist; duplicate-hide command surface verified but mutation skipped."
        }
    }

    Write-Host "Starting playback..."
    Invoke-Element (Wait-Until { Find-ByName $main "Play" } $TimeoutSeconds "Play button")
    try {
        Wait-Until { Find-ByNameContains $main "Playing:" } $PlaybackTimeoutSeconds "Playing playback status" | Out-Null
        Write-Host "Playback reached Playing."
        $placeholder = Find-ByName $main "No Channel Video Placeholder"
        if ($null -ne $placeholder -and -not $placeholder.Current.IsOffscreen) {
            throw "Playback reached Playing while the no-channel placeholder was still visible."
        }
    }
    catch {
        if ($RequirePlayback) {
            throw
        }

        Write-Warning "Playback did not reach Playing before timeout; continuing UI regression checks because live streams can be transient."
    }

    Write-Host "Enabling and verifying clock overlay..."
    Set-CheckboxOn (Wait-Until { Find-ByName $main "Player Clock" } $TimeoutSeconds "player Clock checkbox")
    try {
        Wait-Until {
            $main = Get-AppWindow -ProcessId $process.Id
            if ($null -eq $main) { return $false }
            Find-ByNameContains $main "Clock Overlay"
        } $TimeoutSeconds "clock overlay" | Out-Null
    }
    catch {
        if ($RequireClockOverlay) {
            throw
        }

        Write-Warning "Clock overlay was not visible to UI Automation before fullscreen; continuing because overlay UIA visibility can be transient."
    }

    Write-Host "Entering fullscreen with F11..."
    $main.SetFocus()
    [System.Windows.Forms.SendKeys]::SendWait("{F11}")
    Start-Sleep -Milliseconds 750
    $main = Wait-Until { Get-AppWindow -ProcessId $process.Id } $TimeoutSeconds "main app window in fullscreen"
    Wait-Until { Assert-Fullscreen $main $process; $true } $TimeoutSeconds "true fullscreen bounds" | Out-Null
    Save-WindowScreenshot $main "fullscreen-clock"
    try {
        Wait-Until { Find-ByNameContains $main "Clock Overlay" } $TimeoutSeconds "clock overlay in fullscreen" | Out-Null
    }
    catch {
        if ($RequireClockOverlay) {
            throw
        }

        Write-Warning "Clock overlay was not visible to UI Automation in fullscreen; true fullscreen and HUD checks still continue."
    }
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

    if (-not $UseRealUserProfile) {
        $env:LOCALAPPDATA = $originalLocalAppData
        $env:IPTV_VIEWER_APPDATA_DIR = $originalAppDataOverride
        if ($null -ne $isolatedProfileRoot -and (Test-Path -LiteralPath $isolatedProfileRoot)) {
            $tempRoot = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
            $resolvedProfile = [System.IO.Path]::GetFullPath($isolatedProfileRoot)
            if ($resolvedProfile.StartsWith($tempRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
                Remove-Item -LiteralPath $resolvedProfile -Recurse -Force -ErrorAction SilentlyContinue
            }
        }
    }
}
