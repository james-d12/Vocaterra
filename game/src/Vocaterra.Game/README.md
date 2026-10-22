# Vocaterra.Game

The engine-agnostic game layer: one running game, the conversation with the guide and the facts about the world she speaks of. It references
`Vocaterra.Simulation` by project and the language core only as the `Vocaterra.Nlp.*` packages (never its projects or source), and it knows no
engine (plan section 3, rules 1 and 3). `Vocaterra.MonoGame` and the console host build on it.

Since S4 the running game is the 3D world (`VoxelSession`). Where this file says tiles, the 2D world or `GameSession`, it describes the guide's
design from P1 to P7, when the 2D slice was the game; the 2D slice was retired at S4 (its code is tagged `2d-slice-p7`), and the guide's rules
are the same in 3D with blocks for tiles. The camp objective and the word cards are in "The camp objective (S4)" below.


## The API

```csharp
var bundle  = GameBundle.Load();                         // content/game-v1, found above the executable or the current directory
var session = new GameSession(bundle, worldSeed: 42);    // generates the world, places the guide, starts the conversation
session.Conversation.WarmUp();                           // optional: pay the first-call costs behind a loading screen

session.Step(commands);                                  // once per tick: the simulation step, then the arrow's reach check
if (session.IsNearGuide) { /* show "Pulsa T para hablar" */ }
GuideTurn turn = session.Say("¿dónde está el árbol?");   // between ticks, never inside Step
foreach (var line in turn.GuideLines) Console.WriteLine(line);   // correction, reply, repair, then the world line
var arrow = session.Pointer;                             // where the guide points, until the player gets there
```

| Type | Role |
| --- | --- |
| `VoxelSession` (S4: the only session; `GuideSession` is its shared base) | owns the `VoxelSimulation` over the scene, the bundle, the world seed, the stored time, the guide and the conversation (`GuideService`), the word progress, the camp objective (`Objective`), the word cards (`TakeWordCards`) and the arrow; `Step(commands)` feeds the objective and the cards from the tick's events; `HostEvent(name)` raises a named host event (`objective.water`) |
| `TurnWorker`, `TurnRequest`, `TurnOutcome`, `TurnSource`, `SpeechLoad` | P5: the one thread that loads Vosk, owns it and the conversation, and evaluates submitted turns in order; a request carries every input fixed at submission (scene with a copy of the world around the player and of the inventory, stored time, state digest); outcomes come back in order through `TryTake` (`GameSession.Collect` applies them on the caller's thread) |
| `SpeechEngine` | P5: the speech models from `VOCATERRA_MODELS_DIR` (`FindModels`, `TryCreate`): Vosk small Spanish, and Kokoro speaker 28 behind `CachedSynthesizer` with the cache in `~/.cache/vocaterra/tts` (`Synthesize`). Created, used and disposed by the worker thread only. The manifest comes from the folder, `VOCATERRA_MODELS_MANIFEST` or the core submodule (CR-019) |
| `SpokenUtteranceEvaluator` | P5: the call sequence for one spoken clip: the pipeline over the real recognizer with one n-best hypothesis (`SingleBest`, CR-022); `Trace` returns the result and the recognizer's output, which the guide's second evaluation reuses (`TypedUtteranceEvaluator.EvaluateRecognized`) so the clip is decoded once |
| `AudioClips` | P5: `Resample` (windowed sinc, any rate to 16 kHz), `TrimSilence`, `Prepare` (convert, trim, cap at 8 s, `null` for silence), `FromPcm16`, `ReadWav` (16-bit PCM, channels averaged) |
| `GuideService` | one turn: `Say(line, scene, now, hostStateHash)` evaluates the line on the prompt `guide-open` with the core's scripted recognizer (twice, see step 2 below), carries the context with `ConversationContext.Next`, and returns a `GuideTurn`; `SaySpoken(clip, ...)` does the same from a clip (Vosk decodes it once; `AttachSpeech`, `WarmUpSpeech`). `Context`, `GuideState`, `OpeningLine`, `WarmUp()` |
| `GuideTurn` | the verdict in plain values (`Intent`, `Communicated`, `Correctness`, `Errors`, `Summary`), `Feedback` (the recast or hint), `Replies`, `Repair`, `AgentState`, `HostEvents`, `WorldLine` (the game's own line), `Pointer` (`GuidePointer`: place, target tile, blocks, direction), `IdentifiedNoun`, `Words` (`GuideWord`: each token with the reading the core's resolver chose, CR-002), `Input` (`InputKind.Typed` or `Spoken`, which decides the credit) and `Confidence` (a spoken turn's mean Vosk word confidence), and `GuideLines` in the order shown. For a spoken turn `Line` is what the recognizer heard. No time in it, so equal inputs give an equal turn |
| `WorldFacts3D` | the nearest block of a noun from a viewpoint (`NearestOfNoun(from, "árbol")`), the direction in Spanish (`DirectionPhrase`), the distance in words, `Live(grid)` and `Snapshot(grid)` |
| `GuideNpc` | `Place(world)`: a grass column 8 to 28 columns from the spawn with headroom, clear of trees if it can; `IsNear(player)` |
| `TypedUtteranceEvaluator` | the call sequence over the packages for one typed line (scripted recognizer, fake embeddings, silent synthesizer); the console host uses it too. The pipeline counts each utterance's repeats itself (core M26a, CR-012), so the evaluation takes the carried context as it is |
| `GameBundle` | `Locate()` and `Load()` for `content/game-v1` (`VOCATERRA_CONTENT_DIR` overrides) |
| `GuideEvents` | the host event names the guide's state machine raises: `guide.point`, `guide.identify`, `guide.help`, `guide.name`, `guide.craft`, `guide.find`, `agent.greet`, `agent.farewell` |
| `RecipeBook`, `RecipeEntry`, `RecipeIngredient` | the crafting recipes, `game/recipes.json`, read with `Load(bundle, nouns)` through `TryReadHostFile`: `Entries` (the nouns, for the guide and the panel), `Rules` (the plain `Recipe` values the simulation crafts with, same order), `ForOutput(lemma)`, `IndexOf(lemma)`. A lemma that is not a catalogue noun with an item, or a station that is not a placeable tile, is an `InvalidDataException` |
| `NounCatalog` | the noun catalogue, `game/nouns.json` in the bundle, read with `Load(bundle)` through `ContentBundle.TryReadHostFile` (its bytes are the ones the load hashed, CR-007): `ForLemma`, `ForReading(lemma, pos)` (the catalogue noun a resolved reading names, CR-002), `ForTile`, `ForItem`, `IsColourless`, `Nouns` (lemma, gender, `el`/`un`, plural, tiles, items) |
| `WordProgress` | the learner's counts per catalogue noun (`Records`, `RecordOf`, `SaturationOf(lemma)` and `SaturationOf(ItemId)`, `KnownLemmas`, `MarkRead`), the play `Session`, and `RecordTurn(turn, kind, rules, now)`, which counts one guide turn: heard, spoken, spoken correctly, credit (at most one per noun per turn, and only when its gap has passed), known. Also `DueWords(now, rules)` and `SecondsUntilNextCredit(lemma, now, rules)` |
| `WordRecord`, `WordStage` | one noun's counts (`Known`, `Saturation` in thirds, `Stage`: Unseen, Encountered, Learning, Known) and the time and play session of its last credited use (`LastCreditedAt`, `LastCreditedSession`) |
| `CreditRules` | the credit per kind of input (`Typed`, `Spoken`) and the gaps between credited uses (`Gaps`, default 10 min then 1 h; `GapAfter(n)`): the one place the owner's tuning changes (D-game-6). From P5 `Default` is typed half, spoken full (`FullTyped` is the setting behind `--typed-credit full`); a word then takes three spoken uses or six typed ones, the last gap repeating |
| `CreditOutcome`, `WordCredit`, `DueWord` | what a turn did to a noun (`Credited`, `TooEarly` with the seconds left, `Wrong`, `NamedWrong`) and a word due for review (`Lemma`, `OverdueSeconds`) |
| `GuideReview` | the review question in progress: the due word, the pointed tile and the question; `GuideSession.Review`, `BeginTalk()`, `EndReview()`, `SkipTime(seconds)` |

## Words (P4): the noun catalogue and the D-game-6 rule

- `content/game-v1/game/nouns.json` names the noun of each tile kind (`trunk` and `leaves` are `árbol`, `dirt` is `tierra`, `grass` is `hierba`,
  `stone` is `piedra`) and lists `air` as colourless. Each noun also names its item kinds (`items`, P6: `madera` is `wood`, `hacha` is `axe`),
  and may carry `"mass": true` (counted in `bloques de`) and `"sources"` (`madera` comes from `árbol`, for P6b's "where is it" answers). A test
  checks that every tile and every item is a noun or colourless and every noun has a noun reading and a gender in the bundle's lexicon. The
  core supplies the article and the plural (`Morphology.Article`, `Pluralize`).
- A catalogue noun is **known** after three credited correct uses in three separate turns, spaced in time (below) (`WordProgress.UsesToKnow`). Each correct use adds credit in
  units (`UnitsPerUse` = 6, so `UnitsToKnow` = 18); saturation is units over 18, so a typed noun is 1/3, 2/3, then 1. Nothing decays.
- A use is **correct** when no verdict error's span overlaps the word or the article right before it. The uses of a turn are the catalogue nouns
  the core's resolver chose for the line's words (`GuideTurn.Words`, from `PipelineResult.Words`, CR-002; `es un arbol` counts, `la árbol` does
  not) and the catalogue nouns in the verdict's slot values. A turn that was not communicated counts nothing for use (it still counts as heard:
  a word of the guide's lines that is a catalogue noun's singular or plural form).
- `name_object` (`es un árbol`, `eso es una piedra`) names the cursor's tile: the guide says `¡Eso es! Es un árbol.` when the name is that tile's
  noun and no error was found, and `No, es un árbol.` otherwise (the game checks it, CR-014; the result is `GuideTurn.Naming`: `NameCheck`). **A
  name the guide marks wrong earns no colour for the noun named** (P4b; the orchestrator's default, the owner to confirm); it still counts as said.
  **A naming with no tile to check (`NameCheck.NoTarget`, the cursor on nothing) earns no colour either** (`CreditOutcome.NoTarget`); other intents
  that use a noun correctly (`¿dónde está el árbol?`) earn as before. **Lapse:** when the guide reveals a word (a wrong name, in a review or not),
  that word's gap restarts from now (`Lapses` + 1, last credited time set to now) so copying her correction is `TooEarly`; no credit is removed,
  a word with no credited use or a known word is untouched.
- `GameSession.Say` and `GameSession.Collect` count every turn (`Words`, with `GameSession.Credit` for the turn's `Input` kind and the stored time of the turn, which for a submitted turn is the time at submission) and keep what the last
  turn did (`LastWordTurn`: the nouns credited, the nouns that became known and the outcome per noun, for the toast and the hint).

## Spaced colour and the guide's review (P4b, D-game-6 as amended)

- **The rule.** A word's first correct use earns a third at any time. The second earns a third only if at least 10 minutes of stored time have
  passed since the first credited use, or the play session differs; the third only after 1 hour since the second credited use, or in another
  session. The gaps are one list, `CreditRules.DefaultGapSeconds` (`[600, 3600]`; the last gap repeats for the half-credit uses of P5). A word
  with half credit (P5) takes more credited uses, each gated by the gap after the previous one.
- **Early uses.** A correct use before its gap counts as said and correct (the journal's counts) and the guide answers as usual, but no credit
  and no clock: the gap runs from the last *credited* use. `WordTurnResult.Outcomes` has one `WordCredit` per noun used: `Credited`,
  `TooEarly` (with `SecondsRemaining`), `Wrong` (a verdict error on the word or its article) or `NamedWrong` (the guide's check failed).
- **Time.** `GameSession.StoredTime` is the fixed start plus `Tick / 60` plus the seconds of `SkipTime` (the demo's `--skip` and the tests); no
  clock. **Sessions.** `WordProgress.Session` (1 for a new game; `GameSession(..., playSession)`) is recorded with each credited use. A save (P9)
  restores the records and sets a new number: a plain value change. A different session lifts the gap, and tests construct progress with an
  earlier session (`Session = 2`) to show it.
- **Due words.** A word is due when it has a credited use, is not known and its gap has passed (or the session changed): `DueWords(now, rules)`,
  most overdue first, then the lemma in ordinal order (a session-bypassed word counts as overdue since its last credited use).
- **The review.** When the player opens the dialogue the front-end calls `GameSession.BeginTalk()`. If a word is due and an example tile of it is
  within `WorldFacts.DefaultRadius` of the player, the guide points at the nearest example (`Pointer`, with `Quiz` set so the arrow shows `¿?`
  instead of the noun) and the dialogue says her question (`guide.review.ask`: `¿Qué es esto?` in three wordings; a host line outside the replay
  record, CR-015). The words are tried in due order: one with no example in range is skipped for the next. The player's next understood line is
  evaluated on the bundle prompt `guide-review` and a `name_object` in it is graded against **the pointed tile**, not the mouse cursor; a line the
  guide does not understand leaves the question open. If nothing is due the greeting is unchanged and nothing is pointed.
- Known gaps: `arbol` without the accent is read by the core as the noun with no error, so it counts (checked by a test); a bare noun with no
  article (`arbol`) is not a sentence and no intent takes it.

## One turn, step by step

1. `GameSession.Say` builds a `GuideScene` (the player's centre tile, the cursor tile, whether an arrow is out) and the stored time
   (`StartedAt + Tick / 60`, fixed start `DefaultStartedAt`; nothing reads the clock). The host state hash handed to the core is the
   simulation's state digest (`ComputeStateDigest()`), not the core's default one.
2. `GuideService.Say` evaluates the line twice, both purely. The first evaluation gives the verdict, the words (`PipelineResult.Words`) and the
   host events. The host's facts for the line (`place`, `direction`, `count`, `unit`, `target`) and the learner skills of the catalogue nouns the
   words are read as (`word.<lemma>`, CR-005) are then passed in `ConversationContext.Create(..., hostSlots, extraSkills)`, so the replay record
   holds them and the learner is updated once. Their digest is `HostDigest.Of` (the simulation digest with the slots and skills, the core's
   contract); with neither, the context keeps the bare simulation digest. Only the second result is used; `carried.Next(result, now)` carries the guide's state, the learner
   model and the repeat counts. The repeat counts are the carried ones: the core adds this utterance's repeats itself (CR-012, core M26a).
3. The response lines split into the correction (`IsRecast` or a `feedback.*` pool) and the guide's reply (the agent's pool). The host events come
   from `Decision.Agent.Actions`.
4. For `guide.point` the game finds the nearest tile of the noun in the verdict's slot `place` and renders `guide.point.found` ("El árbol está a la
   derecha, a doce bloques."); for `guide.identify` it names the cursor tile (`guide.identify.found`); for `guide.help` it picks `guide.help.ask`
   or, with an arrow out, `guide.help.walk`.

The game's own line is still **rendered by the game** (CR-003 adopted in part, core M26d; CR-016 open): the core cannot choose its template,
because a transition is chosen by the verdict alone and a template that names a slot the host did not supply fails the whole evaluation. The
three cases (`guide.point.found`, `.here`, `.none`) therefore stay three game renders with `bundle.TemplateEngine` from the bundle's dialogue
pools. Their inputs are host slots of the second evaluation, so the replay record holds them. The variant is drawn from the core's
`SeededRng.ForInteraction(worldSeed, "guide.host", counter)`, so it is a pure function of the seed, the world and the interaction counter.

## Why the guide is here and not in the simulation

The guide's position is a pure function of the generated world (`GuideNpc.Place`); she does not move or collide yet; and her conversation state
belongs to the agent state machine in the context. Putting her in the simulation would add a record to its state digest and to the saves for
nothing that ticks. When characters move, collide or are saved (the innkeeper, G3), they become entities of the simulation.

## The wider world of things (P6)

- The catalogue now names the surface materials and the placed blocks: `arena` (sand), `flor`, `seta`, `madera` (planks; the item is wood), `puerta`,
  `mesa`, `silla`, `cama`, `caja`, and the tools `hacha` and `pico` (items only). `tierra`, `piedra` and `hierba` are unchanged. `seta` is
  new in `lexicon/world.json` (feminine, singular and plural).
- `WorldFacts.NounOfTile` and `TilesOfNoun` cover every new tile, so the guide's "¿dónde está la arena?" points at sand and "¿qué es esto?" names
  it. `WorldFacts` still says nothing about items: the guide's crafting and "where does it come from" answers are P6b.
- `NounCatalog.ForItem(ItemId)` gives the noun of an item, `NounEntry.Mass` and `NounEntry.Sources` the two new fields;
  `WordProgress.SaturationOf(ItemId)` colours an item by its noun, so the inventory and the hotbar use the same rule as the tiles.
- Words added to the CEFR lists: none. `cefr-check` checks dialogue templates only, and no template uses the new nouns yet.

## Crafting and the guide's crafting answers (P6b)

- `game/recipes.json` holds the recipe table of plan section 4.3 (numbers in the file only); `GameSession.Recipes` loads it and hands `Rules` to the
  simulation. `game/nouns.json` gained `horno`, `cristal` (mass), `ladrillo`, `espada`, and a `sources` entry for every material: `madera` from
  `árbol`, `piedra`, `arena`, `tierra`, `flor`, `seta` from themselves. `lexicon/world.json` gained `ladrillo`.
- **`ask_craft`** ("¿cómo hago una mesa?", "¿qué necesito para una puerta?", "¿cómo se hace un horno?", "quiero hacer una silla"; slot `item`): host
  event `guide.craft`, state `talking`. The world line is up to three sentences from the bundle's `guide.craft.*` pools: the ingredients with
  their numbers (a mass noun counted with "bloques de"), the station ("Usa la mesa.") or "No necesitas nada más.", and what the inventory lacks
  ("Te faltan cinco bloques de madera." / "Te falta un bloque de madera." / "¡Ya lo tienes todo!"). An item with no recipe, or a noun that is not in the
  catalogue, gets "Eso no se hace."
- **`ask_find`** ("¿dónde hay arena?", "¿dónde encuentro madera?", "¿de dónde sale la madera?"; slot `material`): host event `guide.find`, state
  `pointing`. When the player carries the material the line says so ("Tienes tres bloques de madera.", no arrow). Otherwise, for a material
  that comes from something else (`madera` from `árbol`) it says where it comes from ("La madera sale de los árboles.") and then the nearest source
  with the P3 line and arrow; a material that is its own tile (`arena`) gets just the nearest tile. **"Dónde hay" moved here from `ask_directions`**
  (the two grammars cannot share it): `¿dónde hay piedra?` is `ask_find` now, with the same arrow.
- **`ask_directions` for a thing the player carries** with no tile in the world ("¿dónde está el hacha?") says "Tienes un hacha." (P6 said "Aquí no hay un hacha").
- The scene carries the inventory (`GuideScene.Inventory`); `GameSession.Say` fills it from the simulation. The facts of the answers are host slots of the
  second evaluation (`thing`, `need`, `station`, `lack`, `source`, plus the P3 ones), so they are in the digest and the replay record. The line is the
  game's render from the bundle's pools (CR-016) and each ingredient part is rendered and joined by the game (CR-018: a template cannot list).
- Words added to the CEFR lists: none. Words in the new lines that `cefr-check --level a1` does not find in the A1 list: `faltar`/`falta` and
  `nada` (both are in the A2 list; `cefr-check --level a2` is clean for the new lines). They are in the brief's wording; the owner may want them in the A1 list.

## Caves, ores and the torch (P7)

- **Catalogue** (`game/nouns.json`, hand-authored, in the content hash): `carbón`, `cobre`, `hierro`, `oro` (mass; tiles `coal-ore`, `copper-ore`,
  `iron-ore`, `gold-ore`; items `coal`, `copper`, `iron`, `gold`; `sources` are themselves, so `ask_find` answers "¿dónde hay hierro?" with the
  nearest ore), `antorcha` (tile `torch`, item `torch`), `lámpara`, `cubo`, `anillo` (items `lamp`, `bucket`, `ring`, no tile). `hierba` now also
  names `tall-grass`. A new `"deep": true` flag marks the ores: when the guide's search finds none, it says "Está muy abajo." (pool
  `guide.point.deep`) instead of "No veo …". `NounEntry.Deep` carries it.
- **World facts** (`WorldFacts`): `carbón`, `cobre`, `hierro`, `oro` name the ore tiles, `antorcha` the torch, `hierba` the grass and tall grass;
  `NounOfTile` is the inverse. The guide's search reads the turn's snapshot (radius 96) out to `WorldFacts.DefaultRadius` (64) on each axis, so
  an ore that is not within 64 tiles of the player counts as "muy abajo".
- **Recipes** (`game/recipes.json`, data only): `antorcha` by hand from `madera` 1 and `carbón` 1, four per craft; `lámpara` at the `mesa` from
  `hierro` 1 and `cristal` 1; `cubo` at the `horno` from `hierro` 3; `anillo` at the `horno` from `oro` 2. The guide's `ask_craft` answers them like
  any other recipe ("Para un cubo necesitas tres bloques de hierro. Usa el horno.").
- **Single-word nouns**: the new ones are `carbón`, `cobre`, `hierro`, `oro`, `antorcha`, `lámpara`, `cubo`, `anillo`. `lexicon/world.json` gained the
  noun readings with gender and plural that the bundle lacked: `carbón` (m, carbones), `cobre` (m, cobres), `cubo` (m, cubos), `lámpara` (f,
  lámparas); `hierro`, `oro`, `antorcha`, `anillo` were already in `seed.json`.
- **CEFR**: `abajo` added to `cefr/a1.txt`, because the new line "Está muy abajo." is template text and `cefr-check --level a1` reads it (`abajo`
  is in the A2 list already, so this is the one A1 addition, and the owner may want to review it). `lámpara` and `anillo` are in the A2 list
  already; `carbón`, `cobre`, `cubo` are in no CEFR list. They appear only in slots, recipes and catalogue entries, which the check does not read,
  so no list was changed for them.
- **Out of scope in P7**: metal tool tiers ("pico de hierro", "hacha de hierro"). A multi-word noun would need the `[NOUN]` slot to bind two tokens,
  and the tool is one item kind per tier; both belong to a later milestone. The metals get a use through the three recipes above.

## Rules this layer keeps

Packages only; no engine type; no `System.Random`, clock, `Stopwatch` or `DateTime` (a test scans the sources); the stored time and the world seed
come from the game; dictionaries that reach output are ordinal. The tests are in `tests/Vocaterra.Game.Tests`: guide turns on seed 42 with the
fixed stored time (verdict, state, host event, target tile, world line, accent-less input, the repair path, determinism), `WorldFacts` tie-breaks,
the guide's bundle content and the architecture rules. P4: `NounCatalog` (coverage of every tile, genders, articles and plurals, the
readings of an unaccented form, the parse errors), `WordProgress` (three uses in three turns, thirds, one credit per turn, the article rule,
the not-communicated turn, heard, read, the credit setting, determinism) and the real core on `es un árbol`, `la árbol` and `es un arbol`. P6b: `RecipeBookTests` (the table, catalogue coverage, stations, malformed files) and
`CraftingGuideTests` (the intents, every answer branch through the real core, the carried line, determinism, the new pools). P7: `OresAndTorchTests`
(each ore is a mass catalogue noun with its tile, item and source; the torch and the metal recipes and their stations; "¿dónde hay carbón?" points,
"¿dónde hay oro?" says "Está muy abajo.", the carried coal is "Tienes dos bloques de carbón.").

## The camp objective and the word cards (S4)

`Objective` (`Objective.cs`) is the checklist `Campamento`: `madera` 5, `frutas` 3, `antorcha` 1 (placed at the fire ring), `agua` 1 (the host
event `objective.water`, which S6 raises when the bottle is filled). It moves only on events: `RecordPickup(item, count)` for wood and berries,
`RecordTorchPlaced(atFireRing)`, `RecordHostEvent(name)`; each returns the steps that this call completed. `Summary` is the line the HUD shows.
`VoxelSession.Step` feeds it from the tick's events; `VoxelSession.AtFireRing` decides the ring. The first time an item enters the inventory the
noun is counted as heard and read (`WordProgress.MarkHeard`, `MarkRead`) and its `WordCard` (`la madera`) waits in `TakeWordCards()`; a card is
never a quiz and never a use.

