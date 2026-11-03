# Core Systems Design Plan: a review against `feature/village`

A review of `docs/game/design/sources/Vocaterra — Core Systems Design Plan.docx` (30 Oct 2026, exported from Claude Docs) against the code and the
decisions on `feature/village` (`3575d46e`, 173 commits ahead of `main`, PR #20 open), and of `docs/game/design/crafting-and-cooking.md` as a
principle for the docx and for the codebase. Written 2026-11-01 as a draft to iterate on. Nothing was built or run. File references are
to `feature/village`.

## Summary

1. **The conversation half of the docx is mostly built; the survival-sandbox half is not.** Already there, sometimes to the number: the
   40-slot inventory with a 10-slot hotbar, tools that stack to 1, coins with buy and sell prices, NPC orders, the recast and repair
   ladder, a vocabulary journal (the learner's book), and the guide answering "how do I make X?" with ingredients and quantities. Missing:
   health and needs, gear, tool tiers, stations beyond the table, land, storage, trust. Four of the docx's six open questions are
   already answered by decisions in force, and the brief constrains a fifth (death).
2. **The docx describes a sandbox; the code is a guided slice.** Only trunks and berry bushes can be gathered, only wood and torches
   placed, there is one recipe, and nothing has health, hunger or an owner. Most of the docx is new code on top of a lesson spine
   (sessions 1 to 10) that has to keep working.
3. **Five conflicts need the owner before any milestone:** hunger, thirst and death against the brief's "no starvation, death or
   permanent loss"; armour and weapons with no combat to use them; an 11-NPC cast against three native Spanish voices that are all taken;
   specialist words (yunque, lingote, telar) against the A2 content gate and the brief's "practical Spanish first" (none of the 26
   docx-specific words I checked are in the bundle's A1 or A2 lists, and 15 are not in its lexicon); and "bare hands can't fell trees"
   against sessions 1 to 6, which are built on felling by hand.
4. **Integration means using the seams the code already has.** Each system raises world events (so lessons, tips and objectives can
   wait on it with no code), lives in a data file with a schema and a validator rule, acts through host-event kinds with fail-forward host
   turns, makes requests as orders, and credits the learner record. The one prerequisite most docx systems share is items as data (C09,
   assessed and held).
5. **The learner model the docx relies on is fed but never read.** Each NPC's conversation carries its own copy of the core's learner
   model; the game saves all seven and makes no decision from any of them. "Errors shape future NPC requests" needs a choice of signal
   first (section 3.7).
6. **The cooking and crafting principle fits this codebase better than the docx's own loop.** The lesson runner and the phrase support
   ladder are most of a procedure engine already: `do` steps wait for world events, `listen` steps check understanding, and the four
   rungs of `SupportLevel` (English and card, card and audio, audio only, none) are the md's fading guidance almost word for word. A recipe
   is a repeatable lesson whose steps are actions.
7. **The principle also fixes the docx's central tension.** "Every need runs through a conversation" makes Spanish the toll on each need,
   which is the brief's first critical risk. Activities make it the tool: the solo route practises Spanish too (following instructions),
   and mastery buys convenience. A recipe the learner follows unaided becomes an instant craft, as "¿Lo de siempre?" shortens a
   conversation.
8. **Recommended order:** settle the conflicts in the docx; small foundations (an `item_crafted` world event, C09, a tool table instead
   of the axe special case, fail-forward as data, the free-talk follow-up from the handover); then one procedure end to end on content
   that exists (bread with Luz at her oven) as the hypothesis test; then the axe with Domingo, dinner at the camp fire, the fading
   beacon, farming across sessions and a notice board; land, storage and gear after the pilot.

## 1. The docx against the code

### 1.1 What exists, system by system

| Docx | On `feature/village` | Gap |
| --- | --- | --- |
| 40-slot inventory, 10-slot hotbar | `Inventory.SlotCount = 40`, `HotbarSize = 10` (`src/Vocaterra.Game.Core/Items/Inventory.cs`) | equipment panel, sort, trash, split, quick stack. `SlotCount` is a constant, so bags and chests need it to become a capacity |
| Stack to 100, tools to 1 | `DefaultStackLimit = 99`; the axe and the book stack to 1 (`Items/ItemDefinition.cs`) | none: the docx's open question 3 is answered |
| Spanish name with its article; English until the word is used enough | items keyed to nouns in `game/nouns.json`; word cards show the article (`el agua`); a noun is known after three spaced spoken uses (D-game-6); English fades per phrase (D-game-13) | an English gloss on item hover that hides once the noun is known. Reuse D-game-6's rule rather than a new counter |
| Chests, barrels, warehouse | a `Chest` block with art only (`Voxel/BlockDefinition.cs`) | containers, their save, ownership |
| Free-form building on plots | placed wood becomes planks, torches can be placed, chunk edits are saved; `TryPlace` checks reach and neighbours, not ownership (`Simulation/VoxelSimulation.cs:522`) | plots and permissions; most blocks are neither gatherable nor placeable |
| Tool gating | trunks (60 ticks by hand) and berry bushes are the only gatherable blocks; the axe cuts a trunk in a quarter of the ticks, as a special case (`VoxelSimulation.GatherTicksOf`, `AxeSpeedUp = 4`) | tool classes, tiers, yield, durability; stone, soil and ore |
| Health, stamina, hunger, thirst | none | all |
| Gear rating, armour, accessories | the axe | all |
| Crafting at stations | recipes are data (`game/recipes.json`, one: a torch from two wood at the table); a station is a block within reach (`Items/Crafting/RecipeBook.cs`, `CraftingRules.cs`); a craft is a click on a row | more stations, processing chains, rent, commission. A craft raises no world event (2.1) |
| Recipes learned by asking; ingredient lists with quantities | the guide answers `ask_craft` ("¿Cómo hago una antorcha?") with the ingredients and their numbers, the station and what the player still lacks (`Npc/NpcService.Guide.cs`, `Craft`) | recipes that are learned rather than known from the start |
| A cast with a language slice each | seven NPCs as data (guide Luna, the shopkeeper, the villager, fisher Mar, baker Luz, smith Domingo, farmer León), each with an agent, prompts, focus rows, a voice and lessons in sessions 1 to 10 | the docx's cast and slices differ from the built ones (1.3, 2.2) |
| Ask, trade, request, service | host-event kinds `give_item`, `take_item`, `sell`, `buy`, `point_at_noun`, `point_at_place`, `point_at_slot` (`game/host-events.json`); orders are objectives with `deliverTo` (`game/objectives/*.json`); fail-forward host turns `<npc>.cant_pay`, `unknown_place`, `cant_sell`, `wont_buy` | commission, repair, rent, grant a plot, teach a recipe |
| Understood: the NPC acts; correct: a bonus; errors recast; repair by ¿Cómo?, slower, choices | the core's feedback policy recasts, then hints, then corrects by repeat count; `Repetir`, `Más despacio`, choices and "no sé" in the panel; the stall's bottle after four misses | no world bonus for correctness: today correctness moves only the learner record and the feedback |
| Voice and text earn the same | typed input earns half the learning credit; the world reward is the same (D-game-6) | none |
| Trust per NPC | none | see 2.2 |
| Currency | `ItemId.Coin`, prices per host event | balancing |
| Notice board from the learner's weak skills | the review deck asks the most overdue phrases (X06); lessons have `review` steps; the core's learner model is updated every turn and saved per NPC, but nothing in the game reads it | the generator (2.2, 3.7) |
| Vocabulary journal | the learner's book (D-game-16): nouns and verbs used, conjugation tables, lessons, the phrase journal, recordings, the review deck | none: the docx should name the book |
| Clean and dirty water | the bottle filled at the river, the village well (`pozo`), water from the guide and the stall | sickness, boiling |
| Tenants, spoilage | none | |

### 1.2 Open questions the project has already answered

| Docx question | Answer in force |
| --- | --- |
| 2D side view or 3D | 3D, first person, voxel (D-game-8, D-game-14); the 2D slice was removed at S4 |
| Single player or co-op | everything is built for one player: one world seed, one stored time, the learner records, the turn worker (`ARCHITECTURE.md`). Co-op would rework the host's duties; I'd close this as single player |
| Stack size for tools | 1 (`ItemDefinition`) |
| Death penalty | the brief: no starvation, death or permanent loss from language mistakes; D-game-8: nothing gated behind language or voice |
| Land upkeep | not decided. I'd say none: upkeep is a chore loop, and it puts a timer on something earned through Spanish |
| What voice earns over text | pronunciation feedback and full learning credit (typed earns half, D-game-6); world rewards are equal |

### 1.3 Where the docx conflicts with decisions in force

1. **Survival stats.** "At zero: death and respawn", health that drains from starving and thirst. The brief puts "hunger/thirst timers
   that can kill or permanently punish" under *leave for later*, and its critical risk 2 is beginners punished for not knowing Spanish.
   The docx's guardrail (slow drain, an option to switch off) is weaker than the rule in force.
2. **Gear without combat.** Armour, weapons, defence and maximum health assume combat, which the brief defers and the code does not
   have. With no combat, the gear rating has nothing to measure.
3. **The cast and the voices.** The docx has eleven NPCs, five of them women. Kokoro has three native Spanish voices and all three are
   taken; the baker speaks with the Italian `if_sara`, the smith with `im_nicola`, and another woman would need a French or Portuguese
   voice or a speed variant, after a trial (VILLAGE_PLAN 2.6). Several docx roles also contradict the built NPCs: "el guía" is Luna,
   "la granjera" is León, "la comerciante" is the shopkeeper, who is a man (D-game-22 (e)).
4. **Specialist words and the content gate.** The gate is an A2 ceiling across the bundle (V08). I checked a sample of words per domain
   against the bundle's lists (`content/game-v1/cefr/a1.txt`, 397 lemmas; `a2.txt`, 663) and its lexicon:

   | Domain (my sample) | Words | A1 list | A2 list | In the lexicon, in neither list | Not in the lexicon |
   | --- | --- | --- | --- | --- | --- |
   | The docx's own (yunque, lingote, telar, cobre, acero, pico, pala, azada, estaca, cofre, alquimia, notario, tabernero, …) | 26 | 0 | 0 | 11 | 15 |
   | Cooking nouns (huevo, leche, agua, plato, taza, sal, sartén, olla, cuchara, harina, …) | 31 | 11 | 7 | 5 | 8 |
   | Cooking verbs (poner, abrir, esperar, calentar, lavar, sacar, cortar, mezclar, echar, …) | 23 | 4 | 6 | 9 | 4 |
   | Crafting and repair (madera, piedra, cabeza, arreglar, romper, unir, martillo, cuerda, clavo, …) | 21 | 3 | 7 | 7 | 4 |
   | Farming, needs and the body (campo, lluvia, frío, calor, sed, doler, plantar, regar, …) | 20 | 7 | 6 | 2 | 5 |

   The lists are short, so even everyday verbs (cortar, mezclar) fall outside them, and every new domain will need lexicon entries and
   waivers. But the docx's words are the worst case and the least useful outside the game, which is the brief's risk 7 ("keep specialist
   game vocabulary secondary").
5. **Bare hands.** "Bare hands can only pick up loose items." Sessions 1 to 6 rest on felling trunks by hand: the camp's five wood, the
   oven order's five, and the two the smith needs to make the axe. Keep hands as the slowest tool, not as no tool.
6. **The language slice per NPC.** The docx gives numbers and gender to the blacksmith and directions to the registrar. The built arc
   gives directions to the fisher (session 4), numbers and prices to the baker (5), people and jobs to the smith (6), adjectives and
   `gustar` to the farmer (7). The built arc is recorded and tested. Keep it, and give each NPC the docx systems that fit its slice.
7. **"Every need runs through a conversation."** With the solo route kept, this gates nothing, but it frames Spanish as the toll on each
   need, and that is the brief's first critical risk ("language becomes a tax on gameplay"). Section 3 proposes a different centre.

## 2. Making the docx's systems fit the code

### 2.1 The seams every new system should use

- **World events and triggers** (C06). A world event for every meaningful moment (`item_crafted`, `meal_eaten`, `tool_broke`,
  `plot_granted`, `step_done`) lets lessons' `do` steps, tips, objectives and the session log wait on it with no code
  (`Event/Trigger.cs`). Today a craft raises a simulation event that only marks the item as learnt (`Session/VoxelSession.cs:354`), so no
  lesson or objective can wait for "make a torch". That is the first fix for any crafting design.
- **A data file, a schema and a validator rule** (C01 to C08): one file kind per system under `content/game-v1/game/`, its schema in
  `content/schema/`, loaded once by `GameData`, cross-checked by `GameDataValidator`, its English in `content/locale/en/`.
- **Host-event kinds and fail-forward host turns** (V02, V03). Commission, repair, rent, grant a plot and teach a recipe are each one
  kind in `HostEventTable`, one case in `VoxelSession.ApplyHostEvents` and a schema change. What the world cannot do goes back to the agent
  as `<npc>.<reason>`, and the NPC says why. The core sees opaque strings and needs no change (ADR 0009).
- **Orders** are already the docx's *Request*: an objective with `deliverTo`, `deliver`, `session` and `lesson`.
- **The learner record.** Phrase progress (support rung, supported, unaided, heard, answered, transfer) and word progress, with the
  spaced gaps of D-game-6, so that no system can be farmed for progress.
- **The house rules.** Game.Core reads no clock and draws no `System.Random`: timers run in stored time or play sessions, draws come from
  `WorldRng` on the world seed. A new save field bumps the save version, with a migration and the `save-v1.json` fixture. A screen is a
  Core model plus a Game layer with layout delegates, as crafting is.

### 2.2 System by system

**Items first (C09).** Every docx system multiplies items (tool classes and tiers, food, seeds, containers, clothes), and items are a
code enum with their icons and held sprites in code. Items as data (`game/items.json` with stack, tool, food and equipment properties;
names kept stable, because saves store them) is the prerequisite. C09 is held until the owner asks (`CLAUDE.md`), so this is decision 7.

**Tools.** Replace the axe special case with a table: a block has a material class (wood, plant, stone, soil), a tool has a class, a tier,
a speed and a yield, and `GatherTicksOf(block, held)` reads the table. Hands stay tier 0 for wood and plants so the built sessions hold.
Two or three tiers named by everyday materials (hands, stone, iron from the smith) give the NPC route its value. Copper, steel and
reforging add grind without adding Spanish (`un hacha de hierro` and `un hacha de acero` teach the same structure), and they need ore,
caves and a furnace the world does not have.

**Stations.** The village already has the docx's NPC stations as props: the smith's forge (a fire ring) and his stone anvil, the baker's
oven (V04). "Rent a station" can be "work at Domingo's anvil, with Domingo", with nothing new built. New stations are blocks with a noun,
as `mesa` is. "Commission" is a host-event kind with a ready time in stored time or the next play session.

**NPCs.** Fit the docx's roles onto the seven NPCs and add at most two, after a voice trial:

| Docx role | Built NPC | Why |
| --- | --- | --- |
| Guide | Luna (la guía) | already the guide, the review hub and the one who answers recipe questions |
| Mayor, land registrar | the villager, given a name and the title of alcalde | he already introduces people and answers directions to any place; describing a claim is the same language turned round |
| Merchant, warehouse keeper | the shopkeeper | already sells at the stall. `buy` (V15) is at the baker, the farmer and the smith, and can join him |
| Tavern keeper | baker Luz | bread, an oven, numbers and prices: the natural cooking teacher |
| Blacksmith, carpenter | smith Domingo | already makes the axe and buys wood ("trabajo con fuego y madera") |
| Farmer | León | already the farmer |
| (not in the docx) | fisher Mar | fishing is an activity of its own (3.6) |
| Healer, tailor | new, if the voices allow | body and symptoms, clothes and colours: Spanish a learner uses outside the game |

**Trust.** Derive it from evidence the game already logs (turns understood at that NPC, orders delivered, phrase uses credited there,
gifts), with diminishing returns per play session, kept in `NpcSave`; unlocks are data in the NPC file. Gate "¿Lo de siempre?" on the
learner record as well as on trust: offer it only when the phrase the order needs is at the last rung (said unaided, spaced). Convenience
then follows mastery and never removes practice that is still due.

**Notice board.** The docx's best idea, and close to free. Order templates as data, each naming the phrase rows it exercises; a generator
picks from them with `WorldRng` on the world seed and the play session, weighted by what is due (the review deck's rule). The notices are
written Spanish, so they are reading practice and need no recordings (lines rendered at run time are text-only by design,
`Sound/NpcVoice.cs`); the NPC's lines at delivery reuse the order pools that exist. Weighting by weak grammar (the docx's "weak number
agreement → Necesito tres lingotes") needs the decision in 3.7.

**Player stats.** If adopted, make needs buffs, not damage. A meal gives a timed buff (stamina regeneration, gather speed, walking speed)
in stored time; hunger takes buffs away and slows stamina, nothing more; nothing kills by default. Raw, cooked and NPC meals become three
sizes of buff. The low-meter lines work better said by the learner to someone who can help ("Tengo hambre" to Luz is production with a
reason) than spoken by a player character who has no voice.

**Gear.** Without combat, aim gear at places: warm clothes for the snowy mountain (cold drains stamina, never health), a lantern for
night and caves, boots for the river. "Vuelve cuando tengas ropa de abrigo" works where "mejor armadura" has nothing to fight. The rating
becomes readiness for an area, and the tailor's clothes and colours become things the player needs.

**Land.** The player's cliff camp (D-game-15) is already the starter plot, with its own fire ring. Granting it after the mayor's
conversation would put a quiz at spawn, which D-game-15 rules out. The first plot to earn is a village lot from the villager (numbers,
prices and places from sessions 4, 5 and 10). The frontier claim is the directions lesson turned round: the learner describes the place,
the game resolves the landmark through `Navigation/ScenePlaces` and checks the direction against the stake, on the worker as
`point_at_slot` does, and a vague description raises `<npc>.vague_place` as a host turn ("¿Cerca de qué?"). Plots are one more check in
`TryPlace`. The docx calls land its riskiest idea. It is also the largest code change (gatherable and placeable blocks, ownership,
saves), so test it after the cheaper hypothesis of section 3.

**Storage.** A chest is an `Inventory` with a capacity. Labels the player types go through the core's lexicon and count as typed word
uses (half credit), so the docx's "low-stakes writing practice" becomes practice with feedback.

**Fail-forward as data.** "A misunderstanding never dead-ends" is code by NPC id today (`NpcService.ShopkeeperPatience`). Every new
system needs the same clause, so make it a field (`patience`: after N misses offer choices, then act anyway), and have the docx say for
each system what happens when the learner cannot say it.

**Free talk first.** The phase B handover (section 5) found that 9 of 27 practised sentences get "No te entendí" from the NPC who taught
them, and that free talk credits only an NPC's focus rows. Chat that builds trust, commissions and services all depend on open prompts
that accept more, so that follow-up should land before any of them.

### 2.3 Suggested edits to the docx

- Pillar 1 becomes: "Every need can be met through Spanish, in a conversation or in an activity. The solo route exists, and it practises
  Spanish too." Add the activity principle as a pillar (section 3).
- Close the four settled open questions (1.2) and keep death penalty and land upkeep as decisions, with the recommendations.
- Replace the cast table with the mapping in 2.2 and the built language slices.
- Cut the tier ladder to two or three everyday materials; drop reforging.
- Player stats: buffs, no death. Gear: aimed at places.
- Name what exists: the book, orders, the review deck, the guide's recipe answer, coins and prices.
- Prototype slice: the village, the starter plot (the camp) and tree to axe to planks exist already; change the slice to test what is
  new (section 4).
- Redraw the loop with the activity inside it:

```
  a need ──► ask an NPC ──► the NPC trades, orders, teaches
    ▲                              │
    │                              ▼
  food, tools, land ◄── do the activity in Spanish: instruction → action → result
                                   │  help fades as each instruction is understood unaided
                                   ▼
                     mastery → instant craft, "¿Lo de siempre?"; what is due comes back
```

## 3. The activity principle: cooking and crafting in this codebase

### 3.1 The request

`crafting-and-cooking.md` asks for systems deep enough that ordinary play repeats Spanish in context, so the game does not depend on
bespoke quests and NPCs. Cooking and crafting are the references: real procedures with tools, materials and ordered steps; guidance that
fades from highlighted, translated, spoken instructions, to Spanish only, to spoken only, to higher-level steps the player completes
alone; and the same verbs and nouns met again in other contexts.

### 3.2 Most of the engine exists

| The md wants | The code has |
| --- | --- |
| A step-by-step walkthrough | a lesson: typed steps as data (`content/schema/lesson.schema.json`: explain, model, listen, repeat, answer, do, recap, review) |
| A step completed by doing | a `do` step waits for a world event with an exact NPC and detail, and can point the beacon at a noun or a place |
| Highlight the right item or tool | the beacon on a noun's nearest block or a named place (V02) |
| Spanish text, audio and translation, then less | `SupportLevel`: English and card, card and audio, audio only, none (`Language/Phrases/PhraseProgress.cs:26`), per phrase; it steps down on spaced unaided uses and back up after "no sé" |
| Measure understanding, not clicks | phrase records count `Heard` and `Answered` (comprehension evidence) apart from production |
| Repetition at the right time | D-game-6's growing gaps (10 minutes, then an hour; a new play session meets either); the review deck picks the most overdue |
| Spanish quantities and lists | the guide's recipe answer renders "dos bloques de madera y seis bloques de piedra" with numbers and plurals |
| An NPC who speaks first | host-initiated agent turns (`Advance`, core CR-015), with host slots |
| Credit without an utterance | `LearnerModel.StepSkills(skills, correct)` (core API section 4) |

What is missing: the step's action (a tool applied to a target with a verb), a station view to do it in, steps that merge into
higher-level instructions, and a runner that repeats rather than completing once.

### 3.3 The design: a procedure is a repeatable lesson whose steps are actions

```json
{
  "id": "pan", "output": "pan", "station": "horno", "teacher": "baker", "level": "a1",
  "steps": [
    { "id": "harina", "verb": "poner", "object": "harina", "into": "bol", "line": "proc.poner.harina" },
    { "id": "agua", "verb": "echar", "object": "agua", "into": "bol", "line": "proc.echar.agua" },
    { "id": "mezclar", "verb": "mezclar", "object": "masa", "tool": "cuchara", "line": "proc.mezclar.masa" },
    { "id": "horno", "verb": "poner", "object": "masa", "into": "horno", "line": "proc.poner.masa.horno" },
    { "id": "sacar", "verb": "sacar", "object": "pan", "from": "horno", "line": "proc.sacar.pan" }
  ],
  "chunks": [ { "line": "proc.hacer.masa", "steps": ["harina", "agua", "mezclar"] } ],
  "goal": "proc.hacer.pan"
}
```

- **Actions, not answer menus.** In the station view the player picks a tool (or the hand) and a target, and the pair decides the verb.
  Where a pair allows several (an onion can be cut or peeled; eggs broken, beaten or poured), a short choice of Spanish verbs appears,
  glossed only at the top rung. Real recipes make that ambiguity common, which is what makes the verb worth understanding. A choice is a
  recognition signal (D-game-10), so it counts as understanding, never as production.
- **Instructions are phrase-sheet rows of the kind the learner hears.** The sheet already has such rows (the NPCs' questions); an
  instruction gets the same columns, the Opus pass and a recording. A step done right after its instruction counts as `Answered` at that
  row's rung. A wrong action is a slip: the instruction is repeated, then slowed, then highlighted. Help is never shamed and never spoils
  the dish; it only makes the use supported instead of unaided.
- **The ladder per row, plus granularity.** Rung 1: highlight, Spanish, audio, English. Rung 2: Spanish and audio, English on the help
  key. Rung 3: audio only, with `Repetir` and `Más despacio`. Rung 4: a chunk's instruction replaces its steps ("Haz la masa") once every
  row in the chunk is at rung 3 or below. Rung 5: the goal alone ("Haz pan"). D-game-6's gaps apply, so baking the same bread five times
  in a row does not fade it.
- **Mastery buys convenience.** When every row of a recipe is at the last rung and the recipe has been finished unaided in two separate
  play sessions, it becomes an instant craft (one click, as the torch is today). The game stops repeating a procedure when it stops
  teaching, which answers the brief's risk 9 (repetition that is tedious). The review deck can still bring a mastered recipe back as a
  teach-back: Luz has forgotten a step, and the learner tells her.
- **Speaking inside the activity**, through intents that exist. The getting-unstuck phrases as controls (`ask_repeat`, `ask_slower`,
  `ask_help`, `dont_know`: "Otra vez, por favor" may become the most used sentence in the game). Asking for what is missing ("¿Tienes
  huevos?", `ask_availability`, `request_item`). The plan and the report around the activity (VILLAGE_PLAN principle 7: "¿Qué vas a
  hacer?" "Voy a hacer pan", and afterwards "¿Qué hiciste?" "Hice pan"). Later, telling an apprentice the steps, which produces the
  imperative and needs a core milestone to grade.
- **Quality is a bonus.** Steps in order, the right tool, the right amount: a better dish and a bigger buff. Understanding is rewarded and
  never required; the dish is always made.
- **Code shape.** `game/procedures/<id>.json` with a schema, loaded by `GameData` and checked by `GameDataValidator` (every noun a
  catalogue noun with an item or a block, every verb in the lexicon within the procedure's level, every line in the bundle and recorded).
  A `ProcedureRunner` in a new `Activities/` folder of Game.Core, modelled on the lesson runner, raising `step_done` and
  `procedure_done` world events and recording phrase uses. A `StationScreen` model in Core and its layer in Game, as `CraftingScreen` and
  `CraftingLayer` are. Each recipe's progress in the save.

### 3.4 Constraints that shape the content

- **Spoken instructions must be recorded.** Lines rendered at run time have no voice by design (`Sound/NpcVoice.cs`: "a templated world
  line of the guide … has no voice by design"), so the audio rung needs a closed bank of recorded instructions, and recipes should be
  built from it. Reuse is the point twice over: the more recipes share "Pon … en el bol", the more the learner hears it, and the less
  there is to review and to listen to (the owner's listening is the real cost, VILLAGE_PLAN section 5). A report of each recipe's new
  lines and each line's number of recipes would keep authors honest.
- **Latin American neutral Spanish** (D-game-8, core ADR 0005) matters more in the kitchen than anywhere else: papa not patata, jugo not
  zumo, frijoles, aguacate, with accepted variants for the rest. Latin American dishes suit it: arepas, quesadillas, guacamole, arroz con
  leche, chocolate caliente, and bread.
- **The imperative is the grammar of instructions**, and it is not taught yet: lessons and the book know `pres`, `pret`, `ir-a` and
  `fut`. The core already reads it (`corta` is the second-person imperative when it opens its clause, core API section 5, and the lexicon
  carries the imperative forms), so understanding instructions needs nothing from the core. A lesson for the tú imperative (pon, echa,
  corta, mezcla, saca; pon and haz are irregular and in nearly every recipe) adds `imp` to the lesson schema and a column to the book's
  table, as V16 did for `fut`. Two things are core work: grading an imperative the learner produces, and generating lines, since the
  template engine's `conj(person, number, tense)` has no mood (until then, instruction lines are written out).
- **Words.** From the table in 1.3: most cooking and farming words are within A2 or one waiver away, but the lexicon lacks common kitchen
  nouns (sartén, olla, cuchara, tenedor, harina, cebolla). Each domain is a content milestone: lexicon entries with their gender, and
  waivers. At this scale a per-domain word list (a level and a reason per word) would serve better than the marked block in `a1.txt`, and
  that is a lint change.

### 3.5 Three references on content that exists

1. **Bread with Luz.** Her oven, her bread, her numbers and prices (session 5) and the oven-wood order are built. A lesson teaches six to
   eight instructions with full support and the tú imperative; the plan and the report reuse session 5's loop; afterwards the player can
   bake again, at her oven or later at their own. Bread is already an item the stall and the market use. This is the smallest end-to-end
   test of the md.
2. **The axe with Domingo.** Today the `axe-handle` order hands the axe over. As a procedure, he talks the learner through it: cut the
   handle, put the head on top, hold it, hit it with the hammer. Positions (encima de, al lado de) reuse session 4's directions. A worn axe
   brought back for repair is the report step, with a reason to speak: "Se rompió el mango" (the preterite of sessions 5 and 8).
3. **Dinner at the camp fire.** Every session ends at the player's own fire, and sleeping there starts the next one (V12c). Cooking dinner
   before sleeping makes that moment a nightly procedure, the meal's buff carries into the next session, and since a new session meets
   D-game-6's gap, each night's cooking earns spaced credit by construction.

### 3.6 The same lens on other systems: one activity per NPC

| System (NPC) | The activity | The Spanish it repeats | What exists | New code |
| --- | --- | --- | --- | --- |
| Navigation (the villager, Mar) | follow directions without the arrow | places, izquierda and derecha, cerca and lejos, seguir, cruzar | `point_at_slot`, `beacon_reached`, `ScenePlaces` (V02) | the beacon fades with the rung of the direction line; arriving unaided is evidence of understanding |
| Farming (León) | dig, sow, water, wait, harvest | plantar, regar, cosechar, semillas, the weather | the wheat field, farmland and wheat blocks, the bottle and the river; sleep as the boundary | crops that grow by play session (the gap again); the runner |
| Trading (the shopkeeper, Luz, León) | prices change with the market day, so they must be asked | ¿Cuánto cuesta?, numbers to 20, más barato que | `sell`, `buy`, `ask_price`, session 10 | prices drawn per session with `WorldRng`; recorded price lines for 1 to 20 |
| Repairs (Domingo) | say what broke, fix it at the anvil | parts (mango, hoja), está roto, se rompió | the smith, the anvil prop | durability; a `repair` host kind |
| Building (the villager) | build to a description: a tenant's room "con una ventana y una mesa" | parts of a house, positions, numbers, adjectives and colours | placing blocks, chunk edits | blueprint checks, more placeable blocks |
| Gathering (a healer) | stays immediate (brief risk 1); foraging by description for a healer: "la flor amarilla, la hoja pequeña" | colours and sizes (session 7) | yellow and white flower blocks | gatherable herbs |
| Needs (a healer, Luz) | say what is wrong to someone who can help | tengo hambre, sed, frío, sueño; me duele | none | buffs; the healer |
| Fishing (Mar) | bait, cast, wait, pull, then cook the catch | pez and pescado, lanzar, esperar | the fisher, the river | the runner |

### 3.7 The learner record as director

The deepest integration is to let the learner record choose what the simulation asks for: which recipe Luz suggests today, which orders
the board posts, which directions an NPC gives without the arrow. The review then happens inside play: the docx's "spaced repetition
hidden inside quests", applied to every activity. Two cautions:

- **Which signal.** The game-wide signals are the game's own records (phrase and word progress), which already drive the review deck.
  The core's learner model, which also tracks error families (`conj`, `gender`), is one copy per NPC conversation (`NpcService.Save.cs`),
  and the game never reads any of them. To steer by weak grammar, the host would share one learner model across the NPCs (a game change
  with a save migration). I'd start with the review deck's "most overdue" rule and add error families later.
- **The brief defers adaptive learning** for the prototype. The "most overdue" rule is already accepted (X06); anything smarter belongs
  after the pilot (R12 in the improvement backlog).

### 3.8 Risks

- **Minigames.** The brief asks to avoid extra minigames. A station view must stay simple (pick a tool, pick a target, no timing or
  dexterity at first) and be measured in the pilot for fun.
- **The listening cost.** Each instruction is two recordings and a listen; reuse and the report in 3.4 are the control.
- **Recognition is not mastery.** Verb choices and actions show understanding. Production comes from the plan and the report, from
  asking for what is missing, and later from the teach-back.
- **Real recipes are long.** Start at three to six steps and chunk early.
- **An in-world kitchen is costly** (block states, animations); the overlay comes first.

## 4. A staged plan

| Stage | What | Size | Gate |
| --- | --- | --- | --- |
| 0 | Settle the docx: the conflicts (1.3), the edits (2.3), the decisions (5) | docs | the owner |
| 1 | Foundations: `item_crafted` and a `step_done` family of world events; C09, items as data with properties; the tool table; fail-forward as data; the handover's free-talk follow-up | S to M each | the merge gate |
| 2 | One procedure end to end: schema, loader, validator, runner, the station view; bread with Luz and a lesson for the tú imperative; a recipes page and an `imp` column in the book | M to L | a pilot measure: first-try understanding per instruction per rung, sessions to fade, and whether players enjoy it |
| 3 | The axe with Domingo; durability and repair; dinner at the camp fire and meal buffs | M | |
| 4 | The fading beacon; farming across sessions; the notice board; market prices per session | M each | |
| 5 | The docx's sandbox systems, with pilot data: land and claims, storage, gear for places, trust, tenants | L | the brief's decision gates |

Stages 2 to 4 change how practice is woven into the world that exists, so they fit either branch of the brief's decision gate ("if
recall or transfer is weak, improve the learning loop"; "if learning works but feels tedious, change how practice is integrated into
objectives"). Stage 5 expands the world and should wait for the pilot. The docx's playtest measures (tools from the blacksmith against
self-crafted, talks without a quest) stay useful; add first-try understanding per rung and how often players choose an activity they no
longer need.

## 5. Decisions for the owner

1. **Pillars.** Reword pillar 1 and add the activity principle as a pillar (2.3, section 3)? I'd say yes.
2. **Needs.** Buffs only, and no death from hunger or thirst by default? I'd say yes.
3. **Combat.** Leave combat and armour out, and aim gear at places? I'd say yes.
4. **Tiers.** Hands, stone, and iron from the smith; no copper, steel or reforging? I'd say yes.
5. **Cast.** The seven NPCs take the docx's roles, with at most two new ones (a healer, a tailor) after a voice trial? I'd say yes.
6. **Land.** The camp is the starter plot, the first lot comes from the villager, and land is built after stage 2? I'd say yes.
7. **C09.** Start items as data, held since the content series closed? Anything past a handful of new items needs it.
8. **The imperative** as the next verb form: understood first, produced later after a core milestone? I'd say yes.
9. **Instructions as content.** Recorded phrase-sheet rows with a reuse report, the Opus pass and the owner's listening, as for lessons?
   I'd say yes.
10. **Order against the handover's follow-ups** (the QA pass, the core's grading gaps, free talk): free talk first, and stage 0 alongside
    the rest?
11. **The learner signal** for the director (3.7): the game's records first, one shared core learner model later?
12. **Models for a new series:** the V series ran Opus 5.5 at xhigh effort; the default policy is Haiku, with Sonnet for design-heavy work.
