param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$OutputRoot = ".\artifacts\release",
    [switch]$NoSelfContained,
    [switch]$DryRun,
    [switch]$CreateMsix,
    [string]$PackageName = "IptvViewer",
    [string]$Publisher = "CN=IPTV Viewer",
    [string]$Version = "1.0.0.0",
    [string]$SignCertificatePath
)

$ErrorActionPreference = "Stop"

function Get-PackageArchitecture {
    param([string]$RuntimeIdentifier)
    if ($RuntimeIdentifier -match "arm64") { return "arm64" }
    if ($RuntimeIdentifier -match "x86") { return "x86" }
    return "x64"
}

function New-LogoPng {
    param(
        [string]$Path,
        [int]$Size,
        [string]$Text
    )

    Add-Type -AssemblyName System.Drawing
    $bitmap = [System.Drawing.Bitmap]::new($Size, $Size)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.Clear([System.Drawing.ColorTranslator]::FromHtml("#111827"))
        $fontSize = [Math]::Max(9, [int]($Size / 5))
        $font = [System.Drawing.Font]::new("Segoe UI", $fontSize, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
        $brush = [System.Drawing.SolidBrush]::new([System.Drawing.ColorTranslator]::FromHtml("#E5E7EB"))
        $format = [System.Drawing.StringFormat]::new()
        $format.Alignment = [System.Drawing.StringAlignment]::Center
        $format.LineAlignment = [System.Drawing.StringAlignment]::Center
        $graphics.DrawString($Text, $font, $brush, [System.Drawing.RectangleF]::new(0, 0, $Size, $Size), $format)
        $bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        if ($null -ne $format) { $format.Dispose() }
        if ($null -ne $brush) { $brush.Dispose() }
        if ($null -ne $font) { $font.Dispose() }
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}

function Resolve-WindowsSdkTool {
    param([string]$ToolName)

    $pathCommand = Get-Command $ToolName -ErrorAction SilentlyContinue
    if ($pathCommand) {
        return $pathCommand.Source
    }

    $candidateRoots = @(${env:ProgramFiles(x86)}, $env:ProgramFiles) |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
        ForEach-Object { Join-Path $_ "Windows Kits\10\bin" } |
        Where-Object { Test-Path $_ }

    foreach ($root in $candidateRoots) {
        $versionDirectories = Get-ChildItem -LiteralPath $root -Directory -ErrorAction SilentlyContinue |
            Sort-Object -Property Name -Descending

        foreach ($versionDirectory in $versionDirectories) {
            $architectureCandidates = @(
                (Join-Path $versionDirectory.FullName "x64\$ToolName"),
                (Join-Path $versionDirectory.FullName "x86\$ToolName")
            )

            foreach ($candidate in $architectureCandidates) {
                if (Test-Path -LiteralPath $candidate -PathType Leaf) {
                    return $candidate
                }
            }
        }
    }

    return $null
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot "src\Iptv.App\Iptv.App.csproj"
$manifestTemplate = Join-Path $repoRoot "packaging\msix\AppxManifest.xml"
$repoRootFull = [System.IO.Path]::GetFullPath($repoRoot)
$outputRootFull = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $OutputRoot))
$publishDir = Join-Path $outputRootFull "publish-$Runtime"
$zipPath = Join-Path $outputRootFull "IptvViewer-$Runtime.zip"
$msixStageDir = Join-Path $outputRootFull "msix-stage-$Runtime"
$msixPath = Join-Path $outputRootFull "IptvViewer-$Runtime.msix"

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
if ($CreateMsix) {
    Write-Host "MSIX: $msixPath"
}
Write-Host "dotnet $($publishArgs -join ' ')"

if ($DryRun) {
    if ($CreateMsix) {
        Write-Host "MSIX dry run: manifest template $manifestTemplate; package identity $PackageName $Version $Publisher; signing cert path provided: $([bool]$SignCertificatePath)."
    }
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
Write-Host "Zip package ready: $zipPath"

if (-not $CreateMsix) {
    Write-Host "MSIX skipped. Re-run with -CreateMsix to stage and pack an MSIX package."
    return
}

$makeAppx = Resolve-WindowsSdkTool -ToolName "makeappx.exe"
if (-not $makeAppx) {
    throw "makeappx.exe not found. Install Windows SDK tooling or omit -CreateMsix."
}

if (-not (Test-Path $manifestTemplate)) {
    throw "MSIX manifest template was not found at $manifestTemplate"
}

if (Test-Path $msixStageDir) {
    Remove-Item -LiteralPath $msixStageDir -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $msixStageDir | Out-Null
Copy-Item -Path (Join-Path $publishDir "*") -Destination $msixStageDir -Recurse -Force

$assetsDir = Join-Path $msixStageDir "Assets"
New-Item -ItemType Directory -Force -Path $assetsDir | Out-Null
New-LogoPng -Path (Join-Path $assetsDir "Logo150.png") -Size 150 -Text "IPTV"
New-LogoPng -Path (Join-Path $assetsDir "Logo44.png") -Size 44 -Text "IP"
New-LogoPng -Path (Join-Path $assetsDir "StoreLogo.png") -Size 50 -Text "IP"

$architecture = Get-PackageArchitecture -RuntimeIdentifier $Runtime
$manifest = Get-Content -Raw -LiteralPath $manifestTemplate
$manifest = $manifest.Replace("__PACKAGE_NAME__", $PackageName.Trim())
$manifest = $manifest.Replace("__PUBLISHER__", $Publisher.Trim())
$manifest = $manifest.Replace("__VERSION__", $Version.Trim())
$manifest = $manifest.Replace("__ARCH__", $architecture)
Set-Content -LiteralPath (Join-Path $msixStageDir "AppxManifest.xml") -Value $manifest -Encoding UTF8

if (Test-Path $msixPath) {
    Remove-Item -LiteralPath $msixPath -Force
}
& $makeAppx pack /d $msixStageDir /p $msixPath /o
if ($LASTEXITCODE -ne 0) {
    throw "makeappx pack failed with exit code $LASTEXITCODE"
}

if (-not [string]::IsNullOrWhiteSpace($SignCertificatePath)) {
    $signtool = Resolve-WindowsSdkTool -ToolName "signtool.exe"
    if (-not $signtool) {
        throw "signtool.exe not found. MSIX was created but not signed: $msixPath"
    }

    $signArgs = @("sign", "/fd", "SHA256", "/f", $SignCertificatePath)
    if (-not [string]::IsNullOrWhiteSpace($env:IPTV_MSIX_CERT_PASSWORD)) {
        $signArgs += @("/p", $env:IPTV_MSIX_CERT_PASSWORD)
    }

    $signArgs += $msixPath
    & $signtool @signArgs
    if ($LASTEXITCODE -ne 0) {
        throw "signtool sign failed with exit code $LASTEXITCODE"
    }
    Write-Host "Signed MSIX package ready: $msixPath"
}
else {
    Write-Host "Unsigned MSIX package ready: $msixPath"
    Write-Host "Provide -SignCertificatePath and optional IPTV_MSIX_CERT_PASSWORD to sign during packaging."
}
