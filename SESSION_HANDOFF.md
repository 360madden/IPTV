# IPTV Viewer Compact Handoff

Date: 2026-06-21
Repo: `C:\RIFT MODDING\iptv`
Remote: `https://github.com/360madden/IPTV.git`
Branch to resume from after merge: `main`
Working branch for this slice: `codex/next-practical-prefs-import-release`
Published baseline before this slice: `a6d320f` (`Merge pull request #7 from 360madden/codex/next-1-10-practical`)

## Current Product State

The repo contains a functional .NET 10 WPF IPTV viewer for user-supplied M3U/M3U8 playlists. Playback uses LibVLC, playlist import supports URL/local file/sample flows, and the app includes channel grouping, hidden/custom groups, favorites, duplicate handling, search, EPG, VOD resume infrastructure, fullscreen/clock overlay, diagnostics, and release packaging. Keep the repository content-neutral: never commit private playlists, provider credentials, generated user-library data, or proprietary stream URLs.

## This Slice

The practical 1-10 follow-up is implemented as a focused, reviewable slice:

- Persisted Basic Mode, first-run dismissal, logo-cache limit, and recent playlist sources in `UiPreferences` without clobbering clock settings.
- Added isolated app-data support (`IPTV_VIEWER_APPDATA_DIR`) and first-run GUI smoke coverage for sample, URL, file-dialog, and continue flows.
- Extended playlist import/parser contracts with item/byte progress reporting for large playlists.
- Added a redacted recent playlist picker for URL/file re-imports.
- Added source profile import/export JSON services and UI commands; imports merge/update existing source profiles and enforce bounded file size.
- Added logo cache statistics, trim-to-limit, and clear-cache UI actions.
- Added release ZIP validation automation and wired it into Windows/MSIX and GitHub Release workflows.
- Added trusted-PFX MSIX signing-secret setup support while retaining self-signed test-certificate fallback.
- Added a guarded GitHub Release dispatch wrapper with `-WhatIf`; no tag or public release was created in this slice.

## Validation Snapshot

Local validation passed on 2026-06-21:

- `dotnet build .\Iptv.slnx --no-restore` — succeeded, 0 warnings/errors.
- `dotnet test .\Iptv.slnx --no-build` — 60/60 passed.
- `dotnet format .\Iptv.slnx --verify-no-changes` — clean.
- `git diff --check` — clean.
- `dotnet run --project .\tools\Iptv.Smoke\Iptv.Smoke.csproj --no-build -- --file .\assets\sample-playlists\duplicate-channels.m3u --search "Fixture Duplicate" --probe-count 2 --timeout-seconds 20` — 2/2 probes reached `Playing`.
- `dotnet run --project .\tools\Iptv.Smoke\Iptv.Smoke.csproj --no-build -- --url https://www.apsattv.com/xumo.m3u --probe-count 3 --timeout-seconds 20` — imported 389 channels; 3/3 probes reached `Playing`.
- `powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\xumo-gui-smoke.ps1 -FirstRunAction LoadSample -SkipBuild -TimeoutSeconds 60` — passed. Earlier in this slice, `ImportUrl`, `OpenFile`, and `Continue` first-run smoke modes also passed.
- `powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\package-release.ps1` — produced `artifacts\release\IptvViewer-win-x64.zip`.
- `powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\validate-release-zip.ps1 -LaunchSeconds 5 -PlaylistFile .\assets\sample-playlists\synthetic-news-sports.m3u` — release ZIP validation passed.
- `powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\start-github-release.ps1 -TagName v0.1.0 -Prerelease -WhatIf` — dispatch wrapper dry-run passed.

## GitHub / Branch State

- Default branch: `main`.
- `main` branch protection requires PRs and strict `Build and Test`.
- Windows CI runs on PRs to `main`; Windows MSIX runs on pushes touching app/package paths.
- GitHub Release publishing remains manual. Use `tools/start-github-release.ps1` only when an actual tag/release should be created.

## Known Cautions

- `MainViewModel` is still large. Continue extracting one feature controller at a time; do not attempt a broad rewrite.
- A trusted public MSIX code-signing certificate was not supplied. This slice added support for trusted PFX setup, but did not replace the existing self-signed test cert.
- No GitHub Release was dispatched because no real release tag/version was requested.
- The generated portable ZIP under `artifacts/` is validation output and should remain uncommitted.

## Best Resume Flow

1. Run `git status --short --branch` and confirm a clean `main` after merge, or continue from `codex/next-practical-prefs-import-release` if the PR is still open.
2. Pull latest: `git pull --ff-only`.
3. Launch with a public test playlist: `launch-iptv.cmd "https://www.apsattv.com/xumo.m3u"`, or pass a local `.m3u` file.
4. For new work, create a `codex/...` branch, keep changes scoped, run targeted local validation, push, open a PR, wait for `Build and Test`, then merge through protected `main`.
5. Highest-impact next product area: extract import/refresh orchestration out of `MainViewModel`, then add persistent recent-source management polish and broader release-smoke coverage.
