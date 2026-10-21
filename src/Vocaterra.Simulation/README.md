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
| `PlayerCommand`, `PlayerCommandKind` | the only input: `MoveLeft`, `MoveRight`, `Jump`, each a level for one tick (send the held ones every tick); two integer arguments are reserved for targeted commands (mine or place at a tile, hotbar slot) added in P6; unknown kinds are ignored |
| `PlayerState` | position and velocity of the player box, `Grounded`, `Facing`, `FeetTile`, drawing helpers in tiles |
| `SimulationEvent` | `PlayerJumped`, `PlayerLanded` for the last step (more kinds are appended) |
| `World` | read-only queries: `GetTile`, `IsSolid`, `Spawn`, `Tiles`, `Info` (seed, size, generator version), `Record`, `ComputeDigestHex()` |
| `TileGrid`, `Chunk`, `ChunkCoord`, `TilePoint` | 32 x 32 chunks, `GetChunk`, dirty flags (a save stores dirty chunks), `ComputeDigest()` |
| `TileId`, `TileDefinition` | `Air`, `Dirt`, `Grass`, `Stone`, `Trunk`, `Leaves`; values are stable (they are hashed and saved) |
| `WorldGenerator` | `Generate(seed[, size])`, `Version` (2), surface bounds for a height |
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
entrances, ore counts). The current order is `terrain`, `layers`, `grass`, `forest`, `spawn`, `spawn-tree`. Caves and ores go between `layers`
and `grass` (so cave mouths in dirt get grass), vegetation after `forest`; the two spawn passes stay last because they need the final tiles.
`spawn-tree` (generator version 2, P3) guarantees a tree base within 20 columns of the spawn: if the forest left none it plants one 10 to 20
columns away (left before right, nearest first) that suits a tree as the forest pass requires and keeps three columns from every other trunk. It
changed 2 of the first 150 seeds on the 512 x 256 test world; seed 42 already had a tree near the spawn, so its golden is unchanged
(`0505865b...58a2`). Give a new pass a
test with a golden and properties, as `GenerationTests` does.

Mining and building (P6) add commands and events and mutate tiles through an internal `TileGrid.SetTile` called from `GameSimulation`; the dirty
flag per chunk is already maintained. Tiles other than the foreground layer (walls, light) become further arrays in `Chunk`.

## Tests

`tests/Vocaterra.Simulation.Tests`: the seed-42 golden, same-seed and other-seed digests, properties over twelve seeds (spawn on the surface
with air above, no floating trees, terrain within bounds, a tree within 20 columns of the spawn for 150 seeds), physics on hand-built worlds (landing, walls, ledges, jumps, coyote time, the
world edge) and on generated terrain, and an ASCII dump of the area around the spawn in the test output
(`dotnet test --logger "console;verbosity=detailed" --filter AsciiMap`).
