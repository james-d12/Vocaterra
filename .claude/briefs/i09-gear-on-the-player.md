# Brief: I09, gear on the player

Sent 2026-11-16 to a general-purpose agent, Sonnet 5.5 at medium effort (the plan's section 4). Worktree
`/home/james/Dev/Vocaterra/.claude/worktrees/i09`, branch `I09-gear-on-the-player`, cut from `feature/inventory` after I05a's gate. The
shared part is `.claude/briefs/i-series-standard.md`. Built without game windows while the owner games: the screenshots are taken by the
orchestrator later, from the commands the agent writes down.

```
You are implementing I09: what the player wears shows on the player (the inventory plan's section 3.11; D-game-35 item 10). No shipped
item can be worn yet (I08a and I08b add the gear): prove it with test items, and with `--equip` once a test or a later milestone gives a
wearable.

The owner is using the machine: open no game window. You cannot take screenshots in this milestone. Prove everything you can with tests
(the appearance in Core, the meshes' boxes, colours and positions in Game.Tests, as the existing figure tests do if there are any; read
tests/Vocaterra.Game.Tests/Graphics first), and write in your report the exact launch commands the orchestrator will run later for the
pictures (first and third person, the front and the back, by day, with a test or data item worn on each body part, a bag and an off-hand
item). If a picture needs a wearable item that does not ship, say how to get one into the run (for example a test-only content folder, or a
flag): do not add shipped item rows.

Today (after I01 to I05a): items may carry `look` (`Items/ItemLook.cs`: colours per body part, `LookColour`; parsed and validated by
I03a, drawn by nobody); the equipment is `Items/Gear/` (`Equipment`, the groups of `game/equipment.json` with a `layer` each: clothing,
armour, cosmetic; accessories, off-hand and backpack have none). The player's figure is `PlayerAvatar`, made by
`src/Vocaterra.Game/Graphics/Figures/FigureDirector.Avatar()` and drawn through `SceneDrawer.DrawWorld` in third person
(`Graphics/Camera/PlayerView`, `CameraMode.ThirdPerson`); the first-person arm is `HeldArm.cs` and `ArmPresenter.cs`; held items are
drawn by `ItemModels.cs`; the boxes come from `FigureMesher.cs` (also used by `NpcFigures.cs`).

What to build:
1. Appearance (Core, engine-free): a type that turns the equipment into one colour (or none) per body part: for each part the first of
   cosmetic, armour, clothing that covers it, from the worn items' `look`. The layer order is data (the groups' `layer` in equipment.json
   and an order you put in the data, not group ids in code: a fourth layer is a data row). Plus what is on the back (the backpack group's
   item) and in the off-hand (the off-hand group's item), found by their groups' role in data, not by id. Rebuilt when the equipment changes
   (or each frame from the state: cheap either way; say which).
2. Third person: `PlayerAvatar` gains the appearance. Covered parts draw as slightly inflated boxes over the body in the look's colour
   (check what `FigureMesher` supports first; reuse it, do not fork it). The worn bag draws as a box on the back (a fixed shape for every bag
   today; a per-item shape is later); the off-hand item draws in the left hand with the same item models the right hand uses.
3. First person: the arm takes the body slot's sleeve colour when something covers the body; the off-hand item shows in the left hand if
   the first-person view has room for it (say what you chose and why if not).
4. Nothing worn: the player looks exactly as today (verify with a test on the meshes: the same boxes and colours as before).
5. No new custom shader, no per-frame allocation in the drawing path for a static appearance (cache meshes on change), and the frame cost
   stays small (count the extra boxes in the report).

Tests (Given_When_Then, mirrored folders, test items from `GearItems` with `look`): the layer order (cosmetic over armour over clothing,
a part only clothing covers, a part nothing covers); a fourth layer added in a test equipment data works with no code edit; back and
off-hand found by role; the meshes with nothing worn equal today's; a worn body item adds its inflated boxes in its colour; the sleeve
colour in first person.

No content change unless the layer order needs a field in equipment.json and its schema (then run the content loop and put the new bundle
hash in `Fixtures/save-v10.json`). No new Spanish.

Documents: docs/game/architecture.md (the appearance, the layer order, how a new layer or a new visible slot is added).

Out of scope: shipped gear and its looks (I08a, I08b), the panel (I04), durability's look (I07), per-item bag shapes, NPC clothing.

The gate is the standard brief's (the count after I05a is in your prompt). Report: the standard one page, plus the extra boxes drawn, the
first-person choices, and the screenshot commands for the orchestrator.
```
