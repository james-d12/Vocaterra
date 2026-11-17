You are implementing B17c, "Light, fog, water and the far view in the shaders", for the Vocaterra repository (C#, .NET 10). The game is
Vocaterra.Game.Core (the engine-free logic), Vocaterra.Game (MonoGame DesktopGL 3.8.6) and Vocaterra.Host; the language core
(Vocaterra.Nlp*) is not yours. B17c is B17's second half: B17b (merged) moved every chunk pass onto the game's GLSL program with today's
look; B17c moves the light, fog and water into the shaders and the far view onto the same program. B23 (propagated sky and block light,
0 to 15, Minecraft-style) follows you and fills the light data you define, so define it for that.
Where to work: the worktree /home/james/Dev/Vocaterra/.claude/worktrees/b17c (an absolute path; cd there first), already on the local
branch `B17c-light-in-shaders` (cut from `feature/biomes` at 25d8c494). Commit after each step that builds and passes its tests, with the
prefix `B17c: `. Do not push. Never push or merge feature/biomes or main. Leave the worktree clean on its branch.
Resources: every dotnet restore, build, test and format, every game run and screenshot through scripts/limited.sh (builds `-m:4`, tests
`-m:2`; the script's caps); one heavy command at a time, no `&`, no wait loops on `pgrep -f` or on a file (a loop left behind runs forever:
several agents left them today; bound game runs with `timeout`). Other agents build at the same time: expect waits. Iterate with a project
or `scripts/test.sh fast`; `scripts/test.sh full` once at the end. Scratch under /home/james/Dev/Vocaterra/artifacts/tmp/b17c/ (not /tmp),
removed at the end. Environment: prefix every command with PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1; game
runs need DISPLAY=:0 and VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models; scripted runs use
`VOCATERRA_RUNS_WORK=/home/james/Dev/Vocaterra/artifacts/tmp/b17c/runs`; live-GL tests run with `VOCATERRA_GL_TESTS=1`. Game windows are
allowed (the owner is not at the screen); look at every picture with the Read tool. Measure and take pictures in a Release build.

Why: the owner's go for B17 (D-game-34 item 41) and his rule that every added graphics effect has a toggle (item 43): "all the additional
graphics we're adding probably should be toggleable via a setting", and for B22's effects already merged: "Sure, add those as well.
Probably group them under the graphics setting." Your row: docs/game/implementation-plan.md section 4.14, B17c.

Read first: CLAUDE.md; docs/game/biomes-plan.md section 3.9 (items 2 to 7: what B17 does); docs/game/b17-spike.md (B17a's spike and the
B17b section: the state scope, the depth-equality trap, the fallback, what B17c inherits); docs/game/decisions.md D-game-34 items 30, 40,
41, 43; docs/game/architecture.md (the drawing); .claude/briefs/b22-report.md (the light bands, the sun per band, `land-colours.json`, the
water depth tint), b17b-report.md, b21-report.md (meshing on the job pool: the mesher runs on workers from a snapshot; keep it so);
src/Vocaterra.Game/Graphics/World (`ChunkMesher`, `MeshVertex`, `LightBands`, `GlChunkPass`, the GLSL sources, `VoxelRenderer`),
Graphics/Far (the far view's tiles and their effect), Graphics/Sky (fog colours), the settings page (`MenuRows`, `SettingId`,
`PlayerSettings`, the Graphics group B17b made).

Rules: a game milestone edits only the game projects, their tests, content/game-v1, content/schema, assets/ and docs/game/ and takes the
core as it is. A regression check is a C# test. Never edit .github/workflows/, either implementation-plan.md, either decisions.md or
docs/game/biomes-plan.md (say in the report what they should say). Engine code only in src/Vocaterra.Game; GL calls on the game thread
only. No new package. The world, the simulation and the saves do not change (shaders never feed the simulation); the goldens and the
fixture stay; a default save is unchanged (settings saved only when they differ from the default, as B17b did). Another agent (B07d) is
adding a "Worker threads" row and the far view's detail rows to the same Graphics group and changing the far store (not its drawing): join
the group, keep off its store code; B25 is fixing a scripted-run stall in the remesh or scripted code.

Deliverables:
1. **Light as data in the vertex:** each corner carries its sky light, block light and corner occlusion (and the face's direction), not a
   finished colour; the shader combines them with uniforms (the sun's direction and colour, the sky's brightness for the hour and weather,
   the glow's strength, the eye's sky share underground), so the time of day, the weather and the darkening change without re-meshing.
   Today's sources fill the channels (B12's darkening by depth as the sky channel, the torch and lava glow as the block channel, the corner
   occlusion as now); B23 will replace their values with propagated light 0 to 15, so give each channel that range. B22's four baked bands
   are retired: the sun's direction and colour become uniforms that follow the hour. The mesher stays a pure function of its snapshot.
2. **Fog by distance and height** matching the far view's haze (the sky's colours for the hour and weather).
3. **A water shader:** B22's depth tint, a brighter edge at grazing angles, a gentle surface movement; no reflection pass.
4. **The far view on the same program and light model** (its tiles drawn through GLSL with the same fog and sun), its textures mipped;
   the near and far meet without a seam in colour at midday and at dusk.
5. **The texture atlas as a texture array** if it removes bleeding between tiles at a distance; say what you chose.
6. **Toggles (item 43)** in the Graphics group, each on by default, saved only when off, a launch option each, and set by the quality
   presets where it makes sense: the sun following the hour (B22's sun per band and deeper dusk), the climate colours (B22's
   `land-colours.json` tint), the water shader (with B22's depth tint), the fog by height, the far view's light. Move MSAA (`--msaa`) and
   B18's grass slider into the group. With an effect off, the picture is today's for that effect.
7. **The fallback:** with "Shaders" off (or no GL 3.3), the built-in effects still draw a correct picture: the mesher (or a CPU step) bakes
   the colours from the same light data as today. Say how.
8. **Kept and measured:** the midday look in clear weather within B17a's tolerance of today's (0.1 percent of pixels differing by more than
   8 of 255; say the numbers, and explain any view outside it), the scripted runs' pictures compared (`compare-runs.py`; every difference
   explained); frame and card times (`--stream-stats`, interleaved A/B), and the processor's time for a time-of-day change and a torch
   placed, before (a re-mesh) and after. Pictures in /home/james/Dev/Vocaterra/artifacts/b-series/b17c/ with an index.md: midday, dusk,
   night with torches, a cave, rain, a lake shore and under water, the far view at midday and dusk, each effect on and off, and the
   settings page's Graphics group.
9. **Tests:** the vertex layout and the shader inputs; the mesher's output a pure function of its snapshot (byte-identical on a worker and
   inline); no re-mesh on a time-of-day change; each toggle saved, read back and applied; the fallback's colours equal to today's.
10. **Documents:** docs/game/b17-spike.md gets a "B17c" section; docs/game/architecture.md, the Game README, docs/game/running.md.

Gate: build with -warnaserror (0 warnings); scripts/test.sh full (12,539 passed on feature/biomes at your base, plus your new tests);
format clean; content validate; the licence scan clean. A timing test that fails under load: rerun it alone and say so. Commit messages
end with
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDbkPTqABsu7sLVD9h6

Report, half a page: branch, head commit, worktree; each deliverable and how; the vertex layout; the comparison and timings; the toggles;
the fallback; what B23 inherits; what the plans should say; anything left. No file dumps or long logs.
