# Brief: I04, the equipment panel

Sent 2026-11-16 to a general-purpose agent, Sonnet 5.5 at medium effort (the plan's section 4). Worktree
`/home/james/Dev/Vocaterra/.claude/worktrees/i04`, branch `I04-equipment-panel`, cut from `feature/inventory` after I09's gate. The
shared part is `.claude/briefs/i-series-standard.md`. Built without game windows while the owner games: the screenshots are taken by the
orchestrator later, with I09's, for owner-review stop 4.

```
You are implementing I04: the inventory screen becomes the equipment panel (the inventory plan's section 3.10; D-game-35 items 4 and 5).
The moves already exist (I02); this milestone is the panel's layout, its cells for the equipment, its words and its stats line.

The owner is using the machine: open no game window and take no screenshot. Prove the layout with tests (hit-tests and rectangles at
every UI scale and window size, as `UiScaleLayoutTests` and I02's `InventoryLayout` tests do), and write in your report the exact launch
commands the orchestrator will run later for the pictures (the panel at the default scale, the smallest window that still gets the full
layout, the compact layout, a held stack, a refusal shown, with and without a worn bag).

Today (after I01 to I09): the screen is `Vocaterra.Game.Core/Items/InventoryScreen.cs` and `InventoryPanel.cs` (open state, focus, one
queued `SlotMove` command per click since I02), drawn by `src/Vocaterra.Game/UI/Inventory/InventoryLayer.cs` with `InventoryLayout.cs`
(the bag cells, `Compact` and `Tight`, I02's `SortButton`, `TrashCell` and `Hit(x, y, slotCount)`). Moves are `VoxelCommandKind.SlotMove`
(A the action: 0 left, 1 right, 2 shift, 3 sort, 4 return; B the container: 0 bag, 1 equipment, 2 trash; C the slot, the equipment's flat
index in equipment.json order); a refusal raises `SimulationEventKind.MoveRefused` (Detail the `MoveReason`, Amount the `EquipResult`). The
equipment's groups, slots and Spanish label nouns are `game/equipment.json` (labels: cabeza, cuerpo, pierna, pie, mano, espalda; the
accessories have none, as the lexicon has no `accesorio`). The stats are `Stats/` (`StatSheet`, `StatKeys`: warmth, walkSpeed,
hungerDrain, thirstDrain, maxStamina, armour). The language layer for item names already exists: the Spanish name with its article from
`NounCatalog`, English on hover until the learner model knows the word, words marked read when the panel opens: keep it as it is.

What to build:
1. Layout (Game, with the geometry in a testable layout type): the paper doll on the left, three columns (clothing, armour, cosmetic: the
   groups with a `layer`, in layer order from data, never by id) by the body rows (the slots, labelled in Spanish by their label nouns with
   the article from `NounCatalog`); below it the groups with counted slots (accessories, off-hand, backpack), each cell labelled where a
   label exists. The bag grid on the right: the hotbar row set apart, then the pack rows (3 to 5 rows as the active count allows). The sort
   button and the trash cell placed properly. A stats line. A compact layout for small windows (the existing `Compact` and `Tight` show the
   pattern; say at which sizes each layout applies). A seventh group in equipment.json must get cells with no layout code edit: prove it
   with a test equipment data.
2. Clicks: left, right and shift-click on equipment cells send `SlotMove` with container 1 and the flat slot index (the hit-test returns
   it); everything I02 did on bag cells keeps working.
3. Refusals: the panel reads `MoveRefused` (and `EquipRefused`) and shows why, briefly, in Spanish where the game already has the words
   (look for existing refusal or "no room" lines in content and the locale), else as an icon or an English line in the locale bundle
   `content/locale/` (outside game-v1's hash; follow how other panels show English help). Say which reasons got which text. No new Spanish
   phrase in game-v1 without a lexicon entry; if a reason needs one, leave it as English for stop 3 and list it.
4. The stats line: armour (`armadura`) and warmth (`calor`) from the sheet, and the accessories' effects when one is worn (walk speed,
   drains, stamina) as small icons with numbers. Use the Spanish noun only if `NounCatalog` and the lexicon have it; otherwise an icon and
   list the missing word for stop 3. Show only stats that are not at their default.
5. I02's three open behaviours, judged and either kept (say why) or changed with tests: shift-click on a stack of several wearables is
   refused rather than wearing one; a wearable the rules refuse does not fall through to the hotbar move; right-click into the trash refuses
   a different item. And check whether quitting the game can meet the save's refusal when a stack is held and the bag is full (the panel
   closes on quit?); fix it if it can.
6. Hover: an equipment cell shows its label noun (and the worn item's name the usual way); an empty cell shows the label only.

Tests (Given_When_Then, mirrored folders): the layout's rectangles and hit-tests at every scale and window case (regular, compact, tight;
30, 40, 50 bag slots); a seventh group gets cells from data; equipment cells send the right container and flat index; the refusal text
mapping; the stats line shows only non-default stats; the labels come from the data and `NounCatalog`.

Content: only if a refusal or stat word goes into the locale bundle (outside game-v1's hash) or a label noun already in the lexicon needs
an entry in `game/nouns.json`; then run the content loop. No new game-v1 Spanish without the lexicon.

Documents: docs/game/architecture.md (the panel's layout and how a new group gets cells), docs/game/ui.md or the doc the inventory screen is
described in (find it).

Out of scope: shipped gear (I08a, I08b), durability's bar (I07), a chest, the avatar.

The gate is the standard brief's (the count after I09 is in your prompt). Report: the standard one page, plus the layout sizes, the refusal
texts, the Spanish words missing for stop 3, what you decided on I02's three behaviours and the quit case, and the screenshot commands.
```
