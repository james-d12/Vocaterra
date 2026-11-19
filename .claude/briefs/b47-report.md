# B47 report (the far-tile flicker with Shaders off; widened to the owner's far-view holes)

Branch `B47-flicker-shaders-off`, head `7d17b292` (feature/world-next at 5e51391a merged). Agent's gate after the last merge: build 0
warnings; full 12,741 passed, 0 failed, 9 skipped; format clean; validate.

Shaders off: option (a) in both paths; the shader clip removed (`uClip`, `uClipCount`, `SetFarClip`, `FarWindow.Most` gone). The mesher keeps
each far tile's quads inside one granule (32 blocks, or two cells from level 4 up) and writes them granule by granule (`FarGranules`); a
stand-in's windows become index runs (`FarTile.RunsOf`, one per row of granules per window) drawn by both paths (`DrawIndexedPrimitives`
start index; GL `DrawFar` ranges). Nothing clipped on the card. A stand-in draws whole only if a window is finer than its granule or it has
no granules. Tiles are cut only when the view builds them (`Build(..., cut: true)`); cut tiles have 1.42 times the quads over levels 0 to 7
(levels 0 to 3 at most 1.25).

The owner's three symptoms: all are a needed leaf with no drawn tile and no drawn stand-in. Cause found and reproduced: B45's
`NearFirstGate` held every far tile and stand-in until the 128-block near view was done (or 20 s); an eye flying at 190 blocks a second
outruns the chunks, so the near view never completes; a fresh start then flight left the far land held 20 s (983 of 1,200 flight frames
with a visible leaf left to the sky); frame-to-frame jumps over 64 blocks do not fire during flight, so the gate armed once and stayed shut.
Fixes: `NearFirstGate.Observe` takes the eye and opens once the eye is 128 blocks from where it was armed (walking never reaches it within
the timeout); `FarView.SelectAhead` asks for coarse leaves and stand-in ancestors up to 160 blocks ahead of a moving eye (capped at half the
voxel zone; nothing for a stationary eye or a jump); `FarView.Reassemble` runs after the frame's uploads (no one-frame flash). Symptoms 1
(grid of strips) and 3 (the zone-edge line) are the same fault; 2 (64-block holes) probably the same at 64-block leaves, **not proven: the
owner to recheck all three in a flight**.

Tests (13 new): `FarTileGranuleTests` (2); `FarViewStandInClipTests` a Theory over both paths checking drawn geometry (every quad in a
window, every flat top inside a window drawn, nothing twice, no cell with two owners, no flip; about 8,000 stand-in draws: 0 whole, 0
flips); `FarViewFlightTests` (GL, effects, GL with the real `LodRing`: the gate opens within 128/3+5 frames; after catch-up no needed leaf
left to the sky, zone edge included); `NearFirstGateTests` +2.

Pictures (artifacts/b-series/b47/): two 11-shot sheets, Shaders off and on, before the gate and look-ahead changes; identical, no gaps or
doubled ground; none of the owner's flight. gl-compare: shore 0.73%, under 0.53%, village 0% of pixels over 8 of 255; meadow and cave
(streamed) time out at 2,400 s (the scripted settle of 16 km on 2 workers; pre-existing). Frame cost: far pass under 1 ms either way (noisy
load); `--stream-stats` prints `far pass (effects|GLSL, processor time)` and the card's time.

Left: the owner's flight check; a fresh fill after the gate opens still needs tile build time; unfit stand-ins (a window more than 4 levels
finer) still draw whole (0 times in tests); the look-ahead covered by the flight test only.

For architecture.md: "Far stand-ins are drawn inside their windows by index runs, in both paths: the mesher cuts every far tile's ground
into granules (32 blocks, or two cells from level 4 up; a quad never crosses a granule's edge) and writes each bucket's quads granule by
granule (`FarGranules`), so `FarTile.RunsOf` gives the runs of a window set and the renderer draws only those. Nothing is clipped on the
card; a stand-in is drawn whole only if a window is finer than a granule or it has no granules. `NearFirstGate` opens when the near view is
done, after 20 s, or when the eye is 128 blocks from where the gate was closed. `FarView` asks ahead of a moving eye for coarse tiles and
stand-ins (`SelectAhead`, up to 160 blocks), and `FarView.Reassemble` runs after a frame's uploads." For running.md: "`--stream-stats` prints
`far pass (effects|GLSL, processor time)` and `far pass (..., the card's time, GL timer query)`; the near-gate line gains `N left behind`."
