# I07 report (durability and repair)

The agent's report, saved by the orchestrator (2026-11-19). Sonnet 5.5 at medium effort; brief `i07-durability.md`. Gated at `75b2494f`:
13,790 passed, 0 failed, 9 skipped (+131); timing 23; format clean; A2 lint clean; content loop clean.

Branch `I07-durability`: ac0bb687 (core, content, save, tests), 324bf986 (wear bar, crafting panel, launch options), 1d9c4a9e (docs).

**Model.** `ItemStack(Item, Count, Wear=0)`; every `InventoryOps` path keeps it; `ItemStack.CanMergeWith` (same item and wear) the one merge
test. `items.json` `durability: {max, source, rate}` (the row names its source), schema, `WearValidator`. Sources in `Items/Wearing/`
(`WearSources.Standard()`): `use` (one per block gathered with the selected tool, in `VoxelSimulation.StepGather`, only where the tool
works), `time` (one per in-game hour worn: 1/24 of the day's ticks; sleep wears nothing), `damage` (`WearDamageModifier` id `wear` in the
I05a pipeline, listed before `armour`; drowning lists none: a test cause proves it). Worn out (wear = max): never lost; a tool at bare
hands' speed; worn gear gives no stats; a bag keeps its slots. `ToolWorn` / `GearWornOut` once; world event `tool_worn`; HUD line
`wear.toolWorn` «Necesito arreglar la herramienta.» (I need to repair the tool.) through the needs HUD (only with needs on). Repair: 21
`recipes.json` rows with `"repairs": true` at mesa, one material each; the most worn stack goes to 0; `CraftResult.NothingToRepair`,
`ItemRepaired`, `item_repaired`; repair rows hidden until something of the item is worn; the panel draws by place; `ui.craftRepair`
"Repair". The torch (stacks to 99) has no durability. Wear bar: `WearBar.Of` (Core), `UI/Common/WearBarLayout`, drawn under icons in the
hotbar, off-hand cell, bag and equipment cells.

**Numbers proposed:** axe 200 gathers (use); clothes 240 hours; ring, bracelet, belt, necklace 480, amulet 720; sack 480, backpack 720;
lantern 240; armour by damage, 1 per point: helmet 150, cuirass 300, greaves 200, boots 150, shield 250. Repair: axe 1 madera; jacket, coat,
backpack 2 tela; cuirass, greaves 2 hierro; the rest one of tela, cuero or hierro.

**Save:** version 11, the only one read; `wear` optional on a slot and a worn entry (left out at 0, clamped on load); the digest appends
(container, slot, wear) only where wear > 0, so runs without wear keep their digests; `save-v10.json` renamed `save-v11.json`.
**Launch:** `--wear item=n`, `--repair item` (scripted steps). **Tests:** 11 files, about 150 tests; the save version constant 10 to 11 in
six save tests; `DamagePipelineTests` (armour, wear); `RecipeBookTests` 22 to 43; `CraftingFrontendTests`; a NoLiteralText entry; rigs.
Not fixed: damage wear unreachable until combat; the tool line only with needs on.
