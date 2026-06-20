# Repository Guidelines

## Project Structure & Module Organization

This is a .NET 10 Windows IPTV viewer. Keep feature code modular and avoid large monolithic files.

- `src/Iptv.App/` contains the WPF UI, composition root, dialogs, playback integration, and view models.
- `src/Iptv.Core/` contains domain models, normalization, redaction, IDs, EPG models, and playback snapshots.
- `src/Iptv.Playlists/`, `src/Iptv.Epg/`, `src/Iptv.Search/`, `src/Iptv.Playback/`, and `src/Iptv.Persistence/` contain focused service libraries.
- `tests/*` mirrors the source projects with xUnit-style unit tests.
- `tools/` contains smoke, benchmark, GUI-smoke, and release packaging helpers.
- `assets/sample-playlists/` is for sanitized fixtures only; never add private provider playlists.

## Build, Test, and Development Commands

Run from the repository root:

```powershell
dotnet restore .\Iptv.slnx
dotnet build .\Iptv.slnx --no-restore
dotnet test .\Iptv.slnx --no-build
.\launch-iptv.cmd
.\launch-iptv.cmd https://www.apsattv.com/xumo.m3u
.\launch-iptv.cmd .\assets\sample-playlists\duplicate-channels.m3u
dotnet run --project .\tools\Iptv.Smoke\Iptv.Smoke.csproj -- --url https://www.apsattv.com/xumo.m3u --probe-count 3 --timeout-seconds 20
```

Use `tools\xumo-gui-smoke.ps1 -SkipBuild -CaptureScreenshots` for Windows UI regression checks when needed.

## Coding Style & Naming Conventions

Follow `.editorconfig`: UTF-8, CRLF, final newline, spaces, four-space C#/XAML indentation. Use nullable-aware C#, file-scoped namespaces, descriptive PascalCase types/members, and camelCase locals/fields. Keep parsing, persistence, playback, search, and UI responsibilities separated. Prefer defensive validation, cancellation support, bounded reads, redacted diagnostics, and explicit user confirmation before destructive organization actions.

## Testing Guidelines

Add or update tests with behavior changes. Prefer targeted unit tests for parser, search, persistence, EPG, and playback contracts before GUI smoke tests. For large-playlist work, run the search benchmark or smoke tool with realistic counts. Do not rely on live IPTV streams as the only validation because availability is transient.

## Commit & Pull Request Guidelines

Use short imperative commit messages, e.g. `Add launch wrapper`. PRs should summarize user impact, changed modules, validation commands, and screenshots for visible UI changes. Keep GitHub updates on review branches; do not force-push or overwrite `main` without explicit approval.

## Security & Configuration Tips

This app is content-neutral. Do not commit credentials, tokenized URLs, private M3U/XMLTV files, provider screenshots, or raw stream logs. Use sanitized fixtures and redacted output only.
