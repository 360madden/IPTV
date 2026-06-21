# IPTV Viewer Compact Handoff

Date: 2026-06-21
Repo: `C:\RIFT MODDING\iptv`
Remote: `https://github.com/360madden/IPTV.git`
Branch for this slice: `codex/next-1-10-library-management`
Baseline before this slice: `9d20f6d` (`Merge pull request #8 from 360madden/codex/next-practical-prefs-import-release`)

## Current Product State

The repo contains a functional .NET 10 WPF IPTV viewer for user-supplied M3U/M3U8 playlists. Playback uses LibVLC. The app now includes playlist URL/local/sample import, recent playlist management, search, grouping, favorites, hidden channels, custom groups, default source visibility rules, EPG, VOD resume infrastructure, fullscreen/clock overlay, diagnostics, release packaging, and redacted smoke tooling. Keep the repository content-neutral: do not commit private playlists, credentials, generated user-library data, or proprietary stream URLs.

## This Slice

The requested 1-10 library-management follow-up is implemented:

- Added `PlaylistImportCoordinator` to time and centralize import/refresh execution outside the main view model.
- Expanded recent playlist source management with rename, pin/unpin, remove, import list, and export list commands/UI.
- Added bounded JSON import/export service for recent playlist sources.
- Added source-profile import conflict preview before overwriting existing names, playback profiles, or default visibility rules.
- Added persistent source default hidden-group rules, with UI to hide/show source groups by default and apply rules to large loaded libraries without per-channel state churn.
- Extended source profile import/export and organization backup/preferences persistence for default hidden-group rules.
- Added a library health dashboard summarizing channel counts, visibility, source/group counts, VOD/series, logos, EPG programs, import duration, and import quality.
- Added CI large-playlist search benchmark and optional release ZIP launch smoke in MSIX workflow.
- Updated signing/release checklist docs for trusted PFX distribution path.
- Added persistence tests for recent playlist source exports and default hidden-group roundtrips.

## Validation Snapshot

Local validation passed on 2026-06-21:

- `dotnet format .\Iptv.slnx --verify-no-changes` — clean.
- `dotnet build .\Iptv.slnx --no-restore` — succeeded, 0 warnings/errors.
- `dotnet test .\Iptv.slnx --no-build` — 63/63 passed.
- `dotnet run --project .\tools\Iptv.SearchBench\Iptv.SearchBench.csproj --no-build -- --count 75000` — completed; name search 500 results in 108 ms.
- `dotnet run --project .\tools\Iptv.Smoke\Iptv.Smoke.csproj --no-build -- --url https://www.apsattv.com/xumo.m3u --probe-count 5 --timeout-seconds 20` — imported 389 channels; 5/5 probes reached `Playing`.
- `powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\validate-release-zip.ps1 -LaunchSeconds 2 -PlaylistFile .\assets\sample-playlists\synthetic-news-sports.m3u` — release ZIP validation passed using existing artifact.
- `git diff --check` — clean.

## GitHub / Branch State

- Default branch: `main`.
- Branch protection requires PRs and strict `Build and Test` on `main`.
- This slice should be pushed as `codex/next-1-10-library-management`, opened as a PR, then merged through protected `main` if CI passes.

## Known Cautions

- `MainViewModel` remains large despite adding modular helpers. Continue extracting one feature area at a time.
- Default source hidden-group rules are defaults, not explicit per-channel visible overrides; user-hidden/favorite/custom state still wins when present.
- The release ZIP validation used an existing local artifact; package generation was not rerun in this slice.
- The optional CI GUI launch smoke is `continue-on-error` because Windows runner desktop availability can vary.

## Best Resume Flow

1. Run `git status --short --branch` and confirm current branch/state.
2. If the PR is not yet merged, push/inspect `codex/next-1-10-library-management` and wait for CI.
3. Test manually with `launch-iptv.cmd "https://www.apsattv.com/xumo.m3u"` and verify recent-source management, source default visibility, and library health UI.
4. Highest-impact next work: split recent-source/source-profile/default-visibility controllers out of `MainViewModel`, then add UI automation coverage for the new dialogs.
