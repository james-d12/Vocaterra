# Procedural world and biomes: the B series

A plan written on 2026-11-15 at the owner's request, from a read of `main` at `c7124db3` (the branch was cut at `9e17c9aa`, which changes
no world code). Nothing was built or run. Refreshed the same day after `feature/biomes` was fast-forwarded to `main` at `0db529a1` (PR #65,
the settlement series): saves are already at version 8, so B05 makes version 9; block ids now run to 56; B08 no longer waits; and the line
references were checked again.

The goal is **an endless, streamed, tall world**, like Minecraft's since 1.18.
- **Above ground:** most of it is generated procedurally, with biomes that include plains, forests, high meadows, great mountain ranges,
  rivers, lakes, beaches and sea.
- **Below ground:** caves, lava, a few kinds of stone, and iron, tin and copper ores.
- **Hand-crafted regions** are kept: first the valley, with its village and its mountain ring, then other towns authored the same way.

The world, biome and underground generation is built from small modules, so later systems can read it without changing the generator. The
first such system is temperature.

**Status (2026-11-15):** accepted by the owner, with the answers in section 5.
- **D-game-34** is decided (`docs/game/decisions.md`). B00 registers the milestones in `docs/game/implementation-plan.md` section 4.
- The answers changed the scope: the pass opens only once the lessons are complete (or with a cheat setting), and nothing new can be
  gathered, so the ores carry no items or nouns (sections 3.5, 3.6 and 4). D-game-32 is used on `main` (the settlement series), and D-game-33 is reserved on `feature/talk-sets`.
- It lives on `feature/biomes`, cut from `main` at `9e17c9aa` and fast-forwarded to `0db529a1` on 2026-11-15, in the worktree
  `.claude/worktrees/biomes`.
- The commit prefix is `B<nn>:`. Nothing is pushed to `main`; the owner merges by pull request.

## 1. The request

> Against the latest main branch, can we investigate how to add procedural generation to the game. However with my game i want areas of
> custom crafted, like what exists now with the village, and mountains. But outside of that I want it to be procedurally generated. My
> idea is that as the game expands, I can have areas of custom placement, like other towns, etc, but most of the world is procedural like
> minecraft. For the procedural generation, to start with I want basic biomes like minecraft, so forests, plains, rivers, mountains. etc.

The owner then added:

> With the new system it needs to be modular code and be able to work with other systems. For example i plan on adding temperature later
> on. The code and biome and world generation needs to be modular and follow SOLID principles

> What about caves and underground? we basically want what minecraft has. They dont need to be overly complicated, but the basic lava,
> underground caves for now is ok, perhaps with a few different typers of stones to change things up. We need ores as well, for now just
> basic iron, tin ,copper, will do.

> also the y axis heighs, i think we need high y axis, to make tall large mountains, sometihng like in 1.18 mc with the wilderness bound
> update that added huge mountains

The page the owner linked (minecraft.wiki, "Wilderness Bound") describes a later update: dappled forests, poplar trees and abandoned camps.
The tall mountains and the deep noise caves came with Minecraft 1.18, which set the world's height at −64 to 320. That is the reference
used here.

The owner's answers to the follow-up questions (2026-11-15) are in section 5. The owner's standing rule also applies: the code follows
`docs/process/code-guidelines.md`:
- single responsibility and composition;
- logic in `Vocaterra.Game.Core`, drawing in `Vocaterra.Game`;
- Given_When_Then tests in folders that mirror the source.

### Why this changes the direction

Three earlier documents rule this out today:
- The prototype brief (`docs/game/design/prototype-brief.md`, LEAVE FOR LATER) lists "Procedural terrain, multiple biomes or a large open
  world".
- D-game-22 says the world grows inward.
- The adaptation design's decision 15 keeps play inside the valley until after the pilot.

The series therefore needs a new owner decision that amends all three: D-game-34 (section 5). It also takes over phase 7 of the settlement
plan ("Larger valley", `docs/game/settlement-plan.md`), which was a fixed 512×512 world. The activities plan's note that "ores need
terrain work" (`docs/game/activities-plan.md`) is the terrain work it meant.

## 2. What exists today (on `main`)

- **A fixed 384×128×384 box, built in full at load.**
  - `SceneBuilder.Build(layout, seed)` (`src/Vocaterra.Game.Core/World/Generation/SceneBuilder.cs:33`) generates the whole valley
    synchronously, in about 0.6 s, holding about 38 MB. It reads `content/game-v1/game/scene.json`.
  - The relief is a hand-drawn grid of one base-32 character per chunk corner (`scene.json`, `terrain.relief`). The grid's size must match
    the world's.
  - `TerrainGenerator.cs` adds noise, domain warp, terraces and site levelling, then carves the single river, lake and waterfall. It works
    in whole-map passes over `int[w*d]` arrays.
  - Heights run from 0 to 128: water level 32, valley floor 36, peaks 108 to 116, ceiling 124.
  - Below the surface everything is plain stone: no caves, no ores, no lava, no layers.
- **The world's edge is a wall.**
  - `VoxelGrid.IsSolid` reads solid outside the grid (`Voxel/VoxelGrid.cs:106`), and writes outside it throw.
  - The mesher treats y < 0 as stone.
  - The ring of authored mountains hides the edge.
- **Storage is dense.**
  - `VoxelGrid` holds a `VoxelChunk[]` with every chunk allocated (`VoxelGrid.cs:14`).
  - The dense index `((cy*ChunksZ)+cz)*ChunksX+cx` is copied in `ChunkVersionTracker.cs:54`, `ChunkViewSet.cs:74`, `WorldFacts.cs:131` and
    `SaveMigration.cs:104`.
  - A `VoxelChunk` is 16³ `ushort` block ids, with a lazy `KindMask` of one bit per kind. That caps block ids at 64. `main` uses 57
    (0 to 56, with the settlement series' beds, bedrolls and counter), which leaves 7 free.
- **Generation is not chunk-local.** The forest draws one sequential `WorldRng.For(seed, "trees")` stream over the whole map
  (`SceneBuilder.cs:1124`), so changing the world's extent reshuffles every tree.
- **Saves diff the whole world.**
  - `VoxelGridDiff.Capture` compares every chunk against a pristine full rebuild. That rebuild is made on the first save and kept, costing a
    0.7 s hitch and 38 MB (`Save/VoxelGridDiff.cs:26`, `Save/SaveService.cs:37`).
  - Edits are keyed by the dense chunk index, plus `WorldDims`.
- **Several systems scan the whole world.**
  - `WorldFacts.Snapshot` copies every non-empty chunk (about 28 MB) on each turn of the guide.
  - `BlocksOfKind` is used for fire rings and the renderer's lights.
  - `ColumnCover` (rain), `RiverCurrents`, `AmbientBeds` (sound places from the layout) and `ChunkViewSet` (counts empty chunks over all
    4,608) also assume the whole world is in memory.
- **The state digest hashes the whole grid.** It feeds NPC turns as `hostStateHash`, and saves check it. Goldens pin it:
  `SceneTests.GoldenDigest`, `AtmosphereSimulationTests` and the save fixtures.
- **Light is baked, not propagated.**
  - Each face gets a fixed sun shade, ambient occlusion and a time-of-day band (`LightBands`).
  - Stone gets a height-banded tint (`ChunkMesher.RockTint`, `:586`).
  - Lanterns glow by distance from a list found once (`VoxelRenderer.FindLights`, `:173`).
  - With nothing darkening a face by its depth under ground, a cave would be lit like a meadow.
- **Gathering is by hand, block by block.**
  - `BlockDefinition` (`Voxel/BlockDefinition.cs:45`) gives each block its gather ticks, its drop (an `ItemId`) and what it leaves behind.
  - Trunks and berry bushes are gatherable. Stone, cobble and mossy stone are not.
  - Items are an `ItemId` enum (`Items/ItemDefinition.cs:12`), and their Spanish nouns are in `game/nouns.json`.
  - The needs model has health (`Needs/PlayerNeeds.cs`), but nothing in the world harms the player.
- **The sky.** The cloud layer floats at a fixed y of 168 (`Graphics/Sky/SkyModel.cs:105`).
- **Building blocks that already fit an endless world:**
  - `TerrainNoise` is integer value noise that depends only on the coordinate, so it is unbounded and correct for negative x and z.
  - `IntMath` provides fixed point (One = 4096); generation never uses floating point.
  - `WorldRng.For(seed, pass, chunkX, chunkY)` already takes chunk keys.
  - `FlowSimulation` is event-driven and never scans the world.
  - `ChunkMesher` holds no engine code.
  - The weather series' rule, "a kind is a row of data", is enforced by tests that forbid kind ids in `src`.
- **One thread.** World building, simulation, meshing and saving all run on the game loop's thread. The only background thread is the NLP
  `TurnWorker`, which reads an immutable `WorldFacts` snapshot.

## 3. Design

### 3.1 The shape in one paragraph

The world becomes a sparse, streamed `VoxelGrid`:
- **Extent:** y runs from −64 to 320; x and z are endless (in practice ±30,000 blocks).
- **Where chunks come from:** a pure `IChunkSource`, so a chunk is a function of the seed, the world data and its coordinates. A
  `CompositeChunkSource` hands out an authored **region's** chunks where there is one. The valley stays where it is, built by the unchanged
  `SceneBuilder`, so every coordinate, every save position and the valley's goldens survive. Everywhere else, and under the valley, the
  **procedural pipeline** fills the chunks, in this order:
  1. climate fields
  2. the height model
  3. biome choice from data
  4. surface shapers
  5. underground passes (strata, ores, caves, lava)
  6. decorators
- **Reading a chunk that isn't loaded generates it on the spot,** so the simulation can never tell which chunks are resident.
- **Saves keep only the chunks that changed.**

### 3.2 Modules

Tests mirror these folders. Everything is in `Vocaterra.Game.Core` unless the folder says Game.

| Folder | Contents | Responsibility |
|---|---|---|
| `World/Noise/` | `TerrainNoise` (moved here, pass names unchanged), `GradientNoise` (2D and 3D), `NoiseStack` (fBm, ridged, warp), `Spline`, `LatticeSampler3` | Integer noise primitives |
| `World/Climate/` | `IClimateSampler`, `ClimateSample`, `ClimateSampler` | Continuous climate at (x, z), and its change with altitude |
| `World/Biomes/` | `BiomeRow`, `BiomeTable : IBiomeSelector`, `BiomeData`, `BiomeValidator` | Biome choice, from data |
| `World/Underground/` | `UndergroundData`, `StrataTable : IStrataSelector`, `OreTable`, `OreRow`, `CaveField : ICaveField`, `UndergroundValidator` | Rock layers, ores, caves and lava, from data |
| `World/Generation/Procedural/` | `IHeightModel`, `HeightModel`, `IColumnShaper`, `IChunkDecorator`, `GenerationPipeline`, the passes, `ProceduralChunkSource` | Turning climate into blocks |
| `World/Regions/` | `IRegion`, `RegionCatalog`, `AuthoredReliefRegion`, `ProceduralReliefRegion` (B15), `RegionBlend` | Hand-crafted areas |
| `World/Streaming/` | `IChunkSource`, `CompositeChunkSource`, `ChunkData`, `ChunkCoord3`, `BandStreamer` (`ChunkStreamer` until B24) | What is loaded |
| `World/` | `WorldFactory`, `WorldQuery : IPlaceInfo`, `WorldGenData` | Wiring, and the shared place query |
| `Voxel/` | `VoxelGrid` (sparse inside), `IVoxelReader`, internal `IVoxelWriter`, `IChunkResidencyListener` | Storage |
| Game: `Streaming/` | `JobPool` (its lanes: residency, mesh, far tiles, ring; `ChunkPrefetcher` until B07) | Worker threads that call the pure source |

Notes on the table:
- **`ProceduralChunkSource`'s passes** are the surface, river banks, the `BankBesideWater` port, strata, ores, caves, lava, the floor, trees
  and undergrowth.
- **`BandStreamer`** (`ChunkStreamer` until B24) holds the residency policy only; the resident cap is an admission cap, with the plan's eviction freeing room (B24).
- **`WorldFactory`** is the composition root.
- **`AuthoredReliefRegion`** is the valley. **`ProceduralReliefRegion`** places towns.

**The seams.** Every implementation behind them is pure, immutable, integer-only and safe to call from any thread.

```csharp
public readonly record struct ClimateSample(int Temperature, int Humidity, int Continentalness, int Erosion, int Ridges, int River);
public interface IClimateSampler  { ClimateSample At(int x, int z); int TemperatureAt(int x, int y, int z); }  // y: lapse with altitude
public interface IHeightModel     { ColumnShape Shape(int x, int z, in ClimateSample c); }
public interface IBiomeSelector   { BiomeRow Select(in ClimateSample c, int height, int x, int z); }   // a data row, no enum
public interface IStrataSelector  { BlockId RockAt(int x, int y, int z, int surface); }                  // stone, slate, granite, limestone
public interface ICaveField       { CaveCell At(int x, int y, int z, in ColumnShape column); }         // solid, air or lava
internal interface IColumnShaper   { string PassName { get; } void Shape(in ColumnContext ctx, IVoxelWriter chunk); }
internal interface IChunkDecorator { string PassName { get; } void Decorate(in ColumnContext ctx, IVoxelWriter chunk); }
public interface IChunkSource     { ChunkData Generate(ChunkCoord3 coord); }
public interface IRegion          { string Id { get; } RegionBounds Bounds { get; } bool Covers(ChunkCoord3 c); ChunkData Copy(ChunkCoord3 c); int EdgeHeight(int x, int z); }
public interface IPlaceInfo       { ClimateSample ClimateAt(int x, int z); int TemperatureAt(int x, int y, int z); BiomeRow BiomeAt(int x, int z);
                                    IRegion? RegionAt(int x, int z); int NaturalSurfaceAt(int x, int z); bool IsUnderground(int x, int y, int z); }
```

**How the principles apply.**
- **Single responsibility.** Climate, height, biome choice, strata, ores, caves, shaping, decoration, regions, residency and storage are
  separate types, each testable alone.
- **Open/closed.**
  - A new feature is a new pass, registered in `WorldFactory` in a fixed order.
  - A pass's unique `PassName` is also its `WorldRng` and noise key. A test checks the names are unique and differ from the valley's
    (`terrain`, `forest`, `trees`, `treeshape`, `undergrowth`).
  - A new biome, rock or ore is a row of data. A new kind of region is a new `IRegion`.
- **Interface segregation.** A consumer that needs blocks takes `IVoxelReader`. One that needs to know about a place takes `IPlaceInfo`.
  Writing stays `internal` to the simulation.
- **Dependency inversion.**
  - Physics, the renderer, sound, the rain cover, the guide's `WorldFacts` and the future temperature system depend on those interfaces,
    never on the generator.
  - `WorldFactory.Create(GameData, seed, mode)` replaces the call to `SceneBuilder.Build` at `src/Vocaterra.Game/Program.cs:90`.
- **One place query for everyone.**
  - `IPlaceInfo` is exposed as `Scene.Places`.
  - It reads the pure model, not the edited grid, so the guide's `TurnWorker` can query it off-thread without a snapshot.
  - Sound, weather, `WorldFacts`, the settlement series' runtime place lookup and the temperature system all read it.
- **Data, not code.**
  - Biome, rock-layer and ore ids never appear in `src`. `NoBiomeIdsInSourceTests` and `NoOreIdsInSourceTests` mirror
    `NoWeatherKindIdsInSourceTests`.
  - A biome or an ore defined only in test data must generate with no code change, as the weather series' E04 did with its fourth kind.
  - The blocks themselves stay `BlockId` values with code-drawn art; data rows name them. Tree species and crown shapes stay code
    strategies, also named by the data.
  - **Generation behaviour stays in code** (owner, 2026-11-15: "The data should just be for basic stuff like, you know, the types of
    biomes, but the biome generation code might be different"). Data holds the biome types and their parameters; how a kind of land is
    shaped (dunes in a desert, crags on a range) is code. A biome row may name code strategies for its own surface shaping and
    decoration, as it names tree species: the strategy has its own name (`dunes`, never a biome id), is registered in `WorldFactory`, and
    fades out at the biome's border so a border never makes a cliff. B03 builds the registry with the default strategies.
- **Purity guards.**
  - A reflection test checks that seam implementations hold only readonly fields and no static mutable state.
  - A token ban keeps `Thread`, `Task`, `lock` and `Interlocked` out of `World/Noise`, `World/Climate`, `World/Biomes`,
    `World/Underground` and `World/Generation`.

### 3.3 How a temperature system plugs in later

A temperature system needs no change to the generator. A `Core/Temperature/PlaceTemperature` would read `IPlaceInfo`, a
`temperature.json`, the weather series' `AtmosphereSnapshot` and shelter. Its value is built from:
- **Climate:** `IPlaceInfo.TemperatureAt(x, y, z)`, the continuous climate temperature with its lapse over altitude. It is the same lapse
  that decides where snow lies on the peaks, so the cold the player feels and the snow they see agree.
- **Day:** plus a day swing from `Daylight`.
- **Weather:** minus cooling from the `WeatherChannels` (cloud cover, precipitation, wind), never from kind ids, following the weather plan's
  rule that consumers read channels.
- **Shelter:** plus a term from `ListenerShelter` or `ColumnCover`.
- **Underground:** a pull towards a steady cave temperature when `IsUnderground`.

It uses fixed point if it feeds the needs simulation. A biome row may also carry an optional base temperature, as data only.

### 3.4 The tall world and the surface model

All maths is fixed point (`IntMath.One` = 4096). Every parameter is data in `world.json` and `biomes.json`.

- **The vertical range.**
  - y runs from **−64 to 320**: 24 chunk layers instead of 8, set in `world.json`.
  - The **sea level stays 32**, so the valley keeps every coordinate (its water, sites, saves and scripted runs). That leaves 96 blocks of
    underground below the sea and 288 above it. Minecraft has 127 and 257, around a sea level of 63.
  - The valley fills chunk layers 0 to 7 of its columns. Above them is air; below them, from −64 to 0, the procedural underground
    (section 3.5).
- **Noise.**
  - Reuse `TerrainNoise` and `IntMath.Ramp`, `Lerp` and `Smooth`.
  - Add integer gradient noise in 2D and 3D with a quintic fade, because value noise gives square-ish contours at large cells. Also add
    `Fbm`, `Ridged`, `Warp`, and a `Spline` whose control points are data.
  - Move `Terrace` (`TerrainGenerator.cs:195`) and `BankHeight` (`:328`) into a shared helper unchanged. The valley's golden guards this.
  - Climate is sampled on a 4-block lattice and interpolated. Detail is computed for each column.
- **Fields**, in the manner of Minecraft 1.18's continentalness, erosion and peaks-and-valleys:

  | Field | Noise and scale (blocks) | Use |
  |---|---|---|
  | Continentalness C | gradient fBm 2048 to 256, warp 192 | sea, coast, inland, far inland |
  | Erosion E | gradient fBm 1536 to 384 | low E is rugged and high; high E is flat and low |
  | Ridges R | ridged noise 640, 320, 160, warp 48 | peaks and valleys within the ranges |
  | Temperature T, humidity H | 3072 to 1024 | the biome lookup, and later temperature |
  | River V | 1024, 512, warp 96 | rivers where V is near zero |
  | Lakes Lk | 256 | inland basins |
  | Detail | `Hills` (64, 32, 16, 8) and crags | the surface |

- **Height.** Three splines from data, combined so that a biome border can never make a cliff:
  - **Base, from C:** sea floor 12 to 26, coast 30 to 35, lowlands 38 to 60, uplands 70 to 120.
  - **Range factor, from E:** 0 on eroded flats, 1 in the rugged highlands.
  - **Peaks, from R:** valleys 0, ridges up to about 190 blocks.
  - **Combined:** height = base + factor × peaks + detail.
  - **Results:** gentle plains and forests near the sea; high meadows and plateaus at 100 to 160; great ranges whose peaks reach 220 to
    300. A soft cap above 280 slows the rise to half rate, and the height is clamped to 310.
  - Terraces (step 6) on the steep middle slopes. Jagged crags only on the highest ridges.
- **Snow and trees follow temperature at altitude.**
  - `TemperatureAt(x, y, z)` falls with height. Snow lies where it drops below a data threshold, so a cold range is white lower down than a
    warm one.
  - The tree line is the same rule with its own threshold.
  - The stone tint bands of `RockTint` become altitude data instead of fixed heights.
- **Water.** Every procedural water block is a source at sea level, so all of it is at rest from the start (H07).
  - **Rivers** carve where |V| is small, down to a bed below the water level (32), with banks from `BankHeight`. They fade out where the
    land rises above about 72, in a region's margin and towards the sea. Like Minecraft's, they have no current and no waterfalls.
  - **Lakes** pull the height towards 29 in low, calm basins.
  - **Every column below 32** fills to 32, as `TerrainGenerator.Finish` does.
  - **The ported `BankBesideWater`** keeps every water edge exitable (D-game-26).
- **Biomes.** Ordered data rows; the first match wins. The validator requires a final catch-all row. All are made from existing blocks:
  - **Water and coast:** ocean, beach, river.
  - **Mountains**, in Minecraft 1.18's manner:
    - jagged peaks (stone and snow on the highest cold ridges);
    - stony peaks (bare stone on warm high ridges);
    - snowy slopes (snow over stone);
    - grove (spruce on snowy slopes);
    - meadow (high grassland with many flowers and no trees).
  - **Then a Whittaker lookup over T × H:** taiga (spruce), plains, forest (oak and birch), birch or dense forest.
  - Borders are dithered by jittering T and H a little.
- **Decoration.**
  - Trees stand on a jittered grid of 4×4 cells per chunk, each cell inside one chunk, seeded by `WorldRng.For(seed, pass, cx, cz)`.
  - Spacing is settled by hash priority among neighbouring cells. This replaces the valley's order-dependent stream, which stays as it is
    inside the valley.
  - Crowns come from the candidates of the neighbouring chunks, and only the blocks inside the chunk being generated are written.
  - Trees need solid ground under the trunk, so none stands over a cave mouth.
  - Undergrowth stays driven by noise.

### 3.5 The underground

The underground is chunk-local and a pure function of (x, y, z). It is configured in `content/game-v1/game/underground.json`.

- **New blocks.** There are 9, so B09 widens the kind mask to 128 first:
  - **Rock:** slate (the deep stone, like Minecraft's deepslate), granite, limestone.
  - **Fluid and floor:** lava, and an unbreakable floor block ("bedrock").
  - **Ores:** iron ore, tin ore, copper ore.
  - **Gravel**, for cave floors and river beds.

  All their art is drawn in code, so the assets stay CC0 (D-game-5).
- **Strata** (`IStrataSelector`):
  - Stone fills from the surface layers down to about y 8.
  - Slate fills below y 0. Between 0 and 8 the change is dithered by a column hash, so the boundary is ragged.
  - Granite and limestone form large blobs from 3D noise in the stone layer.
  - The floor is a ragged layer of bedrock 1 to 4 blocks thick at y −64 to −60.
- **Ores** (`OreTable`, data rows):
  - Each row gives a block, a y range with a peak (a triangular distribution), veins per chunk, vein size, and the rocks it may replace.
  - Starting values, in the manner of 1.18:
    - copper is common from −16 to 112, peaking at 48;
    - iron runs from −24 to 56, peaking at 16, with a second band high in the mountains from 80 to 320;
    - tin is less common, from −32 to 48, peaking at 8.
  - Veins are blobs of 3 to 10 blocks placed with `WorldRng.For(seed, "ore:<id>", cx, cz)`. A vein never crosses its chunk's edge, which
    keeps generation chunk-local.
  - Ores replace rock only, never air, water or another ore.
  - The same ore block serves in stone and in slate, so no "deep" variants are needed.
- **Caves** (`ICaveField`): noise caves like Minecraft 1.18's.
  - **Cheese caverns:** large chambers where one 3D noise field is high, mostly deep down.
  - **Spaghetti tunnels:** long winding tunnels where two 3D noise fields are both near zero.
  - Both are sampled on a coarse lattice (4×8×4 blocks) and interpolated, which keeps the cost per chunk low.
  - Caves reach up into the mountains as well as down into the slate.
  - **Entrances:** tunnels may open at the surface only on dry procedural land, where an entrance mask allows. Never inside a region, on a
    path or within reach of water.
  - **Water never pours in.** A cave cell is kept solid if it lies within 3 blocks of any water cell or under a water column's bed. Every
    test of section 4 checks it.
- **Lava** fills the cave air below a lava level (data, y −54), so the bottom caverns hold lakes of lava.
  - Lava is a flat source at a single level, so it rests at once.
  - In this slice lava does not flow. Section 6 lists flow as later work.
- **Under the valley.** The procedural underground fills y −64 to 0 under the valley's columns from B11. B13 then lets the strata, ore and
  cave passes into the valley's own stone, at least 8 blocks under the surface and away from its sites, paths, water and village. That
  re-records the valley's goldens once, with the owner's review.
- **In play** (B10 and B12):
  - **Nothing new can be gathered** (owner, 2026-11-15: "this is just the world generation for it"). Rock, gravel and the three ores
    are not gatherable, so the underground is reached through cave entrances. The ores have no items and no nouns in this series; they
    wait for the crafting design (`docs/game/design/crafting-and-cooking.md`) and for digging and tools.
  - **Lava** blocks the body, so it can't be walked into. It glows and does no harm (owner, 2026-11-15).
  - **Bedrock** can never be removed, for when digging comes.
- **Light underground** (B12):
  - A face below its column's surface darkens with depth (sky exposure from the pure height model and the column's highest block), down
    to a cave minimum within about 6 blocks.
  - Lava, torches and lanterns light it. The per-column light index is rebuilt when a torch is placed or removed, so a placed torch lights
    a cave.
  - This approximates sky light without propagation. Section 6 lists propagated light as later work.

### 3.6 Regions, the blend and the pass

- **The valley is an `AuthoredReliefRegion`.**
  - It covers chunk columns 0 to 23 and chunk layers 0 to 7.
  - It is built once by the unchanged `SceneBuilder`, then frozen into immutable arrays so worker threads can't race on its lazy caches.
  - It also exports `Terrain.Height`, the edge profile used for blending.
  - Until B13 it stays byte-identical: `SceneTests.GoldenDigest` and `VillageTests` hold.
  - Its trunks keep 2 blocks from the edge, so its crowns never cross the border.
- **The blend sits outside the region,** over a margin of about 64 blocks (data).
  - The procedural height is eased towards the height at the nearest point of the region's edge.
  - Trees keep 3 blocks off the border and thin out across the margin. Rivers, lakes and cave entrances fade out in it.
  - Continentalness is pulled towards land within about 512 blocks, so no sea opens at the valley's feet.
  - Beyond the margin the land is free to rise into the great ranges. Seen from the pass, the valley's ring of 110-block peaks becomes the
    foothills of mountains two or three times their height.
  - An exit corridor declared in `world.json` keeps the way out of the pass walkable.
- **The pass** (B08a builds it, B08b its lock; B07 keeps and lifts the edge wall):
  - The east side of the ring, beside the village, is already its lowest part: in relief rows 12 to 14, columns 22 and 23 are `f` and `k`,
    heights 60 and 80 (column 21 is already `b`, height 44). They are lowered to `c` and `d`, heights 48 to 52.
  - A road runs from the village, about x 316, z 218, to the east edge at x 383.
  - A `paso` site and the exit corridor are added.
  - **The pass is closed until the lessons are done** (owner, 2026-11-15). It opens once every lesson in a list in `world.json` is
    complete; the list holds all of today's lessons. A cheat setting in the settings menu (`PlayerSettings`, saved with the player's
    settings, not the game) opens it at once. Until then:
    - a rockfall of existing blocks fills the pass, placed by data where the road meets the ring;
    - the valley's outer edge keeps today's invisible wall for the body, so the ring can't be climbed over; the land outside is drawn
      but can't be reached;
    - when the pass opens, the rockfall is removed (an event, saved like any change) and the wall lifts. It never closes again.
  - The unlock reads the learner's completed lessons through a Core query; no lesson id appears in `src`.
  - This edits `scene.json`, which the settlement series also edited (it emptied the mountain ledge). That series merged into `main` in
    PR #65 before B00, so B08 starts from its `scene.json` and waits for nothing but B06.
- **Towns** (B15):
  - An `IGroundShape` strategy inside `TerrainGenerator` chooses between the hand-drawn relief and the procedural height.
  - A `ProceduralReliefRegion` then places a layout's sites, props and paths at an offset on procedural ground, with currents and points
    that know their origin.
  - The valley stays byte-identical. This is the mechanism for every later town.

### 3.7 Storage, streaming and determinism

- **`VoxelGrid` keeps its name and its API.** Inside, it stores chunks sparsely by `ChunkCoord3` in a dictionary, with a cache of the last
  chunk used. All-air chunks are never allocated.
  - **Bounded mode** is today's constructor. `SceneBuilder`, the region template and the 41 tests that build grids keep it.
  - **Streamed mode** takes world limits from data: x and z ±30,000, y −64 to 320.
  - Every member that assumed y ≥ 0 (`GroundFinder`, `HighestBlock`, the mesher's "below the world is stone", `BlockRay`, `TeleportTo`)
    reads the world's minimum y instead.
  - The dense members (`Chunks`, `ChunksX` and `ChunksZ`, `BlocksOfKind`, the full `ComputeDigest`) stay valid only on bounded grids. An
    architecture test allow-lists their uses in `src`.
  - About 13 production files change, and the tests barely change.
- **Generate on read.** A read of a missing chunk inside the limits generates and installs it at once, on the game loop's thread.
  - **Installs are not edits.** They don't bump `EditCount` and don't wake `FlowSimulation`. A separate `ResidencyVersion` and an
    `IChunkResidencyListener` tell the renderer, the rain cover and the light index.
  - **Only unchanged chunks are evicted.** Changed chunks are state, and they stay in memory.
  - **Nothing in the simulation enumerates resident chunks.**
    - Fire rings come from the region template.
    - `WorldFacts` gets a window: the region the player stands in, which keeps the guide's answers in the valley byte-identical. Otherwise
      it is a box of radius 128, from 64 blocks below the player to 64 above.
  - **The residency policy lives in Core.** `BandStreamer` (`ChunkStreamer` until B24) returns the wanted chunks (nearest first, with a fixed tie-break) and the
    chunks that may be evicted. It changes performance, never results.
- **A surface band and a near zone keep a tall world affordable.** The view radius at High covers about 1,960 columns, and a mountain column
  can hold 20 full chunks. So:
  - **At a distance** only the *surface band* is resident and meshed: the chunks that meet the surface heights of the column and its
    neighbours. Those heights come from the pure height model, without generating anything.
  - **Chunks wholly underground** are resident and meshed only within a near radius of the player (about 4 chunks), or whenever the player
    is underground. Caves inside a distant mountain are never meshed.
  - A cave mouth seen from afar shows as a dark opening.
- **Threads live only in the Game project.**
  - The job pool (`ChunkPrefetcher` until B07) calls the pure source on its workers (2 until B07d's setting). The game loop installs what they produce, unless the chunk was already
    generated on read; duplicates are dropped.
  - Workers never touch `VoxelGrid`.
  - If generation on the game loop's thread, under a per-frame budget, keeps up with walking (about 51 columns for each chunk boundary
    crossed), the worker stays optional.
- **Scripted runs stay deterministic.**
  - Ticks don't depend on residency.
  - Before each `--shot`, a settle step installs and meshes everything in view (`Remesh(budget: null)`).
  - Residency counts stay out of the compared output.
  - Signed coordinates in steps come with B14 (`Scripted/VoxelStep.cs:54`, now `NumberStyles.None`).
- **The renderer.**
  - Its arrays are keyed by coordinate.
  - A chunk is meshed only when all 26 of its neighbours are resident. Ambient occlusion reads the diagonal cells (`ChunkMesher.cs:626`),
    water depth reads 3 cells down, and lantern glow reaches 9 blocks.
  - The renderer peeks without generating.
  - A per-column light index replaces the one-off global `FindLights` (`VoxelRenderer.cs:173`).
  - Eviction keeps one chunk of slack beyond `KeepDistance`, so a meshed chunk never loses a neighbour.
  - The far plane and fog allow for height, so a peak at 300 isn't cut off while its foot is in view.
  - Absolute float vertices are precise enough to ±30,000 blocks (about 1/512 of a block), so no camera-relative rendering is needed.
  - The cloud layer stays at 168 for now, so the great peaks rise through it. Its height becomes data in B14.

### 3.8 The state digest and saves (B05)

- **The world's state digest.**
  - A header comes first: the seed, the region template's digest, a `ProceduralGenerator.Version` constant, and a hash of the
    world-generation data (`world.json`, `biomes.json` and `underground.json` only, so editing a phrase doesn't move it).
  - Then the *changed* chunks follow, sorted, each written as its coordinates and its digest.
  - Each chunk records a `PristineDigest` lazily, just before its first write, so an edit that is later undone counts as unchanged.
  - Its cost grows with the number of changed chunks, not the size of the world.
- **Saves become version 9** (`main` is at version 8 since the settlement series).
  - `ChunkEdit(X, Y, Z, Runs, Water)` replaces the dense index, and `WorldDims` goes. Y may be negative.
  - The pristine world copy is removed, which also removes the 0.7 s first-save hitch and the extra 38 MB.
  - Restoring a save regenerates each edited chunk, then overwrites it.
  - X01's migration by position stays.
- **Old saves stop loading.** By the owner's rule of no save backward compatibility, `CurrentVersion` and `OldestReadableVersion` both
  become 9, the `save-v8` fixture is replaced by a `save-v9` one, and existing save files stop loading.


### 3.9 Rendering on the graphics card (B17, planned only)

The owner, 2026-11-16 (D-game-34 item 30): "yes plan the custom shaders milestone. Dont do any work but just document it." Not to be
launched until he says.

**Today.** The game draws with MonoGame's two built-in effects only (`BasicEffect`, `AlphaTestEffect`: texture times vertex colour, an
alpha test for leaves and plants, linear fog), because MonoGame's shader compiler (mgfxc) needs Wine on Linux (`VoxelRenderer`'s own
comment). So the processor does everything clever: generation and the far view's tiles on worker threads, chunk meshing on the game
thread within a budget of about 4 ms a frame, and all the light (the sun's tint by face, corner darkening, B12's darkening by depth, the
glow of torches, lanterns and lava) baked into each vertex's colour when a chunk is meshed. The card only draws ready-made triangles:
at High with the far view to 16 km a frame takes about 2.5 ms (median) and the RTX 3070 is mostly idle. Every change of light (a torch
placed, a shaft dug, the darkening's numbers) means re-meshing on the processor, and the far view (B07c) has to choose what to draw on
the processor too.

**What B17 does.** Moves the light, the fog and the water to shaders of the game's own, so the card does that work:
1. **Raw GLSL compiled at run time** (the owner's choice, D-game-34 item 40): shaders in OpenGL's own language, compiled by the driver at
   start, drawn through an MIT OpenGL binding (Silk.NET or OpenTK) on MonoGame's own context with MonoGame's state saved and restored
   round the world's passes; step 0 is a spike that proves one GLSL shader draws chunks inside MonoGame's frame (state, depth, the
   alpha test, the far pass) before anything else moves to it. macOS is out of scope (item 39).
2. **Light as data in the vertex:** each corner carries its sky light, block light and corner occlusion (not a finished colour); the
   shader combines them with uniforms (the sun's direction and colour, the sky's brightness for the hour and the weather, the glow's
   strength, the eye's sky share underground), so the day, the night, the weather and the darkening change without re-meshing; a placed
   torch re-meshes only its own light, not the colours round it.
3. **One voxel shader** for the opaque, cut-out and translucent passes: the block atlas (or a texture array where the GL version allows),
   the alpha test, the light, the fog (by distance and height, matching the far view's haze), the greyscale view.
4. **A water shader:** depth tint, a brighter edge at grazing angles, a gentle surface movement; no reflection pass.
5. **The far view** (B07c's level of detail) drawn with the same light model and fog, its textures mipped.
6. **Kept as they are:** the look at midday in clear weather (a screenshot comparison within a stated tolerance), the scripted runs
   (re-baselined, every difference explained), determinism (shaders never feed the simulation).
7. **Measured:** frame times at each preset, and processor time per frame for meshing and light changes, before and after.

**Not in B17** (shader-pack effects, which the owner set aside for the far view's work): shadow maps, screen-space ambient occlusion,
volumetric clouds and fog, bloom, reflections. Propagated sky and block light (section 6) becomes cheaper to add after B17 but is not
part of it.


### 3.10 The far view at scale (B19, planned only)

The owner, 2026-11-16 (D-game-34 item 35): asked what it takes to reach the render distances of Voxy and Distant Horizons ("the render
distance for that is crazy ... What things do we need to do to get to that level? Because that's what we want to achieve"), then: "Use
those as reference", meaning his three screenshots of Voxy in Minecraft, kept outside git in `artifacts/reference/voxy/` (an index there
says what each shows: every tree a crown of its own for kilometres, lakes and rivers with shores, villages far off, ridge after ridge of
mountains to a horizon tens of kilometres away; shader effects excluded). Not to be launched until he says.

**Where the limits are** (after B07c): distance itself is cheap, since each doubling adds one coarser level of about the same cost. What
limits it is (1) building the far data on the processor (a settled High view at 16 km took about 150 s of worker time in B07c's review),
(2) draw calls (thousands of tiles, each its own draw and effect bind), (3) the card's memory and bandwidth (fat vertices with light baked
in), and (4) precision at tens of kilometres (single-precision positions, one depth range).

**What B19 does** (after B07c, B07d and B17):
1. **A measured spike first:** the far view pushed to 32 and 64 km at High on the RTX 3070 with B07c's design; where the time goes (data,
   draw calls, the card, memory); the result decides the rest and whether step 2 below is needed. Its findings go to the owner.
2. **Step 1, within MonoGame:** tiles merged into large shared buffers per level (a handful of draws a level, not thousands), a 3D tree of
   sections for culling, occlusion culling (hardware queries, or a coarse depth raster of near hills on the processor), camera-relative
   rendering and depth split into ranges, the far levels' cost per frame bounded; built on B17's shaders (compact vertices, light on the
   card, texture arrays). Target: 32 to 64 km at High within 60 fps, the reference pictures' look without their shader effects.
3. **Step 2, only if the spike shows step 1 cannot reach the target:** rendering driven by the card as Voxy does (compute traversal,
   occlusion on the card, indirect draws). MonoGame DesktopGL has no compute shaders or indirect draws, so this needs OpenGL 4.6 beside
   MonoGame through a binding, a newer MonoGame back end, or the open engine decision (D-game-1): the owner's choice, with the spike's
   numbers.
4. **Kept:** the world unchanged; the far view's data and disk store from B07c and B07d; pictures from the reference pictures' kinds of
   viewpoint (a valley from a hill, a plain of lakes from a mountain, a forest by a river), before and after.

## 4. Milestones

Series B, on the branch `feature/biomes`. Milestone branches are `B<nn>-<name>`; the commit prefix is `B<nn>:`. Models follow the owner's
rule (2026-11-06): Haiku 5.5 for small work, Sonnet 5.5 at medium effort for medium work, Sonnet 5.5 at high effort for large or
design-heavy work, and Opus only on request. Every milestone is gated with
`GATE_TARGET=feature/biomes scripts/merge-gate.sh "B<nn>:<branch>"`.

| Id | Milestone | Size | Model | Depends on | Moves |
|---|---|---|---|---|---|
| B00 | Register the series: D-game-34 decided; a section in `implementation-plan.md` §4; rows in `docs/README.md`, `docs/game/README.md` and `docs/open-items.md`; the handover | S | Haiku | the owner accepts | nothing |
| B01 | Noise primitives (2D and 3D), climate with altitude, the tall height model, `BiomeTable`; `world.json` and `biomes.json` with JSON Schemas and validators; `NoBiomeIdsInSourceTests`; purity tests | L | Sonnet high | B00 | bundle hash |
| B02 | `tools/Vocaterra.WorldPreview`: PNG maps of biomes and heights, and vertical slices, for a seed and an area. Uses BCL `ZLibStream` and a hand-written CRC32, no package. Maps for 3 seeds. **The owner reviews the maps before tuning** | S/M | Sonnet medium | B01 | nothing |
| B02b | Terrain tuning after the owner's review of B02's maps (D-game-34 item 16): long ranges like Minecraft 1.18 (chains hundreds of blocks wide, foothills and high meadows, a spine of peaks at 200 to 300); big connected seas and continents with gentle coastal shelves, wide beaches and inland basins as lakes; snow only on the mountains; the cliffs at upland shelves, short and looping rivers and rugged islands fixed. Data first, code only where a number cannot express the shape. The same maps drawn again beside B02's | M | Sonnet high | B02, B03, B05 | bundle hash, procedural goldens |
| B02c | River drainage (item 33): rivers that only fall, a drainage network with tributaries, lakes to their outlets; pure and chunk-independent; B02b's figures kept | L | Sonnet high | B02b | maps, river profiles, in-game pictures |
| B03 | `GenerationPipeline`, the surface passes and `ProceduralChunkSource`, with solid stone down to −64. Tests: a chunk generated alone equals the same chunk generated among others; water at rest; exitable edges; time per chunk; procedural goldens | L | Sonnet high | B01 | new goldens |
| B03b | A procedural sample in the game for pictures: a test aid `--world sample` (with `--seed` and an origin) that builds a bounded box of procedural chunks above y 0, about 256 x 320 x 256, with no valley, NPCs or lessons, so the biomes can be seen and photographed in 3D before the streamed world (B07). A picture set for 3 seeds: each biome, a river, a lake, a coast and a range | S/M | Sonnet medium | B03 | pictures (owner review) |
| B04a | Sparse `VoxelGrid` in both modes, with the y range from data (negative y); coordinate APIs, residency events, `PristineDigest`, `IVoxelReader`; Core consumers keyed by coordinate; the dense-API allow-list test | L | Sonnet high | B00 | nothing (byte-identical) |
| B04b | Game consumers keyed by coordinate: renderer, version tracker, view set, rain cover, light index | M | Sonnet medium | B04a | nothing (identical pictures) |
| B05 | Saves version 9 and the changed-chunk state digest | M | Sonnet medium | B04a | state digests, fixtures |
| B06 | `WorldFactory`; the regions and `CompositeChunkSource` (copy, blend, land bias); streamed mode with generate-on-read; `ChunkStreamer`; `WorldQuery` as `IPlaceInfo`; the `WorldFacts` window. The game itself stays bounded | L | Sonnet high | B03, B05 | new tests only |
| B07 | The game streams the tall world by default; `--world valley` keeps the old bounded valley. Budgeted generation, surface-band residency and eviction, 26-neighbour mesh gating, the valley's edge kept as a wall for the body while the pass is locked (B08b's state) and lifted all the way round when it opens (owner, 2026-11-15: the player may then leave anywhere; the pass is the easy road), far plane and fog for height, a settle step before shots, sparse rain cover, the optional prefetcher. **The far view** (owner, 2026-11-15, D-game-34 item 21: folded into B07, like the Distant Horizons mod): beyond the voxel chunks, level-of-detail terrain tiles read straight from the pure height model and biome table (no chunks generated), coarser with distance, built and cached on worker threads, with skirts, water, snow, a forest tint and haze to the sky, out to **16 km at 60 fps on the High preset** on the owner's machine. **A performance gate on the VM:** the streamed preset distances are set by measurement | L | Sonnet high | B06, B08b | pictures (owner review) |
| B07b | Streamed-world fixes from the world QA pass (2026-11-16): scripted steps wait while the body is held; a far teleport or respawn makes its near zone like a start; a teleport onto water stands on the surface | S/M | Sonnet medium | B07 | runs, tests |
| B07c | Far view like Voxy (items 25, 26, 29): 3D voxel LOD sections from real chunks and the generator's model, on disk per world, textured block faces, stored light, CPU culling | L | Sonnet high | B07 | pictures (owner review), measurement |
| B07e | The far view's seams and gaps, reviewed in Release (the owner's bugs, taken in Debug; Release fixed most): fix what persists settled; a tidy state while the far view builds | M | Sonnet high | B07c | pictures from the owner's positions |
| B07f | The far store's edit layer (codebase review C1 to C3, C6, C7, C10): edits from a save in the far view, overlays per save, races fixed with tests | M | Sonnet high | B07c | tests |
| B07d | The far view's remainder (items 29, 37): settings, pre-generation, compaction, the store's memory and IO (an LRU of real sections, disk checks off the game thread, fsync on a timer, a surface-only ring, pooling) | M | Sonnet high | B07c | measurement |
| B08a | The pass through the east ring in the bounded valley: the relief lowered, the road from the village to the east edge, the `paso` site (no label) and the rockfall that closes the pass, all in `scene.json` | S/M | Sonnet medium | B00 | valley golden, bundle, runs |
| B08b | The pass's lock: the list of required lessons in `world.json` (all of today's), the unlock rule in Core, a cheat setting in the settings menu, the rockfall removed (saved like any change) when the pass opens | S/M | Sonnet medium | B01, B08a | bundle, runs |
| B08c | The pass road from the village: one continuous road of path blocks with no riser or detour (found in the game, 2026-11-16); the run comparison's speed | S | Sonnet medium | B08b, B13 | valley golden, runs |
| B09 | Widen block kinds from 64 to 128 bits: `KindMask`, the mesher's grey masks, `WorldFacts` masks, the tracker | M | Sonnet medium | B04b | nothing |
| B10 | The underground blocks: slate, granite, limestone, gravel, lava, bedrock, and iron, tin and copper ore. Code-drawn art; none gatherable, no items or nouns; lava blocks the body, glows and does no harm; bedrock is unbreakable | M | Sonnet medium | B09 | nothing expected |
| B10b | The body sinks into lava: slow sinking, moving and swimming up, climbing out at an edge; no damage in this series (owner, 2026-11-15); the ores easier to see by colour | S | Sonnet medium | B10 | nothing |
| B11 | The underground generation, configured by `underground.json` (schema, validator, `NoOreIdsInSourceTests`): strata, ores, cheese and spaghetti caves on a coarse 3D lattice, entrances, lava level, the bedrock floor. Tests: chunk-locality, no cave within reach of water, no entrance in a region or on a path, ore counts per seed within ranges, lava at rest, time per chunk. Vertical slices in the preview tool | L | Sonnet high | B03, B10 | procedural goldens |
| B11b | Cave entrances you can walk into; more rock variety in caves and on bare stone (item 27): gravel, sand, dirt and stone kinds in pockets and bands, as data | M/L | Sonnet high | B11, B12 | pictures (owner review) |
| B11c | Caves breaking the surface (the owner's bug 1): no cave opens at the surface except through an entrance | S/M | Sonnet medium | B11b | pictures |
| B12 | The underground in play: darkening by depth, lava light, a light index that follows placed torches, the near zone for underground chunks (wider when the player is underground); a scripted cave tour; a VM performance check | M/L | Sonnet high | B07, B11 | pictures (owner review) |
| B13 | The valley's underground: the strata, ore and cave passes in the valley's own stone, at least 8 blocks deep and away from sites, paths, water and the village. Re-records the valley goldens once | M | Sonnet medium | B08b, B11 | valley golden (owner review) |
| B14 | Integration: ambient sound by biome and underground from `IPlaceInfo`; water-at-rest and exitable-edge checks over 3 seeds; signed coordinates in scripted runs; the cloud layer's height as data; a scripted tour through the pass into each biome and down a cave; documents (`architecture.md`, `saves.md`, `running.md`, and "Adding a biome" and "Adding an ore" in `content/README.md`) and findings | M | Sonnet medium | B12, B13 | new runs |
| B15 | Towns: `IGroundShape`, a `ProceduralReliefRegion` at an offset, and one tiny sample town with no content words | L | Sonnet high | B06 | none: the valley golden must hold |
| B16 | Regions that can grow, shrink and multiply (owner, 2026-11-15; last, after everything else): (1) regions declared in `world.json` (id, layout file, position; the size from the layout; no region's size or position in code), with a validator that refuses overlapping regions or margins and a test region, only in test data, at another size and position that loads, blends and validates with no code change; (2) a region's relief may mark cells "generated ground", so only the terrain wanted by hand is drawn; (3) rivers, lakes and waterfalls as lists in a layout; (4) the valley's trees placed per chunk, so a resize does not reshuffle them. The aim: doubling the valley is a change of data (its size, the ring, the hand-drawn terrain, the sites), not of code | L | Sonnet high | B14, B15 | valley golden once (item 4) |
| B17 | Rendering on the graphics card (section 3.9; D-game-34 item 30; planned only, not to be launched until the owner says): a shader build that works on Linux (spike first), light as data in the vertex (sky, block, occlusion) combined in a voxel shader with the hour, weather and glow as uniforms (no re-mesh for a light change), fog by distance and height, a water shader, the far view on the same light model; the midday look kept within a tolerance | L | Sonnet high | B07c, B12 | pictures (owner review), measurement |
| B17a | B17's step 0: a GLSL spike drawing chunks on MonoGame's context through an OpenGL binding, state saved and restored; a report on the fit | S/M | Sonnet high | B07c | picture, report |
| B18 | Grass density (item 34): render-only decorative grass on grass tops, a settings slider, a denser default; the world unchanged | M | Sonnet medium | B07, B12 | pictures (owner review), measurement |
| B19 | The far view at scale (section 3.10; D-game-34 item 35; planned only, not to be launched until the owner says): a measured spike at 32 and 64 km first; then merged buffers per level, a 3D section tree for culling, occlusion culling, camera-relative rendering and depth ranges on B17's shaders; rendering driven by the card only if the spike shows it is needed (an engine-level choice for the owner); the owner's Voxy pictures (`artifacts/reference/voxy/`) as the target look | L | Sonnet high | B07c, B07d, B17 | pictures (owner review), measurement |
| B20 | No snow in the valley (item 36): the ring's tops without snow, as the slope would carry; the valley golden re-recorded once | S/M | Sonnet medium | B13 | valley golden, pictures |
| B21 | Meshing off the game thread, dirty-only rechecks, no per-frame allocation (review P1, P2, P5) | M/L | Sonnet high | B07e | measurement |
| B22 | Fidelity bundle: MSAA, a moving sun, biome colours, clouds, water (review G2 to G6) | S/M | Sonnet medium | B07c | pictures (owner review) |
| B23 | Sky and block light propagation with smooth light (review G1) | L | Sonnet high | B21 | pictures (owner review), measurement |
| B24 | Guards and cleanups (review A1, A2, C8, C9, T1, T2, docs) | S/M | Sonnet medium | B07f, B21 | tests |

**Order** (owner, 2026-11-15: "as much in parallel as we can", up to three building agents at once; later the same day "only have 1
agent max", so one at a time; that evening "you can have up to 4 agents now", D-game-34 item 19): each milestone starts as soon as
its dependencies are gated, with the critical path first (B04a, B05, B06, B07, B12, B14; and B01, B03, B06). At an owner-review stop only
the work that depends on the review waits. The first waves:
1. B01, B04a and B08a
2. as they merge: B03 and B02 (after B01), B04b and B05 (after B04a), B08b (after B01 and B08a), B03b (after B03), B02b (after B02's review)
3. then B06 (B03, B05), B09 (B04b), B10 (B09), B11 (B03, B10), B07 (B06, B08b), B15 (B06), B12 (B07, B11), B13 (B08b, B11), B14
4. last: B16 (regions that can grow, shrink and multiply), after B14 and B15

The game stays playable at every gate. It switches to the streamed world at B07 and gains its underground at B11 and B12.

**Verification across the series:**
- **Every gate** runs `scripts/merge-gate.sh`: build with `-warnaserror`, the full test suite, format, content validation and lint, and the
  licence scan.
- **Scripted runs:**
  - B04a, B04b and B06 compare exactly.
  - B05 compares with masked lines.
  - B07, B08, B12 and B13 are re-baselined with the owner.
- **Owner reviews:** B02's maps and slices are reviewed by the owner.
- **Pictures for the owner** (owner, 2026-11-15: "screenshots at important parts so that I can review the biomes and how they look and
  what's being generated"). Every milestone that changes what is generated or drawn leaves pictures in
  `/home/james/Dev/Vocaterra/artifacts/b-series/<id>/` (outside git) and lists them in its report; the orchestrator shows them to the
  owner at the milestone's record:
  - B02: maps of biomes and heights, and vertical slices, for 3 seeds;
  - B03: maps of the generated surface blocks, from the preview tool once B02 is in;
  - B03b: the 3D picture set of each biome, river, lake, coast and range, for 3 seeds;
  - B06: the region's border and its blend, from the sample aid run round the valley's edge;
  - B07: the streamed world from the pass and across the presets, beside the performance figures;
  - B08a and B08b: the pass, the road, the rockfall, and the open pass;
  - B10: each new block in the light of day;
  - B11: vertical slices of the underground, and cave mouths and caverns in the sample aid;
  - B12: the cave tour; B13: the valley's caves and ores; B14: the tour through each biome and down a cave; B15: the sample town.
- **Measurement:** B07 and B12 measure, at each preset on the VM:
  - frame medians;
  - meshing and generation time per chunk;
  - resident chunks;
  - memory.
- **The end:** B14 ends with the screenshot tour and the water and cave checks over 3 seeds.

## 5. The owner's decisions

**Answers given on 2026-11-15:**
- **How far the procedural world goes:** endless and streamed. Chunks are generated round the player, far chunks are unloaded, and saves
  keep only edited chunks. The alternative, a larger fixed world, was declined.
- **The first biome slice:** all from existing blocks.
  - Plains and forests (oak and birch, spruce taiga).
  - Mountains (stone, mossy rock, snowy peaks, terraced cliffs).
  - Rivers and lakes (at the water level, with sand banks).
  - Beaches and sea.
- **The valley's ring:** open a pass through it, with a road out, so leaving the valley is a deliberate journey.
- **The code:** modular and SOLID, ready for other systems, a temperature system first.
- **Underground:** caves and lava like Minecraft's, kept simple, with a few kinds of stone. Ores: iron, tin and copper for now.
- **Height:** a tall world for large mountains, like Minecraft 1.18's: −64 to 320 here.

**D-game-34** (decided 2026-11-15, as drafted here; the register in `docs/game/decisions.md` holds it with the answers below): *A tall procedural world round hand-crafted regions (the B
series, `docs/game/biomes-plan.md`).*
- **The world:** endless and streamed, y −64 to 320. It is procedurally generated outside authored regions: biomes, great mountain ranges,
  rivers, lakes and sea above ground; rock layers, caves, lava, and iron, tin and copper ores below.
- **The valley:** the first region, unchanged inside until B13, opened by a pass to the east.
- **What it amends:**
  - the prototype brief's LEAVE FOR LATER line on procedural terrain, biomes and a large open world;
  - D-game-22's inward-only growth;
  - the adaptation design's decision 15 ("inside the valley for now").
- **What it takes over:** the settlement plan's phase 7.

**The answers at B00** (owner, 2026-11-15; recorded in D-game-34):
1. **When the new land opens:** "Its unlocked after completeing currently all the lessons. However a cheat mode in the settings should
   bypass this." Until then a rockfall closes the pass and the valley's edge stays a wall (the owner chose this from two options; the
   other ran the bounded valley until the unlock). Built in B08 (section 3.6).
2. **Lessons:** none in this series uses the new land or the ores.
3. **Digging and tools:** "Nothing can be gathered by hand yet, this is just the world generation for it." No new block is gatherable,
   and the ores have no items (section 3.5).
4. **Lava's harm:** none; lava blocks the body.
5. **The ore items' Spanish nouns:** not needed in this series, since the ores have no items. None of «hierro», «cobre» or «estaño» is on
   `cefr/a1.txt` or `a2.txt` today; when ore items come, each joins the world-words block of `cefr/a1.txt` with its reason, as
   `content/README.md` says, or the owner decides otherwise then.

## 6. What this plan does not do

- **Blocks and biomes.** New biome blocks (desert, swamp, tundra, ice), cave biomes, larger ore veins, and ores beyond iron, tin and copper.
- **Fluids.** Underground water (aquifers), lava that flows, procedural waterfalls, rivers above sea level, and currents outside the
  valley.
- **Underground features.** Structures such as mineshafts and abandoned camps.
- **Gathering and crafting.** Gathering rock, gravel or ores; ore items and their nouns; smelting, tools and digging (section 5,
  answer 3).
- **Rendering.** Propagated sky and block light, and camera-relative
  rendering. (Custom shaders are planned as B17, section 3.9, not launched.)
- **Language.** Biome words, place names, or the guide's knowledge of procedural land.
  - In new land, «¿dónde está el río?» can point at the sea, because "río" names water blocks (`nouns.json`).
  - There are no labels like «bosque», «montaña» or «cueva».
  - `IPlaceInfo` makes these possible later. They will go through the content gate and the A2 ceiling.
- **Temperature.** The temperature system itself; section 3.3 only shows how it plugs in.
- **Towns.** New towns' content: B15 builds the mechanism with a tiny sample only.

## 7. Risks

1. **Performance on the software-GL VM.** The view at High covers about 1,960 columns, 3.4 times the valley, and the world is three times
   taller.
   - Mitigation: the surface band and the near zone (section 3.7), the existing skipping of buried chunks, and B07's and B12's measurement
     gates.
   - The streamed preset distances are set by measurement. Merged meshes per column come later if needed.
2. **Memory.** The surface band keeps about 3 to 5 chunks per distant column, so about 50 to 80 MB at High. Add the near zone, the region
   template (about 23 MB without its empty chunks) and the meshes.
   - Mitigation: tests stay on the bounded valley or small windows, under the 3 GB heap cap of `Vocaterra.Game.Core.Tests`.
   - Palette storage for chunks (4 bits a block) is the next step if B07's measurements need it.
3. **Generation time.** 3D cave noise is the heaviest pass.
   - Mitigation: the coarse 4×8×4 lattice, the surface band (most deep chunks are never generated far away), and a time-per-chunk test
     beside `SceneBuildTimeTests`.
4. **The block-id ceiling.** With the settlement series' blocks (ids up to 56, on `main`) and this series' 9, ids pass 64.
   - Mitigation: B09 comes before B10.
5. **Conflicts with the settlement series.** Both edit `scene.json`.
   - Mitigation: that series merged into `main` (PR #65) before B00, so B08 and B13 start from its `scene.json`. Its runtime place lookup
     should use `IPlaceInfo` once B06 lands.
6. **Residency leaking into determinism.**
   - Mitigation: the dense-API allow-list test, plus a test that runs the same script under two prefetch and eviction policies and expects
     identical state digests and saves.
7. **Lazy caches racing on worker threads.**
   - Mitigation: the region template is frozen into immutable arrays, and workers never touch `VoxelGrid`.
8. **Water meeting caves.** A cave that met a river or the sea would flood through the flow simulation.
   - Mitigation: the no-cave margin round water and under water beds, tested over sampled areas and 3 seeds.
9. **Seams at the region border** (surface blocks, tree line, the jump from the 110-block ring to far higher peaks).
   - Mitigation: the blend margin and the land bias, reviewed in B02's maps and B14's screenshots.
10. **Churn of the content hash.** Re-hashes happen at B01, B08, B10, B11 and during tuning.
    - Mitigation: the state digest hashes only the world-generation data.
11. **Integer overflow and range.** Fixed-point positions hold to 524,288 blocks. Negative y is new to physics, rays, flow and the walk
    planner.
    - Mitigation: tests near ±29,000 in x and z, and at y −60 and 310.

## 8. Progress

| Id | Status | Gate | Notes |
|---|---|---|---|
| (proposal) | written 2026-11-15; caves, ores and the tall world added the same day | none (documents only) | for the owner's review |
| (refresh) | 2026-11-15, after the fast-forward to `main` at `0db529a1` | none (documents only) | saves version 9 (not 8), block ids 0 to 56, B08 no longer waits for `feature/settlement`, line references checked; committed with D-game-34 entered as proposed |
| (accepted) | 2026-11-15: D-game-34 decided, the five answers recorded (section 5); B08 grows to M (the closed pass), B10 loses gathering | none (documents only) | B00 next |
| B00 | done 2026-11-15 | `162dec4f`: 11,498 passed, 3 skipped; the doc-link test failed on two links in the orchestrator's own brief, fixed in the next commit and re-run | section 4.14 of the game plan; open items and index rows |
| (parallel) | 2026-11-15: the owner allows three building agents on the B series and holds only dependent work at a review stop; B08 split into B08a (the pass, now) and B08b (its lock, after B01); the edge wall moves to B07 | none (documents only) | B08a launched beside B01 and B04a |
| (pictures) | 2026-11-15: the owner asks for screenshots at the important points; B03b added (a procedural sample in the game for 3D pictures before B07); a picture list per milestone (section 4, "Pictures for the owner") | none (documents only) | every brief from now on carries it |
| (regions) | 2026-11-15: the owner asks that the starting area can grow or shrink and more hand-crafted areas be added with little change; B16 added at the end of the series ("It's not important at the moment") | none (documents only) | after B14 and B15 |
| B01 | done 2026-11-15 | `94bea060`: 11,597 passed, 3 skipped | noise, climate with altitude, the height model, the biome table (14 rows: `woodland` the catch-all for the plan's "forest", which clashes with a valley pass name; `lake-basin` added), `world.json` and `biomes.json`; report `.claude/briefs/b01-report.md` |
| B04a | done 2026-11-15 | `e6f4755e`: 11,650 passed, 3 skipped | the sparse `VoxelGrid` (bounded and streamed, `WorldLimits`, `IChunkGenerator`, residency, `PristineDigest`, `IVoxelReader`); byte-identical; report `.claude/briefs/b04a-report.md` |
| B03 | done 2026-11-15 | `3b0d7ef6`: 11,732 passed, 3 skipped | `GenerationPipeline` (shapers `gen/relief`, `gen/banks`, `gen/waterside`; decorators `gen/surface`, `gen/water`, `gen/stone`, `gen/trees`, `gen/undergrowth`), `StrategyRegistry`, `ProceduralChunkSource`, `World/Streaming` (`IChunkSource`, `ChunkData`); report `.claude/briefs/b03-report.md` |
| B05 | done 2026-11-15 | `707864aa`: 11,748 passed, 3 skipped | saves version 9 (`ChunkEdit` by chunk coordinates, no pristine copy), the changed-chunk state digest with its header; report `.claude/briefs/b05-report.md` |
| (B02 review) | 2026-11-15: the owner reviewed B02's maps: long ranges, big seas and continents, snow only up high (D-game-34 item 16); B02b added | none (documents only) | B02b after B02's gate |
| (stopped) | 2026-11-15, about 17:27 UTC: the owner, playing games, asked to scale down, then to keep one agent at most, then stopped the work | none | B03b, B04b's merge and B06 stopped; B02's gate stopped before its tests (feature/biomes reset to `3aae9dce`); B02, B04b and B08a reported and waiting to be gated; the handover lists the next steps |
| (resumed) | 2026-11-15, about 18:37 UTC: the owner said "continue"; one building agent at a time, every heavy command at one slot (`VOCATERRA_HEAVY_SLOTS=1`) while he uses the machine | none | B02's gate re-run; B04b's and B08a's merges given to one agent |
| B02 | done 2026-11-15 | `0a389e04`: 11,769 passed, 3 skipped | `tools/Vocaterra.WorldPreview` (biome and height maps, slices, statistics); the maps the owner reviewed (D-game-34 item 16); report `.claude/briefs/b02-report.md` |
| B04b | done 2026-11-15 | `579491b7`: 11,779 passed, 3 skipped | the renderer, version tracker, view set, rain cover and light index keyed by chunk coordinate (identical pictures in its scripted runs); report `.claude/briefs/b04b-report.md` |
| (pictures later) | 2026-11-15: the owner: game windows for pictures only after he finishes playing; B06 goes before B03b's pictures | none | B03b's pictures and B04b's look in the game wait |
| B08a | done 2026-11-15 | `e6eb6e3f`: 11,785 passed, 3 skipped | the pass in the bounded valley: a graded cutting from the village to the east edge (383, 208, y 58), the `paso` sites, the rockfall, the stamp's `floorY`; report `.claude/briefs/b08a-report.md`, merge `.claude/briefs/b08a-merge-report.md` |
| B06 | done 2026-11-15 | `f5a8dbbe`: 11,879 passed, 3 skipped | regions (`IRegion`, `RegionCatalog`, `AuthoredReliefRegion`), the blend (`regionBlend` in world.json: margin 64, edge smoothing, trees thinned, land bias to 512 blocks, the exit corridor from the pass), `CompositeChunkSource`, `WorldFactory` (bounded and streamed), `ChunkStreamer`, `Scene.Places`, the `WorldFacts` window; the dense-member allow-list empty; report `.claude/briefs/b06-report.md` |
| (rivers) | 2026-11-15: the owner, on B06's pictures (rivers stop at the blend margin): rivers near a region start in the hills, as headwaters (D-game-34 item 18) | none | added to B02b |
| (four agents) | 2026-11-15, about 21:45 UTC: the owner: "you can have up to 4 agents now" (D-game-34 item 19) | none | B08b, B09 and B15 launched beside B02b |
| (B02b round 1) | 2026-11-15, about 21:58 UTC: B02b's round 1 (connected seas, coastal shelf, snow above 120, rivers reaching water, headwaters; but 60 to 65% sea in islands, snow plateaus, a coastal dome round the valley); its gate stopped before merging; the owner: redo it, sea 35 to 45% (D-game-34 item 20) | none | B02b round 2 running; report `.claude/briefs/b02b-report.md`, redo `.claude/briefs/b02b-redo.md` |
| (far view) | 2026-11-15, about 22:02 UTC: the owner: the far view (level-of-detail terrain to 16 km at 60 fps on High) folded into B07 (D-game-34 item 21) | none | B07's brief carries it |
| B09 | done 2026-11-15 | `598593b2`: 11,885 passed, 3 skipped | the 128-bit kind mask (`UInt128`, with a two-`ulong` accumulator in the mesher's and the chunk's 4,096-block loops), a width guard, tests above id 63; meshing time unchanged; report `.claude/briefs/b09-report.md` |
| B08b | done 2026-11-15 | `8a589270`: 11,901 passed, 3 skipped | the pass's lock: `pass` in world.json (all 12 lessons), `PassState`, the cheat setting under "Cheats", `--pass open`, the rockfall removed and saved, `PassOpen` in the save; its first gate met the known «porque» core fuzz bug (seed `7PcP-w7xauk1`), the re-run passed; report `.claude/briefs/b08b-report.md` |
| B10 | done 2026-11-15 | `9101f1b8`: 11,961 passed, 3 skipped | nine blocks (slate 57 to copper ore 65), drawn in code; lava stops the body and glows, bedrock indestructible; nothing places them yet (B11); report `.claude/briefs/b10-report.md`, tiles in `artifacts/b-series/b10/` |
| B15 | done 2026-11-15 | `b97f7f39`: 11,980 passed, 3 skipped | towns: `IGroundShape` (hand-drawn or procedural ground), `ProceduralReliefRegion` placed by `RegionPlacement`, `WorldFactory` towns, a sample town in test data, the preview tool's `--region`; report `.claude/briefs/b15-report.md`, maps in `artifacts/b-series/b15/` |
| M96 (core) | done 2026-11-16 | `fbe763f9`: 11,988 passed | «porque» after a determiner of the other number stays the conjunction; the random fuzz failures on it end (core plan §5) |
| B10b | done 2026-11-16 | `1d1adc46`: 11,993 passed, 3 skipped | the body sinks into lava (sink 0.5, swim 0.25 of walk, swim up 1.2; no harm), an orange view in lava, the ores in strong clusters (D-game-34 items 22 and 23); report `.claude/briefs/b10b-report.md`, tiles in `artifacts/b-series/b10b/` |
| B11 | done 2026-11-16 | `9df519e5`: 12,087 passed, 3 skipped | the underground: `underground.json`; strata, bedrock floor, ores, cheese and spaghetti caves on the lattice, lava at -54, entrances on dry land outside regions; water rule (3 blocks, 16 under a bed); the procedural underground under the valley and towns; generator version 2; report `.claude/briefs/b11-report.md`, slices in `artifacts/b-series/b11/` |
| (B02b round 2) | 2026-11-16, about 01:30 UTC: round 2 (sea 38 to 42% in one ocean, continents 8,000 to 12,000, ranges inland round the valley, clean beaches; rivers mostly dry, massifs, polygon coasts); the owner: another round first (D-game-34 item 24) | none | round 3 running; report `.claude/briefs/b02b-round2-report.md`, brief `.claude/briefs/b02b-round3.md` |
| B13 | done 2026-11-16 | `fc27512f`: 12,100 passed, 3 skipped | the valley's underground: B11's passes in the valley's buried stone, 8 from air, 16 from water, margins round sites, paths, props, the village and the pass (a `valley` section in underground.json); the caves meet the procedural ones at y 0; `GoldenDigest` b189ee0a to 83af36d3; in-game screenshots of the village and the peaks byte-identical before and after; report `.claude/briefs/b13-report.md`, slices in `artifacts/b-series/b13/` (owner review) |
| B08c | done 2026-11-16 | `bfcf9b0f`: 12,103 passed, 3 skipped | the pass road from the village: a path not ending at a site keeps its height across a site's blend; the gate at (318, 217), the road 5 wide, a block up every second column; walk from the village 6 turns (was 41); compare-runs.py's mask bounded (4 s); `GoldenDigest` to efcfbe02; report `.claude/briefs/b08c-report.md`, pictures in `artifacts/b-series/b08c/` |
| B02b | done 2026-11-16 | `c2785332`: 12,167 passed, 3 skipped | round 3 (D-game-34 item 24): a water level per column (smooth ground less the hills and a clearance, in steps of 3 above the sea; falls built as falling water, every other block a source, at rest when woken); rivers hold water on 99.6 to 99.9% of their line, lakes 1.5 to 3.2%; chains of one width (`chainBlocks`), spines about 240 thick; coasts warped twice more with a distance noise (straight runs 51% to 36%); ring walk 192 of 192; maps in `artifacts/b-series/b02b/` beside rounds 1 and 2. Open: rivers are noise lines with no direction, so 6.5 to 9.8% of river water stands in humps (largest rise 48 to 84 blocks), for the owner; test bounds loosened for thinner chains (land above 150/200/250, puddles up to 8 per 8k square, shores, level tiles above 220, river reach 85%) | report `.claude/briefs/b02b-round3-report.md` |
| B07 | done 2026-11-16 | `7fd0b8fd`: 12,267 passed, 3 skipped | the streamed world by default (band streaming, a job pool, residency and eviction, the valley's wall kept while the pass is locked, the body held until the world round it is resident) and the far view (level-of-detail tiles to 4, 8 and 16 km, D-game-34 item 21), lit like the chunks, haze from 6% of the range, water from B02b's per-column levels. Release at High with 16 km on the RTX 3070: p50 2.5 ms, p95 7.8, p99 9.6, none over 100 ms outside a start (one 630 ms frame, the synchronous near zone); `--world valley` runs identical; pictures in `artifacts/b-series/b07/`. Open: rivers narrower than a far tile do not show beyond the near ring; scripted `--walk-to` over ungenerated land plans for up to 3 s | reports `.claude/briefs/b07-*-report.md` |
| B12 | done 2026-11-16 | `54cecc7b`: 12,293 passed, 3 skipped | the underground in play: darkening by depth from the pure height model (6-block fall to 0.28 of the sun, smooth per corner; open faces unchanged, the valley above ground identical), torches, lanterns, post torches and lava as glowing kinds (a placed torch lights a cave at once, about 8 blocks), a surface edit re-meshes the chunks beneath, the cave tour `r50-cave-tour`, `--teleport-to`; Release p95 about 10 ms underground; pictures in `artifacts/b-series/b12/`. Open: entrances are pits with no walkable way down (B11b); far from light a cave is nearly black at 0.28 (the owner to judge) | report `.claude/briefs/b12-report.md` |
| B07b | done 2026-11-16 | `15eb6a1c`: 12,305 passed, 3 skipped | the world QA pass's bugs: steps after a teleport wait for the world; a far teleport, respawn or load makes its near zone at once (one frame of 0.8 to 1 s, near view complete in about 1 s on a quiet machine, instead of about 3 s of sky); a teleport onto water stands at the surface; the start's first frame is longer (1.0 to 1.6 s) | report `.claude/briefs/b07b-report.md` |
| B07c | done 2026-11-16 | `23c5795f`: 12,370 passed, 3 skipped | the far view like Voxy: 3D voxel sections at 8 levels from the generator's model and real chunks (edits kept), on disk per world, textured near levels, stored light, water and snow and peaks kept at every level, the horizon to 16 km at High (an 8 MB upload budget, coarse first); an Opus review found the upload cap, water and surface rules, silhouettes, the ring, races, edits, mips and corruption, all fixed but region compaction; p95 about 3 to 4 ms above B07 at load 7 to 11 (to re-measure quietly) | reports `.claude/briefs/b07c-report.md`, `b07c-review.md` |
| B11b | done 2026-11-16 | `80382d33`: 12,399 passed, 3 skipped | natural cave entrances (winding, varied width and height, noise-carved walls, ragged mouths, walkable to a cave 20 or more down) and rock variety (plain stone on cave faces from about 90% to about 50%; gravel, sand, dirt, granite, limestone, basalt; cliff bands and scree), generator version 3; the cave tour through a natural entrance | report `.claude/briefs/b11b-report.md` |
| B18 | done 2026-11-16 | `72c3cd53`: 12,448 passed, 3 skipped | grass density: a render-only tuft layer by biome on grass tops, a Grass slider (default 60), no measurable frame cost | report `.claude/briefs/b18-report.md` |
| B07e | done 2026-11-17 | `84e1e2cb`: 12,451 passed, 3 skipped (two failures not B07e's: the valley timing test, passing alone in 8 s, and a new core fuzz seed) | the far view's seams and gaps: none of the owner's four bugs shows settled in Release; no overlapping levels (a stand-in until all its leaves are on the card), drawing decided by what is on the card, neighbour-aware tile edges and skirts, far tiles over undrawn near columns, tile choice by range over cos(half FOV), the far near plane a seventh of the view; uploads ran only when something was drawn (fixed); the game-window test 2.5 min (was 20) | report `.claude/briefs/b07e-report.md` |
| B20 | done 2026-11-17 | `9d2f2139`: 12,453 passed, 3 skipped (one failure, the valley timing test, passing alone) | no snow in the valley: the snow rule and the `snowLine` field gone (schema, parser, data); former snow tops are bare stone or what the slope carries; a test that the valley lays no snow; `SceneTests.GoldenDigest` and the bundle hash moved, the state digest did not | report `.claude/briefs/b20-report.md`, pictures `artifacts/b-series/b20/` |
| B17a | done 2026-11-17 | `1fe71da5`: 12,461 passed, 5 skipped (two live-GL tests skip without `VOCATERRA_GL_TESTS=1`; one failure, the valley timing test, passing alone) | a GLSL spike: Silk.NET.OpenGL 2.22.0 (MIT) on MonoGame's own context (OpenGL 4.6 compatibility on the RTX 3070, compute and indirect draws present); behind `--gl-shaders` (off) the opaque chunk pass draws through one GLSL program, bit-identical to `AlphaTestEffect` in a meadow and a lit cave, its submit time 24 to 44% lower; GL state read and restored round the pass; recommendation go (B17's rest L) | report `docs/game/b17-spike.md`, `.claude/briefs/b17a-report.md` |
| B22 | done 2026-11-17 | `19d21747`: 12,483 passed, 0 failed, 5 skipped | a fidelity bundle on the built-in effects: 4x MSAA (`--msaa 0|2|4|8`, no measurable cost on the RTX 3070); a dawn band and each band's own sun direction, a deeper dusk; grass, tufts and oak leaves tinted from a 5 x 5 climate map (`land-colours.json`, no biome ids), the far view from the same map; darker, smaller clouds; water tinted by depth | pictures `artifacts/b-series/b22/` (outside git), `.claude/briefs/b22-report.md` |
| B24a | done 2026-11-17 | `ef932990`: 12,485 passed, 0 failed, 5 skipped | the valley underground's `Lay` on at most two threads (was up to eight; the same blocks at one and two); its timing test on the laying's CPU time, fastest of three, budget 5,000 ms against 2,500 ms measured, run alone in the timing collection (the wall-clock test had failed four full runs under load) | `.claude/briefs/b24a-report.md` |
| B21 | done 2026-11-17 | `28d0478c`: 12,514 passed, 0 failed, 5 skipped | meshing on the job pool from a snapshot taken on the game thread, uploads within the frame budget, stale jobs refused by a stamp per chunk, an edit within 40 blocks meshed in its own frame; dirty marking from block changes; 0 bytes a steady frame in residency and remesh; jump and preset changes safe; `HighestBlock` installs nothing; near fill after a jump about 2x faster, frames while the far view fills 4 to 5 ms (were 12 to 14); pictures pixel-identical | `artifacts/b-series/b21/` (outside git), `.claude/briefs/b21-report.md` |
| B07f | done 2026-11-17 | `2f995552`: 12,531 passed, 0 failed, 5 skipped | the far store's edit layer and the review's far-view races (C1 to C3, C6, C7, C10, with race tests): placed and broken blocks climb the levels, so a 4x4 tower 50 high shows as a post from 1 km and 1.3 km and is absent with an empty save; `--shot` waits for the far tiles on the card (an early shot had looked like a void); a near-zone edit no longer leaves a far tile dirty forever (a scripted settle hang); `LodEdits` allocates nothing per frame | `artifacts/b-series/b07f/` (outside git), `.claude/briefs/b07f-report.md` |
| B17b | done 2026-11-17 | `860a4a28`: 12,539 passed, 0 failed, 8 skipped | every chunk pass (faces, lit pieces, cut-outs, grass, glow, water) on the game's GLSL, on by default with a "Shaders" row in the settings' Graphics group and the effects as fallback (below GL 3.3 or a compile failure); one GL state scope a frame; under water through a flip uniform; a card timer in `--stream-stats`; seven of eight views pixel-identical to the effects (the shore 3 pixels by 1/255); processor submit time a quarter to a tenth, the card's equal or lower | `artifacts/b-series/b17b/` (outside git), `docs/game/b17-spike.md` (B17b section), `.claude/briefs/b17b-report.md` |
| B02c | done 2026-11-17 | `4445e907`: 12,565 passed, 1 failed, 8 skipped (the failure `SceneBuildTimeTests`, 3,026 ms against a 3,000 ms wall-clock budget at load 15, passing alone; given to B24) | rivers that only fall: a drainage network (a priority flood over 32-block nodes, each landmass read whole on first touch, 0.25 to 0.9 s) carves rivers and lakes; generator version 4; 9 of 18 goldens and the fixture's digest re-recorded; the far sampler and the entrances pass the river carve; the game loop never waits for a landmass (a dry answer until the streaming workers read it) and allocates nothing per frame; start to first frame +0.4 s, a settled 16 km far view about 20% slower; lakes still come out as steps of levels (B02d) | pictures `artifacts/b-series/b02c/` (outside git), `.claude/briefs/b02c-report.md` |
| B24 | done 2026-11-17 | `536e0e6d`: 12,568 passed, 0 failed, 8 skipped | guards and cleanups: the purity test over every folder that feeds the world (`World/Drainage` included, its one `lock` allowed by name), Game.Core banned from threads, tasks, timers, file IO and environment reads with an allow-list; dead code removed (`ChunkStreamer`, `VoxelGrid.ResidentCoordinates`); the resident cap's bug fixed (after a walk or jump the old place held the cap and the new place loaded nothing); a persistence read-during-write test; timing tests on CPU time with budgets twice the measured cost, so they hold under load; `SettleGuard` for hang guards | `.claude/briefs/b24-report.md` |
| B26 | done 2026-11-17 | `a1371449`: 12,568 passed, 0 failed, 8 skipped | the game's shipped projects (Vocaterra.Game, Vocaterra.Host, SpeechRender) in the gate's licence scan; Zlib allowed; overrides with evidence (NVorbis, PlatformAbstractions MIT; SDL Zlib); OpenAL Soft's LGPL native library a one-package exception (`scripts/license-exceptions.txt`, the owner's item 49) | `.claude/briefs/b26-report.md` |
| B07d | done 2026-11-17 | `93a74756`: 12,643 passed, 0 failed, 8 skipped | the far view's remainder: a "Worker threads" setting (Auto = cores minus 4 in 1..8, a launch option; `JobPool.MaxWorkers` 8, resized while running; scripted runs and tests keep 2); far-view distance, detail, trees, textures and real-land rows in the Graphics group; `--pregen` (4 km in 340 s, resumable); a 256 MB LRU of real sections reloaded from disk; the ring planned on a worker; fsync on a timer; region compaction; pooled arrays; 16 km fill after a teleport 272 s at 2 workers, 147 at 4, 110 at 8 (load 15 to 25) | `artifacts/b-series/b07d/` (outside git), `.claude/briefs/b07d-report.md` |
| B14 | done 2026-11-17 | `50d919a1`: 12,714 passed, 1 failed, 8 skipped (the core's wall-clock lexicon load test, passing alone in 1 s; M100) | integration: ambient sound by place (an `ambient` field per biome row, `PlaceAmbience` from `IPlaceInfo`, a `cave` bed underground, cross-fades; the state digest ignores `ambient`); water at rest and exitable edges over seeds 42, 7 and 2026 (21 windows, all passing on B02c's rivers); signed coordinates in scripted runs; `cloudAltitude` as data; the 21-stop `r51-biome-tour` (every biome, a cave with a lit torch, the far land); "Adding a biome" and "Adding an ore" | `artifacts/b-series/b14/` (outside git), `.claude/briefs/b14-report.md` |
| B27 | done 2026-11-17 | `accc36b1`: 12,713 passed, 2 failed, 8 skipped (the core's wall-clock lexicon test, M100; the underground cost ratio, 8.13 against a bound of 6 at load 18, passing alone: its base got cheaper) | the drainage network's cost back to B02c's base, byte-identical: each node's nearby river reaches cached in its tile, so a column no longer scans 81 nodes; a surface chunk 13.9 to 6.7 ms of CPU, the valley snapshot 5.8 to 2.9 s; B24's budgets halved | `.claude/briefs/b27-report.md` |
| B28 | done 2026-11-17 | `88521d07` (gated with M100): 12,715 passed, 0 failed, 8 skipped | test speed: the longest classes split so xUnit runs them in parallel (StreamedWorldHostTests into nine, FarViewTests into six, seven more into two to four), Game.Core.Tests on 6 threads (peak 5.5 GB), NamePlates' two game processes side by side, about 35 slow classes marked Slow (the fast tier 3 minutes); the gate's test phase 26 minutes (Game.Core.Tests 26, Game.Tests 18), from about 50 earlier on 2026-11-17 with B27's cheaper generation | `.claude/briefs/b28-report.md`, docs/process/test-performance-plan.md (T11) |
