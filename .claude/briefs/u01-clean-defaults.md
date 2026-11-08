You are implementing the game milestone **U01, clean defaults**, of the Vocaterra repository (C#, .NET 10, a MonoGame first-person voxel game that teaches Spanish). The language core is the `Vocaterra.Nlp` engine and the Spanish pack `src/Vocaterra.Nlp.Es`; the game is `Vocaterra.{Game.Core,Game,Host,SpeechRender}`. **This milestone's work is in the game.** It is the first of the presentation track (design section 14): ordinary play starts clean (the debug overlay and the dialogue's developer note off unless `--dev` or F3), one speak hint instead of three, a compact objective tracker, and room beside the hotbar for N05's need meters. It changes code and support-language text only.

Do the work yourself; do not spawn subagents.

## Where to work, and pushing
Work only in the git worktree `/home/user/Dev/Vocaterra/.claude/worktrees/u01`, already on the branch `U01-clean-defaults` (from `feature/adaptive_design` at its tip when you start). cd there first; never edit `/home/user/Dev/Vocaterra` or any other worktree; do not switch branches.
- **Commit and push often** (the owner's rule): commit after each item that builds and passes its tests, with the prefix `U01: `, and push after every commit (`git push -u origin U01-clean-defaults` the first time).
- Never push `main` or `feature/adaptive_design`, and do not merge. When you finish, leave the worktree on your branch, clean.

## The specification
The owner's Adaptation Design Document is the specification. Its text (extracted from `docs/game/design/sources/Vocaterra_Adaptation_Design_Document.docx`, one paragraph or table cell per line) is at `/tmp/claude-1000/orch/adaptation-design.txt`: read the sections this brief names with `grep -n` and `sed -n` (the headings are numbered, e.g. `grep -n '^9\.[0-9]* ' …`), never the whole file. Do not edit the `.docx`. Where the design and the code disagree on a file or line, the code wins; say so in the report. The owner adopted the design's section 18.2 recommendations provisionally on 2026-11-04 (`docs/game/decisions.md`, D-game-23, "Owner's answers, 2026-11-04"); where this brief fixes a value the design leaves open, use it and list it in the report under "values chosen".

## Output and context (the cost of a milestone is its turns times its context)
- Send every heavy command's output to a log file under `/tmp/claude-1000/u01/`, and print at most 30 lines of it.
- Keep `sed -n` ranges under about 150 lines; prefer `grep -n` first. Never read `docs/game/speech-checklist.md`, `manifest.json` or the whole of `content/PHRASES.md`.
- Wait for a long command in the foreground. **Never leave a wait loop or a background process behind** (a `while pgrep -f …` loop matches its own command line and never ends).
- **Disk:** about 13 GB free once you build. While iterating, build only the projects you need; build the whole solution once at the end.

## Resources (the desktop has crashed from overload)
- Run every `dotnet restore|build|test|format|run`, every `scripts/content.sh` call and every game run through `scripts/limited.sh` (`-m:4` builds, `-m:2` tests). `scripts/test.sh` and `scripts/content-loop.sh` run directly (content-loop refuses limited.sh, exit 3).
- Never start two heavy commands at once: no `&`, no background builds. Use `scripts/test.sh fast` or `scripts/test.sh project <test project>` while iterating; `scripts/test.sh full` once before the report.
- Exit code 137 means the 6 GB cap killed the command: lower the parallelism.
- **Another agent (N07, cooking at the fire) works beside you in its own worktree.** `scripts/limited.sh` runs at most two heavy commands at once machine-wide and caps them together at 8 GB; a command may wait for a slot, which is normal. A command killed with exit 137 or 143 may have met the shared cap: rerun it once, and report it if it happens twice. N07 edits the crafting panel, the item catalogue, `content/game-v1` (recipes, foods, nouns) and `content/locale/en`: keep your edits to the files your deliverables name, and do not reformat other entries.

## Environment
- Prefix every command inline with `PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1`.
- The sandbox refuses a command line containing the words `eval` or `hash`; use a wrapper script under `/tmp/claude-1000/u01/`.
- Model-gated tests and game runs read `VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models`. Game runs need `DISPLAY=:0`, each its own `--session-dir` under `/tmp/claude-1000/u01/`; scripted steps need `--screenshot`. The host: `dotnet src/Vocaterra.Host/bin/Debug/net10.0/Vocaterra.Host.dll`.
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
The LanguageTool check works from this sandbox (since 2026-11-05): after a content edit that adds Spanish, `scripts/content-loop.sh --grammar --speech` refreshes `content/grammar/` and reports new findings; never edit `content/grammar/` by hand. This milestone adds no Spanish; if it edits `content/game-v1` (a tip's trigger, for example), run the loop with `--grammar --speech`.

## Gate (yours, before reporting)
- `scripts/limited.sh dotnet build Vocaterra.sln -warnaserror -m:4`, 0 warnings.
- `scripts/test.sh full` (or `scripts/content-loop.sh --speech --tier full` if you edited content), all passing: 9451 passed and 3 skipped before you; report the new count.
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
- `CLAUDE.md`; `docs/process/code-guidelines.md`; `docs/game/architecture.md` (by `grep -n`, the HUD layers); `docs/game/decisions.md` D-game-9 (the support routes, which must stay reachable) by `grep -n`.
- `docs/game/implementation-plan.md` section 4.9 (your row) and the section 5 entries of N05 and L01 (`grep -n '^- \*\*N05\|^- \*\*L01'`): N05 put the need bars and icons beside the hotbar (`UI/Hud/NeedsHudLayout.cs`); L01 changed the Lessons page and made the beacon follow the tracked objective.
- The design (`/tmp/claude-1000/orch/adaptation-design.txt`): 14.2 and 14.3 (`grep -n '^14\.[23] '`, about 15 lines). Its file and line references are from before N05 and L01: find them by `grep -n`.
- Code: `Startup/LaunchOptions.cs`, `VoxelGame.cs` (the debug overlay's start state, today `!_playtest`), `DialogueModel.cs` (the developer note), `UI/Hud/TipLayer.cs`, `UI/Hud/HudLayers.cs` (`ObjectiveLayer`), `UI/Hud/HudLayout.cs`, `UI/Hud/NeedsHudLayout.cs`, `Session/VoxelSession.Sleep.cs` (the rest line), `game/tips.json` (`tip.speak`), the lesson strip and the dialogue footer (`grep -rn 'speak' src/Vocaterra.Game/UI | head`), `tests/Vocaterra.Game.Tests/UI/Common/UiScaleLayoutTests.cs`, `scripts/scripted-runs.sh`.

## The deliverables (14.3)
1. **`--dev` and F3.** A `--dev` launch flag sets the start state of the debug overlay and of the dialogue's developer note (the grey "intent, understood, 100 %, 12 ms" line under the player's line); without it both start off, also outside `--playtest`; F3 toggles both in any mode; `--playtest` stays clean. The "heard / expected" line after speech stays visible (14.2): say which line that is and keep it.
2. **Scripted runs.** Runs whose captures read the developer note or the overlay pass `--dev` in `scripts/scripted-runs.sh` (you may edit that file for this), or their captures are re-baselined; say which and why. Test aids and console output (stdout) are unchanged.
3. **The tip box** never overlaps the debug overlay when F3 is on.
4. **One speak hint:** `tip.speak` shows at the first conversation only; the lesson strip and the dialogue footer stop repeating it (keep each route reachable by key, D-game-9).
5. **The objective tracker** shows one line per objective, three at most, the most recently advanced first, the rest as "+n"; a lesson's `do` step shows in the lesson strip, not also in the tracker; the rest line keeps its place.
6. **Room for the needs:** the tracker and the tip box leave N05's bars and icons their room.
7. **Documents:** `docs/game/running.md` (`--dev`, F3), the HUD doc if it lists layers.

## The tests
- Layout: no overlap of the tracker, the tip box, the debug overlay (F3 on), the need bars and icons, and the hotbar at 1280x720 and 800x600 at 100, 125 and 150 % (extend `UiScaleLayoutTests` or the HUD layout tests).
- The start states: no flag → overlay and note off; `--dev` → on; F3 toggles; `--playtest` → off.
- The tracker: four objectives give three lines and "+1", the most recently advanced first; a lesson `do` step is not in the tracker.
- `tip.speak` at the first conversation only.
- The D-game-9 tests unchanged (do not edit them; if one fails, the change is wrong).

## Behaviour checks (before the report; look at each screenshot)
- A day-2 world with no flag: a screenshot with no debug text, the tracker, the need icons (`--needs on --set-need thirst 40`) and the hotbar, at 1280x720 and at 800x600 with text scale 150.
- The same with `--dev`: the overlay and, in a conversation, the developer note; the tip box clear of the overlay.
- A conversation without `--dev`: no developer note; the speak hint only on the first.
