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
- Playlist refresh with added/removed/unchanged diff summary.
- XMLTV file import with channel/program counts and basic channel matching.
- Search, group/category filters, favorites, and local favorite persistence.
- LibVLC playback with fullscreen toggle, volume, buffering presets, and startup timeout guidance.
- Optional clock overlay with position, size, opacity, 24-hour, and seconds settings.
- True app-managed fullscreen with video overlay clock, mini-HUD controls, auto-hide, double-click toggle, and monitor preference.
- Keyboard shortcuts: `Ctrl+F` search, `Ctrl+L` import URL, `Ctrl+O` import file, `Ctrl+R` refresh, `Space` play, `P` pause, `S` stop, `C` clock, `F`/`F11` fullscreen, `Esc` exit fullscreen.
- Redacted diagnostics panel for import/playback events.

## Privacy Policy for Development

Do not commit private playlists, tokenized stream URLs, credentials, screenshots showing private provider data, or logs containing raw IPTV URLs. Use sanitized fixtures only.

## Playback Note

The UI is wired through a playback boundary so the backend can evolve. The current app shell is prepared for LibVLC-backed playback while the lower-level contracts remain backend-neutral.
