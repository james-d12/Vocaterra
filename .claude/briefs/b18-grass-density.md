You are implementing B18, "Grass density", for the Vocaterra repository (C#, .NET 10). The game is Vocaterra.Game.Core (the engine-free
logic), Vocaterra.Game (MonoGame) and Vocaterra.Host; the language core (Vocaterra.Nlp*) is not yours. This milestone's work is in the
game: a decorative grass layer in the renderer (src/Vocaterra.Game/Graphics/World), the player's settings (Vocaterra.Game.Core/Menu:
`PlayerSettings`, `SettingId`, the settings' save and launch options), the settings page's label in content/locale/en/ui.json, and their
tests.
Where to work: the worktree /home/james/Dev/Vocaterra/.claude/worktrees/b18, already on the local branch `B18-grass-density` (cut from
`feature/biomes` at @@BASE@@). Run every command from that directory. Commit after each step that builds and passes its tests, with the
prefix `B18: `. Do not push. Never push or merge feature/biomes or main. Leave the worktree clean on its branch.
Resources: every dotnet restore, build, test and format, every game run and screenshot through scripts/limited.sh (builds `-m:4`, tests
`-m:2`); one heavy command at a time, no `&`, no wait loops on `pgrep -f`. Three other agents build at the same time: expect waits.
Iterate with `scripts/test.sh fast` or a project; `scripts/test.sh full` once at the end. Caps: 10 GB a command, 16 GB in all (the
script's defaults). /tmp is a 16 GB RAM-backed tmpfs shared by every agent: keep scratch on disk under
/home/james/Dev/Vocaterra/artifacts/tmp/b18/ and remove it at the end. Environment: prefix every command with PATH="$HOME/.dotnet:$PATH"
DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1; game runs need DISPLAY=:0 and VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models; scripted
runs use `VOCATERRA_RUNS_WORK=/tmp/claude-1000/b18-runs`. **Game windows are allowed; look at every picture you take with the Read tool.**
Quality before speed.

The owner (2026-11-16, D-game-34 item 34 in docs/game/decisions.md): "there's not a lot of grass, I guess. Could we increase the volume of
grass? Perhaps make that a slider in the settings as well." Today the grass you see is the undergrowth's tall-grass blocks (S14: crossed
cut-out quads, `ChunkMesher`'s undergrowth, drawn within `QualityPreset.PlantDistance`, 64 blocks by default), placed by the generator
(biomes.json `decor` densities; the valley's own undergrowth from its scene), so meadows read as bare green blocks with a few tufts.

Read first: CLAUDE.md; docs/game/implementation-plan.md sections 1, 3, 4.14 (your row, B18) and 6; docs/game/running.md (launch options);
the code: Graphics/World (`ChunkMesher`'s undergrowth, `VoxelRenderer`'s plant pass and `PlantDistance`), Graphics/Blocks (the tall-grass
art), Graphics/QualityPreset.cs, Menu/PlayerSettings.cs and SettingId.cs (how a slider is specified, saved and applied through `Changed`),
Startup/LaunchOptions.cs and Menu/LaunchSettings.cs, content/locale/en/ui.json (the settings labels). Read the sections named, never whole
long files.

Rules: a game milestone edits only the game projects, their tests, content/game-v1, content/schema, content/locale, assets/ and docs/game/
and takes the core as it is. A regression check is a C# test. Never edit .github/workflows/, either implementation-plan.md, either
decisions.md or docs/game/biomes-plan.md. Engine code only in src/Vocaterra.Game; Core stays pure. **The world does not change:** no
generated or saved block changes (a setting must never change the world), so the 18 procedural goldens, `SceneTests.GoldenDigest` and the
fixture's state digest do not move; the decorative grass has no collision, is not a block, is not edited, is not saved. Keep off what other
agents change now: B07c (Graphics/Far, World/Lod, `StreamedWorldHost`, `JobPool`), B02c (World/Generation/Procedural rivers), M97 (the
core).

Deliverables:
1. **A decorative grass layer:** on the top face of grass blocks (and the meadow-like tops the generator uses: name them as data, not
   hard-coded ids, if the block data allows), crossed cut-out tufts like the tall grass (with two or three variants in height and tint so
   it does not tile), placed at positions from a hash of the cell (stable as you move and across runs), never on a cell that holds a plant,
   a path, a farm, a crop, snow, a door or other blocks above, never in the valley's paths, sites or buildings' floors (it sits on grass
   tops only, so check paths and farmland are not grass); drawn only within a distance from the eye, fading out at its edge with no pop;
   swaying with the plants if they sway. Built with the chunk's mesh or as its own pass, whichever measures better; no per-frame
   allocation.
1b. **By biome** (the owner, a minute later: "at least, you know, grass in the plains and the forests and stuff like that"): lush in
   plains and meadows, on forest floors between and under the trees, and on the valley's grass; sparser in taiga and dry land; none on
   sand, snow, gravel or stone. The weights per biome as data that does not move the world-generation digest (not in world.json,
   biomes.json or underground.json, whose hash is part of the save's state digest: a small render file of its own, or the block or
   client data), read through the place the renderer already knows (`IPlaceInfo`, or the column's biome as the far view reads it).
2. **A "Grass" slider** on the settings page (0 to 100%, steps of 10, default chosen so meadows read lush: say why), saved with the other
   settings, applied at once through `Changed` (re-mesh or redraw as needed), a launch option (`--grass <0-100>`) that wins for the run as
   the others do, and the label in content/locale/en/ui.json. 0 is today's look exactly.
3. **Presets:** the layer's distance and its density cap scaled by the quality preset (low, medium, high), so Low stays cheap.
4. **Measurements** (Release, `--stream-stats`, the B07 walk along the pass road and a meadow view, at Low, Medium and High, with the
   slider at 0, the default and 100): frame p50/p95/p99, meshing time, triangle counts; say the load beside each.
5. **Tests:** the slider's spec, save round-trip and launch option; positions stable from the hash and identical across runs; nothing on
   non-grass tops, plants, paths or snow; 0 draws nothing extra (the meshes equal today's); the world unchanged (the goldens above).
6. **Pictures** in /home/james/Dev/Vocaterra/artifacts/b-series/b18/ with an index.md and the frame times: the same meadow, the valley's
   village green and a hillside at midday, at 0, the default and 100; one at dusk; one at Low. Look at each; fix what looks wrong (tiling,
   tufts floating or on paths, popping at the edge).
7. **Documents:** docs/game/running.md (the option and the setting), the Game README (the grass layer).

Gate: build with -warnaserror (0 warnings); scripts/test.sh full (@@COUNT@@ passed before, plus your new tests); format clean; content
validate; the `--world valley` scripted runs unchanged at the default when the slider is 0 (and the differences at the default explained:
only the new grass). Commit messages end with
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDbkPTqABsu7sLVD9h6

Report, half a page: branch, head commit, worktree; how the layer works; the slider and its default; test counts; the measurements; the
pictures and what they show; anything left. No file dumps or long logs.
