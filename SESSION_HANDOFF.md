# IPTV Viewer Compact Handoff

Date: 2026-06-21
Repo: `C:\RIFT MODDING\iptv`
Remote: `https://github.com/360madden/IPTV.git`
Branch to resume from after merge: `main`
Working branch for this slice: `codex/next-1-10-practical`
Published baseline before this slice: `a736368ac8fc7f060709c5d77b9f1339bef65ea5` (`Merge pull request #5 from 360madden/codex/review-practical-fixes`)

## Current Product State

The repo contains a functional .NET 10 WPF IPTV viewer for user-supplied M3U/M3U8 playlists. Playback uses LibVLC, playlist import supports URL and local file workflows, and the app includes channel grouping, hidden/custom group support, duplicate handling, search, EPG, VOD resume infrastructure, fullscreen/clock overlay, diagnostics, and release packaging. Keep the repository content-neutral: never commit private playlists, provider credentials, generated user-library data, or proprietary stream URLs.

## This Slice

The practical 1-10 follow-up was completed in small, reviewable pieces:

- Split stream-health state tracking from `MainViewModel` into `StreamHealthTracker`.
- Extracted fullscreen monitor detection into `FullscreenMonitorService`.
- Added a first-run setup dialog with sample, file import, URL import, and privacy reminder actions.
- Added Basic Mode to hide advanced organization, EPG, VOD, fallback, and diagnostics panels.
- Added visible import progress text plus a cancel command for long playlist imports.
- Added redacted diagnostics export from the diagnostics panel.
- Added a 50k-channel search regression/performance test.
- Improved duplicate-dialog GUI smoke timing with a configurable timeout.
- Added `tools/configure-msix-signing-secrets.ps1` and configured `IPTV_MSIX_CERT_BASE64` / `IPTV_MSIX_CERT_PASSWORD` in `360madden/IPTV` using a temporary self-signed certificate.
- Added `.github/workflows/github-release.yml` for manual ZIP/MSIX GitHub Release publishing.

## Validation Snapshot

Local validation passed on 2026-06-21:

- `dotnet format .\Iptv.slnx --verify-no-changes`
- `dotnet build .\Iptv.slnx --no-restore`
- `dotnet test .\Iptv.slnx --no-build` — 52/52 passed
- `powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\package-release.ps1 -DryRun -CreateMsix`
- `dotnet run --project .\tools\Iptv.Smoke\Iptv.Smoke.csproj --no-build -- --file .\assets\sample-playlists\duplicate-channels.m3u --search "Fixture Duplicate" --probe-count 2 --timeout-seconds 20` — 2/2 probes reached `Playing`
- `dotnet run --project .\tools\Iptv.Smoke\Iptv.Smoke.csproj --no-build -- --url https://www.apsattv.com/xumo.m3u --probe-count 3 --timeout-seconds 20` — imported 389 channels; 3/3 probes reached `Playing`
- `powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\configure-msix-signing-secrets.ps1 -Repository 360madden/IPTV -Force`
- `gh secret list --repo 360madden/IPTV` — confirmed both MSIX signing secrets exist
- `git diff --check`

## GitHub / Branch State

- Default branch: `main`.
- `main` branch protection is enabled: PRs required, strict `Build and Test` required, force pushes disabled, deletions disabled.
- Windows CI runs for every PR to `main` and every push to `main`.
- Windows MSIX runs on pushes touching app/package paths and can use the configured signing secrets.
- GitHub Release publishing is manual via the `GitHub Release` workflow; dispatch it with a tag such as `v0.1.0` when a release should be created.

## Known Cautions

- `MainViewModel` is still large. Continue splitting one feature/controller at a time rather than attempting a broad rewrite.
- The MSIX certificate is self-signed. Testers may need to trust it locally, or replace it with a trusted code-signing certificate later.
- Import progress is stage-based; true byte/item-level progress would require extending the playlist importer contract.
- Basic Mode hides advanced panels but does not yet persist as a user preference.

## Best Resume Flow

1. Run `git status --short --branch` and confirm you are on clean `main` tracking `origin/main`.
2. Pull latest: `git pull --ff-only`.
3. Launch with a public test playlist: `launch-iptv.cmd "https://www.apsattv.com/xumo.m3u"`, or pass a local `.m3u` file.
4. For new work, create a `codex/...` branch, keep changes scoped, run targeted local validation, push, open a PR, wait for `Build and Test`, then merge through protected `main`.
5. Highest-impact next product area: continue shrinking `MainViewModel`, then add persisted first-run/basic-mode preferences and stronger GUI smoke coverage for the new setup/import flows.