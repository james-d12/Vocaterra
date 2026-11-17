You are implementing B22, "A fidelity bundle with built-in effects", for the Vocaterra repository (C#, .NET 10). The game is
Vocaterra.Game.Core (the engine-free logic), Vocaterra.Game (MonoGame) and Vocaterra.Host; the language core (Vocaterra.Nlp*) is not yours.
This milestone's work is in the drawing: src/Vocaterra.Game (VoxelGame.cs's device settings, Graphics/World `ChunkMesher`'s face light
and colours, Graphics/Sky (clouds), the water's colour, Graphics/Far `FarPalette`), any colour data it needs, and their tests.
Where to work: the worktree /home/james/Dev/Vocaterra/.claude/worktrees/b22, already on the local branch `B22-fidelity-bundle` (cut from
`feature/biomes` at edd60cc6). Run every command from that directory. Commit after each step that builds and passes its tests, with the
prefix `B22: `. Do not push. Never push or merge feature/biomes or main. Leave the worktree clean on its branch.
Resources: every dotnet restore, build, test and format, every game run and screenshot through scripts/limited.sh (builds `-m:4`, tests
`-m:2`; the script's caps); one heavy command at a time, no `&`, no wait loops on `pgrep -f`. Four other agents build at the same time:
expect waits. Iterate with a project or `scripts/test.sh fast`; `scripts/test.sh full` once at the end. Scratch on disk under
/home/james/Dev/Vocaterra/artifacts/tmp/b22/ (not /tmp), removed at the end. Environment: prefix every command with
PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1; game runs need DISPLAY=:0 and
VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models; scripted runs use `VOCATERRA_RUNS_WORK=/home/james/Dev/Vocaterra/artifacts/tmp/b22/runs`.
**Game windows are allowed unless the orchestrator says the owner is playing; look at every picture with the Read tool.** Quality before
speed: the owner judges this by its pictures. Take pictures in a Release build.

Why: the owner asked for an Opus review of the codebase, performance and graphics fidelity, and said "go ahead with all the fixes
recommended from review" (D-game-34 item 37). Read docs/research/2026-11-16-codebase-review.md section 5 (graphics fidelity). B22 is its
quick-win bundle, all possible with MonoGame's built-in effects (no custom shaders: B17 is planned separately):
- **G4. No antialiasing:** `GraphicsDeviceManager` sets no `PreferMultiSampling` (`VoxelGame.cs:414-421`): stair-stepped silhouettes and
  shimmering far tiles. 4x MSAA on DesktopGL (check it takes effect; a setting or the preset to turn it off on Low if it costs).
- **G3. The sun on the blocks never moves:** faces are shaded from a constant `ChunkMesher.SunDirection` (:145) while the sky's sun follows
  `SkyLook.SunDirection` (`SkyRenderer.cs:283`); at dusk the ground darkens only about 17% while the sky turns orange. Bake each light band
  (Day, Dusk, Night: the band remesh already exists) with its own sun azimuth (dawn from the east, dusk from the west), and deepen the warm
  tint of `DuskLight` (:539). The far view must follow the same light (it shares the face light).
- **G2. One saturated lime green everywhere** (mean ground about RGB 89,179,49, saturation about 0.73; no biome variation): biome colour
  maps for grass and leaves from the climate's temperature and humidity (as data: a small colour table per biome or a 2D map, not ids in
  code), less saturation overall, and the far palette (`FarPalette`) from the same map so near and far match. The valley's own grass may
  take its biome's colour; say how it looks.
- **G5. Clouds** read as near-white low slabs: darker, bluer bottoms (`CloudBottom`), and higher, smaller boxes (the cloud layer's height
  is B14's data item: if you change it, do it as data and say so).
- **G6. Water** is a flat blue sheet: tint by depth per vertex (shallows lighter and clearer over sand, deep water darker), and a lighter
  edge where it meets the shore. Reflections and specular are B17's.

Read first: CLAUDE.md; docs/game/implementation-plan.md sections 1, 3, 4.14 (your row, B22) and 6; the review's section 5; the code named
above; docs/game/weather-plan.md for how the day bands and the sky are made (Atmosphere and Graphics/Sky). Other agents are changing nearby
code now: B07e (Graphics/Far's mesher, selector, renderer and FarView, and a few lines of VoxelRenderer), B07f (World/Lod and Streaming),
B02c (rivers): keep to `FarPalette` in Graphics/Far, and say if you must touch more.

Rules: a game milestone edits only the game projects, their tests, content/game-v1, content/schema, assets/ and docs/game/ and takes the
core as it is. A regression check is a C# test. Never edit .github/workflows/, either implementation-plan.md, either decisions.md or
docs/game/biomes-plan.md. Engine code only in src/Vocaterra.Game; Core stays pure. The world's blocks do not change (the goldens and the
fixture's digest stay); the scripted runs' pictures will change (say how; re-baseline). No atmosphere kind or biome id named in src (data).

Deliverables:
1. G4, G3, G2, G5, G6 as above, each with a test where it is logic (the sun azimuth per band, the colour map's lookup and its data's
   validation, the water tint by depth, the far palette from the same map).
2. Measurements (Release, the B07 walk along the pass road at Low, Medium and High, before and after, with `--stream-stats`): frame p50,
   p95, p99; say the load; MSAA's cost.
3. Pictures before (feature/biomes at the base) and after from the same places, in /home/james/Dev/Vocaterra/artifacts/b-series/b22/ with
   an index.md: a meadow and a forest at midday, the same at dawn and dusk (the sun on the faces from the right side), a lake shore and a
   river (the water), the sky with clouds, a far view over several biomes (the colours near and far), the valley's village.
4. Documents: the Game README (light and colour), docs/game/running.md if a setting or option is added.

Gate: build with -warnaserror (0 warnings); scripts/test.sh full (12,399 passed before, plus your new tests); format clean; content
validate. Commit messages end with
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDbkPTqABsu7sLVD9h6

Report, half a page: branch, head commit, worktree; each item and how; tests; measurements; the pictures and what they show; anything
left. No file dumps or long logs.
