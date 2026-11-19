# I02 report (interaction)

The agent's report, saved by the orchestrator (2026-11-16). Sonnet 5.5 at medium effort; brief `i02-interaction.md`.

Branch `I02-interaction`, head c584502b, worktree `.claude/worktrees/inventory/.claude/worktrees/i02` (nested under the inventory
worktree: the orchestrator's `git worktree add` used a relative path). Clean; three local commits (containers and ops; command, state,
digest and save; panel clicks, hit-test, layer, scripted step and docs). The agent rewrote the commits with filter-branch to fix the
trailers' blank line.

## Built (src/Vocaterra.Game.Core unless noted)

- `Items/Containers/`: `ISlotContainer` (Id, SlotCount, Discards, Get, CanPut, Put); `BagContainer` (active slots only),
  `EquipmentContainer` (the equip rules as its accept check), `TrashContainer` (one slot); `HeldStack` (a container that remembers where
  it was picked up); `SlotContainers` (the registry); `IQuickTarget` (the equipment claims wearables); `InventoryOps`, `MoveTransaction`,
  `MoveAction`, `MoveReason`, `MoveResult`. Every move runs through the transaction: each put is judged against the state the earlier puts
  made, and a refusal undoes the puts already made, so every move is all or nothing and no move names a container type. One edit in
  `Items/Gear`: `Equipment.CanTakeOff`.
- `Simulation/VoxelSimulation.Interaction.cs`: `Held`, `Trash`, `Containers`, `Moves`, `Move()`, `ReturnHeld()`. A worn-item change raises
  `ItemEquipped` or `ItemUnequipped` and rebuilds the stats. `Restore` clears the held stack and the trash.
- `SaveGame.Capture` calls `ReturnHeld` first; it throws `InvalidOperationException` (as a session not at rest does) only when the bag is
  full and the stack has nowhere to go. The autosave already catches it.
- `InventoryPanel` queues one command per click; `WorldStepper` takes them (a new optional constructor argument). Left, right and
  shift-click on a bag cell or the trash cell; left click on the sort button sorts; closing queues `ReturnHeld`.
- Vocaterra.Game: `InventoryLayout` gained `SortButton`, `TrashCell` and `Hit(x, y, slotCount)`; `InventoryLayer` draws them and the held
  stack at the pointer (icons only; I04 places and words them); `VoxelGame` routes clicks to the panel.
- Scripted step `--move <left|right|shift>,<bag|equip|trash>,<slot>`, `--move sort`, `--move return` (`VoxelStepKind.SlotMove`,
  `VoxelStep.TryParseMove`); it needs `--screenshot` like the other steps, so it opens a window (none was run).
- Docs: architecture.md (containers, moves, the held stack, adding a container), saves.md (the held stack and the trash are not saved),
  running.md (`--move`).

## Command, events, codes

- `VoxelCommandKind.SlotMove = 10`, one kind with a sub-code: A the `MoveAction` (0 left, 1 right, 2 shift, 3 sort, 4 return the held
  stack); B the container (0 bag, 1 equipment, 2 trash); C the slot (the equipment's flat index in `equipment.json` order); sort and return
  ignore B and C; D unused. Equip and Unequip unchanged.
- `SimulationEventKind.MoveRefused = 21` (`EquipRefused` untouched): Detail the `MoveReason`, Amount the `EquipResult` when a worn slot
  refused, X the container, Y the slot, Z the action.
- `MoveReason`: Done 0, NoSuchContainer 1, NoSuchSlot 2, NothingToMove 3, DifferentItem 4, SlotFull 5, NoRoom 6, StackTooLarge 7,
  SlotRefused 8, NoSuchAction 9.

## Closing on a full bag

The stack stays held, never trashed or lost (chosen over refusing to close, since the panel would need the outcome of a command a tick
later). Return order: the origin slot if empty, else the bag (merge, then the first free slot). The panel draws it again on reopening; a
save returns it first and refuses only when it cannot. Hard to reach: the origin slot is normally still free.

## Digest, saves, tests

- The digest adds the held stack (with its origin) and the trash only when they hold something; a run that moves and puts back has the
  digest of a run that never moved; two runs of one command list match. No save version change; fixtures untouched.
- 11,905 passed, 0 failed, 3 skipped (+86 against 11,819); build 0 warnings; format clean; content validate clean.
- New tests: every move and each all-or-nothing refusal (every container, the held stack and the trash snapshot unchanged); merge overflow,
  half of an odd stack, a different item refused; shift-click of a wearable, a worn item, a worn bag, accessories, hotbar to pack and back;
  sort; trash; a test `ChestContainer` with no edit to the ops; the held-stack return and the full-bag case; the command, event and digest;
  panel clicks and close; the hit-test at six scale and window cases (30, 40, 50 slots); `--move` parsing.
- Existing tests, goldens and fixtures changed: none. Files outside the game's area: none.

## Known, not fixed

- Shift-click on a stack of more than one wearable is refused with StackTooLarge (it does not take one).
- Shift-click on a wearable the rules refuse shows the equip reason; it does not fall through to hotbar and pack.
- Right-click put-one into the trash refuses a different item; only a whole stack replaces the trash's item.
