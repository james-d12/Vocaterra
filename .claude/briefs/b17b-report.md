B17b report (agent ad35c5e0ea5b1fbcc, Sonnet 5.5 high, 2026-11-17 about 12:50 UTC)

Branch `B17b-gl-passes`, head `4bc67f13` (three commits on 91fe07ec), worktree `.claude/worktrees/biomes/.claude/worktrees/b17b` (nested).
- Every chunk pass (faces, lit pieces, cut-outs, B18's grass, glow, water) through `GlChunkPass` and one GLSL pair; the position as four
  dot products (glow depth bit-identical); uploads in `Install` on the game thread. Far view, sky, figures, rain and UI stay on MonoGame.
- One state scope a frame (`GlStateSaver` adds blend function and equation, unpack alignment, primitive restart); no MonoGame call inside
  (figures, plot outlines and highlight drawn before it; a source test enforces it). Under water: a `uFlipY` uniform and clockwise front.
- GL on by default; a "Shaders" row in the Graphics group ("Shaders and effects"), saved only when off (default saves unchanged);
  `--gl-shaders on|off`; `QualityPreset.Shaders`; switching in play needs no restart (a brief remesh). Below GL 3.3 or a compile failure:
  the effects, one `gl:` log line. `GlCardTimer` (GL_TIME_ELAPSED) in `--stream-stats`.
- Comparison (Release, High, 1280x720, artifacts/b-series/b17b/): meadow, wood-dusk, cave, torches (village at hour 0), under water,
  village, grass: 0 pixels differ; shore 3 pixels by 1/255. Scripted r14, r15, r31 byte-identical bar `gl:` lines (`compare-runs.py` masks
  them). Times (interleaved, shared machine): processor submit a quarter to a tenth (meadow 1.17 to 0.36 ms, under water 3.80 to 0.35);
  card time equal or lower in every pair; frame p50 lower (meadow 4.6 to 3.2, torches 4.8 to 2.3).
- Suite: 12,518 passed, 4 failed first run (two timing tests passing alone; two of its own, fixed: a subheading count, a stale allow-list
  entry); the full suite not rerun after the fixes. GlShaderTests 17 of 17 with `VOCATERRA_GL_TESTS=1`. Format, content, licence clean.
- Risk: placing torches in a scripted run then `--wait` stalls (also in the effects path) since B21; B17a's torch cave view could not be
  retaken; r42-camp, r01-route, r40-inn not compared. For B17c: light still in the vertex colour; far view and sky on MonoGame.
