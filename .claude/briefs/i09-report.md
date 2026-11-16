# I09 report (gear on the player)

The agent's report, saved by the orchestrator (2026-11-16). Sonnet 5.5 at medium effort; brief `i09-gear-on-the-player.md`.

Branch `I09-gear-on-the-player`, commits 06c8657a (code) and ece04184 (docs), worktree `.claude/worktrees/i09`, clean, not pushed.

## Gate

Build -warnaserror 0 warnings; `scripts/test.sh full` 11,996 passed, 0 failed, 3 skipped (+19 against 11,977); format clean; content
validate passes. No content change: no rehash, the save fixtures untouched.

## Built

- Core (`Items/Gear`): `Appearance` (one colour per covered body part, the sleeve colour, the bag item, the off-hand item);
  `AppearanceBuilder.Of(equipment, roles)` reads the layer groups from the highest `layer` number down, and for each look part the first worn
  item covering it wins (a fourth layer is a row with a higher number; file order does not matter); `AppearanceRoles` finds the back and the
  off-hand by the slot's `wear` kind (`back`, `offhand`), not by group id, so equipment.json did not change; the sleeve is the `sleeve` look
  part, else `body`; `Equipment.Revision` moves on every equip, unequip, set or restore.
- Game (`Graphics/Figures`): `PlayerGear` rebuilds only when `Revision` changes (appearance, dressed parts, off-hand mesh cached; a frame
  with the same gear allocates nothing); `GearFigureParts`, a table of shape rows (head, body, sleeve, legs, feet), each an inflated box
  about a quarter pixel over the part, plus a fixed pack box on the back with the flap picture (the bag's `back` colour or leather); a new
  visible part is one row; `FigureMesher.AppendAttached` carries the off-hand mesh with the left arm's swing (no fork).
- Wiring: `PlayerAvatar.Gear`, `ArmFrame.Sleeve` and `OffHand`, `ArmMeshes.Arm(sleeve)` (one mesh per sleeve colour); `FigureDirector`
  and `ArmPresenter` take an optional `PlayerGear`, shared by `VoxelGame`; `VoxelRenderer` draws the third-person off-hand item and a
  mirrored left arm in first person.
- architecture.md: the appearance, the layer order, adding a layer, adding a visible part.

## Nothing worn, cost, first person

- Nothing worn: the avatar's gear is null and the parts array is today's instance; a mesh test checks the same vertices. An item with no
  `look` changes nothing.
- Cost: at most 9 extra boxes, 54 faces (head 1, body 1, sleeves 2, legs 2, feet 2, pack 1), plus the off-hand item's extruded faces (about
  100 to 200 quads), transformed each frame into the reused figure buffer. Zero with nothing worn.
- First person: the arm takes the sleeve colour; the off-hand item shows in a mirrored left arm at the bottom left, with the idle sway but not
  the gather swing, only when one is worn.

## Tests

`AppearanceBuilderTests` (8, Core) and `PlayerGearTests` (10, Game): the layer order, the sleeve fallback, roles by wear kind, a fourth layer
as a data row, layer number versus file order, `Revision`; the meshes with nothing worn, inflated boxes and sleeve colours, the 9-box count,
the bag box, an item with no look, off-hand caching and swing, rebuilding only on change, the first-person arm. No existing test, golden or
fixture changed. No file outside the game's area.

## Screenshots (not run: no window)

No shipped item can be worn, so the pictures need a test-only content folder: a copy of `content/game-v1` with `look` rows in its
items.json (a clothing item per part, a bag with `wear: back` and a capacity, an off-hand item), the run pointed at it with the content-dir
option, the items given with `--equip`, the third-person camera option, `--time 12`. Shots: first person; third person front and back; a
piece on each of head, body, legs, feet; a bag; an off-hand item. The flag names are to be checked against `LaunchOptions.cs` before the run.
