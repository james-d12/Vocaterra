You are implementing Y15, "The stake taken at the purchase", for the Vocaterra repository (C#, .NET 10). The game is Vocaterra.Game.Core,
Vocaterra.Game, Vocaterra.Host and tools/Vocaterra.SpeechRender; the language core is Vocaterra.Nlp and its packs. This milestone is a
small change of game logic asked for by the owner on the pull request (2026-11-12): "Remove the stake from the bag after the purchase."
The core is taken as it is.

Where to work: /home/james/Dev/Vocaterra/.claude/worktrees/y15, which already exists on the branch `Y15-stake-taken` (cut from
`feature/settlement` after Y14 was recorded; Y00 to Y14 are all in it). Start every command with
`cd /home/james/Dev/Vocaterra/.claude/worktrees/y15 &&` and use absolute paths for file tools; your shell may start in another checkout,
which you must not edit. Commit after each item that builds and passes its tests, with the prefix `Y15: `. Do not push your branch. Never
push or merge `feature/settlement` or `main`. When you finish, leave the worktree on its branch, clean, with no process of yours running.
No other agent builds while you work.

Resources (the machine has crashed from overload): run every dotnet restore, build, test, format and run, every scripts/content.sh call
and every screenshot or scripted run through scripts/limited.sh (for example `scripts/limited.sh dotnet build Vocaterra.sln -warnaserror
-m:4`; tests with `-m:2`). scripts/test.sh, scripts/content-loop.sh and scripts/scripted-runs.sh call it themselves: run them directly
(content-loop refuses to run under limited.sh). Never start two heavy commands at once (no `&`, no background builds, no parallel test
runs), and write no shell wait loop on `pgrep -f`. Exit 137 means the 6 GB cap killed it: lower the parallelism. Set
`VOCATERRA_RUNS_WORK` to a folder whose name is unique to Y15 (in your scratchpad) for every `scripts/scripted-runs.sh` call.

Environment: prefix every command inline with PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1 (the sandbox refuses
a command line containing the words `eval` or `hash`: use a wrapper script in your scratchpad for those). Model-gated tests need
VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models.

Read first (lean: name the sections, never whole long files): CLAUDE.md; docs/process/code-guidelines.md; the report
/home/james/Dev/Vocaterra/.claude/worktrees/settlement/.claude/briefs/y11-report.md (the sale, the stake's offer, the checklist
`my-land`). The code: `Session/VoxelSession.Land.cs` (`SellPlot`, `LandOfferAt`), `Session/VoxelSession.cs` (`Give`, `Take`, `CanGive`,
`SecondStake`), `Items/Inventory.cs` (`CanExchange`), `Npc/NpcHostEvents.cs` (the mayor's answers by the plot's facts, near line 330),
`Session/ObjectiveBook.cs` (how a step is counted and restored: `restore` values such as `plot-owned`), `content/game-v1/game/objectives/my-land.json`,
`content/locale/` (`ui.plotBought`), `tests/Vocaterra.Game.Core.Tests/Land/PlotPurchaseTests.cs`.

Rules: a game milestone edits only the game projects, their tests, content/game-v1, content/locale, content/schema, assets/ and docs/game/
and takes the core as it is. A regression check is a C# test, never a script with text baselines; tests are named Given_When_Then in
folders that mirror the source; do not edit CLAUDE.md, either implementation-plan.md, either decisions.md or docs/game/settlement-plan.md
(the orchestrator does); no System.Random and no clock in the evaluation path; nothing under .github/workflows/. No new Spanish line (the
English hint carries the news). The content gate at the A2 ceiling must stay clean. No save backward compatibility (no migration code).

Deliverables:

1. **The mayor keeps the stake.** In `SellPlot`, when the sale goes through, every stake in the bag leaves it (through `Take`, so the log
   shows it handed to the mayor), in the same operation as the coins and the kit: a refused sale leaves the stake where it was. A sale
   with no stake in the bag (the plot marked by `--mark-plot`, or the stake put in a chest) still goes through; a stake in a chest stays
   there (not chased; say so). Decide whether the exchange's room check counts the stake's slot as freed (a full bag that holds the stake
   and pays only part of its coins could then still take the kit): do it if it is a few lines, and say what you chose.
2. **The checklist stays right.** `my-land`'s `stake` step counts the bag and a load restores it from the bag. After the purchase it must
   stay done, in the same run and after a save and load (with no stake left in the bag): for example the step is also done when the
   plot is owned (a `restore` value, or the counting rule in `ObjectiveBook`: choose the smallest honest change and say why). Update the
   objective's `about` text. If you edit the bundle, `scripts/content-loop.sh` hashes it and puts the new hash into the save fixture.
3. **What the player is told:** `ui.plotBought` says the mayor keeps the stake (for example "You bought the plot for {price} coins. The
   mayor keeps the stake; the camp kit is in your bag."). Check that nothing afterwards offers, asks for or points at the stake: the
   mayor's offer (`LandOfferAt` returns nothing once the land is owned), his answers by the plot's facts (owned comes before `no_stake`),
   the HUD's plot line (it shows "Your land" near the plot without the stake), the stake's use hook. Report what each does now.
4. **Tests** (in `PlotPurchaseTests` and where they belong): a purchase leaves no stake and the step `my-land.stake` done; each refused
   sale keeps the stake; a sale with no stake held goes through; the save round-trip after a purchase has no stake and the step done after
   the load; the exchange's room rule you chose in 1. Update every existing assertion that expected the stake to stay after a purchase.
5. **Scripted runs:** capture `r41-land` and `r42-camp` before your first change and after your last; report each difference and why (the
   stake gone from the bag after the purchase is the expected one).
6. **Documents:** grep docs/game/ and content/README.md for statements that the stake stays or is kept after the purchase (running.md's
   walkthrough, playtest.md, architecture.md, saves.md, scripted-runs.md) and correct them.

Gate (from your worktree's root): scripts/limited.sh dotnet build Vocaterra.sln -warnaserror -m:4 (0 warnings); scripts/test.sh full
(report the count against Y14's: 11,463 passed, 3 skipped); scripts/limited.sh dotnet format Vocaterra.sln --verify-no-changes clean;
scripts/limited.sh scripts/content.sh validate passes; the A2 content gate (`scripts/content.sh lint content/game-v1 --strict --level a2
--fail-on-level --host-pools 'guide.*'`) clean. A failure of the known fuzz coverage floor (`PipelineFuzzTests`, "The generator no longer
reaches fallback:...") is rerun once and reported. Do not commit bin/, obj/ or artifacts/.

Commit messages end with
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>

Report, half a page: branch, commit SHA, worktree path; what changed (the sale, the step's rule, the room rule, the hint); what the mayor,
the HUD and the stake's hook do after the purchase; the tests added or changed and the count; the scripted runs' differences; the
documents corrected; anything left undone and why. Leave out how you searched.
