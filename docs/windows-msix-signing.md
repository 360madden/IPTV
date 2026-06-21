# Windows MSIX Signing

The Windows MSIX workflow builds, tests, packages, and optionally signs `IptvViewer-win-x64.msix`.

## Required workflow

Run from GitHub Actions with **Windows MSIX** or push to `master`/`main`. The workflow:

1. Restores `Iptv.slnx`.
2. Builds Release.
3. Runs tests.
4. Packages ZIP and MSIX with `tools/package-release.ps1`.
5. Signs only when a certificate secret is configured.

## Optional signing secrets

Create a code-signing PFX outside the repository. Then add repository secrets:

- `IPTV_MSIX_CERT_BASE64`: base64 of the `.pfx` file.
- `IPTV_MSIX_CERT_PASSWORD`: PFX password, if required.

PowerShell example:

```powershell
[Convert]::ToBase64String([IO.File]::ReadAllBytes(".\private-signing-cert.pfx")) |
    Set-Clipboard
```

Do not commit certificates, passwords, private keys, or signed artifacts containing private test data.

To configure secrets from a trusted PFX without exposing plaintext in logs:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\configure-msix-signing-secrets.ps1 -PfxPath .\private-signing-cert.pfx -PfxPassword "<password>"
```

Omit `-PfxPath` only for temporary self-signed tester builds.

For trusted distribution, use a CA-issued or enterprise-trusted PFX stored outside the repo and restrict access to the GitHub environment that publishes releases. Before relying on a signed MSIX, install/test it on a clean Windows profile where the certificate chain is already trusted; self-signed certificates are suitable only for private tester builds.

## Local dry run

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\package-release.ps1 -DryRun -CreateMsix
```

Use a real signing cert only for release builds you intend to distribute.
