# The village and the lessons: the V series

A plan written on 2026-10-30 at the owner's request, from a read of the plans, the decisions, the content bundle, the locale, the
game's code and the models on this machine (`main` at `10afbd6`, after X06 and the mobile report). Nothing was built or run; the
one computation is the village's footprint, drawn from `game/scene.json` (section 2.2). Its goal: **more Spanish in the same world**: a
bigger village with more people, each with a reason to be spoken to, and a connected run of lessons from the end of A1 into A2, in which
every verb the player meets is used in the present, the past and the future (section 2.9 and the ladder in 3.5; added the same day on the
owner's second line).

**Status:** phase A done on 2026-10-31 (V01 to V07, V09, V10 to V12 and the follow-ups V12b and V12c, all gated on `feature/village`); **phase B started on 2026-10-31** on the owner's word after a playtest (D-game-22 (g): all of it on `feature/village`, reviewed by the owner at the end; the provisional voices are still to be heard) and **was done on 2026-11-01** (V08, V13 to V18, the follow-up V15b and V09 again, with the core's M56 to M62 and M62b; 7259 tests passing; the owner's review is next) (owner, 2026-10-30, D-game-22: phase A now, the proposals of section 6 accepted, the farmer León,
provisional voices recorded now and heard afterwards; every milestone by Opus 5.5 at xhigh effort). Its milestones are V01
to V17 (section 4), on the feature branch `feature/village` (never `main`; the owner merges by pull request). Commit prefix `V<nn>:`.

**The owner's brief (2026-10-30):** take the current map and make the village bigger, with more NPCs (a blacksmith, a baker, and so on) and
more lessons; avoid making the map larger unless an objective or a lesson genuinely justifies it; make the NPCs interlink; use the map's
space intelligently; connect the lessons; aim at A1 to A2.

**What this plan does not do.** It adds no terrain, no site and no road: the village grows inside its own clearing (section 2.2), the
fisher stands on the bridge's east landing, which exists, and the lessons send the player to places already on the map. It changes no
core project (`Vocaterra.Nlp`, `Vocaterra.Nlp.Es`): every grammar it needs is a bundle intent, and section 5 names the three places the core
must be tried before a lesson relies on it. It keeps D-game-12: every new row stays `llm-opus (provisional)` until a Spanish speaker reads
it, and the owner listens to every new recording (section 2.6 says why the listening list is the real cost of this plan).

## 1. The request

> Take the current map, make the village bigger with more NPCs (blacksmith, baker, etc.) and have more lessons. Avoid making the map
> larger unless an objective or lesson genuinely justifies it. Make the NPCs interlink, and use the map space intelligently. Lessons should
> be connected, and aim for A1 to A2 content.

And, the same day, after the first draft: "The lesson should also include past, present, and future use of verbs." So every lesson from
session 4 on uses its verbs in the three times, around something the player has just done or is about to do (section 3.5, principle 7),
and the series ends with the simple future at A2 (session 9). Section 2.9 says what the core, the runner and the book already do for tenses.

Two earlier owner answers bear on it (`docs/game/research/2026-10-23-improvement-backlog.md`): R14 (numbers and prices, "yes") and R15
(more NPCs with other voices, "yes for the voices and the unreviewed text"); R18 (an orders loop, "yes, better after the first pilot") is
taken up here on the owner's new request, in the smallest form that gives numbers a use (section 3.4). The brief's own caution stands:
"if recall or transfer is weak, improve the learning loop before expanding the world" (`PROTOTYPE_BRIEF.md`, decision gates). This plan
therefore has a cut line after phase A (section 4): three lessons that finish A1, before anything at A2.

## 2. What the investigation found

### 2.1 The content today

| | Count | Where |
| --- | --- | --- |
| Lessons | 3: `first-words` (37 steps, session 1), `personas` (20, session 2), `ayer-y-manana` (19, session 3) | `content/game-v1/game/lessons/` |
| Phrase rows | 46 (28 the learner says, 18 understood) | `content/PHRASES.md`, `game/phrases.json` |
| Intents | 18 | `intents/intents.json` |
| Prompts | 28 (6 of the NPCs, 22 of the lessons) | `prompts/prompts.json` |
| Dialogue | 109 pools, 175 variants; 430 WAVs (215 lines at two speeds, 33 MB) | `dialogue/dialogue.json`, `speech/` |
| NPCs | 3: the guide (Luna), the shopkeeper (`Tendero`), the villager (`Aldeano`) | `game/npcs/` |
| Nouns | 19 (`árbol` to `libro`); items: wood, torch, berry, bottle, water bottle, bread, book | `game/nouns.json`, `Items/ItemDefinition.cs` |
| Objectives | 3: `meet-guide`, `camp` (wood 5, fruit 3, torch, water), `light-fire` | `game/objectives/` |
| Checks | 31 items (16 heard, 15 said), about 7 minutes a battery | `game/checks.json` |
| Verb forms taught | querer, tener, necesitar (all persons, present); ser, estar, ir (present); ir a + infinitive (yo, nosotros); comí, bebí, fui and the tú forms (yo and tú only, no regular `-ar` preterite, no simple future) | the lessons' `verbs` |

The four nouns `madera`, `fruta`, `agua` and `pan` carry every verb exercise (X00's "thin content" finding). No row uses a number, a price,
a place other than the river, a person's job, an adjective or `gustar`. The lexicon has all of these (section 2.7).

### 2.2 The village and the valley

The world is 384 x 384 x 128 (`game/scene.json`, S13). The village is a levelled disc of radius 20 (blend 8) at (292, 218) on the valley
floor, inside a tree clearing of radius 36 (`trees.clearings`). The valley floor (`relief` rows 9 to 19, columns 13 to 19, height 36) runs
from x 208 to 320 and z 160 to 300: flat ground all round the village. The lake (212, 294, 46 x 28) is south-west, the river 50 blocks west,
the bridge's east landing (`bridge.east`, 238, 205, radius 3) on the road 63 blocks from the square. The signpost says `Tienda / Río /
Montaña`; `montaña` has no scene point.

The footprint today, one character a block, x east, z south, from `scene.json` (`+` levelled ground, `.` the paved square, `=` the road):

```
 -16               ++++++++++++++TTTTT++++++              tower
 -10          ++++++++i++++++++++++++++++++++++++
  -9          +++++++++++SS+++++++++++++HHHHH++++         stall (counter west), house 1
  -6        +++++++++++..SS....i.......+HHHHH++++++
  -3        +++++++++++..c.............++++++++++++
  -2        +++++++++++..c.............+fffffff++++       the wheat field
  +0       ++++++++++++................+fffffff+++++
  +3 =      +++++P+++++.........V......+fffffff++++       signpost, villager
  +5  =     ++++++====+................+HHHHH++++++       house 2
  +7    =  ====+++HHHHH-------++-------+HHHHH+++++        house 3, fences
 +11           +++HHHHH+++++++++++++++++++++++++
```

Free, flat and inside the clearing: the whole north-west quarter (west of the tower, north of the stall), the north-east quarter (east of
the tower), and the south, below the fences. A disc of radius 24 (blend to 32) still leaves 4 blocks to the clearing's edge, so two more
buildings and a well fit **without a new site, a new road or a change of terrain**. Walking is 4.3 blocks a second (`VoxelPhysics.cs:32`):

| Walk | Blocks | Time |
| --- | --- | --- |
| The guide's camp to the square, by the road | 149 | 35 s |
| The bridge's east landing to the square | 63 | 15 s |
| The bakery's door to the smithy's door (proposed) | 18 | 4 s |
| The square to the field | 8 | 2 s |
| The square to the forest's edge (wood) | 36 | 8 s |

So a lesson can move between two villagers for a few seconds' walk, as `personas` does (villager to stall), and an order for wood costs
under a minute of gathering.

### 2.3 What the systems already do

- **Lessons** are data with eight step types; since X06 a `review` step asks the most overdue phrase of an earlier lesson at any NPC
  (`LessonBook.cs:254`). A lesson unlocks by play session and by `after` (`LessonRunner.cs:248`); a later session's lesson is named by the
  objective line and pointed at by the beacon (`VoxelSession.BeginPlaySession`). A `do` step waits for a world event with an NPC or a detail
  and may point the beacon at a catalogue noun (`LessonRunner.cs:913`). A step may declare the person and the tense its answer must have.
- **Objectives** count world events per step up to a target, with named code hooks (`ObjectiveBook`, C07). Every objective counts from the
  start; there is no session or lesson gate on an objective.
- **NPC host events** map to two actions, `give_item` (one item; the bottle once) and `point_at_noun` (the nearest block of a noun)
  (`HostEventTable.cs:40`, `VoxelSession.cs:584`). The core's agents branch on intent, error tags and `communicated` only
  (`AgentDefinition.cs:37`), never on a slot value: the villager points at the river whatever place was asked (`villager.json`, `villager.point`).
- **The beacon** can already go to a named scene point or labelled prop (`Beacon.SetPoint`, `Navigation/Beacon.cs:33`; the session's start
  and the `point-own-camp` hook use it), and clears when reached (`BeaconDirector.ClearIfReached`), but nothing logs the arrival.
- **The core's grammar** already parses what the new lessons need: `[NUM:qty]` in `request_item` (`quiero dos panes` gives `qty=2`; `quiero dos
  pan` is `noun.number`), `[ADJ]`, `[WORD]`, `[PREP]`; and checks `conj.person`, `conj.tense`, `conj.irregular`, `gender.agree`, `noun.number`,
  `ser_estar`, `word_order` (`SpanishAgreementChecker.cs`). Adjective agreement and word order are checked today and taught by no lesson.
- **The lexicon** has the numerals 0 to 29 (`seed.json`), `costar` (`cuesta`, `cuestan`), `gustar`, `hacer` (`hice`, `hizo`), `haber` (`hay`),
  `moneda`, `manzana`, `herrero`, `herrería`, `hacha`, `pan` (`panes`) and the place nouns of the scene (section 2.7 lists the gaps).
- **The playtest** freezes a bundle and runs 31 items before and after; the recall check loads the same bundle (`PLAYTEST.md` sections 3, 8).
- **The scripted runs** (sixteen, `SCRIPTED_RUNS.md`) and `compare-runs.py` are the regression set for the front end; a content change moves
  their baselines.

### 2.4 What is hard-wired to the three NPCs

A fourth NPC today would get the villager's prompt and opening, the guide's voice and a grey figure. The places, each small:

| What | Where | Today |
| --- | --- | --- |
| The prompt an NPC waits on in each state | `NpcService.PromptAfter`, `Npc/NpcService.cs:447` | guide and shopkeeper by id; **every other NPC gets `villager-open`** |
| The host event an NPC opens a talk with | `NpcService.OpeningEvent`, `NpcService.cs:474` | guide, shopkeeper by id; every other NPC raises `villager.greet` |
| The prompts warmed up at start | `NpcService` constructor, `NpcService.cs:220` | a fixed list of six |
| The speaker of a dialogue pool | `SpeechSpeakers.OfPool`, `Language/Speech/SpeechLines.cs:31` | `shop_*`, `villager_*`, `guide.lesson.shop.*`, `guide.lesson.villager.*`; everything else the guide |
| The Kokoro voice of a speaker | `tools/Vocaterra.SpeechRender/Program.cs:149` | three ids; any other speaker throws |
| `--talk` targets | `src/Vocaterra.Game/Startup/LaunchOptions.cs:29` | `guide`, `shopkeeper`, `villager` |
| Figures and faces | `Graphics/Figures/NpcFigures.cs:134`, `Graphics/Blocks/BlockArtVillage.cs:221`, `BlockAtlasLayout.cs:224` | three looks, four face cells; an unknown figure key is grey |
| The key-word cards before the first question | `Session/GuideSession.cs:514`, `game/words.json` `keyWords` | the shopkeeper only |
| The stall's fail-forward (the bottle after four misses) | `NpcService.ShopkeeperPatience`, `NpcService.cs:690`; `GuideSession.cs:742` | the shopkeeper only; stays code (CONTENT_PLAN 3.6) |
| The guide's offer graded as the stall's offer | `Language/Phrases/PhraseSheet.cs:252` | one prompt pair by name |

CONTENT_PLAN 3.6 moved the NPCs' data and left "the choice of the opening event" and the voice in code as open points. This plan moves the
first and the voice into `game/npcs/<id>.json` (section 3.7, V01) and leaves the shopkeeper's fail-forward where it is.

### 2.5 The gates and what a content edit moves

- **The content gate** (`scripts/merge-gate.sh:78`) runs `lint --strict --level a1 --host-pools 'guide.*'`: a lemma above A1 in a dialogue
  template, a prompt's text or an answer set's canonical answer is a warning, and `--strict` fails on warnings (`LintCefr.cs`). `cefr-check` at
  A1 and A2 only prints (`Program.cs:273`). The core's level lists are cumulative: A2 includes A1 (`CefrChecker.cs:119`). So today **every
  Spanish word an NPC speaks must be in `cefr/a1.txt`**, and the practice since S5 has been to add world words there as provisional
  (`botella`, `madera`, `antorcha`, `fuego`, `despacio`; the sheet's notes). An A2 lesson cannot pass this gate as it is; section 3.9 changes it.
- **The grammar gate** needs the LanguageTool server once per batch of new Spanish (`docs/game/README.md`, "Refreshing the cache").
- **Every bundle edit** re-hashes `game-v1`, so: the goldens may move, `Fixtures/save-v1.json` takes the new hash, the scripted runs are
  re-baselined, and the documents' counts (`README.md`, `PLAYTEST.md`) change; X06 noted that a roster test counts review lines.
- **The sheet test** (`PhraseSheetTests`) checks every row: the variants resolve to the intent at the prompt with no error, the learner-error
  example gives exactly its codes, the audio ids exist.
- **Saves** keep the NPCs in `game/npcs.json` order (new NPCs go at the end), objective steps by `<objective>.<step>`, items by enum name;
  X01 carries a save across bundles. No save version change is needed by this plan unless V03 adds a field (section 3.8).

### 2.6 Voices

Kokoro `kokoro-multi-lang-v1_0` has 54 voices; three are Spanish and all three are taken (`ef_dora` Luna, `em_alex` the shopkeeper, `em_santa`
the villager; `docs/nlp/API.md` section 6). The model's own list (read from `model.onnx`) offers, for Spanish text through the Spanish
phonemizer: the Portuguese `pf_dora`, `pm_alex`, `pm_santa` (very likely the same three timbres), the Italian `if_sara` and `im_nicola`, the
French `ff_siwis`, and many English voices, which would carry an English accent. The renderer also takes a speed. So four new NPCs cannot
each have a native Spanish voice: the honest choices are the Italian pair, speed variants of the three Spanish voices, or fewer NPCs. The
owner must listen before any line is recorded (D-game-12 step 4; V06). This is also why the plan has **one more female NPC at most** until
a voice is approved: the only female Spanish voice is Luna's.

### 2.7 Words: what the lexicon and the lists lack

Checked against `cefr/a1.txt`, `cefr/a2.txt` and the lexicon files (hand-authored and the Wiktionary import):

| Needed | In the lists | In the lexicon | Action |
| --- | --- | --- | --- |
| `panadería`, `panadero/a`, `pescador/a`, `granjero/a`, `horno`, `pozo` | no | no | `lexicon/world.json` (noun, gender); `a1.txt` as world words (section 3.9) |
| `herrero`, `herrería`, `hacha`, `moneda`, `manzana`, `precio`, `dinero` | `moneda`, `precio` A2; `dinero`, `manzana` A1; the rest no | yes | the rest to `a1.txt` as world words |
| `izquierda`, `derecha`, `lado`, `delante`, `detrás`, `enfrente` | `lado`, `delante`, `detrás` A2; the others no | Wiktionary only | `izquierda`, `derecha` to `a1.txt` (every A1 syllabus has them); the rest A2 |
| `puente`, `pueblo`, `lago`, `campo`, `montaña`, `bosque`, `torre`, `camino` | `puente`, `pueblo`, `campo`, `montaña`, `bosque`, `camino` A2; `lago`, `torre` no | `lago` no | world words: the scene labels them already (`scene.json`) |
| `uno` to `diez`, `cuánto/s`, `costar`, `hay`, `gustar`, `hacer`, `trabajar`, `vivir`, `llamar` | A1 (the numerals are function words, exempt) | yes | none |
| the colours, `grande`, `pequeño`, `nuevo`, `viejo`, `caliente`, `frío`, `barato`, `caro`, `cerca`, `lejos` | A1 | yes | none |
| names: `luna` A1; `luz`, `flor` A2; `sol`, `mar`, `domingo` A1 (masculine nouns) | | | a name that is a noun carries that noun's gender into the checker (section 5) |

The Vosk model (`vosk-model-small-es-0.42`) ships no word list (`graph/Gr.fst` only), so whether the learner can *say* a new word is checked
only by the with-models round trip (`SpokenModelTests`, Kokoro to Vosk); each content milestone runs it once.

### 2.8 Found along the way (not fixed)

- **The shopkeeper's gender disagrees across the files:** the label is `Tendero` and the voice `em_alex` (male), while the locale and the
  sheet say "she" (`locale/en/checks.json:30`, `lessons.json` `personas.p3-*`, `PHRASES.md` `que-necesita-usted`). The cheapest fix is "he"
  in the locale (outside the hash); the owner decides (section 6).
- **`PromptAfter` and `OpeningEvent` default to the villager** for any unknown agent (section 2.4): a new agent file would run with the
  wrong prompt and no error. V01 makes an NPC with no `prompts` a validation error.
- **The villager points at the river whatever place is asked** (`villager.point` is `point_at_noun: río`); `ask_directions` accepts any
  `[NOUN:place]`. V02 fixes this by pointing from the slot.
- **X06 left two things undone:** `review` steps in `personas` and `ayer-y-manana`, and the scripted runs of a warm-up
  (`IMPLEMENTATION_PLAN.md` section 4.5, X06). V10 adds the review steps to both while it re-baselines the runs.
- **The speech folder is 33 MB** and the prerender script offers `--rate 16000` "when the folder is over 20 MB". Phase A adds about 95
  lines (190 files); the owner may want the downsample then.
- Two stale comments still call the guide "Rosa" (`PhraseReview.cs:31`, `LessonRunner.cs:444`, noted at C08). No NPC here is named Rosa.

### 2.9 Verbs in three times: what exists

The owner's second line of the day ("past, present, and future use of verbs") was in the brief of S26 too (D-game-19, `SCENARIO_PLAN.md`,
"S26": forms of the verb for we, I, you; "also past, present, future... introduce them early, but of course not overwhelm the user"). What
the three sessions and the code give today, read from the lesson files, `LessonBook.cs`, `LessonRunner.cs`, `ConjugationTable.cs`,
`MorphologyGenerator.cs`, the lexicon files and `docs/nlp/API.md`:

- **Taught so far.** Sessions 1 and 2: the present of `querer`, `tener` and `necesitar` in every person, and of `ser`, `estar`, `ir`.
  Session 3 (`ayer-y-manana`, S27): `ir` in the present (voy, vas, va, van), the near future `ir a` + infinitive (voy a comer, vamos a
  comer, voy a ir) and three preterites in yo and tú (comí/comiste, bebí/bebiste, fui/fuiste), with `ayer`, `hoy`, `mañana`. So the past
  has no third person and no nosotros, no regular `-ar` form (compré), and there is no simple future.
- **The core grades a tense only next to a time marker** (`API.md` "Tense errors", M49, M51): `ayer` (and `anoche`, `anteayer`, a time
  noun with `pasado`) with a present or a simple future expects the preterite; `mañana`, `pasado mañana`, `próximo/a` + a time noun or a
  time noun + `que viene` with a preterite expects `ir a` + infinitive (or `voy` when the verb is `ir`); a conjugated verb after `ir a`
  and `ir` with a bare infinitive are errors. The present and the simple future after a future marker are clean (`mañana como pan`,
  `mañana compraré pan`); `hoy`, `luego` and `después` are not markers. Without a marker the core says nothing about tense: `compro pan`
  for "I bought bread" is correct Spanish to it.
- **The lesson judges the tense when the core cannot** (S27): a `repeat` or `answer` step declares `tense` (`pres`, `pret` or `ir-a`,
  `LessonBook.cs:597`), the right phrase in another tense is a Miss with the step's `tenseRetry`, and at an `ir-a` step a plan said in the
  present with a time word is credited with a `presentNote` ("correct Spanish is never a Miss", S27). The slot DSL has no tense constraint
  (`[VERB:comer]` matches every form), so one `state_action` grammar serves the three times; its grammars today cover `comer`, `beber` and
  `ir` only, with `(mañana|ayer|hoy)?` at either end, and `words.json` lists `mañana` as the only `nearFuture.timeWords` entry.
- **The simple future is half-known.** A lesson's `verbs` may declare `fut` (`TaughtVerbForm`: "the simple future, A2, not taught yet") and
  `TaughtVerbForm.Text` conjugates it, but a step cannot expect it (the schema and `LessonBook.cs:597` allow `pres`, `pret`, `ir-a`), and the
  book's table has three columns (present, past, future (ir a): `ConjugationTable.AllColumns`, `ui.bookColumnNearFuture`). The lexicon has
  every form: the Wiktionary import carries the future, the imperfect and the conditional of 4,080 verbs (`compraré`, `iré`, `hará`,
  `tendré`, `fuimos`, `fueron`, `vendió`, `pescó`, `gustó` are all there), merged into one lexicon at load (`ContentLoader.cs:226`). The
  hand-written seed has the present and the preterite of 31 verbs, and `MorphologyGenerator.ConjugateRegular` builds only those two
  tenses, so a `fut` cell comes from a lexicon entry; a verb outside the import would have none, and none of this plan's verbs is outside it.
- **The CEFR lists are lemma lists**, so a tense costs nothing at the gate: `compré` is `comprar`, A1. The level of a *tense* is the
  lesson's business (section 3.9: no `fut` in an `a1` lesson).
- **The words.** Every verb this plan uses is A1 (`comprar`, `vender`, `pagar`, `trabajar`, `vivir`, `buscar`, `hacer`, `cocinar`,
  `poder`, `llegar`, `venir`, `dar`, `ver`, `llevar`, `ayudar`, `esperar`) except `volver` and `traer` (A2, not used) and `pescar` (in
  neither list: the fisher's own verb, a world word like `madera`). The time words `ayer`, `hoy`, `mañana`, `luego`, `después`, `antes`,
  `ahora`, `siempre`, `nunca`, `semana`, `día`, `año`, `tarde`, `noche` are A1; `pasado`, `próximo`, `pronto`, `temprano` are A2; `anoche`
  is in neither and is not used. `campamento` (the player's camp) is A1.

## 3. The direction

### 3.1 Principles

1. **The world grows inward.** New buildings go on the village's free ground; the one NPC outside it stands on an existing site. No new
   terrain, site or road (section 2.2).
2. **Every NPC needs something another NPC or place provides**, and the player is the one who speaks to move it (section 3.4). The
   shopkeeper and the villager join the web; the guide stays the reviewer (her review and the X06 deck ask every row that names a `reviewLine`).
3. **Each lesson reuses the last one** (`after`, two `review` steps, the previous NPC naming the next), **adds one grammar point and one
   word field, uses its verbs in the three times** (the present, the preterite and the future; the ladder in 3.5), and ends by naming
   where to go next (the objective line, `lesson.<id>.objective`).
4. **A1 first, as data the gate can check** (section 3.9): sessions 4 to 6 finish A1 and are gated at A1; sessions 7 to 10 are A2 and are
   gated at A2. Phase B starts only when the owner says so.
5. **Words the player handles come from the world** (the catalogue, the scene's labels, the items), so the beacon, the book and the
   greyscale rule work on them unchanged.
6. **Nothing is gated behind speech** (D-game-8, D-game-9): an order the learner cannot ask for in Spanish is still delivered by walking
   up with the goods (section 3.4), as the stall's bottle is.
7. **Every order is a plan, a doing and a report.** Before the player goes, the NPC asks what they are going to do (the future, `ir a`:
   "Voy a buscar madera"); the `do` step is the real walk, gather or purchase; back at the NPC the question is what they did (the preterite:
   "Compré dos panes"), and the present is the exchange itself ("Tengo madera"). The same verb three times, around a thing the player
   actually did: that is the "use" of a tense the owner asked for, and the order loop of 3.4 gives it without a new mechanic. At A2 the
   same loop is told about other people (the third person: "Domingo va a hacer un hacha", "hizo un hacha") and then promised (the simple
   future: "Mañana compraré pan").

### 3.2 The village plan

The levelled disc grows from radius 20 to 24 (blend 8, so the slope ends at 32, inside the clearing's 36). Everything below is a scene
edit (`game/scene.json`) with the prop types that exist (`SceneBuilder.BuildProp`: `house`, `stamp`, `fire_ring`, `paving`, `block`, `lamp`,
`tree`); two new stamps use existing blocks. Positions are offsets from the village's centre; the milestone may shift them by a block or
two to satisfy the builder's flat-ground check.

| Place | Name, label | Where | What it is | Why there |
| --- | --- | --- | --- | --- |
| The bakery | `bakery`, `panadería` | house 6 x 5 at (-13, -16), door south | a timber house; an `oven` stamp (cobble, a torch for the fire, a chimney) against its east wall; a crate with `loaves` by the door; a lamp; a paved strip to the square | the north-west quarter is empty and next to the stall, whose bread she bakes |
| The smithy | `smithy`, `herrería` | house 5 x 5 at (12, -16), door west | a `fire_ring` named `forge` outside the door (the hearth), a `stone` block (the anvil), crates, a lamp; a paved strip | the north-east quarter, with the tower between it and the bakery as the directions landmark ("al lado de la torre") |
| The well | `well`, `pozo` | 3 x 3 stamp at (3, 10) | cobble ring, water in the middle, a fence roof | the empty south, behind the fences' gate; a place to name and point at |
| The farmer's stand | (the field, `campo`) | the farmer at (8, 0) facing the wheat field | a crate with `apples` at (8, -3); three `tree` props at x 17 as an orchard | the field already exists between houses 1 and 2; the farmer gives it a voice |
| The fisher's bank | the NPC only | `bridge.east` (238, 205), dx 2, dz 3, facing the river | two crates and a lamp | the landing is on the road 15 s from the square; a new voice outside the village (R15) with no new site |
| Houses 1 to 3 | unchanged | | in dialogue they become "la casa de Luz", "de Domingo", "del aldeano" | `casa` gains a meaning without a block |

The footprint after (same key; `B` the baker, `D` the smith, `F` the farmer):

```
 -16          ++++HHHHHH+++++++++TTTTT++++iHHHHH+++       bakery, tower, smithy
 -15         +++++HHHHHHTT+++++++TTTTT+++++HHHHH++++      the oven (TT)
 -14        ++++++HHHHHHTT+++++++TTTTT+ooo+HHHHH+++++     the forge (ooo)
 -13       +++++++HHHHHH+++++++++TTTTT+ooo D HHHHH++++
 -12       +++++++HHHHHH+++++++++TTTTT+ooo..HHHHH++++
 -11      +++++++++i..+c++++++++++++++++c..++++++++++
 -10      ++++++++++.B++++++++++++++++++++..++++++++++
  -9     +++++++++++..+++SS+++++++++++++..HHHHH+++++++
  -6    +++++++++++++++..SS....i.......+HHHHH++++++++++
  -3    +++++++++++++++..c.............c+++++++++++++++  the apple crate
   0   ++++++++++++++++................Ffffffff+++++++++  the farmer
  +3 =  +++++++++P+++++.........V......+fffffff++++++++
  +7    =++====+++HHHHH-------++-------+HHHHH+++++++++
 +10      ++++++++HHHHH+++++++++++TTT++++++++++++++++    the well
```

Not in this plan: new sign boards (the arms are atlas art, `BlockCell.SignTienda*`; the beacon does the pointing), an interior for the
houses, animals, and `montaña` as a place (no point exists; the signpost's third arm stays a word the fisher can explain).

### 3.3 The people

| NPC id | Name (a decision, section 6) | Label | Place | Voice (trial, V06) | Figure | Role in the lessons |
| --- | --- | --- | --- | --- | --- | --- |
| `fisher` | Mar (m: `el mar`) | `Pescador` | the bridge's east bank | `em_santa` at 0.9, or `im_nicola` | blue coat, boots, a hat | directions to every place; pays for fruit |
| `baker` | Luz (f: `la luz`) | `Panadera` | the bakery | `if_sara` (the one new female voice) | apron, cap, flour-white sleeves | numbers, plurals, prices; buys wood, sells bread |
| `smith` | Domingo (m) | `Herrero` | the smithy | `im_nicola` or `em_alex` at 0.9 | dark apron, rolled sleeves, no hat | who is who, jobs, `hacer`; makes the axe |
| `farmer` | a masculine-noun name (Ángel, León: decision) | `Granjero` | the field | `pm_santa` or `em_santa` at 1.1 | straw hat (the villager's, another colour), green shirt | adjectives, `gustar`; sells apples, needs water |
| `villager` | unchanged (`Aldeano`) | | the square | `em_santa` | unchanged | the one who introduces people (as in `personas`) |
| `shopkeeper` | unchanged (`Tendero`) | | the stall | `em_alex` | unchanged | prices at the stall; "el pan es de Luz" |
| `guide` | Luna | | her camp | `ef_dora` | unchanged | the review hub; the deck asks the new rows too |

Four new NPCs is the most the voices allow (section 2.6); the farmer is the one to drop if the owner wants three.

### 3.4 Five items and four orders

The economy uses three items that exist (`wood`, `berry`, `water-bottle`) and three that V03 adds: `coin` (`moneda`), `apple` (`manzana`,
whose crate art exists as the `apples` block) and `axe` (`hacha`, which gathers a trunk in a quarter of the ticks while held). Items stay
code (C09): three enum values after `Book = 29`, three icons, three held sprites.

| Order (an objective) | At | The player brings | The NPC gives | Taught by | Why it links |
| --- | --- | --- | --- | --- | --- |
| `fruit-for-mar` | the fisher | 3 `fruta` (the bushes at the guide's camp) | 2 `monedas` | session 4 | the camp's fruit skill, paid in the new currency |
| `oven-wood` | the baker | 5 `madera` (the forest's edge) | 4 `monedas` | session 5 | the camp's wood skill; the coins buy her bread (2 each) |
| `axe-handle` | the smith | 2 `madera` and 2 `monedas` (3 until V12: a learner who followed sessions 4 and 5 has 2 left) | the `hacha` | session 6 | wood is faster afterwards: the loop pays back |
| `water-for-plants` | the farmer | 1 `agua` (the river, the bottle; V13: the order names its lesson, so the bottle the learner has carried since session 1 is taken only at the lesson's delivery step, after the plan) | 3 `manzanas` | session 7 | the river skill of session 1 |

Prices: bread 2 at the baker and the stall, an apple 1 at the farmer. An order is an objective with `session` (shown and counting from
that play session) and `deliverTo` (section 3.7): when the panel opens on that NPC and the inventory holds the goods, the host takes them,
raises the NPC's `<npc>.delivered` as a host turn (so the NPC speaks its own thank-you and reward line, and the reward is that line's
`give_item`), logs `order_delivered`, and the objective completes. Asking in Spanish is the lesson's content; walking up with the goods is
enough (principle 6). A purchase is the stall's exchange with a price: `request_item` with `qty` at a prompt whose host event's action is
`sell` takes `qty x price` coins and gives `qty` items; too few coins raise `<npc>.cant_pay`, a host turn the agent answers ("Cuesta cuatro
monedas. No tienes monedas."). Nothing is lost on a miss: the shopkeeper's bottle rule (`VoxelSession.Give`) is the model.

Phase B (session 10) adds the one repeatable source, `buy` (the NPC pays for what the player sells), and is the only place a new intent must
be checked against `state_want` for confusion (section 5).

### 3.5 The lesson arc

Sessions 4 to 6 finish A1; 7 to 10 are A2. One lesson a session, as S26 set it (a lesson needs a load; two lessons may share a session later
by `after` alone). Each is 5 to 10 minutes with two `review` steps, one `do` step at least, one order at most, **its verbs in the three
times** (the ladder below), and the next NPC named at the end. The phrase ids below are proposals; the content milestone writes the rows
(the sheet's columns, D-game-10), the Opus pass reads them, and the owner listens.

| Session | Lesson | NPCs | Grammar | The verbs in three times | Words | Mechanics it needs | Level |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 4 | `el-pueblo` "El pueblo" | the fisher, the villager | `¿Dónde está…?` for any place; `está cerca / lejos`; `a la izquierda / derecha`; `hay` | `voy a la panadería` (now, on the way), `voy a comprar pan` (the plan), `fui a la panadería` (the report, back at the bridge); `está`, `hay` | the places: tienda, panadería, herrería, pozo, torre, puente, río, campo, casa, plaza | the beacon from the `place` slot and by place; `beacon_reached` (V02) | A1 |
| 5 | `numeros-y-pan` "Números y pan" | the baker, the shopkeeper | 1 to 10; plural nouns; `¿Cuántos…?`; `¿Cuánto cuesta?`; **the regular `-ar` preterite** (-é, -aste) | `voy a buscar madera` (the plan), `tengo madera` (now), `compré dos panes` (the report); `cuesta / cuestan` | pan, moneda, madera, horno | `qty` gives N; coins; the oven order; `sell` (V03) | A1 |
| 6 | `gente-del-pueblo` "Gente del pueblo" | the smith, the villager, the farmer | `¿Quién es?`, `es el / la …`; `¿Cómo te llamas?`; `¿Qué haces?`, `trabajo en …`, `vivo en …`; **the third person in three times** | `Domingo trabaja en la herrería`, `vivo en el campamento` (now), `va a hacer un hacha` (before the order), `hizo un hacha` (after it) | the jobs and the names; casa, herrería, campamento | the axe order (V03) | A1 |
| 7 | `como-es` "¿Cómo es?" | the farmer, the shopkeeper | adjectives after the noun, agreement; `me gusta / gustan`; **`gustar` in the past** | `me gusta el pan` (now), `me gustó el pan de Luz` (the report); `voy a buscar agua` then `fui al río` (the water order, reviewed) | the colours, grande, pequeño, nuevo, viejo, caliente, frío; manzana | the water order; apples for sale; `[ADJ]` in `request_item` | A1 to A2 |
| 8 | `ayer-en-el-pueblo` "Ayer en el pueblo" | the villager, the smith, the fisher | **the preterite for every person**: él/ella (`fue`, `hizo`, `compró`, `vendió`, `pescó`), nosotros (`fuimos`, `compramos`), ellos (`fueron`, `comieron`); `tener que` + infinitive | the day of the village, told by the villager and retold by the learner; `tengo que ir` (now) | the week's story of the village | none | A2 |
| 9 | `manana-en-el-pueblo` "Mañana en el pueblo" (new) | the baker, the smith, the fisher | **the simple future** (-é, -ás, -á, -emos, -án; `iré`; `hará`, `tendré` heard), against `ir a`; the later markers (`la semana que viene`, `el próximo día`) | `mañana compraré pan`, `iré al río`, `buscaré madera para Luz`; `Luz hará pan` (the day of tomorrow, the mirror of session 8) | semana, próximo, promesa (heard) | `tense: fut` on a step and the book's fourth column (V16) | A2 |
| 10 | `el-mercado` "El mercado" | the baker, the farmer, the shopkeeper | 11 to 20; `¿Cuánto es todo?`; `más … que`; `¿Puedo pagar con …?`; selling; **the three times reviewed** | `compré tres manzanas` (past), `quiero vender madera` (now), `mañana venderé fruta` (future; `voy a vender` credited) | barato, caro, todo, poder, vender | `buy` (V03b) | A2 |

**The ladder, form by form.** What each session adds (a form is taught once and reviewed after; the heard-only forms in brackets):

| Session | The present | The preterite | The future |
| --- | --- | --- | --- |
| 1, 2 (built) | querer, tener, necesitar in every person; ser, estar, ir | | |
| 3 (built) | voy, vas, va, van | comí, comiste; bebí, bebiste; fui, fuiste | voy a comer, vamos a comer, voy a ir |
| 4 | está, hay; voy a + a place (movement, not a plan) | fui a + a new place (reviewed) | voy a comprar (`comprar` met as an infinitive) |
| 5 | cuesta, cuestan; quiero dos panes; tengo | compré, compraste: the `-ar` pattern | voy a buscar |
| 6 | trabajo, trabaja; vivo, vive; hace; es | hizo [trabajó] | va a hacer: the third person |
| 7 | me gusta, me gustan; es + adjective | me gustó, me gustaron | voy a buscar agua (reviewed) |
| 8 | tengo que + infinitive | fue, compró, vendió, pescó; fuimos, compramos; fueron, comieron | va a + infinitive (reviewed) |
| 9 | (reviewed) | (reviewed, against the future) | compraré, comprarás, comprará; iré; buscaré; [hará, tendré, trabajará, pescará] |
| 10 | vendo, puedo; cuesta | compré tres manzanas (the report of the market) | venderé; voy a vender |

The imperfect (`comía`, `había`) and the perfect (`he comido`) are not in the series: one past is enough to A2 for a Latin American neutral
learner, the core does not grade the preterite against the imperfect, and the brief says not to overwhelm (decision (i), section 6).

**Session 4, "El pueblo" (V10).** The objective line sends the player down the road; the fisher is on the way, at the bridge. He greets
(`hola`, a review step: the transfer of a known phrase to a new voice, R15), asks `¿Qué buscas?` (the villager's line, now his too) and
answers any place with a point and a fixed line. The three times are the walk itself: before the player goes he asks where they are going
and what they are going to do there; when the lesson brings them back to the bridge he asks whether they went.

| Learner says | Spanish | Intent, prompt | Error example (codes) |
| --- | --- | --- | --- |
| `donde-esta-la-panaderia` | ¿Dónde está la panadería? | `ask_directions`, `fisher-open` | dónde está el panadería (gender.agree) |
| `donde-esta-la-herreria` | ¿Dónde está la herrería? | `ask_directions`, `fisher-open` | dónde están la herrería (conj.person) |
| `esta-lejos` | ¿Está lejos? | `ask_far` (new), `lesson-far` | es lejos (ser_estar) |
| `hay-un-pozo` | ¿Hay un pozo en el pueblo? | `ask_availability` (new), `lesson-hay` | hay una pozo (gender.agree) |
| `voy-a-la-panaderia` | Voy a la panadería. | `state_action` (the grammar exists), `lesson-going`; the step expects `pres`, `1sg` | yo vas a la panadería (conj.person) |
| `voy-a-comprar-pan` | Voy a comprar pan. | `state_action` (`comprar` joins it), `lesson-plan-there`; the step expects `ir-a` | voy a compro pan (conj.tense) |
| `fui-a-la-panaderia` | Sí, fui a la panadería. | `state_action`, `lesson-went-there`, on the return; the step expects `pret` | ayer voy a la panadería (conj.tense) |
| `gracias-adios` (review) | Gracias. Adiós. | `thank`, `farewell` | |

| Learner hears | Spanish | Line |
| --- | --- | --- |
| `que-buscas` | ¿Qué buscas? | `fisher_greet.1` |
| `esta-a-la-izquierda` | La panadería está a la izquierda de la torre. | `guide.lesson.fisher.left.1` (a `listen` step: left or right of the tower) |
| `esta-cerca` | La tienda está cerca. El lago está lejos. | `guide.lesson.fisher.near.1` |
| `hay-dos-casas` | En el pueblo hay una tienda, una panadería y tres casas. | `guide.lesson.fisher.hay.1` |
| `adonde-vas` | ¿Adónde vas? | `guide.lesson.fisher.going.1` |
| `que-vas-a-hacer-alli` | ¿Qué vas a hacer allí? | `guide.lesson.fisher.plan.1` |
| `el-letrero` | El letrero dice: tienda, río, montaña. | `guide.lesson.villager.sign.1` (at the signpost) |
| `fuiste-a-la-panaderia` | ¿Fuiste a la panadería? | `guide.lesson.fisher.went.1` (on the return) |

`do` steps: walk to the bakery (`pointPlace: bakery`, waits for `beacon_reached bakery`), then to the smithy; the villager's step reads the
signpost; the last `do` step walks back to the bridge (`talk_opened fisher`, 15 s), where the preterite names the walk just made and the
fisher's last line shows the order `fruit-for-mar` ("Tengo hambre. ¿Tienes fruta?"), which completes whenever the player comes back with
three. New in the bundle: the `fisher` agent and pools (`fisher_hello`, `fisher_greet`, `fisher_point`, `fisher_far`, `fisher_near`,
`fisher_unknown_place`, `fisher_help`, `fisher_thank`, `fisher_bye`, `fisher_delivered`), the answer set `places-village`, the prompts
`fisher-open` and `lesson-far`, `lesson-hay`, `lesson-place`, `lesson-going`, `lesson-plan-there`, `lesson-went-there`, about 8 check items,
the `review` steps X06 left for `personas` and `ayer-y-manana`. Eight rows the learner says is the ceiling of a lesson; the milestone
measures and drops `donde-esta-la-herreria` first if it runs past ten minutes.

**Session 5, "Números y pan" (V11).** At the bakery. Luz counts her loaves (a `listen` step with the numbers), asks how many the player
wants, names the price, and has no wood for the oven. The three times are the oven order (principle 7): `¿Qué vas a hacer?` before the
wood (`voy a buscar madera`), `tengo madera` on the return, and after the purchase `¿Qué compraste?` (`compré dos panes`): the regular
`-ar` preterite (-é, -aste), which session 3's `-er`/`-ir` forms did not give; Luz models it first ("Ayer compré fruta en la tienda.").

| Learner says | Spanish | Intent, prompt | Error example |
| --- | --- | --- | --- |
| `quiero-dos-panes` | Quiero dos panes, por favor. | `request_item` (qty), `baker-open` | quiero dos pan (noun.number) |
| `cuanto-cuesta` | ¿Cuánto cuesta? | `ask_price` (new), `baker-open` | cuánto costa (conj.irregular) |
| `voy-a-buscar-madera` | Voy a buscar madera. | `state_action` (`buscar` joins it), `lesson-wood-plan`; the step expects `ir-a` | voy a busco madera (conj.tense) |
| `tengo-madera` | Tengo madera. | `state_have`, `lesson-wood` | yo tiene madera (conj.person) |
| `compre-dos-panes` | Compré dos panes. | `state_action` (`comprar`), `lesson-bought`, after the purchase; the step expects `pret`, `1sg` | ayer compro dos panes (conj.tense) |
| `hay-pan` | ¿Hay pan? | `ask_availability`, `stall-need` (the stall too) | hay pans (vocab.unknown) |
| `no` (review) | No. | `deny` | |

| Learner hears | Spanish |
| --- | --- |
| `uno-a-cinco` | Uno, dos, tres, cuatro, cinco. |
| `seis-a-diez` | Seis, siete, ocho, nueve, diez. |
| `cuantos-panes` | ¿Cuántos panes quieres? |
| `tengo-diez-panes` | Tengo diez panes. |
| `cuesta-dos-monedas` | El pan cuesta dos monedas. |
| `necesito-madera-para-el-horno` | Necesito madera para el horno. Cinco bloques. ¿Qué vas a hacer? |
| `toma-cuatro-monedas` | ¡Gracias! Toma cuatro monedas. |
| `ayer-compre-fruta` | Ayer compré fruta en la tienda. (the model of -é) |
| `que-compraste` | ¿Qué compraste? |
| `compraste-dos-panes` | Compraste dos panes. ¡Muy bien! (the tú form, a `listen` step) |

The order `oven-wood` (5 wood, 4 coins) then the purchase (`quiero-dos-panes`, 4 coins, 2 loaves), then the report. The stall learns the
price too (`stall.price` host event, "Cuesta dos monedas."). Grammar the core already grades: `quiero dos pan`, `cuánto costa`, `cuestan`
for one loaf, `ayer compro` (expected `compré`), `voy a busco`.

**Session 6, "Gente del pueblo" (V12).** The villager introduces the smith ("Él es Domingo. Es el herrero.", third person reviewed), the
learner asks names and jobs, the smith makes the axe. The three times are about someone else for the first time: `Domingo trabaja en la
herrería` (now), `va a hacer un hacha` (before the order, to the villager's `¿Qué va a hacer Domingo?`), `hizo un hacha` (after it, `¿Qué
hizo Domingo?`); the learner's own present is `vivo en el campamento`.

| Learner says | Spanish | Intent, prompt | Error example |
| --- | --- | --- | --- |
| `como-te-llamas` | ¿Cómo te llamas? | `ask_name` (new) | cómo te llama (conj.person) |
| `que-haces` | ¿Qué haces? | `ask_activity` (new) | qué hace (tú asked as usted: the lesson's person check, not a core error) |
| `es-el-herrero` | Es el herrero. | `name_object` | es la herrero (gender.agree) |
| `trabaja-en-la-herreria` | Domingo trabaja en la herrería. | `state_action` (`trabajar en`, a `[WORD:name]?` subject), `lesson-works`; `pres`, `3sg` | Domingo trabajo en la herrería (conj.person) |
| `vivo-en-el-campamento` | Vivo en el campamento. | `state_action` (`vivir en`), `lesson-lives`; `pres`, `1sg` | vivo en la campamento (gender.agree) |
| `va-a-hacer-un-hacha` | Va a hacer un hacha. | `state_action` (`hacer`), `lesson-will-make`; the step expects `ir-a`, `3sg` | Domingo voy a hacer un hacha (conj.person) |
| `hizo-un-hacha` | Domingo hizo un hacha. | `state_action`, `lesson-made`, after the order; the step expects `pret`, `3sg` | ayer Domingo hace un hacha (conj.tense) |
| `necesito-pan` (review, at the baker) | Necesito pan. | `state_want` | |

Heard: `me-llamo-domingo` "Me llamo Domingo.", `soy-el-herrero` "Soy el herrero. Trabajo con fuego y madera.", `donde-vives` "¿Dónde vives?",
`vive-en-la-casa` "Luz vive en la casa al lado de la tienda.", `que-va-a-hacer-domingo` "¿Qué va a hacer Domingo?", `quieres-un-hacha`
"¿Quieres un hacha? Necesito dos bloques de madera y tres monedas.", `toma-el-hacha` "Toma el hacha.", `que-hizo-domingo` "¿Qué hizo Domingo?",
`ayer-trabajo-mucho` "Ayer Domingo trabajó mucho." (the regular `-ó` heard, a `listen` step). The learner does not say their own name (M29d: a
name is not in the recognizer's vocabulary; the villager's `me-llamo` row says so). `donde-vive` ("¿Dónde vive Luz?", `ask_residence`) is the
row to add if the lesson has room, and `es-el-herrero` the first to drop if it has not.

**Sessions 7 to 10 (phase B: V13, V14, V17, V15)** are designed to the same shape and detailed in their briefs; each starts only after the
owner has heard phase A.

- **Session 7, "¿Cómo es?" (V13):** `la-manzana-es-roja` (`describe`, gender.agree), `quiero-tres-manzanas-rojas` (`request_item` with
  `[ADJ]`, word_order), `me-gusta-el-pan` and `me-gustan-las-manzanas` (`state_like`), `me-gusto-el-pan` "Me gustó el pan de Luz." (the past
  of `gustar`, to the farmer's `¿Te gustó el pan?`; `ayer me gusta el pan` is `conj.tense`), `es-grande` / `es-pequeno`; the water order's
  plan and report reuse `voy a buscar agua` and `fui al río`.
- **Session 8, "Ayer en el pueblo" (V14):** the villager tells the day of the village in three lines to listen to ("Ayer Luz hizo pan.
  Domingo trabajó en la herrería. Mar pescó en el río.") and the learner retells it: `luz-hizo-pan`, `mar-pesco-en-el-rio`,
  `ellos-comieron-pan`; with the fisher, `fuimos-al-rio` "Fuimos al río." (nosotros, to `¿Adónde fueron?`); `tengo-que-ir`
  (`state_obligation`, the present). The persons the preterite still lacked (él/ella, nosotros, ellos) in one lesson, on verbs met before.
- **Session 9, "Mañana en el pueblo" (V17, after V16, new):** the simple future as the second way to say tomorrow, against `ir a`; an
  `explain` step says when each is used (a plan made now: `voy a`; a promise or a prediction: `-é`). Heard: `que-haras-manana` "¿Qué harás
  mañana?", the day of tomorrow ("Mañana Luz hará pan. Domingo trabajará. Mar pescará."), `tendre-pan` "Mañana tendré pan." Said:
  `manana-comprare-pan` "Mañana compraré pan." (`state_action`; the step expects `fut`; `mañana voy a comprar pan` is credited with a note,
  as the present is at an `ir-a` step), `ire-al-rio` "Iré al río.", `buscare-madera` "Buscaré madera para Luz.", `luz-hara-pan` "Luz hará
  pan." (3sg, heard first). Error examples the core grades: `mañana compré pan` (conj.tense), `ayer compraré pan` (conj.tense, expected
  `compré`), `yo comprarás pan` (conj.person).
- **Session 10, "El mercado" (V15):** `cuanto-es-todo`, `es-muy-caro`, `vendo-madera`, `puedo-pagar-con-fruta`, the numbers to twenty and
  `más barato que`; and the market told in three times as the review of the series: `compre-tres-manzanas` (past), `quiero-vender-madera`
  (now), `manana-vendere-fruta` (future; `voy a vender` credited).

### 3.6 New intents, prompts and answer sets

All in the bundle (game-owned); the core's DSL has every token they use.

| Intent | Slot grammars (first pass) | Keyword rule | Answers at | Confusion to check (lint) |
| --- | --- | --- | --- | --- |
| `ask_price` | `cuánto [VERB:costar] [ART]? [NOUN:item]?`, `cuánto [VERB:ser] [ART]? [NOUN:item]?`, `qué precio [VERB:tener] [ART]? [NOUN:item]` | `costar` | the baker, the stall, the farmer | none |
| `ask_far` | `[VERB:estar] (lejos\|cerca)`, `[VERB:estar] muy (lejos\|cerca)`, `[VERB:quedar] (lejos\|cerca)` | `lejos`, `cerca` | the fisher, the villager | `ask_directions` (both have `estar`: the keyword differs) |
| `ask_availability` | `hay [ART]? [NUM:qty]? [NOUN:item]`, `hay [ART]? [NOUN:place] (en el pueblo)?` | `haber` with a slot | the stall, the baker, the fisher | `ask_find` (`dónde hay`: the `dónde` keyword wins there) |
| `ask_name` | `cómo te [VERB:llamar]`, `cómo se [VERB:llamar] (usted\|él\|ella)?` | `llamar` | every villager | none |
| `ask_activity` | `qué [VERB:hacer] (tú\|usted)?`, `en qué [VERB:trabajar]`, `dónde [VERB:trabajar]` | `trabajar` | every villager | `ask_help` has `qué [VERB:hacer]` ("what do I do?"): resolve by the prompt's allowed intents, as today |
| `ask_residence` | `dónde [VERB:vivir] [WORD:name]?`, `dónde [VERB:vivir] (usted\|él\|ella)` | `vivir` | the villager | none |
| `describe` (B) | `[ART]? [NOUN:object]? [VERB:ser] [ADJ]`, `(esto\|eso) [VERB:ser] [ADJ]` | none (it must parse) | the farmer | `name_object` (noun against adjective) |
| `state_like` (B) | `me [VERB:gustar] [ART] [NOUN:item]`, `(sí\|no)? me [VERB:gustar]` | `gustar` | the farmer, the stall | the core's grading of `gustan` (section 5) |
| `state_obligation` (B) | `[VERB:tener] que [VERB:ir] a [ART] [NOUN:place]`, `[VERB:tener] que [VERB:comer] …` | `tener` + `que` | the villager | `state_have` (the keyword rule `tener` + item: `que` must win) |
| `offer_item` (B) | `[VERB:vender] [NOUN:item]`, `[VERB:tener] [NOUN:item] para (ti\|usted)` | `vender` | the baker, the smith | `state_want` and `state_have`: the hardest one; design it last |

Changes to existing intents. **`state_action` grows in phase A**: it is the statement in any tense that hands nothing over (S27), and the
tense rows of 3.5 are all of it. It gains grammars for `comprar`, `buscar`, `hacer`, `vender`, `pagar` and `pescar` (`[VERB:x] [ART]? [NUM:qty]?
[NOUN:item]`), for `trabajar` and `vivir` (`en [ART] [NOUN:place]`), each as a finite verb and after `[VERB:ir] a`; a `[WORD:name]?` or
`[PRON]?` subject (`Domingo hizo un hacha`, `ella va a hacer pan`); and the time words at either end grow from `(mañana|ayer|hoy)?` to the
markers the core knows (`luego`, `después`, `la semana que viene`, `el próximo …`), so that `mañana` is not the only cue. The DSL has no
tense: `[VERB:comprar]` matches `compro`, `compré` and `compraré` alike, and the lesson step's `tense` is the judge (section 2.9). `ask_activity`
(`qué [VERB:hacer]`) already covers the questions the lessons ask in three times (`¿Qué haces?`, `¿Qué hiciste?`, `¿Qué vas a hacer?`, `¿Qué
harás?`). `state_like` (B) takes any tense of `gustar` (`me gustó`). `request_item` gains `[ADJ]?` after the noun (phase B; R02 noted the
gap). Prompts: one open prompt per new NPC (`fisher-open`, `baker-open`, `smith-open`, `farmer-open`) in the
stall's constrained style, plus about five `lesson-*` prompts a lesson with their `lesson-*` answer sets, as S26 did. Answer sets:
`places-village`, `supplies-bakery`, and the lesson sets. Every `hears` line is a `guide.lesson.<npc>.*` pool (so the lint counts it as the
game's, S26) with the NPC as its speaker (V01's rule).

### 3.7 What changes in the data formats

| File | Addition | Read by |
| --- | --- | --- |
| `game/npcs/<id>.json` (schema v2) | `prompts` by state (`{"idle": "baker-open", "offering": "baker-offer"}`; the review prompt for the guide); `opening` (`{"event": "fisher.greet", "when": "always" \| "first" \| "never"}`); `voice` (`{"speaker": 29, "name": "em_alex", "speed": 1.0}`); `keyWords` (moved from `words.json`) | `NpcService`, `SpeechSpeakers`, the renderer, `GuideSession` |
| `game/host-events.json` | `give_item` takes `count` (a number, or `"qty"` for the verdict's slot, capped at 5); new kinds `point_at_place` (a scene name), `point_at_slot` (`"slot": "place"`, resolved through the scene's labels; no match raises `<npc>.unknown_place`), `take_item` (`item`, `count`), `sell` (`item`, `price`), `buy` (B: `item`, `price`) | `HostEventTable`, `VoxelSession.ApplyHostEvents` |
| `game/objectives/<id>.json` | `session` (shown and counting from that play session); `deliverTo` (an NPC) with `deliver` (items and counts); the completion raises `<npc>.delivered` and logs `order_delivered` | `ObjectiveBook`, `VoxelSession` |
| `game/lessons/<id>.json` | `level` (`a1` \| `a2`); a `do` step's `pointPlace` (a scene name) beside `point` (a noun); a step's `tense` may be `fut` (V16, phase B), with a `futureNote` beside `presentNote` for `ir a` said there | `LessonBook`, `LessonRunner`, the validator |
| `game/checks.json` | `lesson` on every item (the lesson it belongs to) | `CheckBank`, `CheckBattery` |
| `game/scene.json` | the village's radius; the props of 3.2; the NPC placements; the stamps `oven` and `well`; labels | the builder |
| `game/words.json` | `keyWords` leaves for the NPC files; the direction words stay; `nearFuture.timeWords` gains the other future markers (`luego`, `después`, `pasado mañana`), so a plan said in the present with any of them is credited as `mañana` is | `GameWords`, `LessonRunner.PresentForPlan` |
| `content/locale/en/*` | `npc.<id>.role` for four NPCs; the lessons' text (with `tenseRetry` and `presentNote` on every tense step, and `futureNote` at a `fut` step); `objective.<id>.title` for the orders; `line.<id>` for every new line; the checks' options; `ui.bookColumnFuture` (V16) | `LocaleBundle` |
| World events | `beacon_reached` (detail: the point), `order_delivered` (npc, detail: the objective), `place_asked` (detail: the place or `unknown`) | the tips, the lessons, the objectives, the session log |

Each new file keeps a provenance item; each schema change bumps `content/schema/*.schema.json` and the schema test.

### 3.8 What changes in code (the game only)

| Milestone | Project | Change | Size |
| --- | --- | --- | --- |
| V01 | Game.Core, SpeechRender, Game | `NpcRoster` reads the new fields; `NpcService.PromptAfter`, `OpeningEvent` and the warm-up list from the roster (the shopkeeper's fail-forward and the guide's review stay by id, documented); `SpeechSpeakers.OfPool` by the NPC ids (`<id>_*`, `guide.lesson.<id>.*`); the renderer's voice from the NPC file; `LaunchOptions` validates `--talk` after the roster loads; `PhraseSheet.cs:252` mapping from the prompt files | M |
| V02 | Game.Core | `HostEventTable` kinds and `count`; `VoxelSession.Give` with a count; `PointAtPlace` and `PointAtSlot` through `Scene.LabelOf` and `Beacon.SetPoint`; `BeaconDirector.ClearIfReached` logs `beacon_reached`; `LessonRunner` `pointPlace`; the validator checks places against the scene | M |
| V03 | Game.Core, Game | `ItemId.Coin = 30`, `Apple = 31`, `Axe = 32`; the axe's gather ticks (`VoxelSimulation`, by the held item); `take_item`, `sell`, (`buy`); objectives' `session` and `deliverTo` (`ObjectiveBook`, the delivery check in `GuideSession.BeginTalk`, the host turn); the HUD hides an objective before its session; save: no new version unless a field is needed (the inventory stores item names; objective steps by id) | M |
| V04 | content only | the scene (3.2); `SceneTests` for the new footprints; the seed-42 build-time budget (`SceneBuildTimeTests`, 3 s) stays | S/M |
| V05 | Game | four figures in `NpcFigures`, four face cells in `BlockAtlasLayout` and `BlockArtVillage`, icons and held sprites for the coin, the apple and the axe (`BlockCell.Icon*`, `HeldArm`) | S/M |
| V06 | SpeechRender, docs | the voice trial (one line, every candidate, every speed), then the voice from the NPC file; `SPEECH_CHECKLIST.md` names the speakers | S |
| V07 | Game.Core | `CheckBank` reads `lesson`; `CheckPlan.For` takes the items of the lessons unlocked by the session (`lesson.Session <= PlaySession`); the pilot's 31 unchanged | S |
| V08 | Game.Core, scripts | the lesson's `level`; `GameDataValidator` checks the lemmas of each lesson's phrases, variants, lines and prompts against the bundle's cumulative level lists, and its `verbs` against its level (no `fut` in an `a1` lesson); the gate's lint moves to `--level a2 --fail-on-level` with `--level a1` as a report | S |
| V16 | Game.Core, Game | the simple future: `tense: fut` on a step (`LessonBook.cs:597`, `lesson.schema.json`); at a `fut` step the near future is credited with a `futureNote`, as the present is at an `ir-a` step (`LessonRunner.WrongTense`, `PresentForPlan`); the book's fourth column (`ConjugationTable.AllColumns`, `ui.bookColumnFuture`), shown once a lesson teaches a `fut` form, as the others are; a test that `TaughtVerbForm.Text` finds `compraré`, `iré` and `hará` in the bundle's lexicon (they come from the Wiktionary import, section 2.9) | S |

No change to the core is expected. If `state_like`, `ask_price` with a plural subject, an adjective after `[NOUN:item]` or one of the tense
cases of section 5 grades wrongly, that is a core milestone (M56 and on), filed from the content milestone's report, never patched in the
game. One possible core S is known in advance and not needed: `MorphologyGenerator.ConjugateRegular` builds no simple future, so a verb
absent from the Wiktionary import would have no `fut` cell in the book; every verb of this plan is in the import.

### 3.9 The gate for A1 and A2 content

Today's gate demands A1 bundle-wide (section 2.5). The rule this plan proposes, for the owner (section 6):

1. **The bundle's ceiling is A2:** `lint --strict --level a2 --fail-on-level --host-pools 'guide.*'`. A word above A2 anywhere fails.
2. **A lesson's promise is per lesson:** every lesson declares `level`; the game's validator (`--validate-content`, in `content.sh refresh`
   and the gate) checks the Spanish of an `a1` lesson (its phrases and variants, the lines its steps name, its prompts' texts) against the
   A1 list, and an `a2` lesson against A1 plus A2. Sessions 1 to 6 are `a1`. The lists are lemma lists, so a form's tense is invisible to
   the lint; the same validator checks a lesson's `verbs` against its level: an `a1` lesson may declare `pres`, `pret` and `ir-a` (as
   session 3 does), an `a2` lesson `fut` as well.
3. **World words** (nouns the player handles or the beacon names: `madera`, `antorcha`, `botella`, `panadería`, `herrería`, `pozo`, `horno`,
   `hacha`, `moneda`, `izquierda`, `derecha`, the labelled places) join `cefr/a1.txt` under a marked block with a one-line reason each, as
   S5 and S17 did; a name that is not a word is listed the same way. `cefr-check --level a1` keeps printing what is above A1 for the owner.
4. The grammar gate, the sheet test and the Opus pass are unchanged.

## 4. Milestones

| ID | What | Depends on | Size | Model | Status |
| --- | --- | --- | --- | --- | --- |
| V00 | This plan; D-game-22 proposed; the row in `IMPLEMENTATION_PLAN.md` 4.8; revised the same day for the three times (section 2.9, the ladder in 3.5, V16, V17) | | | orchestrator | written 2026-10-30; answered the same day (D-game-22: phase A now) |
| V01 | NPC behaviour from data: prompts by state, the opening event, the voice and the key words in `game/npcs/<id>.json`; the speaker of a pool by NPC id; `--talk` from the roster | | M | Opus 5.5 xhigh | done 2026-10-30 (`0ad929d`) |
| V02 | Host actions and the beacon by place: `give_item` with a count, `point_at_place`, `point_at_slot`, `beacon_reached`, a `do` step's `pointPlace` | V01 | M | Opus 5.5 xhigh | done 2026-10-30 (`d53b957`) |
| V03 | Items and orders: the coin, the apple and the axe; `take_item` and `sell`; objectives with `session` and `deliverTo`; `order_delivered` | V02 | M | Opus 5.5 xhigh | done 2026-10-30 (`d53b957`) |
| V04 | The village grows: the scene of 3.2 (radius 24, the bakery and oven, the smithy and forge, the well, the farmer's crate and orchard, the fisher's bank, the placements, the labels) | | S/M | Opus 5.5 xhigh | done 2026-10-30 (`600b8d7`) |
| V05 | Four figures and faces; the coin, apple and axe icons and held sprites | | S/M | Opus 5.5 xhigh | done 2026-10-30 (`600b8d7`) |
| V06 | The voice trial (the owner listens) and the voice from the NPC file | V01 | S | Opus 5.5 xhigh | done 2026-10-30 (`600b8d7`) |
| V07 | The check battery by lesson: `lesson` on the items; a battery takes the unlocked lessons' items | | S | Opus 5.5 xhigh | done 2026-10-30 (`600b8d7`) |
| V08 | The A1/A2 gate of 3.9: `level` on the lessons, the validator's check, the gate's lint at A2 | | S | Opus 5.5 xhigh | done 2026-10-31 (`808e0f0`) |
| V09 | Documents and runs: the counts in `README.md` and `PLAYTEST.md`, `SCRIPTED_RUNS.md` with two new runs (the bakery purchase, asking the way), `SPEECH_CHECKLIST.md`; once per phase | the phase's lessons | S | Opus 5.5 xhigh | phase A done 2026-10-31 (`9ec051d`); phase B done 2026-11-01 (`da8dba6`) |
| V10 | Session 4 "El pueblo": the fisher; places and directions; `ask_far`, `ask_availability`; the three times around the walk (`voy a la panadería`, `voy a comprar pan`, `fui a la panadería`; `state_action` gains `comprar` and the later markers); the order `fruit-for-mar`; the X06 review steps in `personas` and `ayer-y-manana` | V01, V02, V04, V05, V06, V07 | M | Opus 5.5 xhigh | done 2026-10-30 (`3f1ce84`) |
| V11 | Session 5 "Números y pan": the baker; numbers, plurals, `ask_price`; the plan and the report of the oven order (`voy a buscar madera`, `compré dos panes`: the regular `-ar` preterite); the oven order; the purchase; the stall's price | V03, V10 | M | Opus 5.5 xhigh | done 2026-10-31 (`6097249`) |
| V12 | Session 6 "Gente del pueblo": the smith and the farmer; `ask_name`, `ask_activity`, (`ask_residence`); the third person in three times (`trabaja`, `va a hacer`, `hizo`; a name subject in `state_action`), `vivo en`; the axe order | V11 | M | Opus 5.5 xhigh | done 2026-10-31 (`c325b67`) |
| V12b | Follow-up found by V09's runs: the four village NPCs' `focus` (their open prompts' rows), a turn credits its prompt's own row before the stall's fixed mapping (free talk with the baker had credited `pedir-agua`), and a validator rule that a prompt that is the home of practised rows names one in its NPC's focus | V09 | S | Opus 5.5 xhigh | done 2026-10-31 (`29188ce`) |
| V12c | Follow-up found by the owner's playtest (2026-10-31: after the fire, "I don't know where to start"): the next session in the game, by sleeping at the player's own fire (the closing card's Enter, or E at the fire once the session's work is done), a sleep being exactly a load (`SaveGame.Sleep`, proved save for save); the objective's next step; the book's Lessons page says when and where each lesson opens; `--skip-to-session n`, a test aid that starts at session n with the earlier sessions counted as done; runs `r20-sleep` and `r21-skip` | V12b | M | Opus 5.5 xhigh | done 2026-10-31 (`4b88599`) |
| V13 | Session 7 "¿Cómo es?": adjectives and `gustar` in the present and the past; `describe`, `state_like`, `[ADJ]` in `request_item`; the water order (its plan and report reviewed); apples | V08, V12 | M | Opus 5.5 xhigh | done 2026-10-31 (`f5933c8`) |
| V14 | Session 8 "Ayer en el pueblo": the preterite for every person (él/ella, nosotros, ellos; the day of the village told and retold), `tener que`; `state_obligation` | V13 | M | Opus 5.5 xhigh | done 2026-10-31 (`78a37fa`) |
| V16 | The simple future in the runner and the book: `tense: fut` on a step, `ir a` credited there with a note, the fourth column, the schema (3.8) | V08 | S | Opus 5.5 xhigh | done 2026-10-31 (`71573ba`) |
| V17 | Session 9 "Mañana en el pueblo": the simple future (-é, -ás, -á, -emos, -án; `iré`; `hará`, `tendré` heard) against `ir a`; the later markers; the day of tomorrow | V14, V16 | M | Opus 5.5 xhigh | done 2026-10-31 (`cc155d4`) |
| V15 | Session 10 "El mercado": 11 to 20, `¿Cuánto es todo?`, `más … que`, `poder`; `offer_item` and `buy`; the market told in three times | V17 | M/L | Opus 5.5 xhigh | done 2026-11-01 (`d082f05`; V15b `557db47`) |
| V18 | The content loop in one command (owner, 2026-10-31, from `docs/DEVELOPMENT_BOTTLENECKS.md` P2): after an edit of the bundle, the refresh, the LanguageTool cache and its rebuilt-from-nothing check, the prerender, the save fixtures' hash, the goldens on request, the fast tier and the grammar-sensitive fuzz classes, one summary | V13 | S/M | Opus 5.5 xhigh | done 2026-10-31 (`d986adf`): `scripts/content-loop.sh --grammar --speech` |
| V19 | The game findings of the review of `feature/village` (owner, 2026-11-01: "fix them all"; `docs/qa-reports/2026-11-review-feature-village.md` R4, R7 to R10, R1's answer set and two risks): the listener's name after any greeting, one place that reads a line, the market's misses (no item, no room, no cap, zero), the skip that fails loudly | M64 | M | Opus 5.5 xhigh | done 2026-11-01 (`716d469`) |
| V20 | Free talk (handover 9.3 D): each NPC's open prompt takes the intents of the lessons it teaches, so all 27 of phase B's practised lines are understood by the NPC who taught them (a game test runs every said row through its NPC's open prompt); practice credited for any row of a started or finished lesson, at any NPC; focus cards only once their lesson is current or done; an NPC's name capitalised in a recast (not after a determiner); `ask_total` takes `son`, and `cuanto son todo?` (no accent) is `ask_total`, not `describe` (QA-language-8); `quiero cero panes` keeps its credit (documented) | M66, M67 | M/L | Opus 5.5 xhigh | planned |
| V21 | Smaller game fixes (handover 9.3 E): the stall answers by item through `ExchangeMiss`, `shop_hay` agrees with what it sells; the shared feedback lines in each NPC's voice (the WAV count and disk cost reported before rendering); `tener que` in the book; the Lessons page opens at `next:`; one reply per turn, never a recast's praise and "No te entendí" together (QA-language-9); the exchange lines that contradict the deal (the farmer's «Solo compro fruta» to apples, the baker's «Necesitas más monedas» while the order is held, a fixed `¿Cuánto es todo?` answer; QA-progression-3 and 4); the display of M70's accent note | V20 | M | Sonnet 5.5 | planned |
| V22 | Name plates (handover 9.3 F; the owner's design, 2026-11-01): only the plate of the NPC the player faces within talking range (and the beacon's target), drawn depth-tested against the scene and the figures | | S | Sonnet 5.5 | done 2026-11-01 (`1ff8a5a`) |
| V23 | The deferred small items (handover 9.3 G): `KeyWordsTaught` a set of NPC ids with a save migration; the skip buys three apples; the host's `--bag`; the five old world words into `a1.txt`'s block; one line when the closing review has nothing due; the book counts a correct use wherever it was understood; `NearestOfNounJoinedTo` capped; from the progression QA pass: `n4-wood` done when the bag already holds the wood, the objective after a talk starts its lesson, a closing line after session 10, the farewell's prompt id in the log, the first frame | V20 | S/M | Sonnet 5.5 | planned |
| V25 | A load keeps its day (QA-progression-1 and 2; the owner's two counters, D-game-22, 2026-11-01): the village session (lessons, objectives, orders; advanced only by sleeping) restored exactly as saved; a separate sitting counter goes up on each launch or load, for the spacing rule of phrase credit and the session logs; an older save migrates (its session is the day) | V22 | S/M | Sonnet 5.5 | planned |
| V24 | Documents and runs after the follow-ups (handover 9.3 J), as V09 again: the README's counts and open points, `PLAYTEST.md`, `SCRIPTED_RUNS.md`, the speech checklist's counts, the learner's route through sessions 4 to 10, CI's case | all | M | Haiku 5.5 | planned |

**Order** (revised 2026-10-30 when phase A started: V03 moves before V10, because the fisher's order pays in coins and every order needs
`deliverTo`; and every milestone is Opus 5.5 at xhigh, D-game-22). V01, V02, V03, then V04, V05 and V07 (independent, one agent at a
time), V06 (the trial rendered, the owner listens afterwards), V10, V11, V12 and V09. That is
**phase A: sessions 4 to 6, A1 complete, every verb in three times** (about 10 milestones; 3 Sonnet M, the rest Haiku S/M, plus three
content M). The gate runs on `feature/village` after each. **Phase B** (V08, V13, V14, V16, V17, V15, V09 again: sessions 7 to 10, A2, the
simple future last but one) waits for the owner's word after listening to phase A, or for the pilot's data if the owner prefers the brief's
order.

**Phase B's order** (owner, 2026-10-31, after the study of the project's bottlenecks, `docs/DEVELOPMENT_BOTTLENECKS.md`): V08, V13,
V18 (the content loop in one command) and the core's M56 and M59 (agreement with a subject after its verb; the person checks, a lone name
never checked) V14, V16 and the core's M57 (the later markers) done, then V17, the core's M58 and M62
(before V15; M62 by the owner's word after V13), V15 and V09. One agent at a time, every milestone by Opus 5.5 at xhigh.

**The follow-ups' order** (the owner, 2026-11-01, after PR #20's merge; `docs/HANDOVER_VILLAGE_PHASE_B.md` section 9), on
`feature/village-followups`: a QA pass (`language`, then `progression`), the core's M65, M66 and M67, V20, V21, V22, V23, the core's M69,
and V24; the core's M68 only if CI fails a timed test. The QA passes added the core's M70 after M67 and V25 (a load keeps its day) first in the game's queue, beside M66 (2026-11-01). The QA reports' confirmed bugs join where they fit. Models are the owner's mix
(D-game-22, 2026-11-01): Opus 5.5 at xhigh for M65 to M67, M69 and V20; Sonnet 5.5 for V21 to V23; Haiku 5.5 for V24.

**Every content milestone (V10 to V15)**, besides the definition of done (`IMPLEMENTATION_PLAN.md` section 6) and the content loop
(`content/README.md`, "The edit loop"): the sheet rows first (D-game-10), each with a graded error example; the intents' examples with
errors; the Opus pass by the orchestrator (D-game-12 step 3) before the WAVs; `scripts/prerender-speech.sh` and the checklist for the owner;
the grammar cache refreshed with the server; one run with models (`SpokenModelTests` over the new rows); the goldens, the save fixture's
hash and the scripted runs re-baselined; the lesson timed in a scripted run (5 to 10 minutes); the counts in the documents (V09).

## 5. Risks and open points

- **The owner's listening is the real cost.** Phase A adds about 95 lines (190 files, about 14 MB; the tense rows are 5 or 6 lines a
  lesson); phase B about 120 (four lessons now, not three). R14 and R15 said so. The prerender's `--rate 16000` halves the size when the
  owner wants it.
- **Voices.** If no Italian voice sounds right, the choice is speed variants of the three Spanish voices (the same timbre, a different pace)
  or three NPCs instead of four (drop the farmer; session 7 moves to the shopkeeper). The trial comes before any lesson (V06 before V10).
- **Places the core must be tried before a row relies on it** (the CLI, `eval --transcript … --prompt … --content content/game-v1`,
  in the milestone's first hour): `me gustan las manzanas` (a postverbal plural subject: `conj.person` must stay silent); `¿cuánto cuestan los
  panes?` (the same); `quiero tres manzanas rojas` (an adjective after `[NOUN:item]`: `gender.agree` and `word_order` must fire on `rojas`
  misplaced or `rojos`, not on the right form). And the tenses: `ayer compré dos panes` and `fuimos al río` clean (`conj.irregular` silent
  on `fuimos`); `ayer compro pan` gives `conj.tense` expected `compré`; `mañana compraré pan` clean and `mañana compré pan` an error;
  `Domingo hizo un hacha`, `Luz hará pan` and `Mar pescó en el río` clean, the name read as a third-person subject (`domingo`, `luz` and
  `mar` are nouns to the lexicon, all third person singular; a name the lexicon lacks binds no reading and `conj.person` has nothing to
  say); `yo comprarás pan` gives `conj.person`; `compro pan` alone is clean (and is the lesson's Miss at a `pret` step, not the core's). A
  wrong grade is a core milestone (M56 and on) and the row waits.
- **A tense is graded only next to a marker.** So every row in the past or the future either carries a marker in its canonical text
  (`ayer`, `mañana`) or sits at a step that declares `tense`, and its learner-error example carries a marker so that a core code exists
  (the sheet test demands one). The owner's rule from S27 holds: correct Spanish is never a Miss, so a present with a future word at an
  `ir-a` step, or `ir a` at a `fut` step, is credited with a note (V16).
- **Names that are nouns** carry a gender: `Sol es alta` would be `gender.agree` for a female Sol (`sol` is masculine). Hence Luz (f), Mar,
  Domingo (m); a name absent from the lexicon is safe too (`[WORD]` binds no reading) but needs an `a1.txt` entry and a grammar waiver.
- **Confusion between intents** (`ask_activity` against `ask_help`, `state_obligation` against `state_have`, `offer_item` against
  `state_want`): the lint's confusion check decides; a prompt's `allowedIntentIds` is the first lever, as today, and `offer_item` is designed
  last for that reason.
- **The playtest's battery.** Without V07 the pre-check would grow past ten minutes; with it the pilot stays at 31 items and the bundle hash
  of the pilot is whatever is frozen on the day (`PLAYTEST.md` section 3).
- **Scope against the brief.** The brief says to improve the loop before the world; the owner asked for the world. Phase A is the smallest
  world that gives numbers, places and people a use; phase B is held back by a decision.
- **The session count.** Seven more lessons mean seven more loads; a tester who wants two lessons in one sitting cannot (S26's rule). V12c
  answers it: a sleep at the player's fire is the next session, so one sitting plays them in order, and `--skip-to-session n` starts a test at n. An
  owner decision could let a lesson unlock by `after` alone; the plan does not assume it.
- **Not overwhelming.** Each lesson adds one tense form in one person at most (the ladder in 3.5) and reviews the rest; session 8 is the
  exception (three new persons of a tense already known) and session 9 the only new tense. If a lesson's timing shows the tense rows push
  it past ten minutes, the report rows (the preterite) stay and a directions or names row goes, since the owner asked for the tenses.
- **Build time.** More props cost nothing measurable; the seed-42 budget test keeps watch.
- **The recognizer.** Vosk has no word list to check; a new word the learner must say that the round-trip test cannot hear goes to the
  `hears` side or to a typed-only variant, reported in the milestone.

## 6. Owner decisions (D-game-22, proposed)

- (a) **Proceed with phase A** (V01 to V07, V10 to V12): yes or no, and whether before or after the first pilot.
- (b) **The content gate of 3.9**: an A2 ceiling bundle-wide with a per-lesson A1 check, and world words in `a1.txt` under a marked block.
- (c) **The NPC set and their names**: the fisher Mar, the baker Luz, the smith Domingo, a farmer with a masculine-noun name (Ángel, León) or
  no farmer; and whether the villager gets a name.
- (d) **Voices**: the Italian pair and speed variants as the candidates; the owner listens to the trial before V10.
- (e) **The shopkeeper**: "he" in the locale (the voice and the label are male), or a female voice from the trial.
- (f) **The economy**: coins, apples and the axe (V03) as the price of giving numbers a use; or numbers as comprehension only, which drops
  V03 and the orders and keeps the plan to places, people and words.
- (g) **Phase B** (A2, sessions 7 to 10): designed here, started only on the owner's word.
- (h) **The speech folder**: downsample to 16 kHz when phase A lands, or keep 24 kHz.
- (i) **The tenses**: the preterite as the one past (yo and tú from session 3, the regular `-ar` pattern in session 5, every person in
  session 8), two futures (`ir a` from session 3, used in every lesson; the simple future in session 9, at A2, with its own small code
  milestone V16), and the imperfect and the perfect (`comía`, `he comido`) left to a later series. Seven lessons rather than six.
