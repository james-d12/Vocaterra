# QA report: progression (inventory and equipment series meets play), 2026-11-19

Branch `feature/inventory`, commit `35a392bc` (HEAD of `/home/james/Dev/Vocaterra/.claude/worktrees/inventory`). Area: progression, narrowed to how the inventory and equipment series meets play (`docs/game/inventory-plan.md` section 3.13a; D-game-35). The Release build `src/Vocaterra.Game/bin/Release/net10.0/Vocaterra.Game` was used as it stood; nothing was rebuilt and no file in the repository was changed except this report.

Scratch and evidence folder: `/tmp/claude-1000/-home-james-Dev-Vocaterra/bc0e312e-d400-4d18-b834-c5dbe00e0e36/scratchpad/qa-progression/` (one folder per run; each has `out.txt`, `err.txt`, `exit.txt`, the session log under `session/`, and the screenshots and save where the run took them). Runs used `--seed 42 --mute --no-autosave`, one game at a time, through `scripts/limited.sh`, with `DISPLAY=:0` and the shared models folder.

## What was covered

**Game runs: 20, all exit 0.** The listed folder names are the evidence.

- Buying from Pablo (`--talk shopkeeper`, coins given with `--give coin`): `shop1` (a shirt, 3 coins), `shop2` (trousers 3, cloth 1, ring 6, backpack 10 from 20 coins, four lines), `shop3` (backpack 10 then shirt 3 from 13 coins), `shop4` (four shirts, 3 each, from 12 coins), `bag1` (no coins: «Necesitas más monedas.»), `bag2` (bag full: «Tu bolsa está llena.»).
- Buying from Domingo (`--talk smith`): `shop4` (iron 3, helmet 6, shield 8, torch 1, then a lantern with no coins: «Necesitas más monedas.»), `bag2` (a helmet into a full bag, paid with the last coin stack).
- Crafting at the workbench (`--teleport 177,185`, `--craft` for each recipe): `craft1`, all 21 recipes (sombrero, gorro, camisa, pantalón, chaqueta, abrigo, zapato, casco, coraza, greba, bota, anillo, pulsera, cinturón, collar, amuleto, antorcha, escudo, farol, saco, mochila). Leftovers match the inputs (tela 1, cuero 1, hierro 2 from 20, 10, 20 given).
- Durability: `dur2` (axe selected, trunks in 15 ticks; `--repair hacha` at the workbench: «repair: hacha repaired, still worn: False», wood 5 to 4, wear 0), `dur3` (axe at 347: `tool_worn (hacha)` at tick 1176 on the second trunk, the next trunk in 60 ticks, bare hands). `dur1` was run with the book selected, so the axe was not in hand; it proves nothing and is not a finding.
- Temperature: `temp1` (`--time 1 --weather rain --needs on`, starting clothes, two walks), `temp2` (`--time 13 --weather sunny`, the same walks), `temp3` (`--temperature -20`), `coat1` (`--temperature -6 --equip abrigo`), `coat2` (`--equip abrigo --inventory`), `beach2` (noon sunny, `--pass open`, `--equip abrigo`, at the tour's beach column 4856,-616), `hot1` (`--temperature 30`).
- Progression with the starting clothes: `base-r04` (`--lesson-run first-words --journal --journal-phrases`: 25 of 25 steps, completed in session 1, objectives unchanged), `base-r01` (the camp route: frutas 3/3, madera 5/5, the torch crafted and placed on the ring; water not in this route).
- Saves and reads: the save of each run was read for the inventory, the wear, the coins, `systems.temperature` and the worn list.

**CLI evaluations: none.** This pass was progression in the game; the language path was not part of the brief.

**Not run in this pass** (see "Not covered" at the end): `r03-npcs`, the villager, fisher and baker purchase flows, the session-6 lesson and the smith's axe delivery.

## Bugs

Ordered by severity. Every id is `QA-2026-11-19-progression-<n>`. All five are the game's (`Vocaterra.Game.Core`, and the game's docs for the fifth), not the core's `Vocaterra.Nlp*`.

### QA-2026-11-19-progression-1 (major, game): a successful purchase at the stall gives a free water bottle

Reproduce (the four shirts; the last line is the purchase that goes wrong):

```
GAME --seed 42 --mute --no-autosave --talk shopkeeper <12 x --give coin> --say "quiero una camisa" --say "quiero una camisa" --say "quiero una camisa" --say "quiero una camisa" --inventory --screenshot shot.png --frames 30 --save game.save
```

(`shop4` ran the same lines with a Domingo visit after them; `shop2` shows it again with the backpack as the fourth line.)

- Expected: each of the four lines is a sale, the shirt is given, the coins go, and nothing else happens. A fourth sale is not a request that the NPC has to fail forward on.
- Actual: the fourth line is still a sale (`shopkeeper: Aquí tienes. ¡Gracias!`), and then the fail-forward fires in the same turn: `shopkeeper: Toma la botella. El agua está en el río.` and `shopkeeper: ¿Quieres pan?`. The bag gets a water bottle that nobody asked for (`shop4` save: `bottle` 1). The session log turn record is `"fail_forward":true, "host_events":["stall.sell_goods","stall.give_water"]` with `phrase` kind `fail_forward` (`shop4/session/session.jsonl`, the turn at tick 2742 in `shop2`, and the fourth turn of `shop4`). The shop moves to `offering` and asks about bread.
- Cause: `NpcService.FailForward` (`src/Vocaterra.Game.Core/Npc/NpcService.cs`, the call at about line 791 and the method at about line 891) adds one to `agent.Unproductive` for every line at the patience prompt, sale or not, and resets only when it fires. `game/npcs/shopkeeper.json` has `"patience": {"misses": 4, ...}`. So the fourth line at the prompt in a visit gives the bottle whatever it was, and the count never resets on a success. The rule's own comment says it counts "a line ... that did not ask for anything", which a sale is not.
- Owner: game. Likely files: `NpcService.cs` (the counting), `content/game-v1/game/npcs/shopkeeper.json` (the patience block).

### QA-2026-11-19-progression-2 (major, game): the smith offers the session-6 axe order on the first day

Reproduce (any first visit to Domingo):

```
GAME --seed 42 --mute --no-autosave --talk smith --screenshot shot.png --frames 10
```

- Expected: `game/objectives/axe-handle.json` says `"session": 6` and its own text says "from play session 6". On day one Domingo has no order to offer, and the coins and wood it asks for (two wood and two coins) are not yet part of the learner's economy.
- Actual: the first talk in session 1 is `talk: smith (host turn, smith-order-offer): ¿Quieres un hacha? Necesito dos bloques de madera y dos monedas.` The session log has `order_offered` with detail `axe-handle` at tick 2992 of session 1 (`shop4/session/session.jsonl`, and `shop4/out.txt` line 72).
- Cause: `VoxelSession.OrderToOfferAt` (`src/Vocaterra.Game.Core/Session/VoxelSession.cs`, about line 764) picks the first undone order whose `DeliverTo` is the NPC, and never asks `Spec.ShownIn(Day)`, which is the check that holds the session number. `Shown(spec)` (line 120) returns `HasOrder` for an order, so the `session` field gates nothing. This looks like a side effect of the issue #54 offer change; it was not in the plan.
- Owner: game. Likely files: `VoxelSession.cs` (`OrderToOfferAt`, `Shown`), `GuideSession.OrderOffer.cs`.

### QA-2026-11-19-progression-3 (major, game): a coat worn over the starting shirt gives no warmth

Reproduce:

```
GAME --seed 42 --mute --no-autosave --temperature -6 --needs on --teleport 177,185 --look 0,0 --equip abrigo --wait 2 --shot coat.png --wait 3 --screenshot shot.png --frames 30 --save game.save
```

Then the same with `--inventory --screenshot shot.png` in place of the cold steps (`coat2`).

- Expected: a coat (warmth +5, `game/items.json`, plan 3.13a: "a jacket or a coat lifts it to cool") raises the feeling from about -2 to about +2 at a -6 hold, so the cold line does not show. The worn total at the inventory should move from "calor +4" to about "calor +8".
- Actual: the console says `equip: coat worn in cosmetic body`. The screenshot (`coat1/coat.png`) still shows «Tengo frío. / I'm cold.» with the snowflake icon. The inventory (`coat2/shot.png`) shows the shirt still in `ropa / el cuerpo`, the coat in `apariencia / el cuerpo` (red), and "calor +4". The saved `worn` list has `{"group":"cosmetic","slot":"body","item":"coat"}` next to the shirt.
- Cause: the coat is clothing (`tags: clothing`, `wear: body`). The clothing body slot is taken by the shirt, so the equip rule puts it in the first free slot that takes it, which is the cosmetic group. `content/game-v1/game/equipment.json` gives the cosmetic group `"statsApply": false`, so its items add nothing. The learner cannot make the coat count without first taking the shirt off, and the game does not say so.
- Owner: game. Likely files: `content/game-v1/game/equipment.json` (the cosmetic row), `src/Vocaterra.Game.Core/Items/Gear/Equipment.cs` (the slot choice), `EquipmentStats.cs`.

### QA-2026-11-19-progression-4 (major, game): a rainy night with the starting clothes is not cold

Reproduce:

```
GAME --seed 42 --mute --no-autosave --time 1 --weather rain --needs on --teleport 177,185 --look 0,0 --wait 20 --shot cold.png --walk-to 205,185 --walk-to 177,185 --screenshot shot.png --frames 30 --save game.save
```

Compare with `--time 13 --weather sunny` (`temp2`, the same walks).

- Expected: `docs/game/inventory-plan.md` 3.13a: "a rainy windy night about -2 (cold: walk 85%)" with the starting clothes giving +4, so "a rainy night stays just cold", and "a fair noon feels about 18". The cold line «Tengo frío.» shows on entering the band, and the walk is slower.
- Actual, with the saved place temperature (`systems.temperature.felt` is the place's value; checked by holding `--temperature -20`, which saves exactly -20.0):
  - Rain at 1 o'clock: place -0.4 °C, so the feeling is about +3.6 °C (the cool band, walk factor 1.0). No «Tengo frío.» in `cold.png` or at the end (`shot.png`). The two walks take 401 and 387 ticks.
  - Sunny at 13 o'clock: place 18.3 °C, feeling about 22.3 °C. The two walks take 401 and 387 ticks, the same as the rain.
  - The very-cold band does slow the walk: at `--temperature -20` the walks take 611 and 595 ticks (`temp3`, a ratio of about 1.52, which is 1/0.65). So the mechanism works; the shipped numbers just never reach the cold band on an ordinary night.
  - The noon feeling is about 4 °C warmer than the plan's 18, and the rain night about 5 °C warmer than the plan's -2.
- Owner: game (data). Likely files: `content/game-v1/game/weather.json` (the rain kind's cloud, precipitation and wind channels), `content/game-v1/game/temperature.json` (the climate curve and the weather coefficients). The plan's numbers and the shipped numbers should be reconciled; I did not check the code path for the weather channels.

### QA-2026-11-19-progression-5 (minor, game docs): the scripted-runs note says crafting the torch spends three wood

`docs/game/scripted-runs.md` (the `CAMP` note, about line 216) says "crafting the torch spends three wood, so the checklist now finishes". `game/recipes.json` says the torch takes two `madera`, and `base-r01` spent two (five gathered, three left). The checklist is stock-based (`docs/game/running.md`, "completed by the game's events and the inventory"), so the drop from `madera 5/5` to `madera 3/5` after the craft is by design; only the number in the note is wrong. Owner: game (docs).

## Observations

These are not bugs in the brief's sense; they would trouble a learner or a tester.

- **The camp checklist goes back.** After the torch craft the checklist reads `madera 3/5` under a toast that said `Done: madera 5/5` a moment earlier (`base-r01/out.txt`). It is by design (stock-based), but a learner will read it as a regression.
- **A dead-end line after a sale.** After a purchase, "sí, por favor" gets `No te entendí. ¿Me lo dices otra vez?`, though no question was asked (`shop1`, turn 2).
- **The axe offer is dropped silently.** Domingo's pending offer goes to `idle` when the learner buys iron first (`shop4`, turn 5). The offer text says it waits for the next talk; here it was not asked again on that visit. Minor, and it may be the intended "any other line leaves the offer" rule.
- **`--give` gives no reason when the bag is full.** `bag1` printed `give: Coin, bag 0` for six coins given into a full bag, with no refusal. The coins were simply not in the save. A tester cannot tell that from a game fault. Owner: game (the give aid's output).
- **The noon beach is not hot.** At the tour's beach column (4856,-616 with `--pass open`, `beach2`), the place is 15.8 °C, so the feeling is about 19.8 °C with the starting clothes and no «Tengo calor.». Holding the temperature at 30 does show «Tengo calor. / I'm hot.» (`hot1/hot.png`). A beach at noon in a coat will not reach the "very hot" band (35) on the shipped numbers; the brief's expectation needs the design answer.
- **Full bag: the paid stack frees a slot.** With 30 of 30 slots used, Domingo sells a helmet for six coins: the coin stack is spent first and the helmet takes its slot. Pablo refuses a hat for two coins with «Tu bolsa está llena.» (`bag2`), since the coins stay in one stack. Both are consistent with each other; recorded because it is an edge a learner could meet.
- **The crafting panel's footer sits on the bottom edge** at 1280 by 720 (`dur2/shot.png`, `craft1/shot.png`): "C or Esc: close · click a row: make" touches the window's bottom line. Cosmetic; the UI pass should judge it.
- **The worn-out axe's wear bar** renders as an empty dark bar in the bag (`dur3/shot.png`); I could not tell from one picture whether a full-wear bar should read as empty.
- **The cold line and the icon work.** At a -20 hold, «Tengo frío. / I'm cold.» shows over the snowflake icon above the health bar (`temp3/cold20.png`), and the hot line shows the same way (`hot1/hot.png`).

## What works (checked, no bug)

- Prices as in section 3.13a: Pablo's shirt 3, trousers 3, cloth 1, ring 6, backpack 10, iron (Domingo) 3, helmet 6, shield 8, torch 1, and the coins taken match the given coins in each run. A lantern with no coins: «Necesitas más monedas.».
- Two purchases in one visit are each paid and given in full (`shop3`: backpack then shirt, 13 coins, no coins left over).
- All 21 recipes craft at the workbench with the right inputs (see "What was covered").
- The axe: 15 ticks a trunk in hand against 60 bare-handed (`dur2`, `dur3`). It wears out at 350 (`tool_worn`, tick 1176 in `dur3`); the next trunk takes the bare-hands time. Repair at the workbench costs one wood and sets the wear to 0.
- The starting clothes: `base-r04` finishes `first-words` in session 1 with 25 of 25 steps; `base-r01` finishes the trunks, the berries and the torch on the ring with the starting clothes (its bag was not saved, so the bag's contents are not checked).
- The very-cold walk factor (1.52 slower, `temp3`) and the cold and hot lines (`temp3`, `hot1`).

## Not covered

- **`r03-npcs`** and the rest of the opening runs (`r02`, `r17`, `r40`): not run; the lesson and the camp route are covered, the NPC dialogue of r03 is not.
- **The villager, fisher and baker purchases**, the `gente-del-pueblo` lesson and the smith's axe delivery at session 6: not reached (the axe offer appears on day one, bug 2; the delivery was not tested).
- **Two items in one line from Pablo** ("quiero una camisa y un sombrero") and **a too-few-but-not-zero purchase**: not run. Pablo's "not enough coins" was seen only with no coins at all.
- **The HUD line «Necesito arreglar la herramienta.»** was not captured on screen: the `tool_worn` event is logged (`dur3`), but the line is not in any picture (the inventory covers the HUD in `dur3/shot.png`). The line's path is the cold's, which does show.
- **The repair row** in the crafting panel before the repair: the only panel shot was taken after the repair (`dur2/shot.png`), so the row was not seen.
- **Fire warming:** not tested. A night with the starting clothes is not cold (bug 4), so a fire has nothing to warm; a `--temperature` hold replaces the place's value and the fire term with it, so it cannot show the fire either.
- **The coat lifting the feeling from cold to cool:** not shown, because the coat lands in the cosmetic layer (bug 3). The lift can only be checked once that is fixed.
- **Thirst in the heat:** the "drinks faster" effect was not measured (no run compared the thirst meter at a hot band against a warm one).
- **The `--equip` path for a second clothing item** and the shift-click path in the inventory: not tried.
- **A sandbox note:** two status checks were refused by the sandbox's command classifier, because I had typed a stray path into them. Nothing was run or changed by those commands, and they were not retried.
