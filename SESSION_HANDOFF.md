# IPTV Viewer Compact Handoff

Date: 2026-06-21
Repo: `C:\RIFT MODDING\iptv`
Remote: `https://github.com/360madden/IPTV.git`
Branch: `main`
Base before this slice: `af43fd5` (`Improve UI themes and dropdown contrast`)

## Current Product State

The app is a functional .NET 10 WPF IPTV viewer for user-supplied M3U/M3U8 playlists. It remains content-neutral: do not commit private playlists, tokenized stream URLs, credentials, provider screenshots, or raw IPTV logs. Playback uses LibVLC behind the app playback boundary. The UI now has modular themes, high-contrast dropdowns, TV-distance scale, and no-channel placeholder coverage.

## This Slice Completed

Completed the requested 1-10 follow-up set as practical:

1. Added appearance presets: **Desktop**, **Living room**, **High contrast**, and **Custom**.
2. Added a live **Appearance Preview** card plus **Reset Appearance** in UI settings.
3. Persisted selected appearance preset in `UiPreferences`.
4. Added per-source/per-playlist appearance presets under **Source Profiles** with save/apply support.
5. Included source appearance presets in organization preferences, organization backups, and source-profile import/export.
6. Added `F1`/`?` keyboard shortcut help overlay and header shortcut button.
7. Kept existing compact/dense/large-library modes and wired presets to density/large-library choices.
8. Added screenshot regression validator: `scripts/compare_ui_smoke_screenshots.py`.
9. Added tests for appearance preset mapping, MainWindow structure, UI preference persistence, organization persistence, and source-profile export/import.
10. Updated README, release checklist, and polished UI checklist with screenshot review, PR workflow guidance, shortcuts, presets, and source appearance checks.

## Validation Snapshot

Local validation passed on 2026-06-21:

```powershell
dotnet format .\Iptv.slnx --verify-no-changes
dotnet build .\Iptv.slnx --no-restore
dotnet test .\Iptv.slnx --no-build
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\smoke-ui-dropdowns.ps1 -NoBuild
python .\scripts\compare_ui_smoke_screenshots.py
git diff --check
```

Latest observed test totals after this slice: 83 tests passed, 0 failed.

## Files Most Relevant to Resume

- UI: `src/Iptv.App/MainWindow.xaml`, `src/Iptv.App/MainWindow.xaml.cs`
- View model: `src/Iptv.App/ViewModels/MainViewModel.cs`
- Presets: `src/Iptv.App/Services/AppearancePresetCatalog.cs`
- Persistence: `src/Iptv.Persistence/UiPreferences.cs`, `src/Iptv.Persistence/ChannelOrganizationPreferences.cs`, source profile and organization store/backup services
- Tests: `tests/Iptv.App.Tests/*Appearance*`, `tests/Iptv.App.Tests/MainWindowStructureTests.cs`, updated persistence tests
- Smoke guard: `scripts/compare_ui_smoke_screenshots.py`
- Docs: `README.md`, `docs/POLISHED-UI-CHECKLIST.md`, `docs/RELEASE-TEST-CHECKLIST.md`

## Known Cautions

- `MainViewModel` remains large. Future appearance/source-profile work should be peeled into smaller feature view models/controllers before adding more UI state.
- UI smoke screenshots are ignored under `artifacts/`; do not commit generated screenshots unless they are sanitized release assets.
- Branch protection prefers PR-based changes, but this workflow may directly push to `main` when explicitly requested by the user. Always watch `Windows CI` and `Windows MSIX` after push.

## Best Resume Flow

1. Run `git status --short --branch` and confirm whether this handoff's commit has already been pushed.
2. If pushed, check GitHub Actions for `Windows CI` and `Windows MSIX` on the pushed commit.
3. If continuing locally, run the validation snapshot above before further UI work.
4. Next high-impact work: split `MainViewModel` appearance/source-profile state into modular view models, then add automated UIA coverage for shortcut overlay behavior.
