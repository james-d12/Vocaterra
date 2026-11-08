# Activities and the core systems: the W series

The W series implements repeatable activities in the existing village. The
[Adaptation Design Document](design/sources/Vocaterra_Adaptation_Design_Document.docx) specifies the intended mechanics:
section 9 covers player needs, section 10 the bread procedure, section 15 the delivery sequence and section 18 the open decisions.
This plan provides milestone briefs and the later activity roadmap.

**Status:** proposed, with direction confirmed in D-game-23 and D-game-24. Complete the queued language follow-ups, then N01–N07
and the needs playtest before the bread pilot. W01 supplies `item_crafted` before N07. N01 owns save version 6 and its `systems`
map; W06 adds procedure state; W15 adds cooked dishes and buffs to the needs system. Register milestones in
`implementation-plan.md` section 4 and resolve their required decisions before implementation.

**Branch:** `feature/activities`, from `main`; milestone branches `W<nn>-<name>` are gated into it.
**Commit prefix:** `W<nn>:`.

Build one bread procedure with Luz and test it across three sittings. Expand to repair, meals, directions, farming, orders and
prices after the pilot passes. Land, storage, trust, gear and new NPCs depend on later decisions.

## 1. Purpose

Useful activities repeat Spanish in context. Lessons introduce the language; actions provide practice; demonstrated understanding
reduces support. Keep instant crafting for routine items and use procedures where their actions teach useful language.

## 2. What the plan builds on

Implementation foundations (paths are relative to the repository or the named project):

- **Lessons are data** with eight step types; a `do` step waits for a world event with an exact NPC and detail
  (`content/schema/lesson.schema.json`, `Language/Lessons/LessonRunner.cs`). The runner is plain state: the session feeds it turns and
  world events and it reports `LessonEvent`s.
- **The support ladder** is per phrase: `SupportLevel` English and card, card and audio, audio only, none
  (`Language/Phrases/PhraseProgress.cs:26`). A phrase steps down on credited uses past D-game-6's gaps and up after "no sé". Phrase records
  count `Heard` and `Answered` (comprehension) apart from production.
- **World events and triggers** (`Event/WorldEvent.cs`, `Event/Trigger.cs`) drive tips, lessons, objectives and the session log. A craft
  raises no world event today (`Session/VoxelSession.cs:354`).
- **Recipes** are data (`game/recipes.json`, one recipe) and crafting is a click on a row (`Items/Crafting/`). The guide answers
  `ask_craft` with ingredients, numbers, the station and what is missing (`Npc/NpcService.Guide.cs`).
- **Items** are a code enum (`Items/ItemDefinition.cs`, ten kinds); saves store them by enum name (`"item":"Wood"`, save version 5);
  icons are atlas cells painted in code (`BlockAtlasLayout`, `HeldArm`). Only trunks and berry bushes can be gathered; only wood and
  torches placed. The axe's speed is a special case (`VoxelSimulation.GatherTicksOf`, `AxeSpeedUp = 4`).
- **NPCs are data** (`game/npcs/<id>.json`: prompts by state, the opening, the voice, focus rows). Host events map to the kinds
  `give_item`, `take_item`, `sell`, `buy`, `point_at_noun`, `point_at_place`, `point_at_slot` (`game/host-events.json`,
  `Npc/HostEventTable.cs`). A `sell` has one fixed item, so an NPC sells one thing. The stall's fail-forward is code by id
  (`NpcService.ShopkeeperPatience`). The core's agents never branch on a slot value.
- **Orders** are objectives with `deliverTo`, `deliver`, `session` and `lesson` (`Session/ObjectiveBook.cs`). The review deck picks the
  most overdue phrases (`Language/Phrases/ReviewDeck.cs`, `Select`).
- **Speech**: every spoken NPC line is a pre-rendered WAV (two speeds) in the NPC's voice, chosen by the pool's prefix (`<npc>_*`). Lines
  rendered at run time are text-only by design (`Sound/NpcVoice.cs`).
- **The core reads the imperative** (a clause-initial `corta` is the second-person imperative; the lexicon has the imperative forms), but
  lessons and the book teach only `pres`, `pret`, `ir-a`, `fut` and `tener-que`.
- **Words**: the bundle's level lists are short (397 A1 and 663 A2 lemmas); common kitchen nouns (sartén, olla, cuchara, tenedor, harina,
  cebolla) are not in the lexicon (review, 1.3).
- **The learner model** of the core is one copy per NPC conversation and nothing reads it (`NpcService.Save.cs`).

## 3. The direction

### 3.1 Principles

1. **Lessons introduce, activities repeat, the learner record fades the help.** Each activity enters through a lesson (or at its first
   rung, which is a guided walkthrough), and then is played again for its own reward.
2. **Nothing is gated behind language or voice** (D-game-8, D-game-9). The dish is always made, the tool is always mended, the arrow is
   always one key away. Understanding makes things better and faster, never possible.
3. **Credit is spaced.** Every activity credits the learner record through D-game-6's gaps, so repeating an activity many times in a row
   cannot farm progress.
4. **Every moment is a world event, every system is data.** A data file with a schema and a validator rule; a world event for each
   meaningful moment; host-event kinds and fail-forward host turns for NPC actions; the core sees opaque strings (ADR 0009).
5. **Recorded lines come from a closed bank, and reuse wins.** An instruction is a phrase-sheet row with its recording; recipes reuse
   instructions; a report shows each recipe's new lines.
6. **Engine-free and deterministic.** Game.Core reads no clock and draws no `System.Random`; timers run in stored time or play sessions;
   draws come from `WorldRng`. New state goes into the save with a version or a section of its own.
7. **Everyday Spanish first**, Latin American neutral (core ADR 0005): papa, jugo, frijoles, aguacate. Specialist words only where an
   activity needs them, each with a lexicon entry and a reason.
8. **Grow inward.** Activities happen at places that exist: the bakery and its oven, the smithy and its forge, the guide's camp fire, the
   player's own fire, the wheat field, the square.

### 3.2 Decisions this plan assumes

The plan is written on the review's recommendations. Each is in section 12 for the owner; a different answer changes the milestones named.

| Decision | Assumed | If the owner says otherwise |
| --- | --- | --- |
| Needs | five needs, staged effects and death with a cost; N series before the pilot (D-game-24) | resolve the detailed options in design section 18 |
| Combat | none; gear is for places | stage 5 gains combat milestones; W26 changes |
| Tool tiers | hands, stone, iron (from the smith) | W02's table grows; ores need terrain work |
| Cast | the seven NPCs; new ones only after a voice trial | stage 5 adds NPC milestones earlier |
| Land, storage, trust | after the pilot (stage 5) | W22 to W25 move up |
| Items as data | at W12, before stage 3 (not now) | W12 moves to stage 1, or never (new items then go into the enum, as V03 did) |
| The tú imperative | comprehension only; session 11 at `a2` | teaching production is a later W09 decision |
| First activity lessons | new sessions 11 to 13; sessions 1 to 10 untouched | weaving activities into earlier sessions moves their goldens and runs |
| The station view | an overlay panel, as crafting is | an in-world kitchen is a project of its own (block states, animation) |

### 3.3 What this plan does not do

No combat, no co-op, no new terrain, site or road, no new NPC before a voice trial (and none before stage 5), no change to the lessons of
sessions 1 to 10, no speech synthesised at run time, no unrestricted dialogue. The instant torch craft of session 1 stays as it is.

## 4. The design

### 4.1 Procedures: the data

Use design sections 10.7 and 10.16 for the procedure, station, action and phrase definitions. W05 provides schemas and loaders for
`game/procedures/<id>.json`, `game/procedures.json`, `game/stations.json` and `game/actions.json`. The bread procedure has six actions,
two chunks and one goal; three ingredients (flour, water and wood); and bowl/spoon fixtures. Flour and water have a seeded order per
bake. Offer a verb choice only where the actions have distinct results. The nine `does` rows carry instruction text and audio,
without a learner intent or prompt. The recipe level is `a2`.

### 4.2 Procedures: the runtime

`Activities/ProcedureRunner` is deterministic plain state. It accepts actions and reports events; the host owns time and seeded draws.
Design sections 10.10–10.15 define support, spacing, folding, transactions and mastery.

- A first successful rung-1 act moves the instruction to rung 2 without comprehension credit. First-attempt, unassisted acts at
  rungs 2 and 3 record comprehension and eligible instruction-spacing credit. Acts never add production, transfer or noun credit.
- Help restores support; repeat and slower are listening aids. A wrong action changes no inventory and stays outside production slips.
- Bread yields two loaves, plus one for at least five first-attempt successes. Help is allowed for the food reward.
- Exchange all ingredients and outputs atomically on completion. A full bag leaves `ReadyToCollect`; load/resume cannot duplicate output.
- Fold chunks when all constituent rows reach rung 4; introduce the chunk on the next bake. Credit the displayed instruction only.
- Mastery requires the goal at rung 4 and two unassisted completed sittings. Offer a one-click bake and retain guided practice.

### 4.3 The station view

Build `Activities/StationScreen`, `StationPanel` and `UI/Station/StationLayer`, with layout delegates as used by crafting.
Design section 10.8 defines entry priority, fixture layout, inventory paging, target selection and speech recovery. The station is
an overlay; needs pause while it is open. All actions are available by keyboard. Scripted steps are `--station`, `--act` and
`--proc-help`. Follow design decision 19 for text fallback and listening evidence.

### 4.4 The teacher in the loop

Design section 10.14 defines the teaching prompt and host kinds: `procedure_repeat`, `procedure_slower`, `procedure_help`,
`give_missing` and `teach_recipe`. Enter on `<npc>.teach_start` and leave on `<npc>.teach_end`. V29 supplies NPC reply-pool declarations;
W08 follows V29 and the spoken-lines study. Luz supplies missing ingredients once per in-game day. Persist the grant state and
resolve the introductory lesson exception in design decision 20 before implementation.

### 4.5 The tú imperative

The pilot asks learners to understand commands through the support ladder. It does not assess imperative production or add a book
column. W09 is outside the pilot path and requires a later decision on taught forms and level policy.

### 4.6 Saves

- **Version 6 (N01):** an optional `systems` map. Versions 1–5 initialise full needs. W06 adds `systems.procedures` and preserves needs;
  existing version-6 saves initialise absent procedure state. Persist in-progress steps, drawn order, attempts and completion state.
- **Version 7 (W12):** data item IDs and wear. Migrate every enum item present by then, including N07 and W10 additions, and verify
  every supported save version. Each later system has a versioned entry in `systems`.

### 4.7 World events added

| Event | Raised by | Detail | Milestone |
| --- | --- | --- | --- |
| `item_crafted` | `VoxelSession` (a craft, an instant procedure) | the output's noun; amount: the count | W01 |
| `procedure_started`, `procedure_step`, `procedure_done`, `procedure_mastered` | `ProcedureRunner` | 4.2 | W06 |
| `tool_worn`, `tool_repaired` | the simulation, the repair | the tool's noun | W13, W14 |
| `meal_eaten` | `VoxelSession` | the food's noun | N02; reused by W15 |
| `crop_planted`, `crop_ready`, `crop_harvested` | `Activities/Crops` | the crop's noun | W18 |
| `board_read`, `order_posted`, `order_taken` | `Activities/OrderBoard` | the order's id | W19 |

Each joins `WorldEvents.All`, so tips, lessons and objectives may wait for it, and the schemas that list event names.

## 5. Milestones

| ID | What | Depends on | Size | Model | Status |
| --- | --- | --- | --- | --- | --- |
| W00 | Register milestones and resolve required decisions | design section 18 | | orchestrator | done (2026-11-04: game plan section 4.9; section 18.2 adopted provisionally, D-game-23) |
| **Stage 1: foundations** | | | | | |
| W01 | `item_crafted`: a craft is a world event | | S | Haiku | done (2026-11-05, gated at 56f88b4b; game plan section 5) |
| W02 | Tools and materials: a material per block, a tool spec per item, the axe's special case replaced | | S/M | Haiku | done (2026-11-07, gated at `2bd4728e` on `feature/bread-pilot`, 9855 passed; game plan section 5) |
| W03 | Fail-forward as data: `patience` in the NPC file; the stall's rule moved there | | S/M | Sonnet 5.5 medium | done (2026-11-07, gated at `bc12153e` on `feature/bread-pilot`, 9901 passed; game plan section 5) |
| W04 | Price lists: `sell` and `buy` by the item the learner names; `<npc>.dont_sell` | | M | Sonnet 5.5 medium | done (2026-11-07, gated at `fe80787d` on `feature/bread-pilot`, 9938 passed with V31; game plan section 5) |
| W04b | The stall's price list (the owner, 2026-11-07, D-game-23 item 5): flour (and any new goods) sold by the name the learner says, water and bread still free (owner, 2026-11-08, D-game-23 item 6), «No vendo eso.» for anything else, replacing W10's fixed flour sale and `instead`; a content milestone (content loop, recordings, scripted runs that change at the stall explained) | W04 | S/M | Sonnet 5.5 medium | authorised (owner, 2026-11-07), on `feature/bread-pilot` |
| **Stage 2: procedures, and bread with Luz** | | | | | |
| W05 | Procedures, stations and actions as data; the `does` rows of the phrase sheet; the validator's rules | needs playtest (lifted for W05 to W07: D-game-23, 2026-11-06) | S/M | Haiku | done (2026-11-06, gated at 28c5becf on `feature/bread-pilot`, 9614 passed; game plan section 5) |
| W06 | The procedure runner: the ladder up the recipe's tree, credits, help, rewards, mastery, events; `systems.procedures` in save version 6 | W05, N01 | M/L | Sonnet | done (2026-11-06, gated at b1579284 on `feature/bread-pilot`, 9645 passed; game plan section 5) |
| W07 | The station view: model, panel, layer, layout, fixture icons, scripted steps | W06 | M | Sonnet | done (2026-11-06, gated at 23d139e3 on `feature/bread-pilot`, 9737 passed; game plan section 5) |
| W07b | W07's small points: the station's name as the panel's title; help selects what it highlights; the mastered recipe's one-click bake offered at the station (design 10.15) | W07 | S | Haiku | done (2026-11-06, gated at 1d511d76, 9758 passed; game plan section 5) |
| W08 | The teacher in the loop: the `teaching` state and prompt, the three host kinds, the missing ingredient, learning a recipe by asking | W06, W07, V29, spoken-lines study | S/M | Sonnet 5.5 medium | done (2026-11-07, gated at 5091891e on `feature/bread-pilot`, 9794 passed; game plan section 5) |
| W09 | Imperative production in lessons and the book | separate decision | S | Haiku | outside pilot |
| W10 | Session 11 "En la panadería": the lesson, the `pan` procedure, flour, Luz's teaching, the rows and recordings, the checks, run `r27-bread` | W07, W08; W04 only for multi-item vendor pricing | M | content (decision l) | done (2026-11-07, gated at 366a6038 on `feature/bread-pilot`, 9852 passed; game plan section 5) |
| W11 | Stage 2's documents and runs; the pilot measure (telemetry, the session summary, `playtest.md`); run `r28-bread-again` | W10 | S | Haiku | **owner's go or no-go** |
| **Stage 3: crafting, repair and meals** | | | | | |
| W12 | Items as data (C09 revisited): `game/items.json`, icons by key with a fallback, save version 7 (names and wear) | W02 | M | Sonnet | |
| W13 | Durability: wear per tool slot, a worn tool at hands' speed (never lost), `tool_worn` | W12 | S/M | Haiku | |
| W14 | The axe's handle with Domingo: "Se rompió el mango", the `mango` procedure at the forge, `repair`, run `r30-repair` | W08, W13 | M | content (decision l) | |
| W15 | Meals and the camp fire: cooked dishes and buffs using the N-series needs and eating rules; session 12 "En el campamento" with Luna; dinner before sleep; run `r31-dinner` | W08, W12, N07 | M | Sonnet, then content | |
| W16 | Stage 3's documents and runs | W14, W15 | S | Haiku | |
| **Stage 4: the principle spread** | | | | | |
| W17 | The fading beacon: an NPC's directions hold the arrow at the later rungs; arriving unaided is credited; run `r32-no-arrow` | W06 | S/M | Haiku | |
| W18 | Farming with León: crops that grow by play session; session 13 "En el campo"; run `r33-farm` | W06, W07, W12 | M | Sonnet, then content | |
| W19 | The notice board: order templates, a seeded generator weighted by what is due, written notices; run `r34-board` | W12 | M | Sonnet | |
| W20 | Market prices by session | W04 | S/M | Haiku | |
| W21 | Stage 4's documents and runs | W17 to W20 | S | Haiku | |
| **Stage 5: held for the pilot's data** | | | | | |
| W22 to W29 | Land and plots; frontier claims; storage; trust and "¿Lo de siempre?"; gear for places and the tailor; the healer and foraging; tenants and building to a description; fishing with Mar | section 6 | L | | held |
| **Proposed (owner, 2026-11-06)** | | | | | |
| W30 | The recipe book: a second book the player carries beside the journal, given by Luz for the first bread, that holds the known procedures' written steps and can be set down open beside a station while cooking (as Kingdom Come: Deliverance's alchemy book) | W07, W10; W08 for recipes learned by asking | M | Sonnet | proposed, not authorised |

**Order.** Complete language follow-ups, N01–N07 and the needs playtest first; W01 runs before N07. U01 and U02 precede the pilot
if accepted. The pilot path is W05, W06, W07, W08, W10 and W11. W02 and W03 are outside this path; W04 is conditional on the vendor
decision; W09 is later work. W11 uses three tester sittings and delayed recall, then an owner go/no-go. Stage 3 adds W12–W16; stage 4
adds W17–W21. W12 needs W02. U03 and U04 follow the final recall. Gate each milestone and observe the repository’s machine limits.

## 6. The milestones in detail

Every milestone meets the definition of done (`docs/game/implementation-plan.md` section 6) and, while iterating, the fast tier
(`scripts/test.sh fast`, through `scripts/limited.sh`), with the whole suite once before the report. A milestone that edits `content/game-v1`
runs the content loop (`scripts/content-loop.sh --grammar --speech`, V18), re-records goldens that moved, puts the new bundle hash into
`tests/Vocaterra.Game.Core.Tests/Fixtures/save-v1.json`, and re-baselines the scripted runs it moved. New tests mirror the sources
(`tests/Vocaterra.Game.Core.Tests/Activities/`, `tests/Vocaterra.Game.Tests/UI/Station/`).

### Stage 1: foundations

**W01: `item_crafted` (S, Haiku).**
- *Why.* No lesson or objective can wait for a craft today; every crafting design needs it.
- *Code.* `Event/WorldEvent.cs`: `WorldEvents.ItemCrafted = "item_crafted"`, appended to `All`. `Session/VoxelSession.cs`, the
  `SimulationEventKind.ItemCrafted` case: log the world event with the output's noun (`Nouns.ForItem`) and the amount (as pickups do), after
  `LearnItem`.
- *Data.* Any schema that lists world events takes the name.
- *Tests.* A torch craft logs `world item_crafted (antorcha)`; an objective step with that trigger counts it; a lesson `do` step on it ends.
- *Done when* the torch craft in a scripted run prints the event, and no other baseline moves.

**W02: tools and materials (S/M, Haiku).**
- *Why.* The axe is a special case; durability, repair and new tools need a rule.
- *Code.* `Voxel/BlockDefinition.cs`: a `Material` per block (an enum in code: none, wood, plant, stone, soil, water; trunks wood, the berry
  bush plant; blocks stay code, as C09's assessment advised). `Items/ItemDefinition.cs`: an optional `ToolSpec(Material Class, int Speed,
  int Yield = 1)`, the axe `(Wood, 4)`. `Simulation/VoxelSimulation.cs`: `GatherTicksOf` divides by the held tool's speed when its class
  matches the block's material; `AxeSpeedUp` goes. Hands stay speed 1 on wood and plants, so sessions 1 to 6 hold.
- *Tests.* A trunk takes 60 ticks by hand and 15 with the axe (the regression); a tool of another class changes nothing; the HUD's bar
  reads the same number.
- *Done when* every scripted run's baseline is unchanged.

**W03: fail-forward as data (S/M, Haiku; Sonnet if `NpcService` resists).**
- *Why.* Every new system needs "a misunderstanding never dead-ends"; today it is the stall's code.
- *Data.* `game/npcs/<id>.json` (schema 3): an optional `patience`: `{ "misses": 4, "afterHelp": 2, "event": "stall.give_water", "once":
  true }`, the shopkeeper's values today.
- *Code.* `NpcService.ShopkeeperPatience` becomes a rule read from `NpcRoster`; `StallPatience` and `StallPatienceAfterHelp` go.
  `GameDataValidator`: the event exists in `host-events.json` for that NPC.
- *Tests.* The stall's existing tests pass unchanged; a test NPC with a rule fails forward the same way.
- *Done when* the stall behaves exactly as before and the rule is data.

**W04: price lists (M, Haiku).**
- *Why.* A `sell` has one fixed item, so no NPC can sell two things; the bakery needs bread and flour, and the design plan's merchant sells
  "general goods". The handover's "the stall answers any request with the bottle" is the same gap.
- *Data.* `host-events.json` (schema 3): `sell` and `buy` may take `"slot": "item"` and `"prices": { "pan": 2, "harina": 1 }` instead of
  one `item` and `price`. Existing entries keep their form.
- *Code.* `Npc/HostEventTable.cs`, `VoxelSession.ApplyHostEvents` and the worker's check (`NpcService`, where `cant_pay` is decided): read
  the verdict's `item` slot, find its price, sell `qty` of it; an item not on the list raises `<npc>.dont_sell` as a host turn (the
  agent's line: "No vendo …"), as `wont_buy` does for `buy`.
- *Tests.* Two items at one NPC; an unlisted item; too few coins; `qty`; the existing fixed sells unchanged.
- *Done when* a test NPC sells two items by name and every existing sale behaves as before.

### Stage 2: procedures, and bread with Luz

**W05: procedures, stations and actions as data (S/M, Haiku).**
- *Data.* The four file kinds of 4.1 and their schemas; the bundle gets empty lists (`procedures.json: []`, no stations), so it loads
  unchanged. The phrase sheet's `does` kind (`PHRASES.md` columns, `phrases.json`, `phrases.schema.json`).
- *Code.* `Activities/ProcedureBook`, `StationBook`, `ActionTable` (loaders with records, as `LessonBook` and `RecipeBook`); `GameData` gains
  them; `PhraseSheet` reads `does` rows.
- *Validator rules* (`GameDataValidator`): every `what`, `where`, `from` and `with` is a catalogue noun (an item for what is held, a fixture
  for a station's things); every act's verb is in `actions.json` for its pair; every verb lemma is in the lexicon with its tú imperative;
  every row is a `does` row whose line is in the bundle and recorded (the speech index); chunks cover contiguous steps; the station's prop
  exists in the scene; the teacher is an NPC; the procedure's Spanish is held to its `level` as lessons are (V08).
- *Tests.* Each rule on a small JSON string; the bundle still loads.

**W06: the procedure runner (M/L, Sonnet).**
- *Code.* `Activities/ProcedureRunner` (4.2): `Start(procedure, station, teacherPresent)`, `Act(use, on, verb)` returning right, wrong verb,
  wrong thing, out of order; `Repeat`, `Slower`, `Help`, `Leave`; `View` (the instruction, its rung, the highlight); `OnEvent`; `Capture`,
  `Restore`. `Activities/ProcedureProgress` (runs, sessions finished without help, first-attempt successes, mastered). `PhraseProgress.RecordAct` with
  the gap rules shared with uses. The wiring in a new partial `Session/GuideSession.Activities.cs` (as `GuideSession.Review.cs`), so `GuideSession.cs` stays focused. Instant crafts through `CraftingRules` with the procedure's `uses` and `makes`.
- *Save.* Add `systems.procedures` to N01’s version-6 map, preserving needs (4.6).
- *Tests.* A three-step test procedure through every rung; folding into a chunk and the goal; help unfolds a chunk and restores support; two runs within
  ten minutes credit once (the gap); mastery after two play sessions; leaving half-way takes nothing; the run survives a save and a load;
  the same acts give the same events (determinism); the migration from version 5.
- *Done when* the runner passes, with no UI.

**W07: the station view (M, Sonnet).**
- *Code.* Core: `Activities/StationScreen`, `StationPanel`, the view records (cells with an icon key, a noun with its article, a highlight
  flag; the instruction strip). Game: `UI/Station/StationLayer`, `StationLayout`; `VoxelGame` builds the panel as it builds the crafting
  panel; the icons of W10's fixtures (`bowl`, `spoon`, `oven`) painted in the atlas as V05 painted the coin, the apple and the axe, found
  by a key lookup with a plain fallback tile for any key without art. E at a station's prop opens it; the arm and the camera do not move while it is open.
- *Scripted.* `--station`, `--act`, `--proc-help` (`ScriptStepKind`, `ScenarioScript`).
- *Tests.* The verb choice appears only for a pair with several verbs; the highlight after a slip or on help, according to design 10.10; the English only at the first
  rung or after help; a component test runs the test procedure through the panel; layout tests at every UI scale (no overlap).
- *Done when* a scripted run with `--station` and a screenshot shows the view at the first rung (look at the PNG).

**W08: the teacher in the loop (S/M, Haiku).**
- *Code.* `HostEventTable` kinds `procedure_repeat`, `procedure_slower`, `procedure_help`, `give_missing`, `teach_recipe`, applied on the
  game thread where host events are applied; the runner raises `<npc>.teach_start` and `teach_end` as host turns; push-to-talk in the station view goes to
  the teacher at the prompt of its `teaching` state (`NpcService.PromptAfter`).
- *Content (the first instance, Luz).* `agents/baker.json` gains the `teaching` state of 4.4; `prompts.json` `baker-proc` with an answer set
  of the unstuck phrases and requests; `npcs/baker.json` names it; Luz's `ask_craft` answer and offer. The content loop.
- *Tests.* The agent's transitions at `baker-proc` with typed lines; each kind reaches the runner; the daily grant survives reloads, with the accepted lesson exception; `teach_recipe` adds the procedure to the known list.

**W09: imperative production (S, Haiku; outside the pilot).** Define taught forms, book columns, homographs with present-tense
forms and assessment rules after a separate owner decision (4.5).

**W10: session 11 “En la panadería” (M, content model by decision l).**
- Implement design sections 10.6, 10.9, 10.12 and 10.16: A2 lesson, six-action bread recipe, nine instruction rows and four learner rows.
- Add flour using the next free enum ID (34 if N07’s roast apple uses 33). The stall sells flour for one coin and buys bread for one
  coin using fixed host events. Check that `instead` can target the flour sale; include buying and selling turns in the lesson flow.
- Register the nouns, icons, lexicon entries, justified world-word waivers, normal/slow recordings and support-language text.
  Confirm bol or tazón in the content review and use it consistently. The owner listens to Luz’s instruction audio before the pilot.
- Add lesson-filtered comprehension checks and `r27-bread`. Try the lesson’s production lines and error examples in the core’s CLI
  before content depends on them. Follow the content loop and preserve sessions 1–10.

**W11: stage 2's documents, runs and measure (S, Haiku).**
- *Telemetry* (local JSONL, D-game-10): per act, the row, its rung, right first time, help, slips, the seconds from instruction to act;
  per run, the procedure, the session, the quality, the time.
- *The session summary* (`Session/SessionSummary.cs`): "Pan: 4 de 6 pasos sin ayuda."
- *Documents.* `playtest.md` (an activities section and its measures), `README.md` counts, `scripted-runs.md`, `speech-checklist.md`.
- *Run.* `r28-bread-again`: the procedure in a later session (through `--sleep` after completing the day’s work), showing the fade.
- *Runs.* Add `r29-bread-fold` for folding, the goal and mastery.
- *Then the owner's go or no-go* using design section 15.3 and section 10 below.

### Stage 3: crafting, repair and meals

**W12: items as data, C09 revisited (M, Sonnet).**
- *Why.* Stage 3 and 4 add a dozen items (tostadas, semillas, maíz, the martillo if it is carried, the stake later); by then the enum costs
  more than the move.
- *Data.* `game/items.json` and its schema: per item a stable `name` (the key saves use), a stable numeric `id` (the ten enum values kept;
  data-only items from 100, never reused), `stack`, `icon` (an atlas key), `held` (log, torch, bottle, sprite), `tags` (material, food,
  ingredient, tool), `tool` (W02's spec, moved here), `food` (W15's values), `durability` (W13).
- *Code.* `ItemDefinition` becomes an `ItemCatalog` read from the data and passed to what needs it (`Inventory`, `VoxelSimulation`,
  `NounCatalog`); the enum keeps the members code refers to by behaviour (the bottle fills, the book opens) and the loader checks each
  member against its data entry. Game: `BlockAtlasLayout.IconOf` and `HeldArm` by key, with a fallback tile (the noun's first letter) that
  the validator reports.
- *Save.* Version 7: items by name and a `wear` per slot (4.6); the migration maps the ten frozen enum names.
- *Tests.* Catalog and enum agree; stack limits from data; every save version from 1 loads; a data-only item round-trips; the fallback icon.
- *Done when* nothing in the game changes but the save's shape.

**W13: durability (S/M, Haiku).**
- *Code.* `ItemStack` gains `Wear` (meaningful for items with `durability`, which stack to 1); a gather with a tool adds wear; at its
  durability the tool is worn: it works at hands' speed and is never lost or destroyed; `tool_worn` (once); the hotbar draws a wear bar
  (Game). A test aid `--wear <item> <n>` for scripted runs.
- *Tests.* Wear adds up; a worn axe takes 60 ticks; a save keeps wear; the event fires once.

**W14: the axe's handle with Domingo (M, content model by decision l).**
- *Why.* The design plan's repair loop and the principle's procedural crafting, on the order session 6 built, without editing session 6.
- *The flow.* With a worn axe, Domingo asks "¿Qué pasó?"; the learner reports ("Se rompió el mango." or "El hacha está rota.", at the
  first rung with the card, as an NPC's focus row is); Domingo: "Vamos a poner un mango nuevo." The procedure `mango` at the forge: cut the
  wood (`corta la madera`), put the head on the handle (`pon la cabeza en el mango`, positions from session 4: `encima`), hold it, hit it
  with the hammer (`golpea con el martillo`). The axe's wear goes to 0 (`tool_repaired`). One wood is used.
- *Content.* Domingo's `teaching` state and `smith-proc` prompt; a `report_broken` intent (slot grammars `[ART] [NOUN:item] [VERB:estar]
  (roto|rota)`, `se [VERB:romper] [ART] [NOUN:part]`); the `mango` procedure, the forge station (`forge` prop), fixtures (martillo,
  yunque); `mango` (the handle; the lexicon's fruit sense noted), `martillo`, `golpear` with waivers; the rows, the Opus pass, Domingo's
  recordings; run `r30-repair` (`--wear axe 99`).
- *Two ways, as the design plan's "craft it yourself or commission it".* Domingo offers both: "¿Lo hacemos juntos, o lo arreglo yo por dos
  monedas?". The hands-on way is the procedure (free, one wood); the service is a new host kind `repair` (the price in coins, the wear to
  0). With no coins the hands-on way is always open, and his patience rule (W03) offers the choices when the learner cannot say it.
- *Before writing rows,* try `se rompió el mango`, `el hacha está rota` and `el hacha está roto` (`gender.agree` expected: `hacha` is
  feminine with `el`) in the core's CLI; a wrong grade is a core milestone.

**W15: cooked dishes and the camp fire (M; Sonnet for code, then content).**
- Use N-series hunger, eating, healing, pause and save rules. Add dish-specific buffs with deterministic expiry; do not introduce
  another hunger/fullness meter. W12 item data holds food values. Emit the existing `meal_eaten` event.
- Session 12, “En el campamento”, introduces a dish with Luna, followed by independent cooking at the player’s fire. Select the dish
  and recordings through the content review. N07’s instant roast apple remains available.
- Offer dinner before sleep when ingredients are available; dinner remains optional. Add `r31-dinner` and cover buff expiry,
  save/load, needs pauses and the existing sleep rules.

**W16: stage 3's documents and runs (S, Haiku).** As W11, for W12 to W15.

### Stage 4: the principle spread

**W17: the fading beacon (S/M, Haiku).** When an NPC's directions answer at the later rungs of their row (audio only, none), the beacon
is *held*: its target is known and its arrival still checked, but nothing is drawn (`Navigation/Beacon` gains `Held`). Ayuda, or asking
again, draws it (a supported use). Arriving at the place (`beacon_reached`) after a held direction credits the row's act unaided. Arrows
set by lessons, objectives and hooks are never held. Run `r32-no-arrow`.
- *The catch:* a held arrow needs a line that says where. The villager's and the fisher's point lines are fixed ("allí") and the guide's
  are templated, so text-only. Either each directing NPC gets a recorded line per place ("La panadería está al lado de la torre.", about
  ten places for the villager and the fisher: about 20 lines, as session 4 recorded a few), or the guide's templated lines hold the arrow
  only at the text rung. The milestone measures both and the owner picks.

**W18: farming with León (M; Sonnet for the code, then content).** `Activities/Crops`: farmland cells of the wheat field (and later the
player's plot) with a crop state (planted in session n, watered); a watered crop is ready after one sleep; procedures `sembrar` (put the
seeds in the earth, pour water) and `cosechar`; `crop_planted`, `crop_ready`, `crop_harvested`; the `systems` map's `crops` section. Session
13 "En el campo" with León (seeds, water, the weather words he has). New words with waivers (`sembrar`, `regar`, `cosechar`, `semilla`,
`maíz`). The fit: planting and harvesting fall in different sessions, so the farm's words come back across the gap by design. Run
`r33-farm`.

**W19: the notice board (M, Sonnet).** `game/order-templates/<id>.json`: the NPC, the goods with a choice of counts, the pay per item, the
rows it exercises, the level, and the notice as a bundle template ("Necesito {count:words} {item:plural-if(count)}. Pago {pay:words}
monedas."), text only, since notices are read. `Activities/OrderBoard`: three orders posted each play session with
`WorldRng.For(seed, "board", session)`, weighted by the due rows of each template (`ReviewDeck.Select`'s rule), added as runtime orders
(`ObjectiveSpec` with `deliverTo`, `deliver`, `session`); delivery is the existing order path. A board prop in the square (E opens it;
the notices with the ladder's English). The `systems` map's `board` section; `board_read`, `order_posted`, `order_taken`. Run `r34-board`.

**W20: market prices by session (S/M, Haiku).** A price list entry may carry a range; the day's price is drawn per session
(`WorldRng.For(seed, "price", session)`), so "¿Cuánto cuesta?" stays worth asking. The price line is either recorded (a line per number per
seller, about 30 lines for three sellers at 1 to 10) or text-only (a template, as the guide's world lines are): decision p.

**W21: stage 4's documents and runs (S, Haiku).**

### Stage 5: held for the pilot's data

Designed in the review (section 2.2); each needs its own brief after the pilot.

- **W22 Land and plots.** The camp is the starter plot; the first lot from the villager, named and made alcalde (numbers, prices and places
  from sessions 4, 5 and 10); `game/plots.json`; an ownership check in `VoxelSimulation.TryPlace`; more gatherable and placeable blocks
  (stone with a stone tool, W02's materials).
- **W23 Frontier claims.** A stake item; a `describe_location` intent (direction and landmark slots); the resolver on the worker through
  `ScenePlaces`; `<npc>.vague_place` as a host turn ("¿Cerca de qué?").
- **W24 Storage.** A chest is an `Inventory` with a capacity; labels the learner types go through the core's lexicon and count as typed
  word uses.
- **W25 Trust and "¿Lo de siempre?".** Trust derived from logged evidence with diminishing returns per session, kept per NPC; unlocks as
  data; the shortcut offered only when the order's phrase is at the last rung. Needs the free-talk follow-up first (the handover, section 5).
- **W26 Gear for places, and the tailor.** Clothes with equipment slots; cold on the mountain slows walking, never harms; a lantern; the
  tailor after a voice trial.
- **W27 The healer and foraging.** Symptoms ("me duele", "tengo frío"); herbs found by description (colours and sizes from session 7); the
  healer after a voice trial.
- **W28 Tenants and building to a description.** A tenant describes a room; the learner builds it; the check names what is missing.
- **W29 Fishing with Mar.** A procedure (bait, cast, wait, pull), `pez` and `pescado`, and cooking the catch.

### Proposed: the recipe book (owner, 2026-11-06)

**W30: the recipe book (M, Sonnet; proposed, not authorised).** The owner, 2026-11-06: "for the cooking, ideally i would like a book
interface. For example in KCD 1 and 2 when you create potions you use the book as the receipe book essentially and its an interactive
thing". In Kingdom Come: Deliverance the recipe book lies open beside the alchemy bench, its page lists the steps in prose, and the player
reads and follows them with the bench's own actions; the book never performs a step.
- *The idea here.* Each procedure the learner knows (W08's `teach_recipe`, or a recipe given in a lesson) gets a page: its title, what it
  uses and makes, and its steps as the written Spanish of its `does` rows. The book opens beside the station view (W07) and stays open
  while the learner acts; the page marks the current step and the steps done. The written page is a reading route to the same
  instructions Luz speaks, so it follows the support ladder (design 10.10, 10.11): English only at the first rung or after help, the
  chunk's or goal's row once folded, and reading the page is never production credit.
- *The owner's shape* (2026-11-06, answering the orchestrator's questions): "it should be a book the player has on them, but can you
  placed down for longer cooking sessions. So like we have a journal, the player can also get a recipe book. Perhaps as a reward for
  making the first item with luz". So: a new item, the recipe book, separate from the journal (the existing `book` item and its
  `Language/Book/` screen); carried in the inventory and opened from it anywhere; placeable in the world, where it lies open and its page
  shows beside the station view (W07) for a long cooking session, and is picked up again; given by Luz as the reward for the learner's
  first completed procedure with her (W10's first bread, through W06's `procedure_done`), so it holds at least the bread from the start.
- *Built on.* W07's instruction strip as a model the page can host (W07's brief keeps it separate from the panel for this); the journal's
  code (`BookModel`, `BookPages`, `BookTabs`, `BookScreen`) for the page geometry, the tabs and the speech controls; W12 (items as data)
  if it has landed, otherwise the next free item id.
- *Decided* (owner, 2026-11-06: "both defaults are fine"): a recipe's page appears only once it is taught; the page plays the normal and
  slow recordings of its `does` rows, as the journal plays its rows.

### Core milestones this series may need

Filed by the orchestrator in `docs/nlp/implementation-plan.md` (the next free M numbers) only when a W milestone reaches them:

- **Grading the learner's imperatives** beyond mood and person: needed only for a teach-back in which the learner tells an NPC the steps.
- **A mood in the template engine's `conj`** (it takes person, number and tense): optional, for generating instruction lines at authoring
  time instead of writing them out.
- **Per-domain level lists in the lint**: optional, when the waivers in `a1.txt`'s marked block pass about thirty words.
- **Whatever W10's and W14's CLI trials find** (the pronominal preterite `se rompió`, `estar` with a participle).

## 7. What changes in data formats

| File | Change | Milestone |
| --- | --- | --- |
| `game/npcs/<id>.json` | schema 3: `patience`; a `teaching` prompt in `prompts` | W03, W08 |
| `game/host-events.json` | schema 3: `sell` and `buy` by slot with `prices` (and a range, W20); kinds `procedure_repeat`, `procedure_slower`, `procedure_help`, `give_missing`, `teach_recipe`, `repair` | W04, W08, W14, W20 |
| `game/procedures.json`, `game/procedures/<id>.json` | new | W05 |
| `game/stations.json`, `game/actions.json` | new | W05 |
| `game/phrases.json`, `content/PHRASES.md` | the `does` kind | W05 |
| `game/lessons/<id>.json` | `imp` in `tense` and `verbs` | W09 |
| `game/items.json` | new: name, id, stack, icon, held, tags, tool, food, durability | W12 |
| `game/order-templates/<id>.json` | new | W19 |
| `game/nouns.json`, `lexicon/world.json`, `cefr/a1.txt` | the new nouns, their gender, the waivers | W10, W14, W15, W18 |
| `agents/*.json`, `prompts/prompts.json`, `answer-sets/*`, `intents/intents.json`, `dialogue/dialogue.json` | the teaching states, `<npc>-proc` prompts, `report_broken`, the lines | W08, W10, W14, W15, W18 |
| `content/locale/en/*` | the English of every new row, step, station, fixture, event line and UI string | each |
| Save | version 6 (`systems`), version 7 (data item IDs, wear) | N01; W06 adds procedures; W12 |

## 8. What changes in code

| Project, folder | Change | Milestone |
| --- | --- | --- |
| Game.Core `Event/` | the new world event names | W01, and each system |
| Game.Core `Voxel/`, `Items/`, `Simulation/` | materials, tool specs, `GatherTicksOf`; the item catalog; wear | W02, W12, W13 |
| Game.Core `Npc/` | the patience rule; price lists and `dont_sell`; the new host kinds | W03, W04, W08, W14 |
| Game.Core `Activities/` (new) | `ProcedureBook`, `StationBook`, `ActionTable`, `ProcedureRunner`, `ProcedureProgress`, `StationScreen`, `StationPanel`, `Crops`, `OrderBoard` | W05 to W08, W15, W18, W19; needs in N01 |
| Game.Core `Language/` | `does` rows (`PhraseSheet`), `RecordAct` (`PhraseProgress`), the imperative (`LessonBook`, `TaughtVerbForm`, `ConjugationTable`) | W05, W06, W09 |
| Game.Core `Session/` | `GuideSession.Activities.cs` (the wiring); the sleep offer's dinner; the summary line | W06, W11, W15 |
| Game.Core `Navigation/` | the held beacon | W17 |
| Game.Core `Save/` | versions 6 and 7 and their migrations | W06, W12 |
| Game.Core `Scripted/` | `--station`, `--act`, `--proc-help`, `--wear` | W07, W13 |
| Game `UI/Station/` (new), `UI/Hud/`, `Graphics/Blocks/` | the station layer and layout; the wear bar and dish-buff indicators; icons by key | W07, W12, W13, W15 |
| The core | nothing in stages 1 to 4, unless a CLI trial finds a grading gap (section 6, core) | |

## 9. Content cost

The owner's listening is the real cost (village-plan section 5): each line is two files and a listen.

| Milestone | Rows (does, says, hears) | New lines | WAV files | New words | Icons |
| --- | --- | --- | --- | --- | --- |
| W08 (Luz's teaching) | | about 4 | about 8 | | |
| W07 the station view | | | | | bowl, spoon, oven (fixtures) |
| W10 bread | 9, 4, 5 | about 20–25 | about 40–50 | harina, bol, cuchara, masa | flour |
| W14 the handle | 5, 2, 4 | about 10 | about 20 | mango, martillo, golpear, roto | hammer, anvil |
| W15 the camp dish | 5, 2, 4 | about 10 | about 20 | the dish's words | the dish, fire, stick |
| W17 directions | the direction rows | 0 or about 20 | 0 or about 40 | | |
| W18 farming | 6, 4, 8 | about 16 | about 32 | sembrar, regar, cosechar, semilla, maíz | seeds, corn |
| W19 the board | | about 3 (text notices are free) | about 6 | | the board |
| W20 prices | | 0 or about 30 | 0 or about 60 | | |
| **Stages 2 to 4** | | **about 55 to 105** | **about 110 to 210** | | |

Phase B of the village added 111 lines (222 files), for comparison. A recipe built only from instructions that exist adds no recording;
W05's validator can print, per procedure, its new rows and, per row, the procedures that use it.

## 10. Tests, gates and the go or no-go

Use the milestone gate: `GATE_TARGET=feature/activities scripts/merge-gate.sh "<label>:<branch>"`.
Reserve r25–r26 for needs, r27–r29 for bread, then r30-repair, r31-dinner, r32-no-arrow, r33-farm and r34-board.
Confirm availability when registering each run.

Design section 15.3 is the pilot protocol: 3–5 prepared testers, three sittings on separate days, two spaced bakes per sitting,
a frozen executable and bundle, lesson-filtered checks and delayed recall. Report listening separately from text-supported evidence;
the proposed listening target is 70% on eligible first-attempt, unassisted actions. Most testers should encounter a grouped
instruction by the second sitting’s second bake. Measure voluntary replay, useful bread consumption/trade, preparation and transfer.
The owner’s own run is a functional check. Resolve design decisions 1–7 and the relevant edge cases before implementation.

After W11, the owner decides whether to expand, revise the support ladder or shorten the procedure. Keep the frozen pilot unchanged
through its final recall.

## 11. Risks and open points

- **A station view can feel like a minigame**, which the brief warns against. Keep it to holding and using things, with no timing or
  dexterity; measure enjoyment in the go or no-go.
- **Keep session and NPC wiring focused.** W03, W04 and W08 touch the first; W06 puts its wiring in a
  partial of the second. A milestone that cannot keep a change small says so in its report.
- **Save versions 6 (N01) and 7 (W12).** Each comes with a migration and the every-version test; later systems use the `systems`
  map and bump nothing.
- **Recognition is not mastery.** Acts and verb choices are understanding. Production comes from the plan and the report around each
  activity, from asking for what is missing and from the repair phrases.
- **Words.** Kitchen and farm nouns are missing from the lexicon; each milestone adds them with their gender and a reason, and regional
  choices (`bol` or `tazón`, `papa`) go through the Opus pass and the reviewer's later check (D-game-12).
- **Grading.** `se rompió el mango`, `está rota` and the imperative must be tried in the core's CLI before rows rely on them.
- **Dependencies.** Language follow-ups precede needs; needs precede the bread pilot. W03, W04 and W08 build on V29’s NPC reply-pool
  declarations. W08 also follows the spoken-lines study.

- **Scripted-run baselines** move with every content milestone; each re-baselines its own and says which.

## 12. Owner decisions (D-game-23, proposed)

**Before stage 1:**
- (a) **Activity principle:** useful activities repeat Spanish, with support reduced by learning evidence.
- (b) **Needs (confirmed, D-game-24):** five needs, staged effects and death with a cost, built in the N series before the pilot.
- (c) **No combat**; gear for places (W26).
- (d) **Tool tiers**: hands, stone and iron from the smith; no copper, steel or reforging (W02).
- (e) **The cast**: the seven NPCs take the design plan's roles; new NPCs only after a voice trial, in stage 5.
- (f) **Land, storage and trust in stage 5**, after the pilot.
- (g) **Items as data at W12**, before stage 3: requires approval under D-game-21.
- (j) **Order (confirmed):** queued language follow-ups, needs and the needs playtest, then the W pilot.
- (l) **Models**: Haiku and Sonnet as the table says, with the content milestones (W10, W14, the content of W15 and W18) by Opus 5.5 at xhigh
  as the V series was, or by Sonnet.

**Before stage 2:**
- (h) **Imperative comprehension (confirmed):** commands understood through support; production teaching is outside the pilot. Session 11 at A2 is proposed.
- (i) **Instructions as `does` rows**, recorded in the teacher's voice, with the Opus pass and the owner's listening, and the reuse report.
- (m) **New sessions 11 to 13** for the first activities; sessions 1 to 10 untouched.
- (n) **Flour sold at the stall** for one coin through a fixed sell (proposed; design decision 6).
- (o) **The station view as an overlay (confirmed).**

**After stage 2:**
- (q) **The go or no-go** on section 10's measures, and their thresholds.

**Later:**
- (k) **The learner signal** for the board and the director: the game's records now; one shared core learner model only when weak grammar
  should steer (a game change with a save migration).
- (p) **Price lines** recorded or text-only (W20).
