You are implementing B06, "The world factory, regions, the composite chunk source and the streamed mode", for the Vocaterra repository
(C#, .NET 10). The language core is the engine Vocaterra.Nlp, the Spanish pack Vocaterra.Nlp.Es and the Vocaterra.Nlp.* test and tool
projects; the game is Vocaterra.Game.Core (the engine-free logic), Vocaterra.Game (MonoGame), Vocaterra.Host and
tools/Vocaterra.SpeechRender. This milestone's work is in the game, in Vocaterra.Game.Core and its tests; the game itself stays on the
bounded valley (B07 switches it).
Where to work: the worktree /home/james/Dev/Vocaterra/.claude/worktrees/b06, already on the local branch `B06-regions-streaming` (cut from
`feature/biomes` at db61ef0e, which holds B01, B02, B03, B04a, B04b, B05 and B08a). Run every command from that directory. Commit after each item that
builds and passes its tests, with the prefix `B06: `. Do not push your branch. Never push or merge feature/biomes or main. When you
finish, leave the worktree on your branch, clean, and remove any extra worktree you made.
Resources (the machine has crashed from overload): run every dotnet restore, build, test, format and run, every scripts/content.sh call
and every scripted run through scripts/limited.sh (for example `scripts/limited.sh dotnet build Vocaterra.sln -warnaserror -m:4`; tests
with `-m:2`). scripts/test.sh, scripts/content-loop.sh and scripts/scripted-runs.sh call it themselves: run them directly. Never start
two heavy commands at once (no `&`, no background builds, no parallel test runs), and write no shell wait loop on `pgrep -f`. The owner is using the machine: prefix every heavy command with VOCATERRA_HEAVY_SLOTS=1 (one machine-wide slot, shared with the
orchestrator's merge gate), and expect waits. No game windows while he plays: do not run the game, screenshots or
scripts/scripted-runs.sh (they open windows on his screen); the orchestrator runs the scripted runs after your report. While iterating run `scripts/test.sh fast` or `scripts/test.sh project
tests/Vocaterra.Game.Core.Tests`; run `scripts/test.sh full` once at the end, after your last change. Exit 137 means the 6 GB cap killed
it: lower the parallelism.
Environment: prefix every command inline with PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1 (the sandbox may
reject a bare `export`, and it refuses a command line containing the shell builtin words `eval` or `hash`: for `scripts/content.sh hash`
use a wrapper script outside the repository). Model-gated tests need
VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models.

Read first: CLAUDE.md; docs/game/implementation-plan.md sections 1, 3, 4.14 (your row) and 6; docs/game/biomes-plan.md sections 3.1, 3.2
(the seams `IRegion`, `IChunkSource`, `IPlaceInfo`, and "How the principles apply"), 3.3 (how temperature will read `IPlaceInfo`), 3.6
(regions and the blend), 3.7 (generate on read, residency, `WorldFacts`'s window, determinism) and 7 (risks 5, 6, 7, 9); D-game-34 items
14 and 15 in docs/game/decisions.md (B16 later makes regions data-declared: keep the region's size and position out of code constants
now, read from its layout; after the pass opens the player may leave anywhere, so the blend must be walkable round the whole ring, not
only at the pass). The reports of the milestones you build on: .claude/briefs/b03-report.md (`ProceduralChunkSource`, `GenerateColumn`
for a whole column with one window, `ChunkData`), b04a-report.md (`VoxelGrid` streamed mode, `IChunkGenerator`, `ChunkContent`,
residency, the dense-member allow-list with three B06 entries: `FireRings`, `VoxelSession`, `LandRules` use `BlocksOfKind`),
b05-report.md (the state digest; B06 must pass the region template's digest to `WorldStateHeader.For(bundle, seed, templateDigest)`) and
b08a-report.md (the pass: a cutting from the village east to x 383 at about z 208, y 58, closed by a rockfall). The code:
src/Vocaterra.Game.Core/World/ (Scene.cs, Generation/SceneBuilder.cs, Streaming/, Generation/Procedural/), Voxel/,
Npc/WorldFacts.cs (`Snapshot`), Needs/FireRings.cs, Land/LandRules.cs, src/Vocaterra.Game.Core/README.md (B01 and B03 sections). Read the
sections named, never whole long files.

Rules: a game milestone edits only the game projects, their tests, content/game-v1, content/schema, assets/ and docs/game/ and takes the
core as it is. A regression check is a C# test. Never create or edit anything under .github/workflows/; do not edit either
implementation-plan.md, either decisions.md or docs/game/biomes-plan.md (the orchestrator does); engine code only in src/Vocaterra.Game.
The seams' implementations are pure, immutable and safe from any thread (B01's purity tests; extend their folder list to World/Regions);
threads only in the Game project (none in this milestone). The valley stays byte-identical in the default game: `SceneTests.GoldenDigest`
does not move, and the bounded path's tests are the evidence of byte-identity (the orchestrator compares the scripted runs later).
Keep off the files later milestones own: do not edit src/Vocaterra.Game/Graphics/ (B07 wires the streamed world into the renderer), the
terrain tuning's files (B02b: biomes.json,
World/Climate, World/Generation/Procedural/HeightModel.cs, and the existing sections of world.json; read them, do not change them; your
blend numbers go in a new section of world.json), or content/game-v1/game/scene.json
(B08a, B08b).

Deliverables (in Vocaterra.Game.Core):
1. `World/Regions/`: `IRegion` (id, bounds, `Covers`, `Copy` of a chunk, `EdgeHeight`), `RegionCatalog`, and `AuthoredReliefRegion` for
   the valley: built once by the unchanged `SceneBuilder`, then frozen into immutable arrays (so worker threads cannot race on lazy
   caches), its bounds taken from its layout's size (chunk columns and layers 0 to 7 for today's 384 x 128 x 384 valley, never written as
   constants), its trunks kept 2 blocks inside the edge, its edge profile exported for blending, and its template digest computed for
   the state header.
2. `RegionBlend` and `CompositeChunkSource : IChunkSource`: inside a region the region's chunks; under the valley (below its layers) the
   procedural world (solid stone today); above it air; outside, the procedural world with the blend: over a margin of about 64 blocks
   (data in world.json, in a new top-level section of its own so it does not collide with B02b's tuning of the existing sections) the height eases towards the region's edge height at the nearest edge point, trees keep 3 blocks off the border
   and thin across the margin, rivers and lakes fade out in it, and continentalness is pulled towards land within about 512 blocks
   (data) so no sea opens at the valley's feet. Beyond the margin the land is free to rise into the ranges. The road from the pass
   (B08a ends it at x 383, about z 208, y 58) continues walkable for a short stretch outside (an exit corridor in world.json), and the
   blend is walkable out of the valley all round the ring (the owner: the player may leave anywhere once the pass opens).
3. `WorldFactory` (the composition root: `Create(GameData, seed, mode)` returning the scene for the bounded valley, as today, or for the
   streamed world: a streamed `VoxelGrid` whose `IChunkGenerator` is the composite source, converting `ChunkData` to `ChunkContent`).
   Use `GenerateColumn` where a whole column is wanted (one window, about 1.6 ms, against about 1.1 ms for each single chunk); a
   column-level install is fine if the grid's API allows it. The game's start-up keeps calling the bounded path; wire `WorldFactory` in
   only as far as Program.cs's one `SceneBuilder.Build` call (near line 90) can call `WorldFactory.Create(..., bounded)` with no change in
   behaviour.
4. `World/Streaming/ChunkStreamer`: the residency policy only: from the player's position and a radius, the wanted chunks (nearest
   first, a fixed tie-break) and the chunks that may be evicted (unchanged, beyond the keep distance plus one chunk of slack). It changes
   performance, never results.
5. `WorldQuery : IPlaceInfo`, exposed as `Scene.Places`: climate, temperature at (x, y, z), biome, region, natural surface height and
   `IsUnderground`, read from the pure model (not the edited grid), so the guide's worker can query it off-thread.
6. The `WorldFacts` window: inside a region, the region (so the guide's answers in the valley stay byte-identical); elsewhere a box of
   radius 128 blocks, from 64 below the player to 64 above. Fire rings and the other `BlocksOfKind` users read the region template
   instead of scanning the world; the three B06 entries leave the dense-member allow-list (if it becomes empty, keep the test: it must still fail on a new
   use of a dense member; its reason-pattern check, which today also admits a stale `B05:` tag, goes or is tightened).
7. The state header gets the region template's digest (B05's hook).
8. Tests: the streamed world inside the valley equals the bounded valley block for block (and the same `WorldFacts` answers); the blend
   has no step beyond the B01/B03 bound across the border and the margin, with the land bias holding (no sea within the stated radius);
   the same scripted sequence of edits under two streaming policies (different radii and eviction) gives identical state digests and
   saves (the plan's risk 6); generate-on-read from many threads gives the same chunks; a region of another size and position, built
   only in test data, blends and validates with no code change (the shape B16 will need); `IPlaceInfo` answers for sample places;
   `ChunkStreamer`'s order is stable. Time to generate a 3 x 3 column patch beside the valley.
9. **Pictures for the owner** (D-game-34 item 12): the region's border and its blend. If the game cannot yet show the streamed world
   (B07), draw them with the preview tool (tools/Vocaterra.WorldPreview, from B02, already in your base; add a mode that renders the composite source's surface
   height and biome round the valley if it does not have one) as heights and biome maps of about 2,048 blocks round the valley, and slices
   across the ring and out through the pass, for seed 42. Save them in /home/james/Dev/Vocaterra/artifacts/b-series/b06/ (outside git)
   with an index.md saying what each shows.

Gate (from the worktree root): scripts/limited.sh dotnet build Vocaterra.sln -warnaserror -m:4 (0 warnings); scripts/test.sh full
(11,785 passed before your change, plus your new tests) run after your last change; scripts/limited.sh dotnet format Vocaterra.sln
--verify-no-changes clean; scripts/limited.sh scripts/content.sh validate passes; `SceneTests.GoldenDigest` unchanged; no scripted runs (the orchestrator's, after your report). If you edit world.json (the blend's numbers), re-hash and put the new
bundle hash into tests/Vocaterra.Game.Core.Tests/Fixtures/save-v9.json, and re-read its state digest if it moved. Do not commit bin/,
obj/ or artifacts/.
Commit messages end with
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDbkPTqABsu7sLVD9h6

Report, one page: branch, commit SHA, worktree path; what was built (the types, the blend's rules and numbers, the factory's modes,
the streamer's policy); test counts; measurements; the scripted-run comparison; the pictures' folder; bugs found and fixed;
known-not-fixed with reasoning; files touched outside the milestone's area; anything left undone and why. Leave out file dumps and long
logs.
