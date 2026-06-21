# Polished UI Pass Checklist

Use this checklist before packaging or publishing a user-facing build. It focuses on IPTV Viewer readability, theme safety, and TV-distance operation.

## Theme Coverage

- Verify **Dark**, **Light**, and **High contrast** from **UI Settings > Appearance > Theme**.
- Confirm theme choice persists after app restart.
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
```

## Keyboard & Focus

- Tab through major controls and confirm the focus ring is visible.
- Check buttons, checkboxes, dropdowns, text boxes, sliders, expanders, tab items, and menus/tooltips if present.
- Confirm shortcuts still work: `Ctrl+F`, `Ctrl+O`, `Ctrl+L`, `Ctrl+R`, `F11`, `Esc`.

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
```
