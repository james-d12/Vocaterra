# Vocaterra.Game

The engine-agnostic game layer: one running game, the conversation with the guide and the facts about the world she speaks of. It references
`Vocaterra.Simulation` by project and the language core only as the `Vocaterra.Nlp.*` packages (never its projects or source), and it knows no
engine (plan section 3, rules 1 and 3). `Vocaterra.MonoGame` and the console host build on it.

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
| `GameSession` | owns the `GameSimulation`, the bundle, the world seed, the stored time, the guide (`GuideNpc`), the conversation (`GuideService`), `WorldFacts` and the arrow (`PointerMarker`). `Step`, `Say`, `DismissPointer`, `StoredTime`, `PlayerTile`, `IsNearGuide`, `OpeningLine` |
| `GuideService` | one typed turn: `Say(line, scene, now, hostStateHash)` evaluates the line on the prompt `guide-open` with the core's scripted recognizer, carries the context with `ConversationContext.Next`, and returns a `GuideTurn`. `Context`, `GuideState`, `OpeningLine`, `WarmUp()` |
| `GuideTurn` | the verdict in plain values (`Intent`, `Communicated`, `Correctness`, `Errors`, `Summary`), `Feedback` (the recast or hint), `Replies`, `Repair`, `AgentState`, `HostEvents`, `WorldLine` (the game's own line), `Pointer` (`GuidePointer`: place, target tile, blocks, direction), `IdentifiedNoun`, and `GuideLines` in the order shown. No time in it, so equal inputs give an equal turn |
| `WorldFacts` | `Nearest(from, kinds, radius)`, `NearestOfNoun(from, "árbol")`, `DirectionPhrase(dx, dy)`, `NumberInWords(n)`, `TilesOfNoun`, `NounOfTile`, `CenterTile(player)`. Integer arithmetic only |
| `GuideNpc` | `Place(world)`: a grass column 8 to 28 columns from the spawn with headroom, clear of trees if it can; `IsNear(player)` |
| `TypedUtteranceEvaluator` | the call sequence over the packages for one typed line (scripted recognizer, fake embeddings, silent synthesizer); the console host uses it too. `EvaluateCountingThisUtterance` is the CR-012 workaround |
| `GameBundle` | `Locate()` and `Load()` for `content/game-v1` (`VOCATERRA_CONTENT_DIR` overrides) |
| `GuideEvents` | the host event names the guide's state machine raises: `guide.point`, `guide.identify`, `guide.help`, `guide.name`, `agent.greet`, `agent.farewell` |
| `NounCatalog` | the noun catalogue, `game/nouns.json` in the bundle (read after the load, CR-007): `ForLemma`, `ForTile`, `IsColourless`, `Nouns` (lemma, gender, `el`/`un`, plural, tiles, items), `CatalogueNounsOf(surface)` (the readings that are catalogue nouns, CR-002) |
| `WordProgress` | the learner's counts per catalogue noun (`Records`, `RecordOf`, `SaturationOf(lemma)` and `SaturationOf(TileId)`, `KnownLemmas`, `MarkRead`), the play `Session`, and `RecordTurn(turn, kind, rules, now)`, which counts one guide turn: heard, spoken, spoken correctly, credit (at most one per noun per turn, and only when its gap has passed), known. Also `DueWords(now, rules)` and `SecondsUntilNextCredit(lemma, now, rules)` |
| `WordRecord`, `WordStage` | one noun's counts (`Known`, `Saturation` in thirds, `Stage`: Unseen, Encountered, Learning, Known) and the time and play session of its last credited use (`LastCreditedAt`, `LastCreditedSession`) |
| `CreditRules` | the credit per kind of input (`Typed`, `Spoken`) and the gaps between credited uses (`Gaps`, default 10 min then 1 h; `GapAfter(n)`): the one place P5 and the owner's tuning change (D-game-6) |
| `CreditOutcome`, `WordCredit`, `DueWord` | what a turn did to a noun (`Credited`, `TooEarly` with the seconds left, `Wrong`, `NamedWrong`) and a word due for review (`Lemma`, `OverdueSeconds`) |
| `GuideReview` | the review question in progress: the due word, the pointed tile and the question; `GameSession.Review`, `BeginTalk()`, `EndReview()`, `SkipTime(seconds)` |

## Words (P4): the noun catalogue and the D-game-6 rule

- `content/game-v1/game/nouns.json` names the noun of each tile kind (`trunk` and `leaves` are `árbol`, `dirt` is `tierra`, `grass` is `hierba`,
  `stone` is `piedra`) and lists `air` as colourless; items come with P6. A test checks that every tile is a noun or colourless and every noun has
  a noun reading and a gender in the bundle's lexicon. The core supplies the article and the plural (`Morphology.Article`, `Pluralize`).
- A catalogue noun is **known** after three credited correct uses in three separate turns, spaced in time (below) (`WordProgress.UsesToKnow`). Each correct use adds credit in
  units (`UnitsPerUse` = 6, so `UnitsToKnow` = 18); saturation is units over 18, so a typed noun is 1/3, 2/3, then 1. Nothing decays.
- A use is **correct** when no verdict error's span overlaps the word or the article right before it. The uses of a turn are the catalogue nouns in
  the verdict's slot values and in the turn's normalized tokens (`CatalogueNounsOf`, the CR-002 workaround). A turn that was not communicated
  counts nothing for use (it still counts as heard, when the guide's lines say the word).
- `name_object` (`es un árbol`, `eso es una piedra`) names the cursor's tile: the guide says `¡Eso es! Es un árbol.` when the name is that tile's
  noun and no error was found, and `No, es un árbol.` otherwise (the game checks it, CR-014; the result is `GuideTurn.Naming`: `NameCheck`). **A
  name the guide marks wrong earns no colour for the noun named** (P4b; the orchestrator's default, the owner to confirm); it still counts as said.
- `GameSession.Say` counts every turn (`Words`, with `CreditRules.Default` for typed input and the stored time of the turn) and keeps what the last
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
2. `GuideService.Say` evaluates the line with the carried context (`TypedUtteranceEvaluator.EvaluateCountingThisUtterance`: twice, so the repeat
   ladders count this utterance, CR-012), then `utterance.Next(result, now)` carries the guide's state, the learner model and the repeat counts.
3. The response lines split into the correction (`IsRecast` or a `feedback.*` pool) and the guide's reply (the agent's pool). The host events come
   from `Decision.Agent.Actions`.
4. For `guide.point` the game finds the nearest tile of the noun in the verdict's slot `place` and renders `guide.point.found` ("El árbol está a la
   derecha, a doce bloques."); for `guide.identify` it names the cursor tile (`guide.identify.found`); for `guide.help` it picks `guide.help.ask`
   or, with an arrow out, `guide.help.walk`.

The game's own line is **outside the core's replay record** (CR-003, open): the core's templates see only the verdict's slots, so the game renders
the line itself with `bundle.TemplateEngine` from the bundle's dialogue pools after the core is done. The variant is drawn from the core's
`SeededRng.ForInteraction(worldSeed, "guide.host", counter)`, so it is a pure function of the seed, the world and the interaction counter.

## Why the guide is here and not in the simulation

The guide's position is a pure function of the generated world (`GuideNpc.Place`); she does not move or collide yet; and her conversation state
belongs to the agent state machine in the context. Putting her in the simulation would add a record to its state digest and to the saves for
nothing that ticks. When characters move, collide or are saved (the innkeeper, G3), they become entities of the simulation.

## Rules this layer keeps

Packages only; no engine type; no `System.Random`, clock, `Stopwatch` or `DateTime` (a test scans the sources); the stored time and the world seed
come from the game; dictionaries that reach output are ordinal. The tests are in `tests/Vocaterra.Game.Tests`: guide turns on seed 42 with the
fixed stored time (verdict, state, host event, target tile, world line, accent-less input, the repair path, determinism), `WorldFacts` tie-breaks,
the guide's bundle content and the architecture rules. P4: `NounCatalog` (coverage of every tile, genders, articles and plurals, the
readings of an unaccented form, the parse errors), `WordProgress` (three uses in three turns, thirds, one credit per turn, the article rule,
the not-communicated turn, heard, read, the credit setting, determinism) and the real core on `es un árbol`, `la árbol` and `es un arbol`.
