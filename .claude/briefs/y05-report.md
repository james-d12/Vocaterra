# Y05, lodging: the agent's report (2026-11-11), as received

Branch `Y05-lodging`, head `6b5ee369` (two commits: `26f87130`, `6b5ee369`), worktree `.claude/worktrees/y05`. Brief:
`.claude/briefs/y05-lodging.md`.

## Built
- `grant_lodging` host-event kind: `HostEventTable.Kinds` and parse, `NpcEffectKind.GrantLodging`, a schema row in
  `content/schema/host-events.schema.json` (no schema bump). The validator needed no change.
- Applying it calls the new public `VoxelSession.GrantLodging()` (`VoxelSession.Sleep.cs`, dispatched from `ApplyTeacherEffect`):
  sets `LodgingGiven`; a second grant does nothing.
- World event `lodging_granted` in `WorldEvents` and `All` (so `TipEvents` has it); autosave reason "lodging granted".
- Item 3 needed no fix: `systems.sleep` kept `LodgingGiven` through a sleep, a load from a file and a migration.
- The locked bed's prompt: `SleepPlaces.LockedBedAt`, `VoxelSession.AtLockedBed`, `SleepOffer.AtLockedBed` and `LockedBedText`;
  locale string `ui.sleepBedLocked` = "This bed is the inn's: ask for a room first" (also in `LocaleText.cs`'s key list, or the
  validator reports an orphan). `VoxelGame.cs` shows it only at an unmet bed; E does nothing there (the binding is gated on
  `FireOffersSleep || FireNotTired`). The fire's prompt and card are unchanged.
- Dev aid `--lodging` (needs `--dev`; in the help and `docs/game/running.md`); it calls `GrantLodging()` after the skip, so the
  event is raised.
- Beyond the brief: `SleepNow` refuses at a locked bed (otherwise the scripts' `--sleep` slept from the locked bed whenever the
  mountain fire was lit, as it is after `--skip-to-session`).
- Locale manifest refreshed (`scripts/content.sh refresh`). `content/game-v1` untouched: no new hash, no fixture change.

## Tests
- Full suite: 10,759 passed, 3 skipped, 0 failed (10,749 + 10).
- New `tests/Vocaterra.Game.Core.Tests/Session/LodgingTests.cs`, 9 tests on a table built in the test: parse and validate; the flag
  and one event for two grants (through `ApplyHostEvents` **by reflection**, the method being protected and `VoxelSession` sealed);
  the autosave reason; kept across a sleep and a file load with the last place `inn.bed`; the bed unusable before and usable after;
  a night at the bed equals one at the fire (day, sitting, needs); a death after a night at the bed wakes on free ground 2 to 5
  blocks from the bed; the prompt at a locked and an open bed; nothing offered away from a place.
- `Vocaterra.Game.Tests` `DevLaunchTests`: `--lodging` needs `--dev` and is in the usage text. No existing test changed.
- Build 0 warnings; format clean; `content.sh validate` passed.

## Run in the game
`--seed 42 --mute --needs off --dev [--lodging] --skip-to-session 2 --walk-to 278,231 --wait 1 --sleep --wait 2 --screenshot ... --save ...`
(`--walk-to` arrived at 278.86, 231.22).
- With `--lodging`: `lodging: given (--lodging)`; "session 2 ends at the fire, session 3 begins"; the save has
  `systems.sleep = {lodgingGiven: true, last: {id: "inn.bed"}}`, play session 3.
- Without: `sleep: not now (--sleep): the bed is the inn's: lodging is not given yet`; session stays 2; no `sleep` entry; the
  screenshot shows the prompt line.
- Files: `artifacts/y05/{with,without}.{png,save}` in the y05 worktree (git-ignored).

## Scripted runs
`r21-skip`, `r26-death`, `r03-npcs` before and after: same (`r03-npcs` differs in order only).

## Not run
`scripts/content-loop.sh --grammar --speech` (game-v1 untouched).

## Untrue elsewhere
- No line of CLAUDE.md.
- `LodgingGiven`'s doc comment still says "false until a later milestone sets it" (stale; left).
- The settlement plan's Y05 paragraph is now as built; the status is the orchestrator's.

## For Y06 and Y07
- Y06 needs only a row `{"npc": "<innkeeper>", "event": "<id>", "action": {"kind": "grant_lodging"}}` and a state that takes the
  event (the validator checks the NPC exists and a state takes the event).
- Y07 can use the trigger `{"event": "lodging_granted"}` with no npc or detail; `--lodging` raises it at launch.
- The death wake search takes free ground 2 to 5 blocks from the place's cell; at the inn that may land outside the guest room,
  through a wall. Tested only on the test route scene, not on game-v1's inn. Y06 or Y07 should check, or set a smaller wake
  radius per place.
- The console's sleep line says "ends at the fire" even at the bed (wording; a test may pin it; Y08).

## Known, not fixed
The scripts' `--sleep` still sleeps from anywhere while a fire is lit (the existing aid; it keeps `r21-skip` the same).
