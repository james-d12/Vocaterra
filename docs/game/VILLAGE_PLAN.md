# The village and the lessons: the V series

A plan written on 2026-10-30 at the owner's request, from a read of the plans, the decisions, the content bundle, the locale, the
game's code and the models on this machine (`main` at `10afbd6`, after X06 and the mobile report). Nothing was built or run; the
one computation is the village's footprint, drawn from `game/scene.json` (section 2.2). Its goal: **more Spanish in the same world**: a
bigger village with more people, each with a reason to be spoken to, and a connected run of lessons from the end of A1 into A2.

**Status:** proposed; it awaits the owner's answers in section 6 (D-game-22). Its milestones are V01 to V15 (section 4), on the feature
branch `feature/village` (never `main`; the owner merges by pull request). Commit prefix `V<nn>:`.

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
| Verb forms taught | querer, tener, necesitar (all persons, present); ser, estar, ir; ir a + infinitive; comí, bebí, fui and the tú forms | the lessons' `verbs` |

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
- **The speech folder is 33 MB** and the prerender script offers `--rate 16000` "when the folder is over 20 MB". Phase A adds about 80
  lines (160 files); the owner may want the downsample then.
- Two stale comments still call the guide "Rosa" (`PhraseReview.cs:31`, `LessonRunner.cs:444`, noted at C08). No NPC here is named Rosa.

## 3. The direction

### 3.1 Principles

1. **The world grows inward.** New buildings go on the village's free ground; the one NPC outside it stands on an existing site. No new
   terrain, site or road (section 2.2).
2. **Every NPC needs something another NPC or place provides**, and the player is the one who speaks to move it (section 3.4). The
   shopkeeper and the villager join the web; the guide stays the reviewer (her review and the X06 deck ask every row that names a `reviewLine`).
3. **Each lesson reuses the last one** (`after`, two `review` steps, the previous NPC naming the next), **adds one grammar point and one
   word field**, and ends by naming where to go next (the objective line, `lesson.<id>.objective`).
4. **A1 first, as data the gate can check** (section 3.9): sessions 4 to 6 finish A1 and are gated at A1; sessions 7 to 9 are A2 and are
   gated at A2. Phase B starts only when the owner says so.
5. **Words the player handles come from the world** (the catalogue, the scene's labels, the items), so the beacon, the book and the
   greyscale rule work on them unchanged.
6. **Nothing is gated behind speech** (D-game-8, D-game-9): an order the learner cannot ask for in Spanish is still delivered by walking
   up with the goods (section 3.4), as the stall's bottle is.

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
| The farmer's stand | (the field, `campo`) | the farmer at (8, 0) facing the wheat field; a crate with `apples` at (8, -3); three `tree` props at x 17 as an orchard | the field already exists between houses 1 and 2; the farmer gives it a voice | 
| The fisher's bank | the NPC only | `bridge.east` (238, 205), dx 2, dz 3, facing the river; two crates and a lamp | the landing is on the road 15 s from the square; a new voice outside the village (R15) with no new site |
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
| `axe-handle` | the smith | 2 `madera` and 3 `monedas` | the `hacha` | session 6 | wood is faster afterwards: the loop pays back |
| `water-for-plants` | the farmer | 1 `agua` (the river, the bottle) | 3 `manzanas` | session 7 | the river skill of session 1 |

Prices: bread 2 at the baker and the stall, an apple 1 at the farmer. An order is an objective with `session` (shown and counting from
that play session) and `deliverTo` (section 3.7): when the panel opens on that NPC and the inventory holds the goods, the host takes them,
raises the NPC's `<npc>.delivered` as a host turn (so the NPC speaks its own thank-you and reward line, and the reward is that line's
`give_item`), logs `order_delivered`, and the objective completes. Asking in Spanish is the lesson's content; walking up with the goods is
enough (principle 6). A purchase is the stall's exchange with a price: `request_item` with `qty` at a prompt whose host event's action is
`sell` takes `qty x price` coins and gives `qty` items; too few coins raise `<npc>.cant_pay`, a host turn the agent answers ("Cuesta cuatro
monedas. No tienes monedas."). Nothing is lost on a miss: the shopkeeper's bottle rule (`VoxelSession.Give`) is the model.

Phase B (session 9) adds the one repeatable source, `buy` (the NPC pays for what the player sells), and is the only place a new intent must
be checked against `state_want` for confusion (section 5).

### 3.5 The lesson arc

Sessions 4 to 6 finish A1; 7 to 9 are A2. One lesson a session, as S26 set it (a lesson needs a load; two lessons may share a session later
by `after` alone). Each is 5 to 10 minutes with two `review` steps, one `do` step at least, one order at most, and the next NPC named at
the end. The phrase ids below are proposals; the content milestone writes the rows (the sheet's columns, D-game-10), the Opus pass reads
them, and the owner listens.

| Session | Lesson | NPCs | Grammar | Words | Mechanics it needs | Level |
| --- | --- | --- | --- | --- | --- | --- |
| 4 | `el-pueblo` "El pueblo" | the fisher, the villager | `¿Dónde está…?` for any place; `está cerca / lejos`; `a la izquierda / derecha`; `hay` | the places: tienda, panadería, herrería, pozo, torre, puente, río, campo, casa, plaza | the beacon from the `place` slot and by place; `beacon_reached` (V02) | A1 |
| 5 | `numeros-y-pan` "Números y pan" | the baker, the shopkeeper | 1 to 10; plural nouns; `¿Cuántos…?`; `¿Cuánto cuesta?` | pan, moneda, madera, horno | `qty` gives N; coins; the oven order; `sell` (V03) | A1 |
| 6 | `gente-del-pueblo` "Gente del pueblo" | the smith, the villager, the farmer | `¿Quién es?`, `es el / la …`; `¿Cómo te llamas?`; `¿Qué haces?`, `trabajo en …`, `vivo en …` | the jobs and the names; casa, herrería | the axe order (V03) | A1 |
| 7 | `como-es` "¿Cómo es?" | the farmer, the shopkeeper | adjectives after the noun, agreement; `me gusta / gustan` | the colours, grande, pequeño, nuevo, viejo, caliente, frío; manzana | the water order; apples for sale; `[ADJ]` in `request_item` | A1 to A2 |
| 8 | `ayer-en-el-pueblo` "Ayer en el pueblo" | the villager, the smith | the preterite in the third person (`hizo`, `fue`, `estuvo`, `comió`); `tener que` + infinitive | the week's story of the village | none | A2 |
| 9 | `el-mercado` "El mercado" | the baker, the farmer, the shopkeeper | 11 to 20; `¿Cuánto es todo?`; `más … que`; `¿Puedo pagar con …?`; selling | barato, caro, todo, poder, vender | `buy` (V03b) | A2 |

**Session 4, "El pueblo" (V10).** The objective line sends the player down the road; the fisher is on the way, at the bridge. He greets
(`hola`, a review step: the transfer of a known phrase to a new voice, R15), asks `¿Qué buscas?` (the villager's line, now his too) and
answers any place with a point and a fixed line.

| Learner says | Spanish | Intent, prompt | Error example (codes) |
| --- | --- | --- | --- |
| `donde-esta-la-panaderia` | ¿Dónde está la panadería? | `ask_directions`, `fisher-open` | dónde está el panadería (gender.agree) |
| `donde-esta-la-herreria` | ¿Dónde está la herrería? | `ask_directions`, `fisher-open` | dónde están la herrería (conj.person) |
| `esta-lejos` | ¿Está lejos? | `ask_far` (new), `lesson-far` | es lejos (ser_estar) |
| `hay-un-pozo` | ¿Hay un pozo en el pueblo? | `ask_availability` (new), `lesson-hay` | hay una pozo (gender.agree) |
| `gracias-adios` (review) | Gracias. Adiós. | `thank`, `farewell` | |

| Learner hears | Spanish | Line |
| --- | --- | --- |
| `que-buscas` | ¿Qué buscas? | `fisher_greet.1` |
| `esta-a-la-izquierda` | La panadería está a la izquierda de la torre. | `guide.lesson.fisher.left.1` (a `listen` step: left or right of the tower) |
| `esta-cerca` | La tienda está cerca. El lago está lejos. | `guide.lesson.fisher.near.1` |
| `hay-dos-casas` | En el pueblo hay una tienda, una panadería y tres casas. | `guide.lesson.fisher.hay.1` |
| `el-letrero` | El letrero dice: tienda, río, montaña. | `guide.lesson.villager.sign.1` (at the signpost) |

`do` steps: walk to the bakery (`pointPlace: bakery`, waits for `beacon_reached bakery`), then to the smithy; the villager's step reads the
signpost. The order `fruit-for-mar` is shown by the fisher's last line ("Tengo hambre. ¿Tienes fruta?") and completes whenever the player
comes back with three. New in the bundle: the `fisher` agent and pools (`fisher_hello`, `fisher_greet`, `fisher_point`, `fisher_far`,
`fisher_near`, `fisher_unknown_place`, `fisher_help`, `fisher_thank`, `fisher_bye`, `fisher_delivered`), the answer set `places-village`,
the prompts `fisher-open` and `lesson-far`, `lesson-hay`, `lesson-place`, about 6 check items, the `review` steps X06 left for `personas` and
`ayer-y-manana`.

**Session 5, "Números y pan" (V11).** At the bakery. Luz counts her loaves (a `listen` step with the numbers), asks how many the player
wants, names the price, and has no wood for the oven.

| Learner says | Spanish | Intent, prompt | Error example |
| --- | --- | --- | --- |
| `quiero-dos-panes` | Quiero dos panes, por favor. | `request_item` (qty), `baker-open` | quiero dos pan (noun.number) |
| `cuanto-cuesta` | ¿Cuánto cuesta? | `ask_price` (new), `baker-open` | cuánto costa (conj.irregular) |
| `tengo-madera` | Tengo madera. | `state_have`, `lesson-wood` | yo tiene madera (conj.person) |
| `hay-pan` | ¿Hay pan? | `ask_availability`, `stall-need` (the stall too) | hay pans (vocab.unknown) |
| `no` (review) | No. | `deny` | |

| Learner hears | Spanish |
| --- | --- |
| `uno-a-cinco` | Uno, dos, tres, cuatro, cinco. |
| `seis-a-diez` | Seis, siete, ocho, nueve, diez. |
| `cuantos-panes` | ¿Cuántos panes quieres? |
| `tengo-diez-panes` | Tengo diez panes. |
| `cuesta-dos-monedas` | El pan cuesta dos monedas. |
| `necesito-madera-para-el-horno` | Necesito madera para el horno. Cinco bloques. |
| `toma-cuatro-monedas` | ¡Gracias! Toma cuatro monedas. |

The order `oven-wood` (5 wood, 4 coins) then the purchase (`quiero-dos-panes`, 4 coins, 2 loaves). The stall learns the price too
(`stall.price` host event, "Cuesta dos monedas."). Grammar the core already grades: `quiero dos pan`, `cuánto costa`, `cuestan` for one loaf.

**Session 6, "Gente del pueblo" (V12).** The villager introduces the smith ("Él es Domingo. Es el herrero.", third person reviewed), the
learner asks names and jobs, the smith makes the axe.

| Learner says | Spanish | Intent, prompt | Error example |
| --- | --- | --- | --- |
| `como-te-llamas` | ¿Cómo te llamas? | `ask_name` (new) | cómo te llama (conj.person) |
| `que-haces` | ¿Qué haces? | `ask_activity` (new) | qué hace (tú asked as usted: the lesson's person check, not a core error) |
| `es-el-herrero` | Es el herrero. | `name_object` | es la herrero (gender.agree) |
| `donde-vive` | ¿Dónde vive Luz? | `ask_directions` (place from a name: the house) or `ask_residence` (new) | dónde vivo Luz (conj.person) |
| `necesito-pan` (review, at the baker) | Necesito pan. | `state_want` | |

Heard: `me-llamo-domingo` "Me llamo Domingo.", `soy-el-herrero` "Soy el herrero. Trabajo con fuego y madera.", `vive-en-la-casa` "Luz vive en la
casa al lado de la tienda.", `quieres-un-hacha` "¿Quieres un hacha? Necesito dos bloques de madera y tres monedas.", `toma-el-hacha` "Toma el
hacha." The learner does not say their own name (M29d: a name is not in the recognizer's vocabulary; the villager's `me-llamo` row says so).

**Sessions 7 to 9 (phase B, V13 to V15)** are designed to the same shape and detailed in their briefs: session 7's rows are `la-manzana-es-roja`
(`describe`, gender.agree), `quiero-tres-manzanas-rojas` (`request_item` with `[ADJ]`, word_order), `me-gusta-el-pan` and `me-gustan-las-manzanas`
(`state_like`), `es-grande` / `es-pequeno`; session 8's `que-hizo-luz-ayer`, `hizo-pan`, `fue-al-rio`, `tengo-que-ir`, with a three-line story to
listen to; session 9's `cuanto-es-todo`, `es-muy-caro`, `vendo-madera`, `puedo-pagar-con-fruta`, the numbers to twenty and `más barato que`.
Each starts only after the owner has heard phase A.

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

Changes to existing intents: `request_item` gains `[ADJ]?` after the noun (phase B; R02 noted the gap), `state_action` gains `[VERB:hacer]`
and a `[WORD:name]?` subject (phase B). Prompts: one open prompt per new NPC (`fisher-open`, `baker-open`, `smith-open`, `farmer-open`) in the
stall's constrained style, plus about five `lesson-*` prompts a lesson with their `lesson-*` answer sets, as S26 did. Answer sets:
`places-village`, `supplies-bakery`, and the lesson sets. Every `hears` line is a `guide.lesson.<npc>.*` pool (so the lint counts it as the
game's, S26) with the NPC as its speaker (V01's rule).

### 3.7 What changes in the data formats

| File | Addition | Read by |
| --- | --- | --- |
| `game/npcs/<id>.json` (schema v2) | `prompts` by state (`{"idle": "baker-open", "offering": "baker-offer"}`; the review prompt for the guide); `opening` (`{"event": "fisher.greet", "when": "always" \| "first" \| "never"}`); `voice` (`{"speaker": 29, "name": "em_alex", "speed": 1.0}`); `keyWords` (moved from `words.json`) | `NpcService`, `SpeechSpeakers`, the renderer, `GuideSession` |
| `game/host-events.json` | `give_item` takes `count` (a number, or `"qty"` for the verdict's slot, capped at 5); new kinds `point_at_place` (a scene name), `point_at_slot` (`"slot": "place"`, resolved through the scene's labels; no match raises `<npc>.unknown_place`), `take_item` (`item`, `count`), `sell` (`item`, `price`), `buy` (B: `item`, `price`) | `HostEventTable`, `VoxelSession.ApplyHostEvents` |
| `game/objectives/<id>.json` | `session` (shown and counting from that play session); `deliverTo` (an NPC) with `deliver` (items and counts); the completion raises `<npc>.delivered` and logs `order_delivered` | `ObjectiveBook`, `VoxelSession` |
| `game/lessons/<id>.json` | `level` (`a1` \| `a2`); a `do` step's `pointPlace` (a scene name) beside `point` (a noun) | `LessonBook`, `LessonRunner`, the validator |
| `game/checks.json` | `lesson` on every item (the lesson it belongs to) | `CheckBank`, `CheckBattery` |
| `game/scene.json` | the village's radius; the props of 3.2; the NPC placements; the stamps `oven` and `well`; labels | the builder |
| `game/words.json` | `keyWords` leaves for the NPC files; the direction words stay | `GameWords` |
| `content/locale/en/*` | `npc.<id>.role` for four NPCs; the lessons' text; `objective.<id>.title` for the orders; `line.<id>` for every new line; the checks' options | `LocaleBundle` |
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
| V08 | Game.Core, scripts | the lesson's `level`; `GameDataValidator` checks the lemmas of each lesson's phrases, variants, lines and prompts against the bundle's cumulative level lists; the gate's lint moves to `--level a2 --fail-on-level` with `--level a1` as a report | S |

No change to the core is expected. If `state_like`, `ask_price` with a plural subject or an adjective after `[NOUN:item]` grades wrongly
(section 5), that is a core milestone (M56 and on), filed from the content milestone's report, never patched in the game.

### 3.9 The gate for A1 and A2 content

Today's gate demands A1 bundle-wide (section 2.5). The rule this plan proposes, for the owner (section 6):

1. **The bundle's ceiling is A2:** `lint --strict --level a2 --fail-on-level --host-pools 'guide.*'`. A word above A2 anywhere fails.
2. **A lesson's promise is per lesson:** every lesson declares `level`; the game's validator (`--validate-content`, in `content.sh refresh`
   and the gate) checks the Spanish of an `a1` lesson (its phrases and variants, the lines its steps name, its prompts' texts) against the
   A1 list, and an `a2` lesson against A1 plus A2. Sessions 1 to 6 are `a1`.
3. **World words** (nouns the player handles or the beacon names: `madera`, `antorcha`, `botella`, `panadería`, `herrería`, `pozo`, `horno`,
   `hacha`, `moneda`, `izquierda`, `derecha`, the labelled places) join `cefr/a1.txt` under a marked block with a one-line reason each, as
   S5 and S17 did; a name that is not a word is listed the same way. `cefr-check --level a1` keeps printing what is above A1 for the owner.
4. The grammar gate, the sheet test and the Opus pass are unchanged.

## 4. Milestones

| ID | What | Depends on | Size | Model | Status |
| --- | --- | --- | --- | --- | --- |
| V00 | This plan; D-game-22 proposed; the row in `IMPLEMENTATION_PLAN.md` 4.8 | | | orchestrator | written 2026-10-30, awaiting the owner |
| V01 | NPC behaviour from data: prompts by state, the opening event, the voice and the key words in `game/npcs/<id>.json`; the speaker of a pool by NPC id; `--talk` from the roster | | M | Sonnet 5.5 | planned |
| V02 | Host actions and the beacon by place: `give_item` with a count, `point_at_place`, `point_at_slot`, `beacon_reached`, a `do` step's `pointPlace` | V01 | M | Sonnet 5.5 | planned |
| V03 | Items and orders: the coin, the apple and the axe; `take_item` and `sell`; objectives with `session` and `deliverTo`; `order_delivered` | V02 | M | Sonnet 5.5 | planned |
| V04 | The village grows: the scene of 3.2 (radius 24, the bakery and oven, the smithy and forge, the well, the farmer's crate and orchard, the fisher's bank, the placements, the labels) | | S/M | Haiku 5.5 | planned |
| V05 | Four figures and faces; the coin, apple and axe icons and held sprites | | S/M | Haiku 5.5 | planned |
| V06 | The voice trial (the owner listens) and the voice from the NPC file | V01 | S | Haiku 5.5 and the owner | planned |
| V07 | The check battery by lesson: `lesson` on the items; a battery takes the unlocked lessons' items | | S | Haiku 5.5 | planned |
| V08 | The A1/A2 gate of 3.9: `level` on the lessons, the validator's check, the gate's lint at A2 | | S | Haiku 5.5 | planned (phase B) |
| V09 | Documents and runs: the counts in `README.md` and `PLAYTEST.md`, `SCRIPTED_RUNS.md` with two new runs (the bakery purchase, asking the way), `SPEECH_CHECKLIST.md`; once per phase | the phase's lessons | S | Haiku 5.5 | planned |
| V10 | Session 4 "El pueblo": the fisher; places and directions; `ask_far`, `ask_availability`; the order `fruit-for-mar`; the X06 review steps in `personas` and `ayer-y-manana` | V01, V02, V04, V05, V06, V07 | M | Sonnet 5.5 design and intents, Haiku 5.5 rows and WAVs | planned |
| V11 | Session 5 "Números y pan": the baker; numbers, plurals, `ask_price`; the oven order; the purchase; the stall's price | V03, V10 | M | Sonnet 5.5 / Haiku 5.5 | planned |
| V12 | Session 6 "Gente del pueblo": the smith and the farmer; `ask_name`, `ask_activity`, `ask_residence`; the axe order | V11 | M | Sonnet 5.5 / Haiku 5.5 | planned |
| V13 | Session 7 "¿Cómo es?": adjectives and `gustar`; `describe`, `state_like`, `[ADJ]` in `request_item`; the water order; apples | V08, V12 | M | Sonnet 5.5 / Haiku 5.5 | planned (phase B) |
| V14 | Session 8 "Ayer en el pueblo": the preterite in the third person, `tener que`; `state_obligation` | V13 | M | Sonnet 5.5 / Haiku 5.5 | planned (phase B) |
| V15 | Session 9 "El mercado": 11 to 20, `¿Cuánto es todo?`, `más … que`, `poder`; `offer_item` and `buy` | V14 | M/L | Sonnet 5.5 / Haiku 5.5 | planned (phase B) |

**Order.** V01, V02, then V04, V05 and V07 (independent, one agent at a time), V06 with the owner, V10; then V03, V11, V12 and V09. That is
**phase A: sessions 4 to 6, A1 complete** (about 10 milestones; 3 Sonnet M, the rest Haiku S/M, plus three content M). The gate runs on
`feature/village` after each. **Phase B** (V08, V13 to V15, V09 again) waits for the owner's word after listening to phase A, or for the
pilot's data if the owner prefers the brief's order.

**Every content milestone (V10 to V15)**, besides the definition of done (`IMPLEMENTATION_PLAN.md` section 6) and the content loop
(`content/README.md`, "The edit loop"): the sheet rows first (D-game-10), each with a graded error example; the intents' examples with
errors; the Opus pass by the orchestrator (D-game-12 step 3) before the WAVs; `scripts/prerender-speech.sh` and the checklist for the owner;
the grammar cache refreshed with the server; one run with models (`SpokenModelTests` over the new rows); the goldens, the save fixture's
hash and the scripted runs re-baselined; the lesson timed in a scripted run (5 to 10 minutes); the counts in the documents (V09).

## 5. Risks and open points

- **The owner's listening is the real cost.** Phase A adds about 80 lines (160 files, about 12 MB); phase B about as many again. R14 and
  R15 said so. The prerender's `--rate 16000` halves the size when the owner wants it.
- **Voices.** If no Italian voice sounds right, the choice is speed variants of the three Spanish voices (the same timbre, a different pace)
  or three NPCs instead of four (drop the farmer; session 7 moves to the shopkeeper). The trial comes before any lesson (V06 before V10).
- **Three places the core must be tried before a row relies on it** (the CLI, `eval --transcript … --prompt … --content content/game-v1`,
  in the milestone's first hour): `me gustan las manzanas` (a postverbal plural subject: `conj.person` must stay silent); `¿cuánto cuestan los
  panes?` (the same); `quiero tres manzanas rojas` (an adjective after `[NOUN:item]`: `gender.agree` and `word_order` must fire on `rojas`
  misplaced or `rojos`, not on the right form). A wrong grade is a core milestone (M56 and on) and the row waits.
- **Names that are nouns** carry a gender: `Sol es alta` would be `gender.agree` for a female Sol (`sol` is masculine). Hence Luz (f), Mar,
  Domingo (m); a name absent from the lexicon is safe too (`[WORD]` binds no reading) but needs an `a1.txt` entry and a grammar waiver.
- **Confusion between intents** (`ask_activity` against `ask_help`, `state_obligation` against `state_have`, `offer_item` against
  `state_want`): the lint's confusion check decides; a prompt's `allowedIntentIds` is the first lever, as today, and `offer_item` is designed
  last for that reason.
- **The playtest's battery.** Without V07 the pre-check would grow past ten minutes; with it the pilot stays at 31 items and the bundle hash
  of the pilot is whatever is frozen on the day (`PLAYTEST.md` section 3).
- **Scope against the brief.** The brief says to improve the loop before the world; the owner asked for the world. Phase A is the smallest
  world that gives numbers, places and people a use; phase B is held back by a decision.
- **The session count.** Six more lessons mean six more loads; a tester who wants two lessons in one sitting cannot (S26's rule). An owner
  decision could let a lesson unlock by `after` alone; the plan does not assume it.
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
- (g) **Phase B** (A2, sessions 7 to 9): designed here, started only on the owner's word.
- (h) **The speech folder**: downsample to 16 kHz when phase A lands, or keep 24 kHz.
