# IPTV Viewer

A Windows desktop IPTV viewer built with .NET 10 and C#. The app is content-neutral: it does not ship channels, streams, credentials, or playlist sources. Users import their own M3U/M3U8 playlists.

## Current Stack

- `Iptv.App` — WPF Windows shell and composition root.
- `Iptv.Core` — domain models, validation primitives, and privacy-safe URL handling.
- `Iptv.Playlists` — M3U/M3U8 import, parsing, validation, and normalization.
- `Iptv.Search` — fast channel search and filtering.
- `Iptv.Playback` — playback state/contracts.
- `Iptv.Persistence` — local data abstractions and lightweight storage helpers.

## Commands

```powershell
dotnet restore .\Iptv.slnx
dotnet build .\Iptv.slnx --no-restore
dotnet test .\Iptv.slnx --no-build
dotnet run --project .\src\Iptv.App\Iptv.App.csproj
dotnet run --project .\src\Iptv.App\Iptv.App.csproj -- --playlist-url https://www.apsattv.com/xumo.m3u
.\launch-iptv.cmd
.\launch-iptv.cmd https://www.apsattv.com/xumo.m3u
.\launch-iptv.cmd .\assets\sample-playlists\duplicate-channels.m3u
dotnet run --project .\tools\Iptv.SearchBench\Iptv.SearchBench.csproj -- --count 50000
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\package-release.ps1 -DryRun
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\package-release.ps1 -CreateMsix
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\smoke-ui-dropdowns.ps1 -NoBuild
python .\scripts\compare_ui_smoke_screenshots.py
```

Use **Load Sample** in the app to verify import/search/playback plumbing before importing a private playlist. On Windows, `launch-iptv.cmd` starts the WPF app from the repository root and forwards advanced arguments unchanged; a single `http://` or `https://` argument is treated as `--playlist-url`, and a single existing file path is treated as `--playlist-file`.

## Practical Checklists

- `docs/playlist-test-checklist.md` — manual feature checklist for import, playback, fullscreen, organization, EPG, VOD, and privacy checks.
- `docs/RELEASE-TEST-CHECKLIST.md` — build, smoke, packaging, and release-note gate before sharing ZIP/MSIX artifacts.
- `docs/windows-msix-signing.md` — optional MSIX signing setup for release candidates.

For normal testers, prefer the portable self-contained ZIP first; treat signed MSIX as a later distribution path once signing secrets and trust prompts are settled.

## Live Playlist Smoke Test

Use the smoke CLI for repeatable playlist URL checks without driving the GUI:

```powershell
dotnet run --project .\tools\Iptv.Smoke\Iptv.Smoke.csproj -- --url https://www.apsattv.com/xumo.m3u --probe-count 5 --timeout-seconds 20
```

The smoke tool imports the playlist, prints a safe summary, and probes a limited number of streams with dummy audio/video output. It reports channel name, group, host, and probe status without printing full stream URLs.

For a GUI regression pass against the public Xumo playlist:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\xumo-gui-smoke.ps1 -SkipBuild
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\xumo-gui-smoke.ps1 -SkipBuild -ExerciseMutatingOrganization
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\xumo-gui-smoke.ps1 -SkipBuild -CaptureScreenshots
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\xumo-gui-smoke.ps1 -SkipBuild -PlaylistFile .\assets\sample-playlists\duplicate-channels.m3u -ChannelSearch "Fixture Duplicate" -ChannelName "Fixture Duplicate News" -ExerciseMutatingOrganization
```

The GUI smoke uses an isolated local app-data profile by default. Add `-UseRealUserProfile` only when intentionally testing current user settings.

## Current Features

- Local and remote M3U/M3U8 import.
- Playlist refresh with added/removed/unchanged diff summary, removable/new-channel review, and explicit apply/discard approval before replacing the loaded library.
- XMLTV file or URL import with size/time limits, `.gz`/`.zip` guide support, optional auto-load after playlist import, channel/program counts, basic channel matching, selected-channel EPG guide preview, EPG title/description search, and virtualized timeline windows for now, +2 hours, tonight, and tomorrow.
- Search, group/category/content/VOD-year/visibility filters, sort modes, built-in saved smart views, favorites, hidden channels, and custom group assignment.
- Local channel organization persistence for favorites, hidden channels, custom groups, custom order, recently watched sorting, and VOD/series resume progress captured from playback position when available.
- VOD/series library mode with a paged poster grid, poster status, resume-first sorting, and quick selection into the player detail pane.
- Custom group manager with counts, add, rename, delete, import/export CSV, hidden/locked audit restore actions, undo, batch actions, select-all/clear-selection, duplicate-channel preview/hide dialog, advanced smart group rules/presets, and drag/drop or up/down custom ordering.
- Import/export for channel organization backups and smart group presets without raw stream URLs.
- Automatic per-playlist/source organization matching, editable source profile names, provider playback retry/buffer profiles, manual refresh reminders, refresh reconciliation summaries, safe channel details, selected-channel logo caching, visible-logo prefetch, view density, and large-library mode for compact 10k-result browsing.
- Scored fallback stream list for same-name alternate entries plus a 50k-channel search benchmark UI/tool for large playlist checks.
- PIN-gated group locks for hiding restricted groups until unlocked locally.
- VOD/series detail panel with playlist-provided poster/backdrop preview and quick resume markers.
- Stream health dashboard based on playback success/failure/buffering events.
- Release packaging helper for publish/zip output plus optional MSIX staging, packaging, and signing via Windows SDK tools.
- GitHub Actions workflow for Windows Release build/test/MSIX packaging, with optional PFX signing through repository secrets.
- MSIX signing setup is documented in `docs/windows-msix-signing.md`.
- GUI smoke can capture window/fullscreen screenshots into ignored `artifacts/gui-smoke/` for layout regression review.
- UI dropdown smoke captures readable theme/dropdown screenshots into ignored `artifacts/ui-smoke/dropdowns/`; `scripts/compare_ui_smoke_screenshots.py` validates PNG integrity and optional baseline dimensions.
- LibVLC playback with fullscreen toggle, volume, buffering presets, and startup timeout guidance.
- Selectable Dark, Light, and High contrast themes with Desktop, Living room, High contrast, and Custom appearance presets, TV-distance scale, reset button, source/profile-specific appearance presets, and a live appearance preview card.
- Compact/dense channel-list modes and large-library mode for providers with many thousands of channels or VOD entries.
- Optional clock overlay with position, size, background, opacity, 24-hour, and seconds settings.
- True app-managed fullscreen with video overlay clock, mini-HUD controls, auto-hide, double-click toggle, and monitor preference.
- Keyboard shortcuts: `F1`/`?` shortcut help, `Ctrl+F` search, `Ctrl+L` import URL, `Ctrl+O` import file, `Ctrl+R` refresh, `Ctrl+A` select visible, `Ctrl+D` clear selection, `Space` play, `P` pause, `S` stop, `V` favorite, `H` hide/unhide, `B` batch favorite, `Delete` batch hide, `U` batch unhide, `C` clock, `F`/`F11` fullscreen, `Esc` exit fullscreen/close help.
- Redacted diagnostics panel for import/playback events.

## Screenshot Review

Generate local review screenshots before UI-heavy releases:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\smoke-ui-dropdowns.ps1 -NoBuild
python .\scripts\compare_ui_smoke_screenshots.py
```

The screenshots are intentionally ignored by Git so private playlist/provider data is not committed accidentally.

## GitHub Workflow

Prefer PR-based changes for normal development so branch protection and the `Build and Test` check gate merges. Direct pushes to `main` should be intentional maintenance actions only, followed by watching `Windows CI` and `Windows MSIX` to completion.

## Privacy Policy for Development

Do not commit private playlists, tokenized stream URLs, credentials, screenshots showing private provider data, or logs containing raw IPTV URLs. Use sanitized fixtures only.

## Playback Note

The UI is wired through a playback boundary so the backend can evolve. The current app shell is prepared for LibVLC-backed playback while the lower-level contracts remain backend-neutral.
