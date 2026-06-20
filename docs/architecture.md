# Architecture

The app is intentionally split into small projects so agents and contributors can work independently without creating a monolith.

## Dependency Graph

```text
Iptv.Core          no project refs
Iptv.Epg           -> Core
Iptv.Playlists     -> Core
Iptv.Search        -> Core
Iptv.Playback      -> Core
Iptv.Persistence   -> Core
Iptv.App           -> Core, Epg, Playlists, Search, Playback, Persistence
Iptv.Smoke         -> Core, Epg, Playlists
```

`Iptv.App` is the composition root. Lower-level projects must not reference the UI.

## Core Flow

```text
User playlist file or URL
  -> PlaylistImportService
  -> M3uPlaylistParser
  -> ChannelNormalizer and validation
  -> ChannelSearchService
  -> WPF view model
  -> IPlaybackEngine
  -> LibVLC/WPF host
```

XMLTV files flow through `Iptv.Epg` and stay independent from playlist parsing. The app matches EPG channels by `tvg-id` or normalized channel name.

Channel organization state is local-only. `Iptv.Persistence` stores favorites, hidden flags, custom group assignments, custom sort indexes, and recently watched timestamps by stable channel ID, then `Iptv.App` reapplies that state after each playlist import. Sort-mode, empty custom-group preferences, and large-library mode are stored separately so clock/UI settings cannot overwrite organization choices. Organization backup files contain stable channel IDs and user organization metadata only, not raw stream URLs. Logo caching is selected-channel only, size-limited, and failure-isolated.

## Privacy Boundary

Raw stream URLs are represented with `SensitiveUri`. Its `ToString()` returns a redacted value. UI, logs, exceptions, and test snapshots should use redacted values unless the user explicitly performs an advanced diagnostic action.

## Performance Rules

- Parse playlists asynchronously and support cancellation.
- Do not validate every stream on import.
- Keep search over normalized fields, not raw URLs.
- Keep WPF channel lists virtualized.
- Preserve playlist import order and use range-reset collection updates for large channel lists.
- Apply batch channel updates with hash-set selection lookups and one persistence write per action.
- Keep organization operations source-profile-safe by matching saved state through stable channel IDs and reporting refresh reconciliation counts.
- Use large-library mode to cap visible results to 10,000 compact rows when browsing 50k+ channel/VOD catalogs.
- Keep playback backend details isolated from view models.
- Use `tools/Iptv.Smoke` for safe URL import/probe checks before GUI testing.
