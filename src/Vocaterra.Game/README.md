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
| `GuideEvents` | the host event names the guide's state machine raises: `guide.point`, `guide.identify`, `guide.help`, `agent.greet`, `agent.farewell` |

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
the guide's bundle content and the architecture rules.
