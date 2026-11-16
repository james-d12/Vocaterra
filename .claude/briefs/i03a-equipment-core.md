# Brief: I03a, the equipment core

Sent 2026-11-16 to a general-purpose agent, Sonnet 5.5 at high effort. Worktree `/home/james/Dev/Vocaterra/.claude/worktrees/i03a`,
branch `I03a-equipment-core`, cut from `feature/inventory` after I06's gate. The shared part is `.claude/briefs/i-series-standard.md`.

```
You are implementing I03a: the equipment's logic in Vocaterra.Game.Core (the inventory plan's sections 3.4 and 3.5, and 3.1's
principles; D-game-35 items 2, 6, 7, 8). No panel, no drawing, no shipped gear item: those are I04, I09 and I08a. Your work is the model,
its rules, its commands and the stat sheet, proved with test items.

Today: items are data since I01 (`content/game-v1/game/items.json`, `content/schema/items.schema.json`, `Items/IItemCatalogue`,
`ItemCatalogue`, `ItemDefinition`); the bag is `Items/Inventory.cs` (40 slots, 10 the hotbar); inventory edits that change the world
go through `Simulation/VoxelCommand` (a kind and four ints) applied in the 60 Hz tick (`VoxelSimulation.cs`), so scripted runs replay
them and the state digest covers them. I06 added `Temperature/` with `IWarmthSource` and its zero `NoWarmth`, used in
`Session/VoxelSession.Temperature.cs` (`PlayerTemperature`): your stat sheet's `warmth` replaces the zero there.

What to build:
1. `content/game-v1/game/equipment.json` and `content/schema/equipment.schema.json`: the slot groups of the plan's section 3.4 table, each
   with an id, a layer (or none), its slots (the body slots `head`, `body`, `legs`, `feet`; or a count), the item tags it accepts, a
   `noDuplicates` flag (accessories), a `statsApply` flag (false for cosmetic) and a Spanish label noun key per slot. All six groups go in
   the data (clothing, armour, cosmetic, accessories 3, off-hand 1, backpack 1). Equipment code must not name a group's id: a seventh group
   is a data row.
2. Item fields (items.schema.json and ItemDefinition, optional, absent on today's 14 items): `wear` (the slot it fits: head, body, legs,
   feet, accessory, offhand, back), `stats` (an object of open stat keys to numbers), `look` (colours per body part for I09: parsed and
   validated only, nothing drawn). The validator checks that a `wear` item has a tag some group accepts, that stat keys are well formed,
   and that colours parse.
3. `Items/Equipment/` (or the folder the code guidelines suggest): `Equipment` (the worn items by group and slot, each slot one stack of
   one), and the equip rules in one place: the slot's group accepts the item's tags; the item's `wear` matches the slot; no duplicate
   accessory; one item per slot. A refused equip or unequip returns a reason (an enum or code the panel will show later) and leaves the
   bag and the equipment exactly as they were. The backpack slot accepts a `bag` item, but the bag's capacity and the rule that a bag with
   items in its slots cannot come off are I03b's: leave a clear seam for it.
4. Commands: equip (from a bag slot to an equipment slot, swapping with what is worn there) and unequip (to a given bag slot or the first
   free one; refused when the bag is full), as `VoxelCommandKind`s applied in the tick. The equipment is simulation state: it joins the
   state and the digest so that, with nothing worn, today's digests and every scripted run's baseline stay exactly as they are (verify).
   Do not change the save's shape (I03b takes the next save version with the bag); say what happens to worn items on save and load until then
   (they may be lost; no shipped item can be worn yet).
5. Stats: `StatSheet` (read-only values by stat key), `StatBuilder` (sums and factors per key), `IStatContributor` (adds to a builder);
   the equipment is the first contributor, counting only groups with `statsApply` (so cosmetic items give nothing). The sheet is rebuilt
   each tick (or on change; your choice, deterministic either way). Stat keys are open strings; the units of `warmth` are I06's tenths of
   a degree. A later contributor (a meal's buff, W15) must be a new class and its registration, with no edit to the sheet or its readers:
   prove it with a test contributor.
6. Warmth wired: an `IWarmthSource` reading the sheet's `warmth` replaces `NoWarmth` in `VoxelSession.Temperature`. The other stat keys'
   consumers (walk speed, drains, stamina, light, armour) are I05a's and I05b's: do not wire them.
7. A start aid for scripted runs and pictures later: `--equip <item>` (repeatable; gives the item and wears it in the slot its `wear` names,
   refused with a message if it cannot be worn), following `--give` in src/Vocaterra.Game/Startup/LaunchOptions.cs and the scripted-step
   pattern if there is one for `--give`.

Tests (Given_When_Then, folders that mirror the source), with test items from a test catalogue (follow I01's `TestItems` helper): each
equip rule and its refusal with nothing changed; swap on equip; unequip to a chosen slot, to the first free slot, refused when full; the
no-duplicate accessory rule; cosmetic gives no stats; the sheet sums two contributors; a new contributor needs no edit; warmth from a worn
test item raises the feeling in PlayerTemperature; the equipment schema and validator refusals (unknown tag, bad slot, duplicate group
id); two runs of one seed and one command list with equips give the same digest; a run with nothing worn gives today's digest.

Content: equipment.json and the schema edits are in content/game-v1 and content/schema, so run the content loop (content/README.md),
and the new bundle hash goes into save-v9.json. No new Spanish beyond the slot label noun keys; if a label noun is not yet in
`game/nouns.json` and the lexicon, reference the existing nouns (`cabeza`, `cuerpo`, `pierna`, `pie`) and leave the labels the lexicon
lacks for I04 (say which).

Documents: docs/game/architecture.md (the Equipment and Stats folders, how to add a slot group, a stat key, a contributor), a short
paragraph in content/README.md on equipment.json and the new item fields.

Out of scope: the bag's size and capacity, the off-hand's light and the shield's armour (stat consumers), death, the save, the panel, the
avatar, shipped gear items, click handling.

The gate is the standard brief's (the count after I06 is in your prompt). Report: the standard one page, plus the reason codes, the
command kinds and their argument layout, what happens to worn items on save today, and every existing test changed and why.
```
