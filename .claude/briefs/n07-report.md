# N07 report (Sonnet 5.5, 2026-11-05), saved by the orchestrator

Branch `N07-cooking-fire`, built at `50c17886` (pushed, clean), then synced with L01 by a Haiku agent (brief `n07-sync.md`).
Before the sync: 9443 passed, 3 skipped (9433 before); without models 9401 passed, 45 skipped; build, format, validate, A2 lint clean;
grammar 0 new findings (the first run reported 197 while the agent's empty local cache filled; the committed cache gained 17 entries).

- Fire as a station: `VoxelSimulation.StationInReach(BlockId.FireRing)` only while a ring burns (`FireRings.StoneBurns`); noun `fuego`
  (block `fire-ring`) in `nouns.json`. `VoxelGame.AimedAtWorkbench` also takes a lit ring's stone or the ground inside it: E opens the
  crafting panel; an unlit ring does nothing (the sleep offer needs a lit own fire).
- Roast apple: `ItemId.RoastApple = 33`, key `roast-apple`, stack 99, listed under `manzana` with the label «manzana asada» (a new optional
  noun field `labels`, `NounCatalog.LabelOf`). Recipe `manzana` (`makes: roast-apple`, a new optional field) from one apple at `fuego`.
  Foods: `roast-apple` hunger +20, healing 5. Schemas: nouns, host-events, objective, recipes. Event `item_crafted (manzana)`. No save version.
- Display: `IconRoastApple` painted in code; the hotbar and crafting row show «manzana asada»; `RecipePanelLayer` shows a "Fire" panel
  (`ui.fireTitle`). Test aids: `--give <item>`, `--eat` prints health for foods that heal. Docs: `running.md`, `content/README.md`.
- Tests: `Needs/FireCookingTests.cs` (7); three `RecipeBookTests` refusals (unknown station, station with no block, bad `makes`); six
  counts updated (nouns 22 to 23, items, foods, recipes).
- Values chosen: id 33; a recipe needs `makes` (`ItemOf` took a noun's first item); the panel prefers a recipe the player can make now;
  on a lit ring crafting comes before the sleep offer; station reach 6 blocks.
- Design against code: E opens the panel only with the crosshair on the ring, while the HUD panel shows within 6 blocks; `--craft
  manzana` resolves to the apple, so the scripted name is `roast-apple`.
- Behaviour: `give: Apple`; `world item_crafted (manzana)`; `craft: roast-apple made`; `eat: Done (RoastApple), hunger 59.4, health 56.7`
  from hunger 40, health 50; `meal_eaten (manzana)`. Screenshots: the Fire panel; the crafting row «manzana asada», "Uses fuego".
- Scripted runs: speech count 642 to 643 lines (the `fuego` card) in 11 runs; r08 gained a recipe row; r26 (and likely r12, r14, r20,
  r21, not opened) shows the Fire panel at the camp with apples in the bag; r01 and r15 PNGs differ by 1 to 2 colour levels. No re-baseline.
- Saves: v1 and v4 fixtures re-hashed; v5 and the old fixtures load.
