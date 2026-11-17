# B17a: a GLSL spike on MonoGame's context (2026-11-17); B17b: every chunk pass on it; B17c: the light, fog, water and far view in it (below)

Goal (D-game-34 items 30, 39, 40; `docs/game/biomes-plan.md` section 3.9): prove that the game can draw its chunks with its own OpenGL shaders, compiled at run time by the
driver, inside MonoGame's frame, and say how well it fits. The spike draws the **opaque chunk pass** through one GLSL program behind `--gl-shaders` (off by default). The sky, the far
view, the cut-outs (plants, grass), the lantern glow, the water, the figures, the arm and the UI stay on MonoGame. Branch `B17a-glsl-spike`; code in `src/Vocaterra.Game/Graphics/Gl/`.

## What was built

| Piece | File | What it does |
|---|---|---|
| Binding | `Silk.NET.OpenGL` 2.22.0 | MIT (nuspec `license type="expression"`: MIT; its dependencies Silk.NET.Core, Silk.NET.Maths, Ultz.Bcl.Half, Microsoft.Extensions.DependencyModel, Microsoft.Bcl.HashCode, System.Runtime.InteropServices.NFloat.Internal: MIT, checked with the licence scan, below). Direct reference of `Vocaterra.Game` only (engine code, D-game-1); version in `Directory.Packages.props`. |
| Context | `GlLoader.cs` | No second context or window. `SDL_GL_GetProcAddress` is taken from the SDL library MonoGame already loaded (`libSDL2-2.0.so.0`, found by its soname through `NativeLibrary`), and Silk's `GL.GetApi` is given it. Valid on the game thread, which holds MonoGame's context. |
| Facts | `GlInfo.cs` | `GL_VERSION`, renderer, vendor, GLSL version, core or compatibility, and the 4.x features, logged as `gl:` lines at the start. |
| Program | `GlProgram.cs`, `ShaderSources.cs`, `Shaders/opaque.vert.glsl`, `opaque.frag.glsl` | Sources are embedded in the assembly (`Vocaterra.Game.Shaders.*`), compiled and linked at start; a failure is logged with the stage and the driver's own log and the game then draws with the effects as always. |
| Pass | `GlOpaquePass.cs`, `GlChunkBuffer.cs` | A vertex array of its own, a shared quad index buffer (16,383 quads), a copy of the atlas texture (every mip level, point within a level and linear between, clamped: the atlas sampler's settings), per-chunk vertex buffers of its own for the opaque faces (MonoGame gets none of them in this mode), and the draw. |
| State | `GlStateSaver.cs` | Saves and restores what the pass touches (below). |
| Wiring | `LaunchOptions.GlShaders`, `SceneDrawer.Load`, `VoxelRenderer.SetGlPass`, the opaque loop in `DrawWorld` | The pass is made in one place, behind the option; the renderer draws through `AlphaTestEffect` whenever it has no pass, or a render target is bound (see "What is not done"). |

The program does what `AlphaTestEffect` does for the chunks (`VoxelRenderer`): texel times (band tint times vertex colour), alpha test `Greater` against 110 of 255 (half a step of
slack, as MonoGame has), and the linear fog on view-space depth (`EffectHelpers.SetFogVector`: the fog vector is computed on the processor from the view matrix, start and end) towards the
fog colour premultiplied by alpha. Light stays baked in vertex colours: this is the same picture, not a new look.

## The context MonoGame made

On this machine (RTX 3070, NVIDIA 620.70.06, Linux, X11):

```
gl: version 4.6.0 NVIDIA 620.70.06, renderer NVIDIA GeForce RTX 3070/PCIe/SSE2, vendor NVIDIA Corporation
gl: shading language 4.60 NVIDIA, context 4.6 compatibility profile
gl: compute yes, multi-draw indirect yes (with count yes), buffer storage yes, clip control yes, base vertex yes, shader storage yes, max texture 32768, max array layers 2048
```

MonoGame asks SDL for a default context, and the NVIDIA driver answers with the **highest compatibility profile, 4.6**. So everything B19 would use is present on this driver: compute
(4.3), `glMultiDrawElementsIndirect` and the count variant (4.3 and 4.6), immutable buffer storage with persistent mapping (4.4), `glClipControl` (4.5, so reversed depth is possible),
shader storage buffers, base-vertex draws, 2,048 array-texture layers. **What is not known and matters:** what a default context gives on other drivers (not tested here: Mesa's compatibility profiles are often lower than NVIDIA's, and
macOS is out of scope), so the version must be read at run time, as the pass does. The program asks for GLSL 330; the pass checks
the version and falls back with a log line if the context is below 3.3. B19 steps that need 4.3 would have to check `GlInfo` the same way (the code has the flags) or change the
context request, which is MonoGame's (SDL attributes are set before the window exists; there is no hook, so a 4.3 core context would mean a MonoGame patch or the engine change).

## The state hand-over

MonoGame 3.8.6 keeps a cache of the GL state it believes is set (blend, depth, rasteriser, sampler states, the program, the bound buffers and textures, the enabled attribute arrays) and
skips a call when the cache says it is done. A pass of ours that left another state would corrupt every later MonoGame draw. The cache cannot be read from outside, but it is only
wrong when the context differs from what it was when the pass began, so the rule is: **read the real state, set what the pass needs, write back exactly what was read.** What
MonoGame had pending (a state set on the device and applied lazily at its next draw) stays pending, because the real state is again the one its cache last knew.

How it is done:
- `GlStateSaver.Capture` reads (with `glGet`, about 25 calls a frame, microseconds): the program, vertex array and array buffer bindings, the active texture unit and the 2D texture
  and sampler bound on the pass's unit (unit 8: MonoGame uses the low ones), the enable bits for depth test, cull face, blend, stencil test, scissor test and polygon offset, the depth
  function and mask, cull mode, front face, colour mask, viewport and depth range. `Restore` writes them back and **drains the GL error queue**: MonoGame's debug builds check for a GL
  error after their own calls and would blame their next call for ours.
- The pass **sets everything it relies on** instead of trusting the context: depth test on, `LEQUAL`, depth write on, cull back with counter-clockwise front (MonoGame's
  `CullClockwise`), blend, stencil, scissor and polygon offset off, the device's viewport (with MonoGame's flipped y) and depth range.
- **Its own vertex array**, so the attribute arrays it enables and the element-array binding never touch MonoGame's (the compatibility profile's default array 0 stays as MonoGame
  left it). Uploads bind the `COPY_WRITE_BUFFER` target, which MonoGame never uses, so the array-buffer binding is not even touched outside a pass.
- The atlas is **a copy** in a texture of the pass's own, not MonoGame's texture object (whose id is an internal field).

Risks, and what was found:
1. **MonoGame calls disturb the context under the spike's hands.** `Texture2D.GetData` binds MonoGame's own texture on the *active* unit; the first version copied the atlas with a
   texture bound for the upload and uploaded into MonoGame's atlas instead, leaving a black (incomplete) texture of its own: every chunk drew black. Fixed by reading all the texels
   before binding anything. Any MonoGame call between our bind and our draw can do the same; the pass keeps a straight line (Begin, draws, End) with no MonoGame call inside it.
2. **Depth must match bit for bit where passes meet.** The lantern glow pass (MonoGame's effect) redraws the opaque quads with depth test `LEQUAL` and so needs the very depth the opaque pass wrote. With `gl_Position = matrix * position` (fused multiply-adds in another order than MonoGame's translated shader) the depths differed in the last bit and the glow came out in triangles that failed the test (a placed torch's warm light missing on 7.5 percent of the picture). Writing the position as four dot products with the matrix's columns, as MonoGame's translated shaders do, made the picture identical to the pixel (0 differing pixels). This holds only on this driver and compiler: **any pass that depends on equal depth should use the same program as the pass it follows** (the glow and the cut-outs in the real B17), not rely on arithmetic luck.
3. **No render target support.** Under water the scene is drawn into a render target (`UnderwaterPass`); MonoGame flips y and the winding there. The pass falls back to the effect while a render target is bound (`CanDraw`), logged as a count. B17 must flip the clip y and the front face for render targets (a uniform) or draw the scene to a target of its own.
4. **The cache going stale** is covered by restoring real state, but anything the pass changes and the saver does not list is a leak: not saved today are the framebuffer binding (never changed), the pixel store, primitive restart, sRGB framebuffer enable, polygon offset factors (only the enable bit is toggled), line and point sizes, clip distances. None is touched by this pass; a new pass that touches one must add it to the saver.
5. **Threads.** The binding's functions are valid on the thread that holds the context; the game's meshing workers never call GL (uploads happen in `BuildChunk` on the game thread), and the pass asserts nothing about it.
6. **Compatibility versus core.** The pass works on a compatibility context because it makes its own vertex array; in a core context MonoGame itself could not draw without one, so it is the same.
7. **Resource lifetime.** Buffers are deleted with `glDeleteBuffer` on the game thread when a chunk is released or the renderer is disposed (at shutdown the context must still exist: it does, the renderer is disposed first).

## The comparison

`scripts/gl-compare.sh OUT` (Release, High, seed 42, midday, `--pass open`, 1280x720, a fixed 60 Hz scripted clock; `scripts/gl-compare-images.py` compares). Both views, with the
effect and with `--gl-shaders`:

| View | Pixels | Differing at all | By more than 8 of 255 | Largest difference |
|---|---|---|---|---|
| Meadow at 380,217 looking east (midday, High) | 921,600 | **0** | 0 | 0 |
| Cave at 468,62,153 with two torches placed beside the camera | 921,600 | **0** | 0 | 0 |

The tolerance the check uses is 0.1 percent of the pixels differing by more than 8 of 255 (it exits 1 above that); both views are inside it with nothing to spare: they are the same picture.
(The first versions: with `matrix * position` the meadow differed in 279 pixels (0.03 percent, single pixels on thin geometry where the depth of the grass tufts and the plants fell on the other side
of a test) and the cave in 7.5 percent; the dot-product form above made both exact. They are recorded because they show how fragile equal depth is.) Pictures and numbers:
`/home/james/Dev/Vocaterra/artifacts/b-series/b17a/` (outside git): `meadow-effect.png`, `meadow-gl.png`, `cave-effect.png`, `cave-gl.png`, the diffs and each run's stdout.

**Frame times** (`--stream-stats`, 240 frames after the steps, Release, High; the machine was shared with up to four other agents' builds, tests and game runs, load average 10 to 18 on
16 cores, so whole-frame times swing from 5 to 40 ms between two identical runs; read the pass's own number):

| Run | Opaque pass, processor time to submit (median / p95 / p99) | Frame p50 / p95 (noisy) |
|---|---|---|
| Meadow, effect | 1.06 / 1.41 / 3.51 ms | 5.4 / 24.3 |
| Meadow, GLSL | **0.81** / 1.18 / 3.48 ms (-24 percent) | 36.3 / 48.9 (load) |
| Cave, effect | 1.31 / 1.96 / 6.35 ms | 5.4 / 27.4 |
| Cave, GLSL | **0.73** / 1.26 / 1.41 ms (-44 percent) | 6.0 / 29.4 |

The processor cost of submitting the opaque pass (1,063 draw calls and 440k quads in the meadow, 1,725 and 725k in the cave) falls by a quarter to a half: the pass sets its state once and
each chunk is a buffer bind, three attribute pointers and one draw, with none of MonoGame's per-draw validation. It is a fraction of a millisecond out of a frame, so the frame is not
visibly faster; the card's time was not measured (no timer queries yet). **Start-up** costs 54 to 62 ms more (renderer ready in 114 to 122 ms against 60 ms: compile, link, the
atlas copy of every mip level, the index buffer). Chunk meshing and upload times were within noise (1.0 to 1.6 ms a chunk both ways in repeated runs). No GL error was raised in any run.
Memory: in this mode the opaque faces live only in the pass's buffers (MonoGame gets none), so there is no duplicate; the atlas copy is 1 x 4/3 of one texture.

## The gate

Build `-warnaserror` 0 warnings; `scripts/test.sh full` 12,455 passed and 5 skipped (12,448 before, plus 8 new tests; the 2 live-GL ones are skipped without `VOCATERRA_GL_TESTS=1` and passed on the
RTX 3070 with it), 1 failed: `ValleyUndergroundTests...UndergroundCostsUnderOneSecond`, a wall-clock timing test that fails when other agents load the machine (it failed in two full runs at load 10 to 18,
and passes alone: 1 of 1 in 8 s); nothing in it touches the game's drawing. `dotnet format --verify-no-changes` clean; `scripts/content.sh validate` OK (bundle unchanged). **Licence scan** (the gate's, core plus
Game.Core.Tests): PASSED. The scan of `Vocaterra.Game` itself (not part of the gate): the spike's packages `Silk.NET.OpenGL`, `Silk.NET.Core`, `Silk.NET.Maths` (2.22.0) and `Microsoft.Extensions.DependencyModel`
(8.0.0) PASS as MIT; `Microsoft.DotNet.PlatformAbstractions` 3.1.6 (a dependency of Silk.NET.Core) reads UNKNOWN offline because its nuspec names a license file, which is the MIT licence (read from the
package); the other FAIL and UNKNOWN rows of that scan (FontStashSharp: Zlib; MonoGame's SDL and OpenAL libraries and NVorbis: licence files) are the game's earlier packages, not new. A deliberate decision for the
owner: whether the game project joins the gate's scan, and an override line for PlatformAbstractions (MIT, with the evidence) if so.

## The binding's fit (Silk.NET.OpenGL 2.22.0)

- Works on .NET 10 (its libraries target netstandard and net6), loads the whole GL 4.6 surface from any `Func<string, nint>`; no native library of its own to ship.
  `GL.GetApi(new LamdaNativeContext(...))` (the type name has a typo in the library). Dependencies add a handful of small managed assemblies, all MIT (see the table above).
- Hazards met: the generic overloads taking `in T` accept a literal or an array in the wrong place (`DrawElements(..., 0)` bound to a reference-to-0 overload, which would pass the address of a local as an index pointer; the pass uses `(void*)0`, so `AllowUnsafeBlocks` is on in the game project); `PrimitiveType` clashes with MonoGame's (aliased); `GLEnum` is used for many queries the typed enums lack; strings and spans need care (use the span overloads for buffers).
- OpenTK 4.9 (MIT) would do the same; Silk's functions are plain function pointers behind a class, which is the right shape for code that must not own a window.
- It binds no window system, so nothing fights MonoGame for the context.

## What is not done

- Only the opaque pass. The glow pass (MonoGame effect, depth-equal with the opaque pass) happens to match; the cut-outs, the grass, the water, the far view and the figures are MonoGame's.
- Render targets (under water) fall back to the effect.
- No GPU timing, no multi-draw: one draw call per chunk, as before.
- The compile test and the context checks need a window: `VOCATERRA_GL_TESTS=1` runs them (`GlShaderTests`); the gate skips them.
- Linux and Windows only by construction (the loader looks for `libSDL2-2.0.so.0` and `SDL2.dll`).

## Recommendation: go, with these changes

The route works: one context, no second window, the same picture to the pixel, and a lower processor cost. Go on with B17 as raw GLSL on MonoGame's context. What to change before building on it:

1. **Move every chunk pass onto the GL program in one step** (opaque, lit pieces, glow, cut-outs, grass, water): the passes that share depth by equality must share a program, and the state saver is cheap but MonoGame draws in between are the main source of risk; fewer round trips is safer. Keep the effects as the fallback path (the option and the fallback stay).
2. Make the saver a **scope object around all game-owned GL work in a frame** (one Capture and Restore per frame, not per pass) once the passes are contiguous; add the unsaved state to it when a pass touches it.
3. **Render targets**: flip clip y and winding by a uniform, or give the scene a render target of the pass's own (needed for the underwater pass and any later post step).
4. **Own the vertex data**, which the spike already does for the opaque faces: a compact vertex, a texture array in place of the atlas, base-vertex and indirect draws are all one step away and need no MonoGame object. Light as data and the radial fog are the shader's next job.
5. **Decide the context request** before B19's compute and indirect steps: a default MonoGame context is 4.6 compatibility on NVIDIA, unknown on Mesa; the features are checked at run time through `GlInfo`, with the effect path as the fallback.
6. A GL timer query for the card's time, so B17 can be judged on the card and not only on the processor.

Size of the remaining B17 work: **L**, as the plan has it. The spike has done the toolchain part (the context, the loader, the program, the state hand-over, one pass and the
comparison harness, which is the part the plan feared: no Wine, no content pipeline, the licence settled), leaving the other passes (the cut-outs and the water are the awkward ones),
render targets, the atlas as a texture array, light as data, the fog, timer queries, the fallback policy and the tests. Item 1 above (all the chunk passes in one step) is the bulk of it, perhaps M by itself.


---

# B17b: every chunk pass on the game's own GLSL (2026-11-17)

Goal (D-game-34 items 40, 41 and 43; the spike's recommendation 1, 2, 3, 5 and 6): the opaque faces, the lit pieces, the cut-outs, B18's grass layer, the lantern glow and the water all drawn through the game's GLSL on MonoGame's
context in one state scope a frame, with the picture of the effects; a Shaders row on the settings page and a launch option; the card's time measured. Branch `B17b-gl-passes`, code in `src/Vocaterra.Game/Graphics/Gl/`.

## What moved

| Piece | Where | What changed |
|---|---|---|
| One program | `Shaders/chunk.vert.glsl`, `chunk.frag.glsl` (`opaque.*` renamed) | Texel times (band tint times vertex colour), the alpha test (reference 110/255; a reference below 0 is no test: the water), today's linear fog (premultiplied by alpha), the position as four dot products with the matrix's columns (the depth-equality rule of the spike), and a `uFlipY` uniform for render targets. Every pass is this program: the glow shares the opaque faces' program and transform, so its depth equals theirs to the last bit. |
| The passes | `GlChunkPass` (was `GlOpaquePass`), `GlPassKind` | `Use(kind)` sets the state of a kind of draw: **Opaque** (depth test and write, less or equal, back faces culled), **Cutout** (the plants and the grass: both sides), **Glow** (colour added, `ONE ONE`, alpha left; depth read only; the fog colour black), **Water** (`SRC_ALPHA, ONE_MINUS_SRC_ALPHA`, depth read only, no alpha test, one of 16 frames of the animated art on the pass's unit). The grass layer's quadrants are `glDrawElementsBaseVertex` ranges. One shared quad index buffer, one vertex array, one program, 4 state sets a frame (3 without water). |
| Buffers | `VoxelRenderer.ChunkGpu`: `GlOpaque`, `GlAlpha`, `GlPlants`, `GlLit`, `GlGlow`, `GlGrass` | Every part of a chunk uploads on the game thread in `IChunkMeshSink.Install` (B21's path, kept) into buffers of the pass's own; MonoGame gets none. Switching the setting in play lets every mesh go and builds the view again (nearest first, on the workers). |
| Textures | `GlChunkPass.TryCreate` | The atlas (nearest within a level, linear between, every level) and the 16 water frames (trilinear, clamped, last level 5: `TEXTURE_MAX_LEVEL`, which is how MonoGame applies `SamplerState.MaxMipLevel`; the first version took it for the base level and the water came out smooth where the effects' has its glints) are copies in textures of the pass's own, made once. |
| The scope | `GlStateSaver`, `GlChunkPass.Begin`/`End` | One `Capture` and one `Restore` a frame round all the chunk work. The saver now also holds the blend function and equation (the glow's and the water's), the pixel store's unpack alignment (the texture uploads), primitive restart (the quad indices run to 65,535, so the scope turns it off, and the fixed-index bit too on a 4.3 context) beside what it held. Not saved, because nothing touches it: the polygon offset's factors (only the enable bit), line and point sizes, clip distances, the framebuffer. **No MonoGame call stands inside the scope**: the figures, the plots' outlines and the block's frame (opaque, depth tested) are drawn before it in this path (after the glow in the effects' path); a test reads the renderer's source for it. The rain (after the water) finds the device's blend, depth and sampler state set as the water leaves them in the effects' path. |
| Render targets | `GlChunkPass.OrientationFor`, `uFlipY` | A target bound (the scene under water) turns the clip y over and the front face to clockwise, and takes the viewport's y as it is: MonoGame's own convention. The fallback count is gone. |
| The card's time | `GlCardTimer` | A `GL_TIME_ELAPSED` query round the chunk passes, a ring of 24, read a frame or more later without waiting; the pieces of a frame are added up (the effects' path is timed in two pieces round the figures). Works with either path (a query is no state MonoGame tracks); made when `--stream-stats` is given. |
| The setting | `SettingId.Shaders`, `MenuRows` (a "Shaders and effects" sub-group at the end of the Graphics group), `SaveSettings.ShadersOff`, `LaunchSettings.Shaders`, `QualityPreset.Shaders`, `SceneDrawer.ApplyShaders` | A toggle, on by default, written to a save's settings only when off (a save of the default is byte for byte what it was; the fixture is untouched). `--gl-shaders on|off` (a bare `--gl-shaders` is on) wins for the run. A change of quality level sets the Shaders row from the preset (`QualityPreset.Shaders`, true at all three levels), and the row can be changed alone after that: the mechanism the B17c effects will use. **Changing it in play needs no restart**: the pass is made or dropped at once and the world fills in again for a moment. |

## The default and the fallback (what was chosen)

**The GL path is the default**: the comparison below holds (the same picture to the pixel where the effects' output is the same on both, a quarter to a tenth of the processor time to submit, and the card's time equal or lower). `--gl-shaders off`, or the Shaders row off, keeps the effects. The effects are
used **automatically** (one log line, `gl: shaders not used, the effects draw as always: <reason>`, said once) when the loader fails, `GlInfo` reports a context below 3.3, or a stage does not compile or link (the driver's own log is in the line); nothing changes for the player. Unit tests check the
rule for the version, the line, the options and the setting; the live tests (`VOCATERRA_GL_TESTS=1`) compile every embedded source, report a broken one with the driver's log, check that the scope restores everything it captured after every kind of draw, and draw a quad through the pass and
through `BasicEffect` to the back buffer and to a render target and compare the pixels (equal).
Live tests share **one** `GlProbeGame`: a third MonoGame window in the test host hangs (found while writing them), so they take turns on one and leave its state as they found it.

## The comparison

`scripts/gl-compare.sh OUT [view...]` (Release, High, seed 42, 1280x720, a fixed 60 Hz scripted clock, 240 frames after the steps; effects are `--gl-shaders off`, GLSL is `--gl-shaders on`; `GL_RUNS=N` interleaves the two N times). Pictures, differences and stdout:
`/home/james/Dev/Vocaterra/artifacts/b-series/b17b/` (outside git). Tolerance as B17a's: at most 0.1 percent of the pixels differing by more than 8 of 255 (`scripts/gl-compare-images.py`); every view below is **exact or within one step**.

| View | Where | Pixels differing at all | By more than 8 | Largest |
|---|---|---|---|---|
| meadow (midday) | 380,217 looking east, streamed world | 0 | 0 | 0 |
| wood-dusk | the valley wood 196,206 at hour 21 (B22's dusk band) | 0 (first capture: 119, 2 above 8, largest 9: a patch of sky seen through holes in a crown; the next capture, the same binary, 0) | 0 | 0 |
| cave | the landing 468,62,153 looking north-north-west, streamed world | 0 | 0 | 0 |
| torches | the village street at hour 0: torch posts and lanterns with their glow, the NPCs | 0 | 0 | 0 |
| shore | the pool above the waterfall 258,106 (water, translucent, sorted) | 3 (1 step) | 0 | 1 |
| under | under the waterfall's pool 262,41,136 (the scene drawn to a render target, blurred, tinted) | 0 | 0 | 0 |
| village | the street 266,223 | 0 | 0 | 0 |
| grass | the meadow with B18's grass at 60 percent | 0 | 0 | 0 |

The first run of the shore view differed on 1.23 percent of the pixels (up to 96 of 255, all on the water): the water frames had been copied with the sampler's `MaxMipLevel` as the base level instead of the last level; with that read as MonoGame reads it (`TEXTURE_MAX_LEVEL`) the water is
the effects' water to a step. The cave view of B17a (two torches placed beside the camera) cannot be scripted any more: since B21 the scripted sequence (`--give`, `--place antorcha@...`, `--wait`) stalled a run at the wait for over 20 minutes in the effects' path (`--gl-shaders off`), so it is not the GLSL's; the cause was not looked for here, and the B21 report names a torch problem of the scripted `--place`), so the cave view is the landing without torches and the torches are the village street at midnight. The scripted runs on both paths are below.

**Frame times and the card's time** (`--stream-stats`, 240 frames, medians; interleaved effect and GLSL runs, 2 or 3 each in the valley views, 1 in the streamed ones; the machine was shared with up to four other agents' builds, tests and game windows, load 8 to 16 on 16 cores, and the card with their windows: read
the pairs, not the absolute numbers; the card's time is a `GL_TIME_ELAPSED` query round the chunk passes):

| View | Chunk passes, processor time to submit (effects / GLSL) | Card's time, median (effects / GLSL) | Frame p50 (effects / GLSL) | Draw calls, quads (GLSL) |
|---|---|---|---|---|
| meadow | 1.17 / 0.36 ms | 0.34 / 0.21 ms | 4.6 / 3.2 ms | 1,258 and 455k |
| wood-dusk | 2.67 / 0.93 ms | 1.46 / 0.91 ms | 6.7 / 4.6 ms | 1,041 and 362k |
| cave | 1.83 / 0.51 ms | 0.55 / 0.38 ms | 6.2 / 4.0 ms | 1,903 and 741k |
| torches | 2.26 / 0.32 ms | 1.07 / 0.16 ms | 4.8 / 2.3 ms | 567 and 198k |
| shore | 2.49 / 1.42 ms | 2.04 / 1.99 ms | 6.5 / 6.3 ms | 1,675 and 613k |
| under | 3.80 / 0.35 ms | 0.62 / 0.57 ms | 7.0 / 3.0 ms | 514 and 163k |
| village | 1.56 / 0.52 ms | 1.14 / 1.04 ms | 5.7 / 4.5 ms | 567 and 198k |
| grass | 2.11 / 0.68 ms | 4.62 / 0.36 ms (the effects run met another agent's load on the card; its frame p50 was 37 ms) | 37.4 / 5.0 ms | 1,453 and 463k |

The processor's cost of submitting every chunk pass falls to between a quarter and a tenth (the draw calls are the same; the state is set 3 or 4 times a frame instead of once per band and per pass, and each chunk is a buffer bind, three attribute pointers and one `glDrawElementsBaseVertex`).
The card's time is lower or equal in every pair: the card does the same work, with fewer state changes between draws. Nothing is slower; the frame is not the processor's submit time, so the frame p50 moves by 0.2 to 4 ms and in a loaded run by more.
Start-up costs a little more (the compile, link and the copies of the atlas and 16 water frames: tens of milliseconds, once). No GL error was raised in any run (`gl: ... 0 GL error(s) cleared`).

## What B17c inherits

- One program, one vertex array, one index buffer and one state scope: B17c's light as data goes into the vertex (the 24-byte `MeshVertex` has its colour as the baked light today; a second packed field, or the colour's bytes read as sky, block and occlusion, is a change of the mesher and of `chunk.vert.glsl`'s
  `vColour`, both in one place) and into uniforms the pass already sets; the fog and the far view can move to the same program, and the far view and the sky are the passes still on MonoGame, to move into the scope when B17c does the far view on the same light model.
- Each effect is a toggle: `SettingId` plus a `QualityPreset` field plus a row under "Shaders and effects", and `SceneDrawer.Apply...` as for `ApplyShaders`. The pass can be given uniform switches without a remesh when the effect is a shading step (a uniform per effect).
- The **fallback has to stay a picture the effects can draw**: a B17c effect that has no effects' version only exists on the GL path (the toggle and the log line say so), so B17c's default look must be the effects' look with every new effect off, or the fallback changes the game.
- The scope forbids a MonoGame call inside it, and the figures, the plots' outlines and the highlight are drawn before it (opaque and depth tested, so only exact ties changed: none were seen). B17c's translucent effects that need those drawn after the water (name plates are MonoGame sprites, drawn by the overlay after the scope) keep the rule.
- A switch in play remeshes the view (seconds of the world filling in): if an effect ever needs different vertex data, the same path applies; if it does not, a uniform is all it costs.
- Live tests share one MonoGame window (a third window in the test host hangs): new live tests use `GlProbeGame.Shared` and put the state back.
- Still not done: the atlas as an array texture, a compact vertex, indirect and multi-draw (B19), the context request (default 4.6 compatibility here, unknown elsewhere; `GlInfo` checks the version and the log says which), macOS (out of scope).


## The scripted runs, both paths

`scripts/scripted-runs.sh` with `GAME_BIN` a one-line wrapper that adds `--gl-shaders off` for the effects' capture, the default for the GLSL one, then `scripts/compare-runs.py`: **r14-third** (the behind-the-shoulder avatar), **r15-grey-dusk** (greyscale, dusk) and **r31-lake** (walks into the lake: the
surface, under water through the render target, the shore): every file of every run (6 pictures, saves, the session logs, stdout) is **byte for byte the same** on both paths, apart from the four `gl:` start-up lines the GL path prints in stdout (the context's facts and the path in force);
`compare-runs.py` now masks `gl:` lines, so two captures of the same steps compare equal whichever path drew them. **Not compared:** r42-camp, r01-route and r40-inn place a torch: r42-camp stalled at its `--wait` after the placed torch in the effects' path for 8 minutes and was killed (the same stall as the cave view's torches, above; not
the GLSL's); the other scripted runs draw the same scenes as the views of the table.

## The gate

Build `-warnaserror` 0 warnings (the solution, Debug; the game in Release); `scripts/test.sh full`: 12,518 passed, 8 skipped (3 model-gated, and the 5 live-GL tests of this file, which skip without `VOCATERRA_GL_TESTS=1`), 4 failed on the first run: two wall-clock tests that fail when the machine is loaded
(`SceneBuildTimeTests.Seed42SceneMatchesTheGoldenAndLoadsInUnderThreeSeconds`, `StreamedWorldTimeTests...WithinBudget`: both pass alone, 82 of 82 with the other changed tests, 14 s), and two of this milestone's own that the Graphics group's new sub-heading and a file list needed (fixed: 82 of 82). With `VOCATERRA_GL_TESTS=1`: 17 of 17 of `GlShaderTests` on the RTX 3070
(the program compiles and links, every embedded source compiles, a broken one is reported with the driver's log, the scope restores what it captured after every kind of draw, and a quad drawn through the pass and through `BasicEffect` to the back buffer and to a render target gives the same pixels).
`dotnet format --verify-no-changes` clean; `scripts/content.sh validate` OK (lint 0 errors; game-v1's bundle unchanged, the locale bundle's manifest refreshed for the two new strings); licence scan (the gate's, offline) PASSED; no package added.

**What the plans should say** (the documents this milestone may not edit): `docs/game/implementation-plan.md` section 4.14, row B17b: done, with this section and the numbers above; `docs/game/biomes-plan.md` section 3.9: B17b's items 1, 2, 3, 5 and 6 of the spike are done and the GL path is the default (on; the Shaders row; the fallback automatic);
`docs/game/decisions.md`: D-game-34 item 43 is met for the GL path (a Shaders toggle in a "Shaders and effects" group of the Graphics settings, on by default, a quality level sets it, B17c's effects join it each with its own toggle).


---

# B17c: light, fog, water and the far view in the shaders (2026-11-17)

Goal (D-game-34 items 41 and 43; `docs/game/biomes-plan.md` section 3.9 items 2 to 7): move the light, the fog and the water into the shaders, put the far view on the same program and light model, and give every added effect a toggle in the settings page's Graphics group.
Branch `B17c-light-in-shaders`, code in `src/Vocaterra.Game/Graphics/Gl`, `Graphics/World` (`LightData`, `LightModel`, `ChunkMesher`), `Graphics/Sky` (`SunModel`) and `Graphics/Far`.

## What moved

| Piece | Where | What changed |
|---|---|---|
| **Light as data** | `LightData`, `MeshBuffer.Lights`, `ChunkMesher` (`MeshOptions.Data`) | A chunk mesh built for the program has the **material** in the 24-byte vertex's colour and, in a second 4-byte stream, a **light word** a corner: bits 0 to 7 sky light, 8 to 15 block light, 16 to 17 corner occlusion, 18 to 20 the face (east, west, up, down, south, north, `Flat`, `Fixed`), 21 to 23 flags (the water's wave), 24 to 31 an extra byte (a flat face's grey level; the water's depth). The mesher is still a pure function of its snapshot (tested byte for byte on a worker and inline). No band, no tint, no glow mesh in this path. |
| **The shader's light** | `chunk.vert.glsl` | `material * faceShade(sun) * occlusionKeep * sky * sunColour + amber * glowStrength * block` per vertex (so the interpolation across a face is the baked one's), with the sun's direction and colour, the glow's strength, the tints and the occlusion table as uniforms (`GlLightFrame`; the tints and the table are `ChunkMesher`'s own numbers, one source for the shader and the fallback). `LightModel` is the same maths in C#; `LightModelTests` hold it to the baked mesh of every band to the byte, and a live test holds the shader to `LightModel`. |
| **The sun** | `SunModel` | The faces are shaded by the afternoon's fixed sun while the real sun is high (from 42 degrees of elevation: all the midday), by the real sun low down (held at least 7.5 degrees up while it is above the horizon, as B22's dusk band stood it), and by the moon at midnight at night, turned along the great circle through the dusk's whole twilight: a function of the sky's look with no step (the largest step in a minute of game time is under 3 degrees; B22's bands jumped). The sun's colour is the look's `SunLight`. |
| **Fog by height** | `chunk.vert.glsl` | An exponential density along the ray from the eye to the vertex (the closed form), `1 - exp(-density)` mixed with the linear distance fog, in the fog's colour (the look's horizon colour: the haze of the far view), thicker with the weather's fog, none under water. Near chunks and far tiles alike (one program). |
| **The water shader** | `chunk.vert.glsl`, `chunk.frag.glsl`, `AddWater*` in the mesher | B22's depth tint is now computed in the shader from the depth the surface corner carries; the surface's top corners (and the top edge of the sides) move with three sines of the world position and the game's time (0.035 block at most; the sides follow, no seam); a view at a grazing angle takes the haze's colour and turns more opaque (the facet's normal from the screen derivatives). No reflection pass. |
| **The far view** | `FarTerrainRenderer.Draw` with a `FarGlDraw`, `GlChunkPass` (`FarLand`, `FarLit`, `FarWater`), `GlFarBuffers` | The far tiles upload into buffers of the pass and are drawn in a scope of their own (their projection and depth range, then a depth clear): colours baked in the Day's sun as before, brought by the fragment stage to the sun of the moment by each face's own direction (screen-space derivatives of the position, `shade(n, sun) / shade(n, daySun)`, exactly 1 at the Day's sun), with the same fog and fog by height. Its textures are mipped copies: the atlas to the level where a texel is a whole cell, and the noise. Tiles uploaded for the other path are carried over a few a frame when the setting or the shaders change; the climate colours are baked in the tiles, so their toggle makes the tiles again. |
| **The toggles** | `SettingId`, `PlayerSettings`, `SaveSettings`, `MenuRows`, `GraphicsEffects`, `QualityPreset`, `LaunchOptions` | Five rows under *Shaders and effects* with Shaders, and B18's Grass and a new Antialiasing row moved in: sun follows the hour, climate colours, water shader, fog by height, far view light. Each is on by default, written to a save only when off (a save of the default is byte for byte what it was), a launch option each (`--sun-follows-hour`, `--climate-colours`, `--water-shader`, `--height-fog`, `--far-light`, `on|off`), set by the quality level (Low: water shader and fog by height off), applied at once. |

## The effects' path is the fallback (what was chosen)

With Shaders off, no GL 3.3 or a program that does not compile, the renderer draws with MonoGame's effects and the mesher builds as it always did (`MeshOptions.Data` false: the colours baked from the same inputs, the four bands, the glow mesh). Nothing of the light is data there. A hash of every vertex of every mesh of the
scenes in every band, recorded at the base commit before any of this moved, is a test (`BakedLightGoldenTests`); the existing tests of the bands, the depth darkening, the glow, the bed and the water pass unchanged. The toggles that are uniforms in the program (the sun, the water shader) are baked in the fallback: the sun's setting picks Day for every band and the older dusk's light (one band, so no remesh at an hour), the water's the baked tint; the fog by height and the far view's light need the program.

## The texture array (what was chosen)

**Not adopted: the atlas stays one texture.** The bleeding between cells that an array removes is where a texel is more than one cell, and the game never samples there. The atlas's mips are made cell by cell up to level 4, where a texel is a whole 16 x 16 cell (`AtlasMipBleedTests` changes one cell and
checks that no texel outside it moves at any of levels 0 to 4); the far view's sampler (and the GL copy of it, `TryAddFarTextures`) stops at level 4; and the chunks are at most a view distance away, where a block is 2.3 pixels at 1280 wide, 4.6 at 2560 and 6.9 at 3840 (the level the card picks is 2.8, 1.8 and 1.2). An array
would also have put a layer index in every vertex (the mesher's and the far mesher's), for a defect nothing shows. Revisit if the render distance of the chunks grows or the atlas gains cells with their own alpha edges.

## The comparison

Scripted runs (one game run a view, or one run with several `--shot` steps; the steps of each view are in `index.md` beside the pictures; Release, High, seed 42, 1280 x 720, a fixed 60 Hz scripted clock, 240 frames after the steps; the base is the build of `25d8c494`, B17b's merge, run on its own content: the new settings strings are orphans to it) with the defaults of B17c (every effect on); pictures and the
differences, `/home/james/Dev/Vocaterra/artifacts/b-series/b17c/` (outside git, `index.md`). Tolerance as B17a's and B17b's: at most 0.1 percent of the pixels differing by more than 8 of 255 (`scripts/gl-compare-images.py`).

| View | Where | Differing at all | By more than 8 | Largest | Within the tolerance |
|---|---|---|---|---|---|
| meadow, midday | 380,217 east, the streamed world | 367,124 | 7 (0.0008%) | 12 | yes |
| cave | the landing 468,62,153 | 472,227 (none by more than 2) | 0 | 2 | yes |
| village, midday | the street 266,223 | 604,307 | 1 (0.0001%) | 13 | yes |
| rain, midday | the meadow, `--set-weather rain` | 368,067 | 6 (0.0007%) | 11 | yes |
| meadow at dusk (21 h) | | 499,627 | 78 (0.0085%) | 21 | yes |
| wood at dusk (21 h) | the valley wood 196,206 | 592,578 | 116 (0.0126%) | 46 | yes |
| meadow at night (0 h) | | 279,498 | 7 (0.0008%) | 10 | yes |
| night village, torches and lanterns | 266,223 at 0 h | 442,085 | 2 (0.0002%) | 17 | yes: the block light in the faces' vertices is the old glow pass's picture |
| lake shore, midday | the pool above the waterfall 258,106 | 489,627 | 7,663 (0.83%) | 140 | no: the water shader (below) |
| under water | 262,88,136 | 449,773 | 4,792 (0.52%) | 49 | no: the water shader's surface seen from below |
| far view, midday | 812,762 east | 331,790 | 47,033 (5.1%) | 72 | no: the fog by height on the far land (a band 58 rows tall) |
| far view, dusk (20.5 h) | | 386,387 | 62,860 (6.8%) | 58 | no: the same, and the far view's own light |
| lake shore at 20 h | | 514,687 | 348,873 (37.9%) | 112 | no: the sun following the hour (the golden hour: the Day band's fixed afternoon sun before, the real low sun after) |

**Each effect off** (the same scripts with the option; against the base's picture of the same view and tick; the pictures are in `artifacts/b-series/b17c/`, `off-*`). An effect off is the picture without the effect, which for B22's three (the sun, the climate colours, the depth tint) is a picture of before B22 and not the base's:

| Effect off | View | Differing by more than 8 | What it is |
|---|---|---|---|
| fog by height | far view, midday | 14 pixels (0.0015%), largest 77 | the base's picture: the far view drawn through the program is the old far view's, so the 5 percent above is the haze alone |
| Shaders (the fallback, MonoGame's effects) | lake shore, midday; the night village | 0 (7 pixels differ by 1 of 255); 0 | the base's picture to the pixel: the fallback bakes today's colours |
| sun follows the hour | meadow at dusk, far view at dusk | 55.2%, 34.6% | the afternoon sun at every hour and the older, lighter dusk (B22's dusk darkening and its low sun are gone, as the setting says) |
| climate colours | meadow, midday | 2.3% (largest 12) | the colour map's middle everywhere (the near and far land) |
| water shader | lake shore, midday | 3.6% (largest 190) | plain water: no depth tint, no sheen at the edge, no movement |
| far view light | far view, dusk | (a picture only) | the far view drawn by MonoGame's effects in the day's baked light, as before; the chunks in front still take the shader's |
| all five | meadow; the shore; the far view | 2.5%; 0.6%; 32.5% | the climate colours and the depth tint are most of it; nothing else differs at midday |

The first rows are the point: with the light moved into the shader the pictures at midday, in the cave, in the rain, at dusk, at night and by torchlight are today's, to a step or two of rounding (the cave's largest difference anywhere is 2 of 255), and the lanterns' glow, now the block light of the faces' own corners and one pass, adds
up to the same picture as the old second pass (2 pixels differ by more than 8). The views outside the tolerance are each an effect that is new and on by default, explained by its row.

## Times

`--stream-stats`, Release, High, interleaved before / after pairs (two runs each; the machine was shared with up to four other agents' builds, tests and game windows, load 10 to 20, and the card with their windows: read the pairs, not the absolute numbers; the shore's second "before" was killed by the load, so it has one pair and a lone "after").
The card's time is the `GL_TIME_ELAPSED` query round the chunk passes (B17b; the far view's scope is a pass of its own and is not in it, before or after).

| View | Chunk passes, processor time to submit (before / after) | Card's time, median (before / after) | Frame p50 (before / after) | Draw calls and quads (after) |
|---|---|---|---|---|
| shore, run 1 | 1.39 / 1.20 ms | 0.56 / 0.48 ms | 4.3 / 3.7 ms | 1,656 and 609k |
| shore, run 2 | (killed) / 1.05 ms | (killed) / 0.38 ms | (killed) / 3.2 ms | |
| torches, run 1 | 0.33 / 0.41 ms | 0.28 / 0.16 ms | 2.5 / 2.4 ms | 557 and 195k |
| torches, run 2 | 0.75 / 0.94 ms | 1.06 / 1.07 ms | 5.7 / 5.9 ms | |
| meadow, run 1 | 0.51 / 0.92 ms | 0.24 / 0.26 ms | 3.9 / 24.0 ms (after: another agent's window on the machine) | 1,258 and 455k |
| meadow, run 2 | 1.27 / 0.98 ms | 4.23 / 0.36 ms (before: the card shared) | 40.9 / 6.2 ms (before: load) | |

Read as the B17b table was: the processor's time to submit stays at 0.3 to 1.4 ms either side (the passes are the same draw calls; the glow pass's draws are gone and the program sets a few more uniforms a frame), the card's time is equal within the noise of the shared card (the two meadow pairs show the card and the processor of the other agents' runs more than the game's), and the frame
is not the passes' time. Nothing is slower beyond noise; the far view's scope is not in these figures, before or after.

**A change of the hour**, before (the effects' band baking, and B17b's program too: the band is baked into each mesh) and after: the same run held at 13 h for six seconds and the same run with the clock set to 21 h after three (`--set-time`), Release, High, the streamed meadow, the workers' own count of meshes built (the `stream:` line):

| | held at 13 h | 13 h then 21 h | the change costs |
|---|---|---|---|
| before (B17b: the Dusk band is baked into the meshes) | 7,543 meshes in 7.1 s of worker time, frame p50 6.0 ms | 13,372 meshes in 26.2 s, frame p50 43.1 ms, the longest frame 4.6 s | 5,829 meshes built again, 19 s of the workers, a stall |
| after (the sun is a uniform) | 7,543 meshes, frame p50 3.3 ms | 7,543 meshes in 8.5 s, frame p50 5.1 ms, the longest frame 45 ms | none: a change of the hour builds nothing (a test holds the pipeline's job count over a whole day) |


**A torch placed** re-meshes the chunks its light reaches (12 chunks at the village's first torch) before and after, since the block light is in those chunks' vertices; what is gone is the glow mesh (a second set of quads for the faces in reach, 4,244 quads against 3,867 for the 12 chunks) and the band: the time to mesh the 12 chunks, measured in the mesher alone, is 12.8 ms baked and 12.5 ms as data (three runs each, the whole
view of 566 chunks 860 to 970 ms baked and 863 to 875 ms as data), so the processor's cost of a torch is the same re-mesh as before and its light is exactly its own (no colour round it is baked).

## What B23 inherits

- **The two channels are defined and filled.** A corner's sky and block light are bytes of the light word (`LightData`): 255 is level 15 and a level `L` of B23's 0 to 15 is `LightData.FromLevel(L)` (`L * 17`; `LevelOf` the other way), so the propagated levels go in as they are and the shader needs no change. Today's sources fill them in one place each: `ChunkMesher.CornerWord` (a cube corner: the sky byte is B12's darkening by depth,
  `CornerSky`'s mean over the cells that meet at the corner; the block byte is the reach of the lanterns', torches' and lava's glow, `Glow.At`), `Open` (the pieces with no source of their own: plants, grass tufts take the sky share of their cell, the water sky 255), and the fixed word of the self-lit pieces. B23 replaces those two values by the propagated levels of the corner's cells (the same average) and keeps
  `ChunkLightIndex`'s invalidation for the chunks a light reaches.
- **What the shader does with them:** the sky byte scales the sun's light (so a cave's floor is the dark of its sky level, with the cave minimum of B12 (`SkyDepthLight.CaveMinimum`, 0.28) in the byte today: B23 can move it into a uniform as an ambient floor), the block byte adds amber times the glow's strength (`uGlowColour`; a coloured light would need a colour in the word: byte 3 is free outside the water and the flat faces, and two flag bits are).
- **The fallback must bake from the same words.** `Data` false still bakes with the old rules (the bands, the glow mesh, `CornerSky`); B23's propagated light has to reach it too (its brief says "in vertex colours with the built-in effects"): `LightModel.Corner` is the one place that turns a word and a sun into a colour, so the baked path can take B23's levels the same way (a CPU step from the words), with `BakedLightGoldenTests` to be re-recorded for the intended change.
- **No re-mesh for the hour or the weather, ever:** a change of light is a few uniforms (`GlLightFrame`); a torch placed or a shaft dug re-meshes the chunks its light reaches (their words), the same chunks and the same cost as before (12 chunks, 12.5 ms at the village's first torch).
- The vertex is 28 bytes in two streams (24 + 4); the light word has 2 spare flag bits and a free extra byte for what B23 needs next.

## The scripted runs, both builds

`scripts/scripted-runs.sh` with `GAME_BIN` a wrapper (the base's on its own content; B17c's; B17c's with the five effects off), then `scripts/compare-runs.py`: **r14-third** (the behind-the-shoulder avatar), **r15-grey-dusk** (greyscale, dusk) and **r31-lake** (the lake: the surface, under water through the render target, the shore). Every file of every run that is not a picture (the stdout, the session log, the replay, the saves) is **byte for byte the
same** as the base's (after `compare-runs.py`'s masks); only the pictures differ, and each difference is an effect on by default:

| Picture | Differing by more than 8 | Why |
|---|---|---|
| r14-third (the avatar, the run's clock starts at 8 h) | 17.0% | the sun following the hour: at 8 h the sun stands 27 degrees up, so the faces take the morning sun's shade where the base used the afternoon's fixed one |
| r15-grey-dusk (greyscale, `--dusk`) | 5.7% | the sun at dusk (7.5 degrees up in the west) against B22's dusk band, in grey, and the far land's haze |
| r31-lake: water, land, land-north, shot | 16.7%, 22.0%, 0.44%, 0.65% | the morning sun again (the first two face the morning sun), the water shader's surface and edge, the fog by height over the far land |
| the same runs with all five effects off: r14-third, r31-lake water, r31-lake land | 0.28%, 0.015%, 11.0% | what is left is the climate colours (the grass and leaves in the colour map's middle) and the water's depth tint, which are B22's effects and so also off; the lake's water picture, which has no land in its middle, is the base's to 136 pixels |

## The gate

Build `-warnaserror`: 0 warnings, 0 errors (Debug, the solution); `scripts/test.sh full` with `VOCATERRA_GL_TESTS=1`: **12,590 passed, 0 failed, 3 skipped** (the model-gated tests; the base was 12,539), the live GL tests among them (the program compiles, every embedded source compiles, the scope restores everything it captured after every kind of draw including the far kinds,
a quad through the pass equals the effects' picture on the back buffer and a render target, and the shader's light of nine words equals `LightModel` to 2 of 255). New tests: `LightDataTests`, `LightModelTests`, `DataMeshTests`, `SunModelTests`, `BakedLightGoldenTests`, `GraphicsEffectsTests`, `FarGlFormTests`, `AtlasMipBleedTests`, a far-view recolour test, `PlayerSettingsTests` additions and the live numeric test. `dotnet format
--verify-no-changes` clean; `scripts/content.sh validate` and the A2 lint clean (game-v1's bundle unchanged; the locale bundle's manifest refreshed for the new strings: `scripts/content.sh refresh`); the gate's licence scans (offline) PASSED, no package added.

**What the plans should say** (the documents this milestone may not edit): `docs/game/implementation-plan.md` section 4.14, row B17c: done, with this section's numbers (the midday, cave, rain, dusk, night and torch views equal the base's to a handful of pixels; the effects' fallback to the pixel); `docs/game/biomes-plan.md` section 3.9: items 2 to 7 are done, the texture array is not adopted (this section says why), a placed torch re-meshes the chunks its
light reaches as before (the block light is in their vertices) and a change of hour, weather or glow builds nothing; `docs/game/decisions.md`: D-game-34 item 43 is met for B22's effects and B17c's (five rows and Antialiasing in the Graphics group, the quality level sets them), B23 follows with the channels defined here.
