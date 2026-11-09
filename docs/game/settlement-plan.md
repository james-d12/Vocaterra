# Inn, player land and settlement expansion

High-level implementation proposal for review, based on `main` at `0a780dbf` (9 November 2026). This document plans future work;
it changes no gameplay and registers no delivery milestones. Once reviewed, record accepted decisions in [decisions](decisions.md)
and implementation milestones in the [game plan](implementation-plan.md).

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
claim system or W24's general storage system. Its adoption would amend D-game-15's owned mountain camp, D-game-22's inward-only
village growth, the day-one ending under D-game-25, and the W22/W23 scope and scheduling assumptions. Pilot scheduling and any
active content freeze must be reconciled before changing the shared bundle; this proposal does not silently override those gates.

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

## 7. Choices to settle during review

| Choice | Proposed starting point |
| --- | --- |
| Inn price and access | First stay free; later private room paid per night, basic bunk available without coins. Define the exact expiry/reset point and recovery fallback. |
| First purchase | One fixed-size rectangle freely positioned in eligible outskirts; visible fixed price, starter camp equipment included, no upkeep. Tune against measured earnings. |
| Mayor and keeper | Existing villager becomes mayor while retaining lessons/services; one new innkeeper with a reviewed voice. |
| Shelter benefit | Bed/tent access and normal night recovery first; comfort bonuses, weather and general room detection later. |
| Old worlds | Preserve the legacy mountain home and player work; define relocation only where retaining coordinates is impossible. |
| Village/map scale | Agree a layout and route targets, then measure candidate dimensions; 512 × 128 × 512 is a benchmark candidate, not a committed size. |
| Delivery order | Complete the inn-to-owned-camp slice before broad village/map expansion; reconcile the earlier pilot/land gates and register milestones after review. |

Full house construction and furnishing, general storage, farming, extra professions, taxes, multiple plots, land resale, frontier
claims, simulated NPC routines and weather are follow-up proposals. The initial slice should leave clear extension points without
requiring those systems to make the first campsite useful.
