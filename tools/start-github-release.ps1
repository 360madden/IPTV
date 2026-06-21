[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^v\d+\.\d+\.\d+([-.][A-Za-z0-9.]+)?$')]
    [string]$TagName,

    [string]$PackageVersion = "1.0.0.0",

    [switch]$Prerelease,

    [string]$Repository = "360madden/IPTV"
)

$ErrorActionPreference = "Stop"

if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    throw "GitHub CLI 'gh' is required to dispatch the release workflow."
}

gh auth status | Out-Null

$workflow = "GitHub Release"
$prereleaseValue = if ($Prerelease) { "true" } else { "false" }
$arguments = @(
    "workflow", "run", $workflow,
    "--repo", $Repository,
    "--field", "tagName=$TagName",
    "--field", "packageVersion=$PackageVersion",
    "--field", "prerelease=$prereleaseValue"
)

if ($PSCmdlet.ShouldProcess("$Repository $workflow", "Dispatch release $TagName")) {
    gh @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to dispatch GitHub Release workflow for $TagName."
    }

    Write-Host "Dispatched GitHub Release workflow for $TagName in $Repository."
}
