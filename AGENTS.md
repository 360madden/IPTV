# Repository Guidelines

## Project Structure & Module Organization

This repository currently has no committed source tree, test suite, assets, or package manifests. When adding project files, keep the top level predictable:

- `src/` for application code and reusable modules.
- `tests/` or `__tests__/` for automated tests that mirror `src/` paths.
- `assets/` for static media, sample playlists, fixtures, or other non-code inputs.
- `docs/` for architecture notes, setup instructions, and operational runbooks.

Avoid committing generated output, caches, credentials, or machine-specific files unless they are required fixtures.

## Build, Test, and Development Commands

No project-specific build or test commands are configured yet. When tooling is added, document the canonical commands in `README.md` and keep them runnable from the repository root. Suggested examples:

- `npm install` — install dependencies if a `package.json` is introduced.
- `npm run dev` — start a local development server.
- `npm test` — run the full automated test suite.
- `npm run lint` — check formatting and static analysis rules.

If a different stack is chosen, replace these examples with the actual commands.

## Coding Style & Naming Conventions

Match the conventions of the first real implementation added to the repo, and keep style consistent across files. Prefer descriptive names, small modules, and clear boundaries between parsing, I/O, and presentation logic. Use lowercase, hyphenated names for general files and directories where practical, such as `playlist-parser.ts` or `sample-data/`.

Add a formatter or linter early, then make it the source of truth instead of relying on manual style review.

## Testing Guidelines

Place tests close to the behavior they verify and name them after the unit or feature under test, for example `playlist-parser.test.ts`. Cover normal cases, malformed input, and edge cases before adding integration-heavy tests. Each bug fix should include a regression test when feasible.

## Commit & Pull Request Guidelines

There is no Git history in this directory to infer existing commit conventions. Until a convention is established, use short imperative commit messages such as `Add playlist parser` or `Document setup workflow`.

Pull requests should include a concise summary, validation performed, linked issues when applicable, and screenshots or sample output for user-visible changes.

## Security & Configuration Tips

Never commit API keys, account credentials, private IPTV URLs, or personal configuration. Use ignored local environment files for secrets and provide safe examples such as `.env.example` when configuration is required.
