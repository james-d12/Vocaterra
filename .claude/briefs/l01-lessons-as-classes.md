You are implementing the game milestone **L01, lessons as optional classes**, of the Vocaterra repository (C#, .NET 10, a MonoGame first-person voxel game that teaches Spanish). The language core is the `Vocaterra.Nlp` engine and the Spanish pack `src/Vocaterra.Nlp.Es`; the game is `Vocaterra.{Game.Core,Game,Host,SpeechRender}`. **This milestone's work is in the game.** The owner decided it on 2026-11-04 (D-game-25: "Lessons are not the main goal; they are optional classes the player takes on"). N01 (the needs model, with the sleep drive) is merged before you.

Do the work yourself; do not spawn subagents.

## Where to work, and pushing
Work only in the git worktree `/home/user/Dev/Vocaterra/.claude/worktrees/l01`, already on the branch `L01-lessons-as-classes` (from `feature/adaptive_design` at its tip when you start). cd there first; never edit `/home/user/Dev/Vocaterra` or any other worktree; do not switch branches.
- **Commit and push often** (the owner's rule): commit after each item that builds and passes its tests, with the prefix `L01: `, and push after every commit (`git push -u origin L01-lessons-as-classes` the first time).
- Never push `main` or `feature/adaptive_design`, and do not merge. When you finish, leave the worktree on your branch, clean.

## The specification
The owner's Adaptation Design Document is the specification. Its text (extracted from `docs/game/design/sources/Vocaterra_Adaptation_Design_Document.docx`, one paragraph or table cell per line) is at `/tmp/claude-1000/orch/adaptation-design.txt`: read the sections this brief names with `grep -n` and `sed -n` (the headings are numbered, e.g. `grep -n '^9\.[0-9]* ' …`), never the whole file. Do not edit the `.docx`. Where the design and the code disagree on a file or line, the code wins; say so in the report. The owner adopted the design's section 18.2 recommendations provisionally on 2026-11-04 (`docs/game/decisions.md`, D-game-23, "Owner's answers, 2026-11-04"); where this brief fixes a value the design leaves open, use it and list it in the report under "values chosen".

## Output and context (the cost of a milestone is its turns times its context)
- Send every heavy command's output to a log file under `/tmp/claude-1000/l01/`, and print at most 30 lines of it.
- Keep `sed -n` ranges under about 150 lines; prefer `grep -n` first. Never read `docs/game/speech-checklist.md`, `manifest.json` or the whole of `content/PHRASES.md`.
- Wait for a long command in the foreground. **Never leave a wait loop or a background process behind** (a `while pgrep -f …` loop matches its own command line and never ends).
- **Disk:** about 15 GB free. While iterating, build only the projects you need; build the whole solution once at the end.

## Resources (the desktop has crashed from overload)
- Run every `dotnet restore|build|test|format|run`, every `scripts/content.sh` call and every game run through `scripts/limited.sh` (`-m:4` builds, `-m:2` tests). `scripts/test.sh` and `scripts/content-loop.sh` run directly (content-loop refuses limited.sh, exit 3).
- Never start two heavy commands at once: no `&`, no background builds. Use `scripts/test.sh fast` or `scripts/test.sh project <test project>` while iterating; `scripts/test.sh full` once before the report.
- Exit code 137 means the 6 GB cap killed the command: lower the parallelism.

## Environment
- Prefix every command inline with `PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1`.
- The sandbox refuses a command line containing the words `eval` or `hash`; use a wrapper script under `/tmp/claude-1000/l01/`.
- Model-gated tests and game runs read `VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models`. Game runs need `DISPLAY=:0`, each its own `--session-dir` under `/tmp/claude-1000/l01/`; scripted steps need `--screenshot`. The host: `dotnet src/Vocaterra.Host/bin/Debug/net10.0/Vocaterra.Host.dll`.
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
The LanguageTool check works from this sandbox (since 2026-11-05). This milestone adds Spanish (the offer lines, «No tengo sueño.»): after each content edit run `scripts/content-loop.sh --grammar --speech` (it refreshes `content/grammar/`, the hash, the save fixtures and the WAVs), read every new finding and fix the line or report why it stands; never edit `content/grammar/` by hand. The tier-full run before the report is `scripts/content-loop.sh --grammar --speech --tier full`.

## Gate (yours, before reporting)
- `scripts/limited.sh dotnet build Vocaterra.sln -warnaserror -m:4`, 0 warnings.
- `scripts/test.sh full` (or `scripts/content-loop.sh --speech --tier full` if you edited content), all passing: 9382 passed and 3 skipped before you; report the new count.
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
- `CLAUDE.md`; `docs/process/code-guidelines.md`; `docs/game/architecture.md` (by `grep -n`); `docs/game/lessons.md` (its rules on when a lesson opens and on sleep); `content/README.md`.
- `docs/game/decisions.md`: D-game-25 (`grep -n '^## D-game-25'`, all of it), D-game-15 and V12c's rule it amends (`grep -n 'V12c'`), D-game-8 and D-game-16 for what is never lost.
- `docs/game/implementation-plan.md` section 4.9 (your row) and the entries in section 5 of N01 to N04 and N06 (`grep -n '^- \*\*N0'`): N03 built the night and the collapse (`Session/VoxelSession.Night.cs`, `RunDayResets`) and left the fire's offer rule (`SleepOffer`, `CanSleep`) to you; N06 exempts a gift the active lesson's `do` step needs. The line numbers below are from before N01 to N06: find them by `grep -n`.
- The design (`/tmp/claude-1000/orch/adaptation-design.txt`): 5.3 and 5.4 in full (5.4's table names every gate, with file and line), 9.5's first paragraph (sleep), 8.1's "Lessons" (the Lessons page), 18.1's D-game-25 row.
- Code: `Session/VoxelSession.Sleep.cs` (`SessionWorkDone` about line 47, `CanSleep` about line 67), `Language/Lessons/LessonRunner.cs` (about lines 248, 264, 677, 745 to 770; `HoldsOrder`), `Session/GuideApproach.cs` about line 31, `Session/VoxelSession.cs` about line 265 (the beacon on waking), `Session/ObjectiveBook.cs` about line 66, `Session/GuideSession.cs` about line 147, `content/game-v1/game/lessons/*.json` and `game/objectives/*.json`, the Lessons page of the book (`grep -rn 'Lessons' src/Vocaterra.Game/UI/Book`).

## The deliverables (each gate of 5.4's table, each with its test)
1. **One required introduction.** Luna's first meeting on day 1 teaches units u1, u2, u3, u5 and u6 of `first-words`, then the camp task; it alone keeps `autoStart` and Luna's approach within 5 blocks (`GuideApproach`). Units u4 and u4v become **Luna's first class** (a lesson of its own or a second part of first-words; choose the smaller change that keeps every lesson's steps and lines and the save's lesson ids; say which). The camp objectives stay as they are.
2. **Every other lesson is a class.** No lesson starts by itself. The teacher offers it in one line with Sí or No when the player talks to them and a class is open (an offer line per teaching NPC, in its voice; the shape: «¿Quieres una clase? Sí o no.»-style, A1, written by you, checked by the lint and `cefr-check`, with normal and slow WAVs through the content loop), or the player asks: «¿Me enseñas?» (a core intent may not exist: try it through the CLI first; if no intent takes it, use the Lessons page's "Ask for a class" alone and report the core gap) or the Lessons page's "Ask for a class" button/key. «No», «No, gracias» and walking away cost nothing: the NPC's trade prompt stays in place. A class owns the conversation only once accepted. **Esc pauses** an engaged lesson and frees its NPC; a paused lesson holds back no sleep, trade, order or delivery, and resumes when the player asks.
3. **What opens a class:** having met its teacher, and its `after` lesson (the preterite before session 8's retelling). Never the day: a lesson's session only orders the Lessons page (`LessonRunner` about line 248).
4. **Sleep:** day 1 keeps its ending (the introduction, the camp, the fire; `SessionComplete`). From day 2 the fire offers sleep whenever the sleep drive (N01's `PlayerNeeds`) is below 75; otherwise it says «No tengo sueño.» (the player's line, text only, with "I'm not sleepy." beneath, D-game-13). No lesson, objective or order holds sleep back (`SessionWorkDone`, `CanSleep`). With `--needs off` (the sleep drive full) the scripted runs must still be able to sleep: the `--sleep` aid and the scripted `sleep` step sleep regardless of the drive (say how).
5. **Orders** show once their NPC is met (and any order or capability they need), not on days 4 to 7; no lesson holds a delivery (`HoldsOrder`). The oven order's wait for `n4-back` (V14's design) goes with it; say what replaced it.
6. **Cards:** every scaffold phrase has its card from rung 1, whether or not a class taught it (`CardReady`, `GuideSession` about line 147).
7. **The beacon** on waking follows the tracked objective, not the next lesson's NPC; the Lessons page lists the classes on offer, who teaches each, where they are and what each needs (its `after`), with "Ask for a class"; it never shows a next required lesson.
8. **Saves:** versions 1 to 6 with an engaged lesson load with it paused; determinism holds.
9. **Runs and documents:** `r20-sleep` and `r21-skip` re-baselined (explain each difference); `--skip-to-session n` counts the classes before n as done; a new run `r30-no-classes` plays days 1 to 3 taking no class (if `r30` is taken in `docs/game/scripted-runs.md`, use the next free number and say so). `docs/game/lessons.md` updated for the new rules; `docs/game/running.md` if a flag's meaning changes.

## Acceptance (5.4; each a test or a run)
A player who takes no class after the introduction can sleep (when tired), trade, take and deliver every order, and reach the bakery; a declined offer leaves the NPC's trade prompt in place; a class taken late runs as it does today (its steps and lines unchanged); an engaged lesson in an old save loads paused; Esc pauses and frees the NPC.

## Behaviour checks (before the report; show the lines)
- A new world: the introduction runs; on day 2 (after `--sleep`), talk to Domingo: the offer line; answer «no»: his trade prompt; answer «sí» another time: the lesson runs.
- `--set-need sleep 90` at the fire on day 2: «No tengo sueño.»; `--set-need sleep 60`: the sleep offer.
- `r30-no-classes` (or its number) end to end; the session summary.
- Look at one screenshot of the Lessons page.
