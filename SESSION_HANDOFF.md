# IPTV Viewer Compact Handoff

Date: 2026-06-21
Repo: `C:\RIFT MODDING\iptv`
Remote: `https://github.com/360madden/IPTV.git`
Active branch: `codex/next-1-10-modular-health-overrides`
Baseline for this slice: `8a6b859` (`Add library management polish (#9)` on `main`)

## Current Product State

The repo contains a functional .NET 10 WPF IPTV viewer for user-supplied M3U/M3U8 playlists. Playback uses LibVLC. The app supports URL/local/sample import, search, grouping, favorites, hidden channels, custom groups, source profiles, recent playlist sources, EPG, VOD resume surfaces, fullscreen/clock overlay, diagnostics, release packaging, and redacted smoke tooling. Keep it content-neutral: do not commit private playlists, credentials, generated user-library data, or proprietary stream URLs.

## This Slice

The next 1-10 follow-up is implemented on top of merged PR #9:

- Merged PR #9 into `main`, then started `codex/next-1-10-modular-health-overrides`.
- Extracted recent playlist source logic into `RecentPlaylistSourceManager`.
- Extracted source default visibility rule logic into `SourceDefaultVisibilityManager`.
- Added explicit per-channel visibility override persistence via `ChannelUserState.HasExplicitVisibility`, allowing “show despite default hidden rule” without losing legacy hidden behavior.
- Added import memory/GC metrics to `LibraryHealthAnalyzer` and surfaced them in library health.
- Added library health output to redacted diagnostics exports.
- Improved source profile help text to explain backup/restore scope.
- Added `Iptv.App.Tests` with manager and library health unit coverage.
- Expanded persistence tests for explicit visible/hidden override roundtrips.
- Expanded GUI smoke with recent-source import/export and source-profile conflict-preview coverage using an isolated seeded source profile.
- Added CI upload of the large search benchmark timing artifact.

## Validation Snapshot

Local validation passed on 2026-06-21:

- `dotnet restore .\Iptv.slnx` — succeeded.
- `dotnet format .\Iptv.slnx --verify-no-changes` — clean.
- `dotnet build .\Iptv.slnx --no-restore` — succeeded, 0 warnings/errors.
- `dotnet test .\Iptv.slnx --no-build` — 70/70 passed.
- `dotnet build .\Iptv.slnx --configuration Release --no-restore` — succeeded, 0 warnings/errors.
- `dotnet test .\Iptv.slnx --configuration Release --no-build` — 70/70 passed.
- `dotnet run --project .\tools\Iptv.SearchBench\Iptv.SearchBench.csproj --configuration Release --no-build -- --count 75000` — completed; name search 500 results in 108 ms.
- `dotnet run --project .\tools\Iptv.Smoke\Iptv.Smoke.csproj --no-build -- --url https://www.apsattv.com/xumo.m3u --probe-count 5 --timeout-seconds 20` — imported 389 channels; 5/5 probes reached `Playing`.
- `powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\xumo-gui-smoke.ps1 -SkipBuild -TimeoutSeconds 90 -PlaybackTimeoutSeconds 35 -ExerciseLibraryManagementDialogs` — passed; live playback did not reach `Playing` before timeout but UI regression, clock, fullscreen, recent-source, and source-profile conflict-preview checks completed.
- `git diff --check` — clean.

## GitHub / Branch State

- Default branch: `main`.
- Branch protection requires PRs and strict `Build and Test` on `main`.
- Push this branch as `codex/next-1-10-modular-health-overrides`, open a PR, wait for CI, then merge through protected `main` if checks pass.

## Known Cautions

- `MainViewModel` is still large; this slice peels out two managers but further feature extraction remains high leverage.
- Source default hidden rules are defaults. Explicit per-channel hide/show overrides now win when `HasExplicitVisibility` is stored.
- GUI smoke uses real app windows and a public Xumo playlist URL; playback timing can be transient, so playback remains non-required unless `-RequirePlayback` is passed.
- The source-profile conflict smoke seeds an isolated profile by deterministic source ID for the playlist host; keep that path isolated from real user profiles.

## Best Resume Flow

1. Run `git status --short --branch` and inspect the diff.
2. Push `codex/next-1-10-modular-health-overrides` and open the PR.
3. Watch CI, especially the benchmark artifact upload step.
4. If CI is green, merge through the protected flow.
5. Next highest-impact work: continue splitting `MainViewModel` into feature controllers/view models, starting with source profiles or library health.
