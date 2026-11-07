# Water: the H series

A plan written on 2026-11-07 at the owner's request, from a read of `main` at `4c93aa6c` and of `feature/bread-pilot` at `7d36d6df`.
Nothing was built or run. Its goal is **water that behaves like water**: it flows and settles, it has currents, the player can swim in it,
a bubble meter runs down under the surface, and the view under the surface is tinted, fogged and softly blurred.

**Status:** H01 done (2026-11-07); H02 and H05 running (chosen first because they're the ones the owner sees), then H03 and H04. The owner's four answers are in section 6, progress in section 8. Branch `feature/water`, cut from `main` at `4c93aa6c` (worktree `.claude/worktrees/water`). Commit prefix
`H<nn>:`. As with every feature branch, nothing is pushed to `main`; the owner merges by pull request.

## 1. The request

> Plan out adding flowing water to the game, currently we have water blocks but it's not realistic at all. Going underwater, we should
> have a bubble meter that runs out, like Minecraft, and also being underwater should have the underwater blur effect (not too strong).
> [...] We should also add a basic swimming mechanic for the player, like in Minecraft.
>
> Don't change the structure of the map, keep the lake, waterfall, etc. We just want the water mechanic systems added. Also keep in mind
> that this needs to work if the map is expanded and new water sources, lakes, rivers, etc. are added.

Together with the owner's standing rule: the code follows `docs/process/code-guidelines.md` (single responsibility, components,
composition, the command and observer patterns, logic in `Vocaterra.Game.Core` and drawing in `Vocaterra.Game`, Given_When_Then tests in
folders that mirror the source).

So the plan has three constraints:

1. **The map stays as it is.** No change to `scene.json`, `TerrainGenerator`, `SceneBuilder` or the layout specs. Every water block the
   scene builds today is still there, in the same place, after the H series.
2. **Nothing is tied to this map.** No coordinate, no named site, no special case for "the lake" or "the waterfall". Every system reads
   the grid (a cell is `BlockId.Water` or it isn't) and, for river currents, the generic `RiverSpec` list. A new lake, river or spring
   added to the layout gets flow, currents, swimming, breath and the underwater view with no code change.
3. **It merges easily with `feature/bread-pilot`** (section 3).

## 2. What exists today (on `main`)

| Area | Today | Where |
|---|---|---|
| Block | One kind, `BlockId.Water = 9`: not solid, transparent, `Liquid`. No per-block state: a chunk is a `BlockId[4096]` and a `Version`. | `Voxel/BlockId.cs:36`, `Voxel/BlockDefinition.cs:48`, `Voxel/VoxelChunk.cs` |
| World | `VoxelGrid` of 16³ chunks, 384 × 128 × 384 here. Writes only through internal `SetBlock` and `FillColumn`; `EditCount` counts edits. | `Voxel/VoxelGrid.cs` |
| Generation | Water is filled per column up to `Terrain.WaterTop` (`waterLevel` 32, the lake, the river path, the pool). The waterfall is ordinary water blocks standing in a column. | `World/Generation/SceneBuilder.cs:159`, `TerrainGenerator.cs:255-589` |
| Simulation | 60 ticks a second. No block updates of any kind (no crops, no fire, no fluid). The grid changes only on a gather, a place, the debug skip and the build. Water can't be placed; a bottle fills from it. | `Simulation/VoxelSimulation.cs:116`, `:470`, `:539`, `:58-92` |
| Physics | `PlayerBody.Step` doesn't know about water: water isn't solid, so the body sinks through it at full gravity and walks the bottom. No "eye in water" query. | `Physics/PlayerBody.cs:129-194` |
| Rendering | Water has its own translucent mesh drawn back to front with `BasicEffect`. The surface sits at 14/16 of a cell; the side faces choose `Water`, `WaterShallow` or `Waterfall` art from the depth and the neighbours. Static textures, no animation. Fog is the sky's horizon colour. No render targets, no post-processing, no custom `.fx` (mgfxc needs Wine on Linux). | `Graphics/World/ChunkMesher.cs:84`, `:635-712`; `VoxelRenderer.cs:129-144`, `:435-444`; `BlockArtNature.cs:718-737` |
| HUD | No needs, health or breath on `main`; no screen effect layer. | `UI/Hud/` |
| Sound | River and waterfall ambient beds. `FootState.InWater` and `SoundCue.FootWater` are wired, but nothing sets `InWater`. | `Sound/AmbientBeds.cs:121-124`, `Sound/Footsteps.cs:41`, `:180` |
| Save | A diff of chunks against a freshly built scene, as runs of `BlockId` (no state field). Simulation state (`VoxelSimulationState`) keeps the player's body. `CurrentVersion = 5`. | `Save/VoxelGridDiff.cs`, `Save/ChunkEdit.cs`, `Save/SaveGame.cs:48` |

`docs/game/implementation-plan.md` says "water is wadable (no swimming)": this series replaces that.

## 3. What `feature/bread-pilot` touches, and how this plan stays clear of it

Bread-pilot changes nothing about water, breath, swimming or the view underwater. But it rewrites some files this plan would naturally
reach for. The rule here: **the H series puts its logic in new files, and changes a file bread-pilot touches only at a named hook of a few
lines.**

**Files bread-pilot rewrites, which the H series does not change at all:**

- `Voxel/BlockDefinition.cs`: bread-pilot adds `Material` to every row of the table. So the H series adds **no block kinds** (no
  `WaterFlowing1..7`, no new rows) and **no `BlockId` values**. A water cell stays `BlockId.Water`; how full it is lives beside the
  block (section 4.1).
- `Graphics/Blocks/BlockAtlasLayout.cs`, `BlockArt.cs`: bread-pilot appends icon cells. The animated water art lives in its own texture,
  `WaterTextures`, outside the block atlas (section 4.8).
- `Input/GameKey.cs`, `KeyBindings.cs`, `content/game-v1/game/controls.json`: no new key. Swimming uses jump (held to swim up); you sink
  by letting go.
- `UI/Hud/ViewEffectsLayer.cs`, `NeedsHud*.cs`, `Needs/*`, `VoxelSession.Death.cs`, `Event/WorldEvent.cs`: the H series adds its own
  layers, components and events and doesn't edit these before H11. Drowning reaches bread-pilot's health only through a port the H series
  defines (section 4.4).
- `content/`: no content edit, so no bundle re-hash (the tuning is code constants, like `VoxelPhysics`).

**Files neither branch shares, which the H series may change freely:** `VoxelChunk.cs`, `VoxelGrid.cs`, `ChunkMesher.cs`,
`VoxelRenderer.cs`, `ChunkVersionTracker.cs`, `SceneDrawer.cs`, `Footsteps.cs`, `AmbientBeds.cs`, `VoxelSimulationState.cs`,
`ChunkEdit.cs`, `BlockRun.cs`, `VoxelGridDiff.cs`, `SimulationEvent.cs`, and everything under the new folders.

**Hooks in files both branches touch.** These conflicts are expected. Each one is small and its resolution is written down here:

| File | Bread-pilot's change | H series hook | Resolution at merge |
|---|---|---|---|
| `Physics/PlayerBody.cs` | `Step(..., int speedPermille = 1000)` scales the walk | `partial` on the class; one branch at the top of `Step` hands over to `PlayerBody.Swim.cs` when the body is immersed | Keep bread-pilot's signature and pass `speedPermille` to the swim step (H11) |
| `Simulation/VoxelSimulation.cs` | Tool speeds, `WalkPermille`, fires | `partial` on the class; one call to `StepWater()` (in the new `VoxelSimulation.Water.cs`) in `Step`'s fixed order; `IsEmptyOrDecor` also accepts a water cell so a block can be placed into water | Keep both; the call order is written in `Step`'s comment |
| `Session/VoxelSession.cs` | Needs, objectives | Nothing: the new partial `VoxelSession.Water.cs` holds the composition | No conflict expected |
| `UI/Common/LayerStack.cs` | Inserts fade, death, view effects, needs | Inserts `BreathHudLayer` (H04; the tint is drawn by the underwater pass, not a layer) | Bubbles go just above `NeedsHudLayer` |
| `UI/Common/FrameView.cs` | Many new members | Appends `Breath` and `Underwater` | Keep both |
| `VoxelGame.cs` | 316 lines (needs, death, settings) | At most the two lines that copy breath and underwater into `FrameView`; the render target is inside `SceneDrawer` | Keep both |
| `Save/SaveGame.cs` | `CurrentVersion` 5 → 6 (W12 plans 7) | One bump for the water levels | Take the higher number plus one (no save migration: the game is unreleased) |
| `Save/SaveDocument.cs` | `SaveSettings` gains `ViewEffectsOff`, `RelaxedNeeds`, `NeedsRate` | `SaveSettings` gains `UnderwaterBlur` (one trailing parameter, H11, after the merge) | Done on the merged branch: no conflict |
| `Startup/LaunchOptions.cs` | New launch options | `--underwater-blur <off\|low\|medium\|high>` (H05) | Keep both |
| `Sound/SoundCue.cs` (H10 only) | Appends `NeedStage` | Appends `WaterSplash`, `UnderwaterBubbles` | Keep both; the cue isn't persisted, so renumbering is harmless |

A lighter option is to merge `main` into `feature/water` (or `feature/water` into `feature/bread-pilot`) once H05 is done, so the
hooks are reconciled while they're still few, and H06 onwards then start from the merged state.

## 4. Design

The new code lives in one vertical slice, `src/Vocaterra.Game.Core/Water/`, and its drawing in `src/Vocaterra.Game/Graphics/Water/` and
`src/Vocaterra.Game/UI/Hud/`. Each type has one job and is tested on a small hand-built `VoxelGrid`, not on the shipped scene, so the tests
say nothing about this map. The shipped scene appears in one invariant test only (section 4.7).

### 4.1 The water cell: level and storage

- **`WaterLevel`** (value type): `Source` (full, made by the scene or poured), or `Flowing(distance 1..7)` (it thins out as it spreads),
  with a `Falling` flag (fed from above, drawn full height). It is like Minecraft's water state.
- **Storage:** `VoxelChunk` gains a byte layer, `_water`, allocated lazily. A chunk with only source water (every chunk the scene builds)
  allocates none, and a missing layer reads as `Source`. Setting a level bumps `Version`, so the renderer's existing version check
  re-meshes without new plumbing. `ComputeDigest` folds the layer in when it exists, so digests of unchanged scenes are byte-for-byte as
  before.
- **`VoxelGrid`** gains `GetWater(x, y, z)` and an internal `SetWater(x, y, z, BlockId, WaterLevel)`. It also gains the observer the flow
  system needs: `IBlockChangeListener.OnBlockChanged(x, y, z, old, new)`, raised by `SetBlock` (never by the scene build's `FillColumn`,
  so building a scene wakes nothing).

### 4.2 Immersion (one query, used by everything)

**`Immersion.Of(VoxelGrid, PlayerBody)`** returns a small record: `Feet` (the feet cell is water), `Body` (water reaches the waist, the
threshold for swimming), `Eye` (the eye point is inside the water volume, below the drawn surface of that cell for its level), and the cell's
flow vector (section 4.6). This is a pure function. Physics, breath, footsteps, sound and the view all read the same record each tick, so they
can't disagree about whether the player is underwater.

### 4.3 Swimming (`Physics/SwimMotion.cs`, `PlayerBody.Swim.cs`)

A Minecraft-like swim, in the body's fixed-point units, deterministic per tick:

- **Wading** (`Feet` only): walk speed × 0.8; jump as usual.
- **Swimming** (`Body`): gravity × 0.2 and a vertical drag, so you sink slowly (top sink speed about 1.5 blocks/s). Horizontal speed is
  0.6 × walk. **Jump held** swims up at about 2.5 blocks/s; at the surface, holding jump keeps the head bobbing just above.
- **Leaving the water:** swimming into a wall with jump held, when the cell above the wall is free, gives a one-time lift over a 1-block
  ledge (Minecraft's hop out). Without it, a player can't climb out of the lake onto a bank.
- **Landing in water** cancels the fall speed (it matters once bread-pilot's needs or a fall rule look at landings).
- **Currents** add their push (section 4.6) to the velocity while `Feet` or `Body`.
- `SwimMotion` holds the numbers and the velocity rules. It's a component the body composes, so the walk code isn't touched beyond the
  branch in section 3. (Footsteps already read water at the feet, `Footsteps.StateOf`; a first survey said otherwise.)

### 4.4 Breath (`Water/Breath.cs`)

- **Breath** holds `Air` in ticks: 900 when full (15 s), shown as 10 bubbles. While `Eye`, it loses 1 a tick. Otherwise it refills to
  full in about 2 s.
- It **raises events** (the observer pattern): `BreathChanged`, `OutOfAir` (once when it reaches 0, then every second while it stays 0),
  and `BreathRestored`. Breath decides nothing beyond its own count.
- **Running out of air hurts until you surface or die** (owner, 2026-11-07). `DrowningDamage` listens to `OutOfAir`: on each one it takes
  **10 health** (of bread-pilot's 100) through **`IPlayerHealth`**, a small port the H series defines (`Damage(amount, cause)`,
  `IsDead`). Starting from full health, a player who stays down dies about 10 s after the last bubble goes, about 25 s after diving. That's
  close to Minecraft and leaves time to swim up. While drowning, health doesn't recover (the port says so; bread-pilot's needs already block
  recovery while health is being lost). Death is bread-pilot's: its death screen, the belongings chest and the wake (`VoxelSession.Death.cs`).
  The drowning rule only raises a `player_drowned` cause, so the death hint can say "Swim up to breathe".
- **Before the merge** `main` has no health. So on `feature/water` until H11, `IPlayerHealth` has no adapter: breath runs out and
  `OutOfAir` fires, which the tests check with a fake health, but nothing in the running game takes damage yet. H11 adds the adapter over
  bread-pilot's `PlayerNeeds` (one class, `NeedsHealth`), and from then the rule above is live. There's no interim fallback to delete
  later.
- Saved in `VoxelSimulationState`, next to the body, so a save taken under water loads under water with the same air.

### 4.5 Flow simulation (`Water/Flow/`)

A cellular, event-driven model in Minecraft's spirit, split by responsibility:

- **`FlowRules`** (pure): given a cell and its six neighbours, returns its next state. Water flows **down first**. It spreads **sideways
  only when it can't go down** (resting on a solid block or on source water), losing one level per cell, up to 7 cells. Sideways it
  prefers the directions that reach a drop soonest, looking 4 cells out. A flowing cell with no feeding neighbour drains away. Two
  sources do **not** make a third (no infinite water), to keep it predictable.
- **`FlowQueue`**: the cells scheduled for an update, ordered by (due tick, y, z, x), so runs are deterministic and no `HashSet` order leaks
  in.
- **`FlowSimulation`**: every 15 ticks (4 updates a second, Minecraft's pace) it takes due cells, up to a **budget of 2,048 cell updates per
  step** (leftovers wait for the next step, so a broken dam can't stall a frame on a larger map), applies `FlowRules`, writes changes via
  `SetWater`, and schedules the neighbours of each change. It listens to `VoxelGrid` (`IBlockChangeListener`): **any** block change next
  to water wakes that water. That is how it works for any lake or river, present or future.
- **The scene's water is at rest when built.** Scene water is all `Source` and nothing is scheduled at build, so loading a map changes
  nothing and costs nothing. The waterfall stays as it stands: each cell of its column has water below it, so even when woken it only
  flows down, into water. H07 proves this for the shipped scene (section 4.7).
- **What wakes water** (player actions that exist on `main`):
  - gathering a block next to water (a trunk by the river), and the water fills the gap;
  - placing a block into a water cell: `IsEmptyOrDecor` also accepts water, so planks displace water, as in Minecraft, and you can dam
    a stream;
  - a scripted-run step, `pour` in `Scripted/VoxelScript.cs` (untouched by bread-pilot), that sets a source, so QA and scripted runs can
    flood a channel on purpose.

  A bucket is left for after bread-pilot's W12 (items as data), so the H series adds no item rows (section 7).
- **Save:** a level of `Source` saves as today. `ChunkEdit` gains an optional run list of non-source levels (`WaterRun`), and the flow
  queue goes into `VoxelSimulationState`, so a save taken mid-flood resumes exactly. That is one format bump (section 3).

### 4.6 Flow vectors and currents (`Water/Currents/`)

- **`FlowVector.At(grid, cell)`**: for flowing water, the downhill direction of the level gradient (as Minecraft computes it), plus
  straight down for `Falling`.
- **`RiverCurrents`**: source water in a river has no gradient, so it would read as still. It's built once per session from the layout's
  `RiverSpec` list: each river column gets the direction of its path segment, pointing downstream (from the end with the higher water
  surface to the lower; if both are level, in path order, which `content/README.md` documents as upstream to downstream). It isn't saved,
  because it can be rebuilt from the layout. Lakes and pools have no current. A new river in `scene.json` gets a current with no code
  change.
- **Uses:** swimming adds a push (about 0.6 blocks/s in a river, more in flowing water). The renderer orients and scrolls the water
  texture along the vector. A bottle still fills from any water cell.

### 4.7 The "water at rest" invariant (H07)

A test builds the shipped scene, wakes **every** water cell, runs `FlowSimulation` to a standstill (with a cap), and asserts that no block
or level changed. This is the guarantee that the H series leaves the map exactly as it is. It also runs on any future map, so a layout edit
that leaves water hanging over a gap fails it (the fix is in the layout, never a special case in the rules). If the current scene does
**not** pass, H07 stops and reports each cell, with a screenshot, for the owner to decide (section 6, decision 2).

### 4.8 Rendering flowing water (`Graphics/World/ChunkMesher.cs`, `Graphics/Water/`)

- **Surface height from the level:** a source keeps today's 14/16. A flowing cell is lower as it thins (level 7 at about 2/16). Each
  surface corner averages the neighbouring cells, so a spreading sheet slopes smoothly. Falling cells and cells with water above are full
  height. The mesher's existing liquid code (`AddLiquid`, `AddWaterSurface`) keeps its job and gains the corner heights; the depth and
  clarity alpha stay.
- **`WaterTextures`** (new, in `Graphics/Water/`): the water, shallow and falling art, generated in code like the rest of `BlockArtNature`
  (CC0 by construction), as an **animated strip of 16 frames** in its own texture. It's outside the block atlas, so bread-pilot's atlas cells
  don't move. The translucent pass binds the frame for the tick. The surface's texture coordinates are rotated in the mesh to point along
  the flow vector, so the ripples run downstream and the waterfall's sheet falls. Still water gets a slow, gentle shimmer.
- The alpha mesh carries only water today, so it switches texture as a whole and needs no new mesh kind.

### 4.9 The view under water (`Water/UnderwaterView.cs`, `Graphics/Water/UnderwaterPass.cs`)

- **`UnderwaterView`** (Core, pure): `Amount` from 0 to 1, easing in over about 0.15 s when `Eye` turns true and out over 0.3 s, plus the
  ambient colour of the water. The tests are a few lines of ticks.
- **Fog** (the main cue, and free): while `Amount > 0`, `SceneDrawer` blends the renderer's fog towards a blue-green with an end of about 14
  blocks and a start at 0, and clears to the same colour, so the far lake bed fades into murk.
- **Tint:** a full-screen blue-green rectangle at about 25 % × `Amount`, drawn by `UnderwaterPass` as the last step of the world view
  (changed at H05's launch: as a HUD layer it would have touched `LayerStack`, `FrameView` and `VoxelGame`, all rewritten by bread-pilot).
  So it's under every HUD layer, bread-pilot's `ViewEffectsLayer` included.
- **Blur, mild, with no custom shader:** while `Amount > 0` only, `UnderwaterPass` renders the world into a `RenderTarget2D` and makes a
  half-resolution copy with linear filtering. It then composites the full image at 60 % and the up-scaled half-size copy at 40 %, offset by
  half a pixel in four directions. That softens edges without smearing. Above water the pass is skipped, so it costs nothing on land. The
  strength is set by the `UnderwaterBlur` setting below.
- **The blur is configurable** (owner, 2026-11-07): `UnderwaterBlur` is `Off`, `Low` (the default: the 40 % blend above), `Medium` (60 %,
  a quarter-size copy) or `High` (80 %). It's a value object in Core that maps a level to the pass's numbers, so the pass holds no setting
  logic. Until H11 it's set at launch with `--underwater-blur`, so scripted runs can screenshot each level. The settings screen exists only
  on bread-pilot, so its row ("Underwater blur: off / low / medium / high") and saving the choice (`SaveSettings.UnderwaterBlur`, written
  in `VoxelGame`) both come in H11. Saving earlier would have touched `SaveDocument` and `VoxelGame` for a value nobody can change in the
  game yet. Then bread-pilot's `ViewEffectsOff` (the accessibility switch) also forces the blur off. The fog and the tint are never switched
  off, because they tell the player where they are.

### 4.10 The bubble meter (`UI/Hud/BreathHudLayer.cs`, `BreathHudLayout.cs`, `BubbleIcon.cs`)

- Ten bubbles in a row above the right half of the hotbar, as in Minecraft. A bubble pops (a 3-frame shrink) as each tenth of the air goes.
  The meter shows while `Eye` or while `Air` is below full, and fades out 1 s after it refills.
- `BreathHudLayout` is pure (positions for the screen size and UI scale), so it's unit-tested like `HudLayout`. The layer only draws.
  `BubbleIcon` is drawn in code (CC0).
- At the merge, the layout stacks above bread-pilot's `NeedsHudLayout` bars instead of a fixed offset (H11).

### 4.11 Sound (H10, optional)

`SoundCue.WaterSplash` on entering water at speed, `UnderwaterBubbles` while breath runs down, and ambient beds low-passed (quieter and
duller) by `UnderwaterView.Amount`. Footsteps in water already have a cue.

## 5. Milestones

Models follow the owner's rule (2026-11-06): Haiku 5.5 for small tasks, Sonnet 5.5 at medium effort for medium ones, at high for large or
design-heavy ones. At most two building agents at a time. Every milestone: `dotnet build -warnaserror`, the game tests, `dotnet format
--verify-no-changes`, and tests named Given_When_Then in folders that mirror the source.

| Id | Milestone | Size | Model | Depends on | Touches bread-pilot files? |
|---|---|---|---|---|---|
| H01 | **Water cell and immersion:** `WaterLevel`, the lazy level layer in `VoxelChunk`, `GetWater`/`SetWater`, `IBlockChangeListener` on `VoxelGrid`, `Immersion.Of`. Digests unchanged for a built scene (test). | M | Sonnet, medium | none | No |
| H02 | **Swimming:** `SwimMotion`, `PlayerBody.Swim.cs`, the branch in `Step`, wading, jump-held swim up, surface bob, hop out, landing in water. | M | Sonnet, medium | H01 | `PlayerBody.cs` (one branch) |
| H03 | **Breath and drowning:** `Breath`, its events, `DrowningDamage` against the `IPlayerHealth` port (tested with a fake), the save in `VoxelSimulationState`, `VoxelSession.Water.cs`. | M | Sonnet, medium | H01 | `VoxelSimulation.cs` (one call) |
| H04 | **Bubble meter:** `BreathHudLayout`, `BreathHudLayer`, `BubbleIcon`, `FrameView.Breath`. | S | Haiku | H03 | `LayerStack.cs`, `FrameView.cs`, `VoxelGame.cs` (a line each) |
| H05 | **The view under water:** `WaterVolume`, `UnderwaterView`, fog blend and clear colour in `SceneDrawer`, `UnderwaterPass` (the render target, the blur and the tint), the `UnderwaterBlur` setting (`--underwater-blur` at launch), screenshots above and below the lake surface at each blur level. | M/L | Sonnet, high | H01 | `LaunchOptions.cs` (one block) |
| (merge point) | Optional: reconcile with bread-pilot here (section 3). | | | | |
| H06 | **Flow simulation:** `FlowRules`, `FlowQueue`, `FlowSimulation` with its budget, waking on grid changes, placing into water, the scripted `pour` step, save levels (`WaterRun`) and the queue, format bump. | L | Sonnet, high | H01 | `VoxelSimulation.cs` (`IsEmptyOrDecor`), `SaveGame.cs` (version) |
| H07 | **Water at rest:** the invariant test of section 4.7 on the shipped scene, and a timing check that waking all water stays inside the budget. Stops and reports if a cell moves. | S | Haiku | H06 | No |
| H08 | **Flow vectors and currents:** `FlowVector`, `RiverCurrents` from `RiverSpec`, the push in `SwimMotion`, the downstream rule documented in `content/README.md`. | M | Sonnet, medium | H02, H06 | No |
| H09 | **Rendering flowing water:** corner heights from levels in `ChunkMesher`, `WaterTextures` (16 animated frames, outside the atlas), UVs along the flow vector, frame binding in the translucent pass; mesher tests and screenshots of the river, the waterfall and a poured source. | L | Sonnet, high | H06, H08 | No |
| H10 | **Water sounds (optional):** splash, bubbles, muffled ambient beds. | S | Haiku | H03, H05 | `SoundCue.cs` (appended) |
| H11 | **Reconcile with bread-pilot** (when the two branches meet): `speedPermille` into the swim step; `NeedsHealth`, the `IPlayerHealth` adapter over `PlayerNeeds`, so drowning damages and kills, with the `player_drowned` death hint; bubbles stacked above `NeedsHudLayout`; the blur row in the settings screen, saved in `SaveSettings`, and `ViewEffectsOff` forcing it off; one save version after bread-pilot's; the plan's "wadable" line updated; the owner's decisions of section 6 entered in `docs/game/decisions.md` under the next free D-game number. | M | Sonnet, medium | H01 to H09 and the bread-pilot merge | Yes, by design |

The first five milestones give the player the underwater experience (swim, breath, bubbles, the view) on its own. The next four make the
water itself move. The order lets H04 and H05 run side by side, then H06; H08 and H09 follow H06.

**Verification at the series' end** (`docs/process/` cadence: full tests and scripted runs at the milestone that closes the series): the full
suite, the merge gate on `feature/water`, and a scripted run that walks into the lake, dives, surfaces, swims to the bank and hops out, and
dams the river with planks; screenshots at each step for the owner.

## 6. The owner's decisions (2026-11-07)

They go into `docs/game/decisions.md` at H11, under the next free D-game number. They aren't entered now, because bread-pilot may take the
next number first.

1. **Running out of air:** you take damage until you surface or die (section 4.4: 10 health a second, bread-pilot's death flow). No interim
   fallback; the damage is live from H11, when bread-pilot's health arrives.
2. **If H07 finds water that would move when woken:** as recommended. Report each cell with screenshots and fix the layout or keep the cells
   as built, cell by cell. No special cases in the flow rules.
3. **Placing blocks into water:** yes (dams, section 4.5). The bucket stays after bread-pilot's W12.
4. **The blur:** configurable, `Off` / `Low` (default) / `Medium` / `High` (section 4.9).

## 7. What this plan does not do

- No map change: no new lake, river, spring or terrain, and no change to any layout spec or to the scene's generator.
- No new block kinds, block-table rows, atlas cells, keys, content files or core (`Vocaterra.Nlp`) change.
- No bucket, boats, fishing (bread-pilot's W29), rain, ice or tides.
- No custom shader (`.fx`): everything uses `BasicEffect`, `SpriteBatch` and render targets, so the build needs no Wine.
- No NPC swimming: NPCs keep their walk planner, which already refuses water cells (`Navigation/WalkPlanner.cs:166`). Flowing water is
  still `BlockId.Water`, so a flooded path is avoided with no change (a case for H06's tests, not a feature).

## 8. Progress

| Id | Status | Gate | Notes |
|---|---|---|---|
| H01 | done, 2026-11-07 | merged at `68457dc6`, 8869 passed, 0 failed | `WaterLevel` (byte: distance in bits 0-2, falling bit 3), `WaterSurface` (source 14/16; flowing 12, 10, 8, 7, 5, 3, 2 for distance 1..7; falling or water above 16/16), the lazy level layer in `VoxelChunk` (digest unchanged without levels, pinned by a test), `VoxelGrid.GetWater`/`SetWater`, `IBlockChangeListener` and `BlockChange` (internal, raised by `SetBlock` and `SetWater` only), `Immersion.Of`. Finding for H06: `Save/VoxelGridDiff.cs:29` compares blocks only, so a level-only change isn't saved yet. |
| H02 | running (Sonnet 5.5, medium), worktree `.claude/worktrees/h02`, branch `H02-swimming` from `b9ef971c` | | |
| H05 | running (Sonnet 5.5, high), worktree `.claude/worktrees/h05`, branch `H05-underwater-view` from `b9ef971c` | | Screenshots to the scratchpad for the owner |
