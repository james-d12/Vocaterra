> **Historical (R1, the 2D era).** This document describes the 2D Terraria-inspired slice (P1 to P7), retired by the prototype brief (D-game-8, 2026-10-21). The current design is `docs/PROTOTYPE_BRIEF.md`; its plan is `docs/scenario-plan.md`.

# Prototype research: a Terraria-inspired slice on the language core (R1)

Research milestone R1, 2026-10-21. The question: how to build a small prototype that proves (1) the language core works inside a real game
loop and (2) a Terraria-like 2D sandbox where Spanish is learnt by talking can be fun. The owner's scope: a guide NPC answering questions,
directions and help requests by text and speech; mining, building and movement; a forest starter biome; map generation with caves; a word
progress system; nouns drawn in colour once known and in greyscale before (the Noun Town idea); an inventory; MonoGame now, perhaps Godot
.NET later.

Each section answers one question of the brief and ends with a recommendation. The milestone table and the proposed core requests are also in
[prototype-plan.md](../archive/game/research/prototype-plan.md), in a form the orchestrator can paste.

**How the facts were checked.** Core facts were read from the core's documents and source at the main checkout's submodule
(`external/Vocaterra.NLP`, gitlink `db39999`): `docs/api.md`, `docs/pipeline.md`, `docs/lexicon.md`, `docs/PACKAGING.md`, `docs/decisions.md`,
`tools.md`, `high-level-docs.md`, `docs/research/2026-10-20-scaling-approaches.md`, `models/manifest.json` and the public types under `src/`. Nothing was
built or run. Web facts carry their URL; anything not confirmed from a primary source is marked **unverified**.

Contents:

1. [Core integration: what the packages offer, feature by feature, and the gaps](#1-core-integration)
2. [The guide NPC](#2-the-guide-npc)
3. [Architecture for an engine move](#3-architecture-for-an-engine-move)
4. [The world](#4-the-world)
5. [Mechanics and the minimum fun loop](#5-mechanics-and-the-minimum-fun-loop)
6. [The language progress system](#6-the-language-progress-system)
7. [The colour and greyscale mechanic](#7-the-colour-and-greyscale-mechanic)
8. [Assets and licences](#8-assets-and-licences)
9. [Milestone plan](#9-milestone-plan)
10. [Risks and open questions](#10-risks-and-open-questions)
11. [Sources](#11-sources)

## 1. Core integration

### 1.1 What the 0.1.0 packages offer today

The game consumes eleven library packages, `Vocaterra.Nlp.*` 0.1.0, all `net10.0` (core `Directory.Build.props`). The console host references
seven of them (`Asr`, `Content`, `Core`, `Decision`, `Language`, `Pipeline`, `Response`); the prototype adds `Vocaterra.Nlp.Audio` for
push-to-talk. The table lists, for each capability the brief names, the public call and its state.

| Capability | Public call (package) | State at `db39999` |
| --- | --- | --- |
| Load content | `ContentLoader.Load(dir)` returns `ContentBundle` with `Prompts`, `Agents`, `Intents`, `Dialogue`, `Effects`, `AnswerSets`, `Lexicon`, `Morphology`, `TemplateEngine`, `Cefr`, `ContentHash` (Content) | ready; about 220 to 270 ms per load with the 3 000-lemma lexicon (`lexicon.md`), so load once at start-up |
| Text evaluation | `new UtterancePipeline(new PipelineServices(new ScriptedWordRecognizer(asr), null, embeddings, synth, bundle)).Evaluate(clip, prompt, context)` returns `PipelineResult` (Pipeline) | ready; this is what `src/Vocaterra.Host` does; D0, under 5 ms per utterance as a target (`high-level-docs.md`) |
| Verdict | `result.Verdict`: `Intent`, `Slots` (slot name to lemma), `Communicated`, `Correctness` 0 to 1, `Errors` (tag, span in the normalized text, expected, actual), `Confidence`, `ResolvedBy`, `Versions` (Core) | ready |
| Speech capture front-end | `PushToTalkSession.Begin()`, `Append(ReadOnlySpan<short>)` of 16 kHz mono samples, `End()` returns a gain-normalized, VAD-trimmed `PcmClip`; `Resampler.To16k(short[] mono, int sourceRate)`; `Downmixer.ToMono(WavData)` (Audio) | ready; device capture is the engine's job (ADR 0009) |
| Speech recognition, constrained | `VoskWordRecognizer.FromStore(ModelStore.FromEnvironment(), "vosk-model-small-es-0.42")`: grammar from the prompt's answer set, or free vocabulary for an open prompt (Asr) | ready: the only model fetched and fully pinned (`tools.md`; 58 MB in the core's models directory) |
| Speech recognition, open | `SherpaOnnxOfflineRecognizer.FromStore(store, "sherpa-onnx-nemo-parakeet-tdt-0.6b-v3-int8")` (Asr) | code ready, model **not pinned**: file hashes are `null` in `models/manifest.json`, so `ModelStore.ModelHash` throws; the constructor `new SherpaOnnxOfflineRecognizer(dir)` hashes the files itself. 670 MB, about 1.2 s for a 3 s clip (core research, unverified CPU) |
| Text to speech | `pipeline.Synthesize(result.Response, voiceId)` returns `AudioBuffer` per line (`short[]`, sample rate); `new KokoroSynthesizer(dir, speakerId)`; `CachedSynthesizer`; `StaticLineStore` for pre-rendered lines by line id; `SilentSynthesizer` (Response) | code ready, model **not pinned** (`kokoro-multi-lang-v1_0`, 350 MB archive); one voice per instance; carries espeak-ng GPL-3.0 data (core D2: private builds only) |
| Intents and slots | bundle `intents/intents.json`: slot grammars (`[NOUN:place]`, `[VERB:lemma]`, `[WORD:name]`, literals, optional and alternative groups), keyword rules, examples; resolution grammar, then rules, then embeddings (Language) | ready; free-form questions outside the authored intents resolve to `unknown` |
| Dialogue states | `bundle.Agents[id]` (`AgentStateMachine`): states, transitions on intent, error tags and `communicated`; actions with a dialogue pool and an opaque `hostEvent`; `result.Decision.Agent` (`AgentStepResult`: next state, actions) (Decision) | ready; no condition on slot values |
| Reply text | `result.Response.Lines` (`ResponseLine`: line id, text, is-recast), rendered from the bundle's pools with the verdict's slots; feedback recasts, hints and corrections by repeat count | ready; templates see only the verdict's slots (gap CR-003) |
| Effects | `result.Effects` (`ResolvedEffect`: id, opaque kind, value from the correctness curve) | ready |
| Repair | `result.Repair` (`RepairStep`: ask again, slow repeat, offer choices, give up) when not communicated | ready |
| Learner model | `LearnerModel` (BKT): `result.Decision.Learner`, `PKnown(skillId)`, `Skills` (skill id, p-known, attempts, errors). The pipeline updates `intent.<id>` and one skill per error family (`conj`, `noun`, `gender`, ...) | ready; the host persists it. Skills are per intent and per error family, not per word |
| Scheduling | `Fsrs.Review(card, rating, now)`, `Fsrs.Retrievability(card, now)`, `ReviewCard.New(itemId, now)`, `RatingMapper`, `ContentPicker.Next` (Decision) | ready; the host owns the cards |
| Repeat counts | `RepeatCounts.Next(before, verdict)`; `context.Next(result, now)` builds the next context (Pipeline) | ready since core M19d; the host still has its own copy until G1 |
| Lexicon | `bundle.Lexicon.Lookup(form)` gives every reading (`LexicalFeatures`: lemma, part of speech, person, number, tense, mood, gender), accent-insensitive as a fallback (`IsAccentInsensitive`); `EntriesForLemma`, `ContainsLemma`, `SpellingsIgnoringAccents` (Language) | ready; 3 372 lemmas, 41 078 entries |
| Articles and plurals | `bundle.Morphology.Article(Definiteness.Definite, gender, number, lemma)` (handles `el agua`, `el hacha`), `Pluralize`, `InflectNoun`, `AgreeAdjective`, `Conjugate` | ready |
| CEFR level | `bundle.Cefr["a1"]`, `bundle.Cefr["a2"]`: lemma sets; no level per lexicon entry (`lexicon.md`, known limits) | ready for A1/A2 membership |
| Analysis of the utterance | not on `PipelineResult`. `TextNormalizer.Normalize` and `new LinguisticAnalyzer(lexicon, morphology).Analyze(text, grammars)` are public | gap CR-002 |
| Matched answer | not on `PipelineResult` (`MatchResult` is internal to the pipeline's flow; `AnswerMatcher` is public) | gap CR-004 |
| Replay | `pipeline.Trace(...)`, `ReplayLog`, `ReplayEnvelope.From`, `Replayer.Replay` | ready; records the recognizer output, so a spoken turn can be replayed without audio |

### 1.2 Each prototype feature mapped to the calls

| Feature | Calls, in order | Game-owned part |
| --- | --- | --- |
| Guide, typed question | `AsrResult` from the typed text, `ScriptedWordRecognizer`, `Evaluate(PcmClip.FromSamples([]), bundle.Prompts["guide-open"], context)`; read `Verdict`, `Decision.Agent.Actions[*].HostEvent`, `Response.Lines`, `Repair`; `context.Next(result, now)` | the prompt choice, mapping `guide.directions`, `guide.identify`, `guide.help` to world queries, the extra host line (section 2) |
| Guide, spoken question | front-end capture at the device rate, `Resampler.To16k`, `PushToTalkSession.Append`/`End`, `VoskWordRecognizer` in `PipelineServices`, `pipeline.Trace(clip, prompt, context)` on a worker thread (the trace's `Asr.Transcript` is shown) | the microphone, the key, the worker thread, the latency budget |
| Guide speaks | `pipeline.Synthesize(result.Response, "guide")` or `synthesizer.Synthesize(new SynthesisRequest(text, "guide", null))` for the host line; `AudioBuffer` to the engine's audio output | playback (MonoGame `DynamicSoundEffectInstance`; section 2.4) |
| Directions | the `ask_directions` intent's slot `place` holds a lemma (`árbol`, `cueva`); the noun catalogue maps it to tile kinds | the world query and the Spanish direction words |
| Help | `ask_help` intent; the agent's `hostEvent` `guide.help` | the hint chosen from game state (no wood yet: "Corta un árbol con el hacha.") |
| "What is this?" | `ask_what` intent (no slot) | the cursor's target, its noun, the answer line |
| Name an object (the colour mechanic) | a `name_object` intent with grammars `[ART]? [NOUN:object]` and `es [ART]? [NOUN:object]`; the slot is the lemma, agreement errors (`gender.agree`, `noun.number`) come with it | comparing the lemma with the target's, the progress update |
| Word counts | `Verdict.Slots` (noun lemmas) and, until CR-002, the normalized tokens read with `bundle.Lexicon.Lookup`; `Verdict.Errors` spans to exclude words used wrongly | the counts, the known rule, persistence |
| Noun facts for tiles and items | `bundle.Lexicon.Lookup(lemma)` (noun reading, gender), `bundle.Morphology.Article(...)`, `Pluralize`, `bundle.Cefr` | the catalogue that maps a tile or item id to a lemma |
| Learner model and repeat counts | `ConversationContext.Create` at start, `context.Next(result, now)` after each turn; persist `Learner` and `RepeatCountsByTag` | the save file; the stored time |

### 1.3 Gaps, as proposed rows for `docs/CORE_REQUESTS.md`

None of the gaps blocks a prototype milestone except speech out: each has a workaround through public calls of 0.1.0. The rows are written in
the core's terms (call or field, then the game reason); [prototype-plan.md](../archive/game/research/prototype-plan.md) has them in the requests file's table shape.

1. **CR-002, the analysis on the result (should).** `PipelineResult` or `PipelineTrace` carries the normalized tokens, their spans and the
   reading the resolver chose (lemma, part of speech, gender, number), with names (`[WORD]`) and unknown words marked. Reason: word-level
   spoken counts and the colour rule. Workaround: the game runs `TextNormalizer.Normalize(text, new NormalizationOptions(ConvertIndefiniteArticles: false))`
   (the pipeline's own option) and looks each token up; it counts only lemmas in its noun catalogue, which removes most homograph risk. It can
   still disagree with the pipeline, because the reading resolver's grammar step is not reproduced.
2. **CR-003, host-supplied slot values in response planning (should).** An optional `SlotMap` of host facts on `Evaluate` (or on
   `ConversationContext`) that reply and feedback templates can reference, recorded in the replay record and `HostStateHash`. Reason: the
   guide's answers name world facts no verdict holds. Workaround: the game picks a pool (`bundle.Dialogue.TryGetPoolById`), draws a variant
   with `SeededRng.ForInteraction(worldSeed, "guide", counter)` and renders it with `bundle.TemplateEngine.Render(template, SlotMap.From(...))`.
   That line is outside the replay record, which is acceptable for a prototype.
3. **CR-004, the answer match on the result (could).** `MatchResult` (answer id, type, distance, likely pronunciation) on `PipelineResult`.
   Reason: quiz prompts ("say what this is") must know which answer was given. Workaround: the `name_object` intent above.
4. **CR-005, host-named skills in the learner update (could).** Let the host add skill ids (for example `word.árbol`) to an utterance's BKT
   step, or a call that steps named skills alone. Reason: per-noun mastery after the prototype. `LearnerModel.Update(verdict, skills)` is
   public, but it always re-applies the verdict's error families, so a second call for word skills counts each error twice. The prototype uses
   plain counts (section 6), so this waits.
5. **CR-006, speech-out readiness (should).** Pin the Kokoro and Parakeet file hashes in `models/manifest.json`, add
   `KokoroSynthesizer.FromStore`, and document Kokoro's Spanish speaker ids. Reason: the guide speaks (P8); open voice prompts later. Today only
   Vosk is pinned (`tools.md`). Private builds only under core D2.
6. **CR-007, host-owned bundle files (should).** Confirm and document that a bundle may hold files the loader does not parse (such as
   `game/nouns.json`), hashed by the manifest and listed in `provenance.json`, and expose their verified bytes on `ContentBundle`. Reading
   `ContentLoader.cs` at `db39999`, an extra listed file appears to be hashed and accepted (the loader rejects only unlisted or missing files),
   but no call returns its bytes. Reason: the noun catalogue should be inside the content hash, so saves and replays name it. Workaround: read
   the file from disk after `Load`. **Unverified** until P4 runs `scripts/content.sh validate` on such a file.
7. **CR-008, a threading and load-cost contract (should, documentation).** Whether one `UtterancePipeline` with Vosk or sherpa-onnx may be
   called on a worker thread while `Synthesize` runs on another, whether recognizers may be shared, and when models load. Reason: the game
   must never block its frame. `pipeline.md` says the pipeline keeps no state between utterances, but says nothing of concurrent calls.
8. **CR-009, a `net8.0` target (conditional).** Only if the Godot spike (P10) shows that Godot's .NET SDK cannot build against `net10.0`
   libraries. See section 3.4.

Not requested, with the reason: free-form question answering (the prototype authors its intents; the core's M23 intent classifier and M25
optional tutor are the long-term path, and both are open there); lemma, gender, article and CEFR lookups (already public); and the missing
world nouns. A check of the bundle's lexicon found `árbol`, `piedra`, `madera`, `cueva`, `roca`, `hacha`, `tierra`, `hierro`, `antorcha`,
`flor`, `hierba`, `hoja`, `agua`, `arena`, `oro` and `manzana` as nouns, `pico` as a noun and a verb form, `guía` as a noun of both genders
and a verb form, and `cobre` only as a verb form; `carbón`, `arbusto`, `seta`, `tronco`, `bloque`, `mochila` and `bellota` are missing. The
game adds them in a hand-authored `lexicon/world.json`, as the core's own bundle does with `game.json`.

**Recommendation.** Build the prototype on 0.1.0 as it is, with the workarounds, and file CR-002, CR-003, CR-006, CR-007 and CR-008 when P3
starts, so the core can schedule them while the game proves the loop. CR-004 and CR-005 can wait for the prototype's findings; CR-009 waits
for P10.

## 2. The guide NPC

### 2.1 What the guide can understand

The core resolves authored intents; it does not answer arbitrary questions. The guide is therefore a content scene in `content/game-v1`,
built the way the plan's G3 checklist builds the innkeeper (intent, two or more slot grammars, a keyword rule, five examples with a learner
error, answer sets, prompts with `text`, reply pools passing `cefr-check` at A1 and A2):

| Intent | Example utterances | Slots | Host event | What the host does |
| --- | --- | --- | --- | --- |
| `greet` (exists) | hola; buenos días | | `agent.greet` | turn to the player |
| `ask_directions` (G3's planned intent) | ¿dónde está el árbol?; ¿dónde hay una cueva?; busco piedra | `place` (`[NOUN:place]`) | `guide.directions` | nearest tile of that noun's kinds; direction and distance |
| `ask_what` | ¿qué es esto?; ¿qué es eso?; ¿cómo se llama esto? | | `guide.identify` | the noun of the tile or item under the cursor |
| `ask_help` | ayuda; ¿me ayudas?; ¿qué hago? | | `guide.help` | a hint chosen from game state |
| `name_object` | árbol; un árbol; es una piedra | `object` (`[NOUN:object]`) | `guide.named` | compare the lemma with the target; progress |
| `thank`, `farewell` (from G2) | gracias; adiós | | `agent.farewell` | close the panel |

The guide's agent (`agents/guide.json`) needs only two states: `idle` and `talking`. `talking` loops on every intent above, `farewell` returns
to `idle`, and the fallback plays `not_understood` and keeps the state. Slot conditions are not needed, because the state machine does not
branch on what is asked: the host does.

One open prompt, `guide-open`, allows all the guide's intents and carries `text` ("¡Hola! Soy la guía. ¿En qué te ayudo?"). A constrained
twin, `guide-constrained`, uses an answer set `guide-questions` that lists the guide's sentences over the catalogue's place nouns (about
twelve nouns times three patterns), so Vosk's grammar mode has a closed phrase list when open recognition proves too weak (section 2.3).

### 2.2 The world as data the guide can describe

`Vocaterra.Simulation` answers questions about the grid; `Vocaterra.Game` turns them into Spanish:

- `WorldQuery.Nearest(TileKindSet kinds, TilePos from, int radius)` returns the closest matching tile by a breadth-first search over passable
  tiles, so "nearest" means reachable, not through rock, with a straight-line fallback when nothing is reachable within the radius.
- `WorldQuery.CaveEntrances` comes from the generator's own record of where worm tunnels meet the surface (section 4).
- `Direction` from the player to the target: the larger axis decides `a la izquierda`, `a la derecha`, `arriba`, `abajo`; both axes when
  they are close (`abajo, a la derecha`). Distance in blocks, written with the core's `words` filter (`a doce bloques`). All of these words
  are in the bundle's A2 list (`arriba`, `abajo`) or the lexicon (`izquierda`, `derecha`).
- The host line pools: `guide.directions.found` ("{place:article(def):cap} está {direction}, a {distance:words(m)} bloques."),
  `guide.directions.none` ("No veo {place:article(indef)} cerca."), `guide.identify` ("Es {target:article(indef)}."), and
  `guide.help.<situation>` per game situation. The host fills `place`, `direction`, `distance` and `target` itself (CR-003 workaround).
- The answer also changes the world: an arrow above the player points at the target for ten seconds, and the target tile pulses. That is
  the host event made visible, which is what proves the loop.

### 2.3 Turn flow

1. The player walks within four tiles of the guide; a prompt shows "T: hablar". Pressing T opens the dialogue panel, shows `PromptSpec.Text`
   and pauses the simulation (simplest, and keeps the tick stream deterministic).
2. Input: typing (Enter sends) or push-to-talk (hold V). Typing gives the transcript directly. Speech: the front-end's capture delivers device
   samples; `Vocaterra.Game` resamples to 16 kHz (`Resampler.To16k`) and appends them to a `PushToTalkSession`; on release `End()` returns
   the trimmed clip. An empty clip (no speech) shows "No te he oído." and costs nothing.
3. Evaluation runs on a worker task: `pipeline.Trace(clip, prompt, context)` with Vosk for a spoken turn or the scripted recognizer for a typed
   one. The main loop polls the task in `Update` and never waits on it.
4. The result is applied at a tick boundary: the transcript (`trace.Asr.Transcript`), the recast or hint line, the agent's reply, then the
   host line from the host event; `context.Next(result, now)`; word progress; the arrow or highlight.
5. Optional speech out: the lines go to the synthesizer on the worker, and the audio plays when it is ready. The text never waits for audio.
6. Every turn is appended to the session's replay log (`ReplayLog`), so a turn can be reproduced without the microphone.

**Latency budget** for a 2 to 3 second utterance on the 4-core minimum machine the core targets (`tools.md`), from key release:

| Step | Budget | Basis |
| --- | --- | --- |
| VAD trim and resample | under 10 ms | D0 code over about 48 000 samples |
| Vosk, grammar or small open model | under 150 ms (core target, unmeasured) | `high-level-docs.md` performance budget |
| Parakeet open, if used | under 400 ms target; about 1.2 s reported | `high-level-docs.md`; core research section 3.4 |
| Evaluation from transcript to response plan | under 5 ms | `high-level-docs.md` |
| Host line and world query | under 5 ms | a breadth-first search within 64 tiles |
| **Text on screen** | **under 0.5 s with Vosk** | the sum, with margin |
| Kokoro speech, first audio | under 300 ms target, likely more on one thread | `high-level-docs.md`; measure in P8 |

Models load once, behind the loading screen, on a worker: the bundle (about 0.25 s), Vosk (seconds), Kokoro (seconds). Constructors of the
adapters load their models eagerly or on first use; the exact point is CR-008.

### 2.4 Where audio lives

Audio capture and playback are engine code (ADR 0009, `docs/architecture.md`). `Vocaterra.Game` defines two small interfaces and keeps every
sample-rate and clip decision on its side of them:

```csharp
public interface IAudioCapture        // implemented by the front-end
{
    int SampleRate { get; }           // the device rate; Vocaterra.Game resamples
    void Start();                      // push-to-talk pressed
    int Read(Span<short> mono);        // drained every frame on the main thread
    void Stop();                       // released
}

public interface IAudioOut            // implemented by the front-end
{
    void Play(short[] samples, int sampleRate);
}
```

The microphone choice for MonoGame is in section 3.3. **Recommendation:** typed first (P3), the same turn with Vosk next (P5), Kokoro last
(P8, after CR-006); Parakeet only after the core's M24 speech check says which recognizer keeps learner errors.

## 3. Architecture for an engine move

### 3.1 Layout

```
src/Vocaterra.Simulation/   no package references at all; no engine; targets net8.0 and net10.0 (section 3.4)
  World/      TileId, TileGrid, Chunk (32 x 32), WorldInfo (seed, size, generator version)
  Generation/ WorldRng (SplitMix64), ValueNoise, passes (terrain, layers, caves, ores, grass, forest, spawn), GenerationRecord
  Physics/    Body (position, velocity, AABB), TileCollider, the fixed tick (60 Hz)
  Mechanics/  Mining, Building, ItemDefinition, ItemStack, Inventory, Hotbar
  Lighting/   LightMap (flood fill, sunlight)
  Queries/    WorldQuery (nearest tile of a kind, reachability)
  Simulation.cs  Step(IReadOnlyList<PlayerCommand>) advances one tick; Events out (TileMined, ItemPicked, ...)
src/Vocaterra.Game/         references Simulation and the Vocaterra.Nlp.* packages; no engine; net10.0 (as the packages)
  GameSession   owns the Simulation, the ConversationContext, the stored time, word progress and the save
  Guide/        GuideService (turn flow), WorldFacts (directions in Spanish), host-line rendering
  Language/     NounCatalog, WordProgress, KnownRule
  Audio/        IAudioCapture, IAudioOut, VoiceTurn (resample, PushToTalkSession, worker task)
  Saves/        SaveFile (JSON, ordinal order, invariant culture)
src/Vocaterra.MonoGame/     the only engine code: Game subclass, renderers, greyscale, input mapping, microphone, playback, UI
```

The front-end reads state (tiles in view, bodies, inventory, saturation per lemma, dialogue lines) and writes commands (`PlayerCommand`:
move, jump, use at a tile, select a hotbar slot) and conversation input (typed text, captured samples). Nothing in the two libraries knows
about textures, `SpriteBatch`, `Vector2` or input devices; positions are the simulation's own structs. A Godot front-end then re-implements only
`Vocaterra.MonoGame`'s responsibilities.

Two libraries rather than one, because `Vocaterra.Simulation` then builds and tests without the core packages, and the packages need a 3 GB
pack in each worktree (plan section 5). World and mechanics milestones (P1, P6, P7) therefore run in a worktree with no pack. One caveat to
check in P1: `nuget.config` names the submodule's feed folder, and NuGet may refuse a missing local source even for a project that needs no
package from it (**unverified**); if it does, the brief tells the agent to restore with `-p:RestoreSources=https://api.nuget.org/v3/index.json`.

### 3.2 Determinism

The plan's rule 2 holds unchanged, and extends to the world:

- **World seed.** The save owns one `ulong` seed. Generation is a pure function `WorldGenerator.Generate(seed, WorldSize, GeneratorVersion)`.
  Each pass draws from `WorldRng.For(seed, passName, chunkX, chunkY)` (SplitMix64 over a hash of the inputs), so passes do not consume each
  other's draws and a change to one pass does not reshuffle the others. `System.Random`, `Random.Shared`, `Guid.NewGuid` and the clock are
  banned in both libraries; a test scans the assemblies' references for them, as the core's checklist does.
- **Arithmetic.** Generation uses integer hashing and plain `float` arithmetic (add, multiply, divide, floor); no `Math.Sin`, `Pow` or `Exp`,
  whose last bits may differ between platforms. The goldens are SHA-256 hashes of the tile arrays per seed.
- **Time.** The simulation's time is its tick count. The core's stored time is the host's: `GameSession` keeps `startedAt` (Unix seconds,
  read from the clock once when a save is created) plus elapsed play seconds (`tick / 60`), and passes that as `StoredTimestamp`. Nothing in
  the evaluation path reads the clock; tests pass a fixed `startedAt`, as the host's `--transcript` mode does.
- **Seeds for conversation.** The `ConversationContext` world seed is the same save seed. `HostStateHash` is the game's own digest (save id,
  tick, a hash of the modified chunks and of word progress), passed to the context's constructor, as `docs/api.md` section 4 allows.
- **Dictionaries** that reach a save or a log use `StringComparer.Ordinal`; culture is invariant.

### 3.3 MonoGame specifics

- **Version.** MonoGame 3.8.6 is the current stable release. GitHub marked it latest on 7 October 2026, and `MonoGame.Framework.DesktopGL`
  3.8.6 has been on NuGet since 8 October ([releases](https://github.com/MonoGame/MonoGame/releases),
  [NuGet](https://www.nuget.org/packages/MonoGame.Framework.DesktopGL)). No 3.9 or 4.0 preview package was found; the 3.8.5 post plans a
  unified shader compiler for 3.9 ([3.8.5 release post](https://monogame.net/blog/2026-07-15-3.8.5-release-2026/)).
- **.NET 10.** The DesktopGL package ships `net8.0` only, and NuGet lists `net9.0` and `net10.0` as computed. A `net10.0` front-end can
  therefore reference it through ordinary forward compatibility. No official MonoGame statement on .NET 10 for desktop was found.
  MonoGame's Ubuntu guide asks for at least the .NET 9 SDK for its tools and templates
  ([Ubuntu setup](https://docs.monogame.net/articles/getting_started/1_setting_up_your_os_for_development_ubuntu.html)). The repository's
  SDK is 10.0.402. **Unverified** until P2 builds it.
- **DesktopGL on Linux.** The DesktopGL backend uses SDL2 2.32.10 and OpenAL Soft, through the `MonoGame.Library.SDL` and
  `MonoGame.Library.OpenAL` packages. 3.8.6 adds a native OpenGL backend. 3.8.5 added DesktopVK (SDL2, Vulkan, FAudio), which is "meant to
  eventually replace DesktopGL" (same sources). DesktopGL is the right target for now; DesktopVK is a later swap that changes only the
  front-end's package.
- **Content pipeline.** Since 3.8.2 the MGCB builder and editor are per-project local dotnet tools. 3.8.5 added a code-first "Content Builder
  Project", which 3.8.5 calls its first release and 3.8.6 is still fixing
  ([announcement](https://monogame.net/blog/2025-11-06-MonoGame-Content-Builder/),
  [docs](https://docs.monogame.net/articles/getting_started/content_pipeline/content_builder_project.html)). The prototype needs neither:
  PNG textures load at run time with `Texture2D.FromFile`/`FromStream`, sounds with `SoundEffect.FromFile`, and TTF fonts with FontStashSharp
  (Zlib, runtime TTF rendering for MonoGame, [GitHub](https://github.com/FontStashSharp/FontStashSharp)). That removes the tool, its build step
  and SpriteFont character regions. (The default SpriteFont region is 32 to 126, so accented letters need a second region,
  [FontImporter](https://docs.monogame.net/api/Microsoft.Xna.Framework.Content.Pipeline.FontImporter.html).) It also keeps the assets as plain
  files that Godot imports directly.
- **Shaders.** A greyscale effect is a short HLSL `.fx` file: a `Saturation` parameter, `dot(rgb, luma)` and `lerp`. The official 2D tutorial
  has one ([tutorial, chapter 24](https://docs.monogame.net/articles/tutorials/building_2d_games/24_shaders/index.html)). On Linux, though,
  effect compilation still needs Wine: MonoGame's Ubuntu guide says "Effect (shader) compilation requires access to DirectX" and gives a Wine
  setup script (Ubuntu setup page above). Section 7 therefore recommends a CPU-made grey atlas, so the prototype has no shader toolchain.
- **Microphone.** `Microsoft.Xna.Framework.Audio.Microphone` is implemented only on OpenAL platforms, DesktopGL included. It delivers 16-bit
  PCM, at 44 100 Hz by default, through `BufferReady`/`GetData`, with a buffer of 100 to 1 000 ms
  ([how-to](https://docs.monogame.net/articles/getting_to_know/howto/audio/HowTo_Microphone.html),
  [api](https://docs.monogame.net/api/Microsoft.Xna.Framework.Audio.Microphone.html)). The forum reports old problems: distorted, repeating
  audio on DesktopGL 3.8.1 ([thread](https://community.monogame.net/t/cant-get-microphone-to-work-properly-in-monogame-3-8-1/19332)) and a
  Linux crash while listing devices ([thread](https://community.monogame.net/t/microphone-in-3-7/10280)). No release since mentions the
  microphone. The alternatives:
  - **PortAudioSharp2.** Apache-2.0, bundles prebuilt PortAudio for Linux, macOS and Windows, and is what sherpa-onnx's own C# microphone
    example uses ([GitHub](https://github.com/csukuangfj/PortAudioSharp2), [NuGet](https://www.nuget.org/packages/PortAudioSharp2)).
  - **SDL2 capture.** `SDL_OpenAudioDevice` with `iscapture`, through the SDL that MonoGame already loads
    ([SDL wiki](https://wiki.libsdl.org/SDL2/SDL_OpenAudioDevice)). Whether it coexists cleanly with MonoGame is **unverified**.
  - **OpenAL Soft capture.** Through Silk.NET.OpenAL (MIT) or OpenTK's `ALC` functions; the native library is LGPL
    ([openal-soft](https://github.com/kcat/openal-soft)).
  - **NAudio.** Its Linux capture is the prerelease `NAudio.Linux.Alsa` 3.0.0-preview.7
    ([NuGet](https://www.nuget.org/packages/NAudio.Linux.Alsa)).

  **Recommendation:** behind `IAudioCapture`, try MonoGame's `Microphone` first (no new dependency). If its audio is distorted on the
  owner's machine, switch to PortAudioSharp2, which sherpa-onnx's own example proves on Linux.
- **Playback.** `DynamicSoundEffectInstance` takes 16-bit PCM buffers at a given rate, which matches the core's `AudioBuffer`
  (`short[]`, sample rate). Kokoro's 24 kHz output needs no resampling.

### 3.4 What a Godot move would cost

- **Godot status.** The current stable release is Godot 4.7.2 (GodotSharp 4.7.2, 18 August 2026), and 4.8.0-beta.1 came out on 15 October
  ([GodotSharp](https://www.nuget.org/packages/GodotSharp), [4.8 beta 1](https://godotengine.org/download/archive/4.8-beta1)). C# desktop
  export works on Linux, Windows and macOS. C# web export is still unsupported
  ([C# basics](https://docs.godotengine.org/en/stable/tutorials/scripting/c_sharp/c_sharp_basics.html)). Godot is MIT-licensed.
- **The target framework is the real cost.** GodotSharp 4.7 and 4.8 ship `net8.0` only. A `net8.0` Godot project cannot reference a
  `net10.0` library. Users report that changing the project's `TargetFramework` to `net10.0` builds and runs on 4.6 and 4.7, but some saw
  editor builds fail; there is no official support
  ([forum](https://forum.godotengine.org/t/net10-and-c-14/133412),
  [proposals discussion](https://github.com/godotengine/godot-proposals/discussions/13076)). The core's packages are `net10.0` only, so:
  - `Vocaterra.Simulation` targets `net8.0;net10.0` from P1. It has no packages, so this costs one property, and it keeps the world and the
    mechanics reusable from any Godot version.
  - `Vocaterra.Game` must be `net10.0` while the packages are. A Godot front-end then either retargets to `net10.0` (works unofficially) or
    waits for CR-009 (the core multi-targets `net8.0`). P10 measures which one is needed.
- **What moves and what is rewritten.** The two libraries, the content bundle, the PNG and TTF assets and the tests move unchanged. Rewritten:
  - tile drawing (a Godot `TileMapLayer`, or a custom `_Draw`);
  - the grey atlas (a `canvas_item` shader with a `saturation` uniform is natural in Godot;
    [canvas_item shaders](https://docs.godotengine.org/en/stable/tutorials/shaders/shader_reference/canvas_item_shader.html));
  - input mapping;
  - the UI panels;
  - microphone capture (`AudioStreamMicrophone` on a bus with `AudioEffectCapture`, read with `GetBuffer`;
    [AudioEffectCapture](https://docs.godotengine.org/en/stable/classes/class_audioeffectcapture.html));
  - playback (`AudioStreamGenerator`).

  This is roughly the size of P2 plus the UI parts of P3 to P5.

### 3.5 Fit with the console host and D-game-1

`src/Vocaterra.Host` stays a console program with no engine code, as D-game-1 requires. It keeps its role as the cheapest way to exercise
content (`--transcript ... --prompt guide-open` works on the guide as soon as P3 adds it). Later it can drive `Vocaterra.Game`'s `GuideService`
with a fixed world seed, which gives a text front-end for transcripts and tests. The MonoGame front-end is a new project, which is what
D-game-1's "in its own project" asks for.

**Recommendation.** Three new projects (`Vocaterra.Simulation`, `Vocaterra.Game`, `Vocaterra.MonoGame`) and two test projects. Record
D-game-1 as "MonoGame for the prototype; Godot re-assessed after a spike (P10)", and change rule 3 of the plan accordingly.

## 4. The world

### 4.1 Grid and chunks

- **Size.** 1 024 x 512 tiles for the prototype (Terraria's small world is 4 200 x 1 200). 16-pixel tiles. Surface around row 180,
  dirt to about 15 to 30 tiles below it, stone below, caverns in the stone layer.
- **Storage.** Chunks of 32 x 32: a `ushort[]` foreground tile id, a `byte[]` background wall id and a `byte[]` light level per chunk, so
  512 chunks and about 1.5 MB in all. Chunks are the unit of rendering culls, lighting updates, dirty tracking and saves (a save stores the
  generated seed plus the modified chunks only).
- **Tile definitions** are data in `Vocaterra.Simulation` (`TileDefinition`: id, name, solid, hardness, drop item, light emission,
  transparency). The Spanish noun of a tile is not here: it lives in the content bundle's catalogue (section 7), because game words are content.

### 4.2 Generation passes

Terraria builds its world in about 109 named passes ([Terraria Wiki, World creation](https://terraria.wiki.gg/wiki/World_creation);
[Layer](https://terraria.wiki.gg/wiki/Layer)). In order, the early ones are:

- Reset, Terrain, Tunnels, Dirt Wall Backgrounds, Rocks In Dirt, Dirt In Rocks;
- Small Holes, Dirt Layer Caves, Rock Layer Caves, Surface Caves;
- then Grass, and later the ores ("Shinies").

The prototype keeps that shape with eight passes:

| # | Pass | Technique |
| --- | --- | --- |
| 1 | Terrain | 1-D fractal value noise (four octaves) for the surface height per column, plus a low-frequency hill term; the spawn area is flattened |
| 2 | Layers | dirt from the surface to a noisy depth, stone below; patches of dirt in stone and stone in dirt by thresholded 2-D noise |
| 3 | Tunnels | three to five "worm" tunnels from the surface: a seeded random walk whose heading drifts by noise, carving a disc of radius 1 to 3; each entrance is recorded for the guide |
| 4 | Caverns | cellular automata in the stone layer: fill 45 % at random, then four to five steps of the 4-5 rule (a cell becomes rock with five or more rock neighbours); small pockets removed by a flood fill, the largest kept and joined to a tunnel |
| 5 | Ores | copper and iron veins: short seeded random walks, depth bands per ore |
| 6 | Grass | dirt with air above becomes grass |
| 7 | Forest | trees on grass with a minimum spacing (a seeded jittered grid), trunk 5 to 10 tiles with a leaf canopy and branches; bushes, tall grass, flowers (two or three kinds) on free surface tiles; mushrooms on cave floors |
| 8 | Spawn and guide | spawn at the flattest column near the centre; the guide two to six tiles away; a guaranteed tree within 20 tiles and a tunnel entrance within 80 |

References for the techniques:

- Noise heightmaps (frequency, octaves, redistribution) from [Red Blob Games](https://www.redblobgames.com/maps/terrain-from-noise/). For a
  side view, sample 1-D noise along x.
- The 4-5 cellular automaton (about 45 % initial walls, four or five iterations) from
  [RogueBasin](https://www.roguebasin.com/index.php/Cellular_Automata_Method_for_Generating_Random_Cave-Like_Levels) and
  [gridbugs](https://gridbugs.nfshost.com/cellular-automata-cave-generation/).
- The random walk, which is always connected and is the basis of the worm tunnels, from
  [RogueBasin](https://www.roguebasin.com/index.php/Random_Walk_Cave_Generation).

### 4.3 Lighting

Cheap and worth having, because caves need to be dark for the guide's "where is the cave" and torches to matter: sunlight fills every column
from the top down to the first solid tile, then a breadth-first flood fill spreads light with a fall-off per step (more through solid tiles),
torches are sources. Recomputed per chunk neighbourhood when a tile changes. Light is a byte per tile; the renderer multiplies it into the tile
colour. The flood fill is integer code, so it is deterministic and testable.

### 4.4 Testing headless

- Golden: `Generate(42, Small)` hashes to a fixed SHA-256; any change to a pass changes the golden on purpose, with the generator version.
- Properties over a hundred seeds: the spawn tile has air above and ground below; the guide stands on ground; every tree's base is on grass;
  every tunnel entrance connects to the surface; the cavern fill ratio stays between 35 % and 55 %; at least one reachable tree within 20 tiles.
- An ASCII dump helper renders a window of the grid as characters for test output and bug reports (no engine needed).

### 4.5 Recommendation

Value noise and the passes above, written in `Vocaterra.Simulation` without a third-party noise library (about a hundred lines, no licence
question while D-game-2 is open). If the terrain looks poor, the alternative is FastNoiseLite: MIT, one C# file, with OpenSimplex2, Perlin,
Value and Cellular noise, fractals and domain warp ([GitHub](https://github.com/Auburn/FastNoiseLite/tree/master/CSharp)). Copying it in needs
its MIT notice kept and a check that it obeys the arithmetic rule in 3.2. Lighting comes in P7, not P1.

## 5. Mechanics and the minimum fun loop

### 5.1 Movement and collision

- Fixed 60 Hz tick in the simulation; the front-end accumulates frame time and calls `Step` zero or more times per frame.
- The player is an axis-aligned box of 1.25 x 2.75 tiles. Per tick: horizontal acceleration towards the input direction with friction on
  the ground and less in the air; gravity with a terminal fall speed; jump impulse with a variable height (releasing early cuts the rise);
  coyote time of six ticks and a jump buffer of six ticks.
- Collision moves the body on X, resolves against the solid tiles it overlaps, then on Y (the usual tile-platformer sweep). A one-tile step-up
  when walking into a one-tile ledge, as Terraria has, keeps walking over rough terrain pleasant.
- The guide is a simple body with the same collider that idles and wanders a few tiles; it turns to face the player when spoken to.

### 5.2 Mining

- Each tile definition has a hardness; each tool a power. A use action on a tile within reach (4.5 tiles from the player's centre) adds
  power per hit at the tool's speed; when the accumulated damage reaches the hardness, the tile breaks. Damage resets when the target changes.
- Drops go straight into the inventory in the prototype (no item entities): dirt gives dirt, stone gives stone, ore gives ore, a trunk tile
  gives wood and fells the tree above it, leaves sometimes give an acorn.
- Tools: hands mine dirt slowly; the starting pickaxe mines dirt, stone and ore; the starting axe cuts trees.

### 5.3 Building

- The selected hotbar item, if placeable, goes into the target tile when it is within reach, empty, not overlapping a body, and next to a solid
  tile or a background wall (Terraria's adjacency rule, which stops floating blocks).
- Placing consumes one item; a background wall layer is a later addition.

### 5.4 Inventory

- 40 slots, the first 10 the hotbar (keys 1 to 0 and the mouse wheel). A stack is an item id and a count up to the item's stack limit (999 for
  blocks, 1 for tools). Add merges into existing stacks, then the first empty slot; remove takes from the selected slot. Drag and drop in the
  inventory screen, shift-click to move.
- Every item has a catalogue lemma, so inventory icons follow the colour rule too.

### 5.5 The minimum fun loop

The smallest loop that tests the concept:

1. Spawn in a grey forest next to the guide, who speaks first (`PromptSpec.Text`).
2. Ask "¿qué es esto?" pointing at a tree; hear "Es un árbol."; say "un árbol"; every tree on screen gains colour.
3. Chop wood, dig, build a shelter from known (coloured) and unknown (grey) blocks.
4. Ask "¿dónde está la cueva?"; follow the arrow; find grey ores in the dark; ask what they are; name them.
5. The journal fills; the forest is in colour where the player has learnt it.

Each turn of the loop pairs a world goal (wood, shelter, the cave) with a language act (ask, name, follow directions). The colour is the
visible score. Crafting, combat, enemies and a day cycle are left out: they add fun later but test nothing about the concept.

**Recommendation.** Movement and the world first (P1, P2), the guide slice before mining (P3), then colour (P4), voice (P5), mechanics (P6)
and caves (P7). Without mining the slice still proves the integration; without colour it does not prove the concept, so colour comes early.

## 6. The language progress system

### 6.1 Who owns what

| Data | Owner | Where it lives |
| --- | --- | --- |
| BKT skills (`intent.<id>`, error families) | the core computes; the game persists | `result.Decision.Learner`, the save |
| Repeat counts per error tag | the core computes (`RepeatCounts.Next`); the game persists | the context, the save |
| FSRS cards | the core's functions; the game owns the cards | not used in the prototype |
| Word progress per lemma (heard, read, spoken, correct, known) | **the game** | `Vocaterra.Game.Language`, the save |
| The noun catalogue (tile and item to lemma) | **the game** (content) | `content/game-v1/game/nouns.json` |
| Lemma, gender, article, plural, CEFR level | the core's lexicon and morphology, read by the game | `bundle.Lexicon`, `bundle.Morphology`, `bundle.Cefr` |

Word progress is game data because the core's learner model is per skill (intent and error family), not per word, and because "known" is a
game rule. It sits beside the learner model in the save, not inside it.

### 6.2 Data model

```csharp
public sealed record WordProgress(
    string Lemma,             // "árbol"
    int Heard,                // the guide said it (a reply or host line contained it)
    int Read,                 // its label was shown for at least a second (hover, journal, toast)
    int Spoken,               // the player used it in a communicated turn
    int SpokenCorrect,        // ... with no error whose span covers the word or its article
    int CorrectTurns,         // distinct turns with a correct use (several uses in one line count once)
    long FirstSeenTick,
    long LastSpokenTick,
    WordStage Stage);         // Unseen, Encountered, Learning, Known

public sealed record LanguageProgress(IReadOnlyDictionary<string, WordProgress> Words);   // ordinal keys
```

Counting rules:

- **Spoken.** After each communicated turn, the game collects the catalogue lemmas in the utterance: `Verdict.Slots` values that are
  catalogue lemmas, plus normalized tokens whose lexicon readings include a catalogue noun lemma (CR-002 replaces the second source). A token
  counts as correct when no verdict error's span overlaps the token or its article (`gender.agree`, `noun.number`, `vocab.unknown` near-miss).
  A turn that was not communicated counts nothing: the player was not understood.
- **Heard.** Each response line's normalized tokens, read the same way.
- **Read.** The front-end reports a label shown for a second or more (debounced to once per lemma per minute).
- Only catalogue lemmas are counted in the prototype; other words are left to the core's learner model. This keeps the counts meaningful and
  avoids most homographs (`pico` counts as the pickaxe because only the noun is in the catalogue).

### 6.3 When a noun is known

Recommended rule (proposed D-game-6): **known after three correct spoken uses in three separate turns.** Saturation is `CorrectTurns / 3`,
clamped, so colour grows in thirds and every correct use is visible. Hearing and reading never colour a word, but a heard word shows its name
in the journal (an unheard one shows "¿?"). No decay in the prototype; a later version can let colour fade as FSRS retrievability drops
(`Fsrs.Retrievability`), which turns review into "restore the colour" (an idea, not a milestone).

The thresholds live in a small game config file in the bundle (`game/progress.json`: `correctTurnsToKnow`, `typedCountsAs`), so playtests
can tune them without code.

### 6.4 Persistence and display

- Saved in the save's `language.json` with the learner model, the repeat counts, the guide's agent state and interaction counter, ordinal
  keys, invariant culture; a save written twice is byte-identical (a P9 test).
- Shown as: a HUD counter ("Palabras: 7 / 24"), a journal screen (J) listing every catalogue noun with its icon in its current saturation,
  the article and lemma once heard (`el árbol`), the counts and three progress pips; a toast and a colour wave across the screen when a noun
  becomes known ("¡Nueva palabra: el árbol!").

**Recommendation.** Count only catalogue nouns, own the counts in `Vocaterra.Game`, keep BKT as the core's, use the three-turn rule, and
file CR-002 so the counts come from the pipeline's own analysis.

## 7. The colour and greyscale mechanic

### 7.1 Where the idea comes from

Noun Town works like this. Its island "starts in greyscale. Every word you master brings color back to the world." It covers 1 000+ words in
12 languages, including Spanish (Spain and Mexico), and uses speech recognition with 16 NPCs
([Steam](https://store.steampowered.com/app/2313720/Noun_Town_Language_Learning/)). One review describes the loop
([Coto Academy review](https://cotoacademy.com/noun-town-review-should-you-learn-japanese-in-vr/)):

1. A kiosk hands out five nouns.
2. The player finds each object and listens to it.
3. Saying a word correctly colours the object temporarily; a wrong attempt leaves it grey.
4. A short quiz on the five words makes the colour permanent.
5. Spaced repetition then runs from 30 minutes to 30 days.

No fixed repetition count is published. The developer says recognition checks "whether what you said matches the target phrase" without
penalising slight accents ([Noun Town blog](https://noun.town/blog/how-noun-town-teaches-languages-through-play/)). Players often complain
that recognition is unreliable (Steam reviews), which matters here too (section 10).

Vocaterra's version differs in two ways:

- The core grades the whole utterance, so `la árbol` earns no credit and gets a recast.
- Colour grows in thirds instead of a temporary flash followed by a quiz, which suits a sandbox with no lesson structure.

### 7.2 Mapping tiles, items and entities to nouns

The catalogue is content: `content/game-v1/game/nouns.json`, a host-owned file (CR-007), validated by a game test against the bundle's
lexicon:

```json
{
  "schemaVersion": 1,
  "nouns": [
    { "lemma": "árbol",  "tiles": ["tree_trunk", "tree_leaves"], "items": [] },
    { "lemma": "madera", "tiles": [], "items": ["wood"] },
    { "lemma": "tierra", "tiles": ["dirt", "grass"], "items": ["dirt"] },
    { "lemma": "piedra", "tiles": ["stone"], "items": ["stone"] },
    { "lemma": "hierro", "tiles": ["iron_ore"], "items": ["iron_ore"] },
    { "lemma": "flor",   "tiles": ["flower_red", "flower_yellow"], "items": ["flower"] },
    { "lemma": "guía",   "entities": ["guide"], "gender": "f" }
  ],
  "colourless": ["air", "sky", "player", "ui"]
}
```

- The **gender, article and plural** come from the core at load: `bundle.Lexicon.Lookup(lemma)` (its noun reading's gender),
  `bundle.Morphology.Article(Definiteness.Definite, gender, "sg", lemma)` and `Pluralize`. The `gender` field is used only where the lexicon
  has both genders (`guía`).
- The **game test** checks four things:
  - every tile and item id in `Vocaterra.Simulation` is either in a noun entry or in `colourless`;
  - every lemma has a noun reading;
  - every lemma is in `bundle.Cefr` A1 or A2, or is listed as an intended exception;
  - every place noun the guide's grammars accept is in the catalogue.
- **Many-to-one** is allowed: dirt and grass tiles share `tierra` for now (`césped` could follow). Colour is per lemma, so learning a word
  colours every object that uses it.

### 7.3 Rendering

**MonoGame, recommended: a CPU-made grey atlas.**

1. At load, the front-end copies the tile and item atlas with `Texture2D.GetData`.
2. It converts each pixel to luma (`0.299 r + 0.587 g + 0.114 b`, alpha kept) and creates a second texture with `SetData`.
3. A tile with saturation `s` is drawn twice: from the grey atlas, then from the colour atlas with alpha `s` on top (premultiplied alpha
   blending). For `s` of 0 or 1 it is drawn once.

This needs no shader, so no Wine on Linux (section 3.3). A view of about 120 x 68 tiles is at most about 16 000 sprites per frame, which
`SpriteBatch` handles in one or two batches. Light multiplies into the tint colour as usual.

**MonoGame, alternative: a pixel shader.** Saturation travels in the vertex colour's alpha, which opaque tiles do not otherwise need. The
shader computes `lerp(luma, rgb, input.Color.a)` and outputs the texture's own alpha. That is one draw per tile and one effect per batch,
but it needs the `.fx` compiled through Wine or on another machine.

**Godot equivalent:** a `canvas_item` shader:

```glsl
shader_type canvas_item;
uniform float saturation : hint_range(0.0, 1.0) = 1.0;
void fragment() {
    float g = dot(COLOR.rgb, vec3(0.299, 0.587, 0.114));
    COLOR.rgb = mix(vec3(g), COLOR.rgb, saturation);
}
```

A uniform per layer or node is set with `ShaderMaterial.set_shader_parameter`. Per-tile saturation reads the vertex colour's alpha
instead, as in the MonoGame alternative. `modulate` alone can tint but cannot desaturate (section 3.4 sources).

**The interface between the libraries and the front-end** is the same for both engines: `Vocaterra.Game` exposes `float Saturation(TileId)`
and `float Saturation(ItemId)`, computed from word progress through the catalogue.

### 7.4 How a noun becomes known

The proposed rule is D-game-6: three correct spoken uses in three turns, with colour in thirds (section 6.3). A correct use can come from
any communicated turn:

- naming the target (`es un árbol` with the cursor on a tree);
- asking for it (`¿dónde está el árbol?`);
- using it in any other intent.

Two juice details make the progress visible:

- each correct use flashes the objects of that noun to full colour for a second before they settle at their new saturation, which borrows
  Noun Town's temporary colour;
- becoming known sends a colour wave across the screen.

### 7.5 Edge cases

| Case | Handling |
| --- | --- |
| Wrong article or gender (`la árbol`) | the core reports a gender agreement error on the span; no credit; the recast says `el árbol` (heard +1) |
| Feminine nouns with stressed a (`el hacha`, `el agua`) | the article comes from `Morphology.Article(..., lemma)`, which handles them; the analyzer accepts `el hacha` |
| Plural (`dos árboles`) | lemma `árbol`; counts |
| Missing accent, typed (`arbol`) | the lexicon's accent-insensitive lookup reads it; counts; the journal shows the accented spelling |
| Homographs (`pico`, the pickaxe, a peak, a form of `picar`) | only the catalogue noun is counted; the grammar slot `[NOUN:object]` keeps the noun reading |
| A correct word for the wrong object (says `piedra` at a tree) | `name_object` gives no credit to the target; the guide says `No, es un árbol.`; `piedra` still counts as spoken correctly, because it was used correctly as a word |
| Multi-word names (`mineral de hierro`) | avoided in the prototype: one head noun per tile (`hierro`), because `[NOUN]` binds one token |
| Dialect synonyms (`césped` and `pasto`) | a catalogue entry may list alternative lemmas; any of them counts |
| Not understood | nothing counts; the repair ladder runs |
| Objects with no noun (sky, the player, the UI) | listed as `colourless`, always drawn in colour |
| Dark caves hide colour | saturation is applied before lighting; the journal and a hover label still show the state |
| Colour-blind players | a setting adds a small marker to known objects, so colour is not the only cue |
| A save from an older catalogue | progress is keyed by lemma, so tiles that move between lemmas keep the words already known |

**Recommendation.** Use the CPU grey atlas, the catalogue as a host-owned bundle file, colour in thirds and no decay. Revisit the shader
only if the double draw costs frames.

## 8. Assets and licences

| Kind | Candidate | Licence | Fit |
| --- | --- | --- | --- |
| Tiles, platformer | Kenney [1-Bit Platformer Pack](https://kenney.nl/assets/1-bit-platformer-pack) (16 x 16, 400 files) | CC0 | good shapes, but one colour: it would need recolouring, and greyscale says little on a one-colour sprite |
| Tiles, platformer | Kenney [Pixel Platformer](https://kenney.nl/assets/pixel-platformer) with its Blocks and Farm expansions (18 x 18) | CC0 | colourful, so the greyscale effect reads well; 18-pixel tiles are fine if the grid uses 18 |
| Tiles, ground | OpenGameArt [Simple Tileset 16x16](https://opengameart.org/content/simple-tileset-16x16) (dirt, stone) | CC0 | ores not confirmed |
| Tiles, ground | OpenGameArt [Dirt & Grass Tileset](https://opengameart.org/content/dirt-grass-tileset) | CC0 according to a search summary; **unverified** | |
| Avoid | Sprout Lands (Cup Nooble) | free tier non-commercial, no redistribution even if modified ([itch.io](https://cupnooble.itch.io/sprout-lands-asset-pack)) | cannot sit in a repository |
| Avoid | the "Minecraft-ish" platformer tiles on OpenGameArt | CC BY 3.0 | allowed, but needs attribution; prefer CC0 |
| Font | [m5x7](https://managore.itch.io/m5x7) (Daniel Linssen) | CC0 per its itch.io page ("many accented letters") | ñ, ¿ and ¡ not confirmed |
| Font | [Pixel Operator](https://www.dafont.com/pixel-operator.font) | CC0 1.0 since 2018 per its changelog | Latin-1 coverage not confirmed |
| Font | [Silver](https://poppyworks.itch.io/silver) | CC BY 4.0; lists Spanish | needs attribution |
| Font | Press Start 2P | OFL 1.1 (from memory); Latin-1 coverage listed by Google Fonts | OFL is fine to bundle |
| Font | [Kenney Fonts](https://kenney.nl/assets/kenney-fonts) | CC0 | accent coverage not stated |
| Font renderer | [FontStashSharp](https://github.com/FontStashSharp/FontStashSharp) | Zlib | runtime TTF, no SpriteFont regions |
| Sound | Kenney [Impact Sounds](https://kenney.nl/assets/impact-sounds), [RPG Audio](https://kenney.nl/assets/rpg-audio) and the rest of the [audio category](https://kenney.nl/assets/category:Audio) | CC0 (two pages checked) | digging, placing, steps, UI |
| Sound | [Freesound](https://freesound.org/help/faq/) | per sound: CC0, CC BY, CC BY-NC | filter for CC0 |

**No single CC0 pack covers everything.** No CC0 16 x 16 set was found with dirt, grass, stone, ores, trees and a character together. The
practical route has three steps:

1. P2 uses generated placeholder tiles (flat colours with a noise pattern, drawn in code).
2. P4 switches to Kenney Pixel Platformer, with ores and a few forest tiles drawn by hand.
3. A font is chosen by a test that renders á é í ó ú ü ñ ¿ ¡ and fails on a missing glyph.

Placeholder art drawn in code also has no licence question.

**What D-game-2 means.** This repository grants no licence until D-game-2 is decided. Committed third-party assets must still allow
redistribution in a public repository later. CC0 needs nothing, CC BY needs an attribution file, and anything "free, no redistribution" is
excluded. Recommended (proposed D-game-5): CC0 only while D-game-2 is open, recorded in a new `assets/ATTRIBUTION.md` with source and
licence per file, the same way `content/game-v1/ATTRIBUTION.md` records the lexicon.

**What core D2 means.** sherpa-onnx 1.13.8, which the core pins and which the `Asr` and `Response` packages bring in, statically contains
espeak-ng (GPL-3.0). The core accepted this for private development builds only, and the GPL-free pin blocks any distribution (core
`docs/decisions.md`, D2). Upstream closed "Remove espeak-ng and piper-phonemize" on 20 October 2026 (PR #4055), for a breaking 2.0.0 that
takes phonemes from a lexicon or an outside phonemizer ([issue #3731](https://github.com/k2-fsa/sherpa-onnx/issues/3731)). So:

- the prototype is a private build;
- it is never shared as a binary while the packages carry 1.13.8;
- even a text-only build includes the native library, because `Vocaterra.Nlp.Content` references `Asr` and `Response` (`PACKAGING.md`).

Other licences in the speech path:

- Kokoro's archive includes `espeak-ng-data` (GPL-3.0).
- Parakeet is CC-BY-4.0 and needs attribution to NVIDIA.
- The Kroko Spanish streaming zipformer for sherpa-onnx is CC-BY-SA, with commercial use under an OEM licence
  ([model card](https://huggingface.co/csukuangfj/sherpa-onnx-streaming-zipformer-es-kroko-2025-08-06)). It is not a candidate.
- MonoGame is Ms-PL with MIT parts ([LICENSE](https://github.com/MonoGame/MonoGame/blob/develop/LICENSE.txt)). Its OpenAL Soft native
  library is LGPL, dynamically loaded.

## 9. Milestone plan

The full table, with deliverables, demos, tests, models, dependencies and sizes, is in [prototype-plan.md](../archive/game/research/prototype-plan.md). In order:

| ID | Milestone | Model | Size |
| --- | --- | --- | --- |
| G1, G2 | Already planned: adopt M19d, re-sync the bundle | Haiku 5.5 | S, S |
| P1 | Simulation skeleton and surface world (headless) | Sonnet 5.5 | M |
| P2 | MonoGame front-end skeleton | Sonnet 5.5 | M |
| **P3** | **The guide, typed: the smallest vertical slice** | Sonnet 5.5 for scene and session, Haiku 5.5 for content | M |
| P4 | Nouns, word progress and greyscale | Haiku 5.5 | M |
| P5 | Spoken input (microphone, push-to-talk, Vosk) | Sonnet 5.5 | M |
| P6 | Mining, building, inventory | Haiku 5.5 | M |
| P7 | Caves, ores, full forest, lighting | Haiku 5.5 | M |
| P8 | The guide speaks (after CR-006) | Haiku 5.5 | S |
| P9 | Save and load, playtest build | Haiku 5.5 | M |
| P10 | Godot spike (optional) | Sonnet 5.5 | S |

**The smallest vertical slice is P3.** It needs only a walkable world (P1, P2), G1 and G2, and the scripted recognizer: no model, no audio,
no new core call. In it:

- a line typed in the game window is evaluated by the packages;
- the guide's state machine moves;
- the recast and the reply appear;
- a host event changes the world (the arrow to the nearest tree).

That is the integration proof. P4 then proves the concept, and P5 adds the voice on the same turn flow.

**Headless testing throughout.**

- `tests/Vocaterra.Simulation.Tests` needs neither the packages nor an engine: goldens, properties and scripted command sequences.
- `tests/Vocaterra.Game.Tests` uses the packages with the scripted recognizer, a fixed seed and a fixed stored time, as the host's tests do.
- Model-backed tests (Vosk on a recorded WAV) are skipped without `VOCATERRA_MODELS_DIR`, as in the core.
- The MonoGame project holds no logic that needs a test; it is checked by building it and by the owner's demo.

**Relation to the plan.**

- G1 and G2 come first among the language milestones, because P3 uses their API and intents.
- P1 and P2 can run alongside them, since they touch neither the packages nor the bundle.
- G3 (innkeeper) and G4 (quest giver) move after P9. The guide reuses G3's `ask_directions` design.
- G5 is replaced by recording D-game-1 and by P2.

**Disk.** Each worktree that builds against the packages needs its own 3 GB pack (plan section 5), and about 11 GB was free on 2026-10-21.
So run at most one package-building milestone at a time. P1, P6 and P7 can run beside one, because `Vocaterra.Simulation` needs no pack.

## 10. Risks and open questions

| # | Risk or question | Recommendation |
| --- | --- | --- |
| 1 | **D-game-1.** The owner's brief names MonoGame for the prototype; the decision table still says open | Record "MonoGame 3.8 DesktopGL for the prototype; Godot re-assessed after P10". Amend plan rule 3 so engine code is allowed only in `src/Vocaterra.MonoGame` |
| 2 | **Godot cannot reference `net10.0`** officially (section 3.4) | `Vocaterra.Simulation` multi-targets `net8.0;net10.0` now; P10 tests a `net10.0` Godot project; CR-009 if needed |
| 3 | **Microphone capture on Linux** through MonoGame's OpenAL `Microphone` has old distortion and crash reports | Keep it behind `IAudioCapture`, try it first, fall back to PortAudioSharp2 (Apache-2.0, proven by sherpa-onnx's example) |
| 4 | **Recognition quality.** Vosk small has 16 % WER on Common Voice for native speakers (core research, section 3.4); learners do worse; Noun Town players complain about recognition | Keep typing always available. Use the constrained twin prompt (a closed phrase list) for the guide when open mode misfires. Show the transcript, so a misrecognition is visible and not mistaken for a language error. Wait for the core's M24 check before moving to Parakeet |
| 5 | **Recognizers smooth away learner errors** (core research, section 3.4) | Grammar mode with error variants is the error-preserving path. Colour credit needs a communicated turn with no error on the word, so a smoothed error can at worst give credit too early. Acceptable for a prototype |
| 6 | **Shader compilation needs Wine on Linux** | The CPU grey atlas, so no shader at all |
| 7 | **No single CC0 tile set** | Placeholders drawn in code first, Kenney Pixel Platformer next, hand-drawn ores; CC0 only (D-game-5) |
| 8 | **GPL code in sherpa-onnx 1.13.8** (core D2) | Private builds only. Upstream's 2.0.0 removes espeak-ng; the core's GPL-free pin (M24) unblocks distribution |
| 9 | **Speech out not pinned** (Kokoro hashes null) | File CR-006; P8 waits; text first |
| 10 | **The guide's directions are outside the replay record** (the CR-003 workaround) | Accept for the prototype, file CR-003 |
| 11 | **Word counts from a second analysis** can disagree with the verdict (CR-002) | Count only catalogue nouns; file CR-002 |
| 12 | **The known rule may be too fast or too slow** | Thresholds in `game/progress.json`; tune in the P9 playtest |
| 13 | **Typed input makes colour too easy**, and does not train speech | Typed counts the same until P5; then a setting, default half credit |
| 14 | **Is it fun?** The concept could feel like a chore if language gates play | Nothing is gated in the prototype: unknown objects work normally, and colour is the reward. Judge fun at the P9 playtest with a short checklist (time to first colour, words known after 20 minutes, did the player ask the guide unprompted) |
| 15 | **Disk.** A 3 GB pack per package worktree, about 11 GB free | One package milestone at a time; simulation milestones in parallel |
| 16 | **Bundle host files** (CR-007) not confirmed by a run | P4 adds `game/nouns.json` and runs `scripts/content.sh validate` first; if the validator refuses it, the catalogue lives under `content/game-v1-host/` outside the bundle until CR-007 lands |
| 17 | **MonoGame on .NET 10** not officially stated (its package is `net8.0`) | P2's first step is a DesktopGL template project built with SDK 10.0.402; fall back to `net9.0` for the front-end alone if needed |
| 18 | **Free conversation with the guide** (questions outside the authored intents) | Out of the prototype. Unknown input gets `not_understood` and the repair ladder, plus a help line listing what the guide can answer. The long-term route is the core's M23 and M25, both open there (core D8, D11) |

Questions for the owner, each with the recommendation above:

1. D-game-1 for the prototype (row 1).
2. CC0 only while unlicensed (row 7, D-game-5).
3. The known rule (row 12, D-game-6).
4. Whether typed input earns colour (row 13).
5. No gating in the prototype (row 14).
6. Vosk now, Parakeet after M24 (row 4).
7. P1 and P2 alongside G1 and G2 (section 9).

## 11. Sources

Core sources, at gitlink `db39999` in `external/Vocaterra.NLP`:

- documents: `docs/api.md`, `docs/pipeline.md`, `docs/lexicon.md`, `docs/PACKAGING.md`, `docs/decisions.md` (D2, D8, D11),
  `docs/adr/0009-core-and-game-boundary.md`, `docs/research/2026-10-20-scaling-approaches.md` (sections 3.4, 6), `tools.md`, `high-level-docs.md`
  (performance budget), `models/manifest.json`, `implementation-plan.md` (milestones M20 to M25);
- source files: `src/Vocaterra.Nlp.Content/ContentBundle.cs` and `ContentLoader.cs`, `src/Vocaterra.Nlp.Pipeline/PipelineResult.cs`,
  `ConversationContext.cs` and `UtterancePipeline.Response.cs`, `src/Vocaterra.Nlp.Decision/Learning/LearnerModel.cs`,
  `src/Vocaterra.Nlp.Audio/PushToTalkSession.cs`, `src/Vocaterra.Nlp.Response/Speech/KokoroSynthesizer.cs`,
  `src/Vocaterra.Nlp.Asr/Models/ModelStore.cs`, `src/Vocaterra.Nlp.Language/Lexicon/Lexicon.cs`.

Game sources: `implementation-plan.md`, `docs/architecture.md`, `docs/decisions.md`, `docs/CORE_REQUESTS.md`, `src/Vocaterra.Host`,
`content/game-v1` (lexicon checked for the world nouns).

Web sources, retrieved on 2026-10-21. Each URL is cited where its fact is used.

- **MonoGame:**
  - [releases](https://github.com/MonoGame/MonoGame/releases)
  - [DesktopGL on NuGet](https://www.nuget.org/packages/MonoGame.Framework.DesktopGL)
  - [3.8.5 release post](https://monogame.net/blog/2026-07-15-3.8.5-release-2026/)
  - [Ubuntu setup](https://docs.monogame.net/articles/getting_started/1_setting_up_your_os_for_development_ubuntu.html)
  - [Content Builder announcement](https://monogame.net/blog/2025-11-06-MonoGame-Content-Builder/) and
    [docs](https://docs.monogame.net/articles/getting_started/content_pipeline/content_builder_project.html)
  - [shader tutorial](https://docs.monogame.net/articles/tutorials/building_2d_games/24_shaders/index.html)
  - [Microphone how-to](https://docs.monogame.net/articles/getting_to_know/howto/audio/HowTo_Microphone.html) and
    [api](https://docs.monogame.net/api/Microsoft.Xna.Framework.Audio.Microphone.html)
  - [FontImporter](https://docs.monogame.net/api/Microsoft.Xna.Framework.Content.Pipeline.FontImporter.html)
  - [licence](https://github.com/MonoGame/MonoGame/blob/develop/LICENSE.txt)
  - forum threads [1](https://community.monogame.net/t/cant-get-microphone-to-work-properly-in-monogame-3-8-1/19332) and
    [2](https://community.monogame.net/t/microphone-in-3-7/10280)
- **Audio libraries:** [PortAudioSharp2](https://github.com/csukuangfj/PortAudioSharp2),
  [SDL_OpenAudioDevice](https://wiki.libsdl.org/SDL2/SDL_OpenAudioDevice), [OpenAL Soft](https://github.com/kcat/openal-soft),
  [NAudio.Linux.Alsa](https://www.nuget.org/packages/NAudio.Linux.Alsa), [FontStashSharp](https://github.com/FontStashSharp/FontStashSharp)
- **Godot:**
  - [GodotSharp on NuGet](https://www.nuget.org/packages/GodotSharp)
  - [4.8 beta 1](https://godotengine.org/download/archive/4.8-beta1)
  - [C# basics](https://docs.godotengine.org/en/stable/tutorials/scripting/c_sharp/c_sharp_basics.html)
  - [canvas_item shaders](https://docs.godotengine.org/en/stable/tutorials/shaders/shader_reference/canvas_item_shader.html)
  - [AudioEffectCapture](https://docs.godotengine.org/en/stable/classes/class_audioeffectcapture.html)
  - [.NET 10 forum thread](https://forum.godotengine.org/t/net10-and-c-14/133412) and
    [proposals discussion](https://github.com/godotengine/godot-proposals/discussions/13076)
- **World generation:** [Terraria world creation](https://terraria.wiki.gg/wiki/World_creation),
  [Terraria layers](https://terraria.wiki.gg/wiki/Layer), [Red Blob Games](https://www.redblobgames.com/maps/terrain-from-noise/),
  [RogueBasin cellular automata](https://www.roguebasin.com/index.php/Cellular_Automata_Method_for_Generating_Random_Cave-Like_Levels),
  [RogueBasin random walk](https://www.roguebasin.com/index.php/Random_Walk_Cave_Generation),
  [gridbugs](https://gridbugs.nfshost.com/cellular-automata-cave-generation/),
  [FastNoiseLite](https://github.com/Auburn/FastNoiseLite/tree/master/CSharp)
- **Noun Town:** [Steam](https://store.steampowered.com/app/2313720/Noun_Town_Language_Learning/),
  [Coto Academy review](https://cotoacademy.com/noun-town-review-should-you-learn-japanese-in-vr/),
  [developer blog](https://noun.town/blog/how-noun-town-teaches-languages-through-play/)
- **sherpa-onnx:** [NuGet](https://www.nuget.org/packages/org.k2fsa.sherpa.onnx),
  [issue #3731](https://github.com/k2-fsa/sherpa-onnx/issues/3731),
  [Spanish TTS models](https://k2-fsa.github.io/sherpa/onnx/tts/all/Spanish/index.html),
  [Kroko model card](https://huggingface.co/csukuangfj/sherpa-onnx-streaming-zipformer-es-kroko-2025-08-06)
- **Assets:** the Kenney, OpenGameArt, itch.io, DaFont and Freesound pages linked in section 8.

**Not verified:**

- whether MonoGame DesktopGL builds and runs under SDK 10.0.402;
- whether MonoGame's `Microphone` works on current Linux;
- whether SDL capture coexists with MonoGame;
- the Spanish glyph coverage of m5x7 and Pixel Operator;
- whether NuGet refuses a missing local feed folder for a project without core packages;
- whether the content loader accepts a host-owned bundle file in a real validate run;
- the speaker ids of Kokoro's Spanish voices;
- every latency figure, which are the core's targets, not measurements.

All of these are first steps of the milestone that needs them.
