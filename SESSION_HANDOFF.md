# IPTV Viewer Compact Handoff

Date: 2026-06-20
Repo: `C:\RIFT MODDING\iptv`
Branch: `main`
Validated code baseline before final handoff/CI update: `47c75d6576b023aad8a12c92fd18fc845fecaf52` (`Merge pull request #2 from 360madden/codex/fix-msix-workflow-args`)
Remote: `https://github.com/360madden/IPTV.git`

## Current State

The .NET 10 WPF IPTV viewer is functional for user-supplied M3U/M3U8 playlists and is now set up on GitHub. PR #1 merged the app/repo setup, refreshed contributor docs, added CI, release artifact hardening, playlist-file launcher support, duplicate/VOD sample playlists, and playlist fixture tests. PR #2 fixed the MSIX workflow and packaging script after hosted CI exposed PowerShell parameter-binding and Windows SDK tool-discovery issues. The final handoff update also removes path filters from the required Windows CI workflow so protected PRs always receive the `Build and Test` check.

The repository is content-neutral: do not commit private playlists, provider credentials, or generated user library data.

## Validated Gates

Local validation on 2026-06-20 passed:

- `powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\package-release.ps1 -DryRun -CreateMsix`
- workflow-style hashtable dry-run invocation with `DryRun = $true`
- local Windows SDK discovery check for `makeappx.exe`
- `dotnet format .\Iptv.slnx --verify-no-changes`
- `dotnet build .\Iptv.slnx --no-restore`
- `dotnet test .\Iptv.slnx --no-build` — 50/50 passed
- `git diff --check`

Remote validation passed:

- Windows MSIX on PR #2 branch: run `27880620058`
- Windows MSIX on `main`: run `27880692669`
- Windows CI on `main`: run `27880753885`

## GitHub Setup

- Repo metadata set for `360madden/IPTV` with IPTV/.NET/WPF/M3U topics.
- `main` branch protection is enabled.
- Required check: `Build and Test` with strict up-to-date status checks.
- Windows CI runs on every PR to `main` and every push to `main` so the required check is always produced.
- Pull requests are required; force pushes and deletions are disabled.
- Merged feature branches were deleted and local tracking was pruned.

## Known Cautions

- GUI duplicate-dialog smoke was improved but can still be timing-sensitive; CLI fixture coverage passed reliably.
- MSIX artifacts are unsigned unless `IPTV_MSIX_CERT_BASE64` and `IPTV_MSIX_CERT_PASSWORD` secrets are configured.
- GitHub Actions currently warns that some pinned actions target Node 20 while the runner forces Node 24; not failing yet, but update actions when upstream versions are available.

## Best Next Resume Step

Start with `git status --short --branch`, then use `launch-iptv.cmd "https://www.apsattv.com/xumo.m3u"` or a local `.m3u` file to test playlist import and playback. For development, branch from protected `main` and expect PR validation before merge.
