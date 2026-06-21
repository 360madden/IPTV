[CmdletBinding()]
param(
    [string]$Repository = "360madden/IPTV",
    [string]$Publisher = "CN=IPTV Viewer",
    [int]$YearsValid = 3,
    [switch]$Force
)

$ErrorActionPreference = "Stop"

function New-RandomPassword {
    $bytes = [byte[]]::new(32)
    $generator = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try {
        $generator.GetBytes($bytes)
    }
    finally {
        $generator.Dispose()
    }

    return [Convert]::ToBase64String($bytes)
}

function Set-GitHubSecretFromText {
    param(
        [string]$Name,
        [string]$Value,
        [string]$Repo
    )

    if ([string]::IsNullOrWhiteSpace($Value)) {
        throw "Secret '$Name' value is empty."
    }

    $processStartInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $processStartInfo.FileName = (Get-Command gh).Source
    $processStartInfo.Arguments = "secret set $Name --repo $Repo"
    $processStartInfo.RedirectStandardInput = $true
    $processStartInfo.RedirectStandardOutput = $true
    $processStartInfo.RedirectStandardError = $true
    $processStartInfo.UseShellExecute = $false

    $process = [System.Diagnostics.Process]::Start($processStartInfo)
    if ($null -eq $process) {
        throw "Failed to start GitHub CLI while setting secret '$Name'."
    }

    $process.StandardInput.Write($Value)
    $process.StandardInput.Close()
    $standardOutput = $process.StandardOutput.ReadToEnd()
    $standardError = $process.StandardError.ReadToEnd()
    $process.WaitForExit()

    if ($process.ExitCode -ne 0) {
        $detail = if ([string]::IsNullOrWhiteSpace($standardError)) { $standardOutput } else { $standardError }
        throw "Failed to set GitHub secret '$Name': $detail"
    }
}

if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    throw "GitHub CLI 'gh' is required to configure repository secrets."
}

gh auth status | Out-Null

$password = New-RandomPassword
$securePassword = ConvertTo-SecureString -String $password -AsPlainText -Force
$tempDirectory = Join-Path ([System.IO.Path]::GetTempPath()) "iptv-msix-signing-$([Guid]::NewGuid().ToString('N'))"
$pfxPath = Join-Path $tempDirectory "iptv-msix-signing.pfx"
$certificate = $null

try {
    New-Item -ItemType Directory -Force -Path $tempDirectory | Out-Null
    $certificate = New-SelfSignedCertificate `
        -Type CodeSigningCert `
        -Subject $Publisher `
        -CertStoreLocation "Cert:\CurrentUser\My" `
        -KeyAlgorithm RSA `
        -KeyLength 2048 `
        -HashAlgorithm SHA256 `
        -NotAfter (Get-Date).AddYears([Math]::Max(1, $YearsValid))

    Export-PfxCertificate -Cert $certificate -FilePath $pfxPath -Password $securePassword | Out-Null
    $certificateBytes = [System.IO.File]::ReadAllBytes($pfxPath)
    $certificateBase64 = [Convert]::ToBase64String($certificateBytes)

    if (-not $Force) {
        Write-Host "Configuring repository secrets for $Repository using a self-signed code-signing certificate subject '$Publisher'."
        Write-Host "Use -Force in automation to skip this informational prompt."
    }

    Set-GitHubSecretFromText -Name "IPTV_MSIX_CERT_BASE64" -Value $certificateBase64 -Repo $Repository
    Set-GitHubSecretFromText -Name "IPTV_MSIX_CERT_PASSWORD" -Value $password -Repo $Repository

    Write-Host "Configured IPTV_MSIX_CERT_BASE64 and IPTV_MSIX_CERT_PASSWORD for $Repository."
    Write-Host "The certificate is self-signed. Windows testers may still need to trust it, or replace it with a trusted code-signing certificate later."
}
finally {
    if ($null -ne $certificate) {
        Remove-Item -LiteralPath "Cert:\CurrentUser\My\$($certificate.Thumbprint)" -Force -ErrorAction SilentlyContinue
    }

    if (Test-Path -LiteralPath $tempDirectory) {
        Remove-Item -LiteralPath $tempDirectory -Recurse -Force -ErrorAction SilentlyContinue
    }

    if ($null -ne $password) {
        $password = $null
    }
}
