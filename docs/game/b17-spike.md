# B17a: a GLSL spike on MonoGame's context (2026-11-17)

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
