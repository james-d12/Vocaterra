# I10b report (the QA passes' fixes and colder nights)

The agent's report, saved by the orchestrator (2026-11-19). Sonnet 5.5 at high effort; brief `i10b-qa-fixes.md`. Gated at `31bff0ab`:
13,871 passed, 0 failed, 9 skipped (+75); timing 23; format clean; content validate and the A2 gate clean.

Commits (each naming its QA id): 98315a73 ui-2, eb620f43 ui-1, fdcdf3aa ui-5, 112f290a save-1, c6923b3f ui-4, 79eb171a progression-1,
baf3aebf progression-3 and ui-3, be14fc5d progression-4, 05ed62e9 merge (saves.md: I10a's version with this paragraph), d6fac665.

- **ui-1:** the crafting panel is never taller than the window: a window of rows, wheel (2 rows a notch) and Up/Down/PgUp/PgDn, a track and
  gold thumb, header and footer on screen, scroll reset on open (`CraftingLayout` VisibleRows/Scroll/MaxScroll/Track/Thumb,
  `CraftingScreen.Scroll`, `CraftingPanel`).
- **ui-2:** a `light` chip (icon and number, no Spanish noun); the tight layout's chip width 52 to 46 to keep the panel's size.
- **ui-3:** `WearFromStart` leaves a refused item in the bag (a full bag: BagFull).
- **ui-4:** `BagLabelFitter`: the shortest distinguishing prefix (3 letters or more), else a smaller font (three 2 px steps from 16).
- **ui-5:** `GiveLine` prints the catalogue name and "not given, the bag is full".
- **save-1:** an item the bundle lacks (bag or worn) loads empty; a load that repaired anything skips the digest and logs `save_repaired`
  per change (added to `WorldEvents.All`); otherwise a digest mismatch is still refused; a worn item that does not fit its group
  (`EquipmentData.Fits`) goes to the first empty bag slot; a slot with no count is refused naming the slot; refusals are sentences.
- **progression-1:** `NpcService.ExchangeMade` judges the turn's final host events; an exchange no miss answers resets the unproductive
  count.
- **progression-3:** `Equipment.FindSlotFor` considers only groups with `statsApply` true (empty first, then a worn one swapped); the
  cosmetic layer only by dropping on its cell; `Equipment.RefusalFor`; shift-click, `--equip` and `EquipmentContainer.Claims` share it.
- **progression-4:** `temperature.json` `day.amplitudeTenths` 60 to 70, plains `daySwingPermille` 1300 to 1250. Feel (starting clothes /
  coat): camp rain night 1.2 cold / 5.2 cool, clear night 9.2 cool, clear noon 23.2 / 27.2 warm; village rain night -0.5 / 3.5, noon 24.9 /
  28.9 hot; beach noon 25.6 / 29.6 hot, rain night 0.8 / 4.8; plains noon 24.8 / 28.8 hot; peak clear night -3.2 / 0.8 cold, rain night
  -16.7 very cold; cave 10 always. QA's "not cold" was the 15 s ramp; steady state was already cold (2.2) before.

Tests: crafting scroll theory (7 sizes, 3 scales, 24 and 40 rows), `CraftingPanelScrollTests`, `BagLabelFitterTests`, `GiveLineTests`,
`SaveRepairTests`, `FeelTargetsTests`, the light chip, NPC patience, the coat swap and the cosmetic drop, equipment simulation; changed:
`StatLinePlacerTests`, `StatDisplayTests`, `EquipmentSimulationTests` (refused items stay in the bag), `TemperatureFeelTests` (new numbers),
`ClimateTemperatureTermTests` (renamed), two NoLiteralText entries. Doc lines in running.md and scripted-runs.md fixed by the orchestrator.
