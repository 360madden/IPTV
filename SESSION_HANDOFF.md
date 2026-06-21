# IPTV Viewer Compact Handoff

Date: 2026-06-21
Repo: `C:\RIFT MODDING\iptv`
Remote: `https://github.com/360madden/IPTV.git`
Branch to resume from after merge: `main`
Working branch for this slice: `codex/review-practical-fixes`
Published baseline before this slice: `9c7e0da33ca26d057274f549244ef43b2491fa51` (`Merge pull request #4 from 360madden/codex/refresh-compact-handoff`)

## Current Product State

The repo contains a functional .NET 10 WPF IPTV viewer for user-supplied M3U/M3U8 playlists. Playback uses LibVLC, playlist import supports URL and local file workflows, and the app includes channel grouping, hidden/custom group support, duplicate handling, search, EPG, VOD resume infrastructure, fullscreen/clock overlay, diagnostics, and release packaging. Keep the repository content-neutral: never commit private playlists, provider credentials, generated user-library data, or proprietary stream URLs.

## This Slice

Practical follow-up from the repo review was completed without attempting a risky one-shot `MainViewModel` rewrite:

- Added `docs/RELEASE-TEST-CHECKLIST.md` for clean build, smoke, manual app, packaging, and release-note gates.
- Updated `README.md` to point testers/contributors at checklist docs and prefer portable ZIP before signed MSIX.
- Updated `docs/architecture.md` to document `AppServices` as the WPF composition helper.
- Added `src/Iptv.App/Services/AppServices.cs` and moved concrete service wiring out of `MainWindow`.
- Simplified and hardened `SensitiveTextRedactor` query redaction: query names are preserved, every query value is redacted, and malformed escaped keys fall back safely.
- Added a regression test proving non-sensitive-looking query values are still redacted because IPTV providers often use custom token parameter names.

## Validation Snapshot

Local validation passed on 2026-06-21:

- `dotnet format .\Iptv.slnx --verify-no-changes`
- `dotnet build .\Iptv.slnx --no-restore`
- `dotnet test .\Iptv.slnx --no-build` — 51/51 passed
- `powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\package-release.ps1 -DryRun -CreateMsix`
- `dotnet run --project .\tools\Iptv.Smoke\Iptv.Smoke.csproj --no-build -- --file .\assets\sample-playlists\duplicate-channels.m3u --search "Fixture Duplicate" --probe-count 2 --timeout-seconds 20` — 2/2 probes reached `Playing`
- `git diff --check`

Note: an initial build attempt failed because a running local `Iptv.App` process locked output DLLs. The process was stopped and the build passed cleanly afterward.

## GitHub / Branch State

- Default branch: `main`.
- `main` branch protection is enabled: PRs required, strict `Build and Test` required, force pushes disabled, deletions disabled.
- Windows CI runs for every PR to `main` and every push to `main`.
- Windows MSIX runs on pushes touching app/package paths and can be manually dispatched.

## Known Cautions

- `MainViewModel` remains the largest maintainability risk; split it incrementally by feature, not through one broad rewrite.
- GUI duplicate-dialog smoke is improved but timing-sensitive; CLI fixture coverage is reliable.
- MSIX artifacts are unsigned until `IPTV_MSIX_CERT_BASE64` and `IPTV_MSIX_CERT_PASSWORD` secrets are configured.
- GitHub Actions currently emits a Node 20 deprecation warning for pinned actions while hosted runners force Node 24; workflows pass, but action versions should be updated when upstream releases are available.

## Best Resume Flow

1. Run `git status --short --branch` and confirm you are on clean `main` tracking `origin/main`.
2. Pull latest: `git pull --ff-only`.
3. Launch with a public test playlist: `launch-iptv.cmd "https://www.apsattv.com/xumo.m3u"`, or pass a local `.m3u` file.
4. For new work, create a `codex/...` branch, keep changes scoped, run targeted local validation, push, open a PR, wait for `Build and Test`, then merge through protected `main`.
5. Highest-impact next product area: incrementally split `MainViewModel`, starting with stream health or clock/overlay settings, while preserving current behavior.
