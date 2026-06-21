# IPTV Viewer Compact Handoff

Date: 2026-06-20
Repo: `C:\RIFT MODDING\iptv`
Remote: `https://github.com/360madden/IPTV.git`
Branch to resume from: `main`
Published baseline before this handoff refresh: `6539a9498b5303570bd5e59cdde7d1e2571398f1` (`Merge pull request #3 from 360madden/codex/final-handoff-ci-required-check`)

## Current Product State

The repo contains a functional .NET 10 WPF IPTV viewer for user-supplied M3U/M3U8 playlists. Playback uses LibVLC, playlist import supports URL and local file workflows, and the app includes channel grouping, hidden/custom group support, duplicate handling, search, EPG, VOD resume infrastructure, and a polished desktop UI. Keep the repository content-neutral: never commit private playlists, provider credentials, generated user-library data, or proprietary stream URLs.

## Recent Repository Work

- PR #1 merged the app repository setup, refreshed contributor docs, added Windows CI, release artifact hardening, playlist-file launcher support, duplicate/VOD sample playlists, and playlist fixture tests.
- PR #2 fixed Windows MSIX packaging by replacing positional PowerShell array splatting with named hashtable splatting and adding Windows SDK tool discovery for `makeappx.exe`/`signtool.exe`.
- PR #3 refreshed this handoff path and removed Windows CI path filters so the required `Build and Test` check is always created on protected PRs.

## Validation Snapshot

Local validation from the completed setup slice passed:

- `powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\package-release.ps1 -DryRun -CreateMsix`
- workflow-style hashtable dry-run invocation with `DryRun = $true`
- local Windows SDK discovery check for `makeappx.exe`
- `dotnet format .\Iptv.slnx --verify-no-changes`
- `dotnet build .\Iptv.slnx --no-restore`
- `dotnet test .\Iptv.slnx --no-build` — 50/50 passed
- `git diff --check`

Remote validation passed:

- Windows MSIX on `main`: run `27880692669`
- Windows CI on `main`: run `27880897797`
- Current branch protection requires strict `Build and Test`.

## GitHub / Branch State

- Default branch: `main`.
- `main` branch protection is enabled: PRs required, strict `Build and Test` required, force pushes disabled, deletions disabled.
- Windows CI runs for every PR to `main` and every push to `main`.
- Windows MSIX runs on pushes touching app/package paths and can be manually dispatched.
- At handoff creation time there were no open PRs and local `main` matched `origin/main`.

## Known Cautions

- GUI duplicate-dialog smoke is improved but timing-sensitive; CLI fixture coverage is reliable.
- MSIX artifacts are unsigned until `IPTV_MSIX_CERT_BASE64` and `IPTV_MSIX_CERT_PASSWORD` secrets are configured.
- GitHub Actions currently emits a Node 20 deprecation warning for pinned actions while hosted runners force Node 24; workflows pass, but action versions should be updated when upstream releases are available.

## Best Resume Flow

1. Run `git status --short --branch` and confirm you are on clean `main` tracking `origin/main`.
2. Pull latest: `git pull --ff-only`.
3. Launch with a public test playlist: `launch-iptv.cmd "https://www.apsattv.com/xumo.m3u"`, or pass a local `.m3u` file.
4. For new work, create a `codex/...` branch, keep changes scoped, run targeted local validation, push, open a PR, wait for `Build and Test`, then merge through protected `main`.
5. Highest-impact next product area: robust large-playlist/VOD workflows, including faster import progress/cancel UX, custom group/hidden channel persistence QA, and less flaky GUI automation.
