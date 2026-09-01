# Repository working rules

## Required context at task start

Before planning or changing files for any development task, read these current project documents completely:

1. `README.md`
2. `docs/README.md`
3. `docs/ARCHITECTURE.md`
4. `docs/DEVELOPMENT.md`
5. `docs/VERSIONING.md`

Use `docs/README.md` as the document map. Files under `docs/archive` are historical context, not current instructions. Read a release note under `docs/releases` only when the task concerns that release or needs its history.

Keep documentation aligned with the type of change:

- Update `README.md` for user-facing setup, usage, or product overview changes.
- Update `docs/ARCHITECTURE.md` for project boundaries, runtime flow, persistence, or Windows integration changes.
- Update `docs/DEVELOPMENT.md` for development commands, validation, repository operation, dependency, or automation policy changes.
- Update `docs/VERSIONING.md` for version or release policy changes.
- Add or update `docs/releases/vX.Y.Z.md` only for user-visible changes in that release.
- Do not turn archived session handoffs into current project guidance; move durable information into the appropriate current document.
- Do not add a new top-level document under `docs` without adding its purpose to `docs/README.md`.

## End-of-task cleanup

Every development task must finish with a repository hygiene pass.

1. Inspect `git status --short` and the generated contents under `artifacts`.
2. Remove temporary UI captures, render previews, test results, coverage output, and other task-only files by running `./scripts/Cleanup-DevelopmentArtifacts.ps1`.
3. Before a final handoff, use `-IncludeBuildCaches` when no subsequent build or test is needed.
4. Preserve intentional deliverables under `artifacts/packages`, `artifacts/win-x64`, and `artifacts/win-x64-slim` unless the user explicitly asks to remove them.
5. Preserve source-controlled visual assets and all user data under `%LOCALAPPDATA%\DesktopCalendar`.
6. Never use broad wildcard deletion outside this repository. Resolve and validate every cleanup target before removal.
