# IPTV Viewer Compact Handoff

Date: 2026-06-20
Repo: `C:\RIFT MODDING\iptv`
Branch: `master`
Base commit before this handoff slice: `212a3cc Add resume tracking and IPTV library refinements`

## Current State

The .NET 10 WPF IPTV viewer is functional for user-supplied M3U/M3U8 playlists. Recent validated work added VOD resume tracking, paged VOD poster grid, compressed XMLTV import, smart views, EPG search, duplicate-hide preview, custom-group drag/drop, fallback scoring, MSIX signing docs, and GUI screenshot smoke coverage.

This handoff slice adds a root CMD convenience launcher:

- `launch-iptv.cmd` starts `src\Iptv.App\Iptv.App.csproj` from the repo root.
- `launch-iptv.cmd https://www.apsattv.com/xumo.m3u` treats a single HTTP/HTTPS argument as `--playlist-url`.
- Other arguments are forwarded unchanged to the app.
- `README.md` documents the wrapper in the command list and launch note.

## Validation To Preserve

Latest full product validation from the preceding completed slice:

- `dotnet format Iptv.slnx --verify-no-changes`
- `dotnet build Iptv.slnx --no-restore`
- `dotnet test Iptv.slnx --no-build` — 48/48 passed
- `git diff --check`
- `Iptv.SearchBench --count 50000`
- release dry run with MSIX staging
- live Xumo smoke: 389 channels imported, 3/3 playback probes reached `Playing`
- GUI smoke with screenshots succeeded; duplicate mutation skipped because Xumo had no duplicates

Launcher slice validation on 2026-06-20 passed:

- `cmd /c launch-iptv.cmd --help`
- `dotnet build Iptv.slnx --no-restore`
- `dotnet test Iptv.slnx --no-build` — 48/48 passed
- `git diff --check`

## Known Blockers / Cautions

- No private playlists or credentials should be committed.
- `AGENTS.md` exists but is stale relative to the current app; do not overwrite unless explicitly requested.
- Check `git remote -v` before push; if no remote exists, push is blocked until an `origin` URL is configured.

## Best Next Resume Step

Start with `git status --short --branch`, then verify whether this handoff/launcher commit is present and whether a remote has been configured. If continuing feature work, prioritize real VOD resume validation and duplicate-fixture GUI automation.