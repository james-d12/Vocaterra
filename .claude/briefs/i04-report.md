# I04 report (the equipment panel)

The agent's report, saved by the orchestrator (2026-11-16). Sonnet 5.5 at medium effort; brief `i04-equipment-panel.md`.

Branch `I04-equipment-panel`, commits e8f60414 (nouns, refusal lines, cards, rehash) and 181a0d2e (the panel), worktree
`.claude/worktrees/i04`, clean, not pushed. No window opened.

## Gate

Build -warnaserror 0 warnings; `scripts/test.sh full` 12,102 passed, 0 failed, 3 skipped (+106 against 11,996); format clean; content
validate passes. The content loop ran with `--grammar --speech`: 0 new grammar findings, 14 card WAVs, the bundle rehashed, the hash in
`save-v10.json`.

## Built

- Core `Items/Gear`: `EquipmentShape` and `EquipmentCell` (`EquipmentData.Shape`, made once). The groups with a `layer` are columns in layer
  order; rows are the slot names in first-seen order, labelled by each slot's `label`; counted groups flow into the rows below. A seventh
  group needs no code edit (tests: a fourth layered column and a counted group).
- Core other: `InventoryHitKind.Equipment` (the slot is the flat index; `InventoryPanel` sends `SlotMove(1, flat)`); `MoveRefusalText`
  (reason to locale key) and `MoveNotice` (`VoxelSession.MoveNotice`, set from `MoveRefused` and `EquipRefused`, shown 180 ticks);
  `InventoryHover`, `StatDisplay`, `StatChip`, `StatDisplayFormat`. Opening the panel marks the label, worn item and stat nouns read.
- Game `UI/Inventory`: `InventoryLayout(w, h, slots, needsRows, shape)` rewritten (doll left, bag right, the hotbar set apart by
  `HotbarGap`, sort and trash under the bag, stats under the doll); `InventoryLayer` draws it with a footer (the refusal, else the hover,
  else the key hints); `StatIcons` (7x7 icons with a fallback), `StatLinePlacer`.
- Content: seven catalogue nouns (cabeza, cuerpo, pierna, pie, espalda, armadura, calor) in `game/nouns.json`, their English in
  `locale/en/nouns.json`, 14 card WAVs; `armadura` appended to `cefr/a1.txt` with its reason (D-game-35 (a)); `mano` existed. Eleven refusal
  lines in `locale/en/ui.json` and `LocaleText.UiKeys`.
- Docs: architecture.md (the equipment panel, how a new group gets cells), running.md.

## Layout sizes (each the first that fits both ways)

- Regular: 64x80 bag cells, 44 px doll cells, a noun column beside the body rows; 40 slots with the needs list fit from about 950x700.
  1280x720 at 100% (40 or 50 slots) and at 125% (30 slots) get regular.
- Compact: 44x56 cells, 36 px doll cells, no noun column (the hover line carries the nouns); about 632 wide.
- Tight: 36x50 cells, 30 px doll cells, thinner header and footer; 533x400 and 640x480 at 100%.
- Known gap: 50 slots plus the needs list at 533x400 overflows the canvas foot, as before the doll; the tests skip that one case.

## Refusal texts (English, `locale/en/ui.json`)

`refuseDifferent`, `refuseTrashDifferent`, `refuseSlotFull`, `refuseNoRoom` (also BagFull and BagSlotOccupied), `refuseStackTooLarge`,
`refuseNotWearable`, `refuseWrongSlot`, `refuseWrongKind`, `refuseDuplicate`, `refuseWearOne`, `refuseBagNotEmpty`. NothingToMove and the
"no such" reasons show nothing.

## Spanish missing for stop 3

`accesorio` (not in the lexicon: accessory cells have no label); no noun for walk speed, hunger, thirst or stamina on the stats line (icon
and number only); the eleven refusal lines are English; the key hints (`ui.inventoryHints`, `ui.inventoryHintsShort`) still say "left
click: mine, place", stale now that clicks move items.

## I02's three behaviours: kept, each pinned in `QuickMoveJudgementTests`

Shift-click on several wearables is refused (`refuseWearOne`; split with a right click first); a refused wearable does not fall through to
the hotbar move (it would move the item the player meant to wear); right-click into the trash refuses a different item, with its own line.

## The quit case: real, fixed

`SaveService.Write` (quit, menu Exit, F6) called `SaveGame.Capture`, which throws when a stack is held and the bag is full; autosave caught
it, the quit paths did not. `Write` now calls `ReturnHeld` first, logs "not written: a stack is held..." and returns false; the stack stays
held. Test: `SaveServiceTests`.

## Existing tests changed

Noun counts 33 to 40 (`GameDataTests`, `NounCatalogTests`, `SpeechLinesTests`); `InventoryLayoutRowsTests` (3 asserts) and
`NeedsHudLayoutTests.WithoutTheNeedsTheInventoryIsWhatItWas` for the hotbar gap and the buttons' row; `InventoryLayoutHitTests`' header
buttons test rewritten for buttons under the bag; `save-v10.json` bundle hash only. No golden or fuzz change. Outside the game's area: the
content loop's outputs only (`content/grammar`, `content/speech-index.json`, `docs/game/speech-checklist.md`).

## Screenshots (not run)

Need a copy of `content/game-v1` with `look`, `wear` and `stats` rows (a clothing item per part, a bag with `wear: back` and a capacity, an
off-hand item, accessories with walkSpeed, hungerDrain, maxStamina), the run pointed at it (check the option in `LaunchOptions.cs`), then
`--give`, `--equip`, `--move left,bag,<n>` and a screenshot with the inventory open, each `--seed 42 --quality low --frames 3`:
1280x720; 1024x768 (smallest regular); 800x600 (compact); 640x480 at 150% text (tight); a held stack (`--move left,bag,12`); a refusal (a
hat dropped on the legs cell, shot at once); with and without a worn bag.
