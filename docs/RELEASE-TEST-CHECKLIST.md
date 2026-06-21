# Release Test Checklist

Use this checklist before publishing a ZIP/MSIX build or handing a build to a tester. Keep all validation content-neutral: use public fixtures or user-owned playlists only.

## 1. Clean Build Gate

```powershell
git status --short --branch
dotnet restore .\Iptv.slnx
dotnet format .\Iptv.slnx --verify-no-changes
dotnet build .\Iptv.slnx --no-restore
dotnet test .\Iptv.slnx --no-build
git diff --check
```

Expected: clean working tree before packaging, build succeeds, and all tests pass.

## 2. Playlist Import Smoke

```powershell
dotnet run --project .\tools\Iptv.Smoke\Iptv.Smoke.csproj -- --url https://www.apsattv.com/xumo.m3u --probe-count 5 --timeout-seconds 20
dotnet run --project .\tools\Iptv.Smoke\Iptv.Smoke.csproj -- --file .\assets\sample-playlists\duplicate-channels.m3u --search "Fixture Duplicate" --probe-count 2 --timeout-seconds 20
```

Expected: import summaries are printed, full stream URLs are not printed, and probes either reach `Playing` or fail with a clear redacted reason.

## 3. Manual App Pass

1. Start with `.\launch-iptv.cmd`.
2. Click **Load Sample** and confirm channels appear.
3. Import `https://www.apsattv.com/xumo.m3u`.
4. Search, filter by group, favorite, hide/unhide, and assign a custom group.
5. Play a channel, stop it, enter fullscreen with `F11`, show the HUD, and exit with `Esc`.
6. Toggle the clock overlay and verify it remains visible in true fullscreen.
7. Open shortcut help with `F1` or `?`, then close it.
8. Complete `docs/POLISHED-UI-CHECKLIST.md`, including theme, UI scale, source appearance, dropdown, focus, and no-channel placeholder checks.
9. Close and reopen the app, then confirm favorites/hidden/custom group state persists.
10. Open diagnostics and confirm no raw tokenized URLs are visible.

## 4. Packaging Gate

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\package-release.ps1 -DryRun -CreateMsix
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\package-release.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\validate-release-zip.ps1 -LaunchSeconds 5 -PlaylistFile .\assets\sample-playlists\synthetic-news-sports.m3u
```

Expected: the dry run reports the intended package identity and Windows SDK tooling. The default packaging command creates a portable self-contained ZIP under `artifacts/release/`, and ZIP validation confirms required release assets plus a short app launch.

For signed MSIX release candidates, configure `IPTV_MSIX_CERT_BASE64` and `IPTV_MSIX_CERT_PASSWORD`, then run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\package-release.ps1 -CreateMsix -SignCertificatePath <path-to-pfx>
```

Use `tools\configure-msix-signing-secrets.ps1 -PfxPath <trusted-cert.pfx> -PfxPassword <password>` when replacing the temporary self-signed CI certificate with a trusted certificate. Confirm the cert chain is trusted on a clean Windows profile before distributing a signed MSIX; self-signed packages are tester-only. Use `tools\start-github-release.ps1 -TagName v0.1.0 -Prerelease -WhatIf` to preview release workflow dispatch before creating a tag/release.

## 5. Release Notes Check

Document:

- build commit SHA and artifact type (`zip`, `msix`, or both);
- validation commands and smoke playlist used;
- known limitations, including unsigned MSIX status when applicable;
- privacy statement: the app ships no channels, playlists, credentials, or streams.
