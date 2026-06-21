# Polished UI Pass Checklist

Use this checklist before packaging or publishing a user-facing build. It focuses on IPTV Viewer readability, theme safety, and TV-distance operation.

## Theme Coverage

- Verify **Desktop**, **Living room**, and **High contrast** presets from **UI Settings > Appearance > Preset**.
- Verify **Dark**, **Light**, and **High contrast** from **UI Settings > Appearance > Theme**.
- Confirm theme choice persists after app restart.
- Confirm **Reset Appearance** returns the app to the desktop preset.
- Confirm app background, panels, channel list, player details, dialogs, and dropdown popups all update.
- Confirm accent buttons keep readable text in every theme.

## Readability & Scale

- Test **Normal**, **Large**, and **TV distance** from **UI Settings > Appearance > UI scale**.
- Confirm header, library filters, channel rows, player controls, diagnostics, and dialogs remain usable at each scale.
- Check that long channel names and VOD titles trim/wrap cleanly without hiding controls.

## Dropdowns & Interactive States

- Open representative dropdowns: group, category, sort mode, saved smart view, buffering, theme, and UI scale.
- Confirm selected, hover, disabled, keyboard focus, and open states remain high contrast.
- Run the screenshot smoke script when validating locally:

```powershell
.\scripts\smoke-ui-dropdowns.ps1 -NoBuild
python .\scripts\compare_ui_smoke_screenshots.py
```

## Keyboard & Focus

- Tab through major controls and confirm the focus ring is visible.
- Check buttons, checkboxes, dropdowns, text boxes, sliders, expanders, tab items, and menus/tooltips if present.
- Confirm shortcuts still work: `F1`/`?` shortcut help, `Ctrl+F`, `Ctrl+O`, `Ctrl+L`, `Ctrl+R`, `F11`, `Esc`.

## Source Appearance Presets

- In **Source Profiles**, save a source appearance preset and confirm it persists after restart.
- Reopen the playlist and confirm the saved source preset reapplies when the source profile is selected.
- Export/import source profiles and confirm appearance presets are included without stream URLs.

## Playback Panel

- Start with no channel selected and confirm the video area shows the themed placeholder instead of a white LibVLC surface.
- Select and play a sample channel, then stop playback and confirm controls remain readable.
- Test fullscreen with clock overlay and fullscreen HUD visible.

## Regression Gates

```powershell
dotnet format .\Iptv.slnx --verify-no-changes
dotnet build .\Iptv.slnx --no-restore
dotnet test .\Iptv.slnx --no-build
.\scripts\smoke-ui-dropdowns.ps1 -NoBuild
python .\scripts\compare_ui_smoke_screenshots.py
```
