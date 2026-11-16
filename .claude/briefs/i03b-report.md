# I03b report (the bag and the rest of the slots)

The agent's report, saved by the orchestrator (2026-11-16). Sonnet 5.5 at high effort; brief `i03b-bag-and-slots.md`.

Branch `I03b-bag-and-slots`, HEAD c84f3342, worktree `.claude/worktrees/i03b`, clean, nothing pushed, no background command left running.
Commits are by area (death, save, docs, bag core, slot view); the intermediate commits were not each built on their own, the final tree was
built and gated.

## Gate

- Build with -warnaserror: 0 warnings, 0 errors.
- `scripts/test.sh full`: 11,819 passed, 0 failed, 3 skipped (feature/inventory was 11,751: +68 new tests; the 3 skips are the
  model-gated ones as before).
- `dotnet format --verify-no-changes`: clean. `scripts/content.sh validate`: passes; the content loop (`--tier none`) ran once for the
  rehash.
- No game window was opened.

## Built

- **Base 30 from data:** `equipment.json` has `bagSlots: 30` (schema, `EquipmentData.BagSlots`, validator range 10..100), optional in the
  parser with the default `Inventory.DefaultBaseSlots` = 30 (only data-less builds use it: tests, the host's `--bag`).
- **Capacity:** items gain an optional `capacity` (items.schema.json, `ItemDefinition.Capacity`, the catalogue checks: 1..100, only a
  `bag`-tagged item with `wear: back`). `IItemCatalogue.LargestCapacity` is a default interface member.
- **Inventory:** `Items/Inventory.cs` has `BaseSlots`, `MaxSlots` (base plus the largest capacity), `SlotCount` (now the instance's active
  count), `SetActiveCount` and `IsEmptyFrom`. `Slots` is a live view of the active slots, made once (reading it allocates nothing).
  `SetSlot`, `Add` and `Remove` see only active slots; slots past the count are always empty. The static `Inventory.SlotCount` is gone, so
  every consumer names its inventory.
- **Equipment:** `Equipment.Capacity`, `ActiveSlotsFor`, `ActiveSlotsAfter` and `SyncBag`; equip, unequip and restore keep the active
  count at base plus the worn capacity. Code reads capacities from the items and never names a group.
- **The bag rule:** `Items/Gear/BagCapacityRule.cs`, in `EquipRules.Standard()`. A bag cannot come off or be swapped for a smaller one
  while a slot it would drop holds an item; a swap for a bag at least as large is always allowed. New code `EquipResult.BagNotEmpty = 12`,
  appended; a refusal changes nothing.
- **The unequip path:** a bag that comes off goes into the slots that stay; none free gives `BagFull`; a target slot that goes with the bag
  gives `NoSuchBagSlot`.
- **The inventory screen:** `InventoryLayer` draws `view.Items.SlotCount` slots; `InventoryLayout`'s rows are the ceiling of count/10 (3 to
  5). Nothing else in the panel changed.
- **Death:** `DeathSpec.Keeps(item)` and `KnownKeepRules`; `VoxelSession.Death` reads `death.keepInBag` in place of `MaxStack > 1`, and
  `NeedsData.Parse` refuses an unknown value. Worn items and the worn bag stay worn; stacks of one stay in their own slots (never moved, so
  they always fit the count); only stackables go to the chest, as before.
- **State and digest:** `VoxelSimulationState.Slots` has the active length. `Restore` checks the length against the count the worn items
  give, clears the slots, syncs the bag, then fills it. The digest hashes the active slots and appends the active count only when a bag has
  changed it; the worn slots are in the digest only when something is worn.
- **Save version 10** (9 refused; `CurrentVersion` and `OldestReadableVersion` both 10): `inventory` is the active slots only (30, 40 or
  50 entries). New optional `worn`, left out when nothing is worn: `[{"group":"clothing","slot":"body","item":"shirt"}]`
  (`Save/WornSave.cs`, by group id, slot name and item name, never an index). A group, slot or item the data lacks loads as nothing:
  `AdaptWorn` reports dropped `worn items`, and `AdaptInventory` resizes the list to the kept worn items' count and reports stacks in
  vanished slots as dropped `inventory slots`.
- **Fixture:** `Fixtures/save-v9.json` became `save-v10.json` (version 10, the first 30 slots, slots 30..39 checked empty first, new digest,
  new bundle hash). Nothing shipped is wearable, so it wears nothing; worn items are covered by round-trip tests. It was transformed by a
  throwaway test (deleted), not recorded by running the game.
- **Docs:** saves.md (version 10), architecture.md, content/README.md (`capacity`, `bagSlots`), the Core README line.

## Existing tests, fixtures and baselines changed

- **40 to 30 slots, on purpose:** InventoryTests (30 slots, 99*30 berries); CraftingTests (berries sized by the count); PlotPurchaseTests
  (axes granted 38 became 28, three places); count loops in ProcedureRunnerTests, StationPanelTests, DeathTests, FireRingRescanTests,
  NeedsRateTests, RelaxedNeedsTests, ExchangeMissTests, ProcedureSessionTests, PriceListSessionTests, SessionSkipTests,
  GatheringAndPlacingTests, HostActionTests, EquipmentSimulationTests and EquipmentTests; ItemsSaveTests (30 slots, the v10 fixture, the
  refusal for version 9).
- **Version 9 to 10:** LandSaveTests, NeedsSaveTests, SleepEntrySaveTests, WeatherSaveTests, AtmosphereSaveTests (`TheSaveVersionIsTen`),
  the fixture's name in several tests.
- **Pinned digests:** `AtmosphereSimulationTests` `PinnedFresh` and `PinnedAfterWalking`. Cause proven: the fresh digest recomputed by hand
  with 40 empty slots equals the old pin (8213aec4...), with 30 the new one (82c9c42d...); only the shorter slot hash moved them.
- **Layout:** the inventory grid at every scale in `UiScaleLayoutTests` now runs 30, 40 and 50 slots.
- No golden, fuzz or scripted-run baseline moved.

## New tests

InventoryActiveSlotsTests, BagCapacityRuleTests, ItemCapacityTests, BagSimulationTests, DeathGearTests, WornSaveTests, EquipmentDataTests
(`bagSlots`), InventoryLayoutRowsTests (Game.Tests). `GearItems` gained `Sack` (10), `Rucksack` (20) and `GearItems.Session()`.

## Outside the game area

`scripts/content-loop.sh`: one message string, `save-v9.json` to `save-v10.json`. `content/game-v1/manifest.json` rehashed by the loop.

## Known, not fixed

- The active count is clamped to `MaxSlots` (base plus the largest capacity). Data with two back slots holding two bags could lose slots
  silently, and no validator checks it; unreachable with the shipped one-slot backpack group.
- `save-v10.json` wears nothing (no shipped wearable), so worn loading is covered only by round-trip tests.
- The panel is still the old one with 3 to 5 rows; there is no UI to take a bag off, and the refusal reaches the player only through the
  command event (I02, I04).
