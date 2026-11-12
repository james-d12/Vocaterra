# Brief: E04, weather in Core

Sent 2026-11-10 to a general-purpose agent, Sonnet 5.5 at medium effort. Worktree `/home/james/Dev/Vocaterra/.claude/worktrees/e04`,
branch `E04-weather-core`, cut from `feature/weather` after E01's gate. The shared part is `.claude/briefs/e-series-standard.md`.

```
You are implementing E04, "weather in Core": the weather as saved, deterministic world state, with its kinds as rows of data. Nothing is
drawn or heard yet (E05, E06). Your work is in src/Vocaterra.Game.Core, tests/Vocaterra.Game.Core.Tests, content/game-v1 and
content/schema. The design is docs/game/weather-plan.md sections 3.1 and 3.2; where this brief is more specific, the brief wins.

E02 (drawing the day and night) runs beside you in src/Vocaterra.Game: do not edit that project beyond what compiling requires (a line,
named in your report).

Read (the sections named): src/Vocaterra.Game.Core/Atmosphere/ (all of E01's files: DayData, DayValidator, DayClock, DayClockState);
Simulation/VoxelSimulation.Atmosphere.cs and how `VoxelSimulation.Create(scene, day)` and `Step` use it; Simulation/VoxelSimulationState.cs;
Mathematics/WorldRng.cs (`For` and `Mix`); Session/GuideSession.cs (WorldSeed, near line 315) and Session/VoxelSession.cs (where the
simulation is created, and where simulation events become world events: the `DayPhaseChanged` case); Session/VoxelSession.Night.cs;
Event/SimulationEvent.cs and Event/WorldEvent.cs; Save/SaveDocument.cs and Save/SaveGame.cs (the `systems.atmosphere` entry E01 added);
Content/GameData.cs and GameDataValidator.cs; content/README.md ("The edit loop", "Changing a format", the table of game files, "A
trigger"); docs/game/saves.md; E01's tests (tests/Vocaterra.Game.Core.Tests/Atmosphere, Save/AtmosphereSaveTests.cs,
Simulation/AtmosphereSimulationTests.cs, Session/DayClockSessionTests.cs) as the models for yours.

The principle (section 3.1): a weather kind is a row of data, not a class and not an enum. Each row sets numeric channels; Core blends
the channels of the previous and the current kind into one snapshot per tick; every later consumer reads the snapshot and never a kind's
id. No file under src/ may name a kind's id (`sunny`, `cloudy`, `rain`) except the default data in point 1.

Deliverables:

1. In src/Vocaterra.Game.Core/Atmosphere/:
   - `WeatherChannels`: `CloudCover`, `Sunlight`, `Fog`, `Wind` (each 0 to 1), `PrecipitationKind` (a string id, empty for none) and
     `PrecipitationIntensity` (0 to 1), with a `Blend(a, b, t)`. The precipitation's kind is the one being blended towards while its
     intensity is above zero (rain fading out keeps the kind `rain` until it reaches zero).
   - `WeatherKind`: an id (lowercase kebab-case), its channels, `minSeconds` and `maxSeconds` of a spell, `next` (weighted ids), a
     `morningWeight`, and an optional `say` object with `day` and `night` strings (dialogue keys E07 will fill and validate; you only
     carry them).
   - `WeatherData`: loads content/game-v1/game/weather.json after the DayData pattern: `kinds`, `start` (the kind of a new game) and
     `transitionSeconds`. `WeatherData.Default` is one neutral kind for a simulation built without game data. A validator beside
     DayValidator: ids unique and well formed; channels within 0 to 1; a precipitation kind with intensity above zero, and none without;
     spells positive with min not above max; every `next` id known, weights positive, at least one `next` unless the kind is the only
     one; `start` known; at least one positive `morningWeight`; `transitionSeconds` not negative and not above the shortest spell.
   - First data, three kinds (the owner's first version). `sunny`: cloud cover 0.42 (today's clouds), sunlight 1, fog 0, wind 0.2, no
     precipitation, spells of 240 to 600 s, next `cloudy`, morning weight 6. `cloudy`: cloud cover 0.9, sunlight 0.7, fog 0.25, wind
     0.4, no precipitation, 180 to 420 s, next `sunny` weight 3 and `rain` weight 2, morning weight 3. `rain`: cloud cover 1, sunlight
     0.5, fog 0.5, wind 0.6, precipitation `rain` at 0.8, 120 to 300 s, next `cloudy` weight 3 and `sunny` weight 1, morning weight 1.
     `start` is `sunny`, `transitionSeconds` 45. Leave `say` out of the data until E07.
   - `WeatherSystem`: the state is the current and the previous kind, the spell's start and end tick, a spell counter and a held flag.
     When the tick reaches the spell's end it draws the next kind by the current kind's weights and the new spell's length from its
     range, both from `WorldRng` keyed by the world seed, a fixed pass name and the spell counter (no other source of randomness), and
     the new spell starts at that tick (a game restored long after a spell's end changes once, not once per tick). `BlendAt(tick)` is 0
     to 1 across `transitionSeconds` from the spell's start. `RollMorning(tick)` draws by the morning weights (its own pass name and
     counter) and starts that kind with no transition (a sleep is a cut). `Force(id, tick)` holds a kind until `Release(tick)`, for tests
     and screenshots (E05 adds the flag and the menu row). Unknown ids throw.
   - `AtmosphereSnapshot` (a readonly record struct) and `AtmosphereModel.At(clock, weather, tick)`: the hour, the phase, the daylight,
     the blended channels, the current kind's id and the kind's `say` key for the phase (the night's when the phase is Night, else the
     day's; null when the data has none). Name the snapshot AtmosphereSnapshot, not Atmosphere: a type named like its namespace is
     trouble.
2. Simulation/VoxelSimulation.Atmosphere.cs: the simulation owns the weather beside the clock, is created with the bundle's WeatherData
   and the world seed (the session passes both; a simulation built without them uses the default data and seed 0), steps it from
   `StepAtmosphere`, exposes `Atmosphere` (the snapshot at the current tick) and the host actions (`ForceWeather`, `ReleaseWeather`).
   `WakeUp()` (the one call a night and a collapse make, and the one SleepTests makes on its loaded game) now also rolls the morning's
   weather, so a sleep still equals a load plus the night. A simulation event for each change of kind (a spell's end, a morning roll, a
   force or release that changes the kind), once per change; a load raises none.
3. The world event `weather_changed`, its detail the new kind's id, raised by the session like `day_phase`, named in Event/WorldEvent.cs
   and listed where content/README.md lists the events a trigger may name.
4. The digest and the save: the weather's state joins the clock's in the simulation's state and in `systems.atmosphere`. In the initial
   state (the start kind, its first spell, never rolled or held) it adds nothing to the digest and is not written, so a new game's
   digest and both save fixtures are as they were until the first change of weather; from then on it is part of both. A save without
   the entry restores to the initial state. Pin the unchanged default digest with a test. If an existing test, golden or scripted-run
   record pins a digest or an event log of a run longer than the first spell (at least 240 s of ticks), it will move: find out which do
   (the full suite tells), record them again deliberately where that is the documented way, and name each in the report. Do not bump
   the save version unless it cannot be avoided; if so, stop and report why first.
5. Content: content/game-v1/game/weather.json, content/schema/weather.schema.json, registration in GameData and GameDataValidator, the
   provenance entry, the row in content/README.md's table of game files. Then scripts/content-loop.sh (`--record` only if goldens
   moved, and say which; `--tier full` once before the report).
6. Documents: the Atmosphere section of src/Vocaterra.Game.Core/README.md (the weather, the channels and the rule that consumers read
   the snapshot); the save entry in docs/game/saves.md.

Tests (tests/Vocaterra.Game.Core.Tests, mirroring the source): the blend (ends, middle, the precipitation rule); the parse and each
validator rule; a spell ends at its tick and the next kind and length come only from the seed and the counter (two systems, same seed,
same sequence; another seed, another sequence); over many spells and seeds every kind of the first data occurs, each spell's length is
within its range, and a kind never follows one whose `next` does not list it (so `rain` never follows `sunny` directly); the morning roll
follows the morning weights and cuts without a transition; force and release; the snapshot in the middle of a transition; the event once
per change; the digest unchanged in the initial state and changed after the first change; a save taken in the middle of a transition
restores the same snapshot and continues to the same next kinds (after Save/AtmosphereSaveTests.cs); a sleep and a collapse roll the
morning. And the test the series rests on: a fourth kind (for example a fog with its own channel values) defined only in the test's
data runs through the system and the snapshot with no code naming it, together with a test that scans src/ for the first data's ids
outside the default data (after the model of Content/NoLiteralTextTests.cs or ArchitectureRulesTests.cs).

Out of scope: any drawing, sky, light, cloud, rain or sound (E05, E06); the `--weather` flag, the menu row, the F3 line and scripted
steps (E05); the NPCs' answer and any dialogue or speech (E07); any gameplay effect of weather.
```
