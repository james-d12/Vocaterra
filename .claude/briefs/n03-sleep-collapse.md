You are implementing the game milestone **N03, sleep and the collapse**, of the Vocaterra repository (C#, .NET 10, a MonoGame first-person voxel game that teaches Spanish). The language core is the `Vocaterra.Nlp` engine and the Spanish pack `src/Vocaterra.Nlp.Es`; the game is `Vocaterra.{Game.Core,Game,Host,SpeechRender}`. **This milestone's work is in the game.** It builds on N01 (the needs model, `src/Vocaterra.Game.Core/Needs/`) and N02 (eating, drinking, sprinting, rest at a lit fire): it makes a night and a collapse move the needs and the day.

Do the work yourself; do not spawn subagents.

## Where to work, and pushing
Work only in the git worktree `/home/user/Dev/Vocaterra/.claude/worktrees/n03`, already on the branch `N03-sleep-collapse` (from `feature/adaptive_design` at its tip when you start). cd there first; never edit `/home/user/Dev/Vocaterra` or any other worktree; do not switch branches.
- **Commit and push often** (the owner's rule): commit after each item that builds and passes its tests, with the prefix `N03: `, and push after every commit (`git push -u origin N03-sleep-collapse` the first time).
- Never push `main` or `feature/adaptive_design`, and do not merge. When you finish, leave the worktree on your branch, clean.

## The specification
The owner's Adaptation Design Document is the specification. Its text (extracted from `docs/game/design/sources/Vocaterra_Adaptation_Design_Document.docx`, one paragraph or table cell per line) is at `/tmp/claude-1000/orch/adaptation-design.txt`: read the sections this brief names with `grep -n` and `sed -n` (the headings are numbered, e.g. `grep -n '^9\.[0-9]* ' …`), never the whole file. Do not edit the `.docx`. Where the design and the code disagree on a file or line, the code wins; say so in the report. The owner adopted the design's section 18.2 recommendations provisionally on 2026-11-04 (`docs/game/decisions.md`, D-game-23, "Owner's answers, 2026-11-04"); where this brief fixes a value the design leaves open, use it and list it in the report under "values chosen".

## Output and context (the cost of a milestone is its turns times its context)
- Send every heavy command's output to a log file under `/tmp/claude-1000/n03/`, and print at most 30 lines of it.
- Keep `sed -n` ranges under about 150 lines; prefer `grep -n` first. Never read `docs/game/speech-checklist.md`, `manifest.json` or the whole of `content/PHRASES.md`.
- Wait for a long command in the foreground. **Never leave a wait loop or a background process behind** (a `while pgrep -f …` loop matches its own command line and never ends).
- **Disk:** about 15 GB free. While iterating, build only the projects you need; build the whole solution once at the end.

## Resources (the desktop has crashed from overload)
- Run every `dotnet restore|build|test|format|run`, every `scripts/content.sh` call and every game run through `scripts/limited.sh` (`-m:4` builds, `-m:2` tests). `scripts/test.sh` and `scripts/content-loop.sh` run directly (content-loop refuses limited.sh, exit 3).
- Never start two heavy commands at once: no `&`, no background builds. Use `scripts/test.sh fast` or `scripts/test.sh project <test project>` while iterating; `scripts/test.sh full` once before the report.
- Exit code 137 means the 6 GB cap killed the command: lower the parallelism.

## Environment
- Prefix every command inline with `PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1`.
- The sandbox refuses a command line containing the words `eval` or `hash`; use a wrapper script under `/tmp/claude-1000/n03/`.
- Model-gated tests and game runs read `VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models`. Game runs need `DISPLAY=:0`, each its own `--session-dir` under `/tmp/claude-1000/n03/`; scripted steps need `--screenshot`. The host: `dotnet src/Vocaterra.Host/bin/Debug/net10.0/Vocaterra.Host.dll`.
- After any edit of `content/game-v1`: `scripts/content-loop.sh --speech` (it refreshes the hash, the save fixtures, the LanguageTool cache and the WAVs; `--record` when the goldens move; `--tier full` once before the report). Content outside game-v1's hash: `content/locale/` (the English), `content/schema/`. How to edit content: `content/README.md`.

## Rules
- **What this milestone edits:** the game's code and tests (`src/Vocaterra.Game.Core`, `src/Vocaterra.Game`, `src/Vocaterra.Host`, `tests/Vocaterra.Game*.Tests`, `tests/Vocaterra.Host.Tests`), `content/game-v1`, `content/locale`, `content/schema`, `assets/` and `docs/game/` (not the plans or the decisions).
- It takes the core (`src/Vocaterra.Nlp*`, `tests/Vocaterra.Nlp.*`, `tests/golden`, `docs/nlp/`) as it is. A core gap is reported, never patched; a core test that fails after a content edit is wanted feedback: report it.
- A regression check is a C# test, never a script with text baselines.
- Engine code only in `src/Vocaterra.Game`; `Vocaterra.Game.Core` references no package (`ArchitectureRulesTests`). Logic in Core, drawing and devices in Game (`docs/process/code-guidelines.md`, `docs/game/architecture.md`).
- Do not edit either `implementation-plan.md`, `docs/game/activities-plan.md`, `docs/game/village-plan.md`, either `decisions.md`, `scripts/merge-gate.sh`, the design `.docx`, or anything under `.claude/` or `.github/workflows/`.
- Determinism: no `System.Random` and no wall clock in the game's logic; the host owns the stored time, the seed and the repeat counts. Assets CC0 only (D-game-5).
- Every save version from 1 loads; a save round-trips.

## The grammar cache (a known limit of this sandbox)
Here the dotnet process cannot reach the local LanguageTool server (curl can; dotnet reports "no LanguageTool server"), so `--grammar` cannot run. Do not try to bypass the sandbox and do not edit `content/grammar/` by hand. Run the content loop without `--grammar`; its refresh step lists any string of `content/game-v1` the cache lacks, and `GrammarTests.TheCommittedCacheReplaysTheBundleOfflineWithItsWaivers` fails on such a miss. So add no new Spanish text to `content/game-v1` in this milestone unless the design cannot do without it: player-facing English goes in `content/locale/en/`, and a Spanish line the design needs is reported (its text and file) and left for a later milestone, or, if you must add it, stop before the report and say so; the milestone then waits for the owner's grammar refresh.

## Gate (yours, before reporting)
- `scripts/limited.sh dotnet build Vocaterra.sln -warnaserror -m:4`, 0 warnings.
- `scripts/test.sh full` (or `scripts/content-loop.sh --speech --tier full` if you edited content), all passing: 9195 passed and 3 skipped before you; report the new count.
- `scripts/limited.sh dotnet format Vocaterra.sln --verify-no-changes`.
- `scripts/limited.sh scripts/content.sh validate`; the lint at A2 clean (`scripts/content.sh lint content/game-v1 --strict --level a2 --fail-on-level --host-pools 'guide.*'`).
- **The scripted runs** (`docs/game/scripted-runs.md`): `scripts/scripted-runs.sh` before your first edit and after, compared with `python3 -I scripts/compare-runs.py`, one at a time through `scripts/limited.sh`. Explain every difference; re-baseline only the runs this milestone is meant to move.
- **CI's case:** the full suite once without models (`VOCATERRA_MODELS_DIR` an empty folder); report any wall-clock test within four times of its budget.
- Do not commit `bin/`, `obj/` or `artifacts/`. When you finish, `git status` is clean and the branch is pushed.

## Commit
Each message ends with:
```
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDeFsoWE1itA1oaKnYf
```

## Report (one page, under 500 words)
- The branch and the final commit SHA, pushed.
- Each deliverable: what you built (file and type), its tests.
- **Values chosen** where the design left one open, and every place the design and the code disagreed.
- The behaviour checks' lines; the scripted runs' differences; the save versions tested.
- The test count before and after; CI's case; anything left undone, and why.

Before your final message, make sure no process you started is still running (`pgrep -fa 'dotnet|Vocaterra|content-loop|scripted-runs'`, kill only your own).


## Read first (the sections named, not whole files)
- The design: 9.5 (sleep, the night's effects, the collapse, sittings and days), the lines of 9.4 on the collapse and the floors, 9.13 (the night and collapse tests, "a collapse occurs after 60 active seconds at stage 4", "every night and collapse advances the day and the sitting"), and the N03 row of 9.12.
- `.claude/briefs/n01-report.md` and `.claude/briefs/n02-report.md` (what N01 and N02 built, and what they left to N03), then the code: `PlayerNeeds.Sleep`, `Collapse`, `Wake`; `VoxelSession.Needs.cs`; `SaveGame.Sleep` and the other night path (the closing card's Enter and E at the camp fire, `SleepOffer`); `tests/.../SleepTests` ("a sleep equals a load").
- `docs/game/saves.md`, `docs/game/running.md` (`--sleep`), `docs/game/scripted-runs.md`.

## The deliverables
1. **A night moves the needs.** Every night (E at the lit camp fire, Enter on the closing card, and the `--sleep` aid) applies `PlayerNeeds.Sleep`: sleep to 100, health +20 up to 100, hunger −15 and thirst −20 never below 10 (a drive already below 10 stays: N01's choice), stage penalties recomputed before stamina fills to its effective maximum. Apply it on every night path the same way; update `SleepTests` so that "a sleep equals a load" still holds for everything but the needs, and add a test that the needs after a night equal the rule's.
2. **The collapse.** After 60 s of active play at a drive's last stage (N01's timer), the screen fades and the player sleeps where they stand: sleep to 50, stamina full, no health gain, hunger −15 and thirst −20 (floors as above). It counts as a night: the day and the sitting advance, and the player wakes where they fell (not at the camp). A tip explains it the first time (a key with English text in `content/locale/en/`; its Spanish is N05's). Raise the collapse's world event (N01 registered the names; use the one for a collapse and the one for waking, or say which).
3. **Days and sittings.** Every night and every collapse advance both counters; a launch or a load starts a sitting without advancing the day (test both). D-game-6 spacing is preserved (the existing spacing tests pass unchanged).
4. **A day's resets.** One place in the session where a passing night (sleep or collapse) runs the day's resets, with nothing in it yet but a tested seam, so that N04 (the chest goes) and N06 (bushes grow back, gifts return) only add to it.
5. **What stays as it is.** When the fire offers sleep (the 75 rule and «No tengo sueño.») is L01's, not yours: keep today's offer rule and say in the report where L01 will change it. No day–night cycle. `--needs off` keeps every scripted run's night as it is today (re-baseline nothing for it).
6. **Scripted runs.** If a run moves (a night now changes the save's needs), explain each difference and re-baseline only those. A collapse is reachable by a scripted option (`--set-need hunger 0` and a wait, or the smallest new option, documented in `running.md`).

## The tests (under `tests/Vocaterra.Game.Core.Tests/Needs/` and the session's tests)
The night's effects at and around the floors (a drive at 30, at 12, at 8; health at 90 and at 100); stamina filled after the penalties are recomputed; the collapse after exactly 60 active seconds at the last stage and not at 59.9, and not while paused (a conversation, a panel); the collapse's values; waking where the player fell; the day and the sitting after a night, after a collapse, after a load; the day's-resets seam called once per night and once per collapse, never on a load; saves round-trip across a night and a collapse; determinism.

## Behaviour checks (before the report; show the lines)
With `--set-need sleep 40 --set-need hunger 30 --sleep` (or the night path the host has), the needs before and after and the day; with `--set-need hunger 0` and enough active wait, the collapse's event, the player's position before and after, and the day; a load of the result: the day unchanged.
