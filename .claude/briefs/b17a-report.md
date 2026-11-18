B17a report (agent a4e0d4c8b9ed3f258, 2026-11-17 about 04:00 UTC)

Branch `B17a-glsl-spike`, head 0eb63042 (32e04348, 0eb63042 on 87a2809e), worktree clean, not pushed; pictures and run logs in
artifacts/b-series/b17a/. Full report: docs/game/b17-spike.md (on the branch).
Binding: Silk.NET.OpenGL 2.22.0 (MIT), with Silk.NET.Core and Silk.NET.Maths 2.22.0 and Microsoft.Extensions.DependencyModel (MIT);
Microsoft.DotNet.PlatformAbstractions 3.1.6 reads UNKNOWN offline (licence file only; read: MIT). Only Vocaterra.Game references it;
`AllowUnsafeBlocks` on for a DrawElements offset. The gate's scan (core + Game.Core.Tests) passed; a scan of Vocaterra.Game (not in the
gate) passes the new packages but FAILs or UNKNOWNs on packages already there (FontStashSharp Zlib, MonoGame's SDL and OpenAL, NVorbis).
Context: no second context; functions from SDL_GL_GetProcAddress. RTX 3070: OpenGL 4.6.0 NVIDIA 620.70.06, GLSL 4.60, compatibility
profile; compute, multi-draw indirect with count, buffer storage, clip control, base vertex, SSBOs present. Run-time version check falls
back to the effects.
Drawn: behind `--gl-shaders` (off by default) the opaque chunk pass through one GLSL 330 program (embedded sources; errors logged with the
driver's log): atlas, band tint times vertex colour, alpha test 110/255, the same linear fog; its own VAO, atlas copy and per-chunk
buffers. Everything else on MonoGame; under water (render target) it falls back.
State: `GlStateSaver` reads the real GL state with glGet before and restores it after (program, VAO, array buffer, texture unit, binding
and sampler, enables, depth func and mask, cull, front face, colour mask, viewport, depth range), drains GL errors; uploads on
COPY_WRITE. Risks met: `Texture2D.GetData` binds MonoGame's texture (atlas drew black; fixed); depth must match bit for bit with passes
that use LEQUAL (the glow pass failed in triangles with matrix * position; fixed with four dot products); render targets not handled.
Comparison (Release, High, midday, 1280x720, scripts/gl-compare.sh; tolerance at most 0.1% of pixels off by more than 8/255): meadow
(380,217) and cave with two torches (468,62,153): 0 of 921,600 pixels differ. Opaque-pass submit median: meadow 1.06 to 0.81 ms, cave
1.31 to 0.73 ms. Whole-frame times not comparable at load 10 to 18. Start-up +55 ms. No GL errors. No GPU timer queries.
Recommendation: go with raw GLSL on MonoGame's context: all chunk passes on the program in one step (passes sharing depth by equality
share a program), one saver scope per frame, a render-target flip uniform, own the vertex data, settle the context request before B19
(4.3+), add timer queries. The rest of B17 stays L.
Gate (its steps run alone, not merge-gate.sh): build 0 warnings; full 12,455 passed, 5 skipped (+8; 2 live-GL tests skip unless
VOCATERRA_GL_TESTS=1, passed on the 3070), 1 failed: the valley timing test (passes alone); format clean; content validate OK.
NoLiteralTextTests allow-list +4 GL files.
