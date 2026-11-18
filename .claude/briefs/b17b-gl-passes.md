You are implementing B17b, "Every chunk pass on the game's own GLSL", for the Vocaterra repository (C#, .NET 10). The game is
Vocaterra.Game.Core (the engine-free logic), Vocaterra.Game (MonoGame DesktopGL 3.8.6) and Vocaterra.Host; the language core
(Vocaterra.Nlp*) is not yours. B17b is the first half of B17 (rendering on the graphics card); B17c (light as data, fog, water, the far view
on the same light model) follows it, then B23 (propagated light). Keep the vertex layout open for B17c, but do not move the light yet.
Where to work: the worktree /home/james/Dev/Vocaterra/.claude/worktrees/b17b, already on the local branch `B17b-gl-passes` (cut from
`feature/biomes` at 91fe07ec). Run every command from that directory. Commit after each step that builds and passes its tests, with the
prefix `B17b: `. Do not push. Never push or merge feature/biomes or main. Leave the worktree clean on its branch.
Resources: every dotnet restore, build, test and format, every game run and screenshot through scripts/limited.sh (builds `-m:4`, tests
`-m:2`; the script's caps: 10 GB a command, 16 GB in all, three commands machine-wide); one heavy command at a time, no `&`, no wait
loops on `pgrep -f` or on a file (a loop you leave behind runs forever). Other agents build at the same time: expect waits. Iterate with a
project or `scripts/test.sh fast`; `scripts/test.sh full` once at the end. Scratch under /home/james/Dev/Vocaterra/artifacts/tmp/b17b/
(not /tmp), removed at the end. Environment: prefix every command with PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet"
DOTNET_NOLOGO=1; game runs need DISPLAY=:0 and VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models; scripted runs use
`VOCATERRA_RUNS_WORK=/home/james/Dev/Vocaterra/artifacts/tmp/b17b/runs`; live-GL tests run with `VOCATERRA_GL_TESTS=1`. Game windows are
allowed (the owner is not at the screen); look at every picture with the Read tool. Measure and take pictures in a Release build. The
machine has an NVIDIA RTX 3070 on Linux.

Why: the owner, 2026-11-17 about 08:05 UTC: "Okay, you can kick off B17, that's fine" (D-game-34 item 41), after B17a's spike recommended
go. His earlier choices: raw GLSL compiled at run time on MonoGame's own context through an MIT binding (item 40; Silk.NET.OpenGL 2.22.0,
already referenced since B17a), macOS out of scope (item 39).

Read first: CLAUDE.md; docs/game/b17-spike.md (all of it: the state hand-over, the risks, the depth-equality trap, the recommendation's
six items); docs/game/biomes-plan.md section 3.9; docs/game/implementation-plan.md section 4.14 (your row); docs/game/architecture.md (the
drawing and the threads); src/Vocaterra.Game/Graphics/World (`VoxelRenderer` and its passes, the GL pass B17a added, `GlStateSaver`,
`GlInfo`, the shader sources), Graphics/SceneDrawer.cs, the underwater pass; .claude/briefs/b21-report.md (B21 just moved meshing onto
the job pool: uploads happen on the game thread through `ChunkMeshPipeline`; keep that path, upload into your buffers there).

Rules: a game milestone edits only the game projects, their tests, content/game-v1, content/schema, assets/ and docs/game/ and takes the
core as it is. A regression check is a C# test. Never edit .github/workflows/, either implementation-plan.md, either decisions.md or
docs/game/biomes-plan.md (say in the report what they should say). Engine code only in src/Vocaterra.Game. No new package without saying
so (a licence decision for the owner). The world, the simulation and the saves do not change (shaders never feed the simulation); the
goldens and the fixture stay. GL calls only on the game thread.

Deliverables (the spike's recommendation, items 1, 2, 3, 5 and 6):
1. **Every chunk pass on the GL program in one step:** the opaque faces, the lit pieces and the glow, the cut-outs (leaves and plants with
   the alpha test), B18's grass layer, and the water (translucent, sorted as today), drawn through the game's GLSL with the same look:
   texture times the vertex colour, the alpha test, today's linear fog. Passes that meet by depth equality share a program and the
   bit-identical position transform (the spike's dot-product form). The far view, the sky, the figures and the UI stay on MonoGame for now.
2. **One state scope per frame:** `GlStateSaver` becomes a scope round all the game's own GL work in the frame (one Capture and one
   Restore), with the state the spike listed as not saved added when a pass touches it (pixel store, primitive restart, polygon offset
   factors, blend function and equation for the water). No MonoGame call inside the scope.
3. **Render targets:** the scene drawn under water through the same passes (flip the clip y and the front face by a uniform when a target
   is bound, or give the scene its own target); no fallback count left over.
4. **The default and the fallback:** when the comparison below holds, the GL path becomes the default and `--gl-shaders off` (or similar)
   keeps the effects as the fallback; the effects are used automatically when `GlInfo` reports a context below 3.3 or a shader fails to
   compile (one clear log line). Say what you chose. **The owner (2026-11-17, D-game-34 item 43): "all the additional graphics we're
   adding probably should be toggleable via a setting."** So the GL path is a row on the settings page ("Shaders": on or off, saved, and a
   launch option), and the settings page gets a Graphics group that B17c's effects will join (each its own toggle, on by default, with the
   quality presets able to set them). Changing it in play takes effect without a restart if that is simple; otherwise say it needs one.
5. **The card's time:** a GL timer query round the world's passes, its median in `--stream-stats` (with the processor's submit time).
6. **Measured and compared:** `scripts/gl-compare.sh` widened to these views, effect against GL, Release, High: a meadow at midday, the
   valley wood at dusk (B22's dawn and dusk bands), a cave lit by torches, a pool or lake shore (water), under water, the village street,
   a view with B18's grass. Tolerance as B17a's (0.1 percent of pixels differing by more than 8 of 255); exact where it can be. The
   scripted runs' pictures compared on both paths (`compare-runs.py`); every difference explained. Frame times and the card's time both
   ways (interleave A/B runs: other agents load the machine).
7. Tests: the shader sources compile (live-GL, skipped without the variable); the state scope restores what it captured (live-GL); the
   default and the fallback options; the settings row saved and read back; the render-target flip; the built-in path unchanged.
8. Documents: docs/game/b17-spike.md gets a "B17b" section (what moved, the comparison, the measurements, what B17c inherits);
   docs/game/architecture.md (the drawing), the Game README, docs/game/running.md (the option).

Gate: build with -warnaserror (0 warnings); scripts/test.sh full (12,514 passed on feature/biomes before, plus your new tests); format
clean; content validate; the licence scan clean. A timing test that fails under load: rerun it alone and say so. Commit messages end with
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDbkPTqABsu7sLVD9h6

Report, half a page: branch, head commit, worktree; each deliverable and how; the comparison table; frame and card times; the default
chosen; risks left for B17c; anything left. No file dumps or long logs.
