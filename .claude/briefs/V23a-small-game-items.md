You are implementing the game milestone **V23a, the small game items that touch no content**, of the Vocaterra repository (C#, .NET 10, a MonoGame first-person voxel game that teaches Spanish). The language core is the `Vocaterra.Nlp` engine and the Spanish pack `src/Vocaterra.Nlp.Es`; the game is `Vocaterra.{Game.Core,Game,Host,SpeechRender}`. **This milestone's work is in the game's code (`src/Vocaterra.Game.Core`, `src/Vocaterra.Host`, `src/Vocaterra.Game`) and `content/locale` only.** It is part of the follow-ups after PR #20's merge (`docs/HANDOVER_VILLAGE_PHASE_B.md` section 9.3 G; read it) with small findings of the QA pass `docs/qa-reports/2026-11-01-progression.md`. The items that edit `content/game-v1` wait for a later milestone (V23b).

Do the work yourself; do not spawn subagents.

## Where to work, and pushing
Work only in the git worktree `/home/user/Dev/Vocaterra/.claude/worktrees/v23a`, already on your branch `V23a-small-game-items` (from `feature/village-followups` at @@BASE@@): cd there first, and never edit `/home/user/Dev/Vocaterra`, the worktree `.claude/worktrees/village` or any other worktree. **Other agents run at the same time** (a core milestone); every heavy command goes through `scripts/limited.sh`, whose machine-wide lock makes yours wait for theirs.
- **The owner wants work pushed often.** Commit after each item that builds and passes its tests, with the prefix `V23a: `. Push after every commit: `git push -u origin V23a-small-game-items` the first time, then `git push origin V23a-small-game-items`.
- Never push `main` or `feature/village-followups`, and do not merge.
- **Disk is short (about 10 GB free).** Build only the projects you need, never the whole solution: `tests/Vocaterra.Game.Core.Tests`, `tests/Vocaterra.Host.Tests`, `src/Vocaterra.Game` and `tests/Vocaterra.Game.Tests`. The orchestrator's gate builds and tests the whole solution afterwards.

## The items (each with a test that fails before the fix; one commit each)
1. **`WorldFacts.NearestOfNounJoinedTo` floods a water body with no bound** (`src/Vocaterra.Game.Core/Npc/WorldFacts.cs`, about line 253). `WorldFactsTests` ran out of memory once under the 6 GB cap during V25. Cap the visited blocks (for example at 4096) and fall back to the nearest block seen. The results on the shipped scene must not change (the existing tests and the scripted runs' digests), and the test's memory must drop: report its time and, if you can, its peak memory before and after. **Do this item first.**
2. **`KeyWordsTaught` is one save flag** (`Session/GuideSession.cs` about line 356, `Session/VoxelSession.Skip.cs`, `Save/SaveGame.cs`). Make it a set of NPC ids (the NPC whose key words were taught), with a save migration: an older save's `true` means the shopkeeper's. A new field gets a default; the save fixtures must still load (X01).
3. **The session skip buys one apple, not three, for the market** (`Session/SessionSkip.cs`): a skip into session 10 holds what its lesson assumes, three apples (read session 10's lesson in `docs/game/VILLAGE_PLAN.md` section 3.5 and the coin path in `docs/HANDOVER_VILLAGE_PHASE_B.md`, grep "The coin path").
4. **The host's `--transcript` mode has no bag** (`src/Vocaterra.Host/HostOptions.cs` and where a sale or purchase's line is chosen): add an optional `--bag <item:count,...>` (for example `--bag wood:4,coin:2`), so a sale's or a purchase's line follows the bag (no wood: «No tienes madera.»). Without it the host behaves exactly as today (its tests and demos unchanged).
5. **The closing review is skipped when nothing is due:** show one short line instead, so the learner knows it ran. The line is UI text: add it to `content/locale` (the L1 locale bundle, outside `content/game-v1`'s hash; read `content/README.md` for its keys and refresh), never to `content/game-v1`.
6. **The objective after a talk starts its lesson** (QA observation): after `--talk baker` starts `numeros-y-pan`, the HUD still reads "Next: talk to the baker for the lesson …". Once the lesson has begun, the objective says so (in the words the objective already uses for a lesson under way, or a new locale line).
7. **No closing line after session 10:** a sleep after the last lesson goes into session 11 with nothing said. When no later session has a lesson, the sleep offer or the closing card says the lessons are all done for now (a locale line). Do not invent new sessions.
8. **A do step that waits for a pickup the bag already holds** (QA-progression-4, the step `n4-wood` of `numeros-y-pan`, which waits for `item_picked_up` while the learner already holds the five wood): in the lesson runner, a `do` step whose event is a pickup of an item and count the bag already holds is done at once. Change the runner, not the lesson's data. Check the other lessons' do steps for a change of behaviour and list any.
9. **A farewell logged under another NPC's prompt id** (QA observation): `Adiós` to the fisher is logged as `turn 8 (fisher, villager-open)`. Log the prompt actually used.
10. **If small:** the first `--screenshot` before any tick is a partly black frame; take it after the first drawn frame. Skip it if it is not small, and say so.

## Read first (the sections named, not whole files)
- `CLAUDE.md`; `docs/game/ARCHITECTURE.md` (by `grep -n`); `docs/code_guidelines.md`; `content/README.md` (the locale bundle).
- `docs/HANDOVER_VILLAGE_PHASE_B.md` section 9.3 G (`grep -n '^\*\*G\. ' docs/HANDOVER_VILLAGE_PHASE_B.md`, about 20 lines).
- `docs/qa-reports/2026-11-01-progression.md`: QA-progression-4 and the Observations.
- The code named in each item, and its tests.

## Resources (the desktop has crashed from overload)
- Run every `dotnet restore|build|test|format|run`, every `scripts/content.sh` call and every game or screenshot run through `scripts/limited.sh` (builds `-m:4`, tests `-m:2`). Never start two heavy commands at once: no `&`, no background builds.
- While iterating, use `scripts/test.sh project <test project>`. Do not run `scripts/test.sh fast` or `full` (they build the whole solution); the gate runs the full suite.
- Exit code 137 means the 6 GB cap killed the command: lower the parallelism, and never work around the cap.

## Environment
- Prefix every command inline with `PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1`.
- The sandbox refuses a command line that contains the words `eval` or `hash`.
- Game runs need `DISPLAY=:0` and `VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models`; give each run its own `--session-dir` under `/tmp/claude-1000/v23a/`.

## Rules
- A game milestone edits only the game projects and their tests, `content/`, `assets/` and `docs/game/`; **this one edits no file under `content/game-v1`** (its hash must not move: M66 is changing it in parallel). `content/locale` is allowed. It takes the core as it is.
- `Vocaterra.Game.Core` references no package and uses no `System.Random`, `Guid` or clock (`ArchitectureRulesTests`); engine code only in `src/Vocaterra.Game`.
- A save must still load an older save (X01); a new field gets a default.
- If a scripted run's digest or output moves, say which item moved it and why it is right.
- Update `docs/game/README.md` where it lists these open points (grep "KeyWordsTaught", "closing review", "--transcript"), in a few lines.
- Do not edit `scripts/`, `docs/game/IMPLEMENTATION_PLAN.md`, `docs/game/VILLAGE_PLAN.md`, `docs/nlp/`, `docs/qa-reports/` or either `DECISIONS.md`. Never create or edit anything under `.github/workflows/`.
- Write code that reads like the surrounding code.

## Your checks before reporting (from the worktree root, each through scripts/limited.sh)
- Each project you changed builds with `-warnaserror`, 0 warnings; `Vocaterra.Game.Core.Tests`, `Vocaterra.Host.Tests` and `Vocaterra.Game.Tests` pass (the counts before and after).
- `dotnet format --verify-no-changes` on each project you changed.
- `scripts/content.sh validate` (the locale bundle refreshed if you added lines).
- The host demos still hold: `--transcript "quiero agua, por favor" --prompt stall-need` gives `request_item`, communicated; `--transcript "hola, quiero dos panes" --prompt baker-open` gives the sale; `--transcript "vendo madera" --prompt smith-open` gives `offer_item`; and with `--bag coin:0` the bread purchase gives the no-coins line.
- A game run for items 6 and 8: `--skip-to-session 5`, the talk to the baker, a look at the objective line (screenshot), and the wood step with five wood already held.
- Do not commit `bin/`, `obj/` or `artifacts/`.

## Commit
Each message ends with:
```
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
```

## Report (one page, under 400 words)
- The branch and the final commit SHA, pushed.
- For each item: what was built, the test, the result (item 1: the test's time and memory before and after).
- The save migration (item 2); the locale lines added (items 5 to 7).
- Any changed behaviour in other lessons (item 8) or scripted runs.
- The test counts before and after.
- Anything left undone, and why.

Before your final message, make sure no process you started is still running (`pgrep -fa 'dotnet|Vocaterra'`, kill only your own) and that the worktree is clean and pushed.
