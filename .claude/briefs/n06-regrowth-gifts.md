You are implementing the game milestone **N06, food that grows back, and fair gifts**, of the Vocaterra repository (C#, .NET 10, a MonoGame first-person voxel game that teaches Spanish). The language core is the `Vocaterra.Nlp` engine and the Spanish pack `src/Vocaterra.Nlp.Es`; the game is `Vocaterra.{Game.Core,Game,Host,SpeechRender}`. **This milestone's work is in the game.** It builds on the needs series N01 to N04 (`src/Vocaterra.Game.Core/Needs/`, `game/needs.json`, N03's day's-resets seam `RunDayResets`): it makes the free food renew with each night and the NPCs' gifts fair.

Do the work yourself; do not spawn subagents.

## Where to work, and pushing
Work only in the git worktree `/home/user/Dev/Vocaterra/.claude/worktrees/n06`, already on the branch `N06-regrowth-gifts` (from `feature/adaptive_design` at its tip when you start). cd there first; never edit `/home/user/Dev/Vocaterra` or any other worktree; do not switch branches.
- **Commit and push often** (the owner's rule): commit after each item that builds and passes its tests, with the prefix `N06: `, and push after every commit (`git push -u origin N06-regrowth-gifts` the first time).
- Never push `main` or `feature/adaptive_design`, and do not merge. When you finish, leave the worktree on your branch, clean.

## The specification
The owner's Adaptation Design Document is the specification. Its text (extracted from `docs/game/design/sources/Vocaterra_Adaptation_Design_Document.docx`, one paragraph or table cell per line) is at `/tmp/claude-1000/orch/adaptation-design.txt`: read the sections this brief names with `grep -n` and `sed -n` (the headings are numbered, e.g. `grep -n '^9\.[0-9]* ' …`), never the whole file. Do not edit the `.docx`. Where the design and the code disagree on a file or line, the code wins; say so in the report. The owner adopted the design's section 18.2 recommendations provisionally on 2026-11-04 (`docs/game/decisions.md`, D-game-23, "Owner's answers, 2026-11-04"); where this brief fixes a value the design leaves open, use it and list it in the report under "values chosen".

## Output and context (the cost of a milestone is its turns times its context)
- Send every heavy command's output to a log file under `/tmp/claude-1000/n06/`, and print at most 30 lines of it.
- Keep `sed -n` ranges under about 150 lines; prefer `grep -n` first. Never read `docs/game/speech-checklist.md`, `manifest.json` or the whole of `content/PHRASES.md`.
- Wait for a long command in the foreground. **Never leave a wait loop or a background process behind** (a `while pgrep -f …` loop matches its own command line and never ends).
- **Disk:** about 15 GB free. While iterating, build only the projects you need; build the whole solution once at the end.

## Resources (the desktop has crashed from overload)
- Run every `dotnet restore|build|test|format|run`, every `scripts/content.sh` call and every game run through `scripts/limited.sh` (`-m:4` builds, `-m:2` tests). `scripts/test.sh` and `scripts/content-loop.sh` run directly (content-loop refuses limited.sh, exit 3).
- Never start two heavy commands at once: no `&`, no background builds. Use `scripts/test.sh fast` or `scripts/test.sh project <test project>` while iterating; `scripts/test.sh full` once before the report.
- Exit code 137 means the 6 GB cap killed the command: lower the parallelism.

## Environment
- Prefix every command inline with `PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1`.
- The sandbox refuses a command line containing the words `eval` or `hash`; use a wrapper script under `/tmp/claude-1000/n06/`.
- Model-gated tests and game runs read `VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models`. Game runs need `DISPLAY=:0`, each its own `--session-dir` under `/tmp/claude-1000/n06/`; scripted steps need `--screenshot`. The host: `dotnet src/Vocaterra.Host/bin/Debug/net10.0/Vocaterra.Host.dll`.
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
- `scripts/test.sh full` (or `scripts/content-loop.sh --speech --tier full` if you edited content), all passing: 9363 passed and 3 skipped before you; report the new count.
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
- The design: 9.6's food table (the rows "A berry", "A gift", and the paragraph after the table beginning "N06 provides"), 9.13's line "A picked berry bush grows back after the next night; a gift comes once per in-game day, except in a lesson step", and the N06 row of 9.12.
- `.claude/briefs/n03-report.md` and `n04-report.md` (the day's-resets seam and what N04 added to it), then the code: `RunDayResets`; how a berry bush is picked today (`grep -rn -i 'berry\|bush\|fruta' src/Vocaterra.Game.Core | head`); how the stall and the villager give bread and Luna gives water today (`grep -rn -i 'give\|gift' src/Vocaterra.Game.Core/Npc src/Vocaterra.Game.Core/Session | head`, and the host events or effect kinds the agents use in `content/game-v1/agents/*.json`).
- `docs/game/saves.md` (the `systems` map), `content/schema/needs.schema.json`.

## The deliverables
1. **Bushes grow back.** A berry bush picked by hand grows back after the next night (sleep or collapse, through `RunDayResets`), not at a load and not before. The picked bushes are saved and round-trip; the world digest's rule for placed and removed blocks is kept (say how a regrown bush relates to the digest).
2. **Fair gifts.** The stall's bread, the villager's bread and Luna's water are each given once per in-game day; the next night makes them available again. A gift that a lesson step requires is always given in that step, whatever the day's count. What the NPC does when today's gift is used: reuse an existing line of that NPC's pools that fits (a polite no, or the trade prompt); add no new Spanish to `content/game-v1` (if nothing fits, say so and use the nearest existing line; list the Spanish line you would have wanted for a later milestone). The counts are saved and round-trip.
3. **Data.** The regrowth and gift rules live in `game/needs.json` (N01 left their parts optional in the schema): which block regrows, which NPC gives which item once a day. The validator refuses an item or NPC that does not exist.
4. **Runs.** The scripted runs keep their baselines (they take `--needs off`; if `--needs off` should not turn the gift rule off, say why and explain each moved run). If a run moves, explain each difference and re-baseline only those.

## The tests
A bush picked, a night, the bush back; picked, a load, the bush still picked; two picks of two bushes; a gift given, asked again the same day (the fallback), a night, given again; a lesson step's gift given twice the same day; saves round-trip with picked bushes and used gifts; the validator's refusals; determinism.

## Behaviour checks (before the report; show the lines)
Through the host or the game's scripted options: ask the stall for bread twice in one day (the second reply), `--sleep`, ask again (the bread); pick a bush, `--sleep`, the bush back (a screenshot of it, or the block in the save).
