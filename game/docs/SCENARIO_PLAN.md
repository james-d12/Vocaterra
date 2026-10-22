# The prototype scenario plan (the S series)

Written 2026-10-21 from the owner's Prototype Design Brief v1.2 (`Vocaterra_Prototype_Design_Brief.docx`, this folder) and a review of the
codebase as it stands after P7 and D-game-7 (one repository, 3,585 tests, bundle `game-v1` at `fa8fb030…`). It is the orchestrator's entry point
for the next phase: **run S0 first** (it folds this document into `IMPLEMENTATION_PLAN.md` section 4 and records the decisions), then launch
S1 and S5 in parallel. S0 is done (2026-10-21): the plan of record is `game/IMPLEMENTATION_PLAN.md` section 4, with this file as the per-milestone design; the milestone and decision ids below are reserved.

## 1. Why

The brief replaces the Terraria design doc (`Vocaterra_Design_Doc.odt`, deleted). It asks for one compact, end-to-end scenario of 10 to 15 minutes
that tests whether a survival-and-exploration objective makes beginners practise spoken Spanish while still feeling like a game: a
Minecraft-inspired voxel world, a handcrafted forest-to-village-to-river route, gather wood and berries, one craft at a workbench, two or three
NPCs with a small dialogue graph, five target phrases, forgiving survival (no death, no timers, no language gate), a speech UX that never traps
the player, phrase-level tracking of supported versus unaided use, pre, post and delayed-recall checks, minimal telemetry, and every line checked
by a Spanish reviewer before testing.

The codebase is a 2D Terraria side-scroller (P1 to P7: procedural world, caves, ores, lighting, mining, 13 recipes, a static guide, typed and
spoken turns, noun colour with spaced credit). The language core (`nlp/`) already provides everything the scenario needs: constrained
recognition from an answer set (the Vosk grammar), content-driven agent state machines with host-initiated turns (`UtterancePipeline.Advance`,
CR-015), two-phase evaluation with host facts (`Analyze` then `Complete`, CR-017), host-named skills (`phrase.<id>` through `extraSkills` and
`LearnerModel.StepSkills`), a repair ladder that ends in offered choices (`RepairStrategy.OfferChoices`), pre-rendered speech by line id
(`StaticLineStore`), Kokoro with a `speed` parameter, the replay log, and the content lint (M27a). No core change is required to start; two
possible gaps are listed in section 6 and are filed only if they turn out real.

**Owner's decisions (2026-10-21, answered while this plan was made):** 3D voxel in MonoGame (D-game-1 kept); first person; the world in full
colour with the counts, the journal and the spaced review kept, and the grey atlas behind a switch.

## 2. The brief against the codebase

| Brief | Today | Change |
| --- | --- | --- |
| Voxel 3D world, first person, handcrafted route (clearing, path, stall, houses, riverbank, workbench), no procedural terrain | 2D side view, procedural generator (v4), caves, ores, light map | New 3D voxel simulation and renderer; a hand-authored scene; generator, caves, ores and lighting retire from the prototype (code tagged, then removed) |
| Gather wood and berries; one craft (torch or camp item); minimal inventory | Axe and pickaxe mining, 13 recipes, 40 slots | Punch trees and berry bushes; one recipe (`antorcha` at the `mesa`, from wood); recipes trimmed; hotbar kept |
| 2 to 3 NPCs with a small dialogue graph, several valid answers | One static guide; a blacksmith agent in the bundle with no NPC | Shopkeeper (stall) and villager (directions) agents and figures; the guide stays at the camp; one `NpcService` for all agents |
| "¿Qué necesitas?" / "Quiero agua, por favor." / "¿Quieres pan?" / "Sí, por favor." / "No, gracias." / "¿Dónde está el río?" | `buy_item`, `confirm`, `deny`, `ask_directions` exist; no `río`, `agua`, `pan` content; blacksmith prompts | `request_item` intent and `supplies` answer set, stall and villager prompts (constrained), `río` as a world noun; a phrase sheet as source of truth |
| ASR only for intent in a constrained phrase set; uncertain recognition asks for help or a retry, never a failure | Open-mode Vosk, single best (confidence 1.0, CR-022); not-communicated gets "Habla más despacio" | NPC prompts in `Constrained` mode; a game-side uncertainty rule on the per-word mean; reworded `not_understood`; help instead of slow-repeat |
| Mic check, visible states, cancel, timeout, persistent heard-vs-expected, replay and slower audio, text and button fallback, "I don't know" | States and transcript shown; 8 s cap; typed fallback; `ask_help` for the guide only; no NPC audio | Mic-check screen, cancel and watchdog, a persistent heard/expected line, buttons (repeat, slower, help, no sé, type), choices from the repair ladder, pre-rendered NPC lines |
| Track recognition, supported production, unaided recall and transfer separately; no universal mastery model; scaffold then fade | Noun colour with spaced credit (D-game-6); no phrase tracking | `PhraseProgress` with a support level that fades; counts kept; colour no longer drives the world |
| World-wide greyscale: leave for later; colour only as discovery feedback | Every unnamed noun drawn grey | Full colour; `--greyscale` switch for an A/B; a discovery highlight |
| Transfer moment; next-day recall; pre and post checks | None; no saves | A scripted transfer step, save and sessions (P9 re-scoped), a `--check` battery |
| Minimal telemetry | Per-turn timings printed; the core's replay log unused | A session log (JSONL) plus the core's replay log; a report script against the decision gates |
| Content trust: phrase sheet, reviewer, one Spanish variety | Lint and cefr-check exist; no sheet; no reviewer available | `content/PHRASES.md`, lint `--strict` as a gate, Latin American neutral (core ADR 0005); the speaker's check is deferred with the interim workaround of D-game-12 (provisional rows) |
| Original voxel art direction, CC0 | Code-drawn 2D placeholders, CC0 font | Code-drawn block atlas and cube figures first (CC0 by construction); a CC0 texture pass later if wanted |

## 3. Decisions to record in `DECISIONS.md` (S0)

- **D-game-8, the prototype brief adopted (owner, 2026-10-21).** The brief defines the prototype's scope, scenario, guardrails and gates. World:
  3D voxel, first person, MonoGame (D-game-1 unchanged; engine code only in `src/Vocaterra.MonoGame`). The 2D Terraria slice is parked: its code
  is tagged `2d-slice-p7` and removed from `main` once the 3D world reaches gathering and crafting (S4). Spanish variety: Latin American neutral
  (core ADR 0005). Nothing in the game is gated behind language or voice; every failure has a recovery path.
- **D-game-6 amended (owner, 2026-10-21).** The world is drawn in full colour. The counts (heard, read, said, said correctly), the journal, the
  typed-half and spoken-full credit and the spaced gaps stay and now also serve phrases (the delayed-recall check). The grey atlas stays behind
  `--greyscale` for an A/B in the playtest. Colour never fades.
- **D-game-9, speech rules (from the brief).** ASR is used for intent in a constrained phrase set; an uncertain recognition (not communicated, or a
  per-word confidence mean under the threshold) asks for a retry with help and is logged as a voice event, never as a language failure; a
  one-time mic check; visible recording and thinking states; cancel and a timeout; a persistent "oí / esperaba" line; replay and slower audio;
  typed and button fallback; "no sé" always available and never shamed.
- **D-game-10, learning measurement (from the brief).** A hand-authored phrase sheet is the source of truth; each phrase is tracked through
  shown-card, supported, unaided, choice-only (a recognition signal) and "no sé" uses; no automated CEFR placement or global score; a pre-check,
  a post-check and a later recall check; telemetry local only (JSONL in the session folder).
- **D-game-11, the Noun Town review data (decided, owner, 2026-10-21).** The raw Steam review JSON (450 KB of third-party text) is never
  committed: `.gitignore` covers `scripts/noun_town/*.json` and `docs/research/noun-town/*.json` (done the same day). The two summaries and the
  fetch scripts are committed and move to `docs/research/noun-town/` in S0.
- **D-game-12, the Spanish reviewer (deferred by the owner, 2026-10-21; a workaround is needed).** The brief wants every phrase, translation,
  accepted response and recording checked by a competent Spanish speaker before testing; none is available yet. Interim workaround until a
  speaker checks the sheet: (1) the phrase set stays at the brief's five phrases plus the five service words, all textbook A1, so a later
  one-hour check closes the gap; (2) every row passes the core's lint `--strict` and `cefr-check` at A1 (automated and supplementary, never
  approval); (3) an LLM review pass at authoring time (Opus, on the owner's go, run by the orchestrator in S5) checks naturalness, Latin American
  neutral usage and the English meaning of every row and every accepted variant, and its verdict is recorded in `PHRASES.md` as
  `reviewer: llm-opus (provisional)` with the date; (4) the owner listens to every pre-rendered WAV in S9 for cut-offs and text-audio
  mismatches; (5) the playtest checklist marks the content-trust gate provisional and testers are told the Spanish has not been checked by a
  native speaker. Rows marked provisional are re-checked when a speaker is found; nothing else waits on this.

## 4. Milestones

New series `S<nn>` (branch `S<nn>-<name>`, commit prefix `S<nn>:`). P8 (the guide speaks), P9 (save and load), G3 (innkeeper) and G4 (quest giver)
are superseded by the rows below; P10 (the Godot spike) stays optional and later. Models follow the orchestrator's policy: Sonnet 5.5 for the
design-heavy rows, Haiku 5.5 otherwise; Opus only on the owner's request. One agent per milestone; S5 runs beside S1 to S3 because their folders
are disjoint (`content/` against `src/Vocaterra.Simulation` and `src/Vocaterra.MonoGame`).

| ID | Milestone | Model | Depends on | Size |
| --- | --- | --- | --- | --- |
| S0 | Plan and docs (the orchestrator, no agent) | — | this document | S |
| S1 | 3D voxel simulation, headless | Sonnet 5.5 | S0 | M |
| S2 | First-person voxel front-end | Sonnet 5.5 | S1 | M |
| S3 | The scenario world and the guide in 3D | Haiku 5.5 | S2 | M |
| S4 | Gather, craft, the camp objective; retire the 2D slice | Haiku 5.5 | S3 | M |
| S5 | Content: the phrase sheet, the stall and the villager | Sonnet 5.5 (design), Haiku 5.5 (content) | S0 | M |
| S6 | NPC exchanges in the world, the transfer moment | Sonnet 5.5 | S3, S5 | M |
| S7 | Speech UX: mic check, cancel, heard-vs-expected, fallbacks, constrained prompts | Sonnet 5.5 | S6 | M |
| S8 | Phrase progress and fading scaffolds; the review extended to phrases | Sonnet 5.5 | S6 | M |
| S9 | NPCs speak: pre-rendered Kokoro lines in the bundle | Haiku 5.5 | S5, S6 | M |
| S10 | Save, sessions, resume (P9 re-scoped) | Haiku 5.5 | S4, S8 | M |
| S11 | Checks (pre, post, recall), the session log and the report | Haiku 5.5 | S8, S10 | M |
| S12 | Playtest build and facilitator checklist | Haiku 5.5 | S7, S9, S11 | S |

Every milestone ends with the gate in `IMPLEMENTATION_PLAN.md` section 6 and the orchestrator's behaviour spot-check (the demo line run, the
screenshot looked at). Sizes: S about one agent session, M two to three.

### S0: plan and docs (the orchestrator; no code, no agent)

1. `DECISIONS.md`: the rows and sections of section 3 above.
2. `docs/PROTOTYPE_BRIEF.md`: the brief's text as Markdown so agents can read it (the docx stays as the source; `pandoc` is not installed, so
   extract with a Python standard-library script over `word/document.xml`: paragraphs, headings and tables; the three concept images stay inside
   the docx). `docs/SPECIFICATION.md` becomes a pointer to the brief with the one-paragraph premise.
3. `IMPLEMENTATION_PLAN.md`: section 1 (the decisions), section 2 (the layout after S4), section 3 rule 8 ("nothing is gated behind language or
   voice; every failure has a recovery path"), section 4 (this series as the table and a new 4.4 "The prototype scenario"; P8, P9, G3, G4 marked
   superseded by S9, S10, S5/S6; P10 optional, later), section 5 (one finding line pointing here), section 6 (the demo lines of S2, S3, S6).
4. `../nlp/IMPLEMENTATION_PLAN.md` §5: M29a and M29b as `proposed, conditional` (section 6 below).
5. `../README.md` (Terraria to Minecraft-inspired voxel), `README.md` (this folder: the scenario, the new run lines as they land),
   `../CLAUDE.md` (facts: the brief, 3D first person, the S series, the content gate with lint `--strict`), `../.claude/agents/orchestrator.md`
   (read-first list gains the brief and this plan; commit prefix `S<nn>:`).
6. `../scripts/noun_town/` to `docs/research/noun-town/` (the summaries and the fetch scripts only; the raw JSON is already git-ignored,
   D-game-11). `docs/research/PROTOTYPE_PLAN.md` and `PROTOTYPE_RESEARCH.md` get a note at the top: they describe the 2D era (R1).
7. One commit with the brief and the deleted `.odt`, pushed.

### S1: 3D voxel simulation, headless (Sonnet 5.5, M)

In `src/Vocaterra.Simulation`, a new namespace `Vocaterra.Simulation.Voxel` beside the 2D code (which stays until S4 so `main` keeps working):

- `BlockId : ushort`, append-only like `TileId`: air, grass, dirt, stone, sand, trunk, leaves, bush, berry bush, water, path, planks, door, table
  (the workbench), torch, stall, roof, fence, wall. `BlockDefinition`: solid, gather ticks, drop item.
- `VoxelGrid` in 16³ chunks; x east, y up, z south; bounded, out of bounds solid; a digest per chunk and for the world. A `Scene` built by a
  deterministic `SceneBuilder` from a hand-authored layout file, `content/game-v1/game/scene.json` (a top-down ASCII height and feature map plus
  placed props and NPC positions). Tree scatter from `WorldRng` seeded by the world seed, kept clear of the route. No noise terrain, no caves, no
  ores, no light map.
- `PlayerBody3D`: AABB 0.6 x 1.8 blocks, eye height 1.62, integer units of 1/4096 block (as the 2D body), gravity, walk, jump, one-block step-up,
  facing yaw; commands `Move(dx, dz)`, `Look(yaw)`, `Jump`, `Interact(x, y, z)`, `Place(x, y, z)`, `SelectSlot`, `Craft`. `BlockRay` (DDA) for
  picking from the eye, reach 4 blocks. Reused as they are: `WorldRng`, `ValueNoise`, `Inventory`, `ItemDefinition`, `Recipe` and crafting,
  `SimulationEvent`, the fixed 60 Hz tick, `DeterminismRulesTests` (banned tokens).
- `AsciiMap3D` (a top-down and a side slice) for tests and the report; a seed-42 scene golden; physics properties (lands and stops, step-up,
  cannot enter solids, falls off edges); picking tests; the digest changes on every block edit.
- Demo: the test output prints the scene's golden hash and a top-down ASCII map of the route.

### S2: first-person voxel front-end (Sonnet 5.5, M)

In `src/Vocaterra.MonoGame`, the only engine code:

- `ChunkMesher` (culled faces, per-face shade, a code-drawn 16 x 16 block atlas made the way `TileArt` makes tiles), `VoxelRenderer` with
  `BasicEffect` (texture plus vertex colour, depth buffer on, alpha for water; no custom shader, so nothing needs Wine on Linux),
  `FirstPersonCamera` (mouse look, WASD, Space; relative mouse mode; `--look yaw,pitch` for scripts), crosshair, block highlight, a held-item
  sprite, NPC figures as cube assemblies with billboard name labels, the pointer as a beacon column plus an edge arrow when off screen.
- The 2D `IFrameLayer` UI (dialogue panel, toasts, journal, hotbar, inventory, crafting, loading) draws over the scene unchanged; `FrameView`
  gains the camera. Script steps in 3D: `--walk-to x,z`, `--look`, `--interact x,y,z`, `--place item@x,y,z`; `--screenshot` works under
  `xvfb-run -a`; the frame-time line is printed as now.
- The spike risk, verified first and reported: MonoGame 3D on DesktopGL and Linux (depth, texture sampling, alpha, back-buffer read-back).
- Demo: `--seed 42 --walk-to <x,z> --look 90,0 --screenshot <png>` shows the clearing from the player's eyes with the guide's label.

### S3: the scenario world and the guide in 3D (Haiku 5.5, M)

- The layout file for the route: spawn in a forest clearing with the campsite (a `mesa` workbench, an unlit fire ring, the guide), a path
  south-east, the village (a stall with an awning, three houses, a signpost `Tienda / Río`), the riverbank (water blocks, a bridge).
- `WorldFacts3D`: nearest block of a noun by horizontal distance; direction relative to the player's facing (`adelante`, `detrás`, `a la
  izquierda`, `a la derecha` and the combinations); distance in blocks in words. `GuideService`'s world line, host slots and arrow work on it
  unchanged.
- `game/nouns.json` gains the new blocks (`agua`, `río`, `camino`, `casa`, `tienda`, `puente`, the berry bush) and loses the ores; `lexicon/world.json`
  gains what the lexicon lacks. Content note: `baya` is in neither the lexicon nor the A1/A2 lists; `fruta` and `manzana` are the A1
  alternatives. The agent proposes, the reviewer decides in S5. Decided in S5 (provisional review, 2026-10-21): `la fruta` / `las frutas` for the berry item, no noun taught for the bush.
- Demo: `--say "¿dónde está el río?" --screenshot <png>` prints `ask_directions, entendido` and the guide's line with the direction and distance,
  and the beacon stands on the river.

### S4: gather, craft, the camp objective; retire the 2D slice (Haiku 5.5, M)

- Punch a trunk for `madera` (no tool), pick berries from a bush, `antorcha` ← 2 `madera` at the `mesa` (`recipes.json` trimmed to the
  prototype), place the torch at the camp (a flame quad; no light simulation, no night).
- `Objective` in `Vocaterra.Game`: a checklist HUD (`Campamento: madera 5/5 · frutas 3/3 · antorcha 1/1 · agua 0/1`), completed by host events and
  the inventory, never by language. A first pickup of a noun shows its word card (article, written form; audio from S9), counted as heard and
  read, never a quiz (the brief's step 2). Hotbar kept, the 40-slot screen kept as is.
- Then the 2D slice retires: tag `2d-slice-p7` on the last commit that holds it, delete the 2D simulation, generator, light map, 2D renderer and
  their tests; the R1 research docs keep their note from S0.
- Demo: a scripted run gathers five wood and three berries, crafts and places the torch headless, and the HUD shows `3/4`.

### S5: content, the phrase sheet, the stall and the villager (Sonnet 5.5 design, Haiku 5.5 content; M)

- `content/PHRASES.md`, the source of truth: one row per phrase with id, Spanish, English, accepted variants, one learner-error example, the
  NPC and situation, the support card text, the audio file ids, reviewer and date. Starts with the brief's five phrases plus `hola`, `gracias`,
  `adiós`, `no sé`, `ayuda`. Latin American neutral. The reviewer column reads `llm-opus (provisional)` until a Spanish speaker checks the
  sheet (D-game-12): the orchestrator runs that review pass on the finished sheet, on the owner's go, before the milestone merges.
- Bundle: intent `request_item` (`quiero agua, por favor`, `agua, por favor`, `necesito agua`, `me da agua`); answer set `supplies` (`agua`, `pan`,
  the berries); prompts `stall-need` ("¿Qué necesitas?", Constrained, `request_item`), `stall-offer` ("¿Quieres pan?", Constrained,
  `confirm` and `deny`), `villager-open` ("Hola.", Open: `ask_directions`, `request_item`, `greet`, `thank`, `farewell`, `ask_help`); agents
  `shopkeeper` (idle → asking → offering → done; host events `stall.give_water`, `stall.give_bread`; the opening question as an `onHostEvent`
  transition so it is a core turn in the replay record) and `villager` (idle → pointing; host event `villager.point`); slot-free pools where
  possible so S9 can pre-render them; the camp's transfer lines (the guide: "¿Quieres agua?"); `not_understood` reworded ("No te entendí. ¿Lo
  dices otra vez?"); the not-communicated feedback type changed from `SlowRepeat` to a help line; `ask_help` allowed on every prompt. A trailing
  "por favor" is tried as `(por favor)?` in the grammar DSL; if the DSL cannot express it, that is core gap M29a.
- The blacksmith and spell material leaves `content/game-v1` (unreachable from the prototype's prompts; the core's sample keeps it), so the
  reviewed sheet and the bundle are the same set.
- Gates added to the content procedure from here on: `scripts/content.sh lint game/content/game-v1 --strict --level a1` clean with an example
  expectation for every phrase; `cefr-check` clean at A1 and A2; a test that every PHRASES.md row is an example of its intent in the bundle and
  resolves to it. The `hash` step: the sandbox refuses the word, so the orchestrator runs it with the owner's standing permission or the owner
  runs it.
- Demo: `dotnet run --project game/src/Vocaterra.Host -- --transcript "quiero agua, por favor" --prompt stall-need` prints `request_item`,
  communicated, no errors; `--transcript "quiero aguas por favor"` prints `noun.number`.

### S6: NPC exchanges in the world, the transfer moment (Sonnet 5.5, M)

- `NpcService` generalises `GuideService`: one `ConversationContext` per agent, the opening line through `pipeline.Advance` (CR-015), the
  two-phase evaluation (`Analyze` then `Complete`, CR-017) with host slots, host events mapped to the world (water and bread into the inventory,
  the villager's beacon on the river, the objective's steps). NPC entities in the scene at the layout file's positions; "E: hablar" when the
  crosshair is on one.
- Fill the bottle: `Interact` on water with the objective open gives `agua`.
- The transfer step after the objective: the guide (a different NPC, no card) asks "¿Quieres agua?", and at a house the player must ask for bread
  unprompted; both are logged as transfer uses (S8). Nothing blocks: walking to the river without asking is allowed and logged as "found without
  Spanish".
- Demo: one scripted run walks the route, asks for water, asks for the river, fills the bottle and completes the objective; the console prints
  each turn and the objective's steps.

### S7: speech UX (Sonnet 5.5, M)

- A mic-check screen at start (a level meter from the capture's RMS, "Di: hola", shows what was heard, skip allowed, result logged).
- Cancel with Esc while "Pensando…" (the request is flagged and its outcome dropped in `GameSession.Collect`); a 5 s watchdog in the front-end
  that offers typing.
- A persistent panel line `Oí: «…» · Esperaba: «…»` (expected is the phrase sheet's card for the prompt); buttons and keys `Repetir`, `Más
  despacio`, `Ayuda`, `No sé`, `Escribir` (mouse hit-test like `CraftingLayout`); the repair ladder's `OfferChoices` rendered as two or three
  buttons (a choice counts as recognition only).
- The uncertainty rule (D-game-9) on `GuideTurn.Confidence` and `communicated`; NPC prompts evaluated in `Constrained` mode (the Vosk grammar
  built from the answer set, the core's own path); every voice event logged.
- Possible core gap M29b: evaluating a retry with the repair's `RetryRequest` instead of the prompt's request (check `PipelineOptions` first).
- Demo: `--mic-check --screenshot` shows the meter and the heard line; `--speak "quiero agua"` at the stall with the heard/expected line.

### S8: phrase progress and fading scaffolds (Sonnet 5.5, M)

- `PhraseProgress` in `Vocaterra.Game`: per phrase the card shown count, supported uses (card or audio visible), unaided uses, choice-only uses,
  "no sé" count, last use (stored time, session). A support level per phrase: card plus audio → audio only → nothing, stepped down after a
  correct use and the D-game-6 gap. Before the first stall exchange the key words (`agua`, `pan`) are taught with the written form and audio (the
  brief's step 4).
- `phrase.<id>` skills through `extraSkills` (and `StepSkills` when a card is shown); the journal gains a phrases page; the guide's due review
  (`GameSession.BeginTalk`) also asks a due phrase in context ("Pídeme agua"). Determinism: stored time only, no clock.
- Demo: the scripted run's journal screenshot shows each phrase's support level and counts; the console prints `phrase: pedir-agua, supported 1,
  unaided 0`.

### S9: NPCs speak (Haiku 5.5, M)

- `scripts/prerender-speech.sh` renders every slot-free line of the bundle with Kokoro (guide 28 `ef_dora`, shopkeeper 29 `em_alex`, villager 53
  `em_santa`; speed 1.0 and 0.8) into `content/game-v1/speech/<lineId>[.slow].wav` as host-owned bundle files (hashed, provenance
  `generatedBy: kokoro-tts`). The game plays them through the core's `StaticLineStore` and MonoGame's `DynamicSoundEffectInstance`;
  `Repetir` and `Más despacio` replay them. The guide's templated world lines stay text; runtime synthesis is off by default.
- The playtest build then loads Vosk only: no Kokoro and no sherpa-onnx at runtime, which also sidesteps CR-021. The WAVs are model output under
  core D2's private-build terms. The owner listens to every file for cut-offs and text-audio mismatches (the interim content-trust check of
  D-game-12); a speaker's check of the recordings is deferred with the rest.
- Demo: the stall exchange is heard; `--screenshot` run prints `speech: 31 lines, 62 files, 0 missing`.

### S10: save, sessions, resume (Haiku 5.5, M)

- A save file: scene diff per chunk, player, inventory, objective, word and phrase progress, learner model, repeat counts, agent states, stored
  time, skipped seconds, session number. `--save <file>`, `--load <file>`; a save written twice is byte-identical; loading bumps the session,
  which lifts the spacing gaps (D-game-6); the next-day recall check depends on it.
- Demo: quit after the objective, load, the world, inventory, journal and phrase page are the same, the session counter reads 2.

### S11: checks and telemetry (Haiku 5.5, M)

- `--check pre|post|recall` runs a battery outside the world: comprehension (hear a line, pick one of three pictures or actions) and production
  (an English situation, say it; "no sé" allowed), order from the seed, graded by the core, results logged.
- `SessionLog` in the front-end (the only place with a clock): one JSONL per session with turns (replay envelope id, latency, confidence,
  communicated, input kind, support level), voice events (hint, replay, slower, no sé, choice, typed fallback, cancel, timeout, mic check),
  objective steps, transfer uses and check results, next to the core's replay log (utterance and host-turn envelopes, `ReplayLog`).
- `scripts/playtest-report.sh` summarises a session folder against the brief's decision gates (playable, enjoyable, learning signal, transfer).
- Demo: a scripted session writes the log; the report prints completion, retries, support used, recall and transfer counts.

### S12: playtest build and facilitator checklist (Haiku 5.5, S)

- `--playtest` profile: no debug overlay, mic check on, checks on, logs on, `--greyscale` off.
- `docs/PLAYTEST.md`: the facilitator script (pre-check, the run, post-check, next-day recall, what to observe, the gates, the beginner audience
  rule, and the note that the content-trust gate is provisional while D-game-12 is deferred: testers are told the Spanish has not been checked
  by a native speaker). A fresh-clone build and a linux-x64 private build folder with a check that no sherpa-onnx native ships.
- Demo: the owner follows the checklist once end to end before any tester.

## 5. Determinism and boundary rules that carry over

- Everything in `Vocaterra.Simulation` and `Vocaterra.Game` stays clock-free and `System.Random`-free; the scene builder draws only from
  `WorldRng` seeded by the world seed; the stored time is the start plus `Tick / 60` plus skipped seconds; the session log and the mic-check live
  in the front-end.
- Engine code only in `src/Vocaterra.MonoGame` (D-game-1). No copied core source; a core need is a core milestone (section 6).
- `HostStateHash` keeps covering the host slots and skills (`HostDigest.Of`); `NpcService` recomputes it after `Next`.
- Assets CC0 only (D-game-5); the block atlas and the cube figures are drawn in code.

## 6. Possible core gaps (filed as core milestones only if confirmed)

- **M29a:** a trailing optional literal group in the slot-grammar DSL (`(por favor)?`), if S5 finds it unsupported.
- **M29b:** evaluate with an explicit `RecognitionRequest` (the repair ladder's `RetryRequest`), if S7 finds no option for it.
- Already proposed, useful, not blocking: M28b (CR-016 host-fact replies, CR-018 lists), M28d (CR-022 ASR confidence, CR-019 manifest copy).

## 7. Verification

- Every milestone: `scripts/merge-gate.sh` from `main` (build with warnings as errors, the whole suite, format, bundle validation, the licence
  scan), then the orchestrator's behaviour spot-check: the milestone's demo line and a screenshot looked at.
- Content (S5, S9): `scripts/content.sh validate`, `lint game/content/game-v1 --strict --level a1`, `cefr-check` at A1 and A2, the PHRASES.md sync
  test, every referenced WAV present and hashed.
- End to end (from S6): one scripted run under `xvfb-run -a` that walks the route, gathers, crafts, asks for water, asks for the river, fills the
  bottle, completes the objective and the transfer step, and writes a session log; the report script reads it back.
- Playtest (S12): a fresh clone builds and runs the scripted scenario; the facilitator checklist is followed once by the owner before any tester.

## 8. Second pass: the owner's playtest feedback (2026-10-22), milestones S13 to S21

The owner played the S12 build: "it's ok", with five changes asked for:

- English should guide a player who knows no Spanish at all.
- The start should be more gradual: the guide teaches a few things first, and that interaction alone should take 5 to 10 minutes.
- The player starts alone at their own camp on a cliff side.
- The world should look much more realistic: high mountains, dense forest, vegetation, and a much larger map.
- The look should come close to the brief's three reference images (A: the world, B: the village stall, C: crafting and the HUD).

The decisions are D-game-13 (English guidance), D-game-14 (world scale and look) and D-game-15 (the opening and the guide's lesson). The
reference images are inside `Vocaterra_Prototype_Design_Brief.docx`. Extract them to a scratch folder (outside the repository) with
`unzip -o -j game/docs/Vocaterra_Prototype_Design_Brief.docx 'word/media/*' -d <scratch>`; image1 is reference A, image2 is B and image3 is C.
They are generated concept art: never commit them and never copy from them. They set the target (shape, colour, scale, detail).

What the references show, against the S12 build. The S12 build has a dark grey sky, flat untextured blocks with dull colours, a small flat
map and plain cube figures.

- **Sky.** A bright saturated blue sky with flat blocky clouds and a square sun.
- **Distance.** A bluish aerial-perspective haze at distance.
- **Mountains.** High snow-capped peaks with bare rock faces and a waterfall falling from a cliff.
- **The valley.** A river winding through a green valley to a lake.
- **Forest.** Dense broadleaf and conifer trees with big blocky canopies.
- **Ground cover.** Tall grass, ferns, reeds and yellow and white flowers everywhere.
- **Textures.** Pixel-art textures on every block: grass tops and sides, dirt, stone, bark, log rings, planks, cobble, path gravel.
- **The village.** Timber-framed houses with stepped roofs and chimneys, a stone tower, fences, a crop field, lanterns that glow, and
  signposts with arrows (`Tienda`, `Río`, `Montaña`).
- **The stall.** A striped awning, with bread, apples and bottles on display.
- **Characters.** Blocky but expressive, with faces, clothes (an apron, a baker's hat) and a backpack.
- **Dialogue.** A dark rounded panel with the speaker's name over the line.
- **HUD.** A hotbar with item icons and counts, and a crafting panel with recipe icons.

The references are third person; the brief's first person stays the default, and S15 adds a behind-the-shoulder view as a toggle.

Order:

- Track A, the world: S13, then S14, then S15.
- Track B, the learning: S16 runs beside S13, because its folders stay apart (the game layer and a UI layer, not the renderer or the
  scene).
- S17 needs S13 (the cliff camp and the new route) and S16 (the lesson runner). It may run beside S14 or S15.
- S17b runs after S17 (it edits the dialogue panel and model that S17 changes).
- S19 (the record) needs S17 (the guide's lesson). S20 (the book) needs S19 and S15 (the panel style). S21 (F8 reports) needs S19
  (the recent turns) and runs after S20. S18 runs last, after S21.

Every milestone keeps the whole scenario playable end to end and updates the scripted routes and tests that the new geography moves. The rules
of section 5 carry over.

### S13: the large world and its terrain (Sonnet 5.5, L)

- **Size.** The map grows to 384 x 384 blocks and 128 tall (D-game-14): 24 x 24 x 8 chunks of 16³.
- **How the terrain is made.** `scene.json` describes it instead of listing it:
  - generator parameters;
  - named sites at fixed positions: `camp.player`, `camp.guide`, `village`, `stall`, `river`, `bridge`, `lake`, `waterfall`, `trail`;
  - structures as small block stamps.

  The scene builder generates the heightmap from `WorldRng` and the seed, then:
  - blends the terrain flat around each site;
  - carves the river (from a waterfall off the northern cliffs, through the valley, past the village, under the bridge, into a lake);
  - carves the trail.

  A different seed changes the terrain but never moves a site.
- **Shape.** High mountains along the north and west reach about 110 to 125, with bare stone above about 85 and snow caps above about 100;
  cliffs are steep stepped faces. Forested foothills step down to a valley floor at about 30 to 40. The village stands on meadow near the
  river.
- **The player's camp** sits on a cliff ledge at about 65 to 75 on the mountainside, facing south-east over the valley. From it the player
  sees the forest, the river, the village and the far peaks, the composition of reference A. A switchback trail leads down to the guide's camp
  in a forest clearing at the trail's foot.
- **Walking times**, at the present walk speed, are measured and reported. Targets: player camp to guide camp 40 to 70 s, guide camp to
  village 30 to 60 s, village to river 10 to 25 s.
- **Rendering at this scale:**
  - only chunks within a view distance are meshed and drawn;
  - empty and fully buried chunks are skipped;
  - chunks are frustum-culled;
  - meshing runs to a per-frame budget (or off the main thread; the simulation stays deterministic);
  - distance fog matches the view distance.

  The target is a real GPU (D-game-14). On this VM (llvmpipe) the build must run and take screenshots at a `--quality low` preset; frame
  times are reported, not gated.
- **Load cost.** Generation and the digests at load stay under about 3 s on this VM. `ComputeStateDigest` is cached, not recomputed per frame.
  A save stays edited chunks only.
- **What moves with it.** The existing objects, NPCs and objectives move to their sites: the workbench and the guide at `camp.guide`, the stall,
  the houses and the signpost at the village, the bridge on the river. The fire ring for the end of the session is at `camp.player`
  (D-game-15). `WorldFacts3D` direction and distance words stay right at the new distances (`cerca`, `lejos`). Scripted runs, demos and tests
  follow the new layout. The bundle is re-hashed.
- **Demo:** `--quality low --screenshot` from the player's camp looking south-east, and one from the valley looking north at the peaks.
  The orchestrator compares both with reference A for shape and scale (textures and vegetation come in S14).

### S14: the look, textures, vegetation, sky and light (Sonnet 5.5, M)

- **Textures.** The block atlas is pixel art drawn in code: 16 x 16 per face, generated deterministically at load from a fixed hash (no
  `System.Random`), CC0 by construction (D-game-5). Faces: grass top and side, dirt, stone, mossy stone, snow, sand, gravel path, cobble,
  oak bark and log rings, spruce bark, planks, leaves (oak, spruce, with cut-out holes), water.
- **Trees:**
  - broadleaf oaks with large blocky canopies in the valley;
  - spruces with tiered cones on the slopes;
  - a few birches;
  - density high enough to read as a dense forest, with paths and clearings left open.
- **Undergrowth.** Crossed-quad plant blocks that do not collide: tall grass, ferns, reeds by the water, and yellow and white flowers.
  They are placed by the scene builder from `WorldRng`, are part of the digests, and are thinned near paths and sites.
- **Light:**
  - a brighter, saturated palette (the brief's art-direction rules);
  - directional sun shading per face;
  - per-vertex ambient occlusion;
  - a sky gradient with a square sun and a layer of flat blocky clouds;
  - a bluish aerial-perspective fog at distance;
  - water that is translucent with a lighter shore;
  - a waterfall face.
- **Quality presets** `--quality low|medium|high`: view distance, plant density drawn, AO, clouds. High is the default on a real GPU.
- **`--greyscale` (D-game-6).** Unchanged in meaning. Plants and blocks that are not taught nouns stay in colour, as non-noun blocks do now.
- **Demo:** the two S13 screenshots again, plus one inside the forest. They should read as reference A's style: textured, bright and dense.

### S15: the village, props, characters and the HUD (Sonnet 5.5, M)

- **The village:**
  - timber-framed houses with stepped roofs and chimneys;
  - a stone tower;
  - fences and a small crop field;
  - lanterns that glow at dusk (a light colour on nearby faces);
  - signposts with arrow boards and readable text (`Tienda`, `Río`, `Montaña`).
- **The stall** gets a striped awning and displayed bread, apples and bottles in crates (reference B).
- **The guide's camp** has a workbench under a timber pergola with a torch (reference C).
- **The player's camp** has a tent, a cold fire ring and a backpack or chest.
- **Characters.** Humanoid figures with head, body, arms and legs, pixel-art faces and distinct clothes: the guide, the shopkeeper (apron and
  hat) and the villager. They have an idle animation and turn to face the player when spoken to. The player has a figure with a backpack for the
  behind-the-shoulder view, toggled with F5. First person stays the default.
- **The first-person arm** (owner, 2026-10-22: "it looks odd with its black outline"). It loses the black outline and becomes a
  blocky arm in the player's skin and sleeve colours, textured and shaded like the figures and lit by the scene. It holds the selected item
  (a log, the torch with its flame, the bottle) and has a small swing when the player gathers or places.
- **The dialogue panel** is restyled like reference B: a dark rounded panel with the speaker's name in a warm colour over the line, anchored
  near the speaker when on screen.
- **The HUD** gets a hotbar with pixel-art item icons and counts, and the workbench's recipe panel shows the inputs and the output as icons
  with `have/need` counts (reference C).
- **Demo:** screenshots at the stall (compared with reference B) and at the guide's workbench (compared with reference C).

### S16: the English guidance layer and the lesson runner (Sonnet 5.5, M)

- **English is on-screen text only (D-game-13).** Only Spanish is spoken. The English comes from a guide panel: a short line under or beside the
  Spanish, in a different colour, marked as English.
- **English fades per phrase.** S8's ladder gains a first rung:
  1. English explanation, Spanish card and audio;
  2. Spanish card and audio, with the English on the help key;
  3. audio only;
  4. nothing.

  The help key shows the English at every rung (rule 8: nothing is gated behind language).
- **English also covers the rest of the game:**
  - the controls and first-time tips ("Look around with the mouse", "Press E to pick up");
  - the objectives (bilingual HUD lines at first, for example `Campamento (camp): madera (wood) 0/5`, the English dropped as each noun becomes
    known);
  - the feedback after a turn, in plain English keyed by the core's error codes. That text lives in game content: game words live in `game/`,
    and the core is not changed.
- **The lesson runner.** A data-driven sequence in game content (for example `game/lessons.json`) of steps:
  - `explain` (English text);
  - `model` (the NPC says a line, with its WAV);
  - `listen` (the learner hears and picks a meaning);
  - `repeat` (the learner says it with the card);
  - `answer` (a real turn through the core at a prompt);
  - `do` (a world action, for example pick up the fruit the guide points at);
  - `recap`.

  Each step has a skip and a help path; a failed or uncertain turn moves forward (fail-forward, D-game-9). Progress is saved (S10), and
  the session log records each step (S11). S16 ships the runner with a two-step sample lesson; S17 writes the real one.
- **Demo:** a scripted run through the sample lesson prints each step and its outcome. A screenshot shows the English line under the Spanish.

### S17: the opening, the guide's lesson and the ending (Sonnet 5.5, M)

- **The opening (no quiz at spawn, brief step 1).** The player wakes alone at their cliff camp. English tips teach looking, walking, jumping,
  picking up and the inventory. A note (English, with one Spanish word) says the guide's camp is down the trail. The HUD says
  `Go down the trail and find the guide`. The view over the valley is the first thing seen.
- **The guide's lesson** (D-game-15), 5 to 10 minutes. Each unit runs explain, model, repeat, answer and do. Units are short; the English
  thins from the second use of a phrase on.
  1. *Greeting:* the guide waves: "¡Hola!"; the learner says `Hola`. The guide says "Me llamo …" (understood, not said: the learner does not
     say a name, which avoids core finding M29d).
  2. *Yes and no:* the guide holds up items: "¿Es madera?"; the learner answers `Sí` or `No`.
  3. *What is this?:* the learner asks "¿Qué es esto?" at things the guide points to. The guide names them: `madera`, `fruta`, `agua`,
     `botella`, `mesa`, `antorcha`.
  4. *Please and thank you:* the guide offers fruit: "¿Quieres fruta?"; the learner says `Sí, por favor`, then `Gracias`; the guide says
     "De nada".
  5. *Getting unstuck:* the learner says `No sé`, `Otra vez, por favor` and `Más despacio, por favor`. Each does what it says: the
     guide repeats, or repeats slowly.
  6. *The task:* "Necesito madera y frutas." (understood), with English support. The camp checklist begins, and the guide later sends the
     learner to the village with "¿Qué necesitas?", "Quiero agua, por favor." and "¿Dónde está el río?" as now.

  Recap at the end of the lesson: the phrases learnt so far, shown on the journal's phrases page.
- **The ending.** With the torch made and the water fetched, the learner climbs back to their camp and lights the fire ring: the session's
  closing view over the valley at dusk.
- **Content.** New rows in `content/PHRASES.md` and the bundle: intents, examples, prompts, answer sets, dialogue and the support-card
  English. All textbook A1, Latin American neutral, `lint --strict` and `cefr-check` clean. The phrase set grows beyond D-game-12's interim
  five plus five; D-game-15 records the owner's request. The pre and post checks (`checks.json`) gain items for the new phrases (D-game-10),
  the recall check included.
- **A core gap stops that part.** If a phrase needs a call the core lacks (for example routing `Otra vez` and `Más despacio` to the repair
  actions, if that cannot be done in content), the agent stops that part and reports it; it becomes a core milestone.
- **Review and speech.** The orchestrator runs the Opus review pass on the new rows (D-game-12 step 3); the agent applies its advice. Then the
  new lines are pre-rendered (`scripts/prerender-speech.sh`), and `SPEECH_CHECKLIST.md` lists the new WAVs for the owner to listen to.
- **Measure.** One scripted run of the lesson; report its length at a learner's pace. The target is 5 to 10 minutes; the estimate counts the
  audio lengths and a typical answer time.

### S17b: the talk key on screen and the game's own messages in English (Haiku 5.5, S)

The owner's playtest notes (2026-10-22): "we don't have the hotkey for speaking (V) present in the UI when talking with someone", and "when
the models haven't been loaded, the error appears in Spanish. It should appear in English as it's an error." D-game-13 already puts the
controls in English.
- **The talk key is always shown in the dialogue.** `DialoguePanel` shows `Hold V or Space: speak` only while the buttons have focus
  (`ButtonHints`); the typing state (`KeyHints`) never names V. Both states name it, and when speech is unavailable the hint says so in
  English instead (for example `Speech off: type your answer`).
- **The game's own messages are English.** Errors, problems and status lines the game writes about itself, not about the language:
  the speech-model checks in `SpeechEngine` (missing `VOCATERRA_MODELS_DIR`, a missing Vosk or Kokoro model, an unlisted manifest entry,
  a recogniser that fails to load), `TurnWorker`'s synthesis failures, `PushToTalk`'s microphone failure and `DialogueModel`'s fallback
  "could not evaluate". Each says what to do in plain English (for example `Speech models not found: set VOCATERRA_MODELS_DIR (see
  nlp/scripts/fetch-models.sh)`). A sweep of `game/src` finds any other Spanish system text.
- **The panel key hints are English** (dialogue, journal, inventory, crafting), following the place S16 gave the English controls.
- **The journal's labels are English too** (`Diario de frases` becomes `Phrase journal`; also `con ayuda`, `sin ayuda`, `ayuda: inglés,
  tarjeta y audio`, `lista para repasar`, `se oye`, `Sin ayuda: 0 de 12`, `Sesión 1`). S20 rebuilds this page as the book; S17b only
  translates the strings, so S20 inherits English.
- **The microphone check at the start is English** (owner: "a new player, and a new person to Spanish, has no idea what this means").
  `MicCheckModel`'s title, talk hint, pass and fail lines and `PushToTalk.NoMicrophone` become English (for example `Microphone check`,
  `Hold V or Space and say "hola"`, `Got it, I can hear you.`, `I didn't catch that. Try again?`, `No microphone: type your answer.`).
  The word the player says, `hola`, stays Spanish and is shown as the thing to say.
- **The beacon's distance unit is English** (`campamento 74 blocks`): the label is a taught noun, the unit is the game's.
- **What stays Spanish:** everything the learner learns or hears: NPC lines, prompts, Spanish labels of taught nouns, the Spanish half of
  bilingual objectives, and the core's feedback codes (their English text is S16's).
- **Tests:** the hint text in both dialogue states, with and without speech; each model problem's English message (the cheap check runs
  without models: point `VOCATERRA_MODELS_DIR` at an empty temporary folder).
- **Demo:** `--playtest` with `VOCATERRA_MODELS_DIR` unset shows the English model message and the typing hint; with models set, the
  dialogue shows the V hint in both states.

### S19: the learner's word record and lesson record (Haiku 5.5, M)

The owner (2026-10-22, D-game-16): "a book that can be opened that tracks the nouns / verbs that the player has used, for this we need to track
all the input they provide to us (speaking / writing). I also want a UI (perhaps in the book too) to track all completed lessons." S19 is the
data; S20 is the book. No core change is needed: every evaluated turn already carries `PipelineResult.Words` (nlp/docs/API.md), one entry
per token with the core's reading (`Lemma`, `Pos`, `Person`, `Number`, `Tense`, `Mood`, `Gender`), for a typed line exactly as for speech.
- **The input history** (`Vocaterra.Game`, beside `PhraseProgress`): every line the learner gives the game, spoken or typed, in any
  dialogue, lesson step or check. One entry each: the text the core evaluated (the transcript for speech, the typed line), the source
  (voice or typed), where it was said (prompt id, NPC or lesson unit), the verdict (communicated, error tags), and when, as the session
  number and the stored game time (never the wall clock). Bounded: the newest 500 entries are kept in the save; the session log (S11)
  keeps the full stream as now, with typed lines included if they are not yet.
- **The word record**, built from the history's word readings, one row per lemma of a noun or a verb that the core read with a lexicon
  entry (unknown tokens and recogniser noise stay in the history only):
  - nouns: lemma, gender (for the article), the forms used (`agua`, `aguas`), times used, times used correctly (no error span over the word),
    first and last use, and the last sentence it was used in;
  - verbs: lemma (the infinitive, `querer`), each form used with its person and tense (`quiero`, 1st singular present), the same counts and
    the last sentence.
  - Other parts of speech are recorded in the same table with their `Pos`, for a later page, but S20 shows only nouns and verbs.
- **The lesson record:** for each lesson in `lessons.json` (S16's runner, S17's guide lesson), the units done, the state (not started, in
  progress, completed), the session it was completed in, the turns it took, and the help used (English shown, "no sé", repeats). It is
  written by the lesson runner's own completion events, not inferred from phrases.
- **The save (S10):** format version 2 adds the history, the word record and the lesson record. A version 1 save still loads, with an empty
  history and the lesson record rebuilt as far as the saved runner state allows. New learner-model state stays in the game layer, not the core.
- **Determinism:** ordinal comparisons, no clock, no `System.Random`; the record is a pure fold of the history, so replaying a save's
  history gives the same record (a test proves it).
- **Tests:** a spoken and a typed turn both land in the history; `quiero dos aguas` gives the noun `agua` (forms `aguas`) and the verb
  `querer` (form `quiero`, 1st singular present); an error span marks the word used but not correct; unknown words stay out of the record;
  the 500-entry bound; lesson states across a unit, a whole lesson and a resumed save; version 1 to 2 loading.
- **Demo:** `--transcript` cannot show a record across turns, so a scripted run of the guide's lesson (S17's measurement script) prints the
  word record and lesson record at the end.

### S20: the book (Haiku 5.5, M)

The learner's book, the place to see what they have said and learnt (D-game-16). It grows out of the journal (J), which already has the
catalogue words page and the phrases page (S8), rather than adding a second notebook.
- **A book you open.** The journal becomes the book: drawn in code as an open book over the scene, in S15's panel style (reference B),
  CC0 by construction. It opens with J, as now, and the learner also starts with a book item at the cliff camp (S17's opening) that opens
  it when used. It covers the screen and takes the keyboard while open, as the journal does now. Page tabs, turned with Tab, Left, Right
  or a click: **Nouns**, **Verbs**, **Phrases**, **Lessons**, **What I said**. Every title, tab, column head and key hint is English
  (D-game-13); the Spanish is what the learner learnt.
- **Nouns:** the catalogue nouns as on today's words page (seen or `¿?`, icon, heard, read, said, correct, the three pips), plus every
  other noun the learner has used from S19's word record. Each row has the article (`el agua`, `la botella`), the forms used, times used and
  correct, and the last sentence, with its English when the sentence is a known phrase.
- **Verbs:** the infinitive with its English (`querer — to want`, from the bundle's lexicon gloss where there is one), each form the learner
  used with a plain-English label (`quiero — I want`, `¿quieres? — do you want?`), counts, and the last sentence.
- **Phrases:** today's phrases page, unchanged in meaning.
- **Lessons:** every lesson in the bundle with its state (completed, in progress with units done, or not started), when it was completed
  (session n), its units with a tick each, the phrases it taught (S17's recap), and the help used. A completed lesson shows a short
  English summary: "You can greet, say yes and no, ask what something is…".
- **What I said:** the newest entries of S19's history, newest first: the line, voice or typed, where, and whether it was understood,
  with the core's feedback in English (S16's text). This page is the learner's view of "all the input".
- **Large lists scroll** with the wheel and Page Up and Page Down; the book stays readable at 1280 x 720.
- **`--greyscale` (D-game-6)** behaves on the book's icons as it does on the journal's now.
- **Tests:** what each page lists for a fixed record; the English labels for verb forms across person and tense; tab order and scrolling;
  that the book item and J open the same book.
- **Demo:** screenshots of the five pages after S17's scripted lesson run, saved outside the repository.

### S21: F8, the in-game bug and feature report (Haiku 5.5, S)

The owner (2026-10-22, D-game-17): "We need a bug / feature request feature in the game. So pressing F8 will upload a bug report and save as
a markdown." Asked where to upload, the owner chose a local file only: no network (core D8 untouched); the playtest folder collects the files.
- **F8 opens the report panel** anywhere in the game: in the world, in a dialogue, in the lesson, in the journal or the book, in a check.
  - The screenshot is taken from the frame before the panel draws, so it shows what the player saw.
  - The game pauses while the panel is open: no ticks, no turns, the microphone off.
  - The panel is English (D-game-13) and drawn in S15's panel style. It offers a choice of `Bug` or `Feature request` (Tab or click), and a
    multi-line text box with the accents keys of the dialogue (Alt+vowel and the others).
  - Ctrl+Enter or the `Save report` button saves. Esc cancels and keeps nothing.
  - A toast says `Report saved: <path>`.
  - F8 is listed in the help (F1) and the controls in english.json.
- **The report** is a Markdown file plus a PNG, saved in a `reports/` folder in the session's folder (`--session-dir`, default under
  `~/.local/share/vocaterra/sessions/`). A `--report-dir <dir>` option overrides the folder. The file name comes from the session number,
  the tick and the kind (`report-s1-t4681-bug.md`, with a counter if taken), never from the clock. The header may show the local date and
  time through an injected clock (the tests fix it); nothing in the evaluation path reads it. The Markdown holds:
  - the title (the first line of the player's text) and the kind;
  - the player's text;
  - the screenshot, linked;
  - **Game state**, as a table: build commit, bundle id and hash, seed, position, view direction, objective and its steps, lesson and step,
    open panel, quality preset, greyscale, speech status (models found, microphone);
  - **Recent turns**: the last 10 lines from S19's history (text, voice or typed, where, understood, error tags);
  - **Session log**: the last 200 lines of the session log, in a fenced block.
- **The playtest script** `scripts/playtest-report.sh` lists the reports of each session (kind and title) and copies them with the session's
  summary.
- **Tests:**
  - the Markdown for a fixed state, as a golden;
  - the file naming and its counter;
  - the 10 and 200 bounds;
  - cancel saves nothing;
  - the panel's key handling;
  - the pause: no tick passes while the panel is open.
- **Demo:** a scripted run with a flag that opens the panel, types a line and saves it (for example `--report "The bridge is missing"
  --report-kind bug`). It prints the saved path; show the Markdown and the PNG.

### S18: the playtest refresh (Haiku 5.5, S)

- The playtest build, `PLAYTEST.md` and the report script are updated for the new opening, the lesson steps, the longer session and the new
  phrases.
- The launcher `game/playtest/playtest.sh` says "build this folder with `scripts/playtest-build.sh` first" when the game binary is missing.
- The build-size check is re-run.
- The bundle hash is recorded for the owner's freeze before recall sessions.
