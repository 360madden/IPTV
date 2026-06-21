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

function Set-GitHubSecretsFromEnvFile {
    param(
        [hashtable]$Secrets,
        [string]$Repo
    )

    if ($Secrets.Count -eq 0) {
        throw "At least one secret is required."
    }

    $secretPath = Join-Path ([System.IO.Path]::GetTempPath()) "iptv-msix-secrets-$([Guid]::NewGuid().ToString('N')).env"
    try {
        $lines = New-Object System.Collections.Generic.List[string]
        foreach ($name in $Secrets.Keys) {
            $value = [string]$Secrets[$name]
            if ([string]::IsNullOrWhiteSpace($value)) {
                throw "Secret '$name' value is empty."
            }

            $lines.Add("$name=$value")
        }

        [System.IO.File]::WriteAllLines($secretPath, $lines, [System.Text.UTF8Encoding]::new($false))
        gh secret set --repo $Repo -f $secretPath | Out-Null

        if ($LASTEXITCODE -ne 0) {
            throw "GitHub CLI failed while setting repository secrets."
        }
    }
    finally {
        if (Test-Path -LiteralPath $secretPath) {
            Remove-Item -LiteralPath $secretPath -Force -ErrorAction SilentlyContinue
        }
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
    [Convert]::FromBase64String($certificateBase64) | Out-Null

    if (-not $Force) {
        Write-Host "Configuring repository secrets for $Repository using a self-signed code-signing certificate subject '$Publisher'."
        Write-Host "Use -Force in automation to skip this informational prompt."
    }

    Set-GitHubSecretsFromEnvFile -Repo $Repository -Secrets @{
        IPTV_MSIX_CERT_BASE64 = $certificateBase64
        IPTV_MSIX_CERT_PASSWORD = $password
    }

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
