# Documentation rules

How the documents under `docs/` are organised, named and moved. Adopted 2026-11-03; the index is [docs/README.md](../README.md).

## Two kinds of document

- **Living documents** describe the project as it is now and are kept current. They sit with their half: `game/`, `nlp/` (the language
  core), and `process/` for how we work on both. A milestone that changes the system updates them.
- **Records** are dated, written once and kept as evidence of what was known at the time. They sit by kind at the top of `docs/`,
  whichever half they concern: `handovers/`, `research/`, `qa-reports/` and `archive/`. A record is not rewritten as the project moves
  on; only a status note at its top and link repairs after a move are allowed.

## Where a new document goes

| It is | It goes in | Named |
| --- | --- | --- |
| A session's state for the next session | `handovers/` (open); later `handovers/done/` | `YYYY-MM-DD-HHMM-<slug>.md`, UTC |
| A study, review or report answering one question | `research/`, plus a row in its README | `YYYY-MM-DD-<slug>.md`, or a folder of that name |
| A QA pass or a branch review | `qa-reports/`, plus its bugs in the triage table | `YYYY-MM-DD-<area>.md`, `YYYY-MM-DD-review-<branch>.md` |
| A superseded document | `archive/`, at its original path, plus a row in its README | its original name |
| A plan for a milestone series | `game/` or `nlp/`, beside the implementation plan | `<SERIES>_PLAN.md`, as the existing plans |
| Reference or a guide to the game or the core | `game/` or `nlp/`, linked from that half's README | lowercase kebab-case |
| The owner's design inputs | `game/design/`; the original Word and OpenDocument files in `game/design/sources/` | lowercase kebab-case |
| How we work on both halves | `process/` | lowercase kebab-case |
| A document about code or data | beside it (`content/`, `scripts/`, `src/…/README.md`) | `README.md` or as its neighbours |

Session state goes only in a handover: never as a dated section of an agent definition, a plan, a README or a research report. A
milestone's status lives in its implementation plan, an owner decision in a decision register, a bug's status in the QA triage table;
everything else links to them. Milestone briefs are sent inline and kept in `.claude/briefs/` as sent.

## Lifecycle

- **Handovers:** the top of `handovers/` holds only open ones, with next steps still to do and at most one per workstream. A handover
  moves to `done/` when a later one for the same workstream replaces it or its work is finished (merged, or dropped by the owner).
  The rules and the index: [handovers/README.md](../handovers/README.md).
- **Research:** stays in `research/` for good; its README records the outcome, and a report that replaces another says so there.
- **QA reports:** stay in `qa-reports/`; each bug's status changes in the triage table, never in the report.
- **Plans:** a plan whose series is done stays beside the implementation plan with its status, while its milestone ids are cited.
- **Archive:** a living document moves to `archive/` when it no longer describes the project or a plan anyone will act on. Records
  never move there.

## Names

- **Every Markdown file is lowercase kebab-case** (`implementation-plan.md`, `2026-11-03-1019-docs-structure.md`,
  `.claude/briefs/m70-qa-core-findings.md`), adopted 2026-11-03. Records add their date prefix; the archive keeps a document's path
  with its kebab-case name.
- **The exceptions are names that a tool or code reads:** `README.md` (a folder's index, which GitHub and other tools look for),
  `CLAUDE.md` (Claude Code reads that name), and the data files `content/PHRASES.md`, `content/game-v1/ATTRIBUTION.md`,
  `assets/ATTRIBUTION.md` and `assets/audio/SOURCES.md` (the game, its tests and the hashed bundle name them). Renaming one of these is
  a code change.
- A document is named in prose by its file name without `.md` (`village-plan V16`, `implementation-plan` section 5).
- Ids keep their meaning for good: milestone ids (M, G, P, S, X, R, C, V, W), decision ids (D, D-game), ADR numbers and bug ids.

## Paths that code reads

These paths are read by code or the build, so moving them is a code change too:

- `docs/game/speech-checklist.md`: written by `tools/Vocaterra.SpeechRender` (`Program.cs`, `DefaultChecklist`) and named by
  `scripts/prerender-speech.sh`. SpeechRender also looks for a voice trial in `docs/game/voice-trial/` (`DefaultTrial`); the V06 trial was
  deleted on 2026-11-03, so the checklist has no provisional-voices table, and a new trial belongs outside `docs/` (`--trial-folder`).
- `docs/nlp/package-readme.md`: the core's NuGet readme, included by `Directory.Build.props`.

## Moving a document

1. `git mv` it (a folder: the whole folder), so history follows it.
2. Repair every reference in the same change: the Markdown links (relative to the file that holds them), and the literal paths in
   other documents, `CLAUDE.md`, `.claude/agents/`, scripts, project files and code comments. Search for the old path and for the bare
   file name, including hidden folders: `grep -rn '<old name>' --exclude-dir={.git,bin,obj,worktrees} .`. Leave `.claude/briefs/` as
   sent, but repair a Markdown link there if it breaks.
3. Check:

   ```bash
   PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1 \
     scripts/test.sh project Vocaterra.Nlp.Tests --filter FullyQualifiedName~MarkdownLinkTests
   git diff --check
   ```

   `MarkdownLinkTests` checks that every relative link names a file or folder that exists. It does not check `#anchors` or paths
   inside code spans, so check any anchor or literal path you changed by hand. A change that touches code comments or project files
   also builds the solution.
