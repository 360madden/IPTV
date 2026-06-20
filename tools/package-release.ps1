param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$OutputRoot = ".\artifacts\release",
    [switch]$NoSelfContained,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot "src\Iptv.App\Iptv.App.csproj"
$repoRootFull = [System.IO.Path]::GetFullPath($repoRoot)
$outputRootFull = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $OutputRoot))
$publishDir = Join-Path $outputRootFull "publish-$Runtime"
$zipPath = Join-Path $outputRootFull "IptvViewer-$Runtime.zip"

if (-not (Test-Path $project)) {
    throw "App project was not found at $project"
}

$repoRootForCompare = $repoRootFull.TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
$outputForCompare = $outputRootFull.TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
if (-not $outputForCompare.StartsWith($repoRootForCompare, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "OutputRoot must resolve inside the repository for safe cleanup. Resolved path: $outputRootFull"
}

$publishArgs = @(
    "publish", $project,
    "-c", $Configuration,
    "-r", $Runtime,
    "-o", $publishDir,
    "/p:PublishSingleFile=false",
    "/p:IncludeNativeLibrariesForSelfExtract=true"
)

if ($NoSelfContained) {
    $publishArgs += "--no-self-contained"
} else {
    $publishArgs += "--self-contained"
}

Write-Host "Repository: $repoRoot"
Write-Host "Publish directory: $publishDir"
Write-Host "Zip: $zipPath"
Write-Host "dotnet $($publishArgs -join ' ')"

if ($DryRun) {
    Write-Host "Dry run complete. No files were written."
    return
}

New-Item -ItemType Directory -Force -Path $outputRootFull | Out-Null
if (Test-Path $publishDir) {
    Remove-Item -LiteralPath $publishDir -Recurse -Force
}

dotnet @publishArgs

if (Test-Path $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $zipPath -Force

$makeAppx = Get-Command "makeappx.exe" -ErrorAction SilentlyContinue
if ($makeAppx) {
    Write-Host "makeappx.exe detected. Add an MSIX manifest/project to enable signed MSIX packaging in a future slice."
} else {
    Write-Host "makeappx.exe not found. Zip package created; MSIX packaging requires Windows SDK tooling and signing material."
}

Write-Host "Release package ready: $zipPath"
