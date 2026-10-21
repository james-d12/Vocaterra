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
