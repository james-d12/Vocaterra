# Vocaterra.Simulation

The engine-agnostic game world: tiles in chunks, seeded generation, the player body and a fixed 60 Hz tick. It takes no packages and no
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
| `TileId`, `TileDefinition` | `Air`, `Dirt`, `Grass`, `Stone`, `Trunk`, `Leaves`, then P6's `Sand`, `Flower`, `Mushroom`, and the placed `Planks`, `Door`, `Table`, `Chair`, `Bed`, `Chest`, then P6b's `Furnace`, `Glass`, `Brick`; values are stable (they are hashed and saved); each definition has its solidity, mining ticks, required tool and drop |
| `WorldGenerator` | `Generate(seed[, size])`, `Version` (3), surface bounds for a height |
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
entrances, ore counts). The current order is `terrain`, `layers`, `grass`, `forest`, `spawn`, `spawn-tree`, `sand`, `flora`. Caves and ores go
between `layers` and `grass` (so cave mouths in dirt get grass), vegetation after `forest`; the spawn passes come before the surface dressing
because they need the final trees and terrain.
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

## Tests

`tests/Vocaterra.Simulation.Tests`: the seed-42 golden, same-seed and other-seed digests, properties over twelve seeds (spawn on the surface
with air above, no floating trees, terrain within bounds, a tree within 20 columns of the spawn for 150 seeds), physics on hand-built worlds (landing, walls, ledges, jumps, coyote time, the
world edge) and on generated terrain, P6's `InventoryTests` (stacks, limits, the hotbar, removals), `MiningAndPlacingTests` (time, tools, drops,
reach, the full inventory, placing rules, determinism) and `SurfaceDressingTests` (sand, flowers, mushrooms, the trees and the spawn unchanged), P6b's `CraftingTests` (ingredients, stations and their reach, the full inventory, the command, determinism) and `FellingTests` (base, middle, neighbour, full inventory), and an ASCII dump of the area around the spawn in the test output
(`dotnet test --logger "console;verbosity=detailed" --filter AsciiMap`).
