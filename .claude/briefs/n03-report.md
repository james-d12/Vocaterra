# N03 report (Sonnet 5.5, 2026-11-05), saved by the orchestrator

Branch `N03-sleep-collapse`, final commit `861c5698`, on feature/adaptive_design at c521c745.

- Night: `Session/VoxelSession.Night.cs`, `SaveGame.Sleep(session, document, collapse = false)`; `ApplyNight` on every night path (E at the fire,
  Enter on the card, `--sleep`) after the restore; `--needs off` leaves the meters.
- Collapse: `SaveGame.Sleep(..., collapse: true)`, `VoxelSession.CollapseDue`, `VoxelGame.DriveCollapse`/`PassNight` (the game triggers it: a save
  capture needs the scene); 60 s active at sleep stage 4, 1.5 s fade out, the night where the player stands, 3 s fade in (`FadeLayer`,
  `FrameView.FadeAlpha`); never in `--playtest`. The collapse is the sleep drive's only (N01's `CollapseDue`, design 9.5); scripted:
  `--set-need sleep 0 --wait 62`. `PlayerNeeds.CollapseDue` tolerates 1e-6 s (tick sums).
- Events: `player_collapsed (sueño)`, `player_woke (collapse)`; a plain night raises `slept`. Tip `collapse` (game/tips.json; English
  `tip.collapse`); no Spanish.
- Day's resets seam: `RunDayResets`, `DayResetCount`, once per night and collapse, never on a load.
- The fire-offer rule unchanged (`SleepOffer`, `VoxelSession.Sleep.cs` `CanSleep`, `RestDue`): L01 changes it there.
- Tests: `Needs/NightTests.cs` (22); `SleepTests.LoadOf` applies the night rule; `NeedsSaveTests` renamed test. 9195 -> 9219 passed; CI's case 9177, 45 skipped.
- Checks (`--skip-to-session 2`): night with sleep 40, hunger 30, health 70 -> hunger 14.9, sleep 100, health 90.1, day 3; collapse at 200,200 ->
  events above, position kept, sleep 49.9, stamina full, health unchanged; a load: sitting 4, digest equal. Scripted runs unchanged.
