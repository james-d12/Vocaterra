You are implementing the game milestone **W07, the station view**, of the Vocaterra repository (C#, .NET 10, a MonoGame first-person voxel game that teaches Spanish). The language core is the `Vocaterra.Nlp` engine and the Spanish pack `src/Vocaterra.Nlp.Es`; the game is `Vocaterra.{Game.Core,Game,Host,SpeechRender}`. **This milestone's work is in the game**: the overlay through which the learner works a procedure at a station (the model in `Vocaterra.Game.Core`, the drawing in `Vocaterra.Game`), on top of W06's runner, plus three scripted steps.

Do the work yourself; do not spawn subagents.

## Where to work, and committing
Work only in the git worktree `/home/user/Dev/Vocaterra/.claude/worktrees/w07`, already on the branch `W07-station-view` (from `feature/bread-pilot` at its tip, with W05 and W06 merged). cd there first; never edit `/home/user/Dev/Vocaterra` or any other worktree (`.claude/worktrees/adaptive` is the owner's playtest checkout: never touch it); do not switch branches.
- **Commit after each item that builds and passes its tests**, with the prefix `W07: `. Never end your turn, or wait on anything, with uncommitted work.
- **Do not push**: your branch stays local and the gate merges it from there (owner, 2026-11-05). Never push or merge `feature/bread-pilot`, `feature/adaptive_design` or `main`. When you finish, leave the worktree on your branch, clean.
- **Run everything in the foreground and finish in one turn.** Do not start background commands or monitors and end your turn to wait for them.

## The specification
- `docs/game/activities-plan.md`: section 4.3 (`sed -n 120,126p`) and section 6's paragraph **W07** (`sed -n 272,280p`). That paragraph's code, scripted steps, tests and "done when" are the milestone.
- The owner's design document, extracted one paragraph per line at `/tmp/claude-1000/orch/adaptation-design.txt`: **10.8 "The station" (`sed -n 834,842p`)**: entry and its priority, the layout, what can be held, labels, keys, D-game-9 inside the station, audio and the mute fallback; **10.2 (`sed -n 703,717p`)** and **10.10 (`sed -n 895,930p`)** for what each rung shows and when the highlight appears; decision 19 (`sed -n 1466,1468p`). Read only those ranges; do not edit the `.docx`.
- **W06's API** (its report, `.claude/briefs/w06-report.md`, last paragraph; its findings entry `grep -n 'W06, the procedure runner' docs/game/implementation-plan.md`): `GuideSession.StartProcedure`, `ActInProcedure(use, on, verb, presented)`, `CollectProcedure`, `HelpInProcedure`, `Procedures.Repeat/Slower/Leave/Abandon/View/Progress`, `BakeMastered`. Pass `presented` truthfully: `Heard` when the row's audio played, `Read` when its text was on screen; with no audio device or `--mute`, the Spanish card shows at the audio-only rung and the act is `Read` (10.8, decision 19's provisional value in W06's entry). W06 left to you the per-noun read count once per bake (10.10): record it in the view model if 10.10 needs it for the hover English, or say why not.
- The crafting UI is the pattern to follow, end to end: Core `Items/Crafting/CraftingScreen.cs`, `CraftingPanel.cs`, `RecipePanelModel.cs`; Game `UI/Crafting/CraftingLayer.cs`, `CraftingLayout.cs`, `RecipePanelLayer.cs`; how `VoxelGame` builds and opens the crafting panel. Icons: V05 painted the coin, apple and axe (`Graphics/Blocks/BlockAtlasLayout.cs`, `BlockArtVillage.cs`, `BlockCell.Icon*`); paint `bowl`, `spoon` and `oven` the same way, found by a key lookup with a plain fallback tile for any key without art. Scripted steps: `Scripted/ScriptStepKind.cs`, `ScenarioScript.cs`, and `docs/game/scripted-runs.md` for how runs are written.
- Where the design and the code disagree on a file or a name, the code wins; say so in the report. List every value you choose where the design leaves one open.

## A requirement from the owner's later plan (W30, the recipe book)
Keep **the instruction strip a model of its own** (the row's Spanish, its English when shown, its rung, the step dots, the Repetir / Más despacio / Ayuda actions), built from `ProcedureView` and drawn by its own small layer, not fields spread through the station panel. A later milestone shows the same strip on a page of a carried recipe book set down beside the station (`docs/game/activities-plan.md`, "Proposed: the recipe book"). Build nothing of the book.

## What to build (each committed)
1. Core: `Activities/StationScreen`, `StationPanel`, the view records (cells with an icon key, a noun with its article, a highlight flag; the instruction strip model); held things (bag items paged, fixture tools, intermediates, the empty hand), targets, the verb choice only for a pair with several verbs; keys of 10.8 as panel commands.
2. Game: `UI/Station/StationLayer`, `StationLayout`, the strip's layer; `VoxelGame` builds the panel as it builds the crafting panel; E at a station's prop opens it (priority: an NPC, then a station prop, then the workbench); the arm and camera do not move and the needs pause while it is open; the three icons with the fallback tile.
3. Audio: each row through `NpcVoice`, the slow file for "Más despacio"; the mute fallback.
4. Scripted steps `--station <id>`, `--act <use>,<on>[,<verb>]`, `--proc-help` (`ScriptStepKind`, `ScenarioScript`, `LaunchOptions`), documented in `docs/game/scripted-runs.md`'s step list.
5. Tests: the paragraph's list (verb choice only for several verbs; the highlight after a slip or on help per 10.10; the English only at rung 1 or after help; a component test runs a three-step test procedure through the panel; layout tests at every UI scale with no overlap), plus entry priority and the strip model alone.

**The "done when" screenshot with no bread content yet.** The bundle has no procedures or stations until W10. For the screenshot only, copy the bundle to `/tmp/claude-1000/w07/bundle/` (`cp -r content/game-v1`), add there a station on the existing `oven` prop with Luz (`baker`), the three fixtures, a three-step test procedure and its `does` rows reusing lines that are already recorded (any of Luz's), re-hash that copy with `scripts/content.sh hash` through a wrapper script, and run the game with `--content /tmp/claude-1000/w07/bundle --station …` and `--screenshot`. Look at the PNG (Read it) at the first rung and once after `--proc-help`. **Commit nothing of that copy**, and add no procedure, station or Spanish to `content/game-v1`.

Out of scope: the teacher's host kinds and talking to Luz from the station (W08: leave the hook, show nothing), flour and the bread lesson (W10), telemetry (W11), the recipe book (W30).

## Output and context (the cost of a milestone is its turns times its context)
- Send every heavy command's output to a log file under `/tmp/claude-1000/w07/`, and print at most 30 lines of it.
- Keep `sed -n` ranges under about 150 lines; prefer `grep -n` first. Never read `docs/game/speech-checklist.md`, `manifest.json` or the whole of `content/PHRASES.md`.
- **Never leave a wait loop or a background process behind** (a `while pgrep -f …` loop matches its own command line and never ends).
- **Disk:** about 11 GB free. While iterating, build only the projects you need; build the whole solution once at the end. Delete the bundle copy when done.

## Resources (the desktop has crashed from overload; the owner is playing the game on this machine)
- Run every `dotnet restore|build|test|format|run`, every `scripts/content.sh` call, every game run and screenshot through `scripts/limited.sh` (`-m:4` builds, `-m:2` tests). `scripts/test.sh` runs directly.
- Never start two heavy commands at once: no `&`, no background builds. Use `scripts/test.sh project <test project>` or `scripts/test.sh fast` while iterating; `scripts/test.sh full` once before the report.
- Exit code 137 or 143 means a cap killed the command: lower the parallelism or rerun; never raise a cap.

## Environment
- Prefix every command inline with `PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1`.
- The sandbox refuses a command line containing the words `eval` or `hash`; use a wrapper script under `/tmp/claude-1000/w07/`.
- Model-gated tests and game runs read `VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models`. Game runs need `DISPLAY=:0`, each its own `--session-dir` under `/tmp/claude-1000/w07/`; scripted steps need `--screenshot`. The game: `src/Vocaterra.Game/bin/Debug/net10.0/Vocaterra.Game`.

## Rules
- **What this milestone edits:** `src/Vocaterra.Game.Core`, `src/Vocaterra.Game`, `src/Vocaterra.Host` (only if a shared option needs it), their tests (`tests/Vocaterra.Game*.Tests`, `tests/Vocaterra.Host.Tests`), `content/locale` (the `ui.station*` strings and a controls entry, outside game-v1's hash), `content/game-v1/game/controls.json` only if the controls entry lives there (then the content loop below), `assets/` and `docs/game/` (not the plans or the decisions).
- It takes the core as it is. A core gap is reported, never patched. A regression check is a C# test, never a script with text baselines.
- Engine code only in `src/Vocaterra.Game`; `Vocaterra.Game.Core` references no package (`ArchitectureRulesTests`). Logic in Core, drawing and devices in Game (`docs/process/code-guidelines.md`, `docs/game/architecture.md`). Assets CC0 only (D-game-5); paint the icons in code as V05 did.
- Determinism: no `System.Random` and no wall clock in the game's logic. Every save version from 1 still loads.
- Do not edit either `implementation-plan.md`, `docs/game/activities-plan.md`, `docs/game/village-plan.md`, either `decisions.md`, `scripts/merge-gate.sh`, the design `.docx`, or anything under `.claude/` or `.github/workflows/`.
- If you edit anything under `content/game-v1`: `scripts/content-loop.sh --speech` after the edit (run directly, never under limited.sh) and `--tier full` once before the report instead of `scripts/test.sh full`.

## Gate (yours, before reporting)
- `scripts/limited.sh dotnet build Vocaterra.sln -warnaserror -m:4`, 0 warnings.
- `scripts/test.sh full`, all passing: 9645 passed and 3 skipped before you; report the new count.
- `scripts/limited.sh dotnet format Vocaterra.sln --verify-no-changes`; `scripts/limited.sh scripts/content.sh validate`.
- **The scripted runs** (`docs/game/scripted-runs.md`): `scripts/scripted-runs.sh` before your first edit and after, compared with `python3 -I scripts/compare-runs.py`, one at a time, in the foreground. No baseline should move (the bundle has no station); explain any difference (`r30-no-classes`' `declined.png` differed between two captures of the same build in W05: say if it does again).
- Do not commit `bin/`, `obj/` or `artifacts/`. When you finish, `git status` is clean.

## Commit
Each message ends with:
```
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
```

## Report (one page, under 500 words)
- The branch and the final commit SHA (local, not pushed).
- Each deliverable: what you built (file and type), its tests.
- **Values chosen** where the design left one open, and every place the design and the code disagreed.
- The screenshots' paths (keep the two PNGs under `/tmp/claude-1000/w07/`) and what each shows.
- The hook W08 will use to send push-to-talk from the station to the teacher; the strip model's type for W30.
- The scripted runs' differences; the test count before and after; anything left undone, and why.

Before your final message, make sure no process you started is still running (`pgrep -fa 'dotnet|Vocaterra|scripted-runs'`, kill only your own).
