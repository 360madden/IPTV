# Playlist Test Checklist

Use this checklist before trying a private playlist.

1. Run `dotnet run --project .\src\Iptv.App\Iptv.App.csproj`.
2. Click **Load Sample** and confirm channels appear.
3. Search for `news` and confirm the list filters without freezing.
4. Select a channel and click **Play**.
5. Confirm the default UI shows primary IPTV actions first: import, search, group/category filters, channel list, player controls, and the larger selected-channel **TV guide**.
6. Expand **More filters**, **More library tools**, and **Picture/audio fixes and source profile** only when needed; confirm advanced/lab-style controls are not all visible at once.
7. Confirm the selected channel shows a **NOW** badge while loading/playing.
8. Enable **Clock** from playback fixes, then confirm the time appears over the video.
9. Open **UI Settings** and try clock position, size, background, opacity, 24-hour, and seconds options.
10. Try shortcuts: `Space` play, `P` pause, `S` stop, `C` clock, `F11` fullscreen, double-click video, and `Esc` exit fullscreen.
11. Confirm fullscreen is video-only and the clock remains visible over the video.
12. Move the mouse in fullscreen and confirm the mini-HUD appears, then idles away when auto-hide is enabled.
13. If playback does not start, confirm the app shows a clear timeout/failure message.
14. If audio plays but the picture stays black, open **Picture/audio fixes and source profile**, click **Retry**, then test **Disable hardware decoding** plus **Retry** and confirm **Playback Diagnostics** updates without showing raw stream URLs. If the workaround helps, click **Save Source** and confirm **Applied Playback Profile** switches to the saved source profile; advanced users can also save it under **Source Profiles** with **Disable hardware decoding for this source**.
15. Click **Import File** and select a user-provided `.m3u` or `.m3u8`.
16. Confirm the import summary shows channel, warning, duplicate, and error counts.
17. Search/filter by group/category/content/VOD year, toggle **Large library mode**, and change **View density**.
18. Hide a channel, switch **Visibility** to **Hidden only**, and confirm the channel appears there.
19. Change **Sort** between playlist order, name, favorites first, recently watched, and custom order.
20. Add a custom group, assign the selected channel, and confirm the group appears in the group filter.
21. Rename and delete a test custom group from **Channel Organization**.
22. Multi-select several channels and test **Select All Visible**, **Clear Selection**, **Batch Favorite**, **Batch Hide**, **Batch Unhide**, **Assign Group**, and **Clear Group**.
23. Create a smart group rule, preview it, apply it, save/use a preset, and confirm it preserves channels that already have custom groups.
24. Use **Undo** after a batch action or smart group apply and confirm the previous organization state returns.
25. Use **Up**/**Down**, drag/drop on selected channels within one group, and drag a channel onto a custom group, then confirm **Custom order** and custom group assignment persist.
26. Rename a source profile and confirm the profile summary remains stable after refresh.
27. Refresh a playlist and inspect **Refresh Conflict Review** for new/removed channels.
28. Export organization settings, import them back, and confirm favorite/hidden/custom group/sort/large-library/view-density/source-profile state is restored.
29. Mark a channel as favorite, close the app, reopen, and re-import the same playlist to confirm favorite/hidden/custom group/sort state reappears.
30. Select a channel with `tvg-logo`, confirm the details panel shows either a cached logo preview or safe skipped/unsupported message, then test **Prefetch Logos** on visible results.
31. Import XMLTV from `.xml`, `.gz`, or `.zip` when available, then verify the selected channel **TV guide** and EPG search show matching programs.
32. Open the VOD library poster grid, page through entries, play a VOD/series item, and confirm resume progress updates when playback position is available.
33. Attempt playback and confirm **Stream Health** updates with success/failure/buffering counts, fallback scores, and recommendations for repeated slow/timeout events.
34. Confirm normal UI does not show full tokenized stream URLs.

Optional public smoke command:

```powershell
dotnet run --project .\tools\Iptv.Smoke\Iptv.Smoke.csproj -- --url https://www.apsattv.com/xumo.m3u --probe-count 5 --timeout-seconds 20
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\xumo-gui-smoke.ps1 -SkipBuild
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\xumo-gui-smoke.ps1 -SkipBuild -CaptureScreenshots
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\package-release.ps1 -DryRun
```

Do not attach private playlists, stream URLs, or screenshots with provider tokens to bug reports.
