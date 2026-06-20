# Playlist Test Checklist

Use this checklist before trying a private playlist.

1. Run `dotnet run --project .\src\Iptv.App\Iptv.App.csproj`.
2. Click **Load Sample** and confirm channels appear.
3. Search for `news` and confirm the list filters without freezing.
4. Select a channel and click **Play**.
5. Confirm the selected channel shows a **NOW** badge while loading/playing.
6. Enable **Clock**, then confirm the time appears over the video.
7. Open **UI Settings** and try clock position, size, background, opacity, 24-hour, and seconds options.
8. Try shortcuts: `Space` play, `P` pause, `S` stop, `C` clock, `F11` fullscreen, double-click video, and `Esc` exit fullscreen.
9. Confirm fullscreen is video-only and the clock remains visible over the video.
10. Move the mouse in fullscreen and confirm the mini-HUD appears, then idles away when auto-hide is enabled.
11. If playback does not start, confirm the app shows a clear timeout/failure message.
12. Click **Import File** and select a user-provided `.m3u` or `.m3u8`.
13. Confirm the import summary shows channel, warning, duplicate, and error counts.
14. Search/filter by group/category.
15. Hide a channel, switch **Visibility** to **Hidden only**, and confirm the channel appears there.
16. Change **Sort** between playlist order, name, favorites first, recently watched, and custom order.
17. Add a custom group, assign the selected channel, and confirm the group appears in the group filter.
18. Rename and delete a test custom group from **Channel Organization**.
19. Use **Up**/**Down** on a selected channel and confirm **Custom order** preserves the move.
20. Mark a channel as favorite, close the app, reopen, and re-import the same playlist to confirm favorite/hidden/custom group/sort state reappears.
21. Confirm normal UI does not show full tokenized stream URLs.

Optional public smoke command:

```powershell
dotnet run --project .\tools\Iptv.Smoke\Iptv.Smoke.csproj -- --url https://www.apsattv.com/xumo.m3u --probe-count 5 --timeout-seconds 20
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\xumo-gui-smoke.ps1 -SkipBuild
```

Do not attach private playlists, stream URLs, or screenshots with provider tokens to bug reports.
