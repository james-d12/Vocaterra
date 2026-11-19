# Brief: I02, interaction

Sent 2026-11-16 to a general-purpose agent, Sonnet 5.5 at medium effort (the plan's section 4). Worktree
`/home/james/Dev/Vocaterra/.claude/worktrees/i02`, branch `I02-interaction`, cut from `feature/inventory` after I03b's gate. The shared
part is `.claude/briefs/i-series-standard.md`.

```
You are implementing I02: moving items by hand. One address scheme over the bag, the equipment and the trash, the moves over it, the held
stack, and the clicks that drive them (the inventory plan's section 3.9 and 3.1's principles; D-game-35 item 4). No new panel design: I04
rebuilds the panel. Your work is the model, its commands and the click routing on today's inventory screen.

Today (after I01, I06, I03a and I03b): the bag is `Items/Inventory.cs` (an active count of 30 to 50 since I03b: `SlotCount`, `MaxSlots`,
`SetSlot`, `Add`, `Remove`; the first 10 slots are the hotbar); the equipment is `Items/Gear/` (`Equipment`, the rules
`EquipRules.Standard()` with I03b's `BagCapacityRule`, `EquipResult` codes 0 to 12); `Simulation/VoxelCommandKind` ends at `Equip = 8` and
`Unequip = 9` (`VoxelSimulation.Equipment.cs`), and a refused equip raises `SimulationEventKind.EquipRefused`. Inventory edits that change
the world are `VoxelCommand`s (a kind and four ints) applied in the 60 Hz tick, so scripted runs replay them and the state digest covers
them. The screen is `Vocaterra.Game.Core/Items/InventoryScreen.cs` and `InventoryPanel.cs` (the open state and focus) with the drawing in
`src/Vocaterra.Game/UI/Inventory/InventoryLayer.cs` and `InventoryLayout.cs`; clicks reach panels in `VoxelGame.cs` (around line 1000:
`_craftingPanel.Click(input)`, `_stationPanel.Click(input)`, `ClickOnScreens`): follow that pattern.

What to build:
1. `ISlotContainer` (Core, `Items/`): one address scheme (a container id and a slot index) with read, can-accept and put for one slot. First
   implementations: the bag (active slots only), the equipment (each group's slots in equipment.json order, its accept check being the equip
   rules, so a refused put returns the `EquipResult`), and the trash (one slot). A later chest (W24) must be one more implementation with no
   edit to the moves: prove it with a test container.
2. `InventoryOps` (Core): the moves over any two containers, each all or nothing (a refusal leaves every container and the held stack as
   they were, and returns a reason the panel can show later; reuse `EquipResult` for equipment refusals and add a move reason type for the
   rest, codes appended, never renumbered):
   - left click: pick up a whole stack; put the held stack down; swap the held stack with what is there; merge into a stack of the same
     item up to its max (the rest stays held);
   - right click: pick up half (the larger half held); with a held stack, put one down (into an empty slot or a stack of the same item);
   - shift-click: quick move: a wearable from the bag to its slot (the slot its `wear` names; for accessories the first free one); a worn
     item back to the bag (first free slot); otherwise the hotbar (slots 0..9) to the pack (10..) and back, merging into same-item stacks
     first;
   - sort: the pack only (the hotbar is left alone), merging same-item stacks, then catalogue order, then count descending; deterministic;
   - trash: putting a stack in the trash slot replaces what was there (the old one is gone); taking it back out is an ordinary pick-up.
3. The held stack: simulation state (a one-slot container), in the state and the digest only when it is not empty, so with nothing held
   today's digests and every scripted run's baseline stay exactly as they are (verify). Closing the panel returns it to the slot it came
   from, or the first free slot of the bag; if the bag is full, say what you do (the plan has no rule: dropping it into the trash is not
   acceptable; keeping it held until room is, or refusing to close; choose the simpler and say why). The save does not change shape: make
   sure nothing is held when the game saves (closing the panel first, or returning the stack in the save path), and the trash's item is not
   saved (say so in saves.md). No save version change.
4. Commands: each move is a `VoxelCommandKind` appended after `Unequip = 9` (one kind per move or one kind with a sub-code: your choice,
   documented with its argument layout in the enum's doc comments as `Equip` and `Unequip` are), applied in the tick, refused moves raising
   one event the panel can read (extend `EquipRefused` or add a kind; say which). Equip and Unequip stay as they are.
5. Click routing (Vocaterra.Game): on today's inventory screen, left click, right click and shift-click on a bag cell send the commands;
   the held stack is drawn at the cursor (the item's icon and count, as the cells draw them); a sort button and a trash cell are added where
   they fit in today's layout without redesigning it (I04 places them properly); the equipment has no cells yet, so shift-click is the only
   way to wear from the screen until I04. Escape or the inventory key closes the panel and returns the held stack. Hit-testing is Core or
   layout code with tests (`InventoryLayout` already maps cells); the layer only draws.
6. A scripted aid if the scripted-run steps make it cheap (say if not): steps that send the moves by container and slot, so a scripted run
   can prove a move without a mouse.

Tests (Given_When_Then, mirrored folders, test items from `TestItems` and `GearItems`): every move above, including the all-or-nothing
refusals with everything unchanged; merge overflow; right-click half of an odd stack; put one down onto a different item refused; shift-click
of a wearable, a worn item, hotbar to pack and back; sort leaves the hotbar alone and is deterministic; trash replaces; a test container works
with no edit to `InventoryOps`; closing returns the held stack, and the full-bag case; the hit-test of cells, the sort button and the trash
cell at every UI scale (`UiScaleLayoutTests` has the pattern); two runs of one seed and one command list with moves give the same digest; a
run with nothing held gives today's digest.

No content change (no new item, no Spanish). If a label is needed on the sort button or the trash cell, use an icon only and leave the words
to I04 (say so).

Documents: docs/game/architecture.md (the containers, the moves, the held stack, how to add a container), docs/game/saves.md (the held stack
and the trash are not saved).

Out of scope: the paper doll and the panel's design (I04), the equipment's cells, a chest, durability, stat consumers, the avatar.

The gate is the standard brief's (the count after I03b is in your prompt). Report: the standard one page, plus the command kinds and their
argument layout, the move reason codes, what happens when the panel closes on a full bag, and every existing test changed and why.
```
