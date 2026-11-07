You are implementing the game milestone **W04, price lists**, of the Vocaterra repository (C#, .NET 10, a MonoGame first-person voxel game that teaches Spanish). The game is `Vocaterra.{Game.Core,Game,Host,SpeechRender}`; the language core (`Vocaterra.Nlp*`) is not yours. **This milestone is code, schema and tests only**: a `sell` or `buy` host event may carry a price list keyed by the item the learner names, so one NPC can sell several things. **No `content/game-v1` edit**: every existing entry keeps its form and the game behaves exactly as before.

Do the work yourself; do not spawn subagents.

## Where to work, and committing
Work only in the git worktree `/home/user/Dev/Vocaterra/.claude/worktrees/w04`, already on the branch `W04-price-lists` (from `feature/bread-pilot` at its tip). cd there first; never edit `/home/user/Dev/Vocaterra` or any other worktree (`adaptive`, `german`, `v31` and any other are not yours: never touch them); do not switch branches. The worktree is new and not built: build once first.
- **Commit after each item that builds and passes its tests**, with the prefix `W04: `. Never end your turn with uncommitted work.
- **Do not push.** Never push or merge `feature/*` or `main`. Leave the worktree clean on your branch.
- **Run everything in the foreground and finish in one turn.** No background commands, monitors or wait loops. Another agent builds in another worktree at the same time; `scripts/limited.sh` shares the machine, so a heavy command may wait for a slot.

## The rules, environment, resources and gate
Read `/home/user/Dev/Vocaterra/.claude/briefs/w07-station-view.md`, the sections **Output and context**, **Resources**, **Environment**, **Rules** and **Gate**: they apply to you unchanged, with `/tmp/claude-1000/w04/` for your logs and session folders, about 8 GB of disk free (build once; do not copy the repository), and the count given in your prompt as the count before you. A schema edit under `content/schema/` is allowed (it is outside the bundle's hash; check with `scripts/content.sh validate`); `content/game-v1` is not edited, so `scripts/test.sh full` is your full run. A timing test (`GeneratedLexiconTests.LoadingTheBundleIsFastEnough`) can fail while another agent's suite runs beside yours; if it is the only failure, rerun it alone and report both.

## The specification
`docs/game/activities-plan.md` section 6, paragraph **W04** (`sed -n 238,247p`). That paragraph is the milestone: in `host-events.json`, `sell` and `buy` may take `"slot": "item"` and `"prices": { "pan": 2, "harina": 1 }` instead of one `item` and `price`; the code (`Npc/HostEventTable.cs`, `VoxelSession.ApplyHostEvents`, and the worker's check in `NpcService` where `cant_pay` is decided) reads the verdict's `item` slot, finds its price, and sells (or buys) `qty` of it; an item not on the list raises `<npc>.dont_sell` as a host turn (the agent's line «No vendo …»), as `wont_buy` does for `buy`. Where the paragraph and the code disagree on a name, a file or the schema version, **the code wins; say so in the report**. Things to check in the code first:
- How `instead` (V21b, used by W10 for flour at the stall: `NpcTurnRules.AnswerMisses`) and the existing misses (`WontSell`, `NoneAsked`, `CantPay`, `WontBuy`, `CantSell`, `NoneOffered`, `NoRoom`, `NpcEffect.GiftMiss`) are decided, so a priced entry's misses follow the same path and order. If an existing miss already covers "an item not on the list", use it and say so rather than adding a duplicate.
- How the item word in the slot is turned into an item (the noun catalogue, `_nouns`), plural and gender included; how `qty` is read and defaulted today.
- Whether `<npc>.dont_sell` needs an agent state or line in the bundle for the host turn to have something to say. **This milestone adds no Spanish line and no bundle data**: for the tests, use a test bundle or test data (as the existing `HostEventTable` and `NpcService` tests do). If live use would need a line, say so in the report; the bundle's first price list is a later content milestone.
- The validator (`GameDataValidator`): a priced entry names a slot the prompt's intents can fill, every listed item is a known noun, prices are positive integers, and an entry has either `item`/`price` or `slot`/`prices`, never both.
`docs/process/code-guidelines.md` for the style; `Vocaterra.Game.Core` references no package.

## What to build (each committed)
1. The schema and `HostEventTable`: the priced form beside the fixed one, with the validator's rules and a test per rejection.
2. The sale and the purchase by name: `NpcService`'s check and `VoxelSession.ApplyHostEvents` read the item slot and the price, `qty` of it, and the miss for an unlisted item; the bag check before anything changes hands stays atomic.
3. Tests: two items at one NPC (each sold at its own price); `qty` of two; too few coins (`cant_pay`); an unlisted item (`dont_sell`, nothing changes hands); a priced `buy` (two items, an unlisted one gives `wont_buy`); a full bag; **every existing fixed sell and buy unchanged** (the existing tests pass without edits; if one must change, stop and say why).
4. **The scripted runs** (the Gate section): capture before your first edit into `/tmp/claude-1000/w04/runs-before` and after; mask the work-folder path; **every run must be unchanged**. `r27-bread`'s first capture is known to jitter by one tick (2460 or 2461) between builds: report it if seen, it is not yours.

## Known flake, not yours
The fuzz oracle O7 (`IntentResolverFuzzTests`) fails on CsCheck seed `8eu5g1sTe_A8` (a core issue, proposed as M83). If the full suite fails there and nowhere else, rerun once and report it; do not patch the core.

## Commit
Each message ends with:
```
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
```

## Report (half a page)
The branch and final SHA (local); what you built (files, types, the schema change); the validator's new rules; the misses and how they relate to the existing ones; the tests; the scripted-run comparison; the test count before and after; what live use of a price list would still need (lines, data); design versus code; anything left undone.

Before your report, make sure no process you started is still running (`pgrep -fa 'dotnet|Vocaterra|scripted-runs|limited.sh'`, kill only your own).
