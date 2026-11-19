# Brief: I03b, the bag and the rest of the slots

Sent 2026-11-16 to a general-purpose agent, Sonnet 5.5 at high effort. Worktree `/home/james/Dev/Vocaterra/.claude/worktrees/i03b`,
branch `I03b-bag-and-slots`, cut from `feature/inventory` after I03a's gate. The shared part is `.claude/briefs/i-series-standard.md`.

```
You are implementing I03b: the bag shrinks to its base and grows with the worn bag, worn gear survives death and saves, and the save
takes its next version (the inventory plan's sections 3.3 and 3.14, the I03b row of section 4; D-game-35 items 3, 8, 9).

Today (after I01, I06 and I03a): items are data (`game/items.json`, `Items/IItemCatalogue`); the bag is `Items/Inventory.cs` with
`SlotCount = 40` and `HotbarSize = 10` (about 26 uses of the two constants under src/); the equipment is `Items/Gear/` (`Equipment`, its
rules `IEquipRule` and `EquipRules.Standard()`, `EquipResult`, `EquipmentData` from `game/equipment.json` with six groups: clothing,
armour, cosmetic, accessories, offhand, backpack), the stat sheet `Stats/`, and `Simulation/VoxelSimulation.Equipment.cs` with the
`Equip` and `Unequip` commands. Worn items live in `VoxelSimulationState.Worn` and the digest (only when something is worn), but
`SaveGame` does not write them yet. The off-hand and cosmetic groups already work through I03a's generic rules: check them, do not rebuild
them. Death (`Session/VoxelSession.Death.cs`, line 136) moves every stack with `MaxStack > 1` to a belongings chest; the `needs.json`
rule `death.keepInBag: "stacksOfOne"` is parsed into `DeathSpec.KeepInBag` and never read. Saves are at version 9 (I01).

What to build:
1. The bag: the hotbar's 10 slots plus 20 (base 30, from data, not a constant spread through the code). Items gain an optional
   `capacity` field (items.schema.json, ItemDefinition, the catalogue's checks: only a `bag`-tagged item with `wear: back` may have
   one). The backpack group's worn bag adds its capacity: the array is sized to the largest possible bag (base plus the largest capacity in
   the catalogue) and the active count follows the worn bag. Every consumer of `SlotCount` uses the active count (adding, removing,
   exchanging, crafting, the hotbar, the inventory layer, the HUD, scripted runs, the station and plot purchase). Slots past the active
   count are always empty.
2. The bag rule, as one `IEquipRule` (the seam I03a left) plus whatever the unequip path needs: a bag cannot come off, or be swapped for a
   smaller one, while any slot it would drop holds an item; swapping for a bag at least as large is always allowed. A refusal is a new
   `EquipResult` code appended at the end (never renumber), with nothing changed.
3. The inventory screen still draws a fixed four rows of ten (`src/Vocaterra.Game/UI/Inventory/InventoryLayout.cs`, `InventoryLayer.cs`):
   make it draw the active count's rows (three to five), nothing more; I04 rebuilds the panel.
4. Death: worn items and the worn bag stay worn; stacks of one stay in the bag; the bag's stackables go to the belongings chest, as
   today. `VoxelSession.Death.cs` reads `DeathSpec.KeepInBag` instead of the hardcoded `MaxStack > 1` (an unknown value is refused when
   needs.json loads). If the death leaves more stacks of one than the active count can hold (it cannot today), say what happens.
5. The state and digest carry the active count; the worn items stay in the digest only when something is worn.
6. The save's next version, 10: the bag (as I01 stores it, sized to the active count or the maximum: say which) and the worn items by
   group id, slot and item name (never by index, so reordering equipment.json does not scramble a save; a group or item a save names
   that the data lacks loads as nothing, reported like I01's dropped items). Version 9 is refused. `Fixtures/save-v9.json` becomes
   save-v10.json, recorded again, with something worn in it if the fixture's generator allows (test items cannot be in a shipped-bundle
   save: if nothing shipped is wearable, leave it unworn and cover worn items with a round-trip test). docs/game/saves.md gets version
   10.
7. Tests that assumed 40 slots (InventoryTests, BagTests, PlotPurchaseTests, StationPanelTests, ProcedureRunnerTests, DeathTests,
   CraftingTests, MarketLessonTests, NeedsSessionTests, OpeningAndEndingTests, SoundFrontendTests and any other a grep finds): rewrite each
   to the base of 30 on purpose and name each in the report. If a scripted run or digest baseline moves because the bag is smaller, find
   out exactly why before recording it again.

Tests (Given_When_Then, mirrored folders, test items from `TestItems` for bags): base 30 with no bag; a small and a large test bag add 10
and 20; adding fills only active slots and reports full at the active count; the bag rule's refusals and allowed swaps with nothing
changed; death keeps worn items, the worn bag and stacks of one; the save round-trips worn items and the active count; version 9 refused;
a save naming an unknown group or item loads the rest; two runs of one seed and one command list with a bag worn and swapped give the same
digest.

Content: items.schema.json (and needs.json only if its rule's value set changes) are content, so run the content loop; the new bundle hash
goes into the new fixture. No new item rows (the bags are I08b's), no new Spanish.

Documents: docs/game/saves.md (version 10), docs/game/architecture.md (the bag's active count, the bag rule, the death rule), content/README.md
(the `capacity` field).

Out of scope: the panel's design, click handling, the stat consumers, shipped bags and gear, the avatar.

The gate is the standard brief's (the count after I03a is in your prompt). Report: the standard one page, plus the new EquipResult code,
the save's shape for the bag and the worn items, and every existing test, fixture, digest or baseline changed and why.
```
