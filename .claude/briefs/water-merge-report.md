Water merge report (Sonnet 5.5 high, 2026-11-08). Branch water-merge (local) at d162dd6e, worktree .claude/worktrees/t9 clean.

Commits on 45499631: 1d0132a2 the merge of 22caa388; cb1787fe hotbar Gap/Bottom public (BreathHudLayout needs them); b3525556 save-v1/v4
fixtures keep our phrase rows and bundle hash with water's stateDigest; 17ea9a34 drinking by hand aims with InteractTarget (Target looks
through water since H05; 3 EatDrinkSprintTests had failed); d162dd6e NeedsSaveTests pins version 7.

Conflicts: 19 (also SaveGameTests.cs, xunit.runner.json). limited.sh keeps both (slots, slice, peak print; named scope, OOM result read ->
137), tested by hand. PlayerBody.Step(grid, x, z, jump, RiverCurrents? currents = null, int speedPermille = 1000), speedPermille passed to
StepInWater. Session calls StepNeeds and StepWaterEvents. Scene(propCells, currents). SoundCue: NeedStage, WaterSplash, UnderwaterBubbles.
Save version 7: Systems, MetNpcs, Air, Flow; no v6-shape migration. LaunchOptions both sets. LayerStack: NeedsHud then BreathHud.
xunit.runner.json kept 3 threads (ours, T7; water's H08a set 4). Not edited: docs/game/saves.md, architecture.md and plans still say version 6.

Checks: build -warnaserror clean, format clean; full suite 10328 passed, 0 failed, 3 skipped, 7 assemblies (peak 6.0 GB, at the cap, no
kill); validate and A2 lint clean. Scripted runs before/after: differences only from water (digests, hostStateHash, save version,
face/vertex counts, pixels: waterfall rebuilt, animated water, target through water); r27 tick 2460 both. Lake run: wade, swim, dive,
breath, bubbles above the hotbar beside the needs bars, hop out; drowning does not hurt (H11). `--walk-to 212,262` back to the bank gave up
0.8 blocks short after 14400 ticks with 375 jump cues (not compared with a water-only build). Logs /tmp/claude-1000/water-merge/.
H11: nothing built.
