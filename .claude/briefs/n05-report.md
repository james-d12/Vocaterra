# N05 report (Sonnet 5.5, two attempts, 2026-11-05), saved by the orchestrator

The first attempt wrote the code, tests and content edits and stopped before compiling (out of context; it left a scripted-runs baseline
capture running, which finished cleanly). The orchestrator committed its work as WIP `e256beb3`; a second agent (brief
`n05b-continue.md`) finished it. Branch `N05-hud-opening`, final commit `417c7089`, pushed, clean.

- Built: `Needs/NeedsHud.cs` (stage-line queue and sound request, icons, bars, view-effect levels, needs list rows); `GameWords.HudLines`,
  `GameDataValidator.HudLineKeys`; `PlayerNeeds.RateFactor`, `VoxelSession.RelaxedNeeds` (half rates; a faint at zero health, no chest, bag
  kept); `VoxelSession.DeathLine`; «tus cosas» on the chest's beacon; `SoundCue.NeedStage` (synthesised, Sfx, ducks under voices);
  `SaveSettings.ViewEffectsOff`/`RelaxedNeeds` (written only when true); F7 toggles the view effects. Game: `UI/Hud/NeedsHudLayout`,
  `NeedIcons`, `NeedsHudLayer`, `ViewEffectsLayer`, `DeathScreenLayer`; the inventory's needs list; `--relaxed-needs`, `--view-effects on|off`.
- Content: `hudLines` in `game/words.json` (the 14 stage lines of 9.3 and the HUD words), the `needs` tip on `need_stage`, English in
  `content/locale/en/`; N06's lines: `villager_gift_used`, `shop_gift_used` «Hoy no tengo más pan.», `guide_gift_used` «Hoy ya no tengo
  agua.», six WAVs. Grammar: 0 new findings. Goldens: 3 new identity-baseline lines; fuzz digests unmoved.
- Day 1 (`DayOneSafetyTests`): intended pace 81 s active, lowest thirst 96.5; novice 2.5x 204 s, 91.4; 3x 244 s, 89.7; every drive stays
  at stage 0, no recovery action needed. The test grants the bottle if the bag has none (the route does not shop).
- Values chosen: novice pace 2.5x; a stage line 4 s on screen; the death card 4 s; stamina bar only below its drive-cut maximum; dim edges
  from thirst stage 3; the HUD hidden under `--needs off`.
- Behaviour checks: thirst 20 shows the drop with 3 notches, dim edges and «Tengo mucha sed.» / "I'm very thirsty."; `--view-effects off`
  keeps icon and line; death logged `woke at 152,73,106 ... chest none` (empty bag); relaxed: `player_woke (faint)`, no chest; the needs list
  at text scale 100 and 150. Not done: the death card not caught in a screenshot; the stall's second bread not run in the game (tested).
- Scripted runs: the speech-file count line (639/1278 to 642/1284) in most; r25 and r26 gain `tip_shown (needs)` and the HUD in the
  screenshot; r26's save +8 bytes (the tip flag). No re-baseline.
- Tests: 9385 → 9433 passed, 3 skipped; CI's case 9391 passed, 45 skipped, 0 failed. Build, format, validate, A2 lint clean.
- Findings: `morir` («Me muero de …») is in neither the A1 nor the A2 list, and the lint does not read `hudLines` (left unwaived);
  "Luna" is the `guide` agent.
