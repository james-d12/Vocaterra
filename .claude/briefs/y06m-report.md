# Y06m report: merge of origin/main (the weather series, PR #64) into the settlement series

(The agent's report, 2026-11-12, condensed by the orchestrator.)

Branch `Y06m-merge-main`, worktree `/home/james/Dev/Vocaterra/.claude/worktrees/y06m`; merge commit `87b9922a` (parents `9b52db30` and
`origin/main` `c7124db3`), findings entry `d15c1f0b`.

## Conflicts and their resolution
- Game.Core: `GameDataValidator` both checks; `WorldEvent` `LodgingGranted`, then `DayPhase`, `WeatherChanged`; `SaveDocument`
  `SystemsSave(Procedures, Sleep, Atmosphere)`; `SaveGame` capture writes the sleep and the atmosphere, restore passes the
  atmosphere to the simulation, then `RestoreSleep`.
- Game: `ChunkMesher` keeps `PileHeight` with main's `sunTint`; `AddBed` takes the tint (the bed follows the sky's light); doors and
  fences keep main's tint.
- Tests: `SpeechLinesTests` 393 pools, then 396 with the innkeeper's weather answers; `save-v1.json` and `save-v4.json` stay deleted
  (main's edits to them were bundle hashes only).
- Bundle: both manifests and the grammar cache rebuilt; `dialogue.json`, the agents, provenance, the speech index and the lines merged
  as a union without conflict.
- Docs, as a union: content/README, decisions (D-game-31, D-game-32), findings, the implementation plan (4.12, 4.13), saves.md, the
  speech checklist, the handovers README, the Game README; CLAUDE.md, docs/README and open-items merged cleanly.

## Where the two sides met
- Save version 8, oldest readable 8; `systems.sleep` and `systems.atmosphere` side by side; no migration; saves.md one description.
- Sleep: `SaveGame.Sleep` restores the parts (the atmosphere back), `NoteSleptAt`, then `ApplyNight`, which calls main's
  `Simulation.WakeUp()`: every sleep place, the inn's bed included, wakes in the morning. A death moves no clock and wakes at the last
  sleep place.
- The innkeeper got main's E07 `weatherAnswers: ["ask_weather"]` (main's roster tests ask it of every NPC): pools
  `innkeeper_cloudy` «Hoy está nublado.», `innkeeper_rain` «Hoy llueve.», `innkeeper_fair` «Hace buen tiempo.» (the baker's lines),
  transitions `innkeeper.sun/.cloudy/.rain/.fair` in both states, glosses, six speech files in her voice (the only files rendered).
- Fixtures: the one version 8 fixture, with no `atmosphere` (a default clock writes none). Tests changed: `AtmosphereSaveTests` and
  `WeatherSaveTests` read `save-v8.json` (renamed `TheFixtureStillLoads…`); `TheSaveVersionIsStillEight`; `AtmosphereSimulationTests`'
  two world digests re-pinned (`7f607665…`, `77c039bc…`; the route's world holds the inn).

## Bundle and goldens
Bundle hash `e4325ded…` (was `efc2dec0…`) in `save-v8.json`; speech 809 lines, 1618 files; the identity baseline gained 3 lines
(the innkeeper's weather), the end-to-end goldens did not move; content-loop --grammar --speech clean.

## Tests
With models 11,221 passed, 3 skipped; without models 11,174 passed, 50 skipped (feature/settlement after Y06: 10,797; main's last gate
E11: 11,122). Build 0 warnings, format clean, validate passes, A2 lint clean.

## Runs in the game
- `--dev --lodging --skip-to-session 2`, sleep in the inn's bed: woke at 07:01, sunny; save holds `systems.sleep` {lodgingGiven, last
  inn.bed} and `systems.atmosphere`.
- A death after it: woke at (280, 38, 231) inside the guest room; `last` still `inn.bed`.
- A sleep at Luna's fire (session 2): woke with the clock moved, `last: camp.fire`.
- Screenshots in `artifacts/y06m/` of the y06m worktree: Rosa behind her counter, the inn after waking, the village in rain, the death
  wake, the fire, the inn's door in rain, the inn at dusk.

## Scripted runs (all 28, on a build of origin/main and on the merge)
14 equal on order only (r02 to r06, r10, r11, r16 to r19, r22 to r24). The others (r01, r07 to r09, r12, r14, r15, r20, r21, r25 to
r27, r30, r31) differ only for the settlement's reasons: the scene's digest and trees (1959 → 1906), faces, speech counts, save
version 7 → 8, pictures with the inn and the moved house, world digests; r01's ambient and footstep gains and marks shifted (consistent
with the scene, not traced). `r26-death` wakes at 152, 73, 106 on both. `r10-playtest` exits 2 on both; `r20-sleep` never sleeps on
both. Nothing re-baselined.

## For Y07 to Y13
- Every sleep goes through `SaveGame.Sleep` and `ApplyNight` (which wakes the clock).
- Any new opaque mesh in `ChunkMesher` takes the `sunTint`.
- A new NPC needs `weatherAnswers`, the `<id>.sun/.cloudy/.rain/.fair` transitions, their pools and speech (main's roster tests).
- A new NPC or pool moves the counts in `SpeechLinesTests` and adds identity-baseline lines.
- `AtmosphereSimulationTests`' digest pins move with any scene or block change on the route.

## Known, not fixed
Rosa's speech (and the 6 new files) wait for the owner's listening. The fuzz flake `2zwDo8nRB_f7` did not appear here. The scripted
runs pin no hour or weather. The weather handover still says it waits for PR #64's merge (for the orchestrator).
