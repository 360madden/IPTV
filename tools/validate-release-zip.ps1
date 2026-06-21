[CmdletBinding()]
param(
    [string]$ZipPath = ".\artifacts\release\IptvViewer-win-x64.zip",
    [int]$LaunchSeconds = 0,
    [string]$PlaylistFile
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot

function Resolve-RepoPath {
    param([string]$Path)

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $repoRoot $Path))
}

$zipFullPath = Resolve-RepoPath $ZipPath
if (-not (Test-Path -LiteralPath $zipFullPath -PathType Leaf)) {
    throw "Release ZIP was not found: $zipFullPath"
}

$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) "iptv-zip-validate-$([Guid]::NewGuid().ToString('N'))"
$process = $null
try {
    New-Item -ItemType Directory -Force -Path $tempRoot | Out-Null
    Expand-Archive -LiteralPath $zipFullPath -DestinationPath $tempRoot -Force

    $exe = Get-ChildItem -LiteralPath $tempRoot -Recurse -Filter "Iptv.App.exe" -File |
        Select-Object -First 1
    if ($null -eq $exe) {
        throw "Iptv.App.exe was not found after extracting $zipFullPath"
    }

    $runtimeConfig = Join-Path $exe.DirectoryName "Iptv.App.runtimeconfig.json"
    $deps = Join-Path $exe.DirectoryName "Iptv.App.deps.json"
    $vlcDirectory = Join-Path $exe.DirectoryName "libvlc"
    foreach ($requiredPath in @($runtimeConfig, $deps, $vlcDirectory)) {
        if (-not (Test-Path -LiteralPath $requiredPath)) {
            throw "Required release asset missing: $requiredPath"
        }
    }

    if ($LaunchSeconds -gt 0) {
        $arguments = @()
        if (-not [string]::IsNullOrWhiteSpace($PlaylistFile)) {
            $playlistFullPath = Resolve-RepoPath $PlaylistFile
            if (-not (Test-Path -LiteralPath $playlistFullPath -PathType Leaf)) {
                throw "Playlist file was not found: $playlistFullPath"
            }

            $arguments = @("--playlist-file", "`"$playlistFullPath`"")
        }

        if ($arguments.Count -gt 0) {
            $process = Start-Process -FilePath $exe.FullName -ArgumentList $arguments -WindowStyle Hidden -PassThru
        } else {
            $process = Start-Process -FilePath $exe.FullName -WindowStyle Hidden -PassThru
        }
        Start-Sleep -Seconds $LaunchSeconds
        if ($process.HasExited) {
            throw "Release executable exited during the ${LaunchSeconds}s launch check with code $($process.ExitCode)."
        }
    }

    Write-Host "Release ZIP validation passed: $zipFullPath"
}
finally {
    if ($null -ne $process -and -not $process.HasExited) {
        $process.CloseMainWindow() | Out-Null
        if (-not $process.WaitForExit(3000)) {
            Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
        }
    }

    if (Test-Path -LiteralPath $tempRoot) {
        Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}
