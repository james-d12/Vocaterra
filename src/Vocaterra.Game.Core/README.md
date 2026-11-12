# Vocaterra.Game.Core

The engine-agnostic game layer: one running game, the voxel world it runs in ("The voxel world" below), the conversation with the guide and
the facts about the world she speaks of. It references only the language core's `Vocaterra.Nlp.*` projects, and it knows no engine (plan
section 3, rules 1 and 3). `Vocaterra.Game` (the MonoGame front-end) and the console host build on it.

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
| `VoxelSession` (S4: the only session; `GuideSession` is its shared base) | owns the `VoxelSimulation` over the scene, the bundle, the world seed, the stored time, the NPCs and their conversations (`NpcService`), the word progress, the camp objective (`Objective`), the word cards (`TakeWordCards`), the hints (`TakeHints`), the arrow and the game events (`Events`, S6); `Step(commands)` feeds the objective and the cards from the tick's events; `HostEvent(name)` raises a named host event (`objective.water`); `OpenTalk(npc)` / `EndTalk()` start and end a talk with an NPC (S6) and `Say` goes to the NPC the talk is with; the NPCs' host events act on the world through `NpcHostEvents` (a bottle, a bread, the beacon on the river), and the empty bottle is filled at the water by the simulation (`BottleFilled` raises `objective.water`) |
| `TurnWorker`, `TurnRequest`, `TurnOutcome`, `TurnSource`, `SpeechLoad` | P5: the one thread that loads Vosk, owns it and the conversation, and evaluates submitted turns in order; a request carries every input fixed at submission (scene with a copy of the world around the player and of the inventory, stored time, state digest); outcomes come back in order through `TryTake` (`GameSession.Collect` applies them on the caller's thread) |
| `SpeechEngine` | P5: the speech models from `VOCATERRA_MODELS_DIR` (`FindModels`, `TryCreate`): Vosk small Spanish, and Kokoro speaker 28 behind `CachedSynthesizer` with the cache in `~/.cache/vocaterra/tts` (`Synthesize`). Created, used and disposed by the worker thread only. The manifest comes from the folder, `VOCATERRA_MODELS_MANIFEST` or the core submodule (CR-019) |
| `SpokenUtteranceEvaluator` | P5: the call sequence for one spoken clip: the pipeline over the real recognizer with one n-best hypothesis (`SingleBest`, CR-022); `Trace` returns the result and the recognizer's output, which the guide's second evaluation reuses (`TypedUtteranceEvaluator.EvaluateRecognized`) so the clip is decoded once |
| `AudioClips` | P5: `Resample` (windowed sinc, any rate to 16 kHz), `TrimSilence`, `Prepare` (convert, trim, cap at 8 s, `null` for silence), `FromPcm16`, `ReadWav` (16-bit PCM, channels averaged) |
| `NpcService` (S6; it generalised `GuideService`) | the conversations with the NPCs of `game/npcs.json` (seven since village-plan V12), one `ConversationContext` each: `Open(npc, objectiveComplete, now, hash)` takes the NPC's opening host event through `UtterancePipeline.Advance` (the shopkeeper's "¿Qué necesitas?", the villager's "Hola.", the guide's transfer offer; the shopkeeper's second visit hands over the bottle) and returns an `NpcOpening`; `Say(npc, line, scene, now, hash)` evaluates a line in two phases (`Analyze` once, `Complete` with host slots, see step 2 below), carries the context with `ConversationContext.Next` and takes the world's digest again, and returns a `GuideTurn`; `SaySpoken(npc, clip, ...)` does the same from a clip (Vosk decodes it once; `AttachSpeech`, `WarmUpSpeech`). `Context`, `GuideState` and `ContextOf(npc)`, `PromptOf(npc)`, `Records` (every utterance and host turn as a replay entry), `WarmUp()`. The shopkeeper's fail-forward (the repair ladder gave up, help and then another line that is no request, four such lines, or a second visit) is a host turn (`shopkeeper.give`) with the core's own line, so the bottle is never withheld. The guide's own world lines are in `NpcService.Guide.cs` |
| `NpcHostEvents`, `NpcEffect`, `PhraseIds` | S6: the one table that maps the NPCs' host events to the world (`stall.give_water` gives a `botella`, `stall.give_bread` and `villager.give` a `pan`, `villager.point` sets the beacon on the nearest `río` block, `guide.give_water` gives an `agua`), and the sheet's phrase ids the game logs (`PhraseIds.For(intent, slots)`, the fixed mapping after the phrases in play and the prompt's own row, `PhraseSheet.PhraseFor`; `HomeOf`, `IsHomeOf`) |
| `GameEvent`, `PhraseUse`, `VoiceEvent`, `WorldEvent`, `PhraseUseKind` | S6: what the session logs for the phrase progress (S8) and the session log (S11): a `PhraseUse` with the sheet's phrase id and a kind (`supported`: answering the NPC's own question, `unaided`: said on one's own, `choice`: after the repair ladder offered choices, `dont_know`: "no sé" or help, `transfer`: with another NPC after the objective, `fail_forward`: the game carried on without the phrase), `VoiceEvent` (S7: an uncertain recognition, a cancel, the watchdog's timeout, the mic check and every speech-button press, with stable wire names in `VoiceEventNames`) and `WorldEvent` (item given, beacon set, bottle filled, `found_without_spanish`, objective steps, the transfer offer). `GuideSession.Events` is the in-memory list; ticks and stored time only, no clock |
| `PhraseSheet`, `PhraseRow`, `ChoiceOption` | S7: `game/phrases.json` (a host-owned, hashed bundle file; the machine-readable copy of `content/PHRASES.md`, which stays the source of truth): `Rows`, `Find(id)`, `PhrasesAt(prompt)`, `PhraseFor(prompt, intent, slots, river)` (the phrase a turn used: the row the prompt teaches for the intent, else `PhraseIds.For`; V12b), `OfIntent(rows, intent, slots)` (of several rows with the intent, the one naming the place or item), `Expected(prompt, intent, communicated, heard)` (the phrase the prompt waits for: the row of the intent that matched, else the closest row), `Match(phrase)` and `OptionsFor(phrases)` (the buttons for the choices the repair ladder offered, labelled with the sheet's phrase). S8 reads the same rows |
| `UncertaintyRule`, `HeardLine`, `MicCheck` | S7: the uncertainty rule of D-game-9 (`ConfidenceThreshold` 0.55 on the per-word mean without `[unk]`; `IsUncertain`, `IsTooUnsure`); the `Heard: «…» · Expected: «…»` line of a spoken turn; the recognition half of the mic check (Constrained over "hola", then Open, and the level of the clip) |
| `NpcStandOff` | S6: `Choose(grid, npcFeet, from)`: the cell about two blocks from an NPC, on standable ground with a clear line to its chest, nearest to where the player comes from; the scripted `--talk` stops there |
| `GuideTurn` | the verdict in plain values (`Intent`, `Communicated`, `Correctness`, `Errors`, `Summary`), `Feedback` (the recast or hint), `Replies`, `Repair`, `AgentState`, `HostEvents`, `WorldLine` (the game's own line), `Pointer` (`GuidePointer`: place, target tile, blocks, direction), `IdentifiedNoun`, `Words` (`GuideWord`: each token with the reading the core's resolver chose, CR-002), `Input` (`InputKind.Typed`, `Spoken` or, since S7, `Button`: a choice, Ayuda or No sé, which earns no credit), `Choices`, `Uncertain`, `Choice` and `Confidence` (a spoken turn's mean Vosk word confidence), `Npc` and `Prompt` (who the line was said to and on which prompt), `RepairKind`, `FollowUp` and `FailForward` (S6), and `GuideLines` in the order shown (the follow-up last). For a spoken turn `Line` is what the recognizer heard. No time in it, so equal inputs give an equal turn |
| `WorldFacts` | the nearest block of a noun from a viewpoint (`NearestOfNoun(from, "árbol")`), the direction in Spanish (`DirectionPhrase`), the distance in words, `Live(grid)` and `Snapshot(grid)` |
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

- `content/game-v1/game/nouns.json` names the noun of each tile kind (`trunk`, `leaves` and the spruce and birch kinds of S14 are `árbol`, `stone`, `mossy-stone` and `cobble` are `piedra`, `dirt` is `tierra`, `grass` is `hierba`;
  the undergrowth, the snow and the bushes of the 3D world are named by no noun and stay in colour) and lists `air` as colourless. Each noun also names its item kinds (`items`, P6: `madera` is `wood`, `hacha` is `axe`),
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
2. `NpcService.Say` reads the line once (`Analyze`, CR-017: the verdict and the words) and completes the analysis in the NPC's context (`Complete`);
   the guide completes it twice, purely: once to see the host events. The host's facts for the line (`place`, `direction`, `count`, `unit`, `target`) and the learner skills of the catalogue nouns the
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
event `objective.water`, which `VoxelSession` raises when the bottle is filled at the water, S6). It moves only on events: `RecordPickup(item, count)` for wood and berries,
`RecordTorchPlaced(atFireRing)`, `RecordHostEvent(name)`; each returns the steps that this call completed. `Summary` is the line the HUD shows.
`VoxelSession.Step` feeds it from the tick's events; `VoxelSession.AtFireRing` decides the ring. The first time an item enters the inventory the
noun is counted as heard and read (`WordProgress.MarkHeard`, `MarkRead`) and its `WordCard` (`la madera`) waits in `TakeWordCards()`; a card is
never a quiz and never a use.


## Phrase progress and fading scaffolds (S8)

`PhraseProgress` (`PhraseProgress.cs`) keeps one `PhraseRecord` per row of the phrase sheet (the 11 rows of `game/phrases.json`) and is fed by
`GuideSession.Log` from every `PhraseUse` (`GuideSession.PhraseProgress`). The mapping, which `PhraseProgressTests` pin: `Supported`, `Unaided` and
`Transfer` uses add the credit of the input (spoken a full use, typed half, `CreditRules`, in units of `WordProgress.UnitsPerUse`; a typed use
shows as `0.5`); a communicated use with a mistake (`PhraseUse.Correct` false) is a slip and counts nothing; `Choice` adds a choice-only use
(recognition, no production credit); `DontKnow` adds one to each phrase in play (`PhraseUse.InPlay`, the prompt's phrases) and the service phrase
itself (`no-se`, `ayuda`) counts as an unaided use when typed or said; `FailForward` adds one. **A use is `Supported` while help is on offer**
(the card or the audio of its level, or the model just shown after Ayuda or "no sé") **and `Unaided` once the level is `None`**; after the
objective a taught phrase used with another NPC is a `Transfer` (the guide's offer shows no card); the guide's review answer is `Unaided` unless
Ayuda was asked first.

The support level (`SupportLevel`: `EnglishAndCard`, `CardAndAudio`, `AudioOnly`, `None`; S16 added the first rung, see "English and the lessons (S16)" below) steps down after a *credited* correct use: credited means the phrase has
no credited use yet, or the D-game-6 gap (10 minutes after the first, 1 hour after the second and later) has passed since the last one, or the
play session changed; a credited use adds its units toward one full use, which steps the level down (a spoken use steps at once, a typed one at half
credit needs two). An early correct use is counted and logged and does not step. "No sé" or Ayuda, a choice and a fail-forward step the level up
one and clear the credit toward the next step. Only the taught phrases (`PhraseIds.HasScaffold`: `pedir-agua`, `si-por-favor`, `no-gracias`,
`donde-esta-el-rio`) have a level to move; `PhraseIds.FocusAt(prompt)` says which phrases a prompt asks for.

`PhraseSupport` (`GuideSession.Support`) is what the panel shows for the prompt it is on: `Cards` (the sheet's card text broken into phrase,
meaning and glosses, `SupportCard`) at `CardAndAudio`, `AudioOnly` (no card, the replay hook), nothing at `None` until Ayuda or "no sé" reveals the
card, and the sounds to play (`TakeCues()`: the audio line of the phrase when its card appears or its prompt appears at audio-only; the front-end
plays them through `ILinePlayer`, S9 makes them audible). A card that appears counts as shown (`PhraseRecord.CardShown`) and steps the NPC's
learner skill `phrase.<id>` with `LearnerModel.StepSkills`; the turn passes `phrase.<id>` of the phrase used (and of the hears-row the prompt asked)
through `extraSkills` when the line was understood and had no mistake (not for a choice, not for the service words). `OpenTalk(npc,
teachKeyWords: true)` shows the key words `agua` and `pan` first (`KeyWordCards`, counted as heard and read, never a quiz) and runs the shopkeeper's
question after `KeyWordTicks`; `TakeOpening()` returns it. The guide's due review (`BeginTalk`) asks a due phrase first (`PhraseReview`,
`PhraseReviews`): the line is the bundle's pool `guide.review.phrase.<id with underscores>` when it exists, else a placeholder from an existing pool
(the stall's own question for `pedir-agua`); the answer is evaluated on the phrase's own prompt (`GuideScene.PromptOverride`). What a save must keep
(S10): the `PhraseRecord`s (`PhraseProgress.Records`, `Restore`), `Day` and `Sitting`, the carded nouns and the key-words-taught flag, and the NPCs'
learner models (`phrase.*` skills).

## English and the lessons (S16, D-game-13)

English is on-screen text only; only Spanish is spoken. All of it is game content in the bundle, never code strings, and the core is not changed.

- **The ladder** (`SupportLevel`, rules in the enum's remarks, pinned by `SupportLadderTests`): 1 `EnglishAndCard` (English under the Spanish card, with
  its audio), 2 `CardAndAudio` (the English only on the help key), 3 `AudioOnly`, 4 `None`. A phrase with a scaffold starts at 1. A correct full-credit use
  at rung 1 steps to 2 at once (no spacing gap, and it is the first credited use); rungs 2 and 3 step down on a credited use after the D-game-6 gap; two
  typed half uses make one step; "no sé", Ayuda, a choice and a fail-forward step up one rung, never above 1. The help key (`PhraseSupport.Reveal`) shows the card
  and the English at every rung (`PhraseSupport.EnglishUp`). The phrases with a scaffold are the four of `PhraseIds.HasScaffold` plus every phrase a lesson
  teaches (`PhraseProgress` takes them as `scaffolds`; `HasScaffold(id)` is the instance question).
- **`game/english.json`** (`EnglishText`): `ui` words, `npcNames`, `controls` (the controls help), `tips` (first-time tips: `when` is `start`, `aimed_npc` or a world
  event name), `objective` and `nouns` (the bilingual camp line: `Campamento (camp): madera (wood) 0/5 · ...`, each English word dropped once its noun is
  known, the title's once all are), `phrases` (a short English explanation per phrase, on the card at rung 1), `lines` (the English under each NPC line, by its
  Spanish text without case, accents or punctuation; a templated world line has none) and `feedback` (plain English after a turn keyed by outcome and by the
  core's `ErrorTags`, with `unknownError` as the fallback; `EnglishText.FeedbackFor(turn)`). It is checked at load and by `EnglishTextTests`: every outcome and
  every core error code has a line, every speakable NPC line has its English.
- **`TipFeed`** (`GuideSession.Tips`): the tips come one at a time for seven seconds of game ticks and never again; the saved game keeps which were shown, which
  wait and which is up.
- **`game/lessons.json`** (`LessonBook`, `LessonRunner`, `GuideSession.Lessons`): data-driven lessons attached to an NPC. A step has `id`, `type`, `english` and
  optionally `help`, `retry`, `attempts` (1 to 5, default 2); the seven types are `explain`, `model` (`line`, `translation`), `listen` (`line`, `translation`,
  `options`, `correct`), `repeat` (`phrase`, `prompt`), `answer` (`phrase`, `prompt`, `line`, `translation`), `do` (`event`, `detail`: a world event the game
  raises, such as `item_picked_up`) and `recap` (`phrases`). `line` is a pre-rendered speech id (`phrase.hola`, `guide_offer_yes.1`); the file is checked at
  load against the phrase sheet, the bundle's prompts (the prompt must allow the phrase's intent), the speech lines and the world-event names. A lesson
  is offered at its NPC (`L` starts it) or, with `"autoStart": true`, starts at the first talk. **Rules:** every step can be skipped (`K`) and has a help path
  (`H` or Ayuda: the English hint, the model line again, the card and the English); a `repeat` step keeps the card and audio up and counts a *supported* use of the
  phrase, an `answer` step shows no card and counts as the guide's review does (*unaided*, *supported* after help); both are real turns at the step's prompt
  (`GuideScene.PromptOverride`). A turn the core did not understand, an uncertain recognition or another phrase is an attempt that did not work, and after the
  step's attempts the step moves on (`moved_on`, fail-forward, never a language failure, D-game-9); help and "no sé" are never attempts. Progress (`LessonsSave`,
  `TipsSave`) is in the save; each step and its outcome is a `LessonEvent` (the console's `event: lesson ...` lines and the session log's `lesson` lines).
  To add a lesson: edit the file, re-hash the bundle, run the tests (`LessonBookTests` loads the shipped file and checks that its WAVs exist).

  **S17 additions.** A `do` step may carry `point` (a catalogue noun: the beacon goes to the nearest block of it when the step begins) and `hud` (the short line the
  objective area shows while the step waits and the panel is closed). `LessonRunner.OnTurn` says whether the lesson *took* the turn (the core understood it):
  `GuideTurn.LessonHandled` is then set on the copy the session returns (`GuideSession.AfterTurn`), and the guide's ordinary reply and world line are neither shown
  nor played (`GuideTurn.GuideLines`, `DialogueModel`); help and "no sé" are the lesson's help path except in the step whose own phrase is `no-se` or `ayuda`.
  `Otra vez, por favor` and `Más despacio, por favor` are intents (`ask_repeat`, `ask_slower`) of the guide's prompts; her state machine raises the host events
  `guide.repeat` and `guide.repeat_slow`, and the session adds her last line (`LastLineOf`: her opening, a lesson step's line, or the last line of a turn the lesson
  did not take) to the turn's follow-up, with `SlowFollowUp` set for the slow one so the front-end plays the slow recording. The credit of a phrase goes to the
  phrase a step has in play before the fixed intent mapping (`Sí.` in the lesson, `Sí, por favor.` at an offer). Tips have a `delay` (seconds after their
  trigger), so the opening tips come one at a time. `VoxelSession` adds the stage of the objective area (`SessionStage`: find the guide, the checklist, the room at the inn, complete), the arrow to the inn and Luna's line that
  sends the player there when the checklist is complete (`point-inn`, Y07; `TakeSpoken`), the lit fire latch (a torch on the rim of the ring at
  `firering.player`, which no objective waits for any more; the guide's ring still completes the checklist's torch step) and `SessionComplete`. The stage is worked out again after a load
  (`RestoreStage`), so the save has no new field.
  **Sleep places (Y01):** sleeping, the E prompt, the closing card, the arrow to bed and waking after a death ask `VoxelSession.SleepPlaces` (`Session/SleepPlaces.cs`), not
  `firering.player`. The list is `needs.json`'s `sleepPlaces` (id, scene point, kind `fire` or `bed`, reach across and in height, access `always` or `lodging`) resolved against
  the scene's points, plus places added and removed while the game runs. A fire is usable while a lit ring stands at it (read from the world when asked), a bed when its access
  is met (`VoxelSession.LodgingGiven`); `CanSleep` means any place is usable, `AtSleepPlace` that the player stands at a usable one, and the closing card offers the sleep only
  there. A death wakes the player at the place last slept at while it is still in `SleepPlaces`, else at the place `death.wakeAt` names (`WakeAtSleepPlace`, on the nearest free ground two to five blocks from it that is reached on foot from it within 12 steps, Y05a; "at a place" likewise asks a radius, then a walk of radius + 3 steps, `SleepWalk`, so a wall says no). Lodging is given by the host action `grant_lodging` (`NpcEffectKind.GrantLodging`, `VoxelSession.GrantLodging`, Y05: no fee, no expiry; the first grant raises the world event `lodging_granted`, a second changes nothing); at a bed whose access is not met `SleepOffer.AtLockedBed` shows `ui.sleepBedLocked` and E does nothing. The save keeps the last place, the lodging flag and the places made in play (the `sleep` entry of `systems`, version 8; see `docs/game/saves.md`). The world's fire rings are one
  `FireRings` (`VoxelSimulation.Fires`, shared by the session), found by shape when it is made and again on `Rescan()`. (`rest` in `needs.json` is the rule for resting by a lit fire,
  not this list.) The latch `FireLit` (a torch on the rim of `firering.player`) is read only by the sound beds since Y07, which retired the `light-fire` objective and the hooks that used it; Y08 removes the mountain ring.

## The learner's record (S19, D-game-16)

The record of what the learner said and which lessons they finished, for the book (S20). It is game state on the player's machine: the save and
the session log; nothing is sent. Everything is plain values, with no clock and no random number.

- **The history** (`LearnerRecord.History`, entries `InputEntry` in `Learner/InputEntry.cs`). One entry per line the game took and evaluated: every dialogue
  turn (`GuideSession.Apply` for the worker's turns, `GuideSession.Say` for the synchronous path), every check answer (`CheckGrade`, with its words and errors),
  every lesson line (`Place = Lesson`, `Unit = "lesson/step"`). Each has `Id` (1, 2, ... never repeated), `Session` and `StoredTime` (the stored game time,
  Unix seconds), `Source` (`Voice`, `Typed`, `Button`: a repair choice, "ayuda", "no sé"), `Text` (what the core evaluated: the transcript for speech), `Npc`,
  `Prompt`, `Place` (`Dialogue`, `Lesson`, `Check`), `Unit`, `Intent`, `Communicated`, `Held` (an unsure recognition, D-game-9), `Errors` (`GuideError`: tag,
  expected, actual, span) and `Words` (`GuideWord`: text, span, lemma, `Pos`, `Role`, and the reading's `Person`, `Number`, `Tense`, `Mood`, `Gender`). A
  failed or cancelled request and a mic check are not lines and are not entries. The session log's `turn` lines already carry every typed and spoken turn
  (`input`), and `check_result` lines the check answers.
- **The word record** (`LearnerWords`, rows `WordRow`, forms `FormRow`, first and last use `WordUse`). A pure fold of the history: `LearnerWords.Fold(History)`
  gives the same rows as the live record. An entry adds its words when it is not `Held`, not `Button` and not a check answer (`Place == Check`, S19b): check
  answers do not count in the nouns and verbs (D-game-16), though they stay in the history and in "What I said". A token with `Role == Word` and a lemma (names and
  unknown tokens are not in it; numbers are, with their `Pos` `num`). `Times` counts the tokens of the lemma; `TimesCorrect` counts those in a line the core
  understood with no error span over the token. Each form (`aguas`, `quiero` with its person, number, tense and mood) has its own counts. A row is per (lemma,
  part of speech). The save keeps the rows whole, because the history is bounded: `LearnerRecord.SavedEntries` (500) is the newest entries kept in the save, and
  `NextId` goes on from it, so a loaded record is the saved rows, not a fold of the saved history.
- **The lesson record** (`LessonRunner.Record`, rows `LessonRecordRow`, units `LessonUnitRow`). Written by the runner's own events: the step outcomes (`Units`,
  done, skipped or moved on), `Status`, `CompletedSession` (the session of the last completion), `Turns` (turns that reached a repeat or answer step),
  `HelpUses` (the help path, the English shown), `DontKnowUses` ("no sé" said at a turn step or pressed), `RepeatUses` ("Otra vez" and "Más despacio" asked of
  the lesson's NPC) and `Phrases`. The counts are over every run of the lesson.
- **The save's learner part.** `SaveDocument.Learner` (`LearnerRecordSave`: `NextId`, the newest history entries, the word rows); `LessonProgressSave` keeps `Turns`,
  `HelpUses`, `DontKnowUses`, `RepeatUses` and `CompletedSession`. The word rows do not count the check answers (S19b). Only version 8 saves load (Y02): the older
  versions' loading code is gone, and the fixture is `tests/Vocaterra.Game.Core.Tests/Fixtures/save-v8.json`.

**The read API for the book (S20).** Read it from the session; the lists are sorted, so the same record always gives the same order.

- `session.Learner.Nouns`: the noun rows (`WordRow`), by lemma (ordinal). `Lemma`, `Gender` (the lexicon's, e.g. `f`), `Times`, `TimesCorrect`, `Forms` (by text,
  then person, number, tense, mood), `First` and `Last` (`WordUse`: `Entry`, `Session`, `StoredTime`, `Sentence`) and `LastSentence`.
- `session.Learner.Verbs`: the verb rows, by infinitive (`querer`), with the same fields; each `FormRow` has `Person`, `Number`, `Tense`, `Mood` (`1`, `sg`,
  `pres`, `ind`). `session.Learner.Rows` is every row (by lemma, then `Pos`); the other parts of speech are there with their `Pos`.
- `session.Learner.Recent(n)`: the newest `n` history entries, newest first (`InputEntry`); `session.Learner.History` is all of them, oldest first.
- `session.Lessons.Record`: one `LessonRecordRow` per lesson in the bundle's `lessons.json` order: `LessonId`, `Title`, `English`, `Status`, `StepCount`,
  `Units`, `CompletedSession`, `Turns`, `HelpUses`, `DontKnowUses`, `RepeatUses`, `Phrases`.
- `LearnerSummary.Lines(learner, lessons, session, storedTime)`: the console summary the front-end prints at exit (`book: summary at exit`), as text.
- **What the book (S20) adds to the record.** `FormRow.Question` (a form used in a question is a form of its own: `¿quieres?`) and `FormRow.Last` (the
  form's last use, a `WordUse`); both are saved. `LessonDefinition.Summary` and `LessonRecordRow.Summary` (the English line of a completed lesson,
  from the optional `summary` in `game/lessons.json`), `LessonRecordRow.StepIds` (the steps in order; the book groups them into units by their prefix).
- **`game/glosses.json` (S20)**, `VerbGlosses` (`Load(bundle)`, `ForLemma`, `All`; `session.Glosses`): the English of a verb (`to want`), with optional
  `third` and `past` for the irregular ones. The lexicon has no gloss, so a verb not in the file is shown by its infinitive alone. Read by the file's
  manifest entry; a verb whose gloss does not start with `to` is a load error.
- **`EnglishText.FeedbackFor(InputEntry)`** (S20): the feedback lines for a history entry (understood, understood with a mistake, not understood, uncertain,
  "no sé", help), the same lines as `FeedbackFor(GuideTurn)`. `GuideSession.DefiniteArticle(gender, lemma)` gives a noun's article from the bundle's morphology.
- **The book item (S20).** `ItemId.Book` (29, stack 1) is given by `VoxelSession` at a new game (`Simulation.GrantItems`); a loaded save puts its own slots back.
  `btnHold` is gone from `english.json` and `EnglishText.UiKeys`.

## The day and night clock (E01, `Atmosphere/`)

The first piece of the day, night and weather series (`docs/game/weather-plan.md`, D-game-31). The folder is `Atmosphere`, not `Environment`, so the namespace does not shadow `System.Environment`.

| Type | Role |
| --- | --- |
| `DayData` | `game/day.json` (`Load(bundle)`, `Parse(json)`; `DayData.Default` when a simulation is built without game data): `cycleSeconds` (2400, 40 minutes at 60 ticks a second), the hours of a 24-hour dial at which `dawnHour` (4), `dayHour` (6), `duskHour` (20) and `nightHour` (22) begin, `startHour` (8, tick 0 of a new game) and `wakeHour` (7, where a sleep or a collapse wakes). A bundle with no `game/day.json` does not load, as one with no `needs.json` does not. `DayValidator` checks the values with the other files (the cycle positive, every hour on the dial, the four in ascending order, start and wake below 24). |
| `DayClock` | The time of day as a pure function of the tick: `(startTicks + tick + offset) mod cycleTicks`, in integer ticks, so nothing is accumulated. `HourAt(tick)` (0 to 24), `PhaseAt(tick)` (`DayPhase`: Dawn, Day, Dusk, Night) and `DaylightAt(tick)` (0 through the night, rising across dawn, 1 through the day, falling across dusk). `WakeUp(tick)` sets the offset so the hour is `wakeHour` at that tick; `Freeze(hour)` and `Unfreeze(tick)` hold the clock for tests and screenshots. Its state, `DayClockState` (the offset and the held hour), is all that is saved and digested. |
| `DayPhaseNames` | The words a trigger uses for a phase: `dawn`, `day`, `dusk`, `night`. |

`VoxelSimulation.Atmosphere.cs` (after the water system's partial) owns the clock (`Clock`, `Phase`), is started with the bundle's `DayData` (`VoxelSimulation.Create(scene, day)`), and is stepped from `Step` only to notice a change of phase: it appends a `SimulationEventKind.DayPhaseChanged` event (the new phase in `Detail`) at the tick of the boundary, once. A jump (`WakeUp()`, `FreezeClock`, `UnfreezeClock`, a restore) raises none. `VoxelSession` turns the event into the world event `day_phase` (`WorldEvents.DayPhase`, the detail `dawn`, `day`, `dusk` or `night`), which a tip, a lesson step or an objective step may wait for with a `Trigger`. `VoxelSession.ApplyNight` (a sleep and a collapse) calls `WakeUp()`; the stored time, `Day` and `Sitting` are not touched. The digest follows the flow's rule: a clock with no offset that is not held adds nothing, so a run that never slept has the digest it had before the clock; the save's `systems.atmosphere` entry (`docs/game/saves.md`) is written only when it is not that state. The clock runs on its own whether or not the player sleeps. Tests: `Atmosphere/`, `Simulation/AtmosphereSimulationTests`, `Session/DayClockSessionTests` and `Save/AtmosphereSaveTests`.

### The weather (E04, `Atmosphere/`)

The weather is saved, deterministic world state, and its kinds are rows of data, not classes and not an enum. Nothing is drawn or heard from it in E04.

| Type | Role |
| --- | --- |
| `WeatherData`, `WeatherKind` | `game/weather.json` (`Load(bundle)`, `Parse(json)`; `WeatherData.Default` is one neutral kind for a simulation built without game data, the only place in `src/` that names a kind id): the `kinds`, the `start` kind of a new game and `transitionSeconds` (45). A kind has an id (lowercase kebab-case), its channels, `minSeconds` and `maxSeconds` of a spell, weighted `next` kinds, a `morningWeight` and an optional `say` (`day` and `night` dialogue keys, carried for E07). First data: `sunny`, `cloudy`, `rain`. `WeatherValidator` checks the values with the other files. |
| `WeatherChannels` | `CloudCover`, `Sunlight`, `Fog`, `Wind` (0 to 1), `PrecipitationKind` (a style id, empty for none) and `PrecipitationIntensity`. `Blend(a, b, t)` is exact at both ends; the precipitation kind is the one blended towards while the intensity is above zero (a fading rain keeps its kind until it reaches zero). |
| `WeatherSystem` | The state (`WeatherState`: the current and previous kind, the spell's start and end tick, the spell and morning counters, a held flag). At the spell's end it draws the next kind by the weights and the new length from its range, from `WorldRng.For(seed, "weather", spellCounter)` and no other source; a game restored long after the end changes once. `BlendAt(tick)` runs 0 to 1 across the transition from the spell's start (1 for a cut). `RollMorning(tick)` draws by the morning weights (`"weather-morning"`) and cuts. `Force(id, tick)` holds a kind (a cut) until `Release(tick)`, for tests and screenshots. |
| `AtmosphereSnapshot`, `AtmosphereModel.At(clock, weather, tick)` | The hour, the phase, the daylight, the blended channels, the kind's id and its `say` key for the phase. **Consumers read the snapshot** (`VoxelSimulation.Atmosphere`) and never a kind's id to decide anything: a new weather that reuses the channels is a new row, a new phenomenon is a new channel and one consumer. A test (`NoWeatherKindIdsInSourceTests`) scans `src/` for the first data's ids. |
| `AtmosphereState` | What a save and the simulation's state keep of the day and the weather: the clock's offset and held hour, then the weather's state (absent while it is in its first spell). |

**The NPCs' answer (E07).** Asked «¿Qué tiempo hace?», an NPC says the line of the snapshot's `Say` key. The seam is data: an NPC file lists `weatherAnswers` (`["ask_weather"]`), and its agent has, for each key, a transition on the host event `<id>.<key>` (never fired, as V29's `<id>.weather`; it names the pool and keeps the lint's reach check satisfied). `GuideSession.WeatherKey` (the snapshot's `Say`, read where the turn's `GuideScene` is made, so the worker never reads the simulation) travels as `GuideScene.WeatherKey` to `NpcTurnRules.Compose`, which swaps the first reply of an `ask_weather` turn for `NpcTurnRules.WeatherReplyOf` (the transition's own «Hoy hace sol.» stands when there is no key or no pool, as in the host). The one `ask_weather` transition per state stays one rule. `GameDataValidator` requires a `say` on every kind and a pool, by that event, for every key at every NPC with `weatherAnswers`, so a kind added later cannot ship without its line (`Npc/WeatherAnswerBundleTests`, `Npc/WeatherAndWellbeingTests`). No file under `src/` names a key or a kind.

`VoxelSimulation.Atmosphere.cs` owns the weather beside the clock: `VoxelSimulation.Create(scene, day, weather, worldSeed)` (the session passes the bundle's data and the world seed; a simulation built without them uses the default data and seed 0), `Weather`, `Atmosphere`, `ForceWeather(id)` and `ReleaseWeather()`. `WakeUp()` (a sleep and a collapse) also rolls the morning's weather. `SimulationEventKind.WeatherChanged` (the new kind's index in the data) is raised once per change of kind: at the tick of a spell's end, and at the next step for a morning roll or a forced kind; a load raises none. `VoxelSession` turns it into the world event `weather_changed` (the detail is the kind's id), which a trigger may wait for. The digest and `systems.atmosphere` add nothing while the weather is in its first spell, so a new game's digest and save are as they were until the first change of weather (`docs/game/saves.md`). Tests: `Atmosphere/Weather*Tests`, `Simulation/WeatherSimulationTests`, `Session/WeatherSessionTests`, `Save/WeatherSaveTests`, `Architecture/NoWeatherKindIdsInSourceTests`.

## The land rules (Y09, `Land/`)

`Land/` holds where the player may build and where land is bought (D-game-32 (c)): `LandData` (`game/land.json`: the protected discs round scene sites, the protected squares round the `path` and `bridge` blocks, the meadows, the plot's width, depth, price and slope), `LandValidator`
(the site names are the scene's, the sizes positive, the price and margins not negative), `LandRules` (`Create(data, layout, scene)`: one byte per column, the protection and an eligible bit, built once; `CheckPlot(grid, plot)` answers a `PlotCheck`: yes, or the first `PlotFault` of protected, outside the meadows, water, tree, not ground, too steep;
`GroundOf` is a column's ground: the highest block that is not air, undergrowth, a trunk or leaves), `Plot` (west-north corner and size), `Protection` (village, camp, road), `IBuildRules` (what `VoxelSimulation.BuildRules` asks) and `LandRegister` (the rules, the pending plot and the owned one, saved as `systems.land`).
`VoxelSession.Land` is the register; `SetPendingPlot(x, z)`, `OwnPlot()` (from the pending plot, re-checked; no coins) and `IsOwned(x, z)` are what the stake and the mayor will call. Placing planks on a protected column is refused (the event `PlaceRefused`, the session's hint), a torch never is, and a plank placed by the player is taken back only inside the owned plot (`TakeBackRefused`). Tests: `Land/`, `Scripted/LandStepTests`.

## The time and weather switches, and the sound by time of day (E03)

**Sound (`Sound/`).** The birds' weight is the lowland's share times the atmosphere's daylight, and the insects' weight is the same share times one minus the daylight, so the birds sing by day, the insects at dusk and through the night, and the two swap smoothly across dawn and dusk. `AmbientBeds.At(places, listener, atmosphere, fireLit)` takes the snapshot, and `AmbientSound.Observe` and `SoundFrontend.ObserveWorld` pass it through from `SoundTickObserver`, which reads `VoxelSimulation.Atmosphere` once a tick. Nothing in `Sound/` writes to the simulation. The insects loop is six crickets, each chirping one to two times a second in short soft pulses at its own carrier (2.8 to 4.2 kHz) with silence between chirps, never a steady tone (E10).

**Rain, wind and birds in the weather (E06).** The beds read the snapshot's channels and never a kind's id. The new bed `rain` (`AmbientBeds.Rain`) has the precipitation channel's intensity as its weight (0 when the kind is empty), times
one minus 0.75 (`RainCoverCut`) of the share of the listener's cover: `ListenerShelter.CoveredShare` takes nine columns, a block apart, round the feet and counts those whose highest block is over the head (`OverheadCover.Covers`: the top face
of the highest block more than 1.5 blocks above the feet, so tall grass or a flower at the feet is not cover). The query is `VoxelGrid.HighestBlock`, the very one the rain's drops stop at (`ColumnCover` in `Vocaterra.Game`); the
tick observer hands it to `AmbientBeds.At` as a `Func<int, int, int>` (read only, and only asked when it rains), and the nine columns make the rain ease as the player walks out from under an eave (the mixer's 1.5 s cross-fade does
the rest). The wind bed's weight is its rise with height plus 0.5 (`WindChannelReach`) of the wind channel's rise above 0.25 (`WindCalm`), kept at 1: a breeze of the first data's fair weather (0.2) adds nothing, so the sound of
the sunny kind is what it was; the cloudy kind (0.4) adds 0.1 and the rain's (0.6) 0.23 in the lowland. The birds' weight is multiplied by one minus the precipitation's intensity. Every other bed is unchanged (tested). The loop
(`AmbientLoops.Rain`) is a hiss of noise from about 900 to 6500 hertz with a patter of single drops (a short bright ring at a random pitch, ten to eighty a second) and a slow swell, made seamless like the others; at 10 s of 16-bit
mono it is 441 kB in memory, and with the pre-filtered copy for under water all six loops take 5.3 MB, far below the audio budget. The mixer treats it as every bed: cross-faded over 1.5 s, ducked under a voice, silent while the
microphone records, dulled under water.

**Menu (`Menu/`).** `TestingHooks` has the held time and the held weather, each with a get and a set: `TimeHeld` and `SetTimeHeld` (an hour, or `null` for running), `WeatherHeld` and `SetWeatherHeld` (a kind's id, or `null`), and `WeatherIds` (the loaded kinds, in the data's order). `MenuRows` turns them into two rows: **Time of day**, a slider whose first position is Running (`MenuRow.Slider`'s `minKey`) and then the hours 0 to 23, and **Weather**, a picker whose first position is Running and then the ids. Each row shows what the simulation holds, so a clock held by `--time` shows its hour. `VoxelGame` wires the hooks to `FreezeClock`, `UnfreezeClock`, `ForceWeather` and `ReleaseWeather`.

**Scripted steps (`Scripted/`).** `VoxelStepKind.SetTime` (`VoxelStep.Hour`), `RunTime`, `SetWeather` (`VoxelStep.Item`, the id) and `RunWeather` run in `VoxelScript` after the model of `Pour`: they hold or release the clock and the weather from that step on, and log a line each. An id the data does not have is logged with the ids it has and changes nothing; the launch check (`LaunchOptions.CheckWeatherTargets`, called by the host once the data has loaded) refuses it before the run. The core names no kind: the ids are the bundle's.

**The F3 line.** `VoxelDebugText.Atmosphere` is the pure text of the clock and weather line: the hour as `hh:mm`, the phase, `held`; the kind, during a change the kind it leaves and the blend in percent, `held` for a held weather.

## The voxel world (S1; the route in S3; the generated valley in S13)

The prototype's world (docs/game/scenario-plan.md): a generated 384 x 384 x 128 block valley (S13), a first-person body, gathering and placing, all
headless and integer-only. It was the project `Vocaterra.Simulation` until the game's engine-free projects were merged; its folders here are
`Voxel/` (the blocks and the grid), `Mathematics/`, `Physics/`, `Simulation/`, `World/` (with `Layout/` and `Generation/`) and `Navigation/`. The
prototype route is `content/game-v1/game/scene.json` in the bundle (S3, rewritten as generator parameters in S13).

```csharp
var layout = SceneLayout.Load("content/game-v1/game/scene.json");     // or SceneLayout.Parse(json)
var scene = SceneBuilder.Build(layout, seed: 42);                          // a pure function of (layout, seed)
var sim = VoxelSimulation.Create(scene);                                   // the player stands at scene.Spawn
sim.Step([VoxelCommand.Look(Angles.FromDegrees(90), 0), VoxelCommand.MoveRelative(0, 1)]);   // one 1/60 s tick
// read: sim.Grid, sim.Player (eye, yaw, pitch), sim.Inventory, sim.Events, sim.Target (what the crosshair is on)
```

| Type | Role |
| --- | --- |
| `BlockId : ushort`, `BlockDefinition` (`Voxel/`) | air, grass, dirt, stone, sand, trunk, leaves, bush, berry bush, water, path, planks, door, table (the workbench, `mesa`), torch, stall, roof, fence, wall (0 to 18), then S1's `FireRing` 19 (the unlit fire ring), `Sign` 20 (the signpost board) and `Bridge` 21, S13's `Snow` 22 and S14's `SpruceTrunk` 23, `BirchTrunk` 24, `SpruceLeaves` 25, `BirchLeaves` 26, the undergrowth `TallGrass` 27, `Fern` 28, `Reeds` 29, `YellowFlower` 30, `WhiteFlower` 31, then `MossyStone` 32 and `Cobble` 33, and S15's `Timber` 34, `Lantern` 35, `Crate` 36, `AwningRed` 37, `AwningWhite` 38, `Canvas` 39, `Chest` 40, `Farmland` 41, `Wheat` 42, the stall's goods `Loaves` 43, `Apples` 44 and `Bottles` 45, the five signboards `SignTiendaA` 46, `SignTiendaB` 47, `SignRio` 48, `SignMontanaA` 49 and `SignMontanaB` 50, and `PostTorch` 51, the pergola's scenery torch that is not the item's torch (the kind mask holds 64 kinds, so 10 remain); append-only, hashed. Y03 appended `Bed` 52 (the inn's bed) and `Bedroll` 53 (the camp's roll of cloth): both are piles in the look, not solid (a body walks through), not decor (the ray stops on them and nothing is placed into their cell), never gathered, with no drop. A definition holds `Solid`, `Transparent`, `Liquid` (renderer flags), `GatherTicks`, `Drop`, `DropCount`, `BecomesWhenGathered` and (S14) `Decor`. Trunk (an oak, a spruce or a birch: `IsTrunk`): 60 ticks, 1 `wood`, leaves air. **Decor** is the undergrowth (`IsDecor`: tall grass, ferns, reeds and the two flowers, and from S15 wheat and the stall's goods; `IsUndergrowth` is the five the scene sows): not solid, never gathered, and the block ray skips it like air, so the crosshair looks through the grass at the berry bush or the trunk behind it and never stops on it; a block placed into its cell takes its place. Berry bush: 45 ticks, 1 `berry`, leaves a plain bush. `BlockDefinition.PlacedBy(item)` is the item to block map (wood to planks, torch, table, door, stone, dirt, sand) |
| `VoxelGrid`, `VoxelChunk`, `BlockPos`, `VoxelChunkCoord`, `BlockFace` (`Voxel/`) | 16 x 16 x 16 chunks, bounded; `GetBlock` (air outside), `IsSolid` (solid outside), `HighestBlock(x, z)`; `Chunks` are indexed `(cy * ChunksZ + cz) * ChunksX + cx`; a chunk stores `(localY * 16 + localZ) * 16 + localX` |
| `SceneLayout`, `SceneBuilder`, `Scene` (`World/`) | the layout file (`World/Layout/`, read by `SceneLayoutParser`), the deterministic builder (`World/Generation/`, with `TerrainGenerator`), and the result: `Grid`, `Spawn` (cell, yaw and, S14, pitch), `Npcs`, `Points`, `TreeCount`, `PlantCount` |
| `PlayerBody`, `VoxelPhysics` (`Physics/`), `Angles` (`Mathematics/`) | the body and its constants, and the integer angle table |
| `VoxelCommand`, `VoxelCommandKind` (`Simulation/`) | `Move(dx, dz)`, `MoveRelative(strafe, forward)`, `Look(yaw, pitch)`, `Jump`, `Interact(x, y, z)`, `Place(item, x, y, z)`, `SelectSlot(slot)`, `Craft(index)` |
| `VoxelSimulation` (`Simulation/`) | `Create(scene)`, `Step(commands)`, `Tick`, `Player`, `Inventory`, `Events`, `Target`, `GatherTarget`/`GatherProgress`, `SetRecipes`, `CanCraft`/`Craft`, `StationInReach`, `GrantItems`, `TeleportTo(feetCell)`, `ComputeStateDigest()` |
| `BlockRay`, `BlockRayHit`, `LineOfSight` (`Voxel/`) | the 3D DDA: `Cast(grid, body)` or `Cast(grid, eye, yaw, pitch, reach)`; the first block within 4 blocks that is not air and not undergrowth (S14), its face and the cell in front of the face; `LineOfSight.Blocked` (whether a solid block stands between the eye and an NPC) |
| `Beacon`, `WalkPlanner` (`Navigation/`) | the point the player is sent to, and the scripted walk's A* search over the cells a body can stand in |

Shared with the rest of the game: `WorldRng` (in `Mathematics/`, the scene's tree scatter), `Inventory` and `ItemDefinition` (three item kinds:
`Wood`, `Torch` and `Berry`; the numbers of the 2D kinds retired at S4 are not reused), `Recipe` and `CraftingRules` (a recipe's station is a
`BlockId`, the workbench `Table`), and `SimulationEvent` (`X, Y, Z, Detail, Amount` and the kinds `PlayerJumped`, `PlayerLanded`, `ItemCrafted`,
`GatherProgress` 5, `BlockRemoved` 6, `ItemPickedUp` 7, `BlockPlaced` 8). The 2D world (tiles, the generator, the light map, the 2D physics and the
2D simulation) was removed in S4; its last commit is tagged `2d-slice-p7`.

### Coordinates, units, angles

- **Axes:** x east, y up, z south, so row 0 of a top-down map is the north edge. Block (x, y, z) fills the cube from (x, y, z) to (x + 1, y + 1, z + 1).
- **Units:** the body is in integer units of 1/4096 block (`VoxelPhysics.UnitsPerBlock`). The box is 2458 x 7373 x 2458 units
  (0.6 x 1.8 x 0.6 blocks to the nearest unit), the eye is 6636 units above the feet (1.62). `PlayerBody.MinX/MinY/MinZ` is the box's minimum
  corner, `CenterX/FeetY/CenterZ` the middle of the feet, `EyeX/EyeY/EyeZ` the camera. Standing on a block, `FeetY` is a multiple of 4096.
- **Time:** 60 ticks per second. Walking is 294 units per tick (4.3 blocks per second), gravity 36 units per tick squared, a jump starts at
  640 (rises 1.3 blocks), the terminal fall speed is 3000. A walking body steps up one block by itself (`PlayerBody.SteppedUp` is set so a
  camera can ease the lift); two blocks need more than a jump gives.
- **Angles:** whole numbers of 1/4096 turn (`Angles.StepsPerTurn`). **Yaw 0 faces north (-z), 1024 east, 2048 south, 3072 west** (a compass
  bearing, clockwise from above); `Angles.FromDegrees(90)` is east. **Pitch** 0 is level, positive looks up, clamped to +-1023. The look
  direction is (cos p sin y, sin p, -cos p cos y). Sine and cosine are fixed point (65536 is 1.0) from a quarter-turn table built in type
  initialisation by an integer Taylor series (Q30, 64-bit multiplies), the other quarters by symmetry: no `Math.Sin`, no platform
  dependence. A test compares the table with the library sine to one unit in the last place.
- **Commands:** `Move(dx, dz)` walks along the world axes, `MoveRelative(strafe, forward)` along the facing (a front-end with mouse look
  sends this one); both are levels, summed over the tick, and diagonals are scaled by 46341 / 65536 so they are not faster. `Look` is
  absolute. `Interact` is held: progress builds while the target stays the same, is gatherable, is within 4 blocks of the eye (the nearest
  point of the block) and its drop fits; a tick without it resets. `Place(item, x, y, z)` puts one item of that kind, from anywhere in the
  inventory, in an empty cell in reach that touches another block and, if the new block is solid, does not overlap the body. The sim does
  not check line of sight: a front-end picks its target with `BlockRay` (`sim.Target`), and everything that ray can hit passes the reach check.
- **Order inside a tick:** selection and look, gathering, placing, crafting, then the walk with the jump and the physics.

### The layout file (S13: parameters, not blocks)

JSON (comments and trailing commas allowed, `schemaVersion` 2); `SceneLayout.Parse` documents the shape. The file lists no block. It holds the
generator's parameters and the named places:

- `size` {x, y, z} (multiples of 16: 384 x 128 x 384 is 24 x 8 x 24 chunks), `waterLevel` (the lake's surface).
- `terrain`: `relief`, a coarse grid with one base-32 digit per chunk corner (the height divided by 4; the macro shape: the mountains along the
  north and the west, the foothills, the valley floor at 36), and `valley`, `rockLine` (bare stone above 85), `snowLine` (snow above 100 on
  gentle ground), `terrace` (the step of the cliff faces) and `ceiling`.
- `sites`: named places at **fixed** positions (`camp.player`, `camp.guide`, `village`, `bridge.west`, `river`, `waterfall`, `lake`, `trail`...). A
  site with a `radius` levels the ground to `y` (or the height the land has there) and slopes it into the land over `blend`; every site is also a named
  point. `waterfall` {site = the lip, top, width, plateau, pool}, `lake` {site, rx, rz, depth} and `river` {halfWidth, depth, bank, path: waypoints from the
  pool to the lake}. `paths` carve a trail between two sites: `mode: grade` changes the height evenly from the first site's to the last's (a switchback
  down the mountainside), `mode: follow` follows the valley floor, smoothed so it never steps more than a block.
- `trees` (S14) also takes `spruceFrom`, `birch`, `birchAway`, the shore meadow (`shoreClear`, `shoreFade`, `shoreKeep`), `clearings` (round meadows at a site: `site`, `radius`, `fade`, `keep`) and `vistas` (a sight cone from a lookout site: `site`, `yaw`, `half`, `radius`, `down`); `undergrowth` takes `grass`, `fern`, `flowers`, `reeds` and `routeMargin` (percent of the columns); the `spawn` takes a `pitch` in degrees (negative looks down).
- `stamps` (small block drawings, layers of rows with a legend, turned to face a side) and `props` placed at a site plus an offset (`dx`, `dz`):
  `workbench`, `fire_ring`, `stall` (the counter side is `facing`: awning, goods and crates, S15), `house` (timber frame, stepped roof, chimney), `signpost` (`facing` is the side it reads from, `south` or `west`, `stamp` lists its boards: `tienda`, `rio`, `montana`), `lamp`, `block` (a stack of one kind, `block` and `height`), `pergola`, `field`, `fence_line`, `bridge`, `paving`, `stamp`, `bush`, `berry_bush`, `tree`; a
  `name` on a prop registers a named point. A prop on uneven or wet ground is rejected with a message (`SceneLayoutException`), so the layout
  cannot silently build a floating house. `npcs`, `spawn` (a site and an offset, and a facing in degrees: 135 is south-east), `points` and `trees`.

`TerrainGenerator` (integers only: no floating point, no `Math.Sqrt`) makes the ground in this order: the macro relief (Catmull-Rom between the
corners, clamped) plus the seed's value noise (`TerrainNoise`, four octaves, damped to a few percent near the river, the paths and the sites, and
scaled up on the mountains) and stepped terraces; the waterfall's plateau; the river (a smooth curve; its surface is the lowest the ground has been, less
two blocks, from the source to each point, so the water never runs uphill) and the pool; the lake; the graded trail and the road; and last the sites'
level ground. `SceneBuilder` fills the grid column by column (stone, three layers of dirt or sand, the top block by height and slope: sand by the water,
stone above the rock line or on a cliff, snow above the snow line), lays the water, builds the props, and plants the forest.

The forest is the only random draw besides the noise: one tree is tried in every `cell` x `cell` square, at a jittered place from
`WorldRng.For(seed, "trees")` (the draws are made for every square, so the stream stays in step), kept with a `chance` percent (less with height, none
above `treeLine`, a few clearings where the slow noise is low) unless it is on a cliff, on sand or water, within `margin` columns of the route (path
surface, a site's level ground, a prop, the spawn, an NPC, a point) or within `spacing` of another trunk. **S14** adds what the look needs, without touching the S13 draws (the first stream is the one S13 drew, so no trunk moved; a second stream, `treeshape`, picks each tried tree's species and the ragged rim of its crown): oaks (big rounded crowns, three columns wide at the widest) in the valley, spruces (a trunk five blocks longer and a tiered cone) from `spruceFrom` up and all of them 20 blocks higher, birches (a tenth of the lowland, never within `birchAway` of a site, so the guide's clearing keeps its oaks); the share of trees kept falls to `shoreKeep` percent within `shoreClear` blocks of the water and within a clearing's radius (the banks and the village are meadow), and a tree inside a vista's cone must stand under the sight line `down` degrees below the lookout's eye (so the ledge's view of the valley is open). The terrain's relief is looked up at a point moved by slow noise (damped near the sites, the paths and the river), so the valley's walls are not the chunk grid's straight lines; low rock is `MossyStone`.

The **undergrowth** is planted after the forest by `PlantUndergrowth` from the noise of the pass `undergrowth` (keyed from `WorldRng`): tall grass and ferns (more ferns under the trees) on open grass in patches of thicker and thinner cover, flowers in meadows of their own (mostly one colour), reeds on the banks within two columns of the water; nothing grows on a path or within `routeMargin` columns of a prop, a point, an NPC or the spawn, and a third as much one column further. In front of a lookout (inside its cone, 44 blocks out) only flowers grow, drawn low, so the grass does not stand between the player and the valley. **A different seed changes the terrain
between the sites, the river's surface, the forest and the undergrowth, and never moves a site, a point, an NPC or the spawn**; the ground of each site is the same
block for block, the undergrowth on its meadow apart (a test checks it for two seeds).

Cost: the 384 x 384 x 128 world builds in about 0.4 s and its first digest takes about 0.1 s on the development machine (a test holds the two
under three seconds). `VoxelChunk` keeps a count of its non-air blocks and a mask of the block kinds it holds, so a search or a renderer passes
empty chunks by (`VoxelGrid.BlocksOfKind`, `VoxelGrid.HighestBlock`).

### Digests (for the renderer and for saves)

`VoxelGrid.ComputeDigest()` is the SHA-256 of the three sizes (int32 little-endian) and the 32-byte digest of every chunk in `Chunks` order;
a chunk digest is the SHA-256 of its 4096 block ids as 16-bit little-endian. Both are cached and every real edit invalidates them. To
**re-mesh**, compare `VoxelChunk.Version` (it increases on every edit of that chunk) with the value you meshed, or `VoxelGrid.EditCount` for
"anything changed"; the chunk to rebuild is `VoxelChunkCoord.FromBlock(x, y, z)` of an event's position (and the neighbour chunk when the
edit is on a chunk border, because the faces there depend on it). `VoxelSimulation.ComputeStateDigest()` adds the tick, the body, the
selection, the gather progress and the inventory.

### Tests

`tests/Vocaterra.Game.Core.Tests/`, in folders that mirror the source: `World/Generation/SceneTests` (the seed-42 golden, the load cost, identical builds, another seed moving no site, the mountain heights, the
river running downhill, the walking times, trees never on or over the route for three seeds, trunk spacing, the named points and NPCs, a pathfinder plus the
real body walking from the camp over the bridge to the far bank, rejected layouts), `Voxel/VoxelGridTests` (chunks, bounds, the digest changing on
every edit and only in the edited chunk), `Mathematics/AnglesTests`, `Physics/PlayerBodyTests` (landing, walls, the one-block step and the two-block wall, jumps,
edges, diagonal speed, relative movement, and the never-inside-a-solid property over 24 random worlds and 6 route seeds), `Voxel/BlockRayTests`
(known poses and faces, the reach limit to the unit, a property test against a fine march) and `Simulation/GatheringAndPlacingTests` (trunk, berry bush,
reach, a full inventory, placing rules, the torch at the workbench and the fire ring). The shared tests: `InventoryTests` (forty slots, the
hotbar, stacks of 99, what does not fit, removals), `CraftingTests` (the rule: ingredients, the station, the room for the output), `WorldRngTests`
and `ArchitectureRulesTests`, which scans the sources and the project file for engine types, the clock, `System.Random` and any reference beyond
the language core. Print the route:
`dotnet test tests/Vocaterra.Game.Core.Tests --filter "FullyQualifiedName~World.Generation" --logger "console;verbosity=detailed"`.
