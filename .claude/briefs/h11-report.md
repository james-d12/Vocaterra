H11 report (Sonnet 5.5 medium, 2026-11-08). Branch H11-water-reconcile at 965688af, worktree .claude/worktrees/h11 clean, not pushed.
Commits: 153d0c2a (items 1-4), 5a223cca (walker tests), 965688af (architecture note).

Checks: build -warnaserror clean, format clean, content.sh validate clean; content-loop --speech --tier full 10393 passed, 0 failed,
3 skipped (before 10368); goldens and digests unmoved.

1. Drowning: NeedsHealth (IPlayerHealth over PlayerNeeds), built lazily in VoxelSession.Water; PlayerNeeds.Hurt and RecoveryHeld (no
   recovery while air is 0); DieIfDead after drowning damage, death detail player_drowned; locale ui.diedDrowned / ui.faintedDrowned
   ("You ran out of air. Swim up to breathe next time. ..."), LocaleText keys, content/locale/manifest.json refreshed; content/game-v1
   untouched. Needs off or paused: no damage (as hunger and thirst). speedPermille in the swim step: already done by the merge.
2. Bubbles: BreathHudLayout.RaiseAbove lifts the row above the needs stage line or block where they overlap (only the stage line, in
   "beside" mode); unchanged elsewhere; layout test over every canvas and scale.
3. Blur: F8 panel row 4 (Off/Low/Medium/High), optional SaveSettings.UnderwaterBlur (Low writes no field); --underwater-blur overrides
   for a run (LaunchOptions.UnderwaterBlur nullable); UnderwaterBlurSettings.Effective forces Off under ViewEffectsOff. Save version 7.
4. Docs: saves.md "Format version 7"; architecture.md version 7 plus a drowning note. "wadable" left only in water-plan.md line 46 and
   implementation-plan.md line 711.
5. --walk-to 212,262: reproduced (gave up at 212.30,263.30 after 14400 ticks). Cause is the map: the sand bank is 2 blocks above a
   1-block-deep shelf, too shallow to swim; a wading jump rises about 1.3 blocks and the hop-out needs swimming, so no body can climb
   out there (the player gets in by dropping off the cliff). A climbable bank (pond 244,247 to 244,253) works unchanged. Walker and
   hop-out unchanged. Not checked on water's pre-merge branch (terrain and code identical). Open design question for the owner.
6. Tests: Water/DrowningThroughNeedsTests, Save/UnderwaterBlurSettingTests, a NeedsHudLayoutTests case,
   Scripted/WalkerLeavesTheLakeTests; NeedsRateTests and UnderwaterBlurLaunchOptionsTests updated.
7. Scripted runs: baseline /tmp/claude-1000/h11/runs-before taken after the first edit via a stash (r04-r30 re-run on the baseline
   binary with the old ui.json; r01-r03 before the edit); after /tmp/claude-1000/h11/runs-after; compare-runs: all 27 same.
   Lake run /tmp/claude-1000/h11/lakeA/ (a3-dive: bubbles beside needs bar and hotbar); death run /tmp/claude-1000/h11/lakeB/
   (b3-death.png, the drowning hint). Log: player_died detail player_drowned, then player_woke (health 50, needs 50, stamina 100).
   Minor: "your things wait where you fell" shows with an empty bag too, as for other deaths.

To record: the six water decisions (water plan section 6) in decisions.md; the two "wadable" lines; water plan 4.10 and H11 row done;
the cliff at 212,262 as an open design question.
