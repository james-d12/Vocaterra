# Brief: E05, sky and light by weather

Sent 2026-11-10 to a general-purpose agent, Sonnet 5.5 at medium effort. Worktree `/home/james/Dev/Vocaterra/.claude/worktrees/e05`,
branch `E05-sky-by-weather`, cut from `feature/weather` after E02's gate. The shared part is `.claude/briefs/e-series-standard.md`.

```
You are implementing E05, "sky and light by weather": the sky, the clouds, the fog and the world's light follow the weather that E04 put
in Core, on top of the day and night that E02 drew. No rain is drawn and nothing is heard yet (E06). Your work is in
src/Vocaterra.Game (Graphics/, a few lines of VoxelGame.cs and Startup/LaunchOptions.cs) and tests/Vocaterra.Game.Tests. The design is
docs/game/weather-plan.md sections 3.1, 3.4 and 3.5; where this brief is more specific, the brief wins.

What exists (read these first): src/Vocaterra.Game/README.md, the sections "The day cycle", the sky and the light (E02);
Graphics/Sky/SkyLookInput.cs, SkyLook.cs, SkyKeyframe.cs, SkyArc.cs, SkyRenderer.cs and SkyModel.cs (CloudField, `Cover = 42`);
Graphics/World/LightBands.cs and VoxelRenderer.cs (SetAtmosphere, the fog: FogStartFraction, FogEndFor, DryFog, ApplyFog, SetUnderwater,
WaterLight, and where a change of view distance recomputes the fog); VoxelGame.cs where the SkyLook is built from `_sim.Clock` each
frame; Startup/LaunchOptions.cs (`--time`, E02's model for your flag) and tests/Vocaterra.Game.Tests/Startup/TimeLaunchOptionsTests.cs;
in Core, read only: src/Vocaterra.Game.Core/README.md's Atmosphere section, Atmosphere/AtmosphereSnapshot.cs, WeatherChannels.cs, and
`Simulation.Atmosphere`, `ForceWeather(id)`, `ReleaseWeather()`; content/game-v1/game/weather.json (three kinds; the first's cloud cover
is 0.42, today's clouds). E02's tests in tests/Vocaterra.Game.Tests/Graphics/Sky and Graphics/World are the models for yours.

The rule the series rests on: consumers read the snapshot's channels (`CloudCover`, `Sunlight`, `Fog`, `Wind`, the precipitation's kind
and intensity) and never a weather kind's id. No file under src/ may name a kind's id
(tests/Vocaterra.Game.Core.Tests/Architecture/NoWeatherKindIdsInSourceTests.cs scans for them); your `--weather` flag takes whatever
ids the loaded bundle has.

Other work beside you: E07 edits Core's Npc folder and the content bundle; E03 (sound by phase, the menu's rows, the F3 line and the
scripted steps for time and weather) will touch VoxelGame.cs's testing hooks and LaunchOptions.cs's scripted-step cases. Keep your
edits in those two files small and local (the SkyLook's input, one flag), and do not edit src/Vocaterra.Game.Core or content/.

Deliverables:

1. The input. `SkyLookInput` gains the cloud cover, the sunlight and the fog (0 to 1 each), and VoxelGame builds it from
   `Simulation.Atmosphere` (the snapshot) instead of from the clock alone. With the first kind's values (cloud cover 0.42, sunlight 1,
   fog 0) every value of the look is exactly what E02 gives today: pin that with a test, and with a screenshot that is pixel-identical
   to the build before your change at the same tick.
2. The look by weather, all of it continuous in the channels, so the 45-second blend between two kinds shows no step:
   - the dome greys and darkens as the cloud cover rises above the fair-weather value and the sunlight falls, by day and at night
     (an overcast night is darker and has no blue glow), and dawn and dusk lose their amber under a full cover;
   - the sun's disc and halo fade out as the cover closes; the moon and the stars fade the same way;
   - the clouds' three colours darken with the sunlight (grey undersides under a heavy sky);
   - the colour of the sun's light on the world is multiplied by the sunlight channel, through E02's tint, so weather never rebuilds
     a mesh and never changes the band (the band still follows the daylight alone). Check that the tint stays within the range E02
     chose its band edges for, with the lowest sunlight of the data, and say what the range is;
   - the fog's colour stays the horizon's (so it greys with the sky), and the fog comes nearer as the fog channel rises: at 0 the
     distances are today's, at 1 the fog is complete at about a third of the view distance, with the start moved in proportion. It
     composes with the view-distance setting, the quality preset and `SetUnderwater` as today's fog does.
3. The clouds' cover. `CloudField.Generate` takes the cover as a parameter. At 42 percent the boxes are exactly today's (test). A
   higher cover only adds clouds and never moves or removes one that is there (the hash threshold is monotonic; test), and a cloud
   whose cell is near the threshold fades in and out by alpha as the cover passes it, so clouds do not pop during a transition.
   Report the number of cloud vertices at full cover and the frame time with it.
4. `--weather <id>`: a launch flag that holds the weather through `Simulation.ForceWeather` (as `--time` holds the hour), accepting the
   ids of the loaded bundle; an unknown id is an error that lists them. Usage text and tests after `--time`'s.
5. Tests (tests/Vocaterra.Game.Tests, mirroring the source): the first kind's look equals E02's; a heavier sky is darker and less
   saturated at noon, at dusk and at night; the look is continuous along a blend from each kind's channels to each other's (no jump
   larger than a small bound between neighbouring steps); the sun's strength is zero at full cover; the fog's distances are monotonic
   in the channel and today's at zero; the cloud field's two rules; the flag's parsing and errors.
6. Screenshots, written to /tmp/claude-1000/-home-james-Dev-Vocaterra/24d882de-ab0e-4e80-85f7-7ad42d8daea9/scratchpad/e05-shots/ (not
   into the repository), `--quality low`: the camp view at `--time 13` in each of the bundle's three kinds; the village view (lanterns
   in sight) at `--time 13`, `21` and `0` in the heaviest kind; a view straight at the sun at `--time 10` in each kind; and the
   default run's camp view at tick 300 before and after your change (the pixel-identical pair). Flatten to opaque RGB as E02 did.
   Look at them yourself and say in the report what is wrong with any of them.
7. Measurements in the report: the camp view's frame time at the low and the default preset in the first kind (before and after your
   change) and in the heaviest kind.
8. Documents: the sky, clouds, fog and light sections of src/Vocaterra.Game/README.md; `--weather` in docs/game/running.md beside
   `--time`.

Out of scope: rain or any particle, and any sound (E06); the menu's Weather row, the F3 line and scripted steps (E03); the NPCs' lines
(E07); the lanterns' glow stepping at a band's edge (a known E02 limit with its own follow-up); shadows that move with the sun; any
Core or content change.
```
