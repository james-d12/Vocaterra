# Brief: E02, drawing the cycle

Sent 2026-11-10 to a general-purpose agent, Sonnet 5.5 at high effort. Worktree `/home/james/Dev/Vocaterra/.claude/worktrees/e02`, branch
`E02-draw-cycle`, cut from `feature/weather` after E01's gate. The shared part is `.claude/briefs/e-series-standard.md`.

```
You are implementing E02, "drawing the cycle": the sky, the fog and the world's light follow the day clock that E01 put in Core
(src/Vocaterra.Game.Core/Atmosphere/DayClock.cs; `VoxelSimulation.Clock`, `.Phase`, `.Tick`; the data in content/game-v1/game/day.json: a
40-minute cycle, dawn 4, day 6, dusk 20, night 22, start 8). Your work is in src/Vocaterra.Game and tests/Vocaterra.Game.Tests. The
design is docs/game/weather-plan.md sections 3.4 and 3.5; where this brief is more specific, the brief wins.

E04 (weather in Core) runs beside you. It owns src/Vocaterra.Game.Core/Atmosphere/, Simulation/VoxelSimulation.Atmosphere.cs, the save
and content/game-v1: do not edit those. Read the clock through `Simulation.Clock.HourAt(tick)`, `DaylightAt(tick)` and `PhaseAt(tick)`.
Do not create a type named Atmosphere, AtmosphereSnapshot, AtmosphereModel or Weather* anywhere: E04 and E05 do. If you need a Core
change beyond a line or two outside those files, stop and report it.

Read (the sections named): src/Vocaterra.Game/README.md (the world's drawing, the sky, the light, the figures and the arm);
Graphics/Sky/SkyModel.cs and SkyRenderer.cs (all); Graphics/World/VoxelRenderer.cs (the constructor's fog set-up near lines 125-160,
SetUnderwater and ApplyFog 227-248, SetDusk 288-300, DrawWorld from 463, the arm's DiffuseColor near 600-620, how a mesh is built,
kept and released); Graphics/World/ChunkMesher.cs (MeshOptions near 90, SunDirection and the tints 117-123, Light and Glow 489-560);
Graphics/World/ChunkVersionTracker.cs; Graphics/SceneDrawer.cs; Graphics/Figures/HeldArm.cs (ArmLight) and ArmPresenter.cs, and how the
NPC figures are lit; UI/Common/FrameView.cs; VoxelGame.cs (the dusk wiring near 180-245 and 1050-1075, Draw near 723-770);
Startup/LaunchOptions.cs (`--dusk` near 452 and 492, the value flags near 596-600); Graphics/QualityPreset.cs; docs/game/running.md and
docs/game/scripted-runs.md for how a scripted screenshot run is made (`--screenshot`, `--frames`, `--shot`, `--quality low` on software
GL).

Deliverables:

1. First, a short check, kept as a note in your report: setting `DiffuseColor` on the opaque pass's AlphaTestEffect and on the
   translucent pass's BasicEffect tints the drawn world (two screenshots, one with a strong tint). The light design below rests on it;
   if it does not hold, stop and report before building further.
2. `Graphics/Sky/SkyLook` (plain values, no engine type, like SkyPalette): a pure function from a small input (hour, daylight 0 to 1,
   phase; E05 will add cloud cover, sunlight and fog to this input, so make the input a record that can grow) to: the dome's three
   colours, the clouds' three colours, the sun's direction and strength, the moon's direction and strength, the amount of stars, the
   fog's colour (the horizon's, as today), and the colour of the sun's light on the world. Keyframes for night, dawn, day and dusk; day
   and dusk reuse SkyPalette's two existing sets, so noon looks as the game looks today and a held dusk hour looks as `--dusk` looks
   today (as close as the design allows; say where it differs). Dawn may mirror dusk. The night is dark blue and readable, not black:
   the player must still be able to walk and see NPCs without a torch.
3. `SkyRenderer` draws from a SkyLook: the dome is coloured again when the look has moved (cheaply: not a new allocation every frame),
   the sun follows an arc over the day (rising in the east, setting in the west, passing near today's `ChunkMesher.SunDirection` in the
   morning), a square moon in the night sky, simple square stars placed by a hash (no clock, no random generator) that fade in with the
   night. Clouds take their colours from the look.
4. `VoxelRenderer.SetAtmosphere(look)` replaces `SetDusk`: the clear colour and the fog's colour follow the look every frame, and
   compose with `SetUnderwater` (under water the water's colour and fog win, as today) and with the held arm's own use of
   `_solid.DiffuseColor`.
5. Light without a full remesh (the plan's section 3.5): `MeshOptions.Dusk` becomes a `LightBand` (Day, Dusk, Night; dawn uses Dusk's)
   that fixes the baked sun multiplier and the lanterns' strength (Night: the lanterns at full strength, the sun's baked light low and
   blue). A change of band marks meshes stale and builds them again in the background within the existing mesh budget, the old mesh
   staying on screen until its replacement is ready; each mesh remembers the band it was built in. Chunks are drawn grouped by band,
   with `DiffuseColor = the look's sun light / that band's baked sun light`, so every chunk shows the same sun light during the swap
   and the light moves smoothly through the day with no visible step at a band's edge. The held arm (ArmLight), the NPC figures, the
   avatar and the plants follow the same light. `ReleaseAllMeshes` is no longer the way a change of light is shown.
6. The wiring: `VoxelGame` passes the clock's values to the drawer each frame (through FrameView or beside it, as the breath does). A new
   launch flag `--time <hour>` (0 to 24, fractions allowed) holds the clock at that hour through `Simulation.FreezeClock`, so a
   screenshot run can show any hour. `--dusk` and the menu's Dusk lighting row keep working until E03 replaces them: make them hold the
   clock at a dusk hour (and release it when switched off), and leave the sound's `SetDusk` call as it is (E03 moves the sound to the
   phase). Check whether any scripted run or test pins a state digest or an event log of a `--dusk` run, since a held clock is now part
   of the state; report what you find and change only what must change.
7. Tests in tests/Vocaterra.Game.Tests, mirroring the source: SkyLook (the keyframes join without a jump, night darker than day, the
   fog's colour is the horizon's, noon equals today's day palette, the sun below the horizon at night); the band for each phase; the
   tint arithmetic (band light times tint equals the look's light for every band at a boundary hour); the stale-and-replace rule for a
   band change as far as it can be tested without a device; `--time` parsing and its errors. Existing dusk tests are moved to the new
   types on purpose, not deleted.
8. Screenshots, written to /tmp/claude-1000/-home-james-Dev-Vocaterra/24d882de-ab0e-4e80-85f7-7ad42d8daea9/scratchpad/e02-shots/ (not
   into the repository): the same view from the camp, with the sky, the village or a lantern in sight, at `--time` 0, 5, 7, 13, 21 and
   23; one of each band's edge hour a little before and a little after (to show there is no step); one looking at a lantern at night;
   one under water at night. Name the files by their hour. Look at them yourself and say in the report what is wrong with any of them.
9. Measurements in the report: the frame time of the camp view at the default and the low preset before and after your change (the
   same scripted run); how long a full band change takes to finish rebuilding the meshes, and the worst frame time while it runs.
10. Documents: the sky and light sections of src/Vocaterra.Game/README.md; `--time` in docs/game/running.md beside `--dusk`.

Out of scope: the sound beds, the menu's Time of day row, the F3 line and scripted steps (E03); anything about weather, clouds' cover or
rain (E04 to E06); shadows that move with the sun (the faces keep their fixed sun direction; that is a known limit); any change to the
map, the blocks or the content bundle.
```
