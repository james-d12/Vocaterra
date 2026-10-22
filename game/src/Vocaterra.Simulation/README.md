# Vocaterra.Simulation

The engine-agnostic 3D world of the prototype: a hand-authored block scene, the first-person player body, gathering and placing, the
inventory and the crafting rule, all headless and integer-only. It takes no packages and no project references and knows no engine (plan
section 3, rules 2 and 3; D-game-1). It targets `net8.0;net10.0` so a later Godot 4 .NET front-end (GodotSharp ships `net8.0` only) can reference
it. `Vocaterra.Game` (the guide, word progress, the objective) and the front-end build on it. Until S4 this project also held a 2D tile world;
that was retired (its last commit is tagged `2d-slice-p7`).

The API a front-end uses (the S4 world):

```csharp
var layout = SceneLayout.Load("game/content/game-v1/game/scene.json");     // or SceneLayout.Parse(json)
var scene = SceneBuilder.Build(layout, seed: 42);                          // a pure function of (layout, seed)
var sim = VoxelSimulation.Create(scene);                                   // the player stands at scene.Spawn
sim.SetRecipes(recipes);                                                   // the bundle's recipes as plain values
while (running)
{
    accumulator += frameSeconds;
    while (accumulator >= 1.0 / VoxelSimulation.TicksPerSecond)
    {
        sim.Step([VoxelCommand.Look(yaw, pitch), VoxelCommand.MoveRelative(strafe, forward)]);  // one 1/60 s tick
        accumulator -= 1.0 / VoxelSimulation.TicksPerSecond;
    }
    Draw(sim.Grid, sim.Player);                                            // read only
}
```

## The 3D voxel world (S1, `Vocaterra.Simulation.Voxel`; the route in S3)

The prototype's world (docs/SCENARIO_PLAN.md): a generated 384 x 384 x 128 block valley (S13), a first-person body, gathering and placing, all
headless and integer-only (the 2D code above is untouched). The folder is `src/Vocaterra.Simulation/Voxel/`; the prototype route is
`content/game-v1/game/scene.json` in the bundle (S3, rewritten as generator parameters in S13).

```csharp
var layout = SceneLayout.Load("game/content/game-v1/game/scene.json");     // or SceneLayout.Parse(json)
var scene = SceneBuilder.Build(layout, seed: 42);                          // a pure function of (layout, seed)
var sim = VoxelSimulation.Create(scene);                                   // the player stands at scene.Spawn
sim.Step([VoxelCommand.Look(Angles.FromDegrees(90), 0), VoxelCommand.MoveRelative(0, 1)]);   // one 1/60 s tick
// read: sim.Grid, sim.Player (eye, yaw, pitch), sim.Inventory, sim.Events, sim.Target (what the crosshair is on)
```

| Type | Role |
| --- | --- |
| `BlockId : ushort`, `BlockDefinition` | air, grass, dirt, stone, sand, trunk, leaves, bush, berry bush, water, path, planks, door, table (the workbench, `mesa`), torch, stall, roof, fence, wall (0 to 18), then S1's `FireRing` 19 (the unlit fire ring), `Sign` 20 (the signpost board) and `Bridge` 21, S13's `Snow` 22 and S14's `SpruceTrunk` 23, `BirchTrunk` 24, `SpruceLeaves` 25, `BirchLeaves` 26, the undergrowth `TallGrass` 27, `Fern` 28, `Reeds` 29, `YellowFlower` 30, `WhiteFlower` 31, then `MossyStone` 32 and `Cobble` 33; append-only, hashed. A definition holds `Solid`, `Transparent`, `Liquid` (renderer flags), `GatherTicks`, `Drop`, `DropCount`, `BecomesWhenGathered` and (S14) `Decor`. Trunk (an oak, a spruce or a birch: `IsTrunk`): 60 ticks, 1 `wood`, leaves air. **Decor** is the undergrowth (`IsDecor`: tall grass, ferns, reeds and the two flowers): not solid, never gathered, and the block ray skips it like air, so the crosshair looks through the grass at the berry bush or the trunk behind it and never stops on it; a block placed into its cell takes its place. Berry bush: 45 ticks, 1 `berry`, leaves a plain bush. `BlockDefinition.PlacedBy(item)` is the item to block map (wood to planks, torch, table, door, stone, dirt, sand) |
| `VoxelGrid`, `VoxelChunk`, `BlockPos`, `VoxelChunkCoord`, `BlockFace` | 16 x 16 x 16 chunks, bounded; `GetBlock` (air outside), `IsSolid` (solid outside), `HighestBlock(x, z)`; `Chunks` are indexed `(cy * ChunksZ + cz) * ChunksX + cx`; a chunk stores `(localY * 16 + localZ) * 16 + localX` |
| `SceneLayout`, `SceneBuilder`, `Scene` | the layout file, the deterministic builder, and the result: `Grid`, `Spawn` (cell, yaw and, S14, pitch), `Npcs`, `Points`, `TreeCount`, `PlantCount` |
| `PlayerBody3D`, `VoxelPhysics`, `Angles` | the body and its constants, and the integer angle table |
| `VoxelCommand`, `VoxelCommandKind` | `Move(dx, dz)`, `MoveRelative(strafe, forward)`, `Look(yaw, pitch)`, `Jump`, `Interact(x, y, z)`, `Place(item, x, y, z)`, `SelectSlot(slot)`, `Craft(index)` |
| `VoxelSimulation` | `Create(scene)`, `Step(commands)`, `Tick`, `Player`, `Inventory`, `Events`, `Target`, `GatherTarget`/`GatherProgress`, `SetRecipes`, `CanCraft`/`Craft`, `StationInReach`, `GrantItems`, `TeleportTo(feetCell)`, `ComputeStateDigest()` |
| `BlockRay`, `BlockRayHit` | the 3D DDA: `Cast(grid, body)` or `Cast(grid, eye, yaw, pitch, reach)`; the first block within 4 blocks that is not air and not undergrowth (S14), its face and the cell in front of the face |
| `AsciiMap3D` | `TopDown(scene)` (the highest block of each column, the spawn `@` and the NPCs `G K V` drawn over it) and `SideSlice(grid, z, x0, width, yTop, height)` |

Shared with the rest of the game: `WorldRng` (`Vocaterra.Simulation`, the scene's tree scatter), `Inventory` and `ItemDefinition` (three item kinds:
`Wood`, `Torch` and `Berry`; the numbers of the 2D kinds retired at S4 are not reused), `Recipe` and `CraftingRules` (a recipe's station is a
`BlockId`, the workbench `Table`), and `SimulationEvent` (`X, Y, Z, Detail, Amount` and the kinds `PlayerJumped`, `PlayerLanded`, `ItemCrafted`,
`GatherProgress` 5, `BlockRemoved` 6, `ItemPickedUp` 7, `BlockPlaced` 8). The 2D world (tiles, the generator, the light map, the 2D physics and the
2D simulation) was removed in S4; its last commit is tagged `2d-slice-p7`.

### Coordinates, units, angles

- **Axes:** x east, y up, z south, so row 0 of a top-down map is the north edge. Block (x, y, z) fills the cube from (x, y, z) to (x + 1, y + 1, z + 1).
- **Units:** the body is in integer units of 1/4096 block (`VoxelPhysics.UnitsPerBlock`), as the 2D body is. The box is 2458 x 7373 x 2458 units
  (0.6 x 1.8 x 0.6 blocks to the nearest unit), the eye is 6636 units above the feet (1.62). `PlayerBody3D.MinX/MinY/MinZ` is the box's minimum
  corner, `CenterX/FeetY/CenterZ` the middle of the feet, `EyeX/EyeY/EyeZ` the camera. Standing on a block, `FeetY` is a multiple of 4096.
- **Time:** 60 ticks per second. Walking is 294 units per tick (4.3 blocks per second), gravity 36 units per tick squared, a jump starts at
  640 (rises 1.3 blocks), the terminal fall speed is 3000. A walking body steps up one block by itself (`PlayerBody3D.SteppedUp` is set so a
  camera can ease the lift); two blocks need more than a jump gives.
- **Angles:** whole numbers of 1/4096 turn (`Angles.StepsPerTurn`). **Yaw 0 faces north (-z), 1024 east, 2048 south, 3072 west** (a compass
  bearing, clockwise from above); `Angles.FromDegrees(90)` is east. **Pitch** 0 is level, positive looks up, clamped to +-1023. The look
  direction is (cos p sin y, sin p, -cos p cos y). Sine and cosine are fixed point (65536 is 1.0) from a quarter-turn table built in type
  initialisation by an integer Taylor series (Q30, 64-bit multiplies), the other quarters by symmetry: no `Math.Sin`, no platform
  dependence. A test compares the table with the library sine to one unit in the last place.
- **Commands:** `Move(dx, dz)` walks along the world axes, `MoveRelative(strafe, forward)` along the facing (a front-end with mouse look
  sends this one); both are levels, summed over the tick, and diagonals are scaled by 46341 / 65536 so they are not faster. `Look` is
  absolute. `Interact` is held: progress builds while the target stays the same, is gatherable, is within 4 blocks of the eye (the nearest
  point of the block) and its drop fits; a tick without it resets. `Place(item, x, y, z)` puts one item of that kind, from anywhere in the
  inventory, in an empty cell in reach that touches another block and, if the new block is solid, does not overlap the body. The sim does
  not check line of sight: a front-end picks its target with `BlockRay` (`sim.Target`), and everything that ray can hit passes the reach check.
- **Order inside a tick:** selection and look, gathering, placing, crafting, then the walk with the jump and the physics.

### The layout file (S13: parameters, not blocks)

JSON (comments and trailing commas allowed, `schemaVersion` 2); `SceneLayout.Parse` documents the shape. The file lists no block. It holds the
generator's parameters and the named places:

- `size` {x, y, z} (multiples of 16: 384 x 128 x 384 is 24 x 8 x 24 chunks), `waterLevel` (the lake's surface).
- `terrain`: `relief`, a coarse grid with one base-32 digit per chunk corner (the height divided by 4; the macro shape: the mountains along the
  north and the west, the foothills, the valley floor at 36), and `valley`, `rockLine` (bare stone above 85), `snowLine` (snow above 100 on
  gentle ground), `terrace` (the step of the cliff faces) and `ceiling`.
- `sites`: named places at **fixed** positions (`camp.player`, `camp.guide`, `village`, `bridge.west`, `river`, `waterfall`, `lake`, `trail`...). A
  site with a `radius` levels the ground to `y` (or the height the land has there) and slopes it into the land over `blend`; every site is also a named
  point. `waterfall` {site = the lip, top, width, plateau, pool}, `lake` {site, rx, rz, depth} and `river` {halfWidth, depth, bank, path: waypoints from the
  pool to the lake}. `paths` carve a trail between two sites: `mode: grade` changes the height evenly from the first site's to the last's (a switchback
  down the mountainside), `mode: follow` follows the valley floor, smoothed so it never steps more than a block.
- `trees` (S14) also takes `spruceFrom`, `birch`, `birchAway`, the shore meadow (`shoreClear`, `shoreFade`, `shoreKeep`), `clearings` (round meadows at a site: `site`, `radius`, `fade`, `keep`) and `vistas` (a sight cone from a lookout site: `site`, `yaw`, `half`, `radius`, `down`); `undergrowth` takes `grass`, `fern`, `flowers`, `reeds` and `routeMargin` (percent of the columns); the `spawn` takes a `pitch` in degrees (negative looks down).
- `stamps` (small block drawings, layers of rows with a legend, turned to face a side) and `props` placed at a site plus an offset (`dx`, `dz`):
  `workbench`, `fire_ring`, `stall` (the counter side is `facing`), `house`, `signpost`, `bridge`, `paving`, `stamp`, `bush`, `berry_bush`, `tree`; a
  `name` on a prop registers a named point. A prop on uneven or wet ground is rejected with a message (`SceneLayoutException`), so the layout
  cannot silently build a floating house. `npcs`, `spawn` (a site and an offset, and a facing in degrees: 135 is south-east), `points` and `trees`.

`TerrainGenerator` (integers only: no floating point, no `Math.Sqrt`) makes the ground in this order: the macro relief (Catmull-Rom between the
corners, clamped) plus the seed's value noise (`TerrainNoise`, four octaves, damped to a few percent near the river, the paths and the sites, and
scaled up on the mountains) and stepped terraces; the waterfall's plateau; the river (a smooth curve; its surface is the lowest the ground has been, less
two blocks, from the source to each point, so the water never runs uphill) and the pool; the lake; the graded trail and the road; and last the sites'
level ground. `SceneBuilder` fills the grid column by column (stone, three layers of dirt or sand, the top block by height and slope: sand by the water,
stone above the rock line or on a cliff, snow above the snow line), lays the water, builds the props, and plants the forest.

The forest is the only random draw besides the noise: one tree is tried in every `cell` x `cell` square, at a jittered place from
`WorldRng.For(seed, "trees")` (the draws are made for every square, so the stream stays in step), kept with a `chance` percent (less with height, none
above `treeLine`, a few clearings where the slow noise is low) unless it is on a cliff, on sand or water, within `margin` columns of the route (path
surface, a site's level ground, a prop, the spawn, an NPC, a point) or within `spacing` of another trunk. **S14** adds what the look needs, without touching the S13 draws (the first stream is the one S13 drew, so no trunk moved; a second stream, `treeshape`, picks each tried tree's species and the ragged rim of its crown): oaks (big rounded crowns, three columns wide at the widest) in the valley, spruces (a trunk five blocks longer and a tiered cone) from `spruceFrom` up and all of them 20 blocks higher, birches (a tenth of the lowland, never within `birchAway` of a site, so the guide's clearing keeps its oaks); the share of trees kept falls to `shoreKeep` percent within `shoreClear` blocks of the water and within a clearing's radius (the banks and the village are meadow), and a tree inside a vista's cone must stand under the sight line `down` degrees below the lookout's eye (so the ledge's view of the valley is open). The terrain's relief is looked up at a point moved by slow noise (damped near the sites, the paths and the river), so the valley's walls are not the chunk grid's straight lines; low rock is `MossyStone`.

The **undergrowth** is planted after the forest by `PlantUndergrowth` from the noise of the pass `undergrowth` (keyed from `WorldRng`): tall grass and ferns (more ferns under the trees) on open grass in patches of thicker and thinner cover, flowers in meadows of their own (mostly one colour), reeds on the banks within two columns of the water; nothing grows on a path or within `routeMargin` columns of a prop, a point, an NPC or the spawn, and a third as much one column further. In front of a lookout (inside its cone, 44 blocks out) only flowers grow, drawn low, so the grass does not stand between the player and the valley. **A different seed changes the terrain
between the sites, the river's surface, the forest and the undergrowth, and never moves a site, a point, an NPC or the spawn**; the ground of each site is the same
block for block, the undergrowth on its meadow apart (a test checks it for two seeds).

Cost: the 384 x 384 x 128 world builds in about 0.4 s and its first digest takes about 0.1 s on the development machine (a test holds the two
under three seconds). `VoxelChunk` keeps a count of its non-air blocks and a mask of the block kinds it holds, so a search or a renderer passes
empty chunks by (`VoxelGrid.BlocksOfKind`, `VoxelGrid.HighestBlock`).

### Digests (for the renderer and for saves)

`VoxelGrid.ComputeDigest()` is the SHA-256 of the three sizes (int32 little-endian) and the 32-byte digest of every chunk in `Chunks` order;
a chunk digest is the SHA-256 of its 4096 block ids as 16-bit little-endian. Both are cached and every real edit invalidates them. To
**re-mesh**, compare `VoxelChunk.Version` (it increases on every edit of that chunk) with the value you meshed, or `VoxelGrid.EditCount` for
"anything changed"; the chunk to rebuild is `VoxelChunkCoord.FromBlock(x, y, z)` of an event's position (and the neighbour chunk when the
edit is on a chunk border, because the faces there depend on it). `VoxelSimulation.ComputeStateDigest()` adds the tick, the body, the
selection, the gather progress and the inventory.

### Tests

`tests/Vocaterra.Simulation.Tests/Voxel/`: `SceneTests` (the seed-42 golden, the load cost, identical builds, another seed moving no site, the mountain heights, the
river running downhill, the walking times, trees never on or over the route for three seeds, trunk spacing, the named points and NPCs, a pathfinder plus the
real body walking from the camp over the bridge to the far bank, rejected layouts), `VoxelGridTests` (chunks, bounds, the digest changing on
every edit and only in the edited chunk), `AnglesTests`, `PlayerBody3DTests` (landing, walls, the one-block step and the two-block wall, jumps,
edges, diagonal speed, relative movement, and the never-inside-a-solid property over 24 random worlds and 6 route seeds), `BlockRayTests`
(known poses and faces, the reach limit to the unit, a property test against a fine march) and `GatheringAndPlacingTests` (trunk, berry bush,
reach, a full inventory, placing rules, the torch at the workbench and the fire ring). The shared tests: `InventoryTests` (forty slots, the
hotbar, stacks of 99, what does not fit, removals), `CraftingTests` (the rule: ingredients, the station, the room for the output), `WorldRngTests`
and `DeterminismRulesTests`, which scans the sources (every folder, the Voxel folder for floating point too) and the project file for the
forbidden libraries and for any reference beyond the framework. Print the route:
`dotnet test game/tests/Vocaterra.Simulation.Tests --filter "FullyQualifiedName~Voxel" --logger "console;verbosity=detailed"`.
