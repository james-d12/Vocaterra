# Vocaterra.Simulation

The engine-agnostic game world: tiles in chunks, seeded generation, the player body and a fixed 60 Hz tick. Since S1 it also holds the 3D
voxel world of the prototype (`Vocaterra.Simulation.Voxel`, see "The 3D voxel world" at the end) beside the 2D code, which stays until S4.
It takes no packages and no
project references and knows no engine (plan section 3, rules 2 and 3; D-game-1). It targets `net8.0;net10.0` so a later Godot 4 .NET
front-end (GodotSharp ships `net8.0` only) can reference it. `Vocaterra.Game` (guide, word progress, saves) and the front-ends build on it.

## The API a front-end uses

```csharp
var sim = GameSimulation.Create(seed: 42);          // generates WorldSize.Small (1024 x 512) and puts the player on the spawn
while (running)
{
    accumulator += frameSeconds;
    while (accumulator >= 1.0 / GameSimulation.TicksPerSecond)
    {
        sim.Step(commandsForThisTick);               // IReadOnlyList<PlayerCommand>; sim.Step() is a tick with no input
        accumulator -= 1.0 / GameSimulation.TicksPerSecond;
    }
    Draw(sim.World, sim.Player);                     // read only
}
```

| Type | Role |
| --- | --- |
| `GameSimulation` | `Create(seed[, size])` / `Create(World)`, `Step(commands)`, `Tick`, `Player`, `Events`, `World`, `ComputeStateDigest()` |
| `PlayerCommand`, `PlayerCommandKind` | the only input: `MoveLeft`, `MoveRight`, `Jump` (levels, send the held ones every tick); `SelectSlot(slot)`, `Mine(x, y)` (held) and `Place(x, y)` (a press), added in P6; `Craft(recipeIndex)` (a press), added in P6b; unknown kinds are ignored |
| `Recipe`, `Ingredient`, `CraftResult`, `CraftingRules` (P6b) | a recipe as plain values (output item and count, ingredients, the station tile or none); `CraftingRules.Check` and `Craft` are the pure rule over an inventory; `StationReachInTiles` (6) is the one place the station distance lives |
| `PlayerState` | position and velocity of the player box, `Grounded`, `Facing`, `FeetTile`, drawing helpers in tiles |
| `GameSimulation` (P6) | also `Inventory`, `SelectedSlot`, `SelectedStack`, `MiningTarget`, `MiningProgress`, `GrantItems(item, count)`, `ReachInTiles` (5) |
| `Inventory`, `ItemStack`, `ItemDefinition`, `ItemId`, `ToolKind` | 40 slots (`SlotCount`), the hotbar is the first 10 (`HotbarSize`), stacks with one stack limit per kind (`ItemDefinition.MaxStack`, 99; tools 1); `Add` returns what did not fit |
| `SimulationEvent` | `PlayerJumped`, `PlayerLanded`, `TileMined`, `TilePlaced`, `ItemCrafted` for the last step (more kinds are appended) |
| `World` | read-only queries: `GetTile`, `IsSolid`, `Spawn`, `Tiles`, `Info` (seed, size, generator version), `Record`, `ComputeDigestHex()` |
| `TileGrid`, `Chunk`, `ChunkCoord`, `TilePoint` | 32 x 32 chunks, `GetChunk`, dirty flags (a save stores dirty chunks), `ComputeDigest()` |
| `TileId`, `TileDefinition` | `Air`, `Dirt`, `Grass`, `Stone`, `Trunk`, `Leaves`, then P6's `Sand`, `Flower`, `Mushroom`, and the placed `Planks`, `Door`, `Table`, `Chair`, `Bed`, `Chest`, then P6b's `Furnace`, `Glass`, `Brick`, then P7's ores, `Torch` and `TallGrass`; values are stable (they are hashed and saved); each definition has its solidity, mining ticks, required tool and drop |
| `WorldGenerator` | `Generate(seed[, size])`, `Version` (4), surface bounds for a height |
| `LightMap` (P7) | the light of every tile, 0 to 16: `Level`, `Brightness` (rock is never below 6), `EmissionOf`; derived, not in the digest (see below) |
| `GameSimulation.Light`, `TeleportTo(tile)` (P7) | the light map (computed on first use, kept up to date by mining and placing); `TeleportTo` puts the player's feet on a tile, a debug action for the front-end's `--teleport` step |
| `WorldRng`, `ValueNoise` | SplitMix64 and integer value noise |
| `AsciiMap` | `Render(world, cx, cy, w, h, player)` for test output and bug reports |

Coordinates: X to the right, Y downwards, row 0 at the top. Tile positions are `TilePoint`; the player's box is in *units*, 1/4096 of a tile
(`PhysicsConstants.UnitsPerTile`), as integers. The box is 1.25 x 2.75 tiles; `PlayerState.X/Y` is its top-left corner. A body standing on a
tile has its `Bottom` on a multiple of 4096.

## The tick

`Step` is the whole game loop: the simulation's time is `Tick` (60 per second of game time). The front-end owns the clock and the accumulator
and may call `Step` zero or more times per frame. In one tick: the commands are folded into left / right / jump levels (order in the list
does not matter; left plus right cancel), horizontal acceleration or friction is applied (on the ground or in the air), a jump fires when a
press is buffered (6 ticks) and the player is on the ground or left it at most 6 ticks ago (coyote time), releasing early cuts the rise,
gravity is added up to the terminal speed, then the body moves on X (with a step up onto a one-tile ledge when walking on the ground) and on
Y against the solid tiles. Everything is integer arithmetic. Outside the world counts as solid, so the player cannot leave the grid.

## Determinism rules

- Generation is a pure function of `(seed, WorldSize, WorldGenerator.Version)`. Each pass gets `WorldRng.For(seed, passName, chunkX, chunkY)`
  (SplitMix64 over a mix of the inputs; column passes use chunk row 0) or a noise seed from `NoiseSeed(name)`, so passes never consume each
  other's draws.
- No `System.Random`, no clock, no `Math.Sin`/`Pow`/`Sqrt` or other library floating point in the simulation, no hash-ordered iteration; the
  value noise is fixed point. `DeterminismRulesTests` scans the sources and the project file for these.
- `World.ComputeDigestHex()` is the SHA-256 of the tile arrays (width, height, then each chunk row by row, tile ids as 16-bit little-endian).
  `GameSimulation.ComputeStateDigest()` adds the tick and the player state, for the game's `HostStateHash` and replay checks.
- **Any change to a pass changes the golden.** Bump `WorldGenerator.Version`, update the golden in `GenerationTests`, say so in the commit.

## How later passes plug in

A pass is a class implementing `IGenerationPass` (`Name`, `Run(GenerationContext)`) and an entry in `WorldGenerator.DefaultPasses`; the order
there is the order they run. A pass reads and writes `context.Tiles` (`SetTile` is internal to the assembly), uses
`context.Rng(Name, chunkX, chunkY)` or `context.NoiseSeed(...)`, and may record facts in `GenerationRecord` (add members there: cave
entrances, ore counts). The current order is `terrain`, `layers`, `grass`, `forest`, `spawn`, `spawn-tree`, `sand`, `flora`, `caves`, `ores` (the last two since
generator version 4, see P7 below). The spawn passes come before the surface dressing because they need the final trees and terrain.
`spawn-tree` (generator version 2, P3) guarantees a tree base within 20 columns of the spawn: if the forest left none it plants one 10 to 20
columns away (left before right, nearest first) that suits a tree as the forest pass requires and keeps three columns from every other trunk. It
changed 2 of the first 150 seeds on the 512 x 256 test world; seed 42 already had a tree near the spawn.
`sand` and `flora` (generator version 3, P6) are the surface dressing: sand patches (grass and two tiles of dirt become sand where a noise
threshold holds, never within two columns of a tree base or four of the spawn) and flowers and mushrooms on the grass (one number per column
from the chunk's stream). They change the golden (seed 42: `6d148dca...4f5`, was `0505865b...58a2`) and nothing else: the tree bases and the
spawn of seed 42 are the same as in version 2 (`SurfaceDressingTests` checks them by hash). Give a new pass a test with a golden and properties,
as `GenerationTests` does.

## Mining, placing and the inventory (P6)

- **Mine**: `Mine(x, y)` is held: each tick the button is down it adds one tick of progress to the tile, while the target and the selected item
  stay the same (a tick without the command, a new target or a new selected item resets the progress). The tile goes when the progress reaches
  its `MiningTicks` (trunk 90 with the axe, stone 75, dirt and grass 30, sand 20, planks and placed blocks 45 to 60, leaves 12 and flowers and
  mushrooms 6 by hand). The tile needs its tool (`RequiredTool`): the **selected** hotbar slot must hold it (the axe for trunks, the pickaxe for
  stone and the rest, the hand for leaves, flowers and mushrooms). The drop goes to the inventory (trunk and planks give `Wood`, grass gives
  `Dirt`, leaves give nothing); a full inventory holds the mining back and keeps the tile.
- **Reach**: five tiles from the middle of the body to the middle of the tile (squared integer distance). A target out of reach or outside the
  world does nothing.
- **Place**: `Place(x, y)` places the block of the selected item (`ItemDefinition.PlacesTile`: `Wood` makes `Planks`) in an air tile in reach,
  using one item. A solid block may not overlap the player's box (touching an edge is fine); a plant may. Several place commands in one tick
  apply in list order; the selection and the mining come first.
- The order inside a tick: the hotbar selection, the mining, the placements, then the movement and the physics.
- `ComputeStateDigest()` includes the selection, the mining progress and the inventory.
- Doors, tables, chairs, beds and chests are placeable items in the data model; crafting (P6b, below) makes them.

## Crafting and felling (P6b)

- **Recipes are data the host passes in.** `GameSimulation.SetRecipes(IReadOnlyList<Recipe>)` takes plain values; the game layer reads
  `game/recipes.json` from the bundle and has no JSON or package in this project. Recipes are rules, not state: they are not in
  `ComputeStateDigest()`. `Recipes` is the list, and the index in it is the argument of `PlayerCommand.Craft(index)`.
- **The rule** (`CraftingRules`): every ingredient must be in the inventory (summed over the slots); a recipe with a station needs a tile of
  that kind within `StationReachInTiles` (6) tiles of the middle of the body (squared integer distance, the same measure as the mining reach);
  the output must fit once the ingredients are taken out (a full inventory still crafts when the ingredients free a slot). `CanCraft(index)`
  says what a craft would do (`Done`, `NoSuchRecipe`, `MissingIngredients`, `MissingStation`, `InventoryFull`) and changes nothing; `Craft(index)`
  and the `Craft` command do it and add an `ItemCrafted` event. In a tick the crafts come after the mining and the placements, in list order.
- **New kinds**: tiles `Furnace` (15), `Glass` (16), `Brick` (17) and items `Sword` (13, one per slot, no tile), `Furnace`, `Glass`, `Brick`; every placeable
  is mined with the pickaxe and drops itself. The stations are the placed `Table` and `Furnace`.
- **Felling**: mining a `Trunk` tile removes the trunk tiles straight above it and the leaves of the canopy the forest pass plants around the
  top (two columns each side, from two rows above the top trunk tile to one below it), and gives one `Wood` per trunk tile, the mined one
  included. Mining a middle tile leaves the stump below. The drop must fit as a whole or the mining is held back (the tree stays). It reads tiles only,
  so it is deterministic and leaves the generated world (the golden) alone.

Mining and placing change tiles through the internal `TileGrid.SetTile`, which marks the chunk dirty (saves store dirty chunks).

## Caves, ores, vegetation and light (P7, generator version 4)

- **Caves** (`CavePass`, runs last): per chunk, a worm (a seeded random walk of 30 to 70 steps, heading drifting by a third of the time, a disc of
  radius 1 or 2 per step) with a 20 % chance, and a cavern (a 20 x 14 box, 45 % rock to start, four steps of the 4-5 rule: a cell becomes rock when five
  or more of its eight neighbours are rock; the box edge counts as rock) with a 12 % chance. **The rule that keeps the surface sealed:** a cave only
  carves dirt or stone whose depth below the surface column is at least 7 (`CavePass.MinDepth`). So no cave opens within six tiles of the surface
  anywhere, the spawn area included, and the grass pass (which runs earlier) never meets a cave. Caves are closed: light reaches them only from torches.
- **Ores** (`OrePass`, runs last): a vein is a seeded random walk of 4 to 9 cells that turns stone (never dirt) into ore, only inside the ore's depth band
  below the surface column. Per chunk and ore: coal 8 to 60 tiles deep (35 % of chunks), copper 20 to 90 (30 %), iron 40 to 150 (25 %), gold 110 to 260
  (15 %). Seed 42 has 71 coal, 124 copper, 142 iron and 120 gold tiles, each inside its band (`CavesAndOresTests`).
- **Tiles** (appended, ids stable): `CoalOre` 18, `CopperOre` 19, `IronOre` 20, `GoldOre` 21 (solid; pickaxe; drop `Coal`, `Copper`, `Iron`, `Gold`;
  mining 60, 75, 90 and 105 ticks against 75 for stone), `Torch` 22 (not solid, 6 ticks, drops `Torch`, placed from `Torch`), `TallGrass` 23 (not solid,
  6 ticks, drops nothing). **Items** (appended): `Coal` 17, `Copper` 18, `Iron` 19, `Gold` 20, `Torch` 21 (placeable), and `Lamp` 22, `Bucket` 23,
  `Ring` 24 (crafted, one per slot, not placeable yet). Metal tools (a pickaxe of iron) are not in the prototype: the tool tiers would need multi-word
  nouns such as "pico de hierro", which the `[NOUN]` slot cannot bind (one token), so they are out of scope for P7.
- **Vegetation**: the flora pass puts tall grass (`TallGrass`) on about 30 % of the free grass columns (the flower, mushroom and tall grass thresholds
  are 7, 10 and 40 of 100). Trees are unchanged: the forest and spawn-tree passes draw exactly as before, so the tree bases and the spawn of seed 42
  are the same, and the nearest tree is still eight blocks from the spawn.
- **Golden**: seed 42 at version 4 is `d96279bf…e69ae1` (was `6d148dca…4f5`); the tree bases and the spawn did not move.
- **Light** (`LightMap`, a pure integer function of the tiles): the light of a tile is the maximum of three sources, each losing `Falloff` = 2 per
  step through tiles that do not block light (a solid tile takes light from its neighbours but does not pass it on). **Sky:** each column is full
  light (16) down to and including its first solid tile, so an open column is lit and a closed one is dark below its roof. **Torches:** a torch is a
  source at 16, so it lights a disc of radius 8 (`TorchRadius`). **Spread:** the light under an overhang falls off sideways. `Brightness` gives
  solid rock at least `RockFloor` (6), so an unlit cave wall stays readable while the open air of a dark cave is black.
- **Incremental update:** a change in column x recomputes the columns x-9 to x+9 over the full height, reading the unchanged columns on either side as
  the boundary (`TilesChanged`). Light moves at most eight steps, so the result is the same as a full recompute (a test compares the two after
  mining). Mining and placing call it; a host that never reads `Light` pays nothing (it is computed on first use). Timing on seed 42, measured in a
  debug test build on the development machine: a full map about 65 ms (once, at load), an incremental change a median of 0.9 ms, the longest 6.5 ms.
- **Debug:** `GameSimulation.TeleportTo(tile)` puts the body's feet on a tile at rest. It is not a command and not a game rule; the front-end's
  `--teleport` step calls it (see the MonoGame README).

## Tests

`tests/Vocaterra.Simulation.Tests`: the seed-42 golden, same-seed and other-seed digests, properties over twelve seeds (spawn on the surface
with air above, no floating trees, terrain within bounds, a tree within 20 columns of the spawn for 150 seeds), physics on hand-built worlds (landing, walls, ledges, jumps, coyote time, the
world edge) and on generated terrain, P6's `InventoryTests` (stacks, limits, the hotbar, removals), `MiningAndPlacingTests` (time, tools, drops,
reach, the full inventory, placing rules, determinism) and `SurfaceDressingTests` (sand, flowers, mushrooms, the trees and the spawn unchanged), P6b's `CraftingTests` (ingredients, stations and their reach, the full inventory, the command, determinism) and `FellingTests` (base, middle, neighbour, full inventory), and an ASCII dump of the area around the spawn in the test output
(`dotnet test --logger "console;verbosity=detailed" --filter AsciiMap`).
P7: `CavesAndOresTests` (ore bands for seed 42 and over seeds, no cave within six tiles of the surface, the spawn area intact, every seed has a
cave, the same seed gives the same world at version 4) and `OresAndLightTests` (the pickaxe drops each ore's item, the axe cannot mine ore, the
harder ores take longer, the torch is placed and mined back, the sky lights open air and not rock below, a sealed cave is dark, a torch falls off by
two per tile and reaches eight, mining a roof to the sky opens the cave and the light falls off sideways, an incremental change equals a full
recompute, the light map is a pure function of the tiles). `GenerationTests` and `SurfaceDressingTests` carry the version-4 golden and pass list.

## The 3D voxel world (S1, `Vocaterra.Simulation.Voxel`; the route in S3)

The prototype's world (docs/SCENARIO_PLAN.md): a hand-authored 96 x 32 x 96 block scene, a first-person body, gathering and placing, all
headless and integer-only (the 2D code above is untouched). The folder is `src/Vocaterra.Simulation/Voxel/`; the prototype route is
`content/game-v1/game/scene.json` in the bundle (S3).

```csharp
var layout = SceneLayout.Load("game/content/game-v1/game/scene.json");     // or SceneLayout.Parse(json)
var scene = SceneBuilder.Build(layout, seed: 42);                          // a pure function of (layout, seed)
var sim = VoxelSimulation.Create(scene);                                   // the player stands at scene.Spawn
sim.Step([VoxelCommand.Look(Angles.FromDegrees(90), 0), VoxelCommand.MoveRelative(0, 1)]);   // one 1/60 s tick
// read: sim.Grid, sim.Player (eye, yaw, pitch), sim.Inventory, sim.Events, sim.Target (what the crosshair is on)
```

| Type | Role |
| --- | --- |
| `BlockId : ushort`, `BlockDefinition` | air, grass, dirt, stone, sand, trunk, leaves, bush, berry bush, water, path, planks, door, table (the workbench, `mesa`), torch, stall, roof, fence, wall (0 to 18), then S1's `FireRing` 19 (the unlit fire ring), `Sign` 20 (the signpost board) and `Bridge` 21; append-only, hashed. A definition holds `Solid`, `Transparent`, `Liquid` (renderer flags), `GatherTicks`, `Drop`, `DropCount` and `BecomesWhenGathered`. Trunk: 60 ticks, 1 `wood`, leaves air. Berry bush: 45 ticks, 1 `berry`, leaves a plain bush. `BlockDefinition.PlacedBy(item)` is the item to block map (wood to planks, torch, table, door, stone, dirt, sand) |
| `VoxelGrid`, `VoxelChunk`, `BlockPos`, `VoxelChunkCoord`, `BlockFace` | 16 x 16 x 16 chunks, bounded; `GetBlock` (air outside), `IsSolid` (solid outside), `HighestBlock(x, z)`; `Chunks` are indexed `(cy * ChunksZ + cz) * ChunksX + cx`; a chunk stores `(localY * 16 + localZ) * 16 + localX` |
| `SceneLayout`, `SceneBuilder`, `Scene` | the layout file, the deterministic builder, and the result: `Grid`, `Spawn`, `Npcs`, `Points`, `TreeCount` |
| `PlayerBody3D`, `VoxelPhysics`, `Angles` | the body and its constants, and the integer angle table |
| `VoxelCommand`, `VoxelCommandKind` | `Move(dx, dz)`, `MoveRelative(strafe, forward)`, `Look(yaw, pitch)`, `Jump`, `Interact(x, y, z)`, `Place(item, x, y, z)`, `SelectSlot(slot)`, `Craft(index)` |
| `VoxelSimulation` | `Create(scene)`, `Step(commands)`, `Tick`, `Player`, `Inventory`, `Events`, `Target`, `GatherTarget`/`GatherProgress`, `SetRecipes`, `CanCraft`/`Craft`, `StationInReach`, `GrantItems`, `TeleportTo(feetCell)`, `ComputeStateDigest()` |
| `BlockRay`, `BlockRayHit` | the 3D DDA: `Cast(grid, body)` or `Cast(grid, eye, yaw, pitch, reach)`; the first non-air block within 4 blocks, its face and the cell in front of the face |
| `AsciiMap3D` | `TopDown(scene)` (the highest block of each column, the spawn `@` and the NPCs `G K V` drawn over it) and `SideSlice(grid, z, x0, width, yTop, height)` |

Reused as they are: `WorldRng`, `Inventory`, `ItemDefinition`, `Recipe` and `CraftingRules` (a recipe's `TileId.Table` station means the
`Table` block), `SimulationEvent` (extended with `X, Y, Z, Detail, Amount` and the kinds `GatherProgress` 5, `BlockRemoved` 6,
`ItemPickedUp` 7, `BlockPlaced` 8). One item was appended, `ItemId.Berry` 25, kept out of `ItemDefinition.All` (it is in `VoxelOnly`) so the 2D
noun catalogue and its tests do not see it; S4 folds the lists together.

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

### The layout file

JSON (comments and trailing commas allowed); `SceneLayout.Parse` documents the shape: `size` {x, y, z} (multiples of 16), `waterLevel`, a
`legend` (a feature character to a `top` block, a `sub` block for the three layers under it with stone below, `water`, `plantable`,
`reserved`), `heights` and `features` (one string per row, north first, one character per column, west first; a height is a base-36 digit,
the y of the column's top block), `props`, `npcs`, `spawn`, `points` and `trees`. Props: `workbench` {length, axis}, `fire_ring` (x, z the
centre), `stall` {x, z the north-west corner, width, depth, facing = the counter side}, `house` {x, z north-west, width, depth, height,
facing = the door side}, `signpost`, `bridge` {x, z the first block of the centre line, facing, length, width}, `bush`, `berry_bush`, `tree`; a
`name` on a prop registers a named point. A prop on uneven or wet ground is rejected with a message (`SceneLayoutException`), so the layout
cannot silently build a floating house. A *named point* is the block on top of the terrain at that column (the water surface over the
river); NPCs and the spawn are given as a column and stand in the cell above the ground. `facing` is a compass word or whole degrees.

The tree scatter is the only use of the seed: `WorldRng.For(seed, "trees")` draws `attempts` positions, and a tree is planted when the
column is `plantable`, at least `margin` columns (Chebyshev) from every route column (the legend's `reserved` columns, prop footprints, the
spawn, the NPCs and the points) and at least `spacing` from another trunk, up to `max` trees. The canopy reaches two columns, so a margin of
3 keeps leaves off the path. Another seed changes the trees and nothing else (a test checks block by block).

### Digests (for the renderer and for saves)

`VoxelGrid.ComputeDigest()` is the SHA-256 of the three sizes (int32 little-endian) and the 32-byte digest of every chunk in `Chunks` order;
a chunk digest is the SHA-256 of its 4096 block ids as 16-bit little-endian. Both are cached and every real edit invalidates them. To
**re-mesh**, compare `VoxelChunk.Version` (it increases on every edit of that chunk) with the value you meshed, or `VoxelGrid.EditCount` for
"anything changed"; the chunk to rebuild is `VoxelChunkCoord.FromBlock(x, y, z)` of an event's position (and the neighbour chunk when the
edit is on a chunk border, because the faces there depend on it). `VoxelSimulation.ComputeStateDigest()` adds the tick, the body, the
selection, the gather progress and the inventory.

### Tests

`tests/Vocaterra.Simulation.Tests/Voxel/`: `SceneTests` (the seed-42 golden and the printed top-down map of the route, identical builds, another
seed differing only in trees, trees never on or over the route for three seeds, trunk spacing, the named points and NPCs, a pathfinder plus the
real body walking from the camp over the bridge to the far bank, rejected layouts), `VoxelGridTests` (chunks, bounds, the digest changing on
every edit and only in the edited chunk), `AnglesTests`, `PlayerBody3DTests` (landing, walls, the one-block step and the two-block wall, jumps,
edges, diagonal speed, relative movement, and the never-inside-a-solid property over 24 random worlds and 6 route seeds), `BlockRayTests`
(known poses and faces, the reach limit to the unit, a property test against a fine march) and `GatheringAndPlacingTests` (trunk, berry bush,
reach, a full inventory, placing rules, the torch at the workbench and the fire ring). `DeterminismRulesTests` scans the Voxel folder too and
also forbids every floating-point type there. Print the route:
`dotnet test game/tests/Vocaterra.Simulation.Tests --filter "FullyQualifiedName~Voxel" --logger "console;verbosity=detailed"`.
