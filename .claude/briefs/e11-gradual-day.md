# Brief: E11, a gradual day

Sent 2026-11-11 to a general-purpose agent, Sonnet 5.5 at high effort. Worktree `/home/james/Dev/Vocaterra/.claude/worktrees/e11`,
branch `E11-gradual-day`, cut from `feature/weather`. The shared part is `.claude/briefs/e-series-standard.md`.

```
You are implementing E11: the time of day changes gradually. The owner (2026-11-11): "there is no difference at all between 6am -> 8pm,
its only dusk and night. A more gradual change through the day, and more gradual changes from dusk -> dawn -> night would make the time
of day more realistic."

Why it is so today (read these first): `SkyLook.At` and `SkyLook.Weights` (src/Vocaterra.Game/Graphics/Sky/SkyLook.cs) blend three
keyframes (`SkyKeyframe.Day`, `Twilight`, `Night`) by the phase and the daylight only. In the Day phase (6 to 20, content/game-v1/game/
day.json) the weights are a flat (1, 0, 0), so 6:00 looks like 13:00; dawn (4 to 6) and dusk (20 to 22) are straight lines of the
daylight (`DayClock.DaylightAt`, src/Vocaterra.Game.Core/Atmosphere/DayClock.cs); the Night phase is a flat (0, 0, 1). The sun itself
moves (`SkyArc.SunAt`: up at 5, highest, about 56 degrees, at 13, down at 21; under the world, lowest at 1), but the look ignores its
height. The world's light is a per-frame tint over three baked bands (`LightBands`, `VoxelRenderer.SetAtmosphere`): any light inside a
band is free (no remesh), and the band changes stay where they are.

What to build (the look only; no content edit, the phase hours in day.json stay as they are):
1. The day by the sun's height. Add a `Golden` keyframe (a low sun: a warmer, paler horizon, a softer zenith, a warm light a little under
   the noon's, for example about (1.0, 0.9, 0.76), never above 1 in any channel so the Day band's tint stays valid) and blend it with the
   noon look (today's `Day` keyframe, unchanged: it is `SkyPalette`, the sky the game has always had) by the sun's elevation, smoothly
   (smoothstep, not a line): mostly Golden below about 10 degrees, all noon above about 40 degrees. With today's arc that gives a warm early
   morning (6 to 8), a full noon look from about 10 to 16, and a warming afternoon into the dusk. Morning and evening may share the
   Golden keyframe.
2. Dawn and dusk eased and bridged. Across dawn and dusk the look goes Night -> Twilight -> Golden (not straight to noon), and the
   daylight follows a smooth curve (smoothstep of today's linear value, so it is 0.5 half way as today) in `DayClock.DaylightAt`.
3. The night deepens and lifts. Add a `DeepNight` keyframe (darker and less blue than today's `Night`, the lanterns at full) and blend
   today's `Night` (at the edges of the night, a blue hour) towards it by how far the sun is under the horizon, smoothly, deepest round
   1:00, so 22:00 and 3:30 are visibly lighter than midnight. The stars and the moon keep their present rules unless they now look wrong.
4. Everything continuous: no step in any colour of the look as the hour moves, across every phase boundary and band change.
5. Weather composes as before: `SkyWeather`'s grey, darken, cloud-follow and sunlight steps apply after the mix, unchanged.

Rules to keep (existing tests; change one only on purpose and name it in the report): at 13:00 in fair weather the look equals today's
noon look; `--dusk` (the hour 21, `SkyLook.DuskHour`) still shows the low orange sun, warm light and full lanterns; E05's "the first kind
equals E02's look" and continuity tests; `LightBands.For` and its thresholds; the baked band values. Tests that pin `Weights` to (1, 0, 0)
in the day or the old linear daylight will change: rewrite them to the new rules and name each.

New tests (tests/Vocaterra.Game.Tests/Graphics/Sky/ and Game.Core.Tests for the daylight): the look's brightness (luminance of the light
and of the dome's middle) rises from 6 to 13 and falls from 13 to 20; 8:00 and 18:00 differ visibly from 13:00 (a stated minimum
difference) and are warmer (red over blue of the light larger than noon's); midnight is darker than 22:00 and than 3:30; continuity:
sampling every minute of the 24 hours, no colour channel of the look moves by more than a small bound (choose it, about 3 of 255) from
one minute to the next, in fair and in the heaviest weather; the daylight is smooth (no slope jump at the phase edges beyond a bound).
Also: `AmbientBeds` reads the daylight (birds and insects): its tests must still pass (half way through dusk is still 0.5).

Pictures (scripted runs, `--quality low` if software GL; nothing committed): the camp view in fair weather at 5, 6, 7, 8, 10, 13, 16,
18, 19, 20, 21, 22, 0, 2 and 3:30, before your change (build `feature/weather` first, or run the old binary) and after, to
/tmp/claude-1000/-home-james-Dev-Vocaterra/749a3cc3-e476-4312-9dfe-129a441f408b/scratchpad/e11/ as before-<hh>.png and after-<hh>.png,
plus one contact sheet per set (a single image with the 15 views in hour order, labelled) if you can make one with the tools at hand.
Look at them yourself and say in one line each what changes; the owner's test is whether the day now reads as morning, noon, afternoon,
evening and night.

Documents: docs/game/running.md's "Day, night and weather" (what each hour looks like now), the day cycle section of
src/Vocaterra.Game/README.md, and docs/game/architecture.md if it names the three keyframes.

Out of scope: the faces' fixed sun direction (shading that moves with the sun needs a remesh or a shader); the phase hours and every
value in content/; the sound (another agent, E10, is changing the insects' synthesis, and E09 the volumes, in other worktrees at the same
time: do not touch src/Vocaterra.Game.Core/Sound/ or the menu); the weather's data.

The gate is the standard brief's (the suite count on `feature/weather` is given in your prompt). Report: the standard one page, the new
keyframes' values, the elevation thresholds you chose, every existing test changed and why, and the picture list with one line each.
```
