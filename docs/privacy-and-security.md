# Privacy and Security

The application is content-neutral and does not bundle IPTV providers, channels, streams, credentials, or playlist URLs.

## Rules

- Treat all imported playlists as untrusted input.
- Do not log full stream URLs.
- Do not display raw tokenized URLs in normal UI.
- Do not commit private `.m3u`, `.m3u8`, XMLTV files, screenshots, logs, or diagnostics.
- Keep playlist import and playback errors user-friendly and redacted.

## Sensitive URL Handling

Stream URLs are represented with `SensitiveUri`. Its string form is redacted and should be used for diagnostics. Raw URI access is only for playback/network calls inside trusted service boundaries.

## Network Behavior

The app does not validate every stream on import. It parses playlist structure first, then attempts playback only when the user selects a channel.
