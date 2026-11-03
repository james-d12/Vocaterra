# Handover: the documentation structure

Created: 2026-11-03T10:19Z (UTC)
Workstream: the documentation layout (the docs cleanup)
Branch: `main` while working, committed to `feature/docs-structure` (the owner's choice); base `530ab52d`
Worktree: `/home/james/Dev/Vocaterra`
Previous handover: [2026-11-03-0952-docs-organisation](2026-11-03-0952-docs-organisation.md) (its proposed layout was rejected)

## Outcome

The owner found the first proposal's layout weak, because it sorted everything by half and gave the archive no home. Records now
sit by kind at the top of `docs/`, and living documents stay with their half. The layout and its rules are in
[docs/README.md](../../README.md) and [docs/process/documentation.md](../../process/documentation.md). The migration is done, verified and
committed to `feature/docs-structure`. It was not pushed.

## Working tree and running work

- Committed on `feature/docs-structure`, created from `main` at `530ab52d` with the work. Not pushed; no pull request.
- The commit also carries the owner's earlier uncommitted edits to `README.md` and `.claude/agents/orchestrator.md`, which this work
  builds on.
- `docs/Vocaterra_Adaptation_Design_Document.docx` (31 MB) became staged during the session, though this session did not stage it. It
  is left out of the commit and stays in the working tree, until the owner decides whether it belongs in git (Git LFS, or kept
  outside the repository) and where (`docs/game/design/sources/`).
- No agents, worktrees or background processes are left.

## Completed

- **Records by kind:**
  - [`handovers/`](../README.md) holds the open handovers, with `done/` for the rest, and an index and template;
  - [`research/`](../../research/README.md) holds all research, flat and dated, with each outcome recorded;
  - [`qa-reports/`](../../qa-reports/README.md) holds bug reports and a triage table of every bug's status;
  - [`archive/`](../../archive/README.md) keeps superseded documents at their original path under `archive/`.
- **Living documents:** `process/` (code guidelines, test performance, the documentation rules); `game/design/` (the prototype
  brief, the crafting and cooking principle) with `design/sources/` for the Word files. `game/voice-trial/`, `speech-checklist.md`
  and `nlp/package-readme.md` stay put because code reads their paths.
- **Session state moved out of living documents:** the dated handover sections of the orchestrator, the game plan's section 4 and
  the language-pack research's section 7 are now files in `handovers/done/`, with the text unchanged. Their standing rules stay in
  the orchestrator, as undated sections: the village series, content briefs, rules learnt, and CI.
- **Agents:**
  - the orchestrator writes `YYYY-MM-DD-HHMM-<slug>.md` handovers, moves the replaced one to `done/`, reads the open ones at session
    start, and keeps the QA triage table;
  - `qa.md` gives each bug an id unique across reports, `QA-<report>-<n>`;
  - `CLAUDE.md` points to the layout.
- **References repaired:** every relative Markdown link (345 checked, 0 broken), and literal paths in `CLAUDE.md`, the agents,
  scripts, `.gitignore`, code comments and the documents. `.claude/briefs/` is left as sent.

## Unfinished and blocked

Not started; proposals for the owner, not authorised work:
- Shorten the game README (1,210 lines) and the NLP README (559) into quick starts plus guides. Correct their stale statements: the
  "Terraria-like" opening, `.github/workflows-pending/`, and the old project names `Vocaterra.Simulation` and `Vocaterra.MonoGame`
  in the root README and in the orchestrator's brief template.
- Move the long findings logs out of the two implementation plans.
- The rest of R19 (the owner's plan of 2026-10-31): top-level `plans/` and `decisions/`, front matter, and a docs check in the gate
  and CI.
- The adaptation design document: see above.

## Verification

- A local walk of every relative Markdown link, using the same rules as `MarkdownLinkTests`: 345 checked, 0 broken.
- `scripts/limited.sh dotnet build Vocaterra.sln -warnaserror -m:4`: succeeded, 0 warnings, 0 errors. This covers the edited code
  comments and the `.csproj` comment.
- `scripts/test.sh project Vocaterra.Nlp.Tests --no-build --filter FullyQualifiedName~MarkdownLinkTests`: 1 passed.
- `scripts/test.sh project Vocaterra.Host.Tests --no-build --filter 'FullyQualifiedName~PhraseSheetTests|FullyQualifiedName~GrammarStringsExtractorTests'`:
  21 passed (`content/PHRASES.md` line 3 changed).
- `git diff --check`: clean.
- Not run: the full test suite and the merge gate. The changes are Markdown, comments, one `.gitignore` path and a prose line of
  `content/PHRASES.md`.

## Next steps

1. The owner reviews `feature/docs-structure`, pushes it and opens the pull request into `main`.
2. When it merges, move this handover to `done/`.
3. Optional: the owner chooses among the proposals above.
