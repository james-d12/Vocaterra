# Brief: I01, items as data

Sent 2026-11-15 to a general-purpose agent, Sonnet 5.5 at high effort. Worktree `/home/james/Dev/Vocaterra/.claude/worktrees/i01`,
branch `I01-items-as-data`, cut from `feature/inventory` (I00's record and these briefs). The shared part is
`.claude/briefs/i-series-standard.md`.

```
You are implementing I01: items move from code to data (the inventory plan's section 3.2; it takes over the activities plan's W12).
Done when nothing in play changes but the save's shape.

Why: `src/Vocaterra.Game.Core/Items/ItemDefinition.cs` holds `enum ItemId : ushort` (14 members) and a static table of
`ItemDefinition(Id, Name, MaxStack, ToolSpec?)`; about 25 files under src/ read `ItemDefinition.All` or its look-ups (VoxelSession and
its partials, GameDataValidator, StationScreen, HostEventTable, NpcHostEvents, Inventory, ProcedureRunner, VoxelSimulation,
NounCatalog, NeedsValidator, ScriptItemNames, ObjectiveBook, VoxelScript, GuideSession.Activities, Host Program.cs, and in the Game
project VoxelGame, InventoryLayer, BlockAtlasLayout and HeldArm). `content/schema/nouns.schema.json` lists the item names as an enum.
The I series adds about thirty items later (I08a, I08b), and every one must be a row of data, not a new enum member.

What to build:
1. `content/game-v1/game/items.json` and `content/schema/items.schema.json`. Per item: `name` (the stable key that saves and data use:
   today's kebab-case names from nouns.schema.json, `wood`, `torch`, ..., `camp-kit`), `id` (a stable number: the 14 enum values kept;
   data-only items take ids from 100, never reused), `stack` (max stack), `icon` (an atlas key), `held` (how the hand shows it: the kinds
   HeldArm uses today), `tags` (an open list of strings; give today's items honest tags such as tool, food, material, container),
   `tool` (today's ToolSpec, optional). Leave room for the later fields (`wear`, `stats`, `look`, `capacity`, `durability`) without
   adding them: the schema should make adding a field a schema edit only.
2. `IItemCatalogue` (look up by id, by name, by noun where the noun mapping exists today; enumerate in catalogue order), with one
   implementation loaded once by `GameData` from items.json, and passed (constructor or method parameter) to everything that read the
   static table. The static `ItemDefinition.All` and its static look-ups go away. `ItemDefinition` may stay as the row record.
3. The enum keeps only the members code refers to by behaviour (today that may be most or all of the 14: the bottle fills, the book
   opens, the axe chops, the stake and the camp kit place, coins pay; check each). The loader checks every remaining member against a data
   row with the same id and name and fails loudly on a mismatch or a missing row. A data-only item (id >= 100, no enum member) must work
   everywhere an item can be: in the bag, stacked to its data's limit, saved and loaded, named by a noun, a recipe, a shop row.
4. Icons by key (Game): `BlockAtlasLayout.IconFor(ItemId)` and `HeldArm` take the item's `icon` key (`IconForKey` already exists); an
   unknown key draws a fallback tile, and the content validator reports the unknown key. Keep the atlas's order and every existing icon
   exactly, so no screenshot or picture moves.
5. Validation: `nouns.schema.json` stops listing item names; `GameDataValidator` checks instead that every item a noun, a recipe, a shop
   (`host-events.json` sell and buy rows), a gift, a planting, a procedure, a needs rule or a script names is in the catalogue, and that
   names and ids are unique. Tests for each refusal.
6. The save's next version, 9 (`SaveGame.CurrentVersion`): the bag's slots store the item's `name` (and an empty slot as empty, not
   `Wood` with count 0, if that is simple; say what you chose); version 8 is refused like every older version (no migration; there is a
   SaveMigration.cs: leave its existing behaviour for other versions as it is unless it gets in the way, and say what you did).
   `tests/Vocaterra.Game.Core.Tests/Fixtures/save-v8.json` becomes save-v9.json, recorded again with the new bundle hash; every test
   that names the version or the fixture follows. docs/game/saves.md gets the version 9 row.
7. The state digest: keep it on the numeric id so the scripted runs' digests do not move. If any digest, golden or scripted-run baseline
   moves anyway, find out why before recording it again, and name each in the report.

Tests (folders that mirror the source, Given_When_Then): the catalogue loads the 14 rows and matches the enum; a mismatched or missing
enum row fails the load; stack limits come from data (a row's `stack` changed in a test catalogue changes Inventory.Add's behaviour); a
data-only test item (from a test catalogue, not the shipped bundle) goes into the bag, stacks, saves and loads; the fallback icon; the
validator's refusals; a version 8 save is refused with the existing message shape.

Content: items.json is in content/game-v1, so run the content loop (content/README.md, "The edit loop"; `scripts/content-loop.sh`, run
directly, never under limited.sh), with `--record` only if the goldens moved for a reason you understand; the new bundle hash goes into
the save fixture. No new Spanish, no new phrases, no lexicon change.

Documents: docs/game/saves.md (version 9), a short paragraph in content/README.md on items.json and its fields (the full "Adding an
item" is I10's), and docs/game/architecture.md where it describes items or ItemDefinition.

Out of scope: the bag's size (it stays 40 until I03b), equipment and its fields, wear and durability, any new item, the inventory panel,
temperature (another milestone, I06, follows yours and will edit content/game-v1 too).

The gate is the standard brief's. Report: the standard one page, plus the enum members kept and why, the empty-slot choice, the list of
consumers that now take the catalogue, and every golden, digest, fixture or existing test that changed and why.
```
