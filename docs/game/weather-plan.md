# Day, night and weather: the E series

A plan written on 2026-11-10 at the owner's request, from a read of `main` at `fe802f29` (the branch was cut at `24170667`). Nothing was
built or run. Its goal is **a modular atmosphere**: a day and night cycle first, then weather on top of it (sunny, cloudy and rain in the
first version), built so that later weather is a row of data and not a new system.

**Status (2026-11-10):** in progress; E00 to E07 and E02a are done; only E08 (the close-out and the pull request) is left, and it is not launched. The owner's answers are in section 5 and D-game-31,
progress in section 7. It is built on `feature/weather`, cut from `main` at `24170667`, in the worktree `.claude/worktrees/weather`
(the owner, 2026-11-10: "Make sure to make a new git worktree from main"). Commit prefix `E<nn>:`. As with every feature branch, nothing
is pushed to `main`; the owner merges by pull request.

## 1. The request

> Plan out how we can introduce a modular foundational weather system to the game. I want the first version to have the basics like
> rain, cloudy, sunny. The system should allow for future weather systems and be modular.

Asked how the weather should change over time, the owner added: "I guess we need a day / night cycle implemented first, then add the
spells + new roll on waking." So the series has two phases: day and night (E01 to E03), then weather (E04 to E08).

Together with the owner's standing rule: the code follows `docs/process/code-guidelines.md` (single responsibility, components,
composition, logic in `Vocaterra.Game.Core` and drawing in `Vocaterra.Game`, Given_When_Then tests in folders that mirror the source).

## 2. What exists today (on `main`)

- **No time of day and no weather state.** `--dusk` is a static evening look and a testing switch (the menu's Dusk lighting row,
  `Menu/TestingHooks.cs`, `Menu/MenuRows.cs`); it is not saved. Core reads it only for sound (`Sound/AmbientBeds.cs`: birds stop and
  insects start).
- **The sky** (`src/Vocaterra.Game/Graphics/Sky/`): a gradient dome in the colours of `SkyPalette` (a day set and a dusk set), a fixed
  square sun in the direction `ChunkMesher.SunDirection`, and `CloudField`, flat slabs picked by a hash of a grid cell (`Cover = 42`
  percent of cells) that drift with the tick.
- **Fog and clear colour** are set in `Graphics/World/VoxelRenderer.cs` (`ClearColour`, `DryFog`, `FogEndFor`, `ApplyFog`) and blended
  with the water's colour by `SetUnderwater`.
- **Light is baked per vertex** in `Graphics/World/ChunkMesher.cs` (`Light`: the face tint, ambient occlusion, `DuskLight` and the
  lanterns' glow). `VoxelRenderer.SetDusk` releases every mesh and builds the world again. Every effect has `LightingEnabled = false`;
  there is no custom shader (`BasicEffect`, `AlphaTestEffect`, `SpriteBatch` and render targets only, so the build needs no Wine).
- **No particle system.** The nearest things are the translucent water pass and the 2D view-effect layers.
- **Sound**: ambient beds are synthesised in code (`Sound/AmbientLoops.cs`), weighted per place by `AmbientBeds.At` and cross-faded by
  `SoundMixer.SetBeds`. There is a wind bed (by height) and no rain bed.
- **Simulation**: a 60 Hz integer tick (`Simulation/VoxelSimulation.cs`); the water system is a partial class of it
  (`VoxelSimulation.Water.cs`) with its own `Step`, digest part and save fields. Core reads no clock and draws no unseeded random number
  (`ArchitectureRulesTests`); seeded numbers come from `Mathematics/WorldRng.cs`, cosmetic ones from `PresentationRng`.
- **Sleep and days**: `SaveGame.Sleep` and `VoxelSession.ApplyNight` (`Session/VoxelSession.Night.cs`). A sleep moves the day counter
  `GuideSession.Day` ("Sesión n") and runs the daily resets; it moves no stored time (`Save/SleepTests.cs`). Nothing maps stored time to
  an hour.
- **Weather words already ship** (V29): the intents `state_weather` and `ask_weather`, and every NPC answers "¿Qué tiempo hace?" with the
  fixed line "Hoy hace sol." (`<npc>_sun`; `Npc/WeatherAndWellbeingTests.cs`). The village plan called that line "the one seam if the
  game ever has weather".
- **Earlier mentions**: the settlement plan lists weather as follow-up work (its sections on shelter); the water plan rules rain out of
  its own scope. No decision or plan for a day and night cycle or a weather system existed before this one.

## 3. Design

### 3.1 The modular seam: kinds are data, consumers read channels

A weather kind is a row of data, not a class. Each row sets a few numeric **channels**. Core blends the channels of the previous and the
current kind with the time of day into one snapshot per tick, and every consumer (sky, light, fog, rain, sound, the NPC's line) reads
that snapshot. No drawing or sound code switches on a kind id.

- A new weather that reuses the channels (fog, drizzle, a storm without lightning) is a new row in `weather.json` and no code.
- A new phenomenon (snow, lightning) is one new channel or precipitation style plus one consumer.

### 3.2 Core (`src/Vocaterra.Game.Core/Atmosphere/`)

The folder is `Atmosphere`, not `Environment`, so the namespace does not shadow `System.Environment`.

| Type | Role |
|---|---|
| `DayData` | Loads `content/game-v1/game/day.json`: the cycle's length, the hours where dawn, day, dusk and night begin, the start hour and the wake hour. First values: about 30 minutes of day and 10 of night. |
| `DayClock` | The position in the cycle, driven by the tick. Gives the hour, the `DayPhase` (Dawn, Day, Dusk, Night) and a 0 to 1 daylight curve. `WakeUp()` jumps to the wake hour; `Freeze(hour)` holds it for tests and screenshots. |
| `WeatherKind`, `WeatherData` | Loads `content/game-v1/game/weather.json`: per kind the channels (`cloudCover`, `sunlight`, `fog`, `wind`, `precipitation { kind, intensity }`), the range of a spell's length, the weighted `next` kinds, a morning weight, and the dialogue key of the NPC's answer (a day and a night variant). Also the transition time. |
| `WeatherSystem` | State: the current and the previous kind, the spell's start and end tick, a spell counter. At the end of a spell it draws the next kind with `WorldRng.For(seed, "weather", counter)`. `RollMorning()` on a sleep; `Force(id)` for tests. |
| `AtmosphereSnapshot` (record struct) | The blended snapshot: hour, phase, daylight, the channels and the current kind's id. Built by `AtmosphereModel.At(clock, weather)`. |

Wiring, after the water system's pattern:

- `Simulation/VoxelSimulation.Atmosphere.cs`, a new partial like `VoxelSimulation.Water.cs`: it owns the clock and the weather, steps
  them from `Step` and adds their part to the state digest. The session passes the world seed in.
- The world events `day_phase` and `weather_changed` (the detail is the phase or the kind's id), raised through `WorldEvents` and
  `VoxelSession.LogWorld`, so tips, objectives and lesson steps can wait on them with the existing `Trigger`.
- `VoxelSession.ApplyNight` calls `WakeUp()` and `RollMorning()`. Stored time and `Day` are not touched, so `SleepTests`, `NightTests`
  and `LoadKeepsDayTests` pass unchanged.
- The save gets one optional `systems.atmosphere` entry; absent means the start values. For the digest check on load the milestone
  follows `AppendFlowDigest`'s rule (nothing added in the default state) or changes the format outright: no migration code, since the
  game is unreleased.
- The data files follow `Needs/NeedsData.cs` (`schemaVersion`, `GameData.HostText`), with schemas in `content/schema/`, registration in
  `Content/GameData.cs` and `GameDataValidator`, and provenance entries.

The start values keep today's look: the clock starts in the morning and the first spell is sunny, so the existing scripted runs and
screenshots stay in daylight.

### 3.3 The cycle and the day counter

The cycle is free-running (the owner, section 5): day and night loop on their own whether or not the player sleeps. A sleep jumps the
clock to the morning. The day counter stays what it is: it moves only on a sleep, so a long sitting can see more than one sunrise in one
"Sesión n". Sleep's own rule (a lit fire, the sleep drive below 75) does not change.

### 3.4 The sky (`src/Vocaterra.Game/Graphics/Sky/`)

- `SkyLook`, plain values with no engine type: a pure function from the snapshot to the dome's colours, the clouds' colours and cover,
  the sun's and the moon's position, the amount of stars, the fog's colour and distance, and the colour of the sun's light. Its
  keyframes for night, dawn, day and dusk reuse the day and dusk sets of `SkyPalette`; an overcast sky greys them by `cloudCover` and
  `sunlight`.
- `SkyRenderer` draws from a `SkyLook`: the dome is coloured again when the look moves, the sun follows an arc, a square moon and simple
  stars show at night, and `CloudField.Generate` takes the cover as a parameter.

### 3.5 Light without a full remesh (`Graphics/World/`)

`MeshOptions.Dusk` becomes a `LightBand` (Day, Dusk, Night) that fixes the baked sun multiplier and the lanterns' strength. A change of
band marks the meshes stale and builds them again in the background within the mesh budget, the old mesh staying until its replacement
is ready. Chunks are drawn grouped by band with `DiffuseColor = the sun's light / the band's baked light`, so every chunk shows the same
light during the swap and the light changes smoothly over the day. Weather dims the same light, so weather never causes a remesh.
`ArmLight` and the NPC figures take the same light. `VoxelRenderer.SetAtmosphere(look)` replaces `SetDusk` and must compose with
`SetUnderwater` and with the held arm's use of `_solid.DiffuseColor`. E02 starts with a short check that `DiffuseColor` tints both world
passes.

Known limit: the faces keep their fixed sun direction, so the shaded side of a block does not move with the sun.

### 3.6 Rain (`Graphics/Weather/`)

- `PrecipitationField`, with no engine type: drops in a box that wraps round the camera, from `PresentationRng`, with a cached cover
  height per column from `VoxelGrid.HighestBlock`, so no rain falls under a roof or a tree.
- `PrecipitationRenderer`: streak quads from one reused buffer (`BasicEffect`, alpha blend, depth read), drawn after the water pass and
  not under water. A style table per precipitation kind (length, speed, colour, slant by wind) leaves room for snow. The number of drops
  is a new `QualityPreset` field.

### 3.7 Sound

A synthesised `rain` bed in `AmbientLoops` and `AmbientBeds`, its weight from the precipitation's intensity and lower under cover; the
wind raised by the `wind` channel; birds by day and insects at dusk and at night. `AmbientBeds.At` takes the atmosphere where it took the
`dusk` flag. The sound rules of D-game-18 hold: beds duck under voices and are silent while the microphone records.

### 3.8 Switches

`--time <hour>` and `--weather <id>` (both hold the value), `--dusk` kept as an alias; the testing menu's Dusk row becomes a Time of day
row and a Weather row; an F3 line with the hour, the phase and the weather; scripted steps (`VoxelStepKind`, `VoxelScript`,
`LaunchOptions`) after the model of `PourWater`.

### 3.9 The NPCs' answer

`ask_weather` takes its reply pool from the weather kind's dialogue key: the existing `<npc>_sun` ("Hoy hace sol."), and new lines per
NPC for cloudy ("Está nublado."), rain ("Llueve.") and a clear night ("Hace buen tiempo."). The words are already in the intents. New
speech clips are rendered with `tools/Vocaterra.SpeechRender` for the seven NPCs. `Npc/WeatherAndWellbeingTests` changes on purpose.
The reply to the learner's own statement (`state_weather`) stays as it is.

## 4. Milestones

Models follow the owner's rule (2026-11-06): Haiku 5.5 for small tasks, Sonnet 5.5 at medium effort for medium ones, at high for large or
design-heavy ones. At most two building agents at a time. Every milestone: `dotnet build -warnaserror`, the game tests, `dotnet format
--verify-no-changes`, and tests named Given_When_Then in folders that mirror the source. `ArchitectureRulesTests` stays green.

| Id | Milestone | Size | Model | Depends on |
|---|---|---|---|---|
| E00 | **Register the series** (the orchestrator, documents only): this plan, D-game-31, the rows in `docs/README.md`, the game plan's section 4.12 and `docs/open-items.md`, the handover. | S | orchestrator | none |
| E01 | **The clock in Core:** `DayData`, `DayClock`, `DayPhase`, `day.json` and its schema, `VoxelSimulation.Atmosphere.cs`, the digest part, the save entry, the `day_phase` event, a sleep wakes in the morning. Tests: the clock's arithmetic, determinism, the save's round trip, the sleep suites unchanged. | M | Sonnet, medium | E00 |
| E02 | **Drawing the cycle:** `SkyLook`, the dome, the sun's arc, the moon, the stars, fog and clear colour, the light bands with the background remesh and the tint per band, the arm and the figures, and the `--time` flag its screenshots need. Screenshots at six hours. | L | Sonnet, high | E01 |
| E02a | **The lanterns' glow as its own layer** (added after E02's report): the glow leaves the baked vertex light and is drawn as an additive mesh with a continuous strength, so it no longer steps at a band's edge. | M | Sonnet, high | E05 (the same files) |
| E03 | **Sound and switches:** the beds by phase, the menu's Time of day and Weather rows, the F3 line, the scripted steps for the time and the weather, `--dusk` as an alias, `docs/game/running.md`. | S | Haiku | E02, E04 |
| (merge point) | Gate. Day and night can go to the owner as a pull request of its own. | | | |
| E04 | **Weather in Core:** `WeatherKind`, `WeatherData`, `weather.json` and its schema, `WeatherSystem`, the `AtmosphereSnapshot`, `weather_changed`, the morning roll, the save, the digest. One test defines a fourth kind in test data only, to show that a new kind needs no code. | M | Sonnet, medium | E01 |
| E05 | **Sky and light by weather:** the clouds' cover, the overcast colours, the fog's distance, the hidden sun; the `--weather` flag its screenshots need. | M | Sonnet, medium | E02, E04 |
| E06 | **Rain:** `PrecipitationField`, `PrecipitationRenderer`, the cover check, the quality field, the rain bed and the wind. The frame time on the low preset is measured. | M/L | Sonnet, high | E05 |
| E07 | **The NPCs' answer:** the reply pool by weather, the new lines and their speech clips, the content lint, the bundle hash, the goldens, the fixture's hash, `docs/game/speech-checklist.md`. | M | Sonnet, medium | E04 |
| E08 | **Close-out:** the architecture and README updates, a "how to add a weather kind" section in `content/README.md`, `docs/game/saves.md`, the full gate, the screenshot tour, the pull request. | S | Haiku | E03, E06, E07 |

E01, E04 and E07 edit `content/game-v1`, so each runs the content loop (`content/README.md`, "The edit loop"): the new bundle hash, the
validation, the whole suite, the goldens recorded again if they moved, and the hash in the save fixtures.

Verification at the end of the series: the full suite and the merge gate; two runs of one seed and one command list give the same
weather sequence and state digest, and a save taken in the middle of a transition continues the same way; scripted screenshot runs with
`--time` at six hours and `--weather` for each kind, one standing under a roof in the rain and one under water in the rain; an NPC asked
"¿Qué tiempo hace?" in each weather; then the owner's check in the game before the pull request.

## 5. The owner's decisions (2026-11-10)

Recorded as D-game-31.

1. **The first version's reach:** the atmosphere (state, sky, light, fog, rain, sound) and truthful NPC answers to the weather question.
   No gameplay effect yet.
2. **A day and night cycle first,** then the weather: spells that change on their own, and a new roll on waking.
3. **The cycle is free-running:** day and night loop on their own, about 30 minutes of day and 10 of night, set in data. A sleep jumps to
   the morning; the day counter still moves only on a sleep.
4. **The work is done in a new git worktree cut from `main`.**

## 6. What this plan does not do

- No gameplay effect: no wetness, cold or warmth, no rain on fires or crops, no shelter rule. The settlement plan's weather follow-ups
  stay follow-ups.
- No NPC schedules: the NPCs stand where they stand at night and in the rain.
- No seasons, snow, storms or lightning; the design leaves room for them (section 3.1).
- No shadows that move with the sun (section 3.5), and no rain that fills water.
- No truthful agreeing or disagreeing with the learner's own weather statement, no greetings by the time of day, no weather lesson.
- No custom shader (`.fx`) and no core (`Vocaterra.Nlp`) change. If E07 finds it needs a core call, that is a core milestone.

## 7. Progress

| Id | Status | Gate | Notes |
|---|---|---|---|
| E00 | done, 2026-11-10 | documents only | This plan, D-game-31 and the index rows. |
| E01 | done, 2026-11-10 | merged at `1a5a9a07`, 10764 passed, 0 failed, 3 skipped | `Atmosphere/` (`DayData`, `DayClock`, `DayPhase`), `game/day.json` (2400 s cycle; dawn 4, day 6, dusk 20, night 22; start 8, wake 7). The hour is the tick plus a saved offset, so the default state adds nothing to the digest and the save version stays 7. `day_phase` once per boundary. A sleep and a collapse wake at 7. `SleepTests.LoadOf` and `NightTests.ThePlayerWakesWhereTheyFell` changed on purpose (findings, E01). |
| E02 | done, 2026-11-10 | merged at `920233f9`, 10925 passed, 0 failed, 3 skipped | `SkyLook`, `SkyKeyframe`, `SkyArc`, the dome, sun, moon and stars from the look; `LightBand` and the tint per band, a band change rebuilt in the background (6 to 15 s, worst frame 21 ms); `--time`. The default run's camp view is pixel-identical to before. Not fixed: the lanterns' glow steps at a band's edge (E02a). Findings, E02. |
| E02a | done, 2026-11-10 | merged at `89ee19fb`, 10993 passed, 0 failed, 3 skipped | The glow is its own additive mesh with a continuous strength: no step at a band's edge, not dimmed by rain. Beside a lantern noon and dusk are about 5 percent brighter than before. Findings, E02a. |
| E03 | done, 2026-11-10 | merged at `ac7facdd`, 11030 passed, 0 failed, 3 skipped | Birds by the daylight and insects as it falls; the menu's Time of day and Weather rows; the F3 line; the `--set-time` and `--set-weather` steps. Merged with E05 by a second run (both sides' weather checks kept). Findings, E03. |
| E04 | done, 2026-11-10 | merged at `0d73dba9`, 10837 passed, 0 failed, 3 skipped | `WeatherChannels`, `WeatherKind`, `WeatherData` (`game/weather.json`: sunny, cloudy, rain; a 45 s transition), `WeatherSystem` (spells seeded by the world seed and a counter; the morning roll on `WakeUp`), `AtmosphereSnapshot`, `weather_changed`. No kind's id in `src/` outside the default data, and a fourth kind defined only in test data runs with no code. The first spell adds nothing to the digest; the save version stays 7 (findings, E04). |
| E05 | done, 2026-11-10 | merged at `c74ce223`, 10978 passed, 0 failed, 3 skipped | `SkyWeather`, the look from the snapshot, the clouds' cover with a fade, the fog's reach, the sun's light times the sunlight channel (no remesh), `--weather <id>`. The first kind's look is today's exactly (pixel-identical). Weak: a full cover is scattered slabs, not a ceiling. Findings, E05. |
| E06 | done, 2026-11-10 | merged at `09e83885`, 11089 passed, 0 failed, 3 skipped | Rain as streaks in a wrapping box, cut at each column's cover; a style table per precipitation kind; a rain bed, the wind and quieter birds. Findings, E06. |
| E07 | done, 2026-11-10 | merged at `84033e02`, 10936 passed, 0 failed, 3 skipped | The NPCs answer «¿Qué tiempo hace?» by the weather: `weatherAnswers` in an NPC's file, the snapshot's `say` key into the turn, the pool swapped in `NpcTurnRules.Compose`; no core change. 21 new pools and 42 clips (not listened to). The validator requires a line for every kind. Findings, E07. |
| E08 | todo | | |
