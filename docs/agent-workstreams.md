# Agent Workstreams

Use disjoint ownership to avoid merge conflicts.

1. **Core contracts** — `src/Iptv.Core`, `tests/Iptv.Core.Tests`.
2. **Playlist import** — `src/Iptv.Playlists`, `tests/Iptv.Playlists.Tests`, `assets/sample-playlists`.
3. **Search** — `src/Iptv.Search`, `tests/Iptv.Search.Tests`.
4. **EPG** — `src/Iptv.Epg`, `tests/Iptv.Epg.Tests`.
5. **Playback** — `src/Iptv.Playback`, `src/Iptv.App/Playback`, `tests/Iptv.Playback.Tests`.
6. **Persistence** — `src/Iptv.Persistence`, `tests/Iptv.Persistence.Tests`.
7. **Smoke tooling** — `tools/Iptv.Smoke`.
8. **UI shell** — `src/Iptv.App` views, view models, services, and resources.

Shared rules:

- Do not let non-UI projects reference `Iptv.App`.
- Add tests beside each feature slice.
- Do not introduce raw URL logging.
- Keep large-list and import work off the UI thread.
