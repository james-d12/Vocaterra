# Y11 report: buying the plot

(The agent's report, 2026-11-12, condensed by the orchestrator.)

Branch `Y11-buy-plot`, HEAD `45c5ed97` (on `5c45affd`: `dfc836a6` identity baseline +16 lines, `e699bd87` the milestone, `45c5ed97` the
merge of `feature/settlement` with Y08); worktree `/home/james/Dev/Vocaterra/.claude/worktrees/y11`.

## Built
- The mayor: Tomás is «Alcalde», `npc.villager.role` "The mayor"; directions, bread and weather unchanged. New states `offering_stake`
  (`mayor-stake`) and `offering_plot` (`mayor-sell`), shaped like an order's offer (`GuideSession.LandOffer.cs`, `LandOfferAt` and
  `PlotFactsNow` in `VoxelSession.Land.cs`). From day 2 the talk opens with the stake offer (no stake, no plot) or, with a plot marked,
  the sale; before a class offer, which follows in the same talk (`ReleaseClass`). A request for `terreno`/`tierra` goes through his
  `villager.give` row (`instead`) in every state; «Quiero una estaca» gives the stake.
- `sell_plot`: in `HostEventTable`, the schema, `NpcEffectKind.SellPlot`, the validator (`CheckSellPlot`: the five misses, the two offers
  and offering states, their prompts with confirm and deny, `offer_gone`). The pre-check `PlotMiss` (`NpcHostEvents.cs`) runs in the
  worker on `GuideScene.Land` (`PlotFacts`: owned, marked and still valid, price) and again in the session. World event `plot_bought`
  (an autosave reason).
- Misses: `already_owned`, `no_plot` (a stake in the bag), `no_stake` (none: his answer gives one), `cant_pay`, `no_room`.
- The exchange (`VoxelSession.SellPlot`): `PlotMiss` (with `Inventory.CanExchange`: coins out, kit in), then `OwnPlot()` (re-checks the
  world), then the coins taken, the kit given, `plot_bought`, the arrow to the plot. A refusal at any step leaves coins, bag and land
  as they were; a refusal only the session sees gives `ui.plotSaleFailed`. A repeated «sí», a reload or a second talk meet
  `already_owned`; no second stake while one is held.
- The camp kit: `ItemId.CampKit = 36`, `camp-kit`, stack 1, a tent icon (`IconCampKit`); noun `equipo`, label «equipo de campamento»
  (A2, in the lexicon, no clash with `tienda`).
- The checklist `my-land` («Mi terreno», session 2, last in `session.json`): `stake` counts the bag (`item_picked_up` estaca), `buy` is
  `plot_bought` restored by a new hook `plot-owned`; it ends no session. A marked plot points the arrow at the mayor (`PointAtSeller`);
  after the purchase at the plot's middle, labelled «terreno» (`ui.plotBeacon`; the pointer takes any cell).
- Words: `alcalde`, `terreno` in `cefr/a1.txt` with reasons; not in `lexicon/game.json` because the Wiktionary import has both and
  `GeneratedLexiconTests` refuses duplicates (content/README says so). `prado` fails the gate (in `a2.txt`, not in the lexicon):
  «campo» and «cerca del pueblo» used instead.
- Phrases `quiero-un-terreno`, `cuanto-cuesta-el-terreno` (home `mayor-sell`); focus on both prompts with «sí, por favor», «no,
  gracias». Speech: 44 files in `em_santa`. Docs: content/README, architecture, saves, lessons.

## New Spanish
- `villager_land_stake_offer` «Hola. ¿Quieres un terreno? Necesitas una estaca.»
- `villager_land_stake_given` «¡Muy bien! Toma la estaca. Pon la estaca en el campo.» / «¡Claro! Toma esta estaca. Pon la estaca cerca
  del pueblo.»; `villager_land_stake_gave` «Está bien. Toma la estaca. Pon la estaca en el campo.»
- `villager_land_plot_offer` «El terreno cuesta diez monedas. ¿Lo quieres?»; `villager_land_price` «El terreno cuesta diez monedas.
  ¿Quieres el terreno?»
- `villager_land_sold` «¡Muy bien! / ¡Claro! El terreno es tuyo. Toma el equipo.»
- `villager_land_no_plot` «Primero pon la estaca en el campo.»; `villager_land_no_stake` «Primero necesitas una estaca. Toma.»
- `villager_land_cant_pay` «Necesitas diez monedas. Vende madera a Luz.»; `villager_land_no_room` «Tu bolsa está llena. No hay lugar
  para el equipo.»; `villager_land_owned` «El terreno ya es tuyo.»; `villager_land_no` «Está bien. Vuelve cuando quieras.»
- `villager_land_help_stake` «Puedes decir: «Sí, por favor».»; `villager_land_help_sell` «Puedes decir: «Quiero un terreno, por favor».»
- Prompts `mayor-stake` «¿Quieres un terreno?», `mayor-sell` «¿Quieres el terreno?». The price «diez» is in the text; a test pins
  `PlotPrice == 10` to it.

## The core's readings (all communicated, correctness 1)
At `mayor-sell`/`mayor-stake`: sí → `confirm`; «No, gracias.» → `deny`; «no sé» → `dont_know`; «Quiero un terreno(, por favor)» →
`request_item` {terreno}; «Quiero la tierra.» → {tierra}; «¿Cuánto cuesta el terreno?» → `ask_price`; «Quiero una estaca» → {estaca};
«Hola» → `greet`. Gaps: «Quiero comprar un terreno» (unknown at the land prompts; `state_action` at `villager-open`, nothing sold) and
«Quiero este terreno» (unknown).

## Tests
With models 11,398 passed, 3 skipped; without models 11,351 passed, 50 skipped; build, format, validate, A2 lint clean; goldens unmoved
but the 16 identity lines. Added `Land/PlotPurchaseTests` (37 cases through talk turns). Changed counts and pins (nouns 30 → 32, phrase
rows 182 → 184, pools 396 → 410, host-event rows 25 → 27, the label, the tracker, the objective keys, `save-v8.json`'s phrase records,
`ClassTests` and `LessonFrontendTests` hold a stake on day 2).

## Runs in the game (seed 42, `--dev`; A `--skip-to-session 2`, B loads A's save)
A: the stake offer, «sí», `villager.give_stake`, `estaca 1/1`. B: `--mark-plot 266,165`, ten `--give moneda`, the arrow to «Alcalde»;
«El terreno cuesta diez monedas. ¿Lo quieres?»; «Quiero un terreno, por favor.» → `buy_plot`; `item_taken` moneda, `item_given`
equipo, `plot_bought (261,160)`, `beacon_set (terreno …)`, `objective_complete (Mi terreno …)`; `systems.land` owned 261,160; coins 0;
the kit in slot 4. With 9 coins: «Necesitas diez monedas. Vende madera a Luz.», nothing changed. Two misses at the sale: the repeat and
help lines, nothing changed, still `offering_plot`. Screenshots in `artifacts/y11-check/` of the settlement worktree.

## Scripted runs (both sides from `c381d93a`, with Y08)
r03, r10 the same. r21, r26: «Mi terreno» on the tracker from day 2, save differences (bundle, phrase records, `my-land.stake`), speech
810 → 832 lines. r30: his first talk after the sleep offers the stake; the scripted «No, gracias.» now declines the stake, and the
class offer follows.

## For Y12
`OwnedPlot`, `IsOwned`, the kit given by the purchase; add `camp` and `fire` to `objectives/my-land.json`; the arrow on the plot after
the purchase; the stake stays in the bag after it, and `my-land.stake` counts the bag, so `--own-plot` with no stake leaves the
checklist incomplete.

## Known, not fixed
A fuzz flake in the core (`MutationTagFuzzTests.OneMutation_YieldsExactlyTheNamedTag…`, seed `dTc3W4dw5Cg2`: «túneles unos porque», the
analyzer reads «porque» as a plural noun), also failing on `feature/settlement` without Y11; the full run passed. Asking for the stake
while holding one gets «Toma la estaca» but nothing is given. A full bag at the stake offer has no `no_room` miss (`give_item` has none).
A miss ends the offer; the next talk offers again. The new speech waits for the owner's listening.
