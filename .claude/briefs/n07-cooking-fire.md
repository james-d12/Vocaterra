You are implementing the game milestone **N07, cooking at the fire**, of the Vocaterra repository (C#, .NET 10, a MonoGame first-person voxel game that teaches Spanish). The language core is the `Vocaterra.Nlp` engine and the Spanish pack `src/Vocaterra.Nlp.Es`; the game is `Vocaterra.{Game.Core,Game,Host,SpeechRender}`. **This milestone's work is in the game.** It builds on the needs series (N01 to N06: the needs model, eating, the fire rings, regrowth and gifts, the HUD) and W01 (`item_crafted`, a craft as a world event): it makes the lit fire a cooking station with one instant recipe, an apple to a roast apple.

Do the work yourself; do not spawn subagents.

## Where to work, and pushing
Work only in the git worktree `/home/user/Dev/Vocaterra/.claude/worktrees/n07`, already on the branch `N07-cooking-fire` (from `feature/adaptive_design` at its tip when you start). cd there first; never edit `/home/user/Dev/Vocaterra` or any other worktree; do not switch branches.
- **Commit and push often** (the owner's rule): commit after each item that builds and passes its tests, with the prefix `N07: `, and push after every commit (`git push -u origin N07-cooking-fire` the first time).
- Never push `main` or `feature/adaptive_design`, and do not merge. When you finish, leave the worktree on your branch, clean.

## The specification
The owner's Adaptation Design Document is the specification. Its text (extracted from `docs/game/design/sources/Vocaterra_Adaptation_Design_Document.docx`, one paragraph or table cell per line) is at `/tmp/claude-1000/orch/adaptation-design.txt`: read the sections this brief names with `grep -n` and `sed -n` (the headings are numbered, e.g. `grep -n '^9\.[0-9]* ' …`), never the whole file. Do not edit the `.docx`. Where the design and the code disagree on a file or line, the code wins; say so in the report. The owner adopted the design's section 18.2 recommendations provisionally on 2026-11-04 (`docs/game/decisions.md`, D-game-23, "Owner's answers, 2026-11-04"); where this brief fixes a value the design leaves open, use it and list it in the report under "values chosen".

## Output and context (the cost of a milestone is its turns times its context)
- Send every heavy command's output to a log file under `/tmp/claude-1000/n07/`, and print at most 30 lines of it.
- Keep `sed -n` ranges under about 150 lines; prefer `grep -n` first. Never read `docs/game/speech-checklist.md`, `manifest.json` or the whole of `content/PHRASES.md`.
- Wait for a long command in the foreground. **Never leave a wait loop or a background process behind** (a `while pgrep -f …` loop matches its own command line and never ends).
- **Disk:** about 13 GB free once you build. While iterating, build only the projects you need; build the whole solution once at the end.

## Resources (the desktop has crashed from overload)
- Run every `dotnet restore|build|test|format|run`, every `scripts/content.sh` call and every game run through `scripts/limited.sh` (`-m:4` builds, `-m:2` tests). `scripts/test.sh` and `scripts/content-loop.sh` run directly (content-loop refuses limited.sh, exit 3).
- Never start two heavy commands at once: no `&`, no background builds. Use `scripts/test.sh fast` or `scripts/test.sh project <test project>` while iterating; `scripts/test.sh full` once before the report.
- Exit code 137 means the 6 GB cap killed the command: lower the parallelism.
- **Another agent (L01, lessons as classes) works beside you in its own worktree.** `scripts/limited.sh` runs at most two heavy commands at once machine-wide and caps them together at 8 GB; a command may wait for a slot, which is normal. A command killed with exit 137 or 143 may have met the shared cap: rerun it once, and report it if it happens twice. L01 also edits `content/game-v1` (lessons, NPC offer lines, the sleep offer) and `content/locale/en`: keep your edits to the files your deliverables name, and do not reformat other entries.

## Environment
- Prefix every command inline with `PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1`.
- The sandbox refuses a command line containing the words `eval` or `hash`; use a wrapper script under `/tmp/claude-1000/n07/`.
- Model-gated tests and game runs read `VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models`. Game runs need `DISPLAY=:0`, each its own `--session-dir` under `/tmp/claude-1000/n07/`; scripted steps need `--screenshot`. The host: `dotnet src/Vocaterra.Host/bin/Debug/net10.0/Vocaterra.Host.dll`.
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
The LanguageTool check works from this sandbox (since 2026-11-05): after a content edit that adds Spanish, `scripts/content-loop.sh --grammar --speech` refreshes `content/grammar/` and reports new findings; never edit `content/grammar/` by hand. This milestone adds Spanish (the noun `fuego`, the label «manzana asada»): run the loop with `--grammar` after each such edit, read every new finding, and fix the line or say in the report why it stands.

## Gate (yours, before reporting)
- `scripts/limited.sh dotnet build Vocaterra.sln -warnaserror -m:4`, 0 warnings.
- `scripts/test.sh full` (or `scripts/content-loop.sh --speech --tier full` if you edited content), all passing: 9433 passed and 3 skipped before you; report the new count.
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
- `CLAUDE.md`; `docs/process/code-guidelines.md`; `docs/game/architecture.md` (by `grep -n`); `content/README.md` (how to add an item, a noun, a recipe).
- `docs/game/implementation-plan.md` section 4.9 (your row) and the section 5 entries of N02 (the fire rings, eating), N05 and W01 (`grep -n '^- \*\*N0\|^- \*\*W01'`).
- `docs/game/activities-plan.md`: `grep -n 'N07\|roast\|enum ID'` (the roast apple takes the next free item id, 33 if free; W10's flour takes the one after).
- The design (`/tmp/claude-1000/orch/adaptation-design.txt`): 9.6 in full (`grep -n '^9\.6 '`, about 48 lines: "Cooking at the fire (N07)", "What food is worth" and the food table), and 9.13's line on the roast apple.
- Code: the crafting path (`grep -rn 'station' src/Vocaterra.Game.Core --include=*.cs | grep -i recipe | head`; the workbench `mesa` and how E at it opens the crafting panel), the item enum and its catalogue (`grep -rn 'Antorcha\|antorcha' src/Vocaterra.Game.Core --include=*.cs | head`), `Needs/FireRings.cs` (a lit fire ring), `Session/VoxelSession.Eat.cs` (eating, healing), `content/game-v1/game/recipes.json`, `game/needs.json` (`foods`), `game/nouns.json`, the noun `agua` covering the water bottle (the model for `manzana` covering the roast apple).

## The deliverables
1. **The fire is a station.** A catalogue noun `fuego` for the fire-ring block. E at a **lit** fire ring (N02's rule: lit by a torch in its 3x3) opens the crafting panel with the fire's recipes, as E at the workbench does; an unlit ring does not (say what E does there now, and keep it).
2. **The roast apple.** A new item (the next free enum id; say which), listed under the noun `manzana` with the label «manzana asada» (as the water bottle is listed under `agua`); its icon or tint in the atlas (CC0 or drawn in code). The recipe in `recipes.json`: one apple to one roast apple at the station `fuego`, instant. In `needs.json`'s `foods`: hunger +20, healing 5 (9.6's table). Only cooked food heals; raw food keeps its values.
3. **The event.** The craft raises W01's `item_crafted` with the noun `manzana`; an objective or lesson can count it. No new lesson or objective (the design asks for none).
4. **Spanish and speech.** `fuego` and «manzana asada» held at A2 by the lint and `cefr-check`; any line that needs a WAV gets it through the content loop; the English in `content/locale/en/`.
5. **Saves.** No new save version: an item id is enough. A save holding a roast apple round-trips; every version from 1 loads; an old save without one is unchanged.
6. **Documents:** `docs/game/running.md` if a flag or key changes; the content docs that list items, recipes or stations (`grep -rln 'antorcha' docs/game content/README.md`).

## The tests
- E at a lit fire ring opens the fire's recipes; at an unlit ring it does not; the workbench's recipes are unchanged.
- An apple at a lit fire becomes a roast apple; `item_crafted (manzana)` is logged; without an apple, nothing.
- Eating a roast apple: hunger +20, health +5; eating a raw apple: hunger +12 and no healing.
- The validator refuses a recipe whose station is not a known station (if it does not already).
- A save with a roast apple round-trips; determinism.

## Behaviour checks (before the report; show the lines)
- A scripted run: a lit fire ring (the camp's, or light one), `--craft` at it with an apple in the bag (say how you get the apple: the stall, a tree or a test aid), the craft line and the event; eat it (`--eat`) with hunger and health lowered (`--set-need`): the meters before and after.
- One screenshot of the fire's crafting panel; look at it.
