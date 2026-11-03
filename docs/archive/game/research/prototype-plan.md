> **Archived 2026-11-03** from `docs/game/research/prototype-plan.md`: the 2D slice's plan, retired by D-game-8. Its research: [2026-10-21-prototype-research.md](../../../research/2026-10-21-prototype-research.md); the current design: [the prototype brief](../../../game/design/prototype-brief.md).

> **Historical (R1, the 2D era).** This document describes the 2D Terraria-inspired slice (P1 to P7), retired by the prototype brief (D-game-8, 2026-10-21). The current design is `docs/PROTOTYPE_BRIEF.md`; its plan is `docs/scenario-plan.md`.

# Prototype plan: the Terraria-inspired slice (proposal from research milestone R1)

This is the short form of [PROTOTYPE_RESEARCH.md](../../../research/2026-10-21-prototype-research.md), written so the orchestrator can paste it into
`implementation-plan.md` section 4, `docs/decisions.md` and `docs/CORE_REQUESTS.md`. Nothing here is decided until the orchestrator and the
owner accept it. The research document gives the reasons and the sources.

## What the prototype proves

1. The language core works inside a real game loop: a player talks to a guide in Spanish (typed, then spoken), the core evaluates the line
   through the `Vocaterra.Nlp.*` 0.1.0 packages, and the world reacts (directions, help, naming things).
2. The concept can be fun: a small Terraria-like forest with caves, mining, building and an inventory, where every object whose Spanish noun
   the player has not learnt is drawn in greyscale and gains colour as the player says its name correctly (the Noun Town idea).

## Decisions the orchestrator records first

| Proposed id | Decision | Proposed status |
| --- | --- | --- |
| D-game-1 | Engine: **MonoGame 3.8 DesktopGL for the prototype**, in a new project `src/Vocaterra.MonoGame`; Godot 4 .NET re-assessed after P10 (a spike). Engine-agnostic code goes in two new libraries, so the move costs only a front-end. | decided for the prototype (the owner's words in the R1 brief); the long-term engine stays open |
| D-game-5 | Art, font and sound sources: CC0 only for anything committed while D-game-2 is open (Kenney packs, CC0 OpenGameArt sets, a CC0 font; per-file record in `assets/ATTRIBUTION.md`; see the research document, section 8) | open, recommended |
| D-game-6 | When a noun counts as known: three correct spoken uses in three separate turns; colour grows a third per use; no decay in the prototype | open, recommended |

Rule changes that follow from D-game-1: section 3 rule 3 ("No engine code yet") becomes "engine code only in `src/Vocaterra.MonoGame` (and later a
Godot front-end); never in `src/Vocaterra.Host`, `src/Vocaterra.Simulation` or `src/Vocaterra.Game`". G5 is replaced by P2.

## Project layout

```
src/Vocaterra.Simulation/   engine-agnostic, no core packages, no engine: world grid and chunks, seeded generation, physics, mining,
                            building, items, inventory, lighting, the fixed-tick step driven by player commands
src/Vocaterra.Game/         engine-agnostic, references Simulation and the Vocaterra.Nlp.* packages: game session, guide turns, world
                            facts for the guide, the noun catalogue, word progress, save files, the audio-capture and audio-out interfaces
src/Vocaterra.MonoGame/     the MonoGame DesktopGL front-end: window, rendering, greyscale, input to commands, microphone, playback, UI
src/Vocaterra.Host/         unchanged: the console host (later it can drive Vocaterra.Game's guide turns as a text front-end)
tests/Vocaterra.Simulation.Tests/   headless, no packages: generation goldens and properties, physics, mining, building, inventory
tests/Vocaterra.Game.Tests/         headless, with the packages: guide turns with the scripted recognizer, word progress, catalogue coverage
content/game-v1/            gains the guide (agent, intents, prompts, dialogue), lexicon/world.json and game/nouns.json
```

## Milestones

Sizes: **S** about one agent session, **M** two to three, **L** more. Models follow the owner's policy: Haiku 5.5 by default, Sonnet 5.5 only
where the milestone fixes an API or an architecture other milestones build on.

| ID | Milestone | Deliverable | Demo | Test approach | Model | Depends on | Size |
| --- | --- | --- | --- | --- | --- | --- | --- |
| G1 | Adopt the core's M19d API (already planned) | as in plan section 4 | as planned | as planned | Haiku 5.5 | G0 | S |
| G2 | Re-sync the bundle (already planned); decide D-game-4 | as in plan section 4; the generic intents `thank`, `farewell`, `confirm`, `deny` and prompt `text` that the guide reuses | as planned | as planned | Haiku 5.5 | G1 | S |
| P1 | Simulation skeleton and surface world | `Vocaterra.Simulation` (targets `net8.0;net10.0`, no packages): `WorldRng` (SplitMix64), value noise, tile grid in 32 x 32 chunks, heightmap terrain, dirt and stone, grass, trees, spawn point; `Simulation.Step(commands)` at a fixed 60 Hz tick; player body with gravity, walking, jumping and tile collision | `dotnet test` prints the golden hash of seed 42; an ASCII map dump of a 120 x 60 window in the test output | goldens (same seed gives the same SHA-256 of the tile arrays), properties (spawn on the surface, no floating trees, the player lands and stops), scripted command sequences | Sonnet 5.5 (fixes the simulation API every later milestone uses) | D-game-1 | M |
| P2 | MonoGame front-end skeleton | `Vocaterra.MonoGame` (MonoGame 3.8.6 DesktopGL, `net10.0`), no content pipeline (PNG through `Texture2D.FromStream`, TTF through FontStashSharp), placeholder tiles drawn in code, camera, keyboard and mouse to `PlayerCommand`, fixed-tick accumulator, a CC0 font with á é í ó ú ü ñ ¿ ¡ | `dotnet run --project src/Vocaterra.MonoGame -- --seed 42`: walk and jump through the forest | the front-end only builds; all logic stays in P1's tested code; a test that the font's character set covers the Spanish letters | Sonnet 5.5 (the engine host's architecture) | P1 | M |
| **P3** | **Smallest vertical slice: the guide, typed** | `Vocaterra.Game`: `GameSession`, `GuideService` over the packages (scripted recognizer: typed text), `WorldFacts` (nearest tile of a kind, direction and distance in Spanish words); the guide in `content/game-v1` (agent `guide`, intents `ask_directions`, `ask_what`, `ask_help` plus the generic ones, prompt `guide-open`, reply and host-line pools); a dialogue panel with text entry in MonoGame | walk to the guide, type `¿dónde está el árbol?`; the guide answers with a recast if needed and `El árbol está a la derecha, a doce bloques.`, and an arrow points there | `Vocaterra.Game.Tests` on a fixed seed and fixed stored time: the verdict, the agent state, the host line and the arrow target; `cefr-check` A1/A2 clean; the bundle validated and hashed | Sonnet 5.5 for the guide scene and the session API, Haiku 5.5 for the content checklist | G1, G2, P2 | M |
| P4 | Nouns, word progress and greyscale | `game/nouns.json` (tile and item ids to lemma), `lexicon/world.json` (missing nouns), `NounCatalog` (article, gender, plural through `bundle.Lexicon` and `bundle.Morphology`), `WordProgress` (heard, read, spoken, spoken correctly, per lemma), the known rule (D-game-6), a grey atlas made on the CPU at load (no shader, so no Wine on Linux), drawn under the colour atlas at alpha = saturation per lemma, the Kenney Pixel Platformer tiles (CC0), the `name_object` intent ("es un árbol") with the cursor's target, a journal screen and a "new word" toast | point at a grey tree, type `es un árbol`: trees turn a third coloured; after three turns they are in full colour and the journal shows `el árbol` known | catalogue coverage (every tile and item has a lemma with a noun reading and a gender, or is marked colourless); progress counting from scripted verdicts, including `la árbol` (no credit) and `arbol` without the accent; saturation per lemma | Haiku 5.5 | P3 | M |
| P5 | Spoken input | `IAudioCapture` in Vocaterra.Game, a microphone implementation in the front-end (MonoGame's OpenAL `Microphone` first, PortAudioSharp2 if it distorts; research section 3.3), push-to-talk on a key, `PushToTalkSession` and `Resampler.To16k` from `Vocaterra.Nlp.Audio`, Vosk (`vosk-model-small-es-0.42`, already pinned) for the guide, evaluation on a worker thread, the transcript shown | hold V, say `¿dónde está la cueva?`, release: the transcript and the reply appear in under half a second on the dev machine | a model-gated test (needs `VOCATERRA_MODELS_DIR`) feeding a recorded WAV through capture-free code; the turn's latency logged; a replay log (`ReplayLog`) of the turn | Sonnet 5.5 (audio threading and the capture choice) | P3; CR-008 answered helps | M |
| P6 | Mining, building, inventory | tile durability and tool power, mining progress, drops into the inventory, placing from the hotbar within reach, 40-slot inventory with a 10-slot hotbar and stacks, a basic axe and pickaxe | dig down, collect dirt and stone, chop a tree for wood, build a small hut | unit tests for every rule; a scripted sequence: mine 10 dirt, place 10, the world hash returns to the start | Haiku 5.5 | P1, P2 | M |
| P7 | Caves, ores, full forest, light | cave passes (worm tunnels from the surface, cellular-automata caverns), ore veins, bushes, flowers, mushrooms, tall grass, flood-fill lighting with sunlight | the guide answers `¿dónde está la cueva?` with the nearest entrance; caves are dark until lit | goldens and properties per pass (cave entrances reach the surface, cavern fill ratio within bounds, ore counts per seed); lighting tests | Haiku 5.5 | P1, P6 | M |
| P8 | The guide speaks | `IAudioOut` and playback of `AudioBuffer`; Kokoro through the core when CR-006 lands, with `CachedSynthesizer`; until then text only | the guide's reply is heard after it is shown | the synthesis call through the silent synthesizer in tests; a model-gated smoke test | Haiku 5.5 | P5, CR-006 | S |
| P9 | Save and load, playtest build | save files (world diff per chunk, player, inventory, word progress, learner model, repeat counts, agent states, stored time), the help pool by game state, a playtest checklist for the owner | quit and resume with the same world, colours and inventory | round-trip tests; a save written twice is byte-identical | Haiku 5.5 | P4, P6, P7 | M |
| P10 | Godot spike (optional) | a throwaway Godot 4 .NET project that references `Vocaterra.Simulation` and `Vocaterra.Game`, draws the world and runs one typed guide turn; a findings note with the measured cost of the move | the same seed and the same guide turn in Godot | the libraries' tests unchanged; the note | Sonnet 5.5 | P4; the net10.0 question (CR-009) | S |

**Smallest vertical slice:** P3. It needs only a walkable world (P1, P2), the packages (G1, G2) and the scripted recognizer, so no model file,
no audio and no new core call. It proves the integration in-game: the core evaluates a line typed in the game, the agent state machine moves,
the feedback and reply are shown, and a host event changes something in the world (the arrow). P5 adds the microphone on the same turn
flow; nothing above it changes.

**Relation to G1 to G5.** G1 and G2 stay first: P3 uses `ConversationContext.Next`, `RepeatCounts.Next`, `PromptSpec.Text` and the generic
intents that G2 brings in. P1 and P2 do not use the packages, so they can run before or alongside G1 and G2 (they also need no 3 GB pack in
their worktrees, see the research document, section 3). G3 and G4 (innkeeper, quest giver) move after P9: the guide is the first new
character, and it reuses G3's planned `ask_directions` intent. G5 is replaced by D-game-1 (above) and P2.

## Proposed rows for docs/CORE_REQUESTS.md

The rows below continue after CR-001. Each names the call or field in the core's terms and the game reason; each has a workaround through
the 0.1.0 packages, so no prototype milestone is blocked except P8's speech out (CR-006).

| id | request | why | priority |
| --- | --- | --- | --- |
| CR-002 | Expose the analysis on the result: `PipelineResult` (or `PipelineTrace`) carries the normalized tokens with their spans and the reading the resolver chose (lemma, part of speech, gender, number), with names (`[WORD]`) and unknown words marked | Word-level spoken counts and the noun colour rule (P4). Re-running `TextNormalizer` and `LinguisticAnalyzer` in the game copies the pipeline's options and skips the reading resolver's grammar step, so its lemmas can disagree with the verdict's error spans | should |
| CR-003 | Host-supplied slot values in response planning: an optional `SlotMap` of host facts on `Evaluate` (or on `ConversationContext`), usable by reply and feedback templates (`{target:article(def)}`, `{direction}`, `{distance:words(m)}`), copied into the replay record and `HostStateHash` | The guide's answers name world facts (where the nearest tree is, what the cursor points at) that no verdict slot holds (P3). Workaround today: the game renders a second line itself with `bundle.TemplateEngine.Render`, `bundle.Dialogue.TryGetPoolById` and `SeededRng.ForInteraction`, outside the replay record | should |
| CR-004 | Expose the answer match on the result: `MatchResult` (answer id, type, distance, likely pronunciation) on `PipelineResult` | Quiz-style prompts ("say the name of this") need to know which answer the player gave. Workaround: an intent grammar `[ART]? [NOUN:object]` whose slot carries the lemma | could |
| CR-005 | Host-named skills in the learner update: let the host add exercised skill ids (for example `word.árbol`) to an utterance's BKT step, or a `LearnerModel` call that steps named skills without re-applying the verdict's error families | Per-noun mastery (BKT) to drive colour and review after the prototype; today `LearnerModel.Update` re-applies the error families, so a second call double-counts | could |
| CR-006 | Speech-out readiness: pin the file hashes of `kokoro-multi-lang-v1_0` (and `sherpa-onnx-nemo-parakeet-tdt-0.6b-v3-int8`) in `models/manifest.json`, add `KokoroSynthesizer.FromStore`, and document the speaker ids of Kokoro's Spanish voices | The guide speaks (P8); open-mode voice prompts later. Today only Vosk is fetched and pinned, and `ModelStore.ModelHash` refuses unpinned files. Private builds only (core D2) | should |
| CR-007 | Bundle files owned by the host: confirm and document that a bundle may hold files the loader does not parse (for example `game/nouns.json`), hashed by the manifest and listed in provenance, and expose their verified bytes on `ContentBundle` (for example `TryReadHostFile(path)`) | The noun catalogue belongs in the content hash so replays and saves name the exact catalogue (P4). Reading the file from disk after `ContentLoader.Load` works but is not covered by the load's hash check | should |
| CR-008 | Threading and load-cost contract: document whether one `UtterancePipeline` (with Vosk or sherpa-onnx recognizers) may be called from a worker thread while `Synthesize` runs on another, whether recognizers may be shared, and when models load (constructor or first call) | The game never blocks its frame: evaluation runs on a worker, models preload behind a loading screen (P5) | should (documentation) |
| CR-009 | Multi-target the packages for `net8.0` as well as `net10.0` | A Godot 4 .NET front-end must reference the packages. GodotSharp 4.7 and 4.8 ship `net8.0` only; retargeting a Godot project to `net10.0` works by user reports but is unsupported (research section 3.4). File it if the P10 spike fails on `net10.0` | conditional (after P10) |

Not requested: free-form question answering for the guide (the prototype authors its intents; the core's M23 classifier and M25 tutor are
the long-term path), CEFR level per lemma (`bundle.Cefr` already gives the A1 and A2 lemma sets), lemma, gender and article lookups
(`bundle.Lexicon.Lookup`, `bundle.Morphology.Article`), and the missing world nouns (`carbón`, `arbusto`, `seta`, `tronco`, `bloque`, `mochila`,
`bellota`, the noun `cobre`), which the game adds in its own `lexicon/world.json`.

## Open questions for the owner (recommendations in the research document, section 10)

1. Confirm D-game-1 as "MonoGame for the prototype, Godot re-assessed after P10". Recommended.
2. Accept CC0-only assets while D-game-2 is open (D-game-5). Recommended.
3. The known rule (D-game-6): three correct spoken uses in three turns, colour in thirds, no decay. Recommended for the prototype.
4. Should unknown objects be usable at all? Recommended: yes, colour is the reward and nothing is gated, so the fun can be judged on its own.
5. Should typed input count as "spoken" for the colour rule? Recommended: typed counts the same until P5 lands, then a setting (default: typed
   counts half).
6. Voice in: Vosk small (pinned now) first, Parakeet after the core's M24 speech check. Recommended.
7. Order: run P1 and P2 in parallel with G1 and G2, or after them. Recommended: alongside, because P1 and P2 do not touch the packages.
