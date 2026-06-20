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
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\package-release.ps1 -DryRun
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\package-release.ps1 -CreateMsix
```

Use **Load Sample** in the app to verify import/search/playback plumbing before importing a private playlist.

## Live Playlist Smoke Test

Use the smoke CLI for repeatable playlist URL checks without driving the GUI:

```powershell
dotnet run --project .\tools\Iptv.Smoke\Iptv.Smoke.csproj -- --url https://www.apsattv.com/xumo.m3u --probe-count 5 --timeout-seconds 20
```

The smoke tool imports the playlist, prints a safe summary, and probes a limited number of streams with dummy audio/video output. It reports channel name, group, host, and probe status without printing full stream URLs.

For a GUI regression pass against the public Xumo playlist:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\xumo-gui-smoke.ps1 -SkipBuild
```

## Current Features

- Local and remote M3U/M3U8 import.
- Playlist refresh with added/removed/unchanged diff summary and a removable/new-channel review list.
- XMLTV file import with channel/program counts, basic channel matching, selected-channel EPG guide preview, and a virtualized EPG timeline.
- Search, group/category/content/VOD-year/visibility filters, sort modes, favorites, hidden channels, and custom group assignment.
- Local channel organization persistence for favorites, hidden channels, custom groups, custom order, recently watched sorting, and VOD/series resume progress.
- Custom group manager with counts, add, rename, delete, undo, batch actions, select-all/clear-selection, duplicate-channel hiding, advanced smart group rules/presets, and drag/drop or up/down custom ordering.
- Import/export for channel organization backups and smart group presets without raw stream URLs.
- Automatic per-playlist/source organization matching, editable source profile names, provider playback retry/buffer profiles, manual refresh reminders, refresh reconciliation summaries, safe channel details, selected-channel logo caching, visible-logo prefetch, view density, and large-library mode for compact 10k-result browsing.
- PIN-gated group locks for hiding restricted groups until unlocked locally.
- VOD/series detail panel with playlist-provided poster/backdrop preview and quick resume markers.
- Stream health dashboard based on playback success/failure/buffering events.
- Release packaging helper for publish/zip output plus optional MSIX staging, packaging, and signing via Windows SDK tools.
- LibVLC playback with fullscreen toggle, volume, buffering presets, and startup timeout guidance.
- Optional clock overlay with position, size, background, opacity, 24-hour, and seconds settings.
- True app-managed fullscreen with video overlay clock, mini-HUD controls, auto-hide, double-click toggle, and monitor preference.
- Keyboard shortcuts: `Ctrl+F` search, `Ctrl+L` import URL, `Ctrl+O` import file, `Ctrl+R` refresh, `Ctrl+A` select visible, `Ctrl+D` clear selection, `Space` play, `P` pause, `S` stop, `V` favorite, `H` hide/unhide, `B` batch favorite, `Delete` batch hide, `U` batch unhide, `C` clock, `F`/`F11` fullscreen, `Esc` exit fullscreen.
- Redacted diagnostics panel for import/playback events.

## Privacy Policy for Development

Do not commit private playlists, tokenized stream URLs, credentials, screenshots showing private provider data, or logs containing raw IPTV URLs. Use sanitized fixtures only.

## Playback Note

The UI is wired through a playback boundary so the backend can evolve. The current app shell is prepared for LibVLC-backed playback while the lower-level contracts remain backend-neutral.
