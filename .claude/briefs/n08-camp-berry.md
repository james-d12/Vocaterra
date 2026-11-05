You are implementing the game milestone **N08, a berry planted at the camp**, of the Vocaterra repository (C#, .NET 10, a MonoGame first-person voxel game that teaches Spanish). The language core is the `Vocaterra.Nlp` engine and the Spanish pack `src/Vocaterra.Nlp.Es`; the game is `Vocaterra.{Game.Core,Game,Host,SpeechRender}`. **This milestone's work is in the game.** It is the design's decision 14 (self-sufficiency at the camp), adopted provisionally on 2026-11-04 and authorised by the owner on 2026-11-05: the player plants a berry near the camp and it becomes a berry bush through N06's regrowth rule. It is small and builds on N06 (regrowth and gifts) and N01 (the needs data).

Do the work yourself; do not spawn subagents.

## Where to work, and pushing
Work only in the git worktree `/home/user/Dev/Vocaterra/.claude/worktrees/n08`, already on the branch `N08-camp-berry` (from `feature/adaptive_design` at its tip when you start). cd there first; never edit `/home/user/Dev/Vocaterra` or any other worktree; do not switch branches.
- **Commit often, push never** (the owner's rules, 2026-10-30 and 2026-11-05): commit after each item that builds and passes its tests, with the prefix `N08: `. Do not push the branch: it stays local and the gate merges it from there.
- Never push anything (not `main`, not `feature/adaptive_design`, not your branch), and do not merge. When you finish, leave the worktree on your branch, clean.

## The specification
The owner's Adaptation Design Document is the specification. Its text (extracted from `docs/game/design/sources/Vocaterra_Adaptation_Design_Document.docx`, one paragraph or table cell per line) is at `/tmp/claude-1000/orch/adaptation-design.txt`: read the sections this brief names with `grep -n` and `sed -n` (the headings are numbered, e.g. `grep -n '^9\.[0-9]* ' …`), never the whole file. Do not edit the `.docx`. Where the design and the code disagree on a file or line, the code wins; say so in the report. The owner adopted the design's section 18.2 recommendations provisionally on 2026-11-04 (`docs/game/decisions.md`, D-game-23, "Owner's answers, 2026-11-04"); where this brief fixes a value the design leaves open, use it and list it in the report under "values chosen".

## Output and context (the cost of a milestone is its turns times its context)
- Send every heavy command's output to a log file under `/tmp/claude-1000/n08/`, and print at most 30 lines of it.
- Keep `sed -n` ranges under about 150 lines; prefer `grep -n` first. Never read `docs/game/speech-checklist.md`, `manifest.json` or the whole of `content/PHRASES.md`.
- Wait for a long command in the foreground. **Never leave a wait loop or a background process behind** (a `while pgrep -f …` loop matches its own command line and never ends).
- **Disk:** about 13 GB free once you build. While iterating, build only the projects you need; build the whole solution once at the end.

## Resources (the desktop has crashed from overload)
- Run every `dotnet restore|build|test|format|run`, every `scripts/content.sh` call and every game run through `scripts/limited.sh` (`-m:4` builds, `-m:2` tests). `scripts/test.sh` and `scripts/content-loop.sh` run directly (content-loop refuses limited.sh, exit 3).
- Never start two heavy commands at once: no `&`, no background builds. Use `scripts/test.sh fast` or `scripts/test.sh project <test project>` while iterating; `scripts/test.sh full` once before the report.
- Exit code 137 means the 6 GB cap killed the command: lower the parallelism.
- **Another agent (U02, the conversation surface) works beside you in its own worktree.** `scripts/limited.sh` runs at most two heavy commands at once machine-wide and caps them together at 8 GB; a command may wait for a slot, which is normal. A command killed with exit 137 or 143 may have met the shared cap: rerun it once, and report it if it happens twice. U02 edits the dialogue panel (`DialogueModel`, `DialoguePanel`, `DialogueLayout`, the lesson strip), key bindings and `docs/game/running.md`: keep your edits there small and local: keep your edits to the files your deliverables name, and do not reformat other entries.

## Environment
- Prefix every command inline with `PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1`.
- The sandbox refuses a command line containing the words `eval` or `hash`; use a wrapper script under `/tmp/claude-1000/n08/`.
- Model-gated tests and game runs read `VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models`. Game runs need `DISPLAY=:0`, each its own `--session-dir` under `/tmp/claude-1000/n08/`; scripted steps need `--screenshot`. The host: `dotnet src/Vocaterra.Host/bin/Debug/net10.0/Vocaterra.Host.dll`.
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
The LanguageTool check works from this sandbox (since 2026-11-05): after a content edit that adds Spanish, `scripts/content-loop.sh --grammar --speech` refreshes `content/grammar/` and reports new findings; never edit `content/grammar/` by hand. This milestone adds no Spanish unless a line is unavoidable (a hint is English, the support language); if it edits `content/game-v1`, run the loop with `--grammar --speech`.

## Gate (yours, before reporting)
- `scripts/limited.sh dotnet build Vocaterra.sln -warnaserror -m:4`, 0 warnings.
- `scripts/test.sh full` (or `scripts/content-loop.sh --speech --tier full` if you edited content), all passing: 9501 passed and 3 skipped before you; report the new count.
- `scripts/limited.sh dotnet format Vocaterra.sln --verify-no-changes`.
- `scripts/limited.sh scripts/content.sh validate`; the lint at A2 clean (`scripts/content.sh lint content/game-v1 --strict --level a2 --fail-on-level --host-pools 'guide.*'`).
- **The scripted runs** (`docs/game/scripted-runs.md`): `scripts/scripted-runs.sh` before your first edit and after, compared with `python3 -I scripts/compare-runs.py`, one at a time through `scripts/limited.sh`. Explain every difference; re-baseline only the runs this milestone is meant to move.
- **CI's case:** the full suite once without models (`VOCATERRA_MODELS_DIR` an empty folder); report any wall-clock test within four times of its budget.
- Do not commit `bin/`, `obj/` or `artifacts/`. When you finish, `git status` is clean (the branch committed, not pushed).

## Commit
Each message ends with:
```
Co-Authored-By: Claude Haiku 5.5 <noreply@anthropic.com>
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
- `CLAUDE.md`; `docs/process/code-guidelines.md`; `content/README.md` (how to edit `game/needs.json` and its schema).
- `docs/game/implementation-plan.md` section 4.9 (your row) and the section 5 entries of N02 (gathering, fire rings) and N06 (regrowth and gifts) (`grep -n '^- \*\*N02\|^- \*\*N06'`).
- The design (`/tmp/claude-1000/orch/adaptation-design.txt`): `grep -n 'decision 14\|first bush\|Optional N08'` (a few lines: "plant a berry near the camp to establish a bush using N06's regrowth rules"; shelter is out of scope).
- Code: `Session/VoxelSession.Renewal.cs` (the picked bushes, `RenewAtNight`), `Needs/NeedsData.cs` (`RegrowRule`), `Content/GameDataValidator.cs` (`CheckRenewal`), `Session/VoxelSession.cs` (the camp's fire ring point `CampFireRing`, about line 973), the place action (how the right button places a held block item; `grep -n 'Place' src/Vocaterra.Game.Core/Session/*.cs | head`), `Session/VoxelSession.Eat.cs` (the right button eats a held food today: planting must not break eating), `content/game-v1/game/needs.json`, `content/schema/needs.schema.json`, `game/scene.json` (the camp's bushes, `"type": "bush"`).

## The deliverables
1. **The rule in data.** An optional `planting` entry in `needs.json` and its schema: the item planted (`berry`), the block it becomes at once (`bush`, the plain bush, as a picked berry bush is), the block it grows into after a night (`berry-bush`, through N06's regrowth), the radius around the camp's fire ring (8 blocks, horizontal), and the most planted bushes alive at once (6). `GameDataValidator` refuses an unknown item or block, a grown block with no regrowth rule, a radius or a count of 0 or less.
2. **The action.** With a berry held, a chosen input plants it on the top of a grass or dirt block whose cell above is free, inside the radius: the berry leaves the bag, the cell becomes a plain bush and is recorded as picked (so N06's `RenewAtNight` grows it). Choose the input so eating stays as it is: for example **right click on a ground block plants, right click in the air eats** (say what you chose and why); outside the radius, on other ground, at the cap, or with no berry, nothing happens and a short English toast says why (`content/locale/en`). A world event `bush_planted` (detail `fruta`), joining `WorldEvents.All`.
3. **Night and saves.** The planted bush grows into a berry bush at the next night with the same rules as N06's (needs on; `--needs off` plants nothing and grows nothing, like N06); picking it later works as any berry bush. Saved through N06's `systems.bushes` (no new save field if the picked list carries it; say so) and the count of planted bushes alive, if needed; the save version stays 6; every version from 1 loads.
4. **Test aid and docs:** a scripted step `--plant x,y,z` (or the existing `--interact` with a held berry, if it fits; say which); `docs/game/running.md` and `docs/game/saves.md` if a field is added.

## The tests
- Planting inside the radius on grass: the berry is used, a plain bush stands, `bush_planted` logged; a night later it is a berry bush; picking it gives a berry.
- Outside the radius, on stone, on an occupied cell, at the cap of 6, with no berry: nothing changes.
- Eating a berry still works (the input split of deliverable 2).
- `--needs off`: no planting, no growth.
- A save with a planted bush round-trips, and a night after the load grows it; determinism; the validator's refusals; the rule optional in the file.

## Behaviour checks (before the report; show the lines)
- `--needs on`, a berry in the bag (`--give berry` or a picked bush), plant one at the camp, `--sleep`, pick it: the lines.
- One screenshot of the planted bush before and after the night; look at them.
