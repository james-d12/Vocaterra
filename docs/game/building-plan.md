# Building: the oak set and shaped blocks (the K series)

The owner, 2026-11-27: "Can you work on adding a building system to the game? we need basics so player can place blocks, lets introduce
an oak wood complete item set, so stairs, slabs, planks, etc. like minecraft." Then: "building restrictions apply within the valley, but
the procedural area is free build." The decision is [D-game-36](decisions.md#d-game-36-building-the-oak-set-and-shaped-blocks-decided-2026-11-27);
the statuses are in [the game plan](implementation-plan.md) section 4.16. Branch `feature/building` (cut from `main` at `5791c955`),
worktree `.claude/worktrees/building`, commit prefix `K<nn>:`.

## 1. The owner's answers (2026-11-27)

| Question | Answer |
| --- | --- |
| Which oak pieces | **The building set:** oak log (placed along the axis of the clicked face), planks, stairs, slab (a double slab when two meet), a fence that joins its neighbours, a fence gate, a door (two blocks tall, opens) and a trapdoor (opens). Not now: bark-all-round wood, stripped logs, buttons, pressure plates, signs. |
| How the player gets them | **Crafted from `madera` at the workbench (`mesa`).** Trees still drop `madera`, which still places as planks; shops, lessons, recipes and dialogue that name `madera` do not change. |
| Walking up | **Half-block steps:** a grounded walk climbs a slab or a stair step without a jump; a full block still steps up as today. |
| Where | **The land rules apply in the valley; the procedural area is free to build in.** `LandRules` already covers only the valley (`LandRules.Create`: "the rules cover the valley only"); the series keeps it so and a test pins it for every new block. |

## 2. What exists (survey of 2026-11-27, at `5791c955`)

Paths are under `src/Vocaterra.Game.Core` (Core) and `src/Vocaterra.Game` (Game).

- **One `BlockId : ushort` per cell, nothing else** (`Core/Voxel/BlockId.cs`, `VoxelChunk.cs`). Ids 0 to 66 are used, appended only and
  never reused; the limit is 128 (`BlockKinds.Capacity`, the `UInt128` kind masks, the mesher's `KindFlags[128]`). Types are a code enum
  with `BlockDefinition.Table`, a dense array indexed by id. Water is the precedent for a second per-cell layer: a lazily allocated
  `byte[Volume]` in `VoxelChunk`, a digest extension and `WaterRun`s in the save.
- **Non-cube shapes are keyed by id only:** `ChunkMesher.ShapeOf` (Cube, Liquid, Plant, Cross, Torch, Lantern, Pile, Door, Fence). The
  scene's `Door` is a permanently open, non-solid panel whose axis comes from its neighbouring walls; `Fence` and `Counter` are `Tall`
  (they block a body two blocks high). Beds infer their direction from neighbours.
- **Meshing:** `ChunkMesher.Build` from a `ChunkSnapshot` that copies only block ids. `FaceVisible` hides a face behind an opaque cube
  (`ShapeOf == Cube && !Transparent`); ambient occlusion's `Occludes` counts any Cube-shaped block; a chunk-wide skip tests opaque cubes
  on the six face layers. `SkyExposure` treats any solid non-leaf block as blocking the sky. The far mesher (`Far/FarTileMesher.cs`)
  treats every block as a full voxel.
- **Collision and the ray use full unit cells:** `VoxelCollider.Collides` asks `grid.BlocksBody` (solid, the cell above a `Tall` block,
  `StopsBody`); the body is 0.6 × 1.8 blocks in 1/4096-block units; `StepHeight = 4096` (one block) in `PlayerBody.MoveWithStepUp`.
  `BlockRay` is a cell DDA with no sub-cell test. `BlocksBody` and `CanStandOn` are duplicated in `Core/Session/SleepWalk.cs` and
  `ChunkMesher.cs`.
- **Placement:** right click → `LiveVoxelInput.PlacementFor` → `VoxelCommand.Place(item, x, y, z)` (all four ints used) →
  `VoxelSimulation.TryPlace`: refuses out of bounds, an occupied cell, out of reach, no `PlacedBy` block or none in the bag, no face
  neighbour, a solid block overlapping the player, or `PlaceRefusedByLand` (only `Torch` exempt). `BlockDefinition.PlacedBy` is a
  hard-coded switch (`Wood → Planks`, `Torch → Torch`); the `placeable` tag in `items.json` is not read. Breaking: only trunks and the
  berry bush drop items; placed `Planks` can be taken back on owned or unprotected land (`TakeBackRefused`); anything else breaks only
  under the break-all cheat. `SaveMigration.IsPlaceable` is `Planks or Torch`.
- **Items:** `ItemId` is a code enum; `ItemCatalogue` needs a JSON row per member. Recipes are `{output, count, ingredients, station}`
  with Spanish noun keys; stations `mesa` and `fuego`; the only wood recipes are `antorcha`, `escudo` and the axe's repair.
- **Art is drawn in code** (`Game/Graphics/Blocks/BlockArt*.cs` into `BlockAtlas`, 8 columns of 16 px, `ColourRows = 17`, nearly full,
  with a grey twin per cell for the colour rule). `BlockAtlasTests` need a picture on every face of every block; `Trunk` has `TrunkSide`
  and `TrunkEnd`, `Planks` one cell.
- **Saves:** version 11, older refused. `ChunkEdit` holds run-length `BlockRun`s with the enum **name** (a rename breaks saves) and
  optional `WaterRun`s.
- **Words:** `nouns.json` rows `{lemma, items, blocks}`; `NounCatalogTests` needs a noun with a gender and a lexicon entry for every item;
  blocks need none. Per-block tables a new id must fill: `Footsteps.SurfaceOf`, `SoundEventMapper`, `BlockAtlasLayout.CellFor`,
  `ChunkMesher.ShapeOf` and `KindFlags`.
- **No earlier design** for stairs, slabs, rotation or opening doors (the inn's upstairs was deferred, settlement plan, "no stairs in this
  slice").

## 3. Design

### 3.1 A per-cell state byte

A cell gains an optional **state byte**, stored like water: a lazily allocated `byte[Volume]` in `VoxelChunk` that stays null while
every state in the chunk is 0. A state means nothing on its own; the block's id says how to read it (`BlockState` helpers, one per
layout):

| Layout | Bits | Used by |
| --- | --- | --- |
| Axis | 2: X, Y, Z | oak log |
| Facing + half | 2 facing (N, E, S, W) + 1 upside down | stairs |
| Slab | 2: bottom, top, double | slab |
| Facing + open | 2 facing + 1 open | fence gate |
| Door | 2 facing + 1 open + 1 hinge right + 1 upper half | door (two cells, same id) |
| Facing + half + open | 2 facing + 1 top + 1 open | trapdoor |

- `IVoxelReader.GetState` and `IVoxelWriter.SetBlock(pos, id, state)`; setting a block without a state writes 0, so every existing call
  keeps its meaning. `ChunkSnapshot` copies the state array (null when absent).
- **Digests:** the chunk digest takes the state only when the array exists, so a built scene's digests and `stateDigest` stay as they are
  (a test, like water's).
- **Saves:** `ChunkEdit` gains optional `StateRun`s (run-length, like `WaterRun`); the save takes the next version at K01's merge (12, or
  the next free number then) and refuses older saves, with no migration (the owner's standing rule).
- The fence keeps no state: its arms are worked out from its neighbours when it is drawn and when it collides.

### 3.2 Shapes made of boxes

A shape is a short list of axis-aligned boxes in sixteenths of a block, from one pure function in Core,
`BlockShapes.Of(id, state, neighbours)` (the neighbours only for the fence and gate arms). A full cube is the fast path and costs nothing
new. The same shapes feed collision, the ray, the overlap check, standing and the mesher, so what you see is what you bump into.

| Block | Shape (closed) |
| --- | --- |
| Slab | bottom or top half; double = a full cube (drawn and treated as one) |
| Stairs | a bottom half plus a back quarter (two boxes), mirrored when upside down; straight only (no corner stairs in this series) |
| Fence | a 4/16 post plus arms to each neighbouring fence, gate or full solid block; collides 1.5 blocks high (above a 1.3-block jump) |
| Fence gate | two posts and bars across the fence line; open: the bars swing aside and it does not collide |
| Door | a 3/16 panel on the side it faces, one per cell; open: the panel turns to the hinge side |
| Trapdoor | a 3/16 slab at the bottom or top of the cell; open: a panel against the side it faces |

Each face of a shape has a **cover** toward its neighbour (full, partial or none), so the mesher hides a neighbour's face only when the
cover is full (a bottom slab covers the face below it, not the faces beside it).

### 3.3 Collision, the ray and steps

- `VoxelCollider` collects the boxes of the cells the body overlaps (and the cell below, for the 1.5-block fence) instead of whole cells.
  `BlocksBody`, `CanStandOn` and the overlap check in `TryPlace` read shapes; the duplicates in `SleepWalk` and `ChunkMesher` call the
  shared code.
- **Steps (the owner's answer):** the step-up in `PlayerBody.MoveWithStepUp` lands on the top of the box it climbs rather than on a whole
  block, so any rise up to one block climbs as today and a slab or a stair step climbs the same way. A step never lifts the body into a
  box above it.
- `BlockRay` keeps its cell walk; on a shaped cell it tests the ray against the cell's boxes, goes on through the cell when it misses
  (an open gate, the top of a bottom slab), and reports the face of the box it hit and **the hit point within the cell** (placement needs
  it, 3.5).
- The timing tests and the frame time are measured before and after (the collider and the mesher are hot paths).

### 3.4 Drawing

- The mesher draws a shaped block's boxes with its texture cropped to each box face (a slab's side shows the lower half of the planks
  cell), from the same `BlockShapes`. Faces inside a cell's own boxes are dropped.
- Culling uses the covers (3.2). Ambient occlusion counts only full opaque cubes (as today), so a slab never darkens a corner it does not
  fill; the chunk-wide skip is unchanged, since shaped blocks are not opaque cubes.
- `SkyExposure`: slabs, stairs and closed trapdoors block the sky like any solid; fences, gates and doors keep their current treatment
  (the scene's fence and door).
- **The far view** draws slabs and stairs as full blocks in the planks colour and leaves out fences, gates, doors and trapdoors (too thin
  to see at that distance).
- **Art:** the log reuses `TrunkSide` and `TrunkEnd` (turned to its axis); stairs, slab, fence and gate reuse the planks cell; the door
  (upper and lower) and the trapdoor get new code-drawn cells in the planks' colour. `ColourRows` rises if the atlas needs room, with the
  grey twins. CC0 rule unchanged (code-drawn art is source).

### 3.5 Placing and breaking

All placement state is worked out by the simulation from its own state (the player's yaw and the target ray's hit point), so the
command stays `Place(item, x, y, z)` and a scripted run is deterministic.

| Block | Placement rule (Minecraft's, simplified) |
| --- | --- |
| Log | axis of the clicked face (top or bottom → Y) |
| Stairs | facing the player's yaw (climbing away from the player); upside down when the click is on a block's underside or the upper half of a side face |
| Slab | bottom, unless the click is on an underside or the upper half of a side face; a slab clicked into the open half of a slab makes it double |
| Fence | no state |
| Fence gate | across the player's line of sight; opens away from the player |
| Door | needs air above and a block to stand on; writes both cells; faces the yaw; the hinge is on the left unless a door already stands there |
| Trapdoor | on the clicked face's side; top half when the click is on an underside or the upper half of a side face |

- The refusals stay (reach, occupied cell, a face neighbour, overlap with the player now against the true shape, the land rules). In the
  valley the land rules refuse every oak piece on a protected column, as for planks (only `Torch` exempt); **outside the valley there
  are no land refusals** (a test places each oak piece in the procedural area).
- **Breaking:** an oak piece breaks and returns its item, by the same rule as placed planks: on owned or unprotected land in the valley,
  anywhere outside it. A double slab returns two slabs; either half of a door removes both cells and returns one door.
  `SaveMigration.IsPlaceable` includes the oak pieces.
- `PlacedBy` reads the item's block from data or a table that covers the new items (the hard-coded switch is extended or replaced; the
  K06 agent picks, the brief asks for the reasoning).

### 3.6 Opening

The use key (the existing interaction binding; the K07 brief names it after reading `controls.json`) opens and closes a door (both
cells), a gate and a trapdoor in reach. It sets the open bit, collision and drawing follow the shape, a sound plays (an existing CC0 or
code-synthesised wood sound, D-game-18), and no other block changes. The scene's own `Door` and `Fence` stay as they are.

### 3.7 Items, words and recipes

- New `ItemId` members (behaviour lives in code: the placement table), rows in `items.json` with icons drawn from the block art, and
  nouns with a gender and a lexicon entry. Proposed lemmas, Latin American neutral Spanish, provisional until the language review
  (D-game-12):

| Piece | Noun | Gender | Note |
| --- | --- | --- | --- |
| Log | `tronco` | m | new |
| Planks | `madera` | f | existing (item `wood`, block `planks`) |
| Stairs | `escalera` | f | new |
| Slab | `losa` | f | new; the agent checks it against the A2 lint and the lexicon, with `tabla` as the fallback |
| Fence | `valla` | f | new; also names the scene's `Fence` block |
| Fence gate | `portón` | m | new; `tranquera` is regional, `cancela` Spain-leaning |
| Door | `puerta` | f | existing noun; the oak door joins it |
| Trapdoor | `trampilla` | f | new |

- **Recipes at the `mesa`, from `madera` only** (there are no sticks): `escalera` ×4 ← 6, `losa` ×6 ← 3, `valla` ×3 ← 4, `portón` ×1 ← 4,
  `puerta` ×3 ← 6, `trampilla` ×2 ← 6, `tronco` ×1 ← 4. The numbers are a first proposal, tuned at the owner's review.
- The colour rule applies as to any noun: the pieces are drawn grey until the learner knows the word.
- Not in this series: shop rows, lessons, checks, NPC lines about the pieces. `CheckBankTests` lists the new items as lesson-free.
- The content gate (`--strict --level a2`) stays clean; the edit re-hashes `content/game-v1` and runs the whole suite.

## 4. Milestones

| ID | What | Depends on | Size | Model |
| --- | --- | --- | --- | --- |
| K00 | The series registered: this plan, D-game-36, section 4.16 of the game plan, the open items, the handover | the owner's request | S | orchestrator |
| K01 | The state byte: the chunk's lazy state array, `GetState`/`SetBlock` with a state, the snapshot, the digest rule, `StateRun`s and the save's next version; no behaviour change | K00 | M | Sonnet 5.5, medium |
| K02 | The oak blocks' ids and state layouts (`BlockState`), `BlockShapes.Of` and the covers, the per-block tables except art and the mesher (footsteps, sounds, kinds), tests of every shape and state | K01 | M | Sonnet 5.5, medium |
| K03 | Collision, the ray and steps on shapes: `VoxelCollider`, `BlocksBody`, `CanStandOn` and their duplicates, the step onto a box's top, `BlockRay`'s box test and hit point; timing tests before and after | K02 | L | Sonnet 5.5, high |
| K04 | Drawing: the mesher's boxes and cropped faces, culling by cover, ambient occlusion, `SkyExposure`, the far view, the new art cells; screenshots of every piece in every state | K02 | L | Sonnet 5.5, high |
| K05 | Items, nouns and recipes: the `ItemId` members, `items.json` rows and icons, the nouns and lexicon entries, the recipes, the content gate and the re-hash | K02 | M | Sonnet 5.5, medium |
| K06 | Placing and breaking: the placement table, state from yaw and hit point, double slabs, the two-cell door, refusals on the true shape, the land rules (valley only, tested outside it), breaking returns the item, `SaveMigration` | K03, K05 | M/L | Sonnet 5.5, high |
| K07 | Opening doors, gates and trapdoors with the use key: the open bit, collision and drawing follow, the sound | K04, K06 | M | Sonnet 5.5, medium |
| K08 | Close-out: a scripted run that builds a small hut with every piece, the screenshot tour, the shared documents (`running.md`, `architecture.md`, `saves.md`), QA (world, ui), the pull request | all | S | Haiku 5.5 |

K03, K04 and K05 run side by side after K02 (Core physics, Game drawing, content; no shared files expected beyond the block tables K02
settles). **Owner review stops:** after K04 (the look of each piece, from screenshots) and at K08 (play it).

## 5. Out of scope, for later series

Other woods (spruce, birch) and stone or brick pieces; corner stairs; replacing the scene's `Fence` and `Door` with the oak pieces;
buttons, pressure plates and signs; bark-all-round and stripped wood; shop rows, lessons and NPC lines; a placement ghost preview;
the inn's upstairs; a creative stock cheat (offered to the owner, not chosen).

## 6. Risks

- **Hot paths:** the collider, the ray and the mesher run every frame or every chunk; full cubes keep their fast path, and K03 and K04
  run the timing tests.
- **The atlas** is nearly full; raising `ColourRows` changes the atlas size (K04 checks the screenshots and the grey twins).
- **The save version** moves at K01; another branch merged first may take the number (the save rule: the next free version at merge).
- **Spanish:** the new nouns are provisional (D-game-12); the A2 lint may reject one, and the plan names a fallback.
