# Playlist Test Checklist

Use this checklist before trying a private playlist.

1. Run `dotnet run --project .\src\Iptv.App\Iptv.App.csproj`.
2. Click **Load Sample** and confirm channels appear.
3. Search for `news` and confirm the list filters without freezing.
4. Select a channel and click **Play**.
5. Confirm the selected channel shows a **NOW** badge while loading/playing.
6. Try shortcuts: `Space` play, `P` pause, `S` stop, `F11` fullscreen, and `Esc` exit fullscreen.
7. If playback does not start, confirm the app shows a clear timeout/failure message.
8. Click **Import File** and select a user-provided `.m3u` or `.m3u8`.
9. Confirm the import summary shows channel, warning, duplicate, and error counts.
10. Search/filter by group/category.
11. Mark a channel as favorite, close the app, reopen, and re-import the same playlist to confirm the favorite reappears.
12. Confirm normal UI does not show full tokenized stream URLs.

Optional public smoke command:

```powershell
dotnet run --project .\tools\Iptv.Smoke\Iptv.Smoke.csproj -- --url https://www.apsattv.com/xumo.m3u --probe-count 5 --timeout-seconds 20
```

Do not attach private playlists, stream URLs, or screenshots with provider tokens to bug reports.
