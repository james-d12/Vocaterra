# Brief: E01, the clock in Core

Sent 2026-11-10 to a general-purpose agent, Sonnet 5.5 at medium effort. Worktree `/home/james/Dev/Vocaterra/.claude/worktrees/e01`,
branch `E01-day-clock`, cut from `feature/weather` at `848a9778`. The standard blocks (where to work, resources, environment, rules,
gate, commit trailers) are the template's in `docs/process/milestone-briefs.md`; what follows is the milestone's own part, as sent.

```
Read first: CLAUDE.md; docs/game/weather-plan.md (all of it is short; your design is sections 3.2 and 3.3, your row is E01 in section 4);
docs/game/implementation-plan.md sections 3 and 6; docs/process/code-guidelines.md; docs/game/architecture.md lines 95-105 and 362-376;
content/README.md ("The edit loop" and "Changing a format"); docs/game/saves.md. Then the code you follow or change:
src/Vocaterra.Game.Core/Simulation/VoxelSimulation.cs (Step, around lines 160-200, and ComputeStateDigest, around 359-400),
Simulation/VoxelSimulation.Water.cs (the pattern for a system as a partial class, and AppendFlowDigest's rule), Event/SimulationEvent.cs,
Event/WorldEvent.cs, Session/VoxelSession.Night.cs, Session/VoxelSession.Water.cs, Session/VoxelSession.cs (LogWorld near line 888, how
simulation events become world events), Needs/NeedsData.cs (the loader pattern, lines 200-214), Content/GameData.cs (lines 34-60) and
Content/GameDataValidator.cs, Save/SaveDocument.cs (the `systems` map near line 445 and the optional fields), Save/SaveGame.cs (Sleep,
lines 345-373, and the digest check near 302).

Rules beyond the template: do not edit docs/game/weather-plan.md, docs/open-items.md or docs/handovers/. No engine type, no
System.Random, no clock in Vocaterra.Game.Core (ArchitectureRulesTests). One type per file, folders as namespaces, Given_When_Then test
names in folders that mirror the source. No migration code for old saves, but the existing save fixtures must keep loading.

The design to build (the plan's sections 3.2 and 3.3; where this brief is more specific, the brief wins):

1. A new folder src/Vocaterra.Game.Core/Atmosphere/ (namespace Vocaterra.Game.Core.Atmosphere; `Environment` would shadow
   System.Environment) with:
   - `DayPhase`: Dawn, Day, Dusk, Night.
   - `DayData`: loads content/game-v1/game/day.json after the NeedsData pattern. Fields: `cycleSeconds`, the hours on a 24-hour dial at
     which each phase begins (`dawnHour`, `dayHour`, `duskHour`, `nightHour`), `startHour` and `wakeHour`. First values: cycleSeconds
     2400, dawn 4, day 6, dusk 20, night 22, start 8, wake 7 (about 30 minutes from dawn to the end of dusk and 10 of night, D-game-31).
     Follow NeedsData's precedent for a bundle with no such file, and report which it is.
   - `DayClock`: the time of day as a pure function of the simulation tick plus an integer offset: position = (startTicks + tick +
     offsetTicks) mod cycleTicks, in integer ticks. It gives `Hour`, `Phase` and `Daylight` (0 through the night, rising across dawn, 1
     through the day, falling across dusk). `WakeUp(tick)` sets the offset so the hour is `wakeHour` at that tick. `Freeze(hour)` and
     `Unfreeze(tick)` hold the clock for tests and screenshots (API and tests only; E03 adds the flag and the menu row). The saved and
     digested state is the offset and the frozen hour; nothing else.
2. Simulation/VoxelSimulation.Atmosphere.cs, a new partial after VoxelSimulation.Water.cs: owns the clock, is started with the bundle's
   DayData (default values where a simulation is built without game data), and is stepped from `Step` only to notice a change of phase,
   for which it appends a simulation event. Its digest part follows AppendFlowDigest's rule: in the default state (offset 0, not frozen)
   it appends nothing, so every existing digest, fixture and golden stays as it is. Pin that with a test.
3. A world event `day_phase` whose detail is the new phase (`dawn`, `day`, `dusk`, `night`), raised by the session from the simulation
   event, named in Event/WorldEvent.cs, listed where content/README.md and the schemas list the world events a trigger may name.
4. A sleep wakes in the morning: `VoxelSession.ApplyNight` (the night and the collapse) calls WakeUp. Stored time, `Day` and `Sitting`
   are not touched. SleepTests, NightTests, LoadKeepsDayTests and NextSessionTests must pass; if one compares a sleep with a load in a
   way the offset breaks, change that one assertion as little as possible and report it.
5. The save: one optional entry for the atmosphere in the `systems` map (the offset, and the frozen hour when set), written only when it
   is not the default. Both fixtures must still load and pass the digest check. No version bump unless unavoidable; then stop and report.
6. Content: day.json, content/schema/day.schema.json, GameData and GameDataValidator (cycleSeconds positive; hours within 0 to 24; dawn,
   day, dusk and night ascending; start and wake valid), provenance, the row in content/README.md. Then scripts/content-loop.sh
   (`--record` only if goldens move; `--tier full` once before the report).
7. Documents: a section for `Atmosphere/` in src/Vocaterra.Game.Core/README.md; the new save entry in docs/game/saves.md.

Tests: the clock's arithmetic (hour and phase at chosen ticks, the wrap, Daylight's ends and middle, WakeUp from any hour, Freeze and
Unfreeze); DayData's parse and each validator rule; the phase event exactly once at each boundary; determinism (same commands, same
clock and digest; a save after a sleep restores the same hour and continues the same way, after Simulation/VoxelSaveStateTests.cs); the
digest unchanged in the default state; a sleep and a collapse both wake at `wakeHour`.

Out of scope: any drawing, sky, light or sound change (E02, E03); the `--time` flag, the menu row, the F3 line, scripted steps (E03);
anything about weather (E04). Vocaterra.Game needs no change beyond what compiling requires.

Report, one page: branch, commit SHA, worktree path; what was built; the test counts; what happens when a bundle has no day.json;
whether any existing test, golden or fixture changed and why; whether the digest and save version stayed as they were; bugs found and
fixed; known-not-fixed; files touched outside the game's area; anything left undone and why.
```
