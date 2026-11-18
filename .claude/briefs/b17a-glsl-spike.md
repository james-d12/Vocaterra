You are implementing B17a, "A GLSL spike on MonoGame's context", for the Vocaterra repository (C#, .NET 10). The game is
Vocaterra.Game.Core (the engine-free logic), Vocaterra.Game (MonoGame DesktopGL 3.8.6) and Vocaterra.Host; the language core (Vocaterra.Nlp*)
is not yours. This is a **spike**: prove that the game can draw its chunks with its own OpenGL shaders, compiled at run time, inside
MonoGame's frame, and report on the fit. It must not change how the game looks or plays by default.
Where to work: the worktree /home/james/Dev/Vocaterra/.claude/worktrees/b17a, already on the local branch `B17a-glsl-spike` (cut from
`feature/biomes` at 87a2809e). Run every command from that directory. Commit after each step that builds and passes its tests, with the
prefix `B17a: `. Do not push. Never push or merge feature/biomes or main. Leave the worktree clean on its branch.
Resources: every dotnet restore, build, test and format, every game run and screenshot through scripts/limited.sh (builds `-m:4`, tests
`-m:2`; the script's caps); one heavy command at a time, no `&`, no wait loops on `pgrep -f`. Four other agents build at the same time:
expect waits. Scratch on disk under /home/james/Dev/Vocaterra/artifacts/tmp/b17a/, removed at the end. Environment: prefix every command
with PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1; game runs need DISPLAY=:0 and
VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models. Game windows are allowed; look at every picture with the Read tool. The machine
has an NVIDIA RTX 3070 on Linux.

The owner's decisions (docs/game/decisions.md, D-game-34 items 30, 39 and 40): custom shaders are planned as B17 (docs/game/biomes-plan.md
section 3.9); macOS is out of scope; the shader route is **(c) raw GLSL compiled at run time** by the driver, drawn through an MIT OpenGL
binding (Silk.NET or OpenTK) on MonoGame's own context, with MonoGame's GL state saved and restored round the passes, and it opens OpenGL
4.x for B19's later steps. Today the game draws with MonoGame's built-in `BasicEffect` and `AlphaTestEffect` only, because MonoGame's shader
compiler needs Wine on Linux; all light is baked into vertex colours. Read docs/research/2026-11-16-rendering-and-distance-review.md section
3 (the GL entry points MonoGame 3.8.6 has and lacks: no VAOs, no base-vertex draws, no buffer storage; its GL state cache) and the code:
src/Vocaterra.Game/Graphics/World/VoxelRenderer.cs (the passes, the effects, fog), Graphics/SceneDrawer.cs, VoxelGame.cs (the device).

Rules: a game milestone edits only the game projects, their tests, content/game-v1, content/schema, assets/ and docs/game/ and takes the
core as it is. A regression check is a C# test. Never edit .github/workflows/, either implementation-plan.md, either decisions.md or
docs/game/biomes-plan.md. Engine code only in src/Vocaterra.Game. **A new package is a licence decision:** add only an MIT or Apache-2.0
binding (say which, its licence and version, and why); run the licence scan (`scripts/merge-gate.sh` runs it; or the scan script it calls)
and say its result. Keep off what other agents change now (Graphics/Far, World/Lod, Streaming, ChunkMesher's light and colours: B22, B07f).
The default game is unchanged: the GLSL path only behind a launch option (`--gl-shaders` or similar) and off by default.

Deliverables:
1. **Get the GL context:** from MonoGame's window, load the binding's GL functions on the same context (no second context); log the
   `GL_VERSION`, `GL_RENDERER`, `GL_SHADING_LANGUAGE_VERSION` and whether the context is core or compatibility, and which 4.x features are
   present (compute, multi-draw indirect, buffer storage, clip control).
2. **One GLSL program** (a vertex and a fragment shader, as source files embedded in the assembly, compiled at start with clear errors in
   the log on failure) that draws the opaque chunk pass the way `AlphaTestEffect` does today: the atlas texture, the vertex colour, the alpha
   test, the same linear fog. Behind the option, the opaque chunks draw through it; everything else (cut-outs, water, the far pass, the
   sky, the UI) stays on MonoGame.
3. **State:** save what MonoGame's state cache believes (program, bound buffers and textures, blend, depth, rasteriser, viewport, attribute
   arrays) and restore it after the GLSL pass, so MonoGame's later passes and the UI draw correctly; say how you did it and the risks (the
   cache going stale; anything you had to reset).
4. **A comparison:** a picture of the same view with the built-in effect and with the GLSL path (Release, High, midday, a meadow and a cave
   with a torch), pixel-compared within a tolerance (state the number; a test if it can run headless, else a scripted check); frame times
   both ways with `--stream-stats`.
5. **A report for the rest of B17** (as `docs/game/b17-spike.md`, in docs/game/): what works, the binding's fit, the state hand-over's risks,
   what the GL version allows (compute, indirect draws for B19), the measured costs, and a recommendation (go, or what to change) with the
   size of the remaining B17 work.
6. Tests: the shader sources load and compile (a test that skips without a GL context, if needed), the option is off by default, the
   built-in path is unchanged.

Gate: build with -warnaserror (0 warnings); scripts/test.sh full (12,448 passed before, plus your new tests); format clean; content
validate; the licence scan clean. Commit messages end with
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDbkPTqABsu7sLVD9h6

Report, half a page: branch, head commit, worktree; the binding (name, version, licence); the GL context found; what was drawn and how the
state is handed over; the comparison and frame times; the recommendation; anything left. No file dumps or long logs.
