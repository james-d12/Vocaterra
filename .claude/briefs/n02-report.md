# N02 report (Sonnet 5.5, 2026-11-05), saved by the orchestrator

Branch `N02-eat-drink-sprint`, final commit `25639b53`, on feature/adaptive_design at 9a34aa1a.

- Eat: right click with a food (`FoodItemUse`, `WorldInput`, `VoxelSession.EatSelected`/`EatItem` in `Session/VoxelSession.Eat.cs`) through
  `PlayerNeeds.Eat`; at full hunger the food is kept with a hint (`ui.foodKept`); `meal_eaten` with the food's noun.
- Drink: the bottle comes back empty; E at water within reach (`GameCommands.DrinkByHand`, `AimedAtWater`) with nothing held; `water_drunk`
  with `botella`, `pozo` (5 blocks of the well point), `lago` (46 of the lake point) or `río`. Filling the bottle and the no-bottle hint unchanged.
- Sprint: control "sprint" (Left Shift), `SprintHeld`; `sprintSpeedFactor` 1.6 in needs.json (optional in schema); `VoxelSimulation.WalkPermille`,
  `PlayerBody.Step`; a stage's walk factor (0.85/0.9) now slows walking. F1 help rows `eat`, `sprint`. With needs off sprint is free.
- Gathering: N01 already wired the cost; tested from the session; at zero stamina gathering takes twice as long (`gatherSlowFactor` 0.5) and
  never stops. Filling the bottle is no labour.
- Rest: `Needs/FireRings.cs` finds rings by shape (eight stones round an empty middle); lit when a Torch or PostTorch stands in its 3x3 square, so
  the player's ring, the guide's camp ring and the forge count.
- Scripted steps `--eat <item>` and `--drink` (running.md). Keys `ui.foodKept`, `ui.drinkKept`, `control.eat.*`, `control.sprint.*`,
  `key.LeftShift`; Spanish for N05: «No tengo hambre.», «No tengo sed.». No Spanish added to game-v1.
- Checks: `--set-need hunger 40 --eat pan` -> hunger 65, `meal_eaten (pan)`; `--eat fruta` 40 -> 48; full hunger: Kept, no event;
  `--set-need thirst 30 --teleport 234,222 --look 270,-70 --wait 1 --drink` -> thirst 40, `water_drunk (río)`; `--needs off` meters still.
- Scripted runs: all 23 unchanged. Tests 9160 -> 9195 passed, 3 skipped; CI's case passes (45 skipped).
