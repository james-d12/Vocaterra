# Brief: E06, rain

Sent 2026-11-10 to a general-purpose agent, Sonnet 5.5 at high effort. Worktree `/home/james/Dev/Vocaterra/.claude/worktrees/e06`,
branch `E06-rain`, cut from `feature/weather` after E03's and E05's gates. The shared part is `.claude/briefs/e-series-standard.md`.

```
You are implementing E06, "rain": the precipitation the weather's snapshot carries is drawn and heard. It is the game's first particle
effect. Your work is in src/Vocaterra.Game (a new Graphics/Weather folder, one pass in the world's draw, a quality field), in
src/Vocaterra.Game.Core/Sound (a rain bed, the wind) and their tests. The design is docs/game/weather-plan.md sections 3.1, 3.6 and
3.7; where this brief is more specific, the brief wins.

The rule the series rests on: consumers read the snapshot's channels and never a weather kind's id, and no file under src/ names a
kind's id (tests/Vocaterra.Game.Core.Tests/Architecture/NoWeatherKindIdsInSourceTests.cs). The snapshot gives
`PrecipitationKind` (a string, empty for none; the first data has one, read it from content/game-v1/game/weather.json),
`PrecipitationIntensity` (0 to 1, blended across a 45-second transition) and `Wind` (0 to 1). How a precipitation kind looks and
sounds is a row in a small style table keyed by that string, so a later kind (snow) is a new row; the table is the one place the
precipitation's id may be written, and that scan test must be told so in the way it already allows the default data.

Read (the sections named): src/Vocaterra.Game/README.md (the world's drawing order, the day cycle, the weather, the under-water
view); src/Vocaterra.Game.Core/README.md's Atmosphere and Sound sections; Graphics/World/VoxelRenderer.cs (DrawWorld's order and the
translucent water pass's blend and depth states; SetUnderwater's amount; the fog); Graphics/SceneDrawer.cs (how the look and the
under-water amount reach the renderer); Graphics/QualityPreset.cs; Graphics/Sky/SkyLook.cs (the sun light, for the drops' colour by
day and at night); in Core: Mathematics/PresentationRng.cs, Voxel/VoxelGrid.cs (`HighestBlock(x, z)`, near line 132) and the block
change listener (how a placed or removed block is noticed), Sound/AmbientLoops.cs (how the wind's loop is synthesised and made
seamless), Sound/AmbientBeds.cs (`All`, `Gain`, `At`, which takes the atmosphere's snapshot since E03; the wind by height),
Sound/SoundMixer.cs (`SetBeds`, the 1.5 s cross-fade, the duck under voices and the silence while the microphone records),
Sound/UnderwaterSound.cs; docs/game/sound-and-display.md (the rules for sound: nothing in Sound writes to the simulation; the audio
size budget); the E02 and E05 tests as models.

Another agent (E02a) may be adding one additive glow pass to VoxelRenderer.DrawWorld after the opaque and plant passes and changing
ChunkMesher's light. Do not edit ChunkMesher or the light bands, and keep your DrawWorld change to the one new pass, after the water.

Deliverables:

1. `Graphics/Weather/PrecipitationField` (no engine type): the drops as positions in a box that wraps round the camera (so the field
   never ends and costs the same anywhere), placed and timed by a hash or PresentationRng (no clock, no System.Random: the same tick
   and camera give the same drops), falling at the style's speed and slanted by the wind channel. Each column of the box has a cover
   height from `VoxelGrid.HighestBlock`, cached and refreshed when a block in the column changes or the box moves, and a drop is not
   drawn below its column's cover: no rain under a roof, a tree or an overhang, and rain visibly stops at the eaves. The number of
   drops alive scales with the intensity, so a transition thickens and thins the rain smoothly with no pop.
2. `Graphics/Weather/PrecipitationStyle` and its table: per precipitation kind the streak's length and width, the fall speed, the
   colour and opacity, how much the wind slants it, and the count at full intensity as a share of the quality's budget. One row for
   the first data's kind. An unknown kind draws nothing and is reported once in the log, not an error.
3. `Graphics/Weather/PrecipitationRenderer`: the streaks as camera-facing quads from one reused vertex buffer (no allocation per
   frame), `BasicEffect` with vertex colour, alpha blend, depth read and not written, drawn after the water pass; fogged like the
   world, so far rain fades into the haze; lit by the look (dimmer at night); not drawn when the eye is under water (fade with the
   under-water amount) and not drawn into the under-water view's blur wrongly (check the screenshot from under the lake's surface
   looking up).
4. `QualityPreset` gains the drop budget (low, medium, high), chosen so the rain's cost stays small on the low preset under software
   GL. Report the frame time of the camp view in the heaviest kind at each preset before and after, and the vertex count.
5. Sound. A synthesised, seamless `rain` loop in AmbientLoops (filtered noise with a little texture; inside the audio size budget)
   and a `rain` bed in AmbientBeds: its weight is the precipitation's intensity, lower when the listener stands under cover (the
   same cover query as the drops, from Core's grid, so Sound still writes nothing to the simulation), cross-faded by the mixer like
   every bed, ducked under voices and silent while the microphone records, dulled under water with the others. The wind bed's
   weight rises with the wind channel on top of its rise with height. Birds are quieter in rain (multiply by one minus the
   intensity). The gain is set so rain at the first data's intensity does not mask an NPC's voice even before the duck: say what you
   set and why.
6. Tests, mirroring the source: the field is deterministic and wraps (a camera moved by the box's size sees the same pattern); no
   drop below its column's cover, and a column's cover follows a block placed or removed; the alive count is monotonic in the
   intensity and zero at zero; the slant follows the wind; an unknown kind draws nothing; the style table has a row for every
   precipitation kind of the bundle's weather.json (a test over the data, so a kind added later without a style fails here); the rain
   bed's weight by intensity and under cover, the wind's by the channel and the height, the birds' in rain, and every other bed
   unchanged; the loop is seamless (its ends join) and within the size budget.
7. Screenshots to /tmp/claude-1000/-home-james-Dev-Vocaterra/24d882de-ab0e-4e80-85f7-7ad42d8daea9/scratchpad/e06-shots/ (not into the
   repository), `--quality low` unless said, flattened to opaque RGB, with `--weather` set to the bundle's rainy kind: the camp view
   at `--time 13` and `0`; the village at 13 and at 21 with lanterns; standing under a roof or the market stall's awning looking
   out (rain outside, none inside); under a tree; from under the lake's surface looking up, and at the lake's surface from the shore;
   the camp view at the default preset; and the default run's camp view at tick 300 in the first kind, which must be pixel-identical
   to the build before. Look at them and say what is wrong with any.
8. Documents: a rain section in src/Vocaterra.Game/README.md (the field, the cover, the style table, the pass's place in the order,
   the quality field); the rain bed, the wind and the birds in docs/game/sound-and-display.md and the Sound section of
   src/Vocaterra.Game.Core/README.md; the precipitation style in the "weather" notes of content/README.md if that file describes
   weather.json's `precipitation` field.

Out of scope: splashes, puddles, wet blocks, rain filling or rippling water, thunder and lightning, snow, drops on the screen; any
gameplay effect of rain; the lanterns' glow (E02a); Core's Atmosphere folder and content/game-v1.
```
