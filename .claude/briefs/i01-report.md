# I01's report (items as data)

The agent's report, 2026-11-15 (Sonnet 5.5, high). Commit `1996befa` on `I01-items-as-data`, gated into `feature/inventory` at
`e865a818` (11,543 passed, 3 skipped).

- **Data:** `content/game-v1/game/items.json` (14 rows: name, id, stack, icon, held, tags, optional tool) and
  `content/schema/items.schema.json`; the nouns, host-events, objective, session and procedure schemas take key-shaped item names (no
  enum); a provenance entry.
- **Core:** `Items/IItemCatalogue`, `ItemCatalogue`, `ItemHeld`; `ItemDefinition(Id, Name, MaxStack, Icon, Held, Tags, Tool)`; the static
  `ItemDefinition.All` and `Get` gone; `GameData.Items` loaded once. The loader refuses a malformed row, a duplicate name or id, an
  enum member without its row (same id and name), an unknown held kind, icon or tool material.
- **Game:** `BlockAtlasLayout.IconForKey` table-driven, `IconFor(ItemDefinition)` by the row's key, `HeldItems.For(items, stack)` by the
  row's `held`; an unknown key draws `IconPlain`; the game's start passes `BlockAtlasLayout.IconKeys` to `GameData.Load` so the validator
  reports a key without art (the host's `--validate-content` has no atlas and skips that one check).
- **Validator:** `CheckItems` reports duplicates and every unknown item a noun, recipe, shop or gift row, a session grant or an order
  names; planting, procedure and needs checks use the catalogue.
- **Save version 9:** `SlotSave(string Item, int Count)`; an empty slot is `{"item":"","count":0}`; the belongings chest the same; a name
  the catalogue lacks loads as an empty slot and the migration reports it dropped; version 8 refused. `save-v8.json` became
  `save-v9.json`.
- **Enum members kept** (behaviour): Wood, Torch, Berry, Bottle, WaterBottle, Book, Coin, Stake, CampKit. Rows only: Bread, Apple, Axe,
  RoastApple, Flour (the axe's tool behaviour was already its data `ToolSpec`).
- **Consumers taking the catalogue:** Inventory, VoxelSimulation.Create, NounCatalog (and through it RecipeBook, ObjectiveBook, the NPC
  effects' misses), HostEventTable, PlantingUse, FoodItemUse, ScriptItemNames, ProcedureRunner, StationScreen, the VoxelSession partials,
  GuideSession.Activities, NeedsValidator, GameDataValidator, PhraseSheet, the host's Program and ConversationSession, VoxelScript,
  InventoryLayer, VoxelGame, WorldInput, ArmPresenter, BlockAtlas, SceneDrawer.
- **Tests:** ItemCatalogueTests, DataOnlyItemTests (id 100 in the bag, stacked, saved, loaded, named by a noun, recipe, shop and gift,
  resolved by a script), ItemValidationTests, ItemsSaveTests, HeldItemsTests, the TestItems helper. No digest, golden or fuzz baseline
  moved. About 70 test files changed mechanically (the catalogue passed in; removed enum members as `TestItems.Bread` and so on); the
  version assertions 8 to 9; the schema tests' item-enum assertions replaced (names are no longer listed).
- **Not done:** lookup by noun stays in `ScriptItemNames` (the catalogue loads before the nouns); script item names are checked at run time
  only; `ItemId` stays an enum, so a data-only item is `(ItemId)100`. CLAUDE.md named the fixture's old file (made version-neutral by the orchestrator).
- **Environment:** every heavy command with `VOCATERRA_HEAVY_SLOTS=1`; slot waits were long (one format waited about 40 minutes); no game
  window opened.
