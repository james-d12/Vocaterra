You are implementing the game milestone **W03, fail-forward as data**, of the Vocaterra repository (C#, .NET 10, a MonoGame first-person voxel game that teaches Spanish). The game is `Vocaterra.{Game.Core,Game,Host,SpeechRender}`; the language core (`Vocaterra.Nlp*`) is not yours. **The stall's patience rule (the bottle after a learner runs out of road) moves from code to the NPC file, with no change in behaviour.**

Do the work yourself; do not spawn subagents.

## Where to work, and committing
Work only in the git worktree `/home/user/Dev/Vocaterra/.claude/worktrees/w03`, already on the branch `W03-fail-forward-data` (from `feature/bread-pilot` at its tip, with W02 merged). cd there first; never edit `/home/user/Dev/Vocaterra` or any other worktree (`.claude/worktrees/adaptive`, `german` and `m80` are not yours: never touch them); do not switch branches. The worktree is new and not built: build once first.
- **Commit after each item that builds and passes its tests**, with the prefix `W03: `. Never end your turn with uncommitted work.
- **Do not push.** Never push or merge `feature/*` or `main`. Leave the worktree clean on your branch.
- **Run everything in the foreground and finish in one turn.** No background commands, monitors or wait loops.

## The rules, environment, resources and gate
Read `/home/user/Dev/Vocaterra/.claude/briefs/w07-station-view.md`, the sections **Output and context**, **Resources**, **Environment**, **Rules** and **Gate**: they apply to you unchanged, with `/tmp/claude-1000/w03/` for your logs and session folders, about 8 GB of disk free (build once; do not copy the repository), and the count W02's gate reports (given below) as the count before you. **This milestone edits `content/game-v1`** (the shopkeeper's NPC file and the NPC schema), so the Rules' last point applies: `scripts/content-loop.sh` after each content edit (run directly, never under limited.sh) and `scripts/content-loop.sh --speech --tier full` once before the final report instead of `scripts/test.sh full`; `content/README.md` for how to edit content and `docs/nlp/changing-the-core.md`'s part on the shared bundle (the goldens, the identity baseline and `save-v1.json`'s bundle hash after a re-hash). No new Spanish line: the shopkeeper's give turn and its line stay as they are.

## The specification
`docs/game/activities-plan.md` section 6, paragraph **W03** (`sed -n 229,236p`). That paragraph is the milestone. The code today: `src/Vocaterra.Game.Core/Npc/NpcService.cs`, `ShopkeeperPatience` (about line 872), the constants `StallPatience` and `StallPatienceAfterHelp` (about line 173), and the call site bound to the shopkeeper and the prompt `StallNeedPrompt` (about line 772); the host event `NpcHostEvents.ShopkeeperGive` (`"shopkeeper.give"`); the saved state `BottleGiven`, `Unproductive` and `Helped` (`NpcService.Save.cs`). **Where the paragraph and the code disagree, the code wins; say so in the report.** Known ones: the NPC files are `schemaVersion: 2`, not 3 (raise the schema version only if the schema's own rules need it, and say what you did); the event is `shopkeeper.give`, not `stall.give_water`; the rule today applies at one prompt (the stall's need prompt) and not off-prompt, so the data names the prompt it applies at (for example `"prompt"` inside `patience`), and `once` maps onto `BottleGiven`. Keep the save format compatible: an existing save loads and plays the same (`BottleGiven` keeps its meaning, or is read into its successor with a test). `docs/process/code-guidelines.md` for the style; `Vocaterra.Game.Core` references no package.

## What to build (each committed)
1. The schema's optional `patience` (`misses`, `afterHelp`, `event`, `prompt`, `once`; names per the code where it differs), its loader in `NpcRoster`, and the shopkeeper's values in `content/game-v1/game/npcs/shopkeeper.json` (4, 2, `shopkeeper.give`, the stall's need prompt, once). Content loop, re-hash, fixtures.
2. `NpcService` reads the rule from the roster for any NPC at the named prompt; the constants and the shopkeeper-only branch go. `GameDataValidator`: the event exists in `host-events.json` for that NPC, the prompt is one of that NPC's prompts, the numbers are positive and `afterHelp` is at most `misses`; a test per rejection.
3. Tests: the stall's existing tests pass **unchanged** (do not edit them; if one must change, stop and say why in the report); a test NPC with a rule (a test bundle or roster, not `content/game-v1`) fails forward the same way, with and without help, and only once when `once` is true; an NPC with no rule never fails forward; a save from before W03 loads and keeps the bottle given.
4. **The scripted runs** (the Gate section): capture before your first edit into `/tmp/claude-1000/w03/runs-before` and after, compared; **every run must be unchanged** apart from the bundle hash or the speech count if the re-hash moves them. Any other difference is a bug to fix, not to explain away. Mask the work-folder path when you compare. The run `r27-bread` once took its first capture one tick late (2461 instead of 2460) on W02's baseline; if you see that, rerun it once and report both.

## Known flake, not yours
The fuzz oracle O7 (`IntentResolverFuzzTests`) fails on CsCheck seed `8eu5g1sTe_A8` on the feature branch before you (a core issue; the orchestrator plans it). If the full suite fails there and nowhere else, rerun the suite once; report it, do not patch the core.

## Commit
Each message ends with:
```
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
```

## Report (half a page)
The branch and final SHA (local); what you built (files, types, the schema change); the shopkeeper's rule as data; the validator's new rules; the tests; the scripted-run comparison; the goldens and fixtures re-recorded and why; the test count before and after; design versus code; anything left undone.

Before your report, make sure no process you started is still running (`pgrep -fa 'dotnet|Vocaterra|scripted-runs|content-loop'`, kill only your own).
