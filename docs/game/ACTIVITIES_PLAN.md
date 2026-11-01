# Activities and the core systems: the W series

A plan written on 2026-11-01 at the owner's request ("make a more detailed implementation plan"), from the owner's design plan
(`docs/game/Vocaterra — Core Systems Design Plan.docx`), the owner's principle (`docs/game/Vocaterra_Crafting_Cooking.md`) and their review
(`docs/game/CORE_SYSTEMS_REVIEW.md`). It is written against `feature/village` at `3575d46e`, the code the series will start from once PR #20
is merged. Nothing was built or run.

**Status:** proposed. The series waits for PR #20 to merge and for the owner's answers in section 12 (decision D-game-23, proposed).
**Branch:** `feature/activities`, made from `main` after PR #20 merges; milestone branches `W<nn>-<name>`, gated into it; never `main`.
**Commit prefix:** `W<nn>:` (W for work, the world's trades; A would read as a CEFR level).
**Where this file goes:** when R19 (the docs restructure) runs, it moves to `docs/plans/series/w-activities.md`.

**In short.** Four cheap foundations; then one activity end to end (bread with Luz, a new session 11) as the test of the principle,
ending in the owner's go or no-go; then repair with Domingo, meals and dinner at the camp fire; then directions without the arrow, farming
with León, a notice board and market prices. Land, storage, trust, gear and new NPCs wait for the pilot.

## 1. The request

The owner's design plan describes a survival game in which needs send the player to NPCs. The owner's principle asks for more than
that: "normal gameplay systems should be deep enough to create repeated, contextual language practice without needing endless unique
quests or NPCs", with cooking and crafting as the reference systems (real procedures with tools, materials and ordered steps, and
guidance that fades from highlighted, translated, spoken instructions to the player working alone), and the same approach applied to
farming, building, trading, exploration, gathering, repairs and survival.

The review found that the conversation half of the design plan is mostly built, the survival half is not, five parts conflict with
decisions in force, and the code already has most of a procedure engine: the lesson runner and the phrase support ladder. This plan turns
that into milestones.

## 2. What the plan builds on

From the review, the facts every milestone below relies on (paths are on `feature/village`):

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
  lessons and the book teach only `pres`, `pret`, `ir-a` and `fut`.
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
| Needs | buffs only; nothing kills | W15 grows a health model, and the brief's guardrail needs a new decision |
| Combat | none; gear is for places | stage 5 gains combat milestones; W26 changes |
| Tool tiers | hands, stone, iron (from the smith) | W02's table grows; ores need terrain work |
| Cast | the seven NPCs; new ones only after a voice trial | stage 5 adds NPC milestones earlier |
| Land, storage, trust | after the pilot (stage 5) | W22 to W25 move up |
| Items as data | at W12, before stage 3 (not now) | W12 moves to stage 1, or never (new items then go into the enum, as V03 did) |
| The tú imperative | allowed in an `a1` lesson | W09 gates it to `a2`, and W10's lesson becomes `a2` |
| First activity lessons | new sessions 11 to 13; sessions 1 to 10 untouched | weaving activities into earlier sessions moves their goldens and runs |
| The station view | an overlay panel, as crafting is | an in-world kitchen is a project of its own (block states, animation) |

### 3.3 What this plan does not do

No combat, no co-op, no new terrain, site or road, no new NPC before a voice trial (and none before stage 5), no change to the lessons of
sessions 1 to 10, no speech synthesised at run time, no unrestricted dialogue. The instant torch craft of session 1 stays as it is.

## 4. The design

### 4.1 Procedures: the data

A **procedure** is a repeatable lesson whose steps are actions. Four new file kinds, each with a schema in `content/schema/`:

`game/procedures/<id>.json` (listed in order by `game/procedures.json`):

```json
{
  "$schema": "../../../schema/procedure.schema.json",
  "schemaVersion": 1,
  "id": "pan",
  "station": "horno",
  "teacher": "baker",
  "level": "a1",
  "makes": { "item": "bread", "count": 2 },
  "uses": [ { "item": "flour", "count": 1 }, { "item": "water-bottle", "count": 1, "leaves": "bottle" } ],
  "steps": [
    { "id": "harina",  "row": "pon-la-harina",    "do": "poner",   "what": "harina", "where": "bol" },
    { "id": "agua",    "row": "echa-el-agua",     "do": "echar",   "what": "agua",   "where": "bol" },
    { "id": "mezclar", "row": "mezcla-la-masa",   "do": "mezclar", "what": "bol",    "with": "cuchara" },
    { "id": "horno",   "row": "pon-la-masa",      "do": "poner",   "what": "masa",   "where": "horno" },
    { "id": "esperar", "row": "espera-diez",      "wait": 10 },
    { "id": "sacar",   "row": "saca-el-pan",      "do": "sacar",   "what": "pan",    "from": "horno" }
  ],
  "chunks": [
    { "id": "masa",    "row": "haz-la-masa",      "steps": ["harina", "agua", "mezclar"] },
    { "id": "cocinar", "row": "cocina-el-pan",    "steps": ["horno", "esperar", "sacar"] }
  ],
  "goal": { "row": "haz-pan" }
}
```

- A step is an **act** (`do` a verb lemma on `what`, optionally `with` a tool, `where`, `from`) or a **wait** (seconds of game time; its
  instruction carries a number). The player makes an act by holding one thing and using it on another: the tool on the thing (`with`
  on `what`: the spoon on the bowl), the thing on its place (`what` on `where`: the flour on the bowl), or the empty hand on where it comes
  `from` (the hand on the oven). An act may have a `count` ("Pon dos tazas de harina" needs two acts): numbers in use, as session 5 taught
  them. Intermediate things (the dough, the bowl's contents) live in the station view, not in the inventory.
- `uses` are taken from the inventory when the procedure finishes, never at its start, so leaving half-way costs nothing. `leaves` gives
  back a container (the bottle, emptied: the player refills it at the river or the well, which S6 built).
- `row` names a phrase-sheet row of the new kind `does` (below); the row names its recorded line.
- `teacher` is the NPC whose voice records the instructions and who teaches at the station; the player working alone later hears the same
  recordings.

`game/stations.json`:

```json
{
  "schemaVersion": 1,
  "stations": [
    { "id": "horno", "prop": "oven", "npc": "baker",
      "fixtures": [ { "noun": "bol", "icon": "bowl" }, { "noun": "cuchara", "icon": "spoon" }, { "noun": "horno", "icon": "oven" } ] },
    { "id": "fuego", "prop": "firering.player",
      "fixtures": [ { "noun": "fuego", "icon": "fire" }, { "noun": "palo", "icon": "stick" } ] }
  ]
}
```

A station is a named prop of the scene (or a block kind); E at it opens the station view. `npc` is who stands there to teach and talk.
**Fixtures** (utensils and tools fixed at the station) are not inventory items: they need a noun and an icon, nothing else.

`game/actions.json`, the verbs each pair allows (what the player holds, what it is used on):

```json
{
  "schemaVersion": 1,
  "actions": [
    { "use": "harina",  "on": "bol",   "verbs": ["poner", "echar"] },
    { "use": "agua",    "on": "bol",   "verbs": ["echar", "poner"] },
    { "use": "cuchara", "on": "bol",   "verbs": ["mezclar"] },
    { "use": "masa",    "on": "horno", "verbs": ["poner"] },
    { "use": "mano",    "on": "horno", "verbs": ["sacar", "abrir"] }
  ]
}
```

`mano` is the empty hand. Where a pair allows one verb the act happens on the click; where it allows several, a short choice of Spanish
verbs appears. Real recipes make several verbs per pair common, and that is what makes the verb worth understanding.

**Instruction rows.** The phrase sheet (`content/PHRASES.md`, its machine copy `game/phrases.json`, `Language/Phrases/PhraseSheet.cs`)
gains a third kind beside `says` and `hears`: `does`, a line the learner hears and acts on. A `does` row has the Spanish, the English, the
card text, its audio line ids and no intent or prompt. Lines live in the teacher's pools (`baker_proc_harina`), so the voice is the
teacher's (V01's rule). The sheet test checks that each `does` row has audio and that a procedure step, chunk or goal names it.

### 4.2 Procedures: the runtime

`Activities/ProcedureRunner` (a new folder of Game.Core, namespace `Vocaterra.Game.Core.Activities`) is plain state like the lesson runner:
the session feeds it acts and world events; it reports `ProcedureEvent`s and world events; it reads no clock.

**The ladder climbs the recipe's tree.** Each step shows its instruction by its row's rung:

| Rung of the step's row | What the step shows | A right act without help counts as |
| --- | --- | --- |
| English and card | the instruction spoken, its Spanish text, its English; what to hold and where to use it highlighted | supported |
| Card and audio | spoken and written in Spanish; the English on Ayuda | unaided |
| Audio only | spoken, with `Repetir` and `Más despacio`; the text on Ayuda | unaided |
| None | nothing of its own: the step folds into its chunk, whose row's line is given instead ("Haz la masa") | unaided, for the chunk's row and the step's |

A chunk row starts at card and audio (its parts are known; the sentence is new) and fades the same way; when it reaches none, the goal's
line ("Haz pan") replaces the chunk. When the goal row reaches none, the learner makes the recipe from its name alone.

- **Credit.** Showing an instruction counts `Heard` for its row. A right act counts `Answered` (comprehension evidence) and, past the row's
  D-game-6 gap, a credited act that steps the row down: one credited act from the first rung, then one unaided credited act per rung. Three
  spaced acts fold a step, as three spaced uses make a noun known. `PhraseProgress` gains `RecordAct(row, helped, now, session)` beside
  its use rules; `Supported` and `Unaided`, the production counts, are not touched.
- **Help and mistakes.** Ayuda (the H key, or "ayuda" or "no sé" said to the teacher) shows the English and the highlight, makes the act
  supported and steps the row up one, as "no sé" does for a phrase; on a folded step it unfolds it. A wrong act is a slip on the row: the
  first repeats the instruction, the second plays it slowly, the third shows the highlight (counted as help). An act on a later step says
  "Primero, …" and repeats the current one. Nothing blocks: every step can be done with help.
- **Quality.** Steps right first time, out of all steps: a dish of one, two or three stars, carried by the `procedure_done` event (meals
  read it in W15). Help never lowers the quality; only slips do.
- **Mastery.** When the goal row is at none and the recipe was finished without help in two different play sessions, the procedure is
  mastered (`procedure_mastered`) and the station offers it as an **instant craft** (one click, as the torch is). The guided run stays on
  offer and comes first when the goal row is due again by its gap ("¿Hacemos pan juntos?"); it is never forced. The game stops repeating
  a recipe when it stops teaching.
- **World events:** `procedure_started` (detail: the id), `procedure_step` (`pan.mezclar first-try | retried | helped`), `procedure_done`
  (the id; amount: the count made), `procedure_mastered`. A lesson's `do` step can wait for `procedure_done pan`, so no new step type is
  needed.
- **Save:** the procedures' progress (runs, sessions finished without help, best quality, mastered) and a run in progress, in the save's
  new `systems` map (4.6).

### 4.3 The station view

A Core model and panel (`Activities/StationScreen`, `Activities/StationPanel : IScreen`) and a Game layer (`UI/Station/StationLayer`,
`StationLayout`), built as crafting is (`CraftingScreen`, `CraftingPanel`, `CraftingLayer`, layout delegates for hit tests):

```
 ┌──────────────────────────────────────────────────────────────┐
 │ Luz: "Pon la harina en el bol."  [Repetir] [Despacio] [Ayuda]│  the instruction, by rung
 │      Put the flour in the bowl.                    ● ● ○ ○ ○ ○│  the English (rung 1 only); the steps
 ├──────────┬───────────────────────────────────┬───────────────┤
 │ el bol   │                                   │               │
 │ la       │      [ el bol ]      [ el horno ] │   ★ ★ ☆       │  the work area: the targets
 │ cuchara  │                                   │               │
 ├──────────┴───────────────────────────────────┴───────────────┤
 │ la harina ×1   el agua ×1   (the bag's ingredients)           │  what can be held
 └──────────────────────────────────────────────────────────────┘
```

- Labels are nouns with their article (`la harina`, `el bol`): the gender for free, as the design plan wanted. A noun's English shows on
  hover until the noun is known (D-game-6's rule, three spaced spoken uses).
- Keys: 1 to 9 pick what to hold, a click uses it on a target, H help, R repeat, S slower, Esc leaves (the run is kept). Push-to-talk works
  as in the dialogue panel and goes to the teacher (4.4).
- The instruction's audio is the row's line through the existing line player (`NpcVoice`), the slow file for `Más despacio`.
- The station's list shows the procedures the learner knows and, when mastered, their instant crafts.
- Scripted runs gain `--station <id>`, `--act <use> <on> [verb]` and `--proc-help` (`Scripted/ScriptStepKind.cs`).

### 4.4 The teacher in the loop

While a procedure runs at a station with its teacher, the teacher's agent is in a `teaching` state at a constrained prompt
(`<npc>-proc`) that takes the getting-unstuck phrases and requests:

| The learner says | Intent | The agent raises | The game does |
| --- | --- | --- | --- |
| "Otra vez, por favor." | `ask_repeat` | `<npc>.proc_repeat` | the instruction again |
| "Más despacio, por favor." | `ask_slower` | `<npc>.proc_slower` | the slow recording |
| "Ayuda." / "No sé." | `ask_help` / `dont_know` | `<npc>.proc_help` | help, as the H key |
| "¿Tienes harina?" / "Quiero harina." | `ask_availability` / `request_item` | `<npc>.give_missing` | the missing ingredient, once a run (never stuck) |
| "Gracias." | `thank` | (a line) | |

New host-event kinds `procedure_repeat`, `procedure_slower` and `procedure_help` call the runner; `give_missing` gives the item the run
lacks (a `give_item` names a fixed item, and the agent cannot branch on which one is missing, so the host decides it). The agent enters `teaching` on `<npc>.teach_start` (raised by the runner's start) and leaves on `<npc>.teach_end`.
`npcs/<id>.json` names the prompt of the state (`"prompts": { "idle": "baker-open", "teaching": "baker-proc" }`, V01's field).
"Otra vez, por favor" and "Más despacio" were taught in session 1 and are reviewed here with a reason.

**Learning a recipe by asking** (the design plan's recetario): at the teacher's open prompt, `ask_craft` ("¿Cómo se hace el pan?") gets the
recipe's ingredients (the guide's pattern) and an offer; `confirm` raises `<npc>.teach`, a new kind `teach_recipe` that adds the procedure
to the known list, and its first run starts at the first rung.

### 4.5 The tú imperative

Instructions are tú imperatives (pon, echa, mezcla, saca, corta; pon and haz are irregular and in nearly every recipe). The lesson schema's
`tense` gains `imp`; `TaughtVerbForm` reads the second-person singular imperative from the lexicon; the book's conjugation table gains a
column once a lesson teaches an `imp` form (`ConjugationTable.AllColumns`, `ui.bookColumnImperative`), as V16 did for the future. At an
`imp` step the runner reads the core's word reading (mood `imp`, second person singular); a correct present ("pones la harina") is credited
with a note, by S27's rule that correct Spanish is never a miss. Understanding needs nothing from the core; grading the learner's own
imperatives beyond mood and person is a core milestone (section 6, core).

### 4.6 Saves

- **Version 6 (W06): a `systems` map.** `SaveDocument` gains `Systems`, a map from a system's id to its own versioned object
  (`{ "procedures": { "version": 1, … } }`). A system with no entry starts fresh. Later systems (meals, crops, the board) add an entry
  instead of bumping the save's version. The migration from 5 adds an empty map; every older fixture still loads.
- **Version 7 (W12): items by name, and wear.** `SlotSave` writes the item's data name (`wood`, not `Wood`) and a `wear` (0 when new). The
  migration maps the ten frozen enum names. This is the one change the C09 assessment warned about, made once, with a test that every
  version from 1 loads.

### 4.7 World events added

| Event | Raised by | Detail | Milestone |
| --- | --- | --- | --- |
| `item_crafted` | `VoxelSession` (a craft, an instant procedure) | the output's noun; amount: the count | W01 |
| `procedure_started`, `procedure_step`, `procedure_done`, `procedure_mastered` | `ProcedureRunner` | 4.2 | W06 |
| `tool_worn`, `tool_repaired` | the simulation, the repair | the tool's noun | W13, W14 |
| `meal_eaten` | `VoxelSession` | the food's noun | W15 |
| `crop_planted`, `crop_ready`, `crop_harvested` | `Activities/Crops` | the crop's noun | W18 |
| `board_read`, `order_posted`, `order_taken` | `Activities/OrderBoard` | the order's id | W19 |

Each joins `WorldEvents.All`, so tips, lessons and objectives may wait for it, and the schemas that list event names.

## 5. Milestones

| ID | What | Depends on | Size | Model | Status |
| --- | --- | --- | --- | --- | --- |
| W00 | This plan; D-game-23 proposed; the owner's edits to the design plan; the row in `IMPLEMENTATION_PLAN.md` section 4 | the review | | orchestrator | written 2026-11-01 |
| **Stage 1: foundations** | | | | | |
| W01 | `item_crafted`: a craft is a world event | | S | Haiku | |
| W02 | Tools and materials: a material per block, a tool spec per item, the axe's special case replaced | | S/M | Haiku | |
| W03 | Fail-forward as data: `patience` in the NPC file; the stall's rule moved there | | S/M | Haiku | |
| W04 | Price lists: `sell` and `buy` by the item the learner names; `<npc>.dont_sell` | | M | Haiku | |
| **Stage 2: procedures, and bread with Luz** | | | | | |
| W05 | Procedures, stations and actions as data; the `does` rows of the phrase sheet; the validator's rules | | S/M | Haiku | |
| W06 | The procedure runner: the ladder up the recipe's tree, credits, help, quality, mastery, events; save version 6 with the `systems` map | W05 | M/L | Sonnet | |
| W07 | The station view: model, panel, layer, layout, fixture icons, scripted steps | W06 | M | Sonnet | |
| W08 | The teacher in the loop: the `teaching` state and prompt, the three host kinds, the missing ingredient, learning a recipe by asking | W06, W07 | S/M | Haiku | |
| W09 | The tú imperative in the lessons and the book | | S | Haiku | |
| W10 | Session 11 "En la panadería": the lesson, the `pan` procedure, flour, Luz's teaching, the rows and recordings, the checks, run `r24-bread` | W04, W07, W08, W09 | M | content (decision l) | |
| W11 | Stage 2's documents and runs; the pilot measure (telemetry, the session summary, `PLAYTEST.md`); run `r25-bread-again` | W10 | S | Haiku | **owner's go or no-go** |
| **Stage 3: crafting, repair and meals** | | | | | |
| W12 | Items as data (C09 revisited): `game/items.json`, icons by key with a fallback, save version 7 (names and wear) | W02 | M | Sonnet | |
| W13 | Durability: wear per tool slot, a worn tool at hands' speed (never lost), `tool_worn` | W12 | S/M | Haiku | |
| W14 | The axe's handle with Domingo: "Se rompió el mango", the `mango` procedure at the forge, `repair`, run `r26-repair` | W08, W13 | M | content (decision l) | |
| W15 | Meals and the camp fire: fullness and buffs in stored time, eating, `meal_eaten`; session 12 "En el campamento" with Luna; dinner before sleep; run `r27-dinner` | W08, W12 | M | Sonnet, then content | |
| W16 | Stage 3's documents and runs | W14, W15 | S | Haiku | |
| **Stage 4: the principle spread** | | | | | |
| W17 | The fading beacon: an NPC's directions hold the arrow at the later rungs; arriving unaided is credited; run `r28-no-arrow` | W06 | S/M | Haiku | |
| W18 | Farming with León: crops that grow by play session; session 13 "En el campo"; run `r29-farm` | W06, W07, W12 | M | Sonnet, then content | |
| W19 | The notice board: order templates, a seeded generator weighted by what is due, written notices; run `r30-board` | W12 | M | Sonnet | |
| W20 | Market prices by session | W04 | S/M | Haiku | |
| W21 | Stage 4's documents and runs | W17 to W20 | S | Haiku | |
| **Stage 5: held for the pilot's data** | | | | | |
| W22 to W29 | Land and plots; frontier claims; storage; trust and "¿Lo de siempre?"; gear for places and the tailor; the healer and foraging; tenants and building to a description; fishing with Mar | section 6 | L | | held |

**Order.** Stage 1's four milestones are independent and cheap; none blocks stage 2 (W04 is needed only by W10). Stage 2 is the hypothesis
test and runs in its own order: W05, W06, W07, W08, with W09 anywhere before W10, then W10 and W11. **After W11 the owner plays (or pilots)
session 11 twice, in two play sessions, and decides whether to go on** (section 10's measures). Stage 3: W12 first (its item data
carries the tools' wear and the food values), then W13 and W14, and W15 once W12 is in. Stage 4's milestones are independent of each other. One agent at a time throughout (the machine rules); the gate after
each milestone.

## 6. The milestones in detail

Every milestone meets the definition of done (`docs/game/IMPLEMENTATION_PLAN.md` section 6) and, while iterating, the fast tier
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
  `Restore`. `Activities/ProcedureProgress` (runs, sessions finished without help, best quality, mastered). `PhraseProgress.RecordAct` with
  the gap rules shared with uses. The wiring in a new partial `Session/GuideSession.Activities.cs` (as `GuideSession.Review.cs`), so the
  1,468-line `GuideSession.cs` does not grow. Instant crafts through `CraftingRules` with the procedure's `uses` and `makes`.
- *Save.* Version 6, the `systems` map (4.6), with the `procedures` section.
- *Tests.* A three-step test procedure through every rung; folding into a chunk and the goal; a slip unfolds; help steps up; two runs within
  ten minutes credit once (the gap); mastery after two play sessions; leaving half-way takes nothing; the run survives a save and a load;
  the same acts give the same events (determinism); the migration from version 5.
- *Done when* the runner passes, with no UI.

**W07: the station view (M, Sonnet).**
- *Code.* Core: `Activities/StationScreen`, `StationPanel`, the view records (cells with an icon key, a noun with its article, a highlight
  flag; the instruction strip). Game: `UI/Station/StationLayer`, `StationLayout`; `VoxelGame` builds the panel as it builds the crafting
  panel; the icons of W10's fixtures (`bowl`, `spoon`, `oven`) painted in the atlas as V05 painted the coin, the apple and the axe, found
  by a key lookup with a plain fallback tile for any key without art. E at a station's prop opens it; the arm and the camera do not move while it is open.
- *Scripted.* `--station`, `--act`, `--proc-help` (`ScriptStepKind`, `ScenarioScript`).
- *Tests.* The verb choice appears only for a pair with several verbs; the highlight only at the first rung; the English only at the first
  rung or after help; a component test runs the test procedure through the panel; layout tests at every UI scale (no overlap).
- *Done when* a scripted run with `--station` and a screenshot shows the view at the first rung (look at the PNG).

**W08: the teacher in the loop (S/M, Haiku).**
- *Code.* `HostEventTable` kinds `procedure_repeat`, `procedure_slower`, `procedure_help`, `give_missing`, `teach_recipe`, applied on the
  game thread where host events are applied; the runner raises `<npc>.teach_start` and `teach_end` as host turns; push-to-talk in the station view goes to
  the teacher at the prompt of its `teaching` state (`NpcService.PromptAfter`).
- *Content (the first instance, Luz).* `agents/baker.json` gains the `teaching` state of 4.4; `prompts.json` `baker-proc` with an answer set
  of the unstuck phrases and requests; `npcs/baker.json` names it; Luz's `ask_craft` answer and offer. The content loop.
- *Tests.* The agent's transitions at `baker-proc` with typed lines; each kind reaches the runner; the missing ingredient is given once a
  run; `teach_recipe` adds the procedure to the known list.

**W09: the tú imperative (S, Haiku).** 4.5.
- *Code.* `lesson.schema.json` (`imp` in `tense` and in `verbForm.tense`); `LessonBook` (the tense parse); `TaughtVerbForm.Text` (the
  lexicon's imperative); `ConjugationTable.AllColumns` and `ui.bookColumnImperative`; the step's tense judge and an `impNote` (the
  locale); `GameDataValidator` allows `imp` at `a1` (decision h).
- *Tests.* The lexicon has `pon`, `echa`, `mezcla`, `saca`, `corta`, `haz`, `espera`, `toma` as second-person imperatives; the column shows
  once taught; `pones la harina` at an `imp` step is credited with the note.

**W10: session 11 "En la panadería" (M, content model by decision l).**
- *The lesson* `en-la-panaderia` (session 11, `a1`, at Luz, after `el-mercado`): an `explain`; Luz's `model` ("Hoy vamos a hacer pan.
  ¿Me ayudas?"); `answer` "Voy a hacer pan." (`ir-a`); two `listen` steps on the key verbs (mezcla, echa); a `do` step waiting for
  `procedure_done pan`, its HUD line sending the learner to the oven (the procedure at the first rung, in Luz's voice); `answer` "Hice pan." (`pret`, to "¿Qué
  hiciste?"); a `review` step; a `recap`. The plan, the doing and the report of VILLAGE_PLAN's principle 7.
- *The procedure* `pan` of 4.1 (six steps, two chunks, a goal), the station `horno` at the bakery's oven, the actions.
- *Items.* `flour` (`ItemId.Flour = 33` and its icon, as V03 added items, unless W12 has run); `harina` in `nouns.json`; Luz's price list
  (W04) with `harina` at 1 coin; `harina` in her answer set.
- *Words.* `harina`, `bol`, `cuchara`, `masa` in `lexicon/world.json` with their gender; world-word waivers in `cefr/a1.txt`'s marked block,
  one reason each; the choice of `bol` or `tazón` made with the Opus pass (Latin American usage).
- *Rows.* About eight `does` rows (six steps, two chunks, the goal), three `says` rows with graded error examples, five `hears` rows; the
  Opus pass (D-game-12 step 3); `scripts/prerender-speech.sh` for Luz's voice; `SPEECH_CHECKLIST.md`.
- *Checks.* Two or three items for the lesson in `checks.json` (`lesson`, V07): hear an instruction, pick its meaning.
- *Run.* `r24-bread` in `SCRIPTED_RUNS.md`: the lesson end to end, timed at 5 to 10 minutes.
- *Before writing rows,* try in the core's CLI that the lesson's sentences grade as expected (`voy a hacer pan`, `hice pan`, `ayer hago
  pan` → `conj.tense`); a wrong grade waits for a core milestone, as in VILLAGE_PLAN section 5.

**W11: stage 2's documents, runs and measure (S, Haiku).**
- *Telemetry* (local JSONL, D-game-10): per act, the row, its rung, right first time, help, slips, the seconds from instruction to act;
  per run, the procedure, the session, the quality, the time.
- *The session summary* (`Session/SessionSummary.cs`): "Pan: 4 de 6 pasos sin ayuda."
- *Documents.* `PLAYTEST.md` (an activities section and its measures), `README.md` counts, `SCRIPTED_RUNS.md`, `SPEECH_CHECKLIST.md`.
- *Run.* `r25-bread-again`: the procedure in a later session (`--skip-to-session 12` or a sleep), showing the fade.
- *Then the owner's go or no-go* (section 10).

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
  recordings; run `r26-repair` (`--wear axe 99`).
- *Two ways, as the design plan's "craft it yourself or commission it".* Domingo offers both: "¿Lo hacemos juntos, o lo arreglo yo por dos
  monedas?". The hands-on way is the procedure (free, one wood); the service is a new host kind `repair` (the price in coins, the wear to
  0). With no coins the hands-on way is always open, and his patience rule (W03) offers the choices when the learner cannot say it.
- *Before writing rows,* try `se rompió el mango`, `el hacha está rota` and `el hacha está roto` (`gender.agree` expected: `hacha` is
  feminine with `el`) in the core's CLI; a wrong grade is a core milestone.

**W15: meals and the camp fire (M; Sonnet for the code, then content).**
- *Code.* `Activities/PlayerCondition`: fullness from 100 down by one each N seconds of stored time while awake, restored by eating
  (`food.fill` in the item data); buffs with an end in stored time: "bien alimentado" (gather ticks × 0.8) and "comida caliente" (walking
  × 1.1), from the dish's quality. Eating is using a food item (right-click, as `BookItemUse` uses the book); `meal_eaten`. At zero
  fullness the buffs end and the HUD shows once, in Spanish, "Tienes hambre." (its English by the ladder); nothing else happens. The
  `systems` map's `condition` section. Game: a small fullness icon on the HUD.
- *Content: session 12 "En el campamento".* Luna teaches a camp dish at her fire (`tostadas`: cut the bread, put it on the fire, turn it,
  take it out; or `manzanas asadas`, decided with the Opus pass); then the learner cooks it at their own fire (`firering.player`, station
  `fuego`) alone, hearing Luna's recordings. The sleep offer (V12c) asks "¿Cenamos antes de dormir?" when the bag holds what the dish needs;
  never required. Run `r27-dinner`.
- *Why here.* Every session ends at the player's fire; a dinner there turns that moment into a nightly procedure, and a new session meets
  D-game-6's gap, so each night's cooking earns spaced credit by construction.

**W16: stage 3's documents and runs (S, Haiku).** As W11, for W12 to W15.

### Stage 4: the principle spread

**W17: the fading beacon (S/M, Haiku).** When an NPC's directions answer at the later rungs of their row (audio only, none), the beacon
is *held*: its target is known and its arrival still checked, but nothing is drawn (`Navigation/Beacon` gains `Held`). Ayuda, or asking
again, draws it (a supported use). Arriving at the place (`beacon_reached`) after a held direction credits the row's act unaided. Arrows
set by lessons, objectives and hooks are never held. Run `r28-no-arrow`.
- *The catch:* a held arrow needs a line that says where. The villager's and the fisher's point lines are fixed ("allí") and the guide's
  are templated, so text-only. Either each directing NPC gets a recorded line per place ("La panadería está al lado de la torre.", about
  ten places for the villager and the fisher: about 20 lines, as session 4 recorded a few), or the guide's templated lines hold the arrow
  only at the text rung. The milestone measures both and the owner picks.

**W18: farming with León (M; Sonnet for the code, then content).** `Activities/Crops`: farmland cells of the wheat field (and later the
player's plot) with a crop state (planted in session n, watered); a watered crop is ready after one sleep; procedures `sembrar` (put the
seeds in the earth, pour water) and `cosechar`; `crop_planted`, `crop_ready`, `crop_harvested`; the `systems` map's `crops` section. Session
13 "En el campo" with León (seeds, water, the weather words he has). New words with waivers (`sembrar`, `regar`, `cosechar`, `semilla`,
`maíz`). The fit: planting and harvesting fall in different sessions, so the farm's words come back across the gap by design. Run
`r29-farm`.

**W19: the notice board (M, Sonnet).** `game/order-templates/<id>.json`: the NPC, the goods with a choice of counts, the pay per item, the
rows it exercises, the level, and the notice as a bundle template ("Necesito {count:words} {item:plural-if(count)}. Pago {pay:words}
monedas."), text only, since notices are read. `Activities/OrderBoard`: three orders posted each play session with
`WorldRng.For(seed, "board", session)`, weighted by the due rows of each template (`ReviewDeck.Select`'s rule), added as runtime orders
(`ObjectiveSpec` with `deliverTo`, `deliver`, `session`); delivery is the existing order path. A board prop in the square (E opens it;
the notices with the ladder's English). The `systems` map's `board` section; `board_read`, `order_posted`, `order_taken`. Run `r30-board`.

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

### Core milestones this series may need

Filed by the orchestrator in `docs/nlp/IMPLEMENTATION_PLAN.md` (the next free M numbers) only when a W milestone reaches them:

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
| Save | version 6 (`systems`), version 7 (item names, wear) | W06, W12 |

## 8. What changes in code

| Project, folder | Change | Milestone |
| --- | --- | --- |
| Game.Core `Event/` | the new world event names | W01, and each system |
| Game.Core `Voxel/`, `Items/`, `Simulation/` | materials, tool specs, `GatherTicksOf`; the item catalog; wear | W02, W12, W13 |
| Game.Core `Npc/` | the patience rule; price lists and `dont_sell`; the new host kinds | W03, W04, W08, W14 |
| Game.Core `Activities/` (new) | `ProcedureBook`, `StationBook`, `ActionTable`, `ProcedureRunner`, `ProcedureProgress`, `StationScreen`, `StationPanel`, `PlayerCondition`, `Crops`, `OrderBoard` | W05 to W08, W15, W18, W19 |
| Game.Core `Language/` | `does` rows (`PhraseSheet`), `RecordAct` (`PhraseProgress`), the imperative (`LessonBook`, `TaughtVerbForm`, `ConjugationTable`) | W05, W06, W09 |
| Game.Core `Session/` | `GuideSession.Activities.cs` (the wiring); the sleep offer's dinner; the summary line | W06, W11, W15 |
| Game.Core `Navigation/` | the held beacon | W17 |
| Game.Core `Save/` | versions 6 and 7 and their migrations | W06, W12 |
| Game.Core `Scripted/` | `--station`, `--act`, `--proc-help`, `--wear` | W07, W13 |
| Game `UI/Station/` (new), `UI/Hud/`, `Graphics/Blocks/` | the station layer and layout; the wear bar and the fullness icon; icons by key | W07, W12, W13, W15 |
| The core | nothing in stages 1 to 4, unless a CLI trial finds a grading gap (section 6, core) | |

## 9. Content cost

The owner's listening is the real cost (VILLAGE_PLAN section 5): each line is two files and a listen.

| Milestone | Rows (does, says, hears) | New lines | WAV files | New words | Icons |
| --- | --- | --- | --- | --- | --- |
| W08 (Luz's teaching) | | about 4 | about 8 | | |
| W07 the station view | | | | | bowl, spoon, oven (fixtures) |
| W10 bread | 8, 3, 5 | about 14 | about 28 | harina, bol, cuchara, masa | flour |
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

- **The gate** is unchanged: `GATE_TARGET=feature/activities scripts/merge-gate.sh "<label>:<branch>"` after each milestone.
- **New scripted runs:** `r24-bread`, `r25-bread-again`, `r26-repair`, `r27-dinner`, `r28-no-arrow`, `r29-farm`, `r30-board`
  (`SCRIPTED_RUNS.md`, compared with `compare-runs.py`).
- **The go or no-go after W11** (proposed thresholds, the owner's to change):
  - *playable*: no tester is blocked; every run makes the dish;
  - *understanding*: right first time on at least 70 % of the instructions shown at card-and-audio or audio-only, by the second session;
  - *fading*: at least one chunk folds by the third session for most testers;
  - *enjoyment*: testers rate the bakery at least as well as the camp objective, and some bake again without being asked (the design plan's
    "talk to NPCs without a quest", turned to activities);
  - *recall*: the instruction items of the check battery improve from the pre-check to the recall check.
- If understanding is weak, change the ladder (more first-rung runs, slower audio) before stage 3. If it works but feels like homework,
  shorten the procedures and lean on the instant craft. If both hold, go on.

## 11. Risks and open points

- **A station view can feel like a minigame**, which the brief warns against. Keep it to holding and using things, with no timing or
  dexterity; measure enjoyment in the go or no-go.
- **`NpcService` (1,013 lines) and `GuideSession` (1,468 lines) are large.** W03, W04 and W08 touch the first; W06 puts its wiring in a
  partial of the second. A milestone that cannot keep a change small says so in its report.
- **Two save versions** (6 and 7) in one series. Each comes with a migration and the every-version test; later systems use the `systems`
  map and bump nothing.
- **Recognition is not mastery.** Acts and verb choices are understanding. Production comes from the plan and the report around each
  activity, from asking for what is missing and from the repair phrases.
- **Words.** Kitchen and farm nouns are missing from the lexicon; each milestone adds them with their gender and a reason, and regional
  choices (`bol` or `tazón`, `papa`) go through the Opus pass and the reviewer's later check (D-game-12).
- **Grading.** `se rompió el mango`, `está rota` and the imperative must be tried in the core's CLI before rows rely on them.
- **The order with other work.** R19 (the docs restructure) and the village follow-ups (a QA pass, the core's grading gaps, free talk) also
  wait for PR #20. This plan assumes R19 first (it is docs only), the follow-ups' QA pass and free talk next, and W01 after them; stage 1
  could run beside the core follow-ups, one agent at a time.
- **Scripted-run baselines** move with every content milestone; each re-baselines its own and says which.

## 12. Owner decisions (D-game-23, proposed)

**Before stage 1:**
- (a) **Pillars**: reword the design plan's first pillar and add the activity principle (the review, 2.3).
- (b) **Needs as buffs**, nothing kills (W15).
- (c) **No combat**; gear for places (W26).
- (d) **Tool tiers**: hands, stone and iron from the smith; no copper, steel or reforging (W02).
- (e) **The cast**: the seven NPCs take the design plan's roles; new NPCs only after a voice trial, in stage 5.
- (f) **Land, storage and trust in stage 5**, after the pilot.
- (g) **Items as data at W12**, before stage 3: this reverses C09's assessment for items (which said not now), for the reason in W12.
- (j) **The order**: R19, then the village follow-ups' QA pass and free talk, then this series.
- (l) **Models**: Haiku and Sonnet as the table says, with the content milestones (W10, W14, the content of W15 and W18) by Opus 5.5 at xhigh
  as the V series was, or by Sonnet.

**Before stage 2:**
- (h) **The tú imperative at `a1`.**
- (i) **Instructions as `does` rows**, recorded in the teacher's voice, with the Opus pass and the owner's listening, and the reuse report.
- (m) **New sessions 11 to 13** for the first activities; sessions 1 to 10 untouched.
- (n) **Flour sold by Luz** at one coin (W04's price list).
- (o) **The station view as an overlay.**

**After stage 2:**
- (q) **The go or no-go** on section 10's measures, and their thresholds.

**Later:**
- (k) **The learner signal** for the board and the director: the game's records now; one shared core learner model only when weak grammar
  should steer (a game change with a save migration).
- (p) **Price lines** recorded or text-only (W20).
