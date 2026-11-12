# Brief: E03, sound and switches

Sent 2026-11-10 to a general-purpose agent, Haiku 5.5. Worktree `/home/james/Dev/Vocaterra/.claude/worktrees/e03`, branch
`E03-sound-and-switches`, cut from `feature/weather` after E07's gate. The shared part is `.claude/briefs/e-series-standard.md`.

```
You are implementing E03, "sound and switches": the ambient sound follows the time of day, and the time of day and the weather get
their testing switches (menu rows, an F3 line, scripted steps). Small, mechanical work in several places; nothing new is designed.
Your work is in src/Vocaterra.Game.Core (Sound/, Menu/, Scripted/), src/Vocaterra.Game (the hooks in VoxelGame.cs, the F3 text, the
scripted-step flags in Startup/LaunchOptions.cs) and their tests. The plan is docs/game/weather-plan.md sections 3.7 and 3.8.

What exists (read these first): src/Vocaterra.Game.Core/README.md's Atmosphere section; `Simulation.Atmosphere` (an
AtmosphereSnapshot: Hour, Phase, Daylight 0 to 1, the weather's channels, the current kind's id), `Simulation.Clock`
(`IsFrozen`), `Simulation.FreezeClock(hour)`, `UnfreezeClock()`, `Simulation.Weather` (`Data.Kinds`, `IsHeld`, `Current`,
`Previous`, `BlendAt(tick)`), `ForceWeather(id)`, `ReleaseWeather()`; in the game, E02's `--time <hour>` and what `--dusk` and the
menu's Dusk row do today (they hold the clock at hour 21: VoxelGame.cs, search for `SetDusk` and `DuskHour`).

Another agent (E05) is working in src/Vocaterra.Game/Graphics and adds a `--weather <id>` launch flag in Startup/LaunchOptions.cs and
a few lines where VoxelGame.cs builds the sky's look. Do not edit Graphics/, do not add `--weather`, and keep your edits in
VoxelGame.cs and LaunchOptions.cs small and local to the places named below, so the two branches merge cleanly.

No file under src/ may name a weather kind's id (tests/Vocaterra.Game.Core.Tests/Architecture/NoWeatherKindIdsInSourceTests.cs): the
weather row and the scripted step take whatever ids the loaded data has.

Read (the sections named): Sound/AmbientBeds.cs (`At`, near lines 241-310: the `dusk` parameter, the birds and the insects),
Sound/AmbientSound.cs (its `Dusk`, lines 30-39), Sound/SoundFrontend.cs (`SetDusk`, near 95-101), Sound/SoundTickObserver.cs;
Menu/TestingHooks.cs (lines 54-58), Menu/MenuRows.cs (the `dusk` switch near line 85, and how a Slider with an Auto position and a
Picker over names are made: the draw distance and the teleport rows), Menu/MenuText.cs (`testDusk`) and where its English text lives;
src/Vocaterra.Game/VoxelGame.cs (the testing hooks near 1050-1075), UI/Hud/VoxelDebugText.cs; Scripted/VoxelStepKind.cs,
VoxelStep.cs, VoxelScript.cs (the switch near 118-163) and Simulation/VoxelSimulation.Water.cs (`PourWater`, the model of a host
action a step calls), Startup/LaunchOptions.cs (the `--pour` case near 1134 and the flag list near 597); the tests beside each
(Menu/TestingRowsTests.cs, Sound tests of AmbientBeds, Scripted tests, tests/Vocaterra.Game.Tests/Startup).

Deliverables:

1. Sound by the time of day. `AmbientBeds.At` takes the atmosphere's snapshot where it took the `dusk` flag: the birds' weight is
   multiplied by the daylight, the insects' by one minus the daylight (so birds by day, insects at dusk and through the night, a
   smooth change across dawn and dusk). Nothing else in `At` changes, and no weather channel is used yet (E06 adds the rain bed and
   the wind). `AmbientSound`'s `Dusk` and `SoundFrontend.SetDusk` go; the per-tick path reads the simulation's snapshot. The sound's
   rules stay: nothing in Sound writes to the simulation.
2. The menu's testing rows. The Dusk lighting row becomes two rows: "Time of day", a slider with a first "Running" position and then
   the hours 0 to 23 (a held hour is `FreezeClock`, Running is `UnfreezeClock`), and "Weather", a picker with a first "Running"
   position and then the kinds of the loaded data by their ids (`ForceWeather`, `ReleaseWeather`). TestingHooks gets the four hooks
   (get and set for each) in place of `Dusk` and `SetDusk`; VoxelGame.cs wires them. Each row shows the state the simulation has (a
   clock held by `--time` shows its hour).
3. The F3 text gains one line: the hour as hh:mm, the phase, "held" when the clock is held; the weather's current kind, and during a
   transition the previous kind and the blend as a percent, "held" when held.
4. Scripted steps, after the model of `--pour`: `--set-time <hour>` (0 to 24) holds the clock from that step on and `--set-time run`
   releases it; `--set-weather <id>` holds a kind and `--set-weather run` releases it. An unknown id or a bad hour is an error at
   launch where the data is known, otherwise when the step runs, with a message that names the valid values.
5. `--dusk` stays as it is (it holds hour 21); remove what only the old flag needed (the sound option that carried it, the menu text
   key) and nothing else.
6. Tests, mirroring the source: the beds at noon, at midnight and half-way through dusk (birds, insects), and unchanged weights for
   every other bed; the two rows (their positions, what each sets, the shown state); the F3 line's text in four states; each scripted
   step and its errors; the menu text keys present in the locale.
7. Documents: docs/game/running.md (the menu's rows, the F3 line, the steps), docs/game/sound-and-display.md (birds and insects by the
   time of day), docs/game/scripted-runs.md if it lists the step flags, and the Menu, Sound and Scripted sections of
   src/Vocaterra.Game.Core/README.md.

Out of scope: any drawing; the `--weather` launch flag (E05); the rain bed and the wind (E06); the NPCs' lines; content/game-v1.
```
