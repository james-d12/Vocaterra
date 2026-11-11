# Inn, player land and settlement expansion

**Status (2026-11-10):** reviewed. The first slice (the inn, the first night, the land purchase and the campsite) is registered
as the **Y series** in section 8, under [D-game-32](decisions.md), and is built on `feature/settlement`. The larger village and
the larger valley stay an outline until that slice has been playtested.

Sections 1 to 6 are the proposal as it was written on 9 November 2026 against `main` at `0a780dbf`; section 7 now carries the
answers. Where section 8 differs from the earlier sections, section 8 holds.

## 1. Intended experience

The player arrives on the mountain, descends to Luna, completes the existing introduction and practical activities, then continues
to the village. An inn provides their first bed and shelter. They earn money through village activities, choose a piece of land
around the village, buy it from the mayor and establish their own campsite. That same land can later support a home.

```mermaid
flowchart LR
    A[Mountain arrival] --> B[Luna and existing introduction]
    B --> C[Village inn: bed and shelter]
    C --> D[Work, trade and explore]
    D --> E[Choose land and buy from mayor]
    E --> F[Place tent and campfire]
    F --> G[Later: build and furnish a home]
```

Direction from the discussion:

- Keep the mountain start, Luna's teaching and the existing lessons. Classes after the introduction remain optional.
- Introduce an inn/tavern with a usable interior, bed and shelter before the player owns land.
- Make the campsite the first purchased land. This replaces the earlier suggestion of free camping followed by buying that land.
- Preserve free choice of location around the village: the player selects a suitable area, rather than choosing from preset plots.
- Expand usable interiors, the village and the surrounding map as part of this workstream.

**Recommended first slice:** mountain to inn, first night, ordinary earning opportunities, one freely located land purchase and a
working campsite. A furnished player house, extensive farming and additional regions follow later. The inn remains useful for meals,
conversations and accommodation after the player moves out.

The [Core Systems design](design/sources/Vocaterra_Core_Systems_Design_Document.docx) proposes a tavern keeper and starter land;
the [Adaptation design](design/sources/Vocaterra_Adaptation_Design_Document.docx) places a tavern and land beyond its initial scope.
[Activities-plan](activities-plan.md) W22 proposes land and plots, W23 frontier claims, and W24 storage. This proposal brings the
first purchase into the opening progression and uses player-selected boundaries, without requiring W23's full spoken wilderness
claim system or W24's general storage system. Its adoption would amend D-game-15's owned mountain camp, the village plan's
principle 1 (the world grows inward; D-game-22 itself only asks to avoid a larger map), the day-one ending under D-game-25, and
the W22/W23 scope and scheduling assumptions. Pilot scheduling and the playtest's bundle freeze ([playtest](playtest.md),
section 3) must be reconciled before changing the shared bundle; this proposal does not silently override those gates. (No
general content freeze exists; D-game-32 (a) settles the pilot question.)

## 2. What the code already provides

Source takes precedence over historical design baselines. In particular, the current save writer is version 7 and the bread
procedure, needs and optional classes already exist.

| Area | Current implementation and implication |
| --- | --- |
| World layout | [scene.json](../../content/game-v1/game/scene.json) defines a 384 × 128 × 384 world, fixed named sites, routes and a village with a levelled radius of 24 blocks. Terrain between sites varies by seed. Expansion needs authored terrain and routes as well as larger dimensions. |
| Buildings | [SceneBuilder.BuildHouse](../../src/Vocaterra.Game.Core/World/Generation/SceneBuilder.cs) makes floors, walls, roofs and open doorways; stamps support authored shapes. [BlockDefinition](../../src/Vocaterra.Game.Core/Voxel/BlockDefinition.cs) makes doors non-solid. There is a starting point for walk-in interiors, but no lodging, usable bed or shelter system. |
| Sleep and recovery | [VoxelSession.Sleep](../../src/Vocaterra.Game.Core/Session/VoxelSession.Sleep.cs), [SleepOffer](../../src/Vocaterra.Game.Core/Session/SleepOffer.cs) and [VoxelSession.Death](../../src/Vocaterra.Game.Core/Session/VoxelSession.Death.cs) use the fixed `firering.player` location. The front-end's [VoxelGame](../../src/Vocaterra.Game/VoxelGame.cs) also checks the fire. These assumptions must be replaced together. |
| Opening | [session.json](../../content/game-v1/game/session.json), the [objectives](../../content/game-v1/game/objectives), [ObjectiveBook](../../src/Vocaterra.Game.Core/Session/ObjectiveBook.cs) and [SessionEnding](../../src/Vocaterra.Game.Core/Session/SessionEnding.cs) tie the opening to the camp checklist and lighting the player's fire. `EndsSession` is currently inferred from objective type and session, so a new land objective could accidentally hold the first night back. |
| Building and gathering | [VoxelSimulation](../../src/Vocaterra.Game.Core/Simulation/VoxelSimulation.cs) checks reach, inventory, occupancy and support for placement; it has no land ownership check. Player placement currently maps wood to planks and torches to torch blocks. A decorative tent stamp is not a placeable camp system. |
| Planting | [VoxelSession.Planting](../../src/Vocaterra.Game.Core/Session/VoxelSession.Planting.cs) uses `CampFireRing` (`firering`, the guide's ring), rather than `PlayerFireRing`. Player land needs an explicit planting area; changing the mountain site's coordinates alone would not move this behaviour. |
| NPC services | [NpcHostEvents](../../src/Vocaterra.Game.Core/Npc/NpcHostEvents.cs) and [HostEventTable](../../src/Vocaterra.Game.Core/Npc/HostEventTable.cs) support authored replies, item exchanges and prices. Lodging rights and plot purchases need game-owned transactions beyond an ordinary item sale. |
| Dynamic places | [Scene](../../src/Vocaterra.Game.Core/World/Scene.cs) exposes generated points and prop cells; [ScenePlaces](../../src/Vocaterra.Game.Core/Navigation/ScenePlaces.cs) builds its label lookup once. A movable camp needs runtime destinations and interaction targets that also survive loading. |
| Saves | [SaveGame](../../src/Vocaterra.Game.Core/Save/SaveGame.cs) writes version 7, and [SystemsSave](../../src/Vocaterra.Game.Core/Save/SaveDocument.cs) provides optional system entries. [SaveMigration](../../src/Vocaterra.Game.Core/Save/SaveMigration.cs) maps changed world dimensions by position, but recognises a limited set of player edits. Furniture, camps and altered settlement terrain need explicit migration handling. |
| World size and rendering | [VoxelGrid](../../src/Vocaterra.Game.Core/Voxel/VoxelGrid.cs) allocates every chunk; dimensions must be multiples of 16. [VoxelRenderer](../../src/Vocaterra.Game/Graphics/World/VoxelRenderer.cs) limits resident meshes by view distance, but that does not make the underlying world streamed. Larger worlds also increase generation, cloning, digest and save work. |

## 3. Recommended design defaults for review

These details are recommendations, not decisions already made by the owner.

- **One combined inn/tavern and one keeper.** Start with a common room, counter and accessible guest room. An upstairs room is
  desirable if stairs and navigation work comfortably in first person; a ground-floor room is sufficient for the first slice.
- **Affordable accommodation.** First stay free, a modest later room fee, and a basic bunk when the player has no money. Define
  access by in-game nights, with the price and duration visible before payment. No accumulated debt or language-dependent access
  to essential sleep. Audit the earliest food and water routes as well as the new, longer walk to the first bed.
- **Useful shelter first.** An authored room and a pitched tent provide a visible, usable place to rest. Retain current night
  recovery values initially. Weather damage, warmth, comfort tiers and arbitrary player-built room detection are separate work.
- **One initial plot with a fixed rectangular footprint.** The player positions it freely within a broad eligible area around the
  village; show its boundary, price and placement refusals. Protected buildings, roads, water and unsuitable slopes are excluded.
  Ensure there are several distinct usable locations across supported seeds. Plot size and price remain tunable data.
- **Buy before establishing the camp.** A provisional marker records the proposed location; it grants no building rights and
  costs nothing. The mayor confirms those exact boundaries and price. Ownership persists without upkeep. Camping equipment is
  included in the first purchase, avoiding a second affordability gate before the player can use their land.
- **Make ownership valuable through control.** The player chooses where to live and can arrange the camp and later build there.
  The tent need not provide better rest than an inn bed. Keep existing orders and trading available as several earning routes;
  do not require a class, language score or trust grind to buy land.
- **Reuse the villager as mayor if approved.** Preserve the NPC's stable id, lesson role, existing services and saved history.
  Add the innkeeper as a new role, with reviewed text and voice. The full registrar and additional professions remain later scope.

## 4. Delivery sequence

The phase numbers below are local planning labels, not reserved milestone ids. Each phase includes its own persistence and focused
tests. Sizes describe relative scope, not time estimates. Register smaller delivery milestones after this plan is reviewed.

| Phase | Scope and principal code areas | Depends on | Reviewable completion |
| --- | --- | --- | --- |
| 0. Layout, economics and compatibility brief (small) | Sketch the inn, village streets, eligible land and future map bounds; measure current routes and earnings. Capture performance baselines and representative old saves. Resolve the policies in section 7. | Plan review | One agreed first-slice layout, accommodation/land rules, migration approach and performance budgets. |
| 1. Rest and home foundations (medium) | Add game-owned rest locations, access rules and a selected recovery location; route sleep, death recovery, prompts, beacon and save state through them. Adapt existing campfire behaviour first. | 0 | The current camp still works; a test bed produces the same night, needs and learner-spacing transitions. Missing or obstructed home locations have a safe fallback. |
| 2. Inn interior and lodging (medium/large) | Build the first interior using scene/stamp foundations; add usable bed fixtures, room access, innkeeper services, lodging transactions and content. Extend interaction and rendering only where needed. | 1 | Walk in, ask for lodging using speech/type/help, find the bed, sleep, wake safely and save/load. Empty funds and repeated payment attempts are recoverable. |
| 3. Revised opening (medium) | Keep the mountain arrival and Luna's lesson/activities; replace owned-camp spawn dressing and tent-note instructions. Continue to the inn, update objectives, ending, tips, transfer/check flow and skip aids. | 2 | A fresh player completes the existing introduction, reaches accommodation and starts day two without buying land or taking an optional class. |
| 4. Land selection and purchase (large) | Add eligible/protected areas, plot preview and provisional selection; mayor purchase flow, runtime place lookup and persistent ownership. Enforce permissions at world mutation points. | 0, 3 | Select a valid site freely, review boundaries and price, buy once, reload ownership and build only where permitted. Refused purchases consume nothing. |
| 5. Placeable campsite (medium/large) | Add camp equipment and atomic placement/removal, tent/bedroll and fire interactions, a selectable home point, camp objectives and owned-land planting. | 1, 4 | Establish the camp on purchased land, light the fire, sleep there, recover there and reload with all placements intact. The inn remains usable. This completes the first slice. |
| 6. Larger village and further interiors (large) | Expand streets, homes, gardens and service spaces; add the mayor's interior, bakery/workshop interiors and clear landmarks. Preserve lesson interactions, trades, water access and buildable outskirts. | First-slice review, migration foundation | Daily routes remain readable; every added interior has a purpose; moved NPCs, stations and named places remain reachable in new and migrated games. |
| 7. Larger valley (large, measured) | Extend terrain relief, routes, vegetation, water and destinations beyond current bounds; profile generation, rendering and saves. Address demonstrated bottlenecks before choosing final dimensions. | 0 baseline, 6 layout | A larger map with useful destinations and room around the expanded village passes route, water, migration and performance checks. |

Phase 2 may enlarge the existing village just enough to fit the inn. Phase 0 reserves space for later streets and player land so
phase 6 does not build over newly purchased plots. The wider map expansion remains an explicit deliverable in phase 7.

## 5. Implementation boundaries

### Rest, accommodation and the opening

Keep rules and persistent state in `Vocaterra.Game.Core`; MonoGame remains responsible for drawing, targeting and input. A rest
location needs a stable identity, interaction position, safe wake area and access policy. The session selects a recovery location
separately from the currently targeted bed. Reuse `SaveGame.Sleep`, needs recovery and daily resets instead of creating a second
night system. A normal sleep advances the day and sitting exactly once; loading advances the sitting without granting a new night.
Sleep collapse continues to use its existing distinct rules.

Define a safe recovery location before the inn is reached, then the inn fallback and the player's chosen campsite. Expired room
access, blocked beds and removed camp equipment must not leave death recovery without a destination. Preserve needs pauses during
conversation and panels, speech repair and typed/button recovery.

Give the opening an explicit completion condition independent of land ownership and the later campfire objective. Retain lesson
ids, steps, taught language and class prerequisites; change surrounding world guidance and rewards only as necessary. Review the
existing checklist's held-item accounting so food consumed on the way to the inn cannot unexpectedly erase completed introduction
progress. Update `SessionEnding`, `PlaytestFlow`, autosave events and `SessionSkip` with the same progression rules.

### Ownership and camp placement

Keep static eligibility/protection regions in validated content and runtime selections, owned boundaries and home locations in
saved game state. Show boundaries in a world preview with keyboard controls and explanations beyond colour alone. The purchase
checks the selected area and funds again, then transfers coins and grants ownership as one operation. Cancellation, a failed
check, a repeated confirmation or a reload must not duplicate payment, land or starter equipment.

World permissions must cover placement, gathering/removal and camp placement, including direct simulation commands. Preserve
ordinary resource gathering and explicit tutorial/workstation permissions: the torch exercise at Luna's ring must still work
before ownership. Protect village infrastructure without making the wider valley unusable. Review existing water-building uses
when deciding where placement remains allowed.

Camp placement must validate the whole structure, support, entrances, plot containment and wake area before consuming equipment
or changing blocks. Support rearrangement within the owned plot without duplicating items. Land relocation/resale is later scope.
Persist fixture identities and positions; do not rely on generated `Scene.Points` to rediscover a player's new camp. Update dynamic
destinations safely for the dialogue worker, navigation, sleep, fire cooking and planting. A general item-data migration is not a
prerequisite: use the existing item/block registration path for the small initial set, preserving numeric ids and saved names.

### Interiors and world growth

Keep initial interiors in the same voxel world, with continuous movement through doors. Use authored layouts for the inn's rooms,
furniture and service positions; add reusable building primitives where they help later interiors. Check headroom, stairs,
collision, talk range, line of sight, light and interaction priority. Closing/locking doors and NPC schedules are optional follow-ups.

Lay out the expanded village around a central square, service streets, homes/gardens and surrounding fields/player land. Keep daily
services close and use longer routes for exploration. Reserve purchased areas against later authored expansion.

For the valley, benchmark a candidate such as 512 × 128 × 512 before choosing final dimensions. At unchanged height this is about
1.78 times the current chunk count, before extra props, lights and active water. Measure the live world plus pristine/save copies,
not only visible meshes. Retain the current coordinate origin and existing landmarks where possible; extending east/south is
simpler to migrate than recentering everything. Rework terrain relief and water routes deliberately rather than only changing
`size`. Streaming or a renderer rewrite needs its own evidence and scope if measurements show it is necessary.

### Persistence, content and language

Introduce optional, versioned system entries for lodging, land and camp state, with clear defaults for saves without them. Decide
whether the enclosing save format also needs a change after assessing the world-edit changes; do not reuse the adaptation
document's historical save-version allocation. Validate rights and transaction state independently of the voxel digest.

Extend migration before moving buildings or adding new placeable types: today's world-edit filter is insufficient for a furnished
camp. Preserve learning, inventory, existing structures, planted bushes, water state, active procedures and completed objectives.
An old mountain camp needs an explicit compatibility path, not a new purchase charge. Prefer retaining it as a recognised legacy
home; if terrain changes force relocation, preserve its contents and ownership through an explained migration. Test pending
selections, paid rooms, owned-but-empty land and partially established camps as well as finished homes.

New NPCs, places, phrases, events and definitions go through the existing schemas, `GameData` validation, phrase sheet, locale,
grammar and speech pipeline. Preserve existing teacher ids when changing displayed roles. Probe lodging and land requests through
the language core before relying on them; generic language-analysis gaps belong in separately scoped core work. Beds, prices and
ownership remain game concerns. New interactions offer support immediately, independently of whether a class has taught the words.

## 6. Validation and acceptance

During implementation, extend existing focused coverage rather than treating a successful screenshot as sufficient:

- **Progression and rest:** the fresh mountain-to-inn route, first night without land, optional classes, repeated nights, collapse,
  death before accommodation, room expiry and blocked home recovery. Reuse `Save/SleepTests`, `Needs/NightTests`, `Needs/DeathTests`,
  `Needs/DayOneSafetyTests` and `Language/Lessons/ClassTests` under [Game.Core.Tests](../../tests/Vocaterra.Game.Core.Tests).
- **Transactions and permissions:** no funds, changed or invalid selection, full inventory for equipment, duplicate confirmation,
  reload and cancellation; boundaries, slopes, roads, waterways and protected props; the guide's torch exercise before ownership.
- **Persistence:** exact same-bundle round trips; cross-bundle migration from representative supported saves; old mountain camps;
  runtime fixtures, planting and water; no duplicate recovery, equipment or payments. Extend `SaveGameTests` and `SaveMigrationTests`.
- **World and presentation:** several seeds, all service/lesson routes, bed and doorway access, safe spawn/wake points, bread-station
  interactions, keyboard-only placement and supported screen/text sizes. Extend scene/village/navigation tests and scripted runs.
- **Performance:** compare generation plus initial digest, peak memory, frame/mesh timings, water updates, save/load and first
  autosave against phase 0. The existing [SceneBuildTimeTests](../../tests/Vocaterra.Game.Core.Tests/World/Generation/SceneBuildTimeTests.cs)
  has a three-second build-plus-digest budget; preserve it or explicitly review a measured replacement before expanding the map.

Use `scripts/test.sh project <affected-project>` while iterating, the required full milestone gates before delivery, and
`scripts/content-loop.sh --grammar --speech` for bundle changes. The scripts already apply the repository's resource limits.
Rebaseline affected world/scripted results only after inspecting the intended differences. Keep a pilot's executable and bundle
fixed throughout its measurement period.

Playtest the first slice before broad expansion: can a beginner reach and use the inn, understand what buying land provides, find
several valid sites, afford one through ordinary play, establish a camp and return home without facilitator help? Record time to
the first bed, nights and activities before purchase, placement refusals, help used, route length and reasons for returning to the inn.

## 7. Choices settled in the review (2026-11-10)

"Decided" is the owner's answer, recorded in [D-game-32](decisions.md); "default" is the review's choice, which the owner
accepted with the plan and can change.

| Choice | Proposed starting point | Answer |
| --- | --- | --- |
| Inn price and access | First stay free; later private room paid per night, basic bunk available without coins. Define the exact expiry/reset point and recovery fallback. | **Decided:** the bed is free. Asking the innkeeper for a room gives lodging for good; no fee, no expiry, no bunk. With the same recovery for every bed the fee would have bought nothing. |
| First purchase | One fixed-size rectangle freely positioned in eligible outskirts; visible fixed price, starter camp equipment included, no upkeep. Tune against measured earnings. | **Default:** as proposed. The plot is marked with a stake the mayor gives for free; its size and price are data, set by Y00's measurements; the purchase includes one camp kit. |
| Mayor and keeper | Existing villager becomes mayor while retaining lessons/services; one new innkeeper with a reviewed voice. | **Default:** Tomás the villager becomes the mayor (alcalde). One innkeeper, with a provisional voice the owner hears afterwards. |
| Shelter benefit | Bed/tent access and normal night recovery first; comfort bonuses, weather and general room detection later. | As proposed. |
| Old worlds | Preserve the legacy mountain home and player work; define relocation only where retaining coordinates is impossible. | **Decided:** saves break (the owner's rule of 2026-11-04). Save version 8, older saves refused, no legacy camp, no migration work. |
| Village/map scale | Agree a layout and route targets, then measure candidate dimensions; 512 × 128 × 512 is a benchmark candidate, not a committed size. | **Default:** the first slice grows neither the map nor the village's disc; the inn takes the place of `house3`. The rest waits for the first-slice playtest. |
| Delivery order | Complete the inn-to-owned-camp slice before broad village/map expansion; reconcile the earlier pilot/land gates and register milestones after review. | **Decided:** build the first slice now on `feature/settlement`; `main` moves only by the owner's merge, so the bread pilot can run from `main` at any time. |

One choice the proposal did not list. **Decided, the building rule:** placing stays allowed in the wild (plank dams keep
working) and is refused inside protected regions (the village, the roads). Only owned land takes the camp kit, and only there
can the player take back planks they placed.

Full house construction and furnishing, general storage, farming, extra professions, taxes, multiple plots, land resale, frontier
claims, simulated NPC routines and weather effects are follow-up proposals. The initial slice should leave clear extension points
without requiring those systems to make the first campsite useful.

## 8. The first slice: the Y series (registered 2026-11-10, D-game-32)

**Branch** `feature/settlement`, cut from `origin/main` at `588e1536`, in the worktree `.claude/worktrees/settlement` (the owner,
2026-11-10: "Do the work on a new git worktree from main"). **Commit prefix** `Y<nn>:`; milestone branches `Y<nn>-<name>` are
gated into the feature branch and stay local. The letter is Y because every letter of "settlement", "inn", "land" and "camp" was
taken, and E went to the day, night and weather series the same evening.

### 8.1 What the review found

Checked against the code on 2026-11-10. Section 2's table holds; these are the points it misses or gets wrong, and each has an
owner (a decision or a milestone).

| # | Finding | Evidence | What follows |
| --- | --- | --- | --- |
| 1 | Sections 2, 5, 6 and 7 budget save migration and a "legacy mountain home"; the owner's rule is that saves may break | `implementation-plan.md` section 4.10 ("Old saves need not load"), owner 2026-11-04 | D-game-32 (d) |
| 2 | Land work was held "until the pilot's data" (W22 to W29) | `activities-plan.md`, stage 5 and decision (f) | D-game-32 (a) |
| 3 | "Enforce permissions" had no rule. Only wood (as planks) and torches can be placed, anywhere, and a placed plank can never be removed | `BlockDefinition.PlacedBy`; planks have no gather ticks | D-game-32 (c), Y09 |
| 4 | A sleep is a save restored in the same run, so any state that is not in the save is lost every night | `SaveGame.Sleep`, `VoxelGame.PassNight` | Lodging, the pending plot, ownership and the camp are all saved (Y02, Y09, Y12) |
| 5 | The economy cannot carry a price yet: no coins at the start; the one-off orders pay 2 and 4 coins and one costs 2; the repeatable income is one coin per wood, berry or bread sold | `game/host-events.json`, `game/objectives/` | Y00 measures and sets the price as data; the target is a plot the player can afford on day 2 or 3 without grinding |
| 6 | "Freely chosen land" meets dense forest: outside the village's clearing nine cells in ten hold a tree, trunks can be cut and leaves cannot, and the east side is mountainside | `scene.json` (`trees`, the relief's rows 9 to 19) | The eligible land is meadow north and south of the village, cleared by `trees.clearings` (Y04); a footprint with a tree in it is refused (Y09) |
| 7 | The day-one ending speaks in Luna's voice and can begin inside an open conversation; the closing card sleeps wherever the player stands | `Flow/SessionEndingFlow.cs`, `SleepOffer.OnCard` | Y01 (the card), Y07 (the voice and the timing) |
| 8 | The fire-ring finder reads the rings once (and there are two of it), and "my fire is lit" is a latch: a ring placed while the game runs is invisible to rest, cooking and waking | `Needs/FireRings.cs`, `VoxelSession.Eat.cs`, `VoxelSimulation.cs` | One shared finder with a rescan (Y01) |
| 9 | No NPC has stood indoors, and a named point has no height of its own | `SceneBuilder.AddPoint`, `FeetCell` | A ground-floor guest room; no stairs in this slice |
| 10 | The proposal's "selected recovery location" needs a way to choose; D-game-24 already says the player "wakes at the last fire they slept at" | [decisions](decisions.md), D-game-24 point 1 | Recovery is the last place slept; before any sleep, Luna's fire (Y01, Y02) |
| 11 | `needs.json`'s `death.wakeAt` is read and validated and then never used | `Needs/NeedsData.cs`, `VoxelSession.Death.cs` | Y01 makes it real: it names the sleep place the player wakes at before any sleep |
| 12 | A new NPC against "new NPCs only after a voice trial"; the three Spanish voices are all in use | `activities-plan.md` decision (e), `game/npcs/` | Y00's voice trial; a provisional voice, as D-game-22 (d) |
| 13 | The lodging and land words must pass the A2 gate and need lexicon readings, and the core may not read the requests («¿Tienes…?» reads as `state_have` today) | [open items](../open-items.md) section 4, the merge gate's content step | Y00's probe; a core gap becomes a core milestone before Y06 or Y11 |
| 14 | The valley phase's 512 × 512 candidate has 1.78 times the chunks, and the build-time test is already near its three seconds under load | `building-and-testing.md`, `docs/process/test-performance-plan.md` | Outside this slice; the budget is reviewed before that phase is registered |

Three things the proposal expected and the slice does not need: a new block shape for the bed (the "pile" shape of the loaves
and the apples is a low box already), a field that says which objectives end day one (the present rule fits once `light-fire`
leaves `session.json`), and scene places added while the game runs (the arrow needs only a cell).

### 8.2 The defaults behind the milestones

- **No one is locked out of sleep by Spanish.** The innkeeper offers the room when the talk opens; «sí» or asking for it takes
  it; «no sé» or two misses give it anyway (D-game-27's rule). A collapse still ends any day, and Luna's fire stays usable.
- **Day one ends** with the introduction, Luna's camp task and a room at the inn. Lighting your own fire moves to the campsite.
- **The mountain ledge** stays as the place the player arrives; its tent, chest and fire ring go.
- **One plot:** a fixed rectangle, its sides along the world's axes, marked with a stake. Marking again costs nothing.
- **The camp kit** is one item that places the tent, a bedroll and a fire ring as one piece, with a torch to light it.
- **The land steps** are one checklist, «Mi terreno», shown from day 2; it never holds sleep back.
- **The words** (decided after Y00, D-game-32 (e)): the inn is the `posada`, the plot the `terreno`, the mayor the `alcalde`,
  the stake the `estaca`. Each joins the level list and the bundle's lexicon (`lexicon/game.json`; the rule for such words is
  in `content/README.md`) in the milestone that first uses it: `posada` in Y04, `estaca` in Y10, `terreno` and `alcalde` in
  Y11, or in Y06 if the innkeeper's lines need one sooner.
- **The innkeeper's voice** (D-game-32 (f)): `pf_dora`, provisional; the owner listens after Y06 and may change it.

### 8.3 Milestones

| ID | What | Depends on | Size | Model | Status |
| --- | --- | --- | --- | --- | --- |
| Y00 | Brief and probes: the inn's place, the meadows, the plot's size and price, the language probe, the voice trial | none | S/M | Sonnet 5.5, medium | done, 2026-11-10 (documents only: section 9) |
| Y01 | Sleep places, with behaviour unchanged | none | M | Sonnet 5.5, medium | done, gated 2026-11-10 (10,714 tests) |
| Y02 | Save version 8 and the sleep entry | Y01 | M | Sonnet 5.5, medium | done, gated 2026-11-10 (10,719 tests) |
| Y03 | Bed and bedroll blocks | none | S | Haiku 5.5 | done, gated 2026-11-10 (10,734 tests) |
| Y04 | The inn building and the meadows | Y00, Y03 | M | Sonnet 5.5, medium | done, gated 2026-11-11 (10,749 tests) |
| Y05 | Lodging | Y02, Y04 | M | Sonnet 5.5, medium | todo |
| Y06 | The innkeeper: the NPC, her or his lines and voice | Y00, Y05 | M/L | Sonnet 5.5, high | todo |
| Y07 | The opening's logic | Y06 | M | Sonnet 5.5, medium | todo |
| Y08 | The mountain, the strings and the rebaseline | Y07 | M | Sonnet 5.5, medium | todo |
| Y09 | Land rules and protection | Y00, Y02, Y04 (the meadows' sites) | M/L | Sonnet 5.5, high | todo |
| Y10 | The stake and the plot's outline | Y09 | M | Sonnet 5.5, medium | todo |
| Y11 | Buying the plot from the mayor | Y10 | M/L | Sonnet 5.5, high | todo |
| Y12 | The campsite | Y03, Y11 | L | Sonnet 5.5, high | todo |
| Y13 | Close-out: the scripted runs, the documents, QA, the pull request | Y08, Y12 | S/M | Sonnet 5.5, medium | todo |

**Y00 Brief and probes.** No gameplay code. Its results are written into this document as section 9, "Layout and numbers".
- The inn: a ground-floor building of about 9 × 7 at the road's entrance, in place of `house3`, proved by a scene build. (No
  lesson names the houses, but Y00 found that the fisher's lesson counts them: «…y tres casas».) The meadows north and south of the village: at least three valid plots on seed 42 and on three others.
- The plot's size and price, from the coins a player holds on day 2 and day 3 (the orders and the sales).
- The language: the lodging and land lines through `tools/Vocaterra.Nlp.Cli -- eval`, and the words through `cefr-check` and the
  lexicon (posada, hostal or hotel; habitación; cama; terreno; alcalde; estaca). A core gap is written up as a proposed M milestone.
- Three candidate voices for the innkeeper, rendered for the owner.

**Y01 Sleep places.** The mountain fire becomes one authored sleep place, so nothing changes for the player and no save changes.
(The name is "sleep place" because `needs.json`'s `rest` is already the rule for resting by a lit fire.)
- `needs.json` gains a `sleepPlaces` list: for each place an id, a scene point, a kind (`fire` or `bed`), a radius and an access
  (`always` or `lodging`). `death.wakeAt`, unused until now, names the place the player wakes at before any sleep is recorded
  (the mountain fire until Y08, then Luna's `firering`). `Needs/NeedsData.cs`, `Needs/NeedsValidator.cs`, the schema.
- A `SleepPlaces` class the session owns, for the authored places and the ones made while the game runs. A fire is usable when it
  is lit (read live), a bed when its access is met. `CanSleep` means "a usable place exists", so the `--sleep` aid still works;
  `AtOwnFire` becomes `AtSleepPlace`.
- What changes: `Session/VoxelSession.Sleep.cs` (all of it); `Session/SleepOffer.cs` (the card needs the player at a usable
  place and otherwise points at the bed); `VoxelSession.Death.cs` (`WakeAtFire` becomes `WakeAtSleepPlace` and keeps
  `NearestFreeGround`); `VoxelSession.cs` (the `_fireLit` latch and `_playerFireRing`); the front end's prompt, card and
  `SleepNow` in `VoxelGame.cs`; `Input/KeyBindings.cs`.
- One `FireRings` shared by the session and the simulation, with a `Rescan()`.

**Y02 Save version 8.** The current and the oldest readable version both become 8 in `Save/SaveGame.cs`, and the branches for
old versions go. `SystemsSave` gains `Sleep` (the lodging given, the last place slept, the places made in play). "Last slept" is
written after `RestoreParts` inside `SaveGame.Sleep`, because a sleep restores the save taken just before it; a collapse writes
none. One version 8 fixture replaces `save-v1.json` and `save-v4.json`, and the tests that read them change with it.
[saves](saves.md) is updated.

**Y03 Bed and bedroll blocks.** Two ids appended in `Voxel/BlockId.cs`, their rows in `BlockDefinition` (not solid and not
decor, so the crosshair finds them), the pile shape in `Graphics/World/ChunkMesher.cs`, their art in
`Graphics/Blocks/BlockAtlasLayout.cs` and `BlockArt.cs`.

**Y04 The inn building and the meadows.** Every scene edit before Y08 lands here, so the world's digest is pinned again once.
The prop, the stamp, the cells, the meadows' sites and the tests that name `house3` are in section 9.1 and 9.2.
- **The village keeps three houses.** The fisher's lesson says «una tienda, una panadería y tres casas», so `house3` is not
  dropped but moved to a free place inside the disc. Only if no place takes it does the line change, with its recordings and
  its check, and then in Y08, not here.
- The word `posada` joins the level list and the lexicon, so that «¿Dónde está la posada?» finds the inn.
- `game/scene.json`: a `house` for the inn, then a `stamp` for its inside (beds, a counter, a partition with a door gap, window
  gaps through an `air` entry in the legend), the points `bed.inn` and `npc.innkeeper`, a lamp; `house3` goes; `trees.clearings`
  for the meadows.
- `game/needs.json`: the sleep place `inn.bed`, with the access `lodging` (so no one can use it before Y05).
- A walk test from the door to the bed, and screenshots of the inside and of the meadows for the owner.
- *As built (2026-11-11):* the counter is a row of `crate`, not `table` (a table in reach opens the crafting panel; a player can
  still step up onto the crate, which Y08 may stop); `house3` stands at dx 9, dz 13; eight windows, not six; `posada` went into
  the marked block of world words at the end of `cefr/a1.txt` (the A2 gate reads the lists cumulatively). The rest is the
  game's findings' Y04 entry.

**Y05 Lodging.** A `grant_lodging` action kind (`Npc/HostEventTable.cs`, the dispatch in `VoxelSession.cs`,
`content/schema/host-events.schema.json`); giving it twice changes nothing. The world event `lodging_granted`, which is also an
autosave reason. The grant kept in `systems.sleep`. The bed's prompt. Tested on a test bundle: given, saved, loaded; a night in
the bed is the same night as one at the fire; a death after it wakes at the inn.

**Y06 The innkeeper.** The seven steps of `content/README.md` ("Adding an NPC"): the agent, the prompts, the dialogue pools (the
offer, the room given, the goodnight line), `game/npcs/innkeeper.json`, `game/npcs.json`, the place in the scene, the locale's
role and glosses, the figure in `Graphics/Figures/NpcFigures.cs`, the entry in `game/host-events.json`, the rows in
`content/PHRASES.md` and `game/phrases.json`, the provenance, the speech.

**Y07 The opening's logic.**
- The objective `inn-room` (its trigger `lodging_granted`) joins `game/session.json` and `light-fire` leaves it. The hooks
  `spare-torch`, `point-own-camp` and `scan-fire-ring` retire (`ObjectiveBook`, `VoxelSession.cs`, `GameDataValidator`); a
  `point-inn` hook follows the camp task.
- The ending takes its speaker from the closing pool's NPC and waits for the conversation to close.
- `VoxelSession.Skip.cs` gives lodging where it lit the ring; the texts of `PlaytestFlow` and `PlaytestEnd`; Luna's line that
  sends the player to the inn, with its speech.

**Y08 The mountain, the strings and the rebaseline.** The tent, the chest, the crate and `firering.player` leave `scene.json`,
and `camp.player` loses the label "mi campamento"; the mountain's sleep place goes, and `death.wakeAt` names Luna's fire. The console's sleep line says
"at the fire" wherever the player slept: it names the place. The locale's `tips.json`, `ui.json` and
`controls.json`, `words.json`'s `hud.camp`, `Sound/AmbientBeds.cs`. The tests that name `firering.player` (seven files, with
`AmbientBedsTests` and `TestingRowsTests`). The digest pinned again. The scripted runs r01, r10, r20 to r24, r26, r27 and r30
captured again after their differences are read. [lessons](lessons.md), [running](running.md), [playtest](playtest.md).

**Y09 Land rules and protection.** It stays out of `scene.json`.
- `game/land.json` with its schema, its load in `Content/GameData.cs`, a `CheckLand` in the validator and a row in
  `content/README.md`: the protected sites by their scene names with a margin, the path and bridge blocks with a margin, the
  eligible regions, the plot's width and depth, its price, the slope it allows.
- `LandRules` (pure, in Core): a mask per column built when the session starts; `PlotCheck` answers yes or gives the reason
  (protected, outside the meadows, too steep, water, a tree).
- A rules hook on `VoxelSimulation` (as `WalkPermille` is) used by `TryPlace`: planks are refused on protected columns with a
  hint; a torch is always allowed, so the exercise at Luna's ring is untouched. Planks can be gathered inside the owned plot.
  `systems.land` (the pending plot and the owned one).

**Y10 The stake and the plot's outline.** The item `stake` (`Items/ItemDefinition.cs`, its noun, its icon). A use-item hook in
`Input/WorldInput.cs` (as `PlantOn` is) marks the pending plot round the ground cell aimed at; a refusal says why on the hint
line; the pending plot is saved. `OverlayMeshes.PlotOutline`, drawn through `SceneDrawer.DrawWorld` as corner posts and an edge
line, with a HUD line that gives the size and the price (never by colour alone). A launch flag for the scripted runs.

**Y11 Buying the plot.**
- `villager.give_stake` (the `give_item` kind that exists). A `sell_plot` kind whose misses are `no_plot`, `cant_pay`, `no_room`
  and `already_owned`, after the pre-checks of `Npc/NpcHostEvents.cs` and `NpcService.Miss`, with the validator's miss states;
  the plot's facts reach the worker in `GuideScene`.
- One exchange that cannot half happen (`Inventory.CanExchange`): the coins out, the ownership in, the camp kit given.
- The content: the mayor's title (`game/npcs/villager.json`, `npc.villager.role`), his lines for the stake and the sale, the
  rows in `PHRASES.md`, the speech. The checklist `my-land` («Mi terreno», `"session": 2`) with the steps `stake` and `buy`; the
  arrow to the plot.

**Y12 The campsite.**
- The item `camp-kit` and one camp shape (the `tent` stamp that exists, a bedroll, a fire ring). The stamp's rotation moves out
  of `SceneBuilder.BuildStamp` into a helper, so the scene's build and the placing in play put blocks down the same way.
- Placing checks every cell first (inside the owned plot, free, on level ground, clear of the player, a free cell to wake on),
  then sets the blocks, takes the kit, scans the fire rings again and makes the bedroll and the ring sleep places.
- The torch hook in `VoxelSession.cs` is widened to the ring that was placed; planting is allowed inside the plot
  (`VoxelSession.Planting.cs`); `my-land` gains the steps `camp` and `fire`.
- Gathering a camp block on owned land packs the whole camp back into the kit (the first thing to cut if the milestone runs
  large). A load drops a sleep place whose blocks are gone and gives the kit back.

**Y13 Close-out.** The scripted runs `r40-inn`, `r41-land` and `r42-camp`; the documents ([saves](saves.md),
[architecture](architecture.md), [running](running.md), `content/README.md`, the fixture's line in `CLAUDE.md`, the list of
series in `docs/process/documentation.md`); the QA agent on progression, world and save; the open items and the handover; the
pull request from `feature/settlement` into `main` for the owner. The gaps it records: no fire sound at a placed camp, «mi
campamento» not answered when asked for directions, one plot only, no meals at the inn.

### 8.4 Order, and the series beside it

At most two building agents on the machine. Suggested pairs: (Y00, Y01), (Y02, Y03), Y04, (Y05, Y09), (Y06, Y10), (Y07, Y11),
Y08, Y12, Y13 (Y09 names the meadows' sites, which Y04 adds). While the weather session builds, the series runs one agent at a
time, in that order. Two rules: the milestones that edit `scene.json` (Y04, Y08) never overlap, and milestones that edit the bundle
are gated one at a time, the second hashing again after it has merged the first.

**The day, night and weather series (E, `feature/weather`, D-game-31) is being built at the same time**, from the same `main`.
It adds a free-running day and night clock, a `systems.atmosphere` save entry and a sleep that wakes in the morning; it leaves
sleep's rule alone and gives the weather no effect on play. What that means here:

- The two sessions share the machine: the limit of two building agents counts both.
- Both branches edit `Save/SaveDocument.cs`, `Save/SaveGame.cs`, `VoxelSession.Night.cs`, `Content/GameData.cs`, the save
  fixtures and the bundle's manifest. The second pull request to reach `main` merges `main` first and records again. Before
  Y02's gate, merge `main` into `feature/settlement` if the day and night part has landed.
- A night is still a sleep. The bed is free, so nothing here counts the clock's nights.
- E06 draws no rain under a roof or a tree, so the inn and the tent shelter from the rain with no work here. Wetness, warmth
  and comfort stay later work in both plans.

### 8.5 Verification

- **Each milestone:** `scripts/test.sh project <the project it touches>` while working, then
  `GATE_TARGET=feature/settlement scripts/merge-gate.sh "Y<nn>:<branch>"` from the feature branch's worktree (the build with
  warnings as errors, the whole suite, the format check, the A2 content gate, the grammar check, the licence scan).
- **Bundle edits** (Y04, Y06 to Y08, Y11, Y12): `scripts/content-loop.sh --grammar --speech`; the goldens recorded again only if
  they moved; the new bundle hash in the save fixture.
- **Scene edits** (Y04, Y08): the digest in `SceneTests` pinned again after the change is looked at; `SceneBuildTimeTests` still
  under three seconds; screenshots by `--teleport`, `--look` and `--screenshot`.
- **Unchanged behaviour:** Y01 leaves the scripted runs r20, r26 and r30 identical (`scripts/compare-runs.py`); Y02 changes only
  the save files.
- **End to end at Y13,** a fresh game on seed 42: wake on the ledge; Luna's introduction and camp task; the arrow to the inn; the
  room by speech, by typing and by the help route; a night in the bed; day 2's orders and sales; the stake from Tomás; a refusal
  in the village, on the road and on a slope, then a valid plot; the purchase (and with too few coins, with a full bag, and a
  second time); the kit placed; the fire lit; a night at the camp; a death that wakes at the camp; the camp packed and placed
  again; a load at every stage; the inn's bed still usable; a version 7 save refused with a clear message. The plot check
  repeated on three other seeds.
- **Then** the QA agent's reports are triaged, and the owner plays the slice with the measures of section 6.

### 8.6 After the slice

Phases 6 and 7 of section 4 (the larger village with more interiors, the larger valley) are registered after the first-slice
playtest. The valley phase begins with the 512 × 512 benchmark and a review of the three-second build budget.

## 9. Layout and numbers (Y00, measured 2026-11-10)

Measured on `feature/settlement` at `e3e62ee7` with probes kept outside the repository (a console project over `SceneLayout.Parse` and `SceneBuilder.Build`;
the core's `eval` through a wrapper; the game with `--scene`), seeds 42, 7, 1234 and 2025. Blocks: x east, z south; "village" is (292, 218).

### 9.1 The inn

**9 x 7 fits** at village dx -16, dz 8 (x 276 to 284, z 226 to 232), the door in the north wall facing the road: the corners are 17.9, 11.3, 21.3 and 16.1 blocks
from the village centre (the disc is 24). x 275 to 283 was refused (the west wall would stand on the road block (275, 226)). Replaces "about 9 x 7".

| Clears | Measured on the built scene (dump of layers y 37 to 40) |
| --- | --- |
| prop `{"type":"house","site":"village","dx":-16,"dz":8,"width":9,"depth":7,"height":3,"facing":"north","name":"inn","label":"posada"}` | the stamp below goes after it |
| road | via points (270, 226) and (284, 222) are 6 and 4 blocks off; road blocks run at z 224 (x 272 to 282) and z 225 (x 268 to 278) |
| signpost (278, 221), paving (x 284 to 299, z 212 to 223), well (295 to 297, 228 to 230), fences (z 225, x 284 to 290 and 293 to 299) | 5, 3 and 11 blocks away; the fence touches the north-east corner diagonally only |
| `lamp.house3` at (284, 226) | stands on the corner: becomes `lamp.inn`, dx -10, dz 7 |

**What names `house3` or stands on its place** (x 279 to 283, z 225 to 229):

| Where | If it goes |
| --- | --- |
| `scene.json` lines 185 and 198 (the house, `lamp.house3`) | replaced by the inn and `lamp.inn` |
| `SceneTests.cs` 201 and 272, `VillageTests.cs` 113 (a 5 x 5 timber test), `ScenePlacesTests.cs` 27 (nearest "casa"); `src/Vocaterra.Game/README.md` 40 and `LaunchOptions.cs` 94 (`--beacon` help) | each names `house3`: edit them |
| `SceneTests.GoldenDigest` (`b699324f...`; the scratch scene builds to `b16b1788...`) and the end-to-end goldens | pinned again, once, in Y04 |
| the fisher's line «...una tienda, una panadería y tres casas.» (`dialogue.json` 921, `prompts.json` 320, `phrases.json` 60, `checks.json` 399, `PHRASES.md` 164, three locale files, four `speech-index.json` rows) | **becomes false** (two houses): "no lesson names the houses" is right about names, wrong about the count; Y04 or Y08 changes the line, its WAVs and the check |
| `scripts/scripted-runs.sh`, `scripted-runs.md`, `clearing-route.json` | nothing falls on or beside the inn (nearest: r19, r20 and r30 at (282 to 283, 206 to 212), 15 blocks north) |

**Interior**, a stamp placed after the house (same corner, facing north; cell = (col, row) from the north-west corner, col 0 = x 276):

```json
"inn_rooms": { "legend": { "W": "wall", "T": "table", "C": "crate", "a": "air" }, "layers": [
  ["         ", "     T   ", "     T   ", " WWW WWW ", "         ", " C     C ", "         "],
  ["  a   a  ", "         ", "a       a", " WWW WWW ", "a       a", "         ", "   a a   "],
  ["         ", "         ", "         ", " WWWWWWW ", "         ", "         ", "         "] ] }
```

Reception in rows 1 and 2 (the door (280, 226) is not solid; the counter `T` at col 5; the keeper's cell is cols 6 and 7), a partition on row 3 with a gap at col 4 on the door's axis (3 high, lintel),
the guest room in rows 4 and 5 with a `crate` per bed, six one-block windows at eye height; interior 7 x 5 under a 3-high ceiling.

| Cell (x, z) | Note |
| --- | --- |
| the player talks from (280, 227), facing east | 2.04 blocks to the keeper's body; `NpcAim` reaches 8 and 0.8 round the body (feet + 1.2) |
| innkeeper (282, 227), `"dx":-10,"dz":9,"facing":"west"` | feet y 38, behind the counter, enclosed by counter, wall and partition |
| the bed the player sleeps at: crate (277, 231); `bed.inn` = stand cell (278, 231) | a `point` has no height; second bed (283, 231), stand (282, 231); the point `inn` (280, 229) is the gap |

**Proved in scratch copies** (talk is `FigureDirector.Aimed`: the crosshair within `NpcAim.ReachBlocks` and `LineOfSight.Blocked` finding no solid block eye to body): the scene builds; a stamp inside a house's footprint builds (`FlatHeight` accepts it, later puts overwrite the house); the legend `"air"` clears wall cells
(`BlockDefinition.FromName("air")` exists: no parser change); the NPC gets a valid cell; the ray from (280, 227) to the keeper is clear (the counter is one block
high) and is blocked from the street, the bed and the gap's far side, so no one talks through a wall; the keeper's cell cannot be entered; `--walk-to` goes from (280, 221)
through the door and the gap to (278, 231) (77, 28 and 46 ticks), and a column search gives 7.4 blocks from the street to the bed. Screenshots (scratch scene, the shopkeeper
stands in for the keeper), kept in `artifacts/y00/` of the feature branch's worktree (git-ignored): `inn-outside.png`, `inn-inside-counter.png`
(the "E: talk" prompt on the keeper), `inn-inside-beds.png`.

### 9.2 The meadows and the plot

**Probe.** An origin is valid when every footprint column has grass or dirt on top, no water, no trunk or leaves in the 24 blocks above, heights within the slope, and lies outside
the village's disc 24 + blend 8 + 2 = 34, every `path` and `bridge` block + 2, Luna's disc 12 + 2. A region counts origins whose centre is within 20 blocks of its centre; "fit" is
the most non-overlapping plots a greedy pick in six orders finds (a lower bound). **Clearings.** `trees.clearings` takes `site` (a name in `sites`; a `points` entry is refused), `radius` (required), `fade` (default 16), `keep` (percent kept, default 0).
A new named site is needed, and **a site with no radius levels nothing** (the terrain pass skips radius 0): `"meadow.north": {"x":270,"z":180}` is enough.

Origins / fit, slope at most 1, north · south, cleared = two clearings of radius 22, fade 12:

| Footprint | Scene | 42 | 7 | 1234 | 2025 |
| --- | --- | --- | --- | --- | --- |
| 10 x 10 | as is | 0/0 · 24/1 | 10/1 · 7/1 | 11/1 · 20/1 | 0/0 · 5/1 |
| 10 x 10 | cleared, keep 0 | 600/9 · 550/9 | 469/8 · 201/5 | 656/9 · 160/4 | 529/8 · 410/8 |
| 10 x 10 | cleared, keep 5 | 120/5 · 363/7 | 150/5 · 171/4 | 341/7 · 36/3 | 60/2 · 133/5 |
| 12 x 12 | cleared, keep 0 | 463/6 · 436/5 | 336/6 · 107/3 | 547/6 · 79/2 | 406/5 · 281/4 |

As is, 12 x 12 and 16 x 16 have none (whole map: 185 to 294 origins of 10 x 10, 25 to 54 of 12 x 12, none near the village). At slope 2 (keep 0): 12 x 12 fits 6, 6, 6, 5
north and 5, 3, 3, 4 south; 16 x 16 fits 3, 2, 3, 3 and 3, 1, 1, 2 (slope 1: 2, 2, 3, 3 and 2, 1, 0, 2).

**Recommendation.** Plot **10 x 10, slope at most 1, `keep` 0** (three plots fit in both meadows on all four seeds; 12 x 12 needs slope 2 in the south, 16 x 16 never fits).
Meadows: **north** dx -22, dz -38 = (270, 180); **south** dx -28, dz 40 = (264, 258); radius 22, fade 12 (a scan for the most plots; the south is poorer, the ground rises east of
x 304). Margins 34, 2 and 14 as probed. Replaces "meadows north and south" (no centres) and "a fixed rectangle" (no size). Walking from the inn's door (280, 225), a column search
(steps of 1, diagonals 1.41; the same on every seed): north centre **49** blocks, south centre **43**, Tomás at (293, 221) **15**.

### 9.3 The price

From `host-events.json`, `objectives/*.json`, `ItemDefinition.cs`. No coins at the start; an order opens at the first talk with its NPC (D-game-25), once.

| Way | NPC | Coins | The learner says | Limit |
| --- | --- | --- | --- | --- |
| order `fruit-for-mar` | Mar | +2 | «sí»; 3 berries | once |
| order `oven-wood` | Luz | +4 | «sí»; 5 wood | once |
| order `axe-handle` | Domingo | -2 and 2 wood (the axe); `water-for-plants` pays 3 apples, 0 coins | «sí»; wood and coins | once |
| sell wood | Luz, Domingo | +1 each | «vendo madera» (all in the bag), «vendo diez maderas» | none |
| sell berries, bread; buy bread 2, flour 1, apples 1 | León, Pablo; Luz, Pablo, León | +1 each; -2, -1, -1 | «vendo frutas», «quiero dos panes» | none; 5 berry bushes |

«no sé» or two misses also hand a good over (D-game-27). A coin stack holds 99 (40 slots). A trunk by hand takes 60 ticks (1 s); a tree has 4, the orchard 3 trees. Assumed: the
orders are taken and delivered on day 2 (day 1's camp task already carries 5 wood and 3 berries); nothing bought but the axe.
Coins held: the orders only, **6** at the end of day 2 and of day 3 (4 with the axe); the orders and ten wood sold, **16** (14 with the axe), and **26** on day 3 if ten more are sold.
**Recommendation: 10 coins** («diez monedas», A1): the two orders and 4 wood in one «vendo madera» (6 if the axe was taken), about a minute of cutting, on day 2. Orders alone
(6) fall short and the axe order would lock them out; the stake is free. Replaces "tune against measured earnings".

### 9.4 The language probe

`eval --transcript <line> --prompt <id>` on `content/game-v1`: the shopkeeper's `stall-need`, the baker's `baker-open` (same results), the villager's `villager-open`, `stall-offer` for
yes or no. `errors` were empty and correctness 1 unless noted; `u` = `unknown`, not communicated.

| Line | `stall-need`, `baker-open` | `villager-open` |
| --- | --- | --- |
| Quiero una habitación, por favor. / Una habitación, por favor. / Quiero una cama. | `request_item` {habitación}, {cama} | same |
| ¿Tiene / ¿Tienes una habitación? | `state_have` {habitación} (a statement) | u |
| ¿Hay una habitación? | `ask_availability` | u |
| Quiero dormir aquí. / ¿Puedo dormir aquí? | u | u |
| ¿Cuánto cuesta? / ¿Cuánto cuesta el terreno? | `ask_price` / {terreno} | u |
| Quiero comprar un terreno / esta tierra | `state_action` {terreno} / {tierra} | same |
| Quiero este terreno. | u | u |
| Quiero un terreno, por favor. / Quiero la tierra. | `request_item` {terreno} / {tierra} | not run |
| ¿Me das una estaca? / Quiero una parcela. | u, `vocab.unknown` (correctness 0.6) | same |
| sí / no sé | u, `confirm` at `stall-offer`; `dont_know` | u; `dont_know` |
| Buenas noches. / ¿Dónde está el hotel? / ¿Dónde está la posada? | `greet`; not run | not run; `ask_directions` {hotel}; u, `vocab.unknown` |

| Word | Gate (`cefr-check --level a2`) and lexicon | Word | Gate and lexicon |
| --- | --- | --- | --- |
| hotel, habitación, cama, llave, noche, dormir, campamento, comprar, vender, precio | A1; hotel m, habitación f, cama f, llave f, noche f | tierra, cuarto | A2; f, m |
| terreno | **not listed**; m (and adj) | alcalde | **not listed**; m and f (no `alcaldesa`) |
| posada, hostal, pensión, parcela, estaca | **not listed; no lexicon entry** | tienda de campaña, saco de dormir | `campaña` **not listed**, f (`tienda` is the stall's label); `saco` reads as `sacar`, passes |

**Recommendation.** Inn **posada** (A2 list entry and lexicon entry, f; Y06) with **habitación** and **cama** (`hotel` passes today); plot **terreno** (list entry; lexicon m) or
**tierra** (passes now); mayor **alcalde** (list entry). Player lines, all read today: «Quiero una habitación, por favor.», «¿Hay una habitación?» (lodging); «Quiero un terreno, por
favor.», «¿Cuánto cuesta el terreno?» (land). Section 8's "posada, hostal or hotel" becomes posada. **Content alone**, after the shopkeeper and the baker: the agent (an idle state and an offer state on a yes-or-no prompt like `stall-offer`), prompts, pools, `game/npcs` file with
`patience` (the shopkeeper's block) for «no sé» or two misses, the voice, the cell. Code: the `grant_lodging` and `sell_plot` host kinds (Y05, Y11) and the figure `innkeeper` (grey until drawn).

**Core gaps** (none patched; an M milestone only if the owner wants the wording).
- «¿Tiene / Tienes una habitación?»: the core answers `state_have` {habitación}, the agent «¡Qué bien!»; missing: `tener` in a question read as `ask_availability`, as «¿Hay…?» is. Y06 can use «¿Hay…?».
- «Quiero dormir aquí.», «¿Puedo dormir aquí?»: `unknown`, no tag, at every prompt tried; missing: `state_want` with an infinitive (today an item only: «Quiero una habitación para una noche.» at `smith-open`) and a «¿Puedo…?» intent. Y06 does not wait.
- «Quiero comprar un terreno.»: `state_action` {terreno}, the verb `comprar` is in no slot; «Quiero este terreno.» is `unknown`; missing: a verb slot or a purchase intent, `este`/`esta` in `request_item`. Y11 uses `request_item` and `ask_price`. «¿Me das una estaca?» fails only for want of a lexicon entry (content, Y06 and Y10).

### 9.5 The voice trial

In use (`game/npcs/<id>.json`, `voice` {`speaker`, `name`, `speed`, `gain`}): `ef_dora` 28 Luna, `em_alex` 29 Pablo, `em_santa` 53 Tomás and Mar, `if_sara` 35 Luz, `pm_santa` 44 León,
`im_nicola` 36 Domingo: five of seven are men by voice. Unused: `pf_dora` 42, `ff_siwis` 30, `pm_alex` 43 (the old trial: git `a53aa866`).

| Candidate | Sex | Why | RMS |
| --- | --- | --- | --- |
| `pf_dora` 42 | woman | nearest to native Spanish of the unused; probably Luna's timbre, so the two may sound alike | 1800 |
| `ff_siwis` 30 | woman | a timbre no one has; French accent; louder (gain about 0.6) | 3200 |
| `pm_alex` 43 | man | probably Pablo's timbre; a sixth man | 2000 |

Propose **a woman** (two of seven today): `ff_siwis` to be distinct, `pf_dora` to be nearer Spanish. Rendered with `SpeechRender --trial` at 24 kHz mono (the speech folder's rate) into
`/home/james/Dev/Vocaterra/.claude/worktrees/settlement/artifacts/voice-trial/innkeeper/`: `<speaker>-hola.wav` («¡Hola! ¿Quieres una habitación?») and `<speaker>-buenas-noches.wav`, with `trial.json`.

### 9.6 What Y04, Y06, Y09 and Y11 take from this

- **Y04:** the prop, stamp and `lamp.inn` of 9.1 (drop `house3`); points `bed.inn` (278, 231) and `npc.innkeeper`; sites `meadow.north` and `meadow.south` with clearings radius 22, fade 12, keep 0;
  the «tres casas» line, the tests and the digest of 9.1; screenshots as in 9.1.
- **Y06:** the word posada with list and lexicon entries (also alcalde, terreno, estaca); «Quiero una habitación, por favor.», «¿Hay una habitación?»; `patience`; a voice from 9.5; the figure.
- **Y09:** `land.json`: 10 x 10, slope 1, price 10; protected 34 round the village, 2 round paths and bridges, 14 round Luna's camp; regions the two discs of radius 22 (a tree in the footprint refuses it).
- **Y11:** price 10 (the orders alone give 6); `terreno` or `tierra`, `alcalde`; `request_item`, `ask_price`, `confirm` read, the verb of «Quiero comprar…» is lost and «Quiero este terreno» unread; `sell_plot` is code.
