You are implementing the game milestone **N09, the drive rates as a setting**, of the Vocaterra repository (C#, .NET 10, a MonoGame first-person voxel game that teaches Spanish). The language core is the `Vocaterra.Nlp` engine and the Spanish pack `src/Vocaterra.Nlp.Es`; the game is `Vocaterra.{Game.Core,Game,Host,SpeechRender}`. **This milestone's work is in the game.** The owner decided it on 2026-11-05 (`docs/game/decisions.md`, D-game-24, "Owner's answers, 2026-11-05"): N05 found that day 1 barely touches the needs at N01's rates, so for the needs playtest the drive rates become a slider in the settings, and a fixed value is chosen after it. The game has no settings screen yet (the settings are launch flags kept in the save, `SaveSettings`): this milestone adds a small one.

Do the work yourself; do not spawn subagents.

## Where to work, and pushing
Work only in the git worktree `/home/user/Dev/Vocaterra/.claude/worktrees/n09`, already on the branch `N09-needs-rate` (from `feature/adaptive_design` at its tip when you start). cd there first; never edit `/home/user/Dev/Vocaterra` or any other worktree; do not switch branches.
- **Commit and push often** (the owner's rule): commit after each item that builds and passes its tests, with the prefix `N09: `, and push after every commit (`git push -u origin N09-needs-rate` the first time).
- Never push `main` or `feature/adaptive_design`, and do not merge. When you finish, leave the worktree on your branch, clean.

## The specification
The owner's Adaptation Design Document is the specification. Its text (extracted from `docs/game/design/sources/Vocaterra_Adaptation_Design_Document.docx`, one paragraph or table cell per line) is at `/tmp/claude-1000/orch/adaptation-design.txt`: read the sections this brief names with `grep -n` and `sed -n` (the headings are numbered, e.g. `grep -n '^9\.[0-9]* ' …`), never the whole file. Do not edit the `.docx`. Where the design and the code disagree on a file or line, the code wins; say so in the report. The owner adopted the design's section 18.2 recommendations provisionally on 2026-11-04 (`docs/game/decisions.md`, D-game-23, "Owner's answers, 2026-11-04"); where this brief fixes a value the design leaves open, use it and list it in the report under "values chosen".

## Output and context (the cost of a milestone is its turns times its context)
- Send every heavy command's output to a log file under `/tmp/claude-1000/n09/`, and print at most 30 lines of it.
- Keep `sed -n` ranges under about 150 lines; prefer `grep -n` first. Never read `docs/game/speech-checklist.md`, `manifest.json` or the whole of `content/PHRASES.md`.
- Wait for a long command in the foreground. **Never leave a wait loop or a background process behind** (a `while pgrep -f …` loop matches its own command line and never ends).
- **Disk:** about 13 GB free once you build. While iterating, build only the projects you need; build the whole solution once at the end.

## Resources (the desktop has crashed from overload)
- Run every `dotnet restore|build|test|format|run`, every `scripts/content.sh` call and every game run through `scripts/limited.sh` (`-m:4` builds, `-m:2` tests). `scripts/test.sh` and `scripts/content-loop.sh` run directly (content-loop refuses limited.sh, exit 3).
- Never start two heavy commands at once: no `&`, no background builds. Use `scripts/test.sh fast` or `scripts/test.sh project <test project>` while iterating; `scripts/test.sh full` once before the report.
- Exit code 137 means the 6 GB cap killed the command: lower the parallelism.
- **Another agent (U01, clean defaults) works beside you in its own worktree.** `scripts/limited.sh` runs at most two heavy commands at once machine-wide and caps them together at 8 GB; a command may wait for a slot, which is normal. A command killed with exit 137 or 143 may have met the shared cap: rerun it once, and report it if it happens twice. U01 edits `VoxelGame.cs` (the debug overlay's start state), `LaunchOptions.cs` (`--dev`), the HUD layers, the tip and `scripts/scripted-runs.sh`: keep your edits in those files small and local: keep your edits to the files your deliverables name, and do not reformat other entries.

## Environment
- Prefix every command inline with `PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1`.
- The sandbox refuses a command line containing the words `eval` or `hash`; use a wrapper script under `/tmp/claude-1000/n09/`.
- Model-gated tests and game runs read `VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models`. Game runs need `DISPLAY=:0`, each its own `--session-dir` under `/tmp/claude-1000/n09/`; scripted steps need `--screenshot`. The host: `dotnet src/Vocaterra.Host/bin/Debug/net10.0/Vocaterra.Host.dll`.
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
The LanguageTool check works from this sandbox (since 2026-11-05): after a content edit that adds Spanish, `scripts/content-loop.sh --grammar --speech` refreshes `content/grammar/` and reports new findings; never edit `content/grammar/` by hand. This milestone adds no Spanish (the settings panel is English, the support language); if it edits `content/game-v1`, run the loop with `--grammar --speech`.

## Gate (yours, before reporting)
- `scripts/limited.sh dotnet build Vocaterra.sln -warnaserror -m:4`, 0 warnings.
- `scripts/test.sh full` (or `scripts/content-loop.sh --speech --tier full` if you edited content), all passing: 9461 passed and 3 skipped before you; report the new count.
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
- `CLAUDE.md`; `docs/process/code-guidelines.md`; `docs/game/architecture.md` (by `grep -n`: the layers, the input); `docs/game/decisions.md` D-game-24 with its "Owner's answers, 2026-11-05" (`grep -n '2026-11-05' docs/game/decisions.md`).
- `docs/game/implementation-plan.md` section 4.9 (your row) and the section 5 entries of N01 and N05 (`grep -n '^- \*\*N01\|^- \*\*N05'`).
- `docs/game/running.md`: the needs paragraphs (`grep -n -i 'needs\|F7\|relaxed'`).
- Code: `Needs/PlayerNeeds.cs` (`RateFactor`, how relaxed needs halves the rates), `Session/VoxelSession.Needs.cs` (`RelaxedNeeds`), `Save/SaveDocument.cs` (`SaveSettings`: `ViewEffectsOff` and `RelaxedNeeds`, written only when not the default), `src/Vocaterra.Game/Startup/LaunchOptions.cs`, `Program.cs` and `VoxelGame.cs` (how a saved setting and a flag reach the session; F7's toggle), `Input/InputKeys.cs` and `KeyBindings.cs`, `UI/Overlays/ControlsLayer.cs` (the controls list), `UI/Common/LayerStack.cs`, an existing small panel to copy (the crafting or the controls overlay).

## The deliverables
1. **The rate.** One setting, `NeedsRate`: a multiplier on every drive's rate (hunger, thirst, sleep), default 1.0, from 0.5 to 4.0 in steps of 0.25 (say if you choose other steps, and why). It multiplies with relaxed needs (relaxed at 2.0 is 1.0). It does not change stamina, health's recovery, food values or the collapse delay. Kept in `SaveSettings`, written only when not 1.0, so a default save is byte for byte what it was; clamped and snapped on load. Like the other settings it never reaches a digest or the replay: say how a save's meters stay exact.
2. **The flag** `--needs-rate <x>` sets it for a run (scripted runs and tests); a value out of range is refused with a message.
3. **The settings panel.** A small panel opened by one key (choose a free one, F8 if free; say which) and listed in the controls overlay: the drive rate as a slider (left/right or -/+ by keyboard, the value shown as "×1.25"), and the two needs settings that exist as toggles (view effects, relaxed needs), each applied at once. English only (support text). It pauses the needs while open, as the other panels do (N01). Keyboard-only use works; at 1280x720 and 800x600 at 100, 125 and 150 % it fits and overlaps nothing (the layout tests).
4. **Telemetry:** the session summary and the session log name the rate when it is not 1.0 (so a playtest's numbers say what rate they ran at).
5. **Documents:** `docs/game/running.md` (the panel, its key, `--needs-rate`); `docs/game/playtest.md`, if it has a needs section, one line on the slider for the facilitator.

## The tests
- The rate multiplies each drive's fall (a minute of active play at 2.0 drops twice as much as at 1.0; at 0.5 half); stamina and health recovery unchanged; with relaxed needs it multiplies.
- `SaveSettings`: the default writes no field; 1.75 round-trips; a hand-edited 9 is clamped to 4.0 and 1.3 snapped to 1.25.
- The flag: accepted values, a refused one.
- The panel: the layout tests at every canvas and scale; the slider's steps at both ends; the needs paused while it is open.
- Determinism: the same inputs and rate give the same meters.

## Behaviour checks (before the report; look at each screenshot)
- A day-2 world with `--needs on --needs-rate 3`: walk for a minute (a scripted `--wait`); the meters printed, against the same at `--needs-rate 1`.
- A screenshot of the settings panel at 1280x720 and at 800x600 with text scale 150.
