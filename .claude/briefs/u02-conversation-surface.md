You are implementing the game milestone **U02, the conversation surface**, of the Vocaterra repository (C#, .NET 10, a MonoGame first-person voxel game that teaches Spanish). The language core is the `Vocaterra.Nlp` engine and the Spanish pack `src/Vocaterra.Nlp.Es`; the game is `Vocaterra.{Game.Core,Game,Host,SpeechRender}`. **This milestone's work is in the game.** It is the second of the presentation track (design section 7.2, after U01's clean defaults): the dialogue panel shows the current exchange clearly, collapses older lines, carries the lesson strip in its header, gives one coaching line, and keeps every D-game-9 support route reachable by key. It lands before the first pilot tester, so every tester sees the same panel. It changes code and support-language text only (`content/locale` is outside game-v1's hash).

Do the work yourself; do not spawn subagents.

## Where to work, and pushing
Work only in the git worktree `/home/user/Dev/Vocaterra/.claude/worktrees/u02`, already on the branch `U02-conversation-surface` (from `feature/adaptive_design` at its tip when you start). cd there first; never edit `/home/user/Dev/Vocaterra` or any other worktree; do not switch branches.
- **Commit often, push never** (the owner's rules, 2026-10-30 and 2026-11-05): commit after each item that builds and passes its tests, with the prefix `U02: `. Do not push the branch: it stays local and the gate merges it from there.
- Never push anything (not `main`, not `feature/adaptive_design`, not your branch), and do not merge. When you finish, leave the worktree on your branch, clean.

## The specification
The owner's Adaptation Design Document is the specification. Its text (extracted from `docs/game/design/sources/Vocaterra_Adaptation_Design_Document.docx`, one paragraph or table cell per line) is at `/tmp/claude-1000/orch/adaptation-design.txt`: read the sections this brief names with `grep -n` and `sed -n` (the headings are numbered, e.g. `grep -n '^9\.[0-9]* ' …`), never the whole file. Do not edit the `.docx`. Where the design and the code disagree on a file or line, the code wins; say so in the report. The owner adopted the design's section 18.2 recommendations provisionally on 2026-11-04 (`docs/game/decisions.md`, D-game-23, "Owner's answers, 2026-11-04"); where this brief fixes a value the design leaves open, use it and list it in the report under "values chosen".

## Output and context (the cost of a milestone is its turns times its context)
- Send every heavy command's output to a log file under `/tmp/claude-1000/u02/`, and print at most 30 lines of it.
- Keep `sed -n` ranges under about 150 lines; prefer `grep -n` first. Never read `docs/game/speech-checklist.md`, `manifest.json` or the whole of `content/PHRASES.md`.
- Wait for a long command in the foreground. **Never leave a wait loop or a background process behind** (a `while pgrep -f …` loop matches its own command line and never ends).
- **Disk:** about 13 GB free once you build. While iterating, build only the projects you need; build the whole solution once at the end.

## Resources (the desktop has crashed from overload)
- Run every `dotnet restore|build|test|format|run`, every `scripts/content.sh` call and every game run through `scripts/limited.sh` (`-m:4` builds, `-m:2` tests). `scripts/test.sh` and `scripts/content-loop.sh` run directly (content-loop refuses limited.sh, exit 3).
- Never start two heavy commands at once: no `&`, no background builds. Use `scripts/test.sh fast` or `scripts/test.sh project <test project>` while iterating; `scripts/test.sh full` once before the report.
- Exit code 137 means the 6 GB cap killed the command: lower the parallelism.
- **Another agent (N09, a settings panel with the needs-rate slider) works beside you in its own worktree.** `scripts/limited.sh` runs at most two heavy commands at once machine-wide and caps them together at 8 GB; a command may wait for a slot, which is normal. A command killed with exit 137 or 143 may have met the shared cap: rerun it once, and report it if it happens twice. N09 edits `VoxelGame.cs`, `LaunchOptions.cs`, `Input/InputKeys.cs` and `KeyBindings.cs`, `UI/Common/LayerStack.cs` and the controls overlay: keep your edits in those files small and local, and do not take its settings key (F8): keep your edits to the files your deliverables name, and do not reformat other entries.

## Environment
- Prefix every command inline with `PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1`.
- The sandbox refuses a command line containing the words `eval` or `hash`; use a wrapper script under `/tmp/claude-1000/u02/`.
- Model-gated tests and game runs read `VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models`. Game runs need `DISPLAY=:0`, each its own `--session-dir` under `/tmp/claude-1000/u02/`; scripted steps need `--screenshot`. The host: `dotnet src/Vocaterra.Host/bin/Debug/net10.0/Vocaterra.Host.dll`.
- After any edit of `content/game-v1`: `scripts/content-loop.sh --speech` (it refreshes the hash, the save fixtures, the LanguageTool cache and the WAVs; `--record` when the goldens move; `--tier full` once before the report). Content outside game-v1's hash: `content/locale/` (the English), `content/schema/`. How to edit content: `content/README.md`.

## Rules
- **What this milestone edits:** the game's code and tests (`src/Vocaterra.Game.Core`, `src/Vocaterra.Game`, `src/Vocaterra.Host`, `tests/Vocaterra.Game*.Tests`, `tests/Vocaterra.Host.Tests`), `content/game-v1`, `content/locale`, `content/schema`, `assets/` and `docs/game/` (not the plans or the decisions).
- It takes the core (`src/Vocaterra.Nlp*`, `tests/Vocaterra.Nlp.*`, `tests/golden`, `docs/nlp/`) as it is. A core gap is reported, never patched; a core test that fails after a content edit is wanted feedback: report it.
- A regression check is a C# test, never a script with text baselines.
- Engine code only in `src/Vocaterra.Game`; `Vocaterra.Game.Core` references no package (`ArchitectureRulesTests`). Logic in Core, drawing and devices in Game (`docs/process/code-guidelines.md`, `docs/game/architecture.md`).
- Do not edit either `implementation-plan.md`, `docs/game/activities-plan.md`, `docs/game/village-plan.md`, either `decisions.md`, `scripts/merge-gate.sh`, the design `.docx`, or anything under `.claude/` or `.github/workflows/`.
- Determinism: no `System.Random` and no wall clock in the game's logic; the host owns the stored time, the seed and the repeat counts. Assets CC0 only (D-game-5).
- Every save version from 1 loads; a save round-trips.

## The grammar cache
The LanguageTool check works from this sandbox (since 2026-11-05): after a content edit that adds Spanish, `scripts/content-loop.sh --grammar --speech` refreshes `content/grammar/` and reports new findings; never edit `content/grammar/` by hand. This milestone adds no Spanish (the panel's labels are English, the support language); if it edits `content/game-v1`, run the loop with `--grammar --speech`.

## Gate (yours, before reporting)
- `scripts/limited.sh dotnet build Vocaterra.sln -warnaserror -m:4`, 0 warnings.
- `scripts/test.sh full` (or `scripts/content-loop.sh --speech --tier full` if you edited content), all passing: 9476 passed and 3 skipped before you; report the new count.
- `scripts/limited.sh dotnet format Vocaterra.sln --verify-no-changes`.
- `scripts/limited.sh scripts/content.sh validate`; the lint at A2 clean (`scripts/content.sh lint content/game-v1 --strict --level a2 --fail-on-level --host-pools 'guide.*'`).
- **The scripted runs** (`docs/game/scripted-runs.md`): `scripts/scripted-runs.sh` before your first edit and after, compared with `python3 -I scripts/compare-runs.py`, one at a time through `scripts/limited.sh`. Explain every difference; re-baseline only the runs this milestone is meant to move.
- **CI's case:** the full suite once without models (`VOCATERRA_MODELS_DIR` an empty folder); report any wall-clock test within four times of its budget.
- Do not commit `bin/`, `obj/` or `artifacts/`. When you finish, `git status` is clean (the branch committed, not pushed).

## Commit
Each message ends with:
```
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDeFsoWE1itA1oaKnYf
```

## Report (one page, under 500 words)
- The branch and the final commit SHA (local).
- Each deliverable: what you built (file and type), its tests.
- **Values chosen** where the design left one open, and every place the design and the code disagreed.
- The behaviour checks' lines; the scripted runs' differences; the save versions tested.
- The test count before and after; CI's case; anything left undone, and why.

Before your final message, make sure no process you started is still running (`pgrep -fa 'dotnet|Vocaterra|content-loop|scripted-runs'`, kill only your own).


## Read first (the sections named, not whole files)
- `CLAUDE.md`; `docs/process/code-guidelines.md`; `docs/game/architecture.md` (by `grep -n`: the dialogue panel, the layers); `docs/game/decisions.md` D-game-9 (the support routes) and D-game-13 (the player's lines and the English beneath) by `grep -n`.
- `docs/game/implementation-plan.md` section 4.9 (your row) and the section 5 entries of U01 and L01 (`grep -n '^- \*\*U01\|^- \*\*L01'`): U01 hid the developer note and the overlay unless `--dev`/F3, shortened the footer after the speak tip, and built the tracker; L01 added class offers in the panel and a lesson strip on the Lessons page.
- The design (`/tmp/claude-1000/orch/adaptation-design.txt`): 7.2 in full (`grep -n '^7\.2 '`, a table of about 60 lines: each element, ordinary play, when and how, the code) and its acceptance paragraph. Its file and line references are from before U01 and L01: find them by `grep -n`.
- Code: `Game.Core` `Dialogue/DialogueModel.cs` (`MaxLines`, the heard/expected line), `Dialogue/TurnPresenter.cs`, `Dialogue/SpeechButtons.cs`, `Dialogue/DialogueScreen.cs` (the recording, thinking, cancel and timeout states); `Game` `UI/Dialogue/DialoguePanel.cs`, `DialogueLayout.cs`, `SupportLayer.cs`, `UI/Lesson/LessonStripLayout.cs`, `LessonLayer.cs`; the key bindings (`Input/`); the D-game-9 tests (`grep -rln 'D-game-9' tests | head`); `docs/game/scripted-runs.md`.

## The deliverables (7.2)
1. **The current exchange.** The NPC's current line with its English at the phrase's rung, always shown; the learner's last line and its outcome after a turn; the heard/expected line after every spoken turn until the next turn.
2. **One coaching line** after a turn, by case: uncertain ("ask again": the microphone, not your Spanish); not understood; off the prompt (typed lines only); a slip (one recast). One line, not a stack; say where each text comes from (the feedback text, `TurnPresenter`).
3. **Older lines collapsed:** only the current exchange shows by default; an expand key shows the older lines (the design names Tab; Tab is also a typing key today: choose a free key if Tab conflicts, and say which), and the book's "What I said" keeps the full history.
4. **The lesson strip in the panel header** while the panel is open («Lección · Primeras palabras · unidad 1 de 7 · Dilo», the parts from the lesson data and `ui.stepOf`), and one HUD line during a `do` step (as U01's tracker leaves it); no separate strip box over the panel.
5. **Every D-game-9 route reachable by key in each panel state**, each with a test: the mic check, the recording and thinking states, cancel, the timeout, heard/expected, repeat, slower, help, no sé, typing and the buttons. The speech buttons stay always in the panel, each on its key; the support card at its rung, as today.
6. **Scripted runs:** the runs whose captures show the panel are re-baselined, each difference explained (`docs/game/scripted-runs.md`); stdout lines unchanged.
7. **Documents:** `docs/game/running.md` (the expand key; the panel), the architecture doc's dialogue section.

## The tests
- Layout: no two layers overlap at 1280x720 and 800x600 at 100, 125 and 150 % (the canvases of `HudLayout`), in each panel state (idle, recording, thinking, after a turn, expanded, with a support card, with the lesson header).
- The route-by-key tests of deliverable 5, one per route per state where it applies.
- The coaching line: one per case, never two.
- The heard/expected line after every spoken turn, gone at the next.
- The collapsed history: the current exchange only; the expand key shows the rest; the book's history unchanged.
- The D-game-9 tests stay as they are (if one fails, the change is wrong).

## Behaviour checks (before the report; look at each screenshot)
- A conversation at the villager on day 2 (`--skip-to-session 2`, `--talk villager`, a few `--say` lines): the panel after the third turn, collapsed and expanded, at 1280x720 and at 800x600 with text scale 150.
- A lesson step (`--skip-to-session 2`, ask the villager for the class, «Sí»): the header strip.
- A spoken turn if the scripted runs can feed one (a WAV through the existing aid): the heard/expected line.
