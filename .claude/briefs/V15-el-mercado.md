<!-- DRAFT (orchestrator, 2026-10-31): only the "What to build" section is written. Before launching V15, write the rest of this
brief on `.claude/briefs/V17-manana-en-el-pueblo.md` as the model (where to work and pushing, read first, rules, the content loop
line, lean context, the learner's route, CI's case, resources, environment, gate, commit, report), fill `@@M58M62@@` from M58's and
M62's reports (the PHRASES.md stand-ins they now grade) and `@@V17@@` from V17's open points (its report:
`.claude/briefs/V17-report.md`), and add the open points of the handover that concern V15: `hola, quiero dos panes` resolves only
the greeting (say what the market does with it); the coin path (0 coins, 3 apples, 2 loaves and the axe after session 8, plus
whatever V17 changed). Delete this comment. -->
You are implementing milestone **V15, session 10 "El mercado"**, of the Vocaterra repository (C#, .NET 10: a Spanish-learning
first-person voxel game on MonoGame, plus a language core). It is the last content milestone of phase B of the village series
(`docs/game/VILLAGE_PLAN.md`, D-game-22).

## What to build (VILLAGE_PLAN 3.4, 3.5 session 10, the ladder's row 10, 3.6 and section 5)
1. **The lesson `el-mercado` "El mercado"**, level `a2`, with the baker, the farmer and the shopkeeper, reached by sleeping into session 10 (`--skip-to-session 10`): the numbers 11 to 20; `cuanto-es-todo` "¿Cuánto es todo?"; `es-muy-caro` "Es muy caro."; `mas-barato-que` (`más barato que`); `puedo-pagar-con-fruta` "¿Puedo pagar con fruta?" (`poder`); `vendo-madera` "Vendo madera."; and **the three times reviewed**, the series' end: `compre-tres-manzanas` "Compré tres manzanas." (past), `quiero-vender-madera` "Quiero vender madera." (now), `manana-vendere-fruta` "Mañana venderé fruta." (future, `tense: fut`; `voy a vender` credited with the `futureNote`).
2. **The error examples, tried on the CLI first** (the core's M56, M58 and M62 have just landed for this lesson): `¿cuánto cuestan el pan?` (`conj.person`), `¿cuánto costa el pan?` (`conj.irregular`), `hay pans` (`noun.number`), `la manzana es muy caro` and `la manzana es más barato que el pan` (`gender.agree`), `quiero tres rojas manzanas` (`word_order`). Restore in `content/PHRASES.md` the stand-in examples M58 and M62 now grade: @@M58M62@@
3. **`offer_item` and `buy`** (3.6, 3.4): the new intent `offer_item` (`[VERB:vender] [NOUN:item]`, `[VERB:tener] [NOUN:item] para (ti|usted)`; keyword `vender`; at the baker and the smith) and the host's `buy` (the NPC pays for what the player sells): the series' one repeatable coin source. **Design it last**, and check it against `state_want` and `state_have` with the lint's confusion check (section 5); the prompt's `allowedIntentIds` is the first lever. Say what a learner sells, at what price, and how often.
4. **The coin path:** from the learner's route, the coins a learner has at session 10, what the market's purchases cost, and how `buy` makes every purchase reachable; a learner with 0 coins must be able to finish the lesson.
5. **`¿Cuánto es todo?`**: the total of a basket (the stall's `sell` with `qty x price`, V03): say what "todo" sums (the items asked for in this talk, or the basket) and test it.
@@V17@@
