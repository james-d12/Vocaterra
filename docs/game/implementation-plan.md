# Implementation plan: Vocaterra (the game)

> **Layout note (R01, 2026-10-23):** the repository now has one root layout. `nlp/src`, `nlp/tests`, `nlp/tools` and `game/src`, `game/tests`, `game/tools` are `src/`, `tests/` and `tools/`; `nlp/docs` is `docs/nlp/`, `game/docs` is `docs/game/`; `nlp/content/v1` is `content/v1`, `game/content/*` is `content/`, `nlp/golden` is `tests/golden`, `nlp/models` is `models/`, `game/assets` is `assets/`, `nlp/scripts` is `scripts/`, `game/playtest` is `scripts/playtest`; `LICENSE` and `NOTICE` are at the root and cover the whole repository (Apache-2.0). The core is the `Vocaterra.Nlp.*` projects (since M52 and M53 one engine, `src/Vocaterra.Nlp`, and the Spanish pack `src/Vocaterra.Nlp.Es`), the game is `Vocaterra.{Game.Core,Game,Host,SpeechRender}` (`Simulation` and `MonoGame` until R16); `Vocaterra.Nlp.slnf` builds the core alone. Paths and links below were updated to match; the narrative is otherwise unchanged. Since then: `content/v1` was deleted (R02; the one bundle is `content/game-v1`) and CI runs from `.github/workflows/ci.yml` (2026-10-31).

This is the plan for the game: the game projects (`Vocaterra.Game.Core`, `Vocaterra.Game`, `Vocaterra.Host`, `Vocaterra.SpeechRender`), `content/game-v1`, `assets/` and `docs/game/`. The language core (`Vocaterra.Nlp.*`) has its own plan
(`docs/nlp/implementation-plan.md`) and decisions; this file never plans core internals. Since 2026-10-21 (D-game-7) one orchestrator runs both. Owner decisions are in `docs/game/decisions.md`. Status values: `planned`, `in progress`,
`in review` (on its branch, not yet merged), `done` (merged to main and gated; for the S series, merged to `feature/prototype` and gated), `blocked` (names the blocker).

## 1. Decisions taken

- **The core by project reference** (D-game-7, 2026-10-21; before it: packages from a submodule's local feed). `Vocaterra.Game` and
  `Vocaterra.Host` reference the projects under `src`; the game never copies core source (core ADR 0009). A call the core does not
  expose is a core milestone in `docs/nlp/implementation-plan.md`, never a workaround in the game. (`docs/game/CORE_REQUESTS.md`, the request file of
  the two-repository era, was closed when the repositories merged; its open rows became M28b to M28d in the core's plan.)
- **Console host until the engine is decided** (D-game-1). The host is engine-agnostic: no engine code under `src/Vocaterra.Host`.
- **Game words live here**, not in the core. The core knows effect kinds and host event names only as opaque strings.
- **Content tool:** the core's ContentBuilder (`tools/Vocaterra.Nlp.ContentBuilder`), run from source by the root `scripts/content.sh`.
- **Model policy:** Haiku 5.5 by default, Sonnet 5.5 for design-heavy milestones, Opus only on the owner's request.
- **The prototype brief** (D-game-8, owner, 2026-10-21): a 3D first-person voxel slice on MonoGame (forest, path, village stall, river; three
  NPCs; five target phrases), speech rules D-game-9, learning measurement D-game-10, full colour (D-game-6 amended), the Spanish reviewer
  deferred with a provisional workaround (D-game-12). The S series was built on the integration branch `feature/prototype` until 2026-10-22; it then merged into `main` until 2026-10-23, and since then work goes to a feature branch, never `main`.

## 2. Repository layout

```
Vocaterra.sln                 the solution for the whole repository: the core's projects (Vocaterra.Nlp.*) and the game's
Vocaterra.Nlp.slnf            the core alone
Directory.Build.props         shared build settings (net10.0, nullable, warnings as errors, deterministic), the core's packing rules and, for the game, the Linux-only native trim (G7)
Directory.Packages.props      central versions for both halves (third-party packages only)
nuget.config                  nuget.org only
global.json                   the .NET SDK pin (10.0.402, latestPatch)
src/Vocaterra.Game.Core/      the game's engine-free systems: the world and its tick, the session, the guide, the language systems, the screens'
                              models and their keys (references the core's projects, no package, no engine; folders in architecture.md)
src/Vocaterra.Game/           the MonoGame front-end (the only engine code): the window, the drawing, the devices, the UI's layers
src/Vocaterra.Host/           the console host: loads a bundle, evaluates lines, runs the text loop
src/Vocaterra.Nlp/            the language core's engine, and src/Vocaterra.Nlp.Es/ its Spanish pack (their own plan, decisions and docs under docs/nlp/)
tests/                        one test project per source project, their folders mirroring the sources' (R16)
tools/Vocaterra.SpeechRender/ the speech prerender tool (the core's tools are beside it)
content/game-v1/              the repository's one content bundle (R02); content/locale/ the English text (C series)
assets/                       CC0 art, fonts and sounds (assets/ATTRIBUTION.md)
docs/game/                    the game's living documents (index: docs/README.md); research, handovers and QA reports sit in docs/ by kind
scripts/content.sh            validate, hash, cefr-check, lint, pack, verify-pack through the core's content tool
scripts/merge-gate.sh         the orchestrator's merge gate (both halves)
.claude/agents/orchestrator.md   the orchestrator's role file (the entry point for a session)
.github/workflows/ci.yml      CI: build, test, grammar replay, LanguageTool (D-game-3, active since 2026-10-31)
```

The S series adds `src/Vocaterra.Simulation/Voxel/` (S1; now `src/Vocaterra.Game.Core/Voxel/`; the 2D simulation, generator and light map are removed at S4),
`content/game-v1/game/scene.json` (the hand-authored route), `content/PHRASES.md` (the phrase sheet, S5), `content/game-v1/speech/` (pre-rendered
lines, S9), `docs/game/design/prototype-brief.md` and `docs/game/scenario-plan.md` (the brief and its plan) and `docs/research/2026-10-21-noun-town/` (D-game-11).

## 3. Rules every agent follows

1. **The core as it is.** A game milestone edits the game projects, their tests, `content/game-v1`, `assets/` and `docs/game/` only: no copied core source, no edit to a `Vocaterra.Nlp.*` project, `content/v1` or `docs/nlp/`. A missing call or a bug in
   the core is reported in the milestone's report with the call and the reason, and the orchestrator plans it as a core milestone.
2. **Determinism is the host's job.** The core is deterministic when the host supplies its inputs: the world seed, the stored time (Unix
   seconds, passed in), the learner model and the repeat counts (`RepeatCounts.Next`, core M19d). The host never draws its own random
   numbers, except world generation in `Vocaterra.Game.Core` (`World/Generation/`), which is a pure function of the world seed through its own seeded generator
   (`WorldRng`); never `System.Random`, never the clock, and no `Math.Sin` or `Math.Pow` in generation. Only the interactive text loop reads the clock; a `--transcript` run uses a fixed time. Dictionaries that reach output are
   ordered with `StringComparer.Ordinal`. Culture is invariant.
3. **Engine code only in `src/Vocaterra.Game`** (D-game-1, later a Godot front-end of its own). Never in `src/Vocaterra.Host` or
   `src/Vocaterra.Game.Core`, which takes no packages at all and holds the game's logic (`docs/process/code-guidelines.md`; its
   `ArchitectureRulesTests` gate it). A screen in Core names keys as `GameKey` and reads the frame as `IInputFrame` (D-p2-1a).
4. **Content procedure.** Every edit to `content/game-v1` (from the repository root): `scripts/content.sh validate`, then
   `scripts/content.sh hash content/game-v1 --bundle-id game-v1` (rewrites `manifest.json`), then `scripts/content.sh validate` and the tests.
   Files that come from the Wiktionary import keep `generatedBy: wiktionary-import` in `provenance.json` and their CC BY-SA notice in
   `ATTRIBUTION.md`. New hand-authored files are listed in `provenance.json` with `generatedBy: hand-authored`.
   A content milestone also runs `scripts/content.sh cefr-check content/game-v1 --level a2` (the bundle's ceiling since V08; `--level a1`
   is a report) and the host's `--validate-content` (each lesson's Spanish within its own `level`); the tool checks dialogue templates only
   (core finding), so prompt text is checked by hand.
5. **Scenes are design, not code.** A scene is intents, slot grammars, prompts, dialogue pools, agent state machines and effects, all in the
   bundle. The host maps the core's host events and effect kinds to game state; the core does not learn game words.
6. **Build hygiene.** `dotnet build Vocaterra.sln -warnaserror` and `dotnet format Vocaterra.sln --verify-no-changes` (from the root) stay
   clean. Public types have `///` summaries. No new package outside the root `Directory.Packages.props`.
7. **Branches and commits.** One branch per milestone (`G<nn>-<name>`, `P<nn>-<name>`, `S<nn>-<name>` or the orchestrator's name), commit
   prefix `G<nn>: <title>`, `P<nn>: <title>` or `S<nn>: <title>`, trailers as the orchestrator's brief says. Agents do not push.
8. **Nothing is gated behind language or voice** (D-game-8). Every failure (a wrong phrase, an uncertain recognition, a dead microphone, a
   timeout) has a recovery path: retry with help, a choice, typing, "no sé", or simply walking on. Objectives complete by host events and the
   inventory, never by a language score.

## 4. Milestones

The session handovers that stood here (2026-10-21 and 2026-10-22) are in [`docs/handovers/`](../handovers/README.md). The milestone table:

| ID | Milestone | Status | Model | Depends on |
| --- | --- | --- | --- | --- |
| G0 | Repository setup: submodule at `db39999`, local feed, content tool, role file, requests file, gate, this plan | done (merged 2026-10-21) | Haiku 5.5 | core M19e |
| G1 | Adopt the core's M19d API: `RepeatCounts.Next` and `ConversationContext.Next` replace `NextRepeatCounts`; show `PromptSpec.Text` before each turn; README and ARCHITECTURE for the submodule route | done (merged 2026-10-21, `0eece5c`) | Haiku 5.5 | G0 |
| G2 | Re-sync `content/game-v1` with the core sample (prompt `text`, the generic intents, the blacksmith's `confirming` state, the stale `ATTRIBUTION.md` wording); decide D-game-4 | done (merged 2026-10-21, `1452c4c`); D-game-4 decided: diverge | Haiku 5.5 | G1 |
| P1 | Simulation skeleton and surface world: `Vocaterra.Simulation` (no packages, no engine, `net8.0;net10.0`), seeded `WorldRng` (SplitMix64), noise, 32 x 32 chunks, heightmap terrain, grass and trees, spawn, fixed 60 Hz `Step(commands)`, player gravity, walking, jumping, tile collision | done (merged 2026-10-21, `9e40db9`) | Sonnet 5.5 | D-game-1 |
| P2 | MonoGame front-end skeleton: `Vocaterra.MonoGame` (3.8.6 DesktopGL), no content pipeline, placeholder tiles drawn in code, camera, input to commands, CC0 font with the Spanish letters | done (merged 2026-10-21) | Sonnet 5.5 | P1 |
| P3 | **The guide, typed (smallest slice proving the core in game):** `Vocaterra.Game` (`GameSession`, `GuideService`, `WorldFacts`), the guide in the bundle (`ask_directions`, `ask_what`, `ask_help`), dialogue panel; `¿dónde está el árbol?` gets a reply and an arrow | done (merged 2026-10-21) | Sonnet 5.5 (scene and session API), Haiku 5.5 (content) | G1, G2, P2 |
| P4 | Nouns, word progress, greyscale: `game/nouns.json`, `lexicon/world.json`, `NounCatalog`, `WordProgress`, the D-game-6 rule, CPU-made grey atlas, `name_object` intent, journal | done (merged 2026-10-21, `00c6c44`) | Haiku 5.5 | P3 |
| P4b | Spaced colour and the guide's review (D-game-6 amended): growing gaps (10 min, then 1 h, or a new session), early uses logged without colour, the guide asks "¿Qué es esto?" about a due word with the pointer on its nearest example; scripted time skip for the demo | done (merged 2026-10-21, `b433647`, fix `9ce62ef`) | Sonnet 5.5 | P4 |
| P5 | Spoken input: push-to-talk, microphone (MonoGame `Microphone`, PortAudioSharp2 fallback), Vosk small, evaluation on a worker thread | done (merged 2026-10-21, `96d4907`) | Sonnet 5.5 | P3 |
| P6 | Mining, building, inventory (durability, drops, hotbar placement, 40 slots with a 10-slot hotbar; the player starts with `hacha` and `pico`) and the **wider world of things** (section 4.3: new surface tiles and items, each in `game/nouns.json` with the colour rule) | done (merged 2026-10-21, `57244dd`) | Haiku 5.5 | P1, P2, P4b |
| P6b | **Crafting and the guide's crafting answers** (owner, 2026-10-21; section 4.3): recipes in `game/recipes.json`, stations (`mesa`, `horno`), crafting panel; the guide answers "¿cómo hago una mesa?" (recipe, station, what is missing) and "¿dónde hay arena?" (where a material comes from and the nearest source) | done (merged 2026-10-21) | Sonnet 5.5 (design), Haiku 5.5 (content) | P6, G6 |
| P7 | Caves (worms, cellular automata), ores, full forest vegetation, flood-fill lighting; ores `carbón`, `cobre`, `hierro`, `oro` join the catalogue and the recipes (`antorcha` from `madera` and `carbón`, metal tools at the `horno`) | done (merged 2026-10-21) | Haiku 5.5 | P1, P6, P6b |
| P8 | The guide speaks (Kokoro through the core) | superseded by S9 (2026-10-21, D-game-8) | Haiku 5.5 | P5, CR-006 |
| P9 | Save and load, playtest build and checklist | superseded by S10 and S12 (2026-10-21, D-game-8) | Haiku 5.5 | P4, P6, P7 |
| P10 | Godot spike (optional): the same seed and guide turn in Godot 4 .NET; a note on the cost of the move | planned (optional, after the S series) | Sonnet 5.5 | P4 |
| G3 | Innkeeper scene (section 4.1) | superseded by S5 and S6 (2026-10-21, D-game-8) | Sonnet 5.5 for the scene design, Haiku 5.5 for the content checklist | G2, P9 |
| G4 | Quest-giver scene (section 4.2) | superseded by S5 and S6 (2026-10-21, D-game-8) | Haiku 5.5 | G3 |
| G6 | Core bump to `53c52b7` (M26a to M26e) and adoption: drop the CR-012 double evaluation, the CR-013 `esta` lexicon entry if no longer needed, read `nouns.json` with `TryReadHostFile` (CR-007), word lists from `PipelineResult.Words` (CR-002), the guide's world line through host slots (CR-003), the named answer through `PipelineResult.Answer` where it fits (CR-004), `word.<lemma>` extra skills (CR-005) | done (merged 2026-10-21; core pin `53c52b7`) | Haiku 5.5 | P6 |
| G7 | Build trim and licence: Linux-only native assets (owner, 2026-10-21; about 1.5 GB less per build), `LICENSE` (MIT, D-game-2) and README licence lines | done (merged 2026-10-21) | Haiku 5.5 | P6b |
| G8 | The spoken-mistake test (`SpokenModelTests.AMistakeInASpokenLineIsHeardAsSaidAndTheCoreCorrectsIt`, section 4.6 R01 findings): Vosk hears `a dónde está la árbol` for Kokoro's `dónde está la árbol`. Fix the cause if it is the game's (stale cache clip, synthesis settings) with a regression test; a core Asr cause is reported as a core gap; if the recognizer only prefers the bundle's own `a dónde`, allow an optional leading `a ` and keep the `la árbol` assertion. No edit to `content/game-v1`. Owner-approved 2026-10-26; branch `G8-spoken-mistake`, gated into `feature/language-packs` | done (merged to `feature/language-packs` 2026-10-27, `00ff1e5`) | Haiku 5.5 | R01 |
| R1 | Research: Terraria-inspired prototype (guide NPC by text and speech, mining, building, movement, forest biome, map and cave generation, language progress tracking, known nouns in colour and unknown in greyscale, inventory) on MonoGame; findings in `docs/research/` | done (merged 2026-10-21, `0e8d601`); proposals await the owner | Opus 5.5 (owner's request) | G0 |
| R2 | Research: the same game on iOS and Android (desk research, no spikes; owner's request 2026-10-30): what carries over, what changes (platform heads, file seams, touch controls, phone UI, drawing budget, Vosk on Android and iOS, JSON under trimming, lifecycle, ARM64 results, toolchain and stores), size, order, owner questions; findings in `docs/research/2026-10-30-mobile-feasibility.md` | done (on `feature/mobile-research`); proposals await the owner | Opus 5.5 (owner's session) | none |
| S0 | Plan and docs for the prototype brief: D-game-8 to D-game-12, D-game-6 amended, this section, the brief as Markdown, READMEs, Noun Town research moved | done (merged to `feature/prototype` 2026-10-21, `0a60928`) | Haiku 5.5 (docs) | `docs/game/scenario-plan.md` |
| S1 | 3D voxel simulation, headless (`Vocaterra.Simulation.Voxel`, `scene.json`, `PlayerBody3D`, `BlockRay`) | done (merged to `feature/prototype` 2026-10-21, `d2329cb`) | Sonnet 5.5 | S0 |
| S2 | First-person voxel front-end (mesher, renderer, camera, figures, beacon; 2D UI over the scene) | done (merged to `feature/prototype` 2026-10-22, `c5af3d6`) | Sonnet 5.5 | S1 |
| S3 | The scenario world and the guide in 3D (`WorldFacts3D`, the route layout, new nouns) | done (merged to `feature/prototype` 2026-10-22, bundle `5b9d0dab…`) | Haiku 5.5 | S2 |
| S4 | Gather, craft, the camp objective; retire the 2D slice (tag `2d-slice-p7`) | done (merged to `feature/prototype` 2026-10-22, bundle `1e13e231…`) | Haiku 5.5 | S3 |
| S4b | The `--greyscale` A/B switch in 3D (D-game-6 amended; S4 left a stub) | done (merged to `feature/prototype` 2026-10-22) | Haiku 5.5 | S4 |
| S5 | Content: `content/PHRASES.md`, the stall and the villager, `request_item`, constrained prompts; Opus review pass (D-game-12) | done (merged to `feature/prototype` 2026-10-22, bundle `c3cac003…`) | Sonnet 5.5 (design), Haiku 5.5 (content) | S0 |
| S6 | NPC exchanges in the world (`NpcService`), filling the bottle, the transfer moment | done (merged to `feature/prototype` 2026-10-22, bundle `eb4f0951…`) | Sonnet 5.5 | S3, S5 |
| S7 | Speech UX: mic check, cancel, watchdog, heard-vs-expected, buttons, choices, uncertainty rule, constrained prompts | done (merged to `feature/prototype` 2026-10-22, bundle `c3ad62fe…`) | Sonnet 5.5 | S6 |
| S8 | Phrase progress and fading scaffolds; the review extended to phrases | done (merged to `feature/prototype` 2026-10-22) | Sonnet 5.5 | S6 |
| S9 | NPCs speak: pre-rendered Kokoro lines in the bundle | done (merged to `feature/prototype` 2026-10-22, bundle `f5f28b88…`); **the owner listens to every WAV** (`docs/game/speech-checklist.md`, D-game-12 step 4) | Haiku 5.5 | S5, S6 |
| S10 | Save, sessions, resume (P9 re-scoped) | done (merged to `feature/prototype` 2026-10-22) | Haiku 5.5 | S4, S8 |
| S11 | Checks (pre, post, recall), the session log and the report script | done (merged to `feature/prototype` 2026-10-22, bundle `cd63077b…`) | Haiku 5.5 | S8, S10 |
| S12 | Playtest build and facilitator checklist | done (merged to `feature/prototype` 2026-10-22); **the owner's pre-flight** (`docs/game/playtest.md` section 12) | Haiku 5.5 | S7, S9, S11 |
| S13 | The large world: 384 x 384 x 128 generated terrain, mountains, cliffs, the river and waterfall, sites, the player's cliff camp and trail, view-distance rendering (`docs/game/scenario-plan.md` section 8) | done (merged to `feature/prototype` 2026-10-22) | Sonnet 5.5 | S12, D-game-14 |
| S14 | The look: code-drawn pixel-art textures, tree species, undergrowth, sun shading, AO, sky, clouds, haze, water, `--quality` presets | done (merged to `feature/prototype` 2026-10-22, `849fd52`) | Sonnet 5.5 | S13 |
| S15 | The village, props, humanoid characters, the reference-B dialogue panel, the HUD icons and recipe panel, the F5 view, the first-person arm without its outline | done (merged to `feature/prototype` 2026-10-22, `788f5a1`) | Sonnet 5.5 | S14 |
| S16 | The English guidance layer (D-game-13) and the data-driven lesson runner | done (merged to `feature/prototype` 2026-10-22, bundle `4be20d61…`) | Sonnet 5.5 | S12, D-game-13 |
| S17 | The opening at the cliff camp, the guide's 5 to 10 minute lesson, the ending; new phrases, checks, Opus pass, speech | done (merged to `feature/prototype` 2026-10-22, `b0ebf5f`, bundle `9582e14f…`); **the owner listens to the 72 new WAVs** (`docs/game/speech-checklist.md`) | Sonnet 5.5 | S13, S16, D-game-15 |
| S17b | The talk key (V) shown in the dialogue in every state; the microphone check, the game's own errors and key hints in English (owner's playtest notes) | done (merged to `feature/prototype` 2026-10-22, `678e5a2`) | Haiku 5.5 | S17 |
| S19 | The learner's record: every spoken and typed input, the nouns and verbs used (from the core's word readings), the lessons completed; save version 2 (D-game-16) | done (merged to `feature/prototype` 2026-10-22, `c92e486`) | Haiku 5.5 | S17, D-game-16 |
| S19b | Check answers do not count in the word record (owner); old saves lose the check uses their history holds | done (merged to `main` 2026-10-23, `7fda074`) | Haiku 5.5 | S19, S24 |
| S20 | The book: the journal as an openable book with Nouns, Verbs, Phrases, Lessons and What I said pages (D-game-16) | done (merged to `feature/prototype` 2026-10-22, `ee2ec79`) | Haiku 5.5 | S15, S19 |
| S20b | Replay any noun or phrase from the book (speaker button, slow with Shift); card recordings for the 12 block nouns; heard-only phrases play their NPC line | done (merged to `main` 2026-10-23, `b9c64de`, bundle `e244285b…`); **the owner listens to the 24 new card WAVs** | Haiku 5.5 | S20, S24 |
| S21 | F8: the in-game bug and feature report, saved as Markdown with a screenshot, game state, recent turns and the session log tail; local only (D-game-17) | done (merged to `feature/prototype` 2026-10-22, `917d2c4`) | Haiku 5.5 | S19, S20, D-game-17 |
| S18 | The playtest refresh (build, checklist, report, launcher message; the consent text mentions that the book keeps what the player said; the facilitator collects the F8 reports) | done (merged to `feature/prototype` 2026-10-22, `3c9a741`); **the owner's pre-flight** (`docs/game/playtest.md`) | Haiku 5.5 | S15, S17, S17b, S20, S21 |
| S22 | The second pass's known issues: an uncrowded conversation screen, the F10 label, measured session durations, a Vosk route step and test, thin pergola posts, the book in greyscale, the book item for old saves | done (merged to `feature/prototype` 2026-10-22, `8609183`) | Sonnet 5.5 | S18 |
| S23 | LanguageTool over the game's Spanish: strings extractor, first waiver round, the `si quiero` and `traveller` findings, an advisory gate step (required after the waiver round) | done (merged to `feature/prototype` 2026-10-22, `ddb834a`); the gate's game-v1 grammar step is required | Haiku 5.5 | S22, core M30, D10 |
| S24 | The audio system (mixer, ducking under the voices, silence while the microphone records), interaction and progress sounds; CC0 packs plus code, no music (D-game-18) | done (merged to `main` 2026-10-22, `9291bab`); **the owner listens to the cues once** (judged by name and length only) | Sonnet 5.5 | S23, D-game-18 |
| S25 | Ambient beds by place (birds, wind, river and waterfall, village, fire, dusk insects) and footsteps per surface | done (merged to `main` 2026-10-23, `6c73d63`); **the owner listens once** | Haiku 5.5 | S24 |
| S26 | Verbs in the lessons: a yo/tú unit in session 1, lesson "Personas" (él/ella/usted, nosotros, ustedes/ellos) in session 2; lessons unlock by session and replay from the book; Opus pass, speech (D-game-19) | done (merged to `main` 2026-10-23, `7748348`, bundle `becd2669…`); **the owner listens to 62 new WAVs** | Sonnet 5.5 | S25, D-game-19 |
| S27 | Verbs, session 3: "Ayer y mañana" (ir a + infinitive, a few preterite forms, time words) | done (merged to `main` 2026-10-23, `ca27650`, bundle `492cbe4b…`); **the owner listens to 44 new WAVs** | Sonnet 5.5 | S26, core M49 |
| S28 | The book remade as a book (two-page spread, ribbons, page turn) with conjugation tables (used, taught, hidden until taught) | done (merged to `main` 2026-10-23, `f966590`) | Sonnet 5.5 | S20b, S27 |

**Standing direction (owner, 2026-10-22, late):** finish the queued milestones (M50, S19b, S20b, S25, S26, M49, S27, S28) and push each
to `main`. Do not work on the German milestones (M31 to M48, D14); they wait until much later. After the queue, the orchestrator researches
and makes its own improvements to the game on a new branch, `feature/experiment` (not `main`).

Each milestone ends with the gate in section 6 and a line in section 5. Since 2026-10-22 (owner) every milestone is gated with
`scripts/merge-gate.sh "S<nn>:<branch>"` from the `main` checkout and pushed straight to `main`. Since 2026-10-23 (owner) nothing is
merged into or pushed to `main`: milestones are gated with `GATE_TARGET=<feature branch>` and pushed to that branch, and the owner merges
it into `main` by pull request. S0 to S23 were merged into
`feature/prototype`, which `main` fast-forwarded to; that branch is retired.

Prototype order (owner, 2026-10-21): G1 and G2 run alongside P1 and P2 (P1 and P2 do not use the packages); then P3, the smallest vertical
slice; then P4 to P9; P10 is optional. Details, demos and test approaches per milestone: `docs/archive/game/research/prototype-plan.md`; reasons and
sources: `docs/research/2026-10-21-prototype-research.md`. G5 is replaced by D-game-1 and P2.

### 4.1 G3: the innkeeper (design, moved from the core's plan section 7.4)

The first new NPC scene, chosen from the loop the core's design names (spells, trade, quests, dialogue): trade and spells exist, quests and
everyday dialogue are the gaps. The innkeeper is an NPC with a small everyday exchange.

| Scene / NPC | New intents | Example utterances | Slots |
| --- | --- | --- | --- |
| Innkeeper (`innkeeper`) | `order_item` | quiero una cerveza; dos panes, por favor | item, qty |
| | `ask_directions` | ¿dónde está la herrería? | place |
| | `ask_name` | ¿cómo te llamas? | |
| | `introduce_self` (exists in the core sample) | me llamo Ana; soy Ana | name (`[WORD:name]`) |
| | `farewell` (exists) | adiós; hasta luego; hasta mañana | |
| | `thank` (exists) | gracias; muchas gracias | |

Cross-cutting, used by every NPC: `confirm` and `deny` (exist in the core sample; `sí; claro; vale, acepto` and `no; no, gracias; ahora no`).

Checklist per new intent: an intent definition with its required slots, at least two slot grammars, a keyword rule and at least five examples
(one with a typical learner error); answer sets where a constrained prompt needs them (`food-drink`, `places`, `yes-no`); open and
constrained prompts for the scene (about twelve, with difficulty); dialogue pools `<intent>_reply` with at least three variants using
template filters, plus `greet_reply`, `not_understood` and the innkeeper's pools, all passing `cefr-check` at A1 and A2 (extend `cefr/a1.txt`
and `cefr/a2.txt` by hand where a template needs a word); the innkeeper's agent in `agents/innkeeper.json` with its variant pools (the
core's bundle format has `agents/`, not `npcs/`; check the core's `docs/nlp/pipeline.md` and the loader before adding any file kind); effects
entries (`order_item` maps to a tavern price curve through the host, not in the core); no change to the feedback policy or the evaluation
config.

The core needs no change for this scene. `[WORD:slot]` already exists in the core (M17b-core): it matches one all-letter token (accents and
ñ included; no digits, punctuation or hyphens), binds the normalized text, and the bound word carries no reading, so it never triggers
`vocab.unknown`, the agreement check or a recast. Known limits to document: hyphenated names do not match, and recasts print names in lower
case. The DSL has no tense constraint, so `report_quest` (G4) accepts any form of the verb; person agreement is still checked.

Gates: the section 6 gate; `cefr-check` clean at A1 and A2; the bundle validated, hashed and re-tested; the host demo lines for the scene
(`--transcript ... --prompt <id>`) recorded in the README; the innkeeper's utterances evaluated through the host with the expected errors.

### 4.2 G4: the quest giver (design, moved from the core's plan section 7.4)

| Scene / NPC | New intents | Example utterances | Slots |
| --- | --- | --- | --- |
| Quest giver (`captain`) | `ask_quest` | ¿tienes trabajo para mí?; ¿hay una misión? | |
| | `report_quest` | he matado tres arañas; maté tres arañas | target, qty |

The quest giver's state machine: idle, then offering_quest (`confirm` or `deny`), then quest_active (`report_quest` with the right target and
quantity), then quest_done. Quest rewards are a game curve (`effects`); the host decides what a finished quest changes. The per-intent
checklist of G3 applies. Answer set `quest-targets` is new. Its `report_quest` example needs at least one wrong-tense learner error to show
the known limit above.

### 4.3 The wider world of things and crafting (owner, 2026-10-21; design for P6, P6b and P7)

The owner asked for more things to name and for crafting, with the guide answering how to make things and where to find materials.
Decided: crafting comes after P6 and before P7 (surface materials first; caves add ores), and some recipes need a station (Terraria style).
Every tile and item below is a catalogue noun in `game/nouns.json` and follows D-game-6 (spaced colour); the word list stays single-word
nouns at A1 or A2 so the slot grammars stay simple. The agents check each lemma's noun reading and gender in the bundle's lexicon and add
missing ones to `lexicon/world.json`; words outside A1 go to the A1 or A2 list with a note for the owner's content review.

| Milestone | Tiles (in the world) | Items (in the inventory) |
| --- | --- | --- |
| P6 | `tierra`, `hierba`, `piedra`, `árbol`, `arena`, `flor`, `seta`; placed: `madera` (planks), `puerta`, `mesa`, `silla`, `cama`, `caja` | `madera`, `piedra`, `tierra`, `arena`, `flor`, `seta`, `hacha`, `pico` |
| P6b | placed: `horno`, `cristal`, `ladrillo` | `espada`, `puerta`, `mesa`, `silla`, `cama`, `caja`, `horno`, `cristal`, `ladrillo` |
| P7 | ores `carbón`, `cobre`, `hierro`, `oro` (caves) | the same, plus `antorcha` |

Recipes (P6b; numbers are a first pass for the playtest, kept in `game/recipes.json`, never in code):

| Station | Recipe |
| --- | --- |
| by hand | `mesa` ← 10 `madera` |
| `mesa` | `puerta` ← 6 `madera`; `silla` ← 4 `madera`; `cama` ← 15 `madera`; `caja` ← 8 `madera`; `espada` ← 2 `madera` + 6 `piedra`; `horno` ← 20 `piedra` + 4 `madera` |
| `horno` | `cristal` ← 2 `arena`; `ladrillo` ← 2 `piedra` |
| P7 adds | `antorcha` ← 1 `madera` + 1 `carbón` (by hand); metal tools at the `horno` |

The guide's crafting answers (P6b, Sonnet for the design, the content by Haiku):
- `ask_craft`: "¿cómo hago una mesa?", "¿qué necesito para una puerta?", "¿cómo se hace un horno?". Slot `item` (a catalogue noun).
  The answer names the ingredients with their numbers, the station, and what the player is missing from the inventory: "Para una puerta
  necesitas seis bloques de madera. Usa la mesa." / "Te faltan dos." Mass nouns (`madera`, `piedra`, `arena`, `tierra`) are marked
  `mass` in `nouns.json` and counted with `bloques de`.
- `ask_find`: "¿dónde hay arena?", "¿dónde encuentro madera?", "¿de dónde sale la madera?". Slot `material`. The answer says where the
  material comes from (`sources` in `nouns.json`: `madera` from `árbol`) and the nearest source, with the P3 arrow: "La madera sale de los
  árboles. Hay un árbol allí, a la izquierda, a cinco bloques." The world line uses the CR-003 workaround (the game renders it).
- An item the game does not know or has no recipe gets a pool line ("Eso no se hace.").
- Crafting panel: recipes the player can make here, with Spanish labels drawn by the colour rule (a grey icon until the word is coloured);
  seeing a label counts as "read", never as a spoken credit. Crafting itself is plain game logic in `Vocaterra.Simulation` (inventory
  arithmetic, deterministic), the recipe data in the bundle, the panel in `Vocaterra.MonoGame`.

### 4.4 The prototype scenario (the S series; owner's brief v1.2, D-game-8)

The design of record is `docs/game/scenario-plan.md`: section 2 maps the brief to the codebase, section 4 gives each milestone's deliverables and
demo, section 5 the determinism and boundary rules that carry over, section 6 the possible core gaps (M29a, M29b), section 7 verification.
The brief itself is `docs/game/design/prototype-brief.md`. Order: S0, then S1 beside S5 (disjoint folders), then S2, S3, S4, S6, then S7 beside S8 only if
their files stay disjoint (otherwise one after the other), S9, S10, S11, S12. P8, P9, G3 and G4 are superseded (section 4 table); the
blacksmith and spell material leaves `content/game-v1` in S5 (the core's sample keeps it). The route: spawn in a forest clearing with the camp
(a `mesa`, an unlit fire ring, the guide), a path south-east to the village (a stall with an awning, three houses, a signpost `Tienda / Río`),
the riverbank (water, a bridge). The five phrases: "¿Qué necesitas?", "Quiero agua, por favor.", "¿Quieres pan?", "Sí, por favor." /
"No, gracias.", "¿Dónde está el río?".

### 4.5 Experiments on `feature/experiment` (the X series; owner's standing direction, 2026-10-22)

The owner: "I want you to research and make improvements to the game against a new branch called feature/experiment for any changes you come
up with." The X series is the orchestrator's own work. It is planned here, gated with `GATE_TARGET=feature/experiment` from a
`feature/experiment` checkout, and pushed to that branch only, never `main`. The rules are the same as everywhere else: briefs, model
policy, machine resources and the merge gate. A change that needs an owner decision (a licence, scope, a D-number) is brought to the owner,
not made. The German milestones stay parked (D14).

| ID | Experiment | Status | Model | Depends on |
| --- | --- | --- | --- | --- |
| X00 | Research: a ranked backlog of improvements (learning effect, player experience, robustness, technical debt), with evidence from the code, plans, findings and screenshots | done (`docs/research/2026-10-23-improvement-backlog.md`, R01 to R18) | Sonnet 5.5 | S28 |
| X01 | Saves survive content updates: a save made under an older bundle loads under a newer one (learner record, lessons, phrases, inventory and world edits kept where their ids still exist; the rest dropped and logged), instead of being refused; a strict mode keeps the refusal for the playtest's frozen bundle | done (merged to `feature/experiment` 2026-10-23, `bf90201`) | Sonnet 5.5 | S28 |
| X02 | Core M51: `ir` + a bare infinitive (`voy comer`) flagged | done (merged to `feature/experiment` 2026-10-23, `e70441c`; see core §5b M51) | Sonnet 5.5 | M49 |
| X03 | Book polish: the right page scrolls; `vamos` glossed "we go" in What you said; ser and estar glossed apart ("to be: what", "to be: where or how") | done (merged to `feature/experiment` 2026-10-23, `08152fb`, bundle `2156744b…`) | Haiku 5.5 | S28 |

| X04 | Session summary card from the learner's record (R01), and a second try after "No sé" in the same session (R02) | done (merged to `feature/experiment` 2026-10-23, `6e14821`) | Haiku 5.5 | X01 |
| X05 | Autosave at safe points, and a crash handler around `game.Run()` that writes a crash report next to the F8 reports (R04) | done (merged to `feature/experiment` 2026-10-23, `001d75a`) | Haiku 5.5 | X01 |
| X06 | Review deck: a session warm-up of due items, a "Review now" button in the book, and a `review` lesson step that brings earlier phrases back inside later lessons (R06, R07) | done (2026-10-30, merged by PR #17): the deck, the warm-up offer, Review now, the `review` step type, tests; the `review` steps in Personas and Ayer y mañana came with V10; no scripted run of a warm-up is recorded | Sonnet 5.5 | X03, X04 |
| X07 | Tools for the owner: `--mic-lab` (measure the real microphone path) and `--bench-route` (a real-GPU benchmark). Built only: the agent runs neither (R03 first half, R13) | planned | Haiku 5.5 | X05 |
| X08 | UI scale and readable text: a text-scale setting and layouts that hold at larger sizes (R09) | done (merged to `feature/experiment` 2026-10-23, `f3cf721`) | Sonnet 5.5 | X03 |
| X09 | Playtest tooling: per-step evidence in the report, and a test that keeps the playtest documents' counts equal to the bundle (R10; the counts drifted in S26, S27 and S28) | planned | Haiku 5.5 | X04 |

**Waiting on the owner** (from X00's open questions; not planned until answered):
- R05: may the pre-check stop early for a lost beginner?
- R12: may lessons adapt per learner?
- R14, R15, R18: numbers, more NPCs and orders, before or after the first pilot?
- R11: a title screen now?
- the voices for new NPCs;
- running the mic lab, the GPU bench and the pre-flight;
- the pilot's date.
The rest of the backlog (R08 feedback audit, R16 technical debt, R17 small issues) follows the first batch.

### 4.6 Repository milestones (R01, R02; owner, 2026-10-23)

Two-digit `R01`, `R02` are repository-wide milestones (both halves at once), not the research items R1 or the backlog items R08 to R18
above. They land on the feature branch `feature/flatten` (never `main`; the owner merges it by pull request). Plan:
the owner-approved plan of 2026-10-23 (the flatten, then one bundle).

| ID | What | Status |
| --- | --- | --- |
| R01 | Flatten `nlp/` and `game/` into one root layout (`src/`, `tests/`, `tools/`, `docs/nlp/`, `docs/game/`, `content/`, `assets/`, `models/`, `scripts/`); one `Directory.Build.props` keyed on project name; `Vocaterra.sln` regenerated (the three duplicated tool entries gone) plus `Vocaterra.Nlp.slnf`; Apache-2.0 everywhere (D-game-20); the gate refuses `main` | done (Sonnet), gated on `feature/flatten` 2026-10-23, pushed |
| R02 | The core runs on `content/game-v1`; `content/v1` (the blacksmith sample, core D7) is deleted. Core tests break when game content breaks the core (the owner wants that signal); hash literals become computed checks; effects, quantity slots and the worked example get in-memory fixtures; goldens re-recorded on game-v1 | done (Sonnet), gated on `feature/flatten` 2026-10-23, pushed |
| R16p2 | R16 phase 2 and the game's folders (`docs/archive/game/game-projects-layout.md`): engine-free logic to `Vocaterra.Game.Core`, `GameKey`/`IInputFrame` (D-p2-1a), Game regrouped by area and feature, tests mirror source folders, stale project names fixed. Batches 1 to 8 on `feature/game_refactor` | done (2026-10-28, on `feature/game_refactor`; see the R16 phase 2 entry in section 5) |

**R01 findings.**
- 1517 renames at 100% in the move commit; `git log --follow` reaches pre-move history on both sides. Test counts identical to the
  pre-move baseline in all 17 test projects (no models). Gate with models: 4909 passed, 3 skipped, 1 failed (below); build 0 warnings,
  format, content, grammar and licence scan clean.
- **Pre-existing failure, not R01's:** `SpokenModelTests.AMistakeInASpokenLineIsHeardAsSaidAndTheCoreCorrectsIt` (needs Kokoro and Vosk)
  hears `a dónde está la árbol` for the synthesized `dónde está la árbol` and expects the latter. It fails identically on the pre-move
  commit `e0ab15a`; model-gated tests are skipped in agent runs without `VOCATERRA_MODELS_DIR`, so it went unnoticed. To be fixed in its
  own game milestone (the constrained grammar now offers `a dónde`; decide whether the test or the recognizer's phrase set is wrong).
- `tests/Vocaterra.Game.Core.Tests/Fixtures/save-v1.json` stores the game-v1 bundle hash: every bundle re-hash must update it, or two save
  tests fail.
- `.github/workflows-pending/ci.yml` still uses the old `nlp/` paths (left untouched; the CI milestone rewrites it).

**R02 findings.**
- Gate with models: 5196 passed, 3 skipped, 1 failed (the pre-existing `SpokenModelTests` failure above); build, format, content, grammar
  and licence scan clean. No per-project count dropped; Content 606 -> 867 and Pipeline 244 -> 264 from data-driven cases over every
  game-v1 intent example, prompt, agent and answer phrase; `GameBundleContractTests` replaces `SampleContentTests`.
- A deliberate break (the prompt `villager-open` removed) fails 172 core tests (Content, Pipeline, Golden, Fuzzing): the coupling works.
- Core bug found by the fuzz suite on game-v1 and fixed: `AgreementChecker` reported `conj.tense` twice on one span when a past marker
  met `ir a` + a finite verb (O2, seed `3KdllRVaJeE6`); regression tests in `NearFutureTenseTests`. Generator and model fixes, no oracle
  weakened: the sentence realiser respects time adverbs and the infinitive after `ir a`; O11's agent model skips host-event transitions
  (CR-015); two conditional O15 tolerances (a dialogue pool no agent names; slot grammars of an intent with keyword rules) and the lint
  fields as optional.
- The core demo is now `eval --transcript "yo quiere dos espada" --prompt villager-open --content content/game-v1` (exactly `conj.person`,
  `noun.number`, `qty=2`); the design document's `yo tiene dos espada` is a pipeline test on an in-memory prompt.
- A model-gated test (`VoskPipelineTests.ARealRecognitionReplaysWithoutTheModel`) still named a v1 prompt; agent runs skip model tests, so
  only the gate with models caught it. Briefs that touch content should ask for one with-models run of the affected projects.
- Game-v1 content notes (reported, not changed): `cefr/a1.txt` and `a2.txt` share 7 lemmas; the `feedback.recast.conj.person` pool needs
  a `verb` slot no game grammar names, so the generic recast answers; `quiero una azul espada` on `villager-open` is `word_order` but not
  communicated (`request_item` has no adjective slot).

**CI requests not yet planned** (moved on 2026-11-03 from `.claude/agents/orchestrator.md`; the text is unchanged). CI itself,
`.github/workflows/ci.yml`, has run since 2026-10-31.

- Still open from the owner's 2026-10-23 answers:
  - **SonarCloud** as in `james-d12/CodeGuard` `.github/workflows/ci.yml` (`dotnet-sonarscanner` begin/end around build and test,
    Cobertura coverage), with organisation `personal-james`, project key `personal-james_vocaterra`, name `Vocaterra`, and a `SONAR_TOKEN`
    secret the owner creates;
  - speech models downloaded and cached in CI, so the model-gated tests run;
  - CI on pushes to `feature/experiment` too;
  - ideally the checks shared with `scripts/merge-gate.sh` (one checks script).
- **The scripted runs as integration tests in CI or the gate** (owner, 2026-11-07, for later): the runs of
  [scripted-runs.md](scripted-runs.md) prove that parts of the game work end to end, but today they are only run by hand before and after a
  front-end change and compared with `scripts/compare-runs.py`; neither CI nor `scripts/merge-gate.sh` runs them. To plan: a headless
  display in CI (Xvfb with Mesa's software GL for MonoGame DesktopGL); a pass/fail rule (the game exits 0, writes its screenshots and saves,
  logs no exception; then perhaps a comparison against a committed baseline with the existing masks); the speech models for the runs that
  need them (r11-mic, r17-speak, the same need as the cached models above); the cost per run (a few minutes for the whole set), and
  whether the gate takes a small smoke subset while CI takes them all. Water runs (swimming with the bubble meter; a pour and a dam) were
  proposed on 2026-11-07 and set aside until then.
- Not chosen: a "no pending rows" gate and a Claude review in CI.

### 4.7 Content as data and the L1 locale layer (the C series; owner, 2026-10-29, D-game-21)

The plan, the milestones C01 to C10 and their states are in `docs/game/content-plan.md` (section 4). The series runs on `feature/content`
(made from `main` at `604903f`, after R16 phase 2 landed): every commit goes straight to it and is pushed there, never `main`, and the
gate runs on it at the end of each milestone. On the owner's
instruction one session (Opus 5.5) does every milestone itself, with no subagents. The findings of each milestone go into section 5.

**Closed (owner, 2026-10-29: "work for content is done").**
- C01 to C08 are done and gated on `feature/content`, and go to `main` by pull request #16.
- C09 (items, blocks, sounds and signs as data) is assessed: sounds first.
- C10, the authoring CLI (`new npc|objective|lesson`, `refs <id>`, `check`), is specified in `content-plan.md` section 3.8.
- Neither C09 nor C10 is built; each becomes a milestone when the owner asks for it.

### 4.8 The village and the lessons (the V series; phase A done 2026-10-31, phase B done 2026-11-01, D-game-22)

The owner's request (2026-10-30): a bigger village with more NPCs (a blacksmith, a baker) and more lessons, without a larger map,
with the NPCs interlinked, the map's space used, the lessons connected, A1 to A2; and, the same day, "past, present, and future use of
verbs" in the lessons. The plan, the design and the milestones V01 to V17 are in `docs/game/village-plan.md`: the village grows inside its
own clearing (a bakery, a smithy, a well, the farmer at the field, a fisher on the bridge's east landing); four NPCs, three items (a coin,
an apple, an axe) and four orders tie them together; seven lessons, sessions 4 to 10, each reusing the last and using its verbs in the
present, the preterite and the future around the order it carries (the plan, the doing, the report), with the simple future at A2 in
session 9. Phase A (V01 to V07, V10 to V12: sessions 4 to 6, A1 complete) and phase B (V08, V13 to V17: A2) are separated by an owner
decision. The series runs on `feature/village`, commit prefix `V<nn>:`. **Phase A started on 2026-10-30** (D-game-22: the plan's
proposals, the farmer León, provisional voices heard afterwards, phase B held; every milestone by Opus 5.5 at xhigh effort); **phase A was done on
2026-10-31** (V01 to V07, V09, V10 to V12 and the follow-up V12b; 6030 tests passing, bundle `7ba32270…`). The owner's playtest the same
day added V12c (the next session in the game), and the CI run on PR #20 the core's M60 (the lint's speed). **Phase B started on 2026-10-31**
on the owner's word: all of it on `feature/village`, reviewed by the owner at the end. V13 (session 7) was done on 2026-10-31; after the bottlenecks study the owner added V18 and moved the core's M56, M59 (before V14) and M58 (before V15) into the series. **Phase B was done on 2026-11-01** (V08, V13 to V18, V15b and V09 again, with the core's M56 to M62 and M62b; sessions 7 to 10 at A2, ten lessons; 7259 tests passing, bundle `36b2b8ad…`); the owner reviews the branch and PR #20. **The review** (2026-11-01, `docs/qa-reports/2026-11-01-review-feature-village.md`) found ten points; on the owner's word all were fixed before the merge: the core's as M64, the game's as V19 (7420 tests passing, bundle `0f8faea0…`). The milestones' statuses are in the plan's section 4. The findings of each milestone go into section 5.

### 4.9 The adaptation: needs, lessons as classes, presentation and the bread pilot (N, L, U and W series; D-game-23 to D-game-25)

The owner's Adaptation Design Document (`docs/game/design/sources/Vocaterra_Adaptation_Design_Document.docx`) is the specification:
section 9 the needs (N), 5.3 and 5.4 lessons as classes (L01), 7.2 and 14.3 the presentation (U01, U02), 10 the bread procedure (W), 15
the sequence and 18 the decisions. The owner said on 2026-11-04 "implement this design" (D-game-23, the answers of 2026-11-04): phases
B to E on `feature/adaptive_design`, branched from `feature/village-decisions` (the follow-ups), with section 18.2's recommendations
adopted provisionally for the owner's confirmation. The W rows are in `docs/game/activities-plan.md` section 5; their status is kept
there. Commit prefixes `N<nn>:`, `L01:`, `U<nn>:`, `W<nn>:`; one milestone branch each, gated into `feature/adaptive_design`.

| ID | What | Depends on | Size | Model | Status |
| --- | --- | --- | --- | --- | --- |
| N01 | The needs model (design 9.2 to 9.4, 9.10, 9.13): five needs in `Game.Core/Needs/` (`PlayerNeeds`), their stages and links, `game/needs.json` and its schema, save version 6 with the optional systems map, the world events, `--needs off` and `--set-need`; the session told each tick whether play is active | the follow-ups | M/L | Sonnet 5.5 | done (2026-11-05, gated at `3a63832e`, 9158 passed) |
| L01 | Lessons as optional classes (5.3, 5.4; D-game-25): the introduction alone required; classes offered and chosen; sleep when the sleep drive is below 75; orders by NPC met; no lesson holds an NPC, an order or the day; r20 and r21 re-baselined, a new run with no classes | N01 | M | Sonnet 5.5 | done (2026-11-05, gated at 971b1058, 9451 passed) |
| N02 | Eating, drinking and sprinting (9.6): eat and drink actions, water by hand, the sprint control, the gathering cost, rest at a lit fire | N01 | S/M | Sonnet 5.5 | done (2026-11-05, gated at `3a4345f8`, 9195 passed) |
| N03 | Sleep and the collapse (9.5): a night's effects, the collapse and waking where the player fell, every night and collapse advances the day; the offer rule is L01's | L01, N02 | S/M | Sonnet 5.5 | done (2026-11-05, gated at `7cf2e115`, 9219 passed) |
| N04 | Health and death (9.4, 9.7): losses and recovery, death and the belongings chest (decision 10), waking at the fire; runs r25-thirst and r26-death | N03 | M | Sonnet 5.5 | done (2026-11-05, gated at `b72b9184`, 9246 passed) |
| N05 | The HUD and the opening (9.8, 9.9): bars, icons, stage lines, sounds, view effects and their option, the tip and the death screen, the relaxed-needs option (decision 17); layout tests at every scale; the day-1 route proven safe | N04 | S/M | Sonnet 5.5 | done (2026-11-05, gated at 6dfa8566, 9433 passed) |
| N06 | Food that grows back, and fair gifts (9.6; decision 12): berry bushes regrow after a night; bread and water gifts once per in-game day | N05 | S/M | Sonnet 5.5 | done (2026-11-05, gated at 468a1b29, 9382 passed; built before N05, its Spanish lines left to N05) |
| W01 | `item_crafted`: a craft is a world event (activities plan) | N06 | S | Haiku 5.5 | done (2026-11-05, gated at 56f88b4b, 9385 passed) |
| N07 | Cooking at the fire (9.6): the noun `fuego`, an apple to a roast apple at any lit fire, through the crafting path and `item_crafted` | W01 | S/M | Sonnet 5.5 | done (2026-11-05, gated at 20a8da0c, 9461 passed) |
| N08 | A berry planted at the camp (decision 14, adopted provisionally) | N06 | S | Haiku 5.5 | done (2026-11-06, gated at 209049db, 9561 passed) |
| N09 | The drive rates as a setting (owner, 2026-11-05): a slider in the settings and `--needs-rate`, multiplying every drive's rate, for the needs playtest; a fixed value after it | N05 | S | Sonnet 5.5 | done (2026-11-05, gated at 37c7640b, 9501 passed) |
| U01 | Clean defaults (14.3): the debug text off by default; one speak hint; the objective tracker; room for the need meters | N05 | S | Sonnet 5.5 | done (2026-11-05, gated at dd01a6e9, 9476 passed) |
| U02 | The conversation surface (7.2): developer notes behind `--dev`/F3, older lines collapsed, the lesson strip in the header, every D-game-9 route reachable by key | U01 | M | Sonnet 5.5 | done (2026-11-06, gated at fe986862, 9546 passed) |
| U02b | The conversation surface's QA fixes (`docs/qa-reports/2026-11-06-ui.md`, ui-1 to ui-5): the expanded panel scrolls to every older line, the current exchange fits at 800x600 and 150 %, the not-understood turn's English and coaching line, the lesson heading's shortening, the hidden-lines plural | U02 | S | Haiku 5.5, then Sonnet 5.5 | done (2026-11-06, gated at 565554f3, 9586 passed) |
| The needs check | The owner and two or three testers play days 1 to 3 with needs on (9.12) | N01 to N07 | | the owner | waits for the owner |

After U02 come W05 to W08 and W10 (activities-plan section 5; design section 10); W11, the pilot, waits for testers and the owner's
listening (decision 2). Since 2026-11-06 W05 to W07 are built on `feature/bread-pilot` while the owner runs the needs check
(D-game-23, owner's answers of 2026-11-06).

### 4.10 The conversation panel and the book (GitHub issues #5, #6 and #7; U05 to U08; owner, 2026-11-08)

The owner filed three UI issues and asked for them to be investigated and planned on a worktree from `main` (2026-11-08): #6 the NPC's
name in the conversation panel is too small, #5 the conversation panel should scroll through the whole history, #7 the book has no visual
hierarchy. The handover of 2026-11-05 left "a visual redesign of the look (… the book …)" unplanned; the issues make it planned work. U03
and U04 stay reserved for the design document's presentation track (section 18), so these are U05 to U08. Branch `feature/ui-issues`
(from `main` at `ca4b69c8`), one milestone branch each, gated into it; commit prefixes `U05:` to `U08:`. The scripted-run pictures that show
the panel or the book will change; re-record them and say which in the report.

| ID | What | Depends on | Size | Model | Status |
| --- | --- | --- | --- | --- | --- |
| U05 | The NPC's name as the panel's title (#6): the name at 32 px, the lesson heading moved off its line, the "Guía: " prefix dropped from the NPC's lines; 16 px in the compact panel | | S | Sonnet 5.5 medium | done (2026-11-08, gated at `1f56f6b9`, 10402 passed) |
| U06 | The panel scrolls (#5, step 1): the mouse wheel opens and scrolls the older lines, a scrollbar while lines are out of view, one page per NPC | U05 | S/M | Sonnet 5.5 medium | done (2026-11-08, gated at `dfbb778f`, 10419 passed) |
| U07 | The book's hierarchy (#7): line kinds, a type and ink scale, one row per entry with the details on the right page, each page's layout; a mockup for the owner first | | M/L | Sonnet 5.5 high | done (2026-11-08, gated at `da52f055`, 10453 passed; the owner's answers D-game-28) |
| U08 | The conversation history kept in the save (#5, step 2): each NPC's lines saved, capped per NPC, shown again after a restart | U06 | M | Sonnet 5.5 medium | done (2026-11-08, merged at `f87776bb`; its gate failed on a core fuzz seed found before U08, fixed by core M91; gated with M91 at `6275653f`, 10431 passed) |

**One branch and one pull request** (owner, 2026-11-08): U05 to U08 all land on `feature/ui-issues` and in PR #47, which closes #5, #6 and #7.

**Order:** U05, U06, then U07 (its mockup can start at once, it touches other files), then U08.

**What the investigation found (2026-11-08, at `ca4b69c8`).**

- **#6, the name.** `DialoguePanel.Draw` (`src/Vocaterra.Game/UI/Dialogue/DialoguePanel.cs`) draws `DialogueModel.NpcName` in the body's font
  (`FontProvider.NativeSize`, 16 px), and `DrawLessonHeading` puts the lesson's heading beside it in the brighter `HintColor` at the same
  size, so the heading takes the eye. Every NPC line repeats the name as a prefix ("Guía: ") at the same weight, so the title is not set
  apart from the lines under it. `DialogueLayout` places the lines at a fixed `Panel.Y + 8 + LineHeight + 6`, with no header height of its own.
- **#5, the history.** The history exists but is short and hidden. `DialogueModel` keeps the last 60 lines (`MaxLines`), in memory only;
  `SwitchTo` clears them when the learner talks to another NPC; nothing is saved. The panel opens on the current exchange (U02); PageUp or O
  shows the older lines and Up and Down scroll them (U02b). `DialogueScreen` does not read `input.Wheel` (the book's `BookScreen` does), and
  there is no scrollbar. The only sign of the older lines is the row "N earlier lines hidden (PageUp or O)", and `PanelFitter` drops it first
  when room is short, so in the issue's picture (with the developer note on) nothing shows that more lines exist.
- **#5, the storage the issue asks about.** No database is needed. The save is one JSON document (`SaveDocument`); it already keeps every
  line the learner said, uncapped (`LearnerRecordSave.History`, `InputEntry`: session, stored time, text, NPC, prompt, intent, errors), which
  the book's "What I said" reads. Only the NPC's side is missing, a few kilobytes per NPC. SQLite would be a native package (the
  `Game.Core` references none; `ArchitectureRulesTests` gate it) and a second persistence path beside the one save file. If an uncapped log is
  ever wanted, an append-only `.jsonl` file beside the save is the next step, still not SQLite.
- **#7, the book.** Every list row is 16 px text on one fixed 19 px grid (`BookGeometry.LineHeight`; the scroll, the selection and the
  speaker and practise buttons' hit tests count in rows). The page's title, the entries and their count rows look alike. The five
  `BookTone` inks are all browns on cream: `InkHint` (104,90,74) and `InkUnseen` (108,92,72) are almost the same, and `InkKnown` (116,70,6)
  is lighter than `Ink` (44,32,26), so a known word reads weaker than one being learnt. Each entry carries a count row ("heard 1 read 3
  said 0 correct 0") as heavy as the word, also for words not met (four zeros). `BookPages.Phrases` adds no `BookLine.Gap`, so the
  phrases page is 92 rows without a break. The lessons list writes out every unit and phrase of every lesson; "What I said" repeats
  "session 1" on each entry and cuts the feedback with "…". The font is PixelOperator (`assets/fonts/pixel-operator`), sharp only at whole
  multiples of 16 px, so the scale is 16 and 32, and the rest of the hierarchy comes from ink, spacing and less text.

**U05, the NPC's name as the panel's title (#6).**

- The name at 32 px (`FontProvider.NativeSize * 2`, as the book's titles), in `NameColor`, with a rule under it.
- `DialogueLayout.HeaderHeight`, used by the lines' top, the status and the lesson heading; the layout tests at every UI scale and at
  800 x 600 still hold (the current exchange fits, U02b).
- The lesson heading on a second header line, or right-aligned in the muted colour; it no longer competes with the name.
- The NPC's lines lose the "Name: " prefix; colour and the English under them already say whose they are. The learner's "You: " stays.
- In the compact panel (`CompactBelow`) the name stays at 16 px so the few rows are kept.
- Tests: the layout's header for both sizes; the lines' prefixes; the hit tests of the buttons unchanged.

**U06, the panel scrolls (#5, step 1).**

- The wheel in `DialogueScreen`, as in `BookScreen`: up expands the page (as PageUp) and scrolls towards older lines; down scrolls back, and
  at the newest line collapses to the current exchange. PageUp, O, Up and Down keep working (D-game-9: every route by key).
- A thin scrollbar at the right of the lines while lines are out of view, in place of the hidden-lines row that `PanelFitter` drops for room.
- One page per NPC (a dictionary of line lists in `DialogueModel`), so talking to the baker keeps Luna's page; `MaxLines` per NPC, raised to
  200.
- Tests in `Game.Core`: wheel to expand and back, one page per NPC, the scrollbar's thumb from the fit.

**U07, the book's hierarchy (#7).**

- **First a mockup** of the five pages for the owner (a picture or an artifact), before the build; the owner's answers are D-game-28
  (2026-11-08).
- **Line kinds:** `BookLineKind` (Section, Entry, Meta, Gap) on `BookLine` in `Game.Core`, set by `BookPages`; the layer picks font, ink and
  spacing from the kind, not from tone and indent. A Section takes two grid rows, so the row-based scroll and hit tests stay.
- **Scale and ink:** the page title at 32 px with the page's ribbon colour as accent; entries in the darkest ink; meta clearly lighter and
  not the unseen colour; known items strongest, unseen faded. The `UiPalette` contrast checks stay and cover the new inks.
- **One row per entry:** icon, word, pips or lamps, speaker button. The counts move to the right page, which already shows the selected
  item; no zero counts for items not met.
- **Per page:** Nouns in sections Known, Learning, Not met yet, the unmet ones as a row of icons. Phrases with Spanish dark and English
  light, the "help: …" text dropped (the lamps say it, with a key), a gap or a group per lesson. Lessons one row each (title, progress,
  state), the current one marked; units and phrases on the right page. What I said grouped under a heading per session, the outcome as a
  coloured mark (understood, small mistake, not understood) with the full feedback on the right page. Verbs: the legend as swatches.
- Tests: `BookPages` emits the kinds; the spread and scale layout tests (`BookSpreadTests`, `UiScaleLayoutTests`); the speech and practise
  buttons' hit tests.

**U08, the conversation history kept in the save (#5, step 2).**

- Each NPC's page in the save (an optional map in `SaveDocument`, NPC to its last 200 lines with their speaker kind); restored on load,
  so the panel scrolls back over earlier sessions. Old saves need not load (the game is unreleased; no migration).
- The save fixtures re-recorded; `docs/game/saves.md` updated.
- Tests: round trip, the cap, an NPC's page after a reload.

### 4.11 The menu and the settings (issue #34; owner, 2026-11-08)

Branch `feature/menu`, commit prefix `Menu:`. The owner's issue asked for Esc to open a menu with Resume, Settings (ambient and voice volume, graphics quality, font size),
Controls (a display of the configured keys) and Exit (saves, then closes), and added: configure as much as makes sense so testing is easier. The decisions are listed in
the findings (the Menu entry of section 5).

| ID | What | Depends on | Size | Model | Status |
| --- | --- | --- | --- | --- | --- |
| Menu | The menu: `Game.Core/Menu/` (`MenuScreen`, `PlayerSettings`, `MenuRows`, `ControlsListing`), `Game.Core/Input/ControlTable` (every control and its keys in one table that the bindings, the panels and the controls page read), `UI/Menu/` (`MenuLayer`, `MenuLayout`, `MenuMetrics`); the settings in `SaveSettings` (quality, mouse sensitivity, invert Y, field of view added), applied at run time (the mixer, the canvas, the renderer's preset, the camera, the look); the testing section; Esc opens the menu; its Controls page lists the keys (F1 was removed at the owner's request on 2026-11-09); `--menu`, `--settings`, `--controls`, `--menu-scroll`; the old F8 settings panel and F1 help retired | N09, X08, H11 | L | Sonnet 5.5 | done: merged into `main` by PR #46 (2026-11-09) |
| Menu-2 | Every launch-flag setting that can change while the game runs moved into the menu: the draw distance (a saved slider with an Auto position) and `--voice-volume`, `--ui-volume`, `--fov`, `--mouse-sensitivity`, `--invert-y` so each menu setting also has a flag; the Testing section grown and grouped (the five needs as sliders, teleport and beacon over the scene's named points, dusk, greyscale, all-nouns-known view, tips, autosave, typed credit) | Menu | M | Sonnet 5.5 | done: merged into `main` by PR #46 (2026-11-09) |

### 4.12 Day, night and weather (the E series; owner, 2026-11-10, D-game-31)

Branch `feature/weather`, commit prefix `E<nn>:`. The design, the owner's answers and the progress table are in
[the weather plan](weather-plan.md); the statuses below follow its section 7.

| ID | What | Depends on | Size | Model | Status |
| --- | --- | --- | --- | --- | --- |
| E00 | The series registered: the plan, D-game-31, the index rows, the handover | none | S | orchestrator | done, 2026-11-10 |
| E01 | The clock in Core: `Atmosphere/DayClock`, `day.json`, the simulation's part, digest, save, the `day_phase` event, a sleep wakes in the morning | E00 | M | Sonnet 5.5, medium | done: gated at `1a5a9a07` on `feature/weather` (2026-11-10), 10764 passed |
| E02 | Drawing the cycle: `SkyLook`, the dome, sun, moon and stars, fog, the light bands and their tint | E01 | L | Sonnet 5.5, high | done: gated at `920233f9` on `feature/weather` (2026-11-10), 10925 passed |
| E02a | The lanterns' glow as its own additive layer, so it does not step at a light band's edge (found by E02) | E05 | M | Sonnet 5.5, high | done: gated at `89ee19fb` on `feature/weather` (2026-11-10), 10993 passed |
| E03 | Sound and switches for the time: beds by phase, `--time`, the menu row, F3, the scripted step | E01, E02 | S | Haiku 5.5 | done: gated at `ac7facdd` on `feature/weather` (2026-11-10), 11030 passed |
| E04 | Weather in Core: `weather.json`, `WeatherSystem`, the `Atmosphere` snapshot, `weather_changed`, the morning roll | E01 | M | Sonnet 5.5, medium | done: gated at `0d73dba9` on `feature/weather` (2026-11-10), 10837 passed |
| E05 | Sky and light by weather; `--weather` and its switches | E02, E04 | M | Sonnet 5.5, medium | done: gated at `c74ce223` on `feature/weather` (2026-11-10), 10978 passed |
| E06 | Rain: the drops, the cover check, the rain bed and the wind | E05 | M/L | Sonnet 5.5, high | done: gated at `09e83885` on `feature/weather` (2026-11-10), 11089 passed |
| E07 | The NPCs' answer to «¿Qué tiempo hace?» by weather: lines, speech clips, the content loop | E04 | M | Sonnet 5.5, medium | done: gated at `84033e02` on `feature/weather` (2026-11-10), 10936 passed |
| E08 | Close-out: documents, the full gate, the screenshot tour, the pull request | E03, E06, E07 | S | Haiku 5.5 | todo |

## 5. Findings log

The findings log is in [findings.md](findings.md) (moved out on 2026-11-09; "section 5" in other documents means that file). Record a
milestone's findings there, one entry at the end, and its status in section 4's tables.

## 6. Definition of done (every milestone)

1. `dotnet build Vocaterra.sln -warnaserror` with 0 warnings and `dotnet test Vocaterra.sln` green, with the test count in the report.
2. `dotnet format Vocaterra.sln --verify-no-changes` clean.
3. `scripts/content.sh validate` passes; after a content edit the bundle is hashed and its manifest committed; `cefr-check` is clean at A2 (the
   bundle's ceiling since V08) and the host's `--validate-content` holds each lesson to its level, for any content that adds templates or lessons.
4. The demos run (since S4 the 3D voxel world is the only world; the 2D demos retired with the slice, tag `2d-slice-p7`):
   the host `dotnet run --project src/Vocaterra.Host -- --transcript "quiero agua, por favor" --prompt stall-need` prints `request_item`,
   communicated, and `--transcript "quiero dos agua por favor" --prompt stall-need` prints `noun.number` (expected 'aguas'); the core demo
   (`docs/nlp/README.md`) still yields exactly `conj.person` and `noun.number`. The game (needs a display, `DISPLAY=:0`): `dotnet run --project
   src/Vocaterra.MonoGame -- --seed 42 --look 120,-5 --say "¿dónde está el río?" --screenshot <scratch>/s3.png --frames 300` prints
   `ask_directions, entendido` and "El río está allí, adelante, a la izquierda, a cincuenta y cuatro bloques." (V02); the PNG shows the dialogue panel and the beacon
   arrow. Each later milestone's demo is in `docs/game/scenario-plan.md` section 4. Look at every PNG.
5. No violation of section 3: no copied core source, no edit under `nlp/` in a game milestone, nothing under `.github/workflows/`, no
   engine code outside `src/Vocaterra.MonoGame`.
6. `git status` is clean after the commit: no `bin/`, `obj/` or `nlp/artifacts/` output is staged.
7. The milestone's line in section 5 and its row in section 4 are updated by the orchestrator, not by the agent.
- **V21: replies and exchanges (2026-11-02, Sonnet 5.5; gated on `feature/village-followups` at `4a4a440`, 7885 passed, 3 skipped, with the models).**
  - **Built:** `give_item` takes an optional `asked` list of nouns (`host-events.json`, its schema, `NpcEffect.Asked`), so `ExchangeMiss` answers any other noun
    with the NPC's `wont_sell` line: the stall takes `agua` and `botella` («Solo tengo agua.», `shop_wont_sell.1`), the villager `pan` («Solo tengo pan.»,
    `villager_wont_sell.1`); the missed gift leaves the turn's host events, and the offer of bread after it is dropped. León refuses apples with «No compro
    eso. Compro fruta del bosque.» (`farmer_wont_buy.1`, apples are fruit). The baker says «Primero, la lección. Luego, las monedas.» (`baker_wait.1`, the
    new host event `baker.cant_pay_wait`, `GuideScene.OrderHeld`) when the order that would pay her is held by the lesson. One reply per turn: a recast's
    praise is dropped when the line is not understood, and no recast is given with the NPC's bare acknowledgement («¡Qué bien!», `answer_accepted`). A sink
    intent `ask_how` at the free-talk prompts takes the questions V20 found read as statements (`quién/qué/cómo/cuál es grande`, `como es la manzana`, `ellas
    adónde van`), answered by the fallback («No te entendí»). `ask_name` binds a name (`me [VERB:llamar] [WORD:name]`, M70's finding). 10 WAVs; the speech
    lines 934 to 940 files; the scripted runs differ only in those counts.
  - **The gate:** the first gate failed two tests that V21 and M70 broke together, each on its own branch green: V21's not-understood case `buscé madera`
    is understood since M70 (the preterite's slip), and M70's `me llamo ana` test pinned the bundle's missing name slot. A Sonnet fix merged the feature
    branch into V21 (`0ec4afd`) and moved both tests (`buscaré madeda` for the not-understood case; the real bundle binds the name), with the try-lines
    baselines `m66` and `m70` re-recorded for V21's intended changes (`ellas adónde van` is `ask_how`, `me llamo ana` and `me llamo luis` are `ask_name`).
  - **The orchestrator's Spanish pass (D-game-12):** the stall now refuses what it has: it gives the bottle, then offers bread, and its lesson says «Sí,
    hay pan», yet `quiero pan` gets «Solo tengo agua.» and `¿hay pan?` «Hay agua.» (V21 changed it from «Hay agua y pan.», which was true). To V21b:
    `shop_hay.1` back to «Hay agua y pan.», `shop_wont_sell.1` «No tengo eso. Tengo agua y pan.» for items the stall has neither of, and a request for
    bread never refused. The other new lines stand.
  - **Found by the spot-check:** the host's `--transcript` path does not follow the game's turn. With `--bag`, `quiero madera` at `stall-need` gives
    «¿Quieres pan?» and then «Solo tengo agua.» (the game drops the offer), and `compré three manzanas` at `farmer-open` and `buscaré madeda` at
    `lesson-will-fetch` still give the recast's praise and a second reply (the game gives one). `ConversationSession` copies the game's miss handling
    instead of calling it, so `try-lines --host` and the QA agent see replies the game never says. To V21b: the host's replies come from the game's turn code.
  - **Left:** the total of `¿Cuánto es todo?` («Quince manzanas, quince monedas.», the lesson row `quince-monedas`) does not follow the bag (to V21b: the
    unit price in free talk). A fuzz O3 failure seen once in the fix's content loop, seed `2cqJbc9y05h3` (`delicado danó hechos`, `dañó` typed with `n`,
    given `word_order`), likely M70's `ñ` fold: to M69.
- **V21b: voice, book and pages (2026-11-02, Sonnet 5.5; gated on `feature/village-followups` at `d6afaba`, 8133 passed, 3 skipped, with the models).**
  - **The Spanish pass over V21's lines:** `shop_hay.1` «Hay agua y pan.» again; `shop_wont_sell.1` «No tengo eso. Tengo agua y pan.» only for
    an item the stall has neither of; `quiero pan` at the stall gives the bread (`stall.give_water` takes `instead: {pan: give_bread}`, a
    `shopkeeper.give_bread` transition). León answers `¿Cuánto es todo?` in free talk with «Una manzana, una moneda.» (`farmer_unit.1`, the
    NPC's `freeTalkPools`); the market lesson keeps «Quince manzanas, quince monedas.».
  - **The shared feedback lines in each NPC's voice:** 10 lines (`answer_accepted`, `not_understood`, `feedback.hint`, `hint.gender.agree`,
    `slow_repeat`) × 6 NPCs, 120 WAVs at two speeds, about 10.8 MB; ids `<line>.<npc>`, the guide's file the fallback. Proof: the sound log
    plays `not_understood.1.shopkeeper`; no screenshot of a «¡Muy bien!».
  - **`tener que` in the book:** a fifth column «must (tener que)», tener's present and the infinitive; `ayer-en-el-pueblo` declares it.
  - **The Lessons page** opens with the `next:` lesson at its top (session 9: lines 158 to 186 of 192).
  - **M70's accent note** under the reply in the correction colour (`GuideTurn.AccentNotes`, `DialogueSpeaker.Accent`); the host prints
    `note: «compré», con tilde: es pasado` for `ayer compre pan`.
  - **V23b's two items:** the five world words moved into `a1.txt`'s block (lint and cefr counts unchanged). The book already counts a use
    as correct wherever the line was understood; the «0 correct» V20 saw came from a prompt whose answer set rejects the line (`vocab.wrong`,
    intent `unknown`, the core's verdict: the open point below). A regression test pins a free-talk `ayer compré pan` counted correct.
  - **The host's replies from the game's turn code:** `NpcTurnRules` (`AnswerMisses`, `Compose`) in Game.Core, called by `NpcService` and
    the host's `ConversationSession` (one path). Through the host: `quiero madera` at `stall-need` «No tengo eso. Tengo agua y pan.»;
    `compré three manzanas` at `farmer-open` the recast's praise alone; `buscaré madeda` at `lesson-will-fetch` «No te entendí» alone.
  - **Runs and baselines:** the scripted runs differ in the speech count (940 to 1062 files), `r20-sleep` and `r21-skip`'s screenshots (the
    Lessons page) and `r22-market`'s total line. The identity baseline: 1 new entry, 3 moved. `try-lines --check` unchanged.
  - **The orchestrator's checks** on the merged tree through the host: the lines above as stated; the host demos hold. A fuzz O3 failure
    in its content loop (seed `emOiS5FTcQv3`, `me catorce dañó seguro uno` typed with `n` given `noun.number`) ran on the core before M69;
    replayed on the merged tree it passes (M69's fix: a typed accent picks the form that has it).
  - **Left:** a correct line with a word outside an open prompt's answer set gets `vocab.wrong` and "No te entendí" (V17, seen again by
    M69 and here); the commit that holds item 1 also holds the content refresh, so the earlier commits do not all pass on their own.
- **V24: documents and runs after the follow-ups (2026-11-02, Haiku 5.5; gated on `feature/village-followups` at `56d2790`, 8133 passed, 3 skipped, with the models).**
  - **The scripted runs against `1dbb0e5`** (22 runs): every difference traces to a follow-up. `session_start` gains `day` and every save
    the sitting (V25); speech 914 to 1062 files (V20, V21, V21b); r15 no plate on the fisher (V22); r18 and r19 no early focus cards
    (V20 (c)); r20 and r21 the Lessons page at `next:` (V21b); r22 León's «Una manzana, una moneda.» (V21b) and «Nothing to review yet»
    (V23a); r23 the free-talk line credited and answered (V20). Nothing unexplained.
  - **The new run `r24-followups`:** `mañana compraré pan` and `ayer compre pan` to Luz in free talk, `quiero madera` at the stall;
    deterministic after the masks but for the turn's latency label in the pictures, so it joins `compare-runs.py`'s order-only runs.
  - **The learner's route, sessions 4 to 10:** each objective and beacon lead to the lesson's NPC; the seven lessons ran to their ends; 11 of
    11 taught lines understood in free talk by the NPC who taught them, 6 of 11 untaught ones. Four runs were needed: a scripted run stops
    at 18 000 ticks with no message.
  - **The documents:** `docs/game/README.md` (the counts: 30 intents, 531 lines in 1062 speech files; the follow-ups' paragraph; the route;
    CI's case; "Open points after the follow-ups"), `playtest.md`, `scripted-runs.md`. The speech checklist had nothing to render. The
    follow-ups added 74 lines and 148 files, none heard yet.
  - **CI's case:** without models 8091 passed and 45 skipped. The bundle load (1.1 to 1.7 s of 3000 ms) would likely fail at four times;
    the scene build (668 of 3000 ms), the world snapshot (923 ms) and the chunk digest (105 ms of 100, the whole test) are at risk.
  - **The orchestrator's checks:** in the game, `ayer compre pan` to Luz in free talk does show the accent note under the reply, and
    `manana compraré pan` is understood (M70); the agent's report and documents said otherwise, corrected in `236e741`. Confirmed: a line
    not understood at an open prompt gets two replies («No te entendí. ¿Me lo dices otra vez?» and the core's repair «¿Cómo?»).
  - **Left (game, in the README's open points):** the two replies; the book credits `quiero-dos-panes` for a refused `Quiero dos
    manzanas.`; the book keeps the typed form `compre`; the scripted run's silent stop at 18 000 ticks; an untaught statement understood at
    some NPCs and not others (the owner's rule to choose); `vocab.wrong` outside an open prompt's answer set (core and game, V17).
- **V26: V24's four game bugs (2026-11-02, Sonnet 5.5; gated on `feature/village-followups` at `01db096`, 8142 passed, 3 skipped, with the models).**
  - **One reply to a line not understood:** `NpcService` passes no repair when the core's is `AskAgain` and the NPC's reply asked already
    («No te entendí. ¿Me lo dices otra vez?»); `SlowRepeat`, `OfferChoices` and `GiveUp` stay. The host's `repair:` line stays a diagnostic.
  - **No credit for a refused request with another noun:** `PhraseSheet.OfIntent` returns an intent's only row only when the turn's item is
    the row's (`quiero dos manzanas` at Luz is no use of `quiero-dos-panes`; `quiero tres panes` is). The refused line is now a `pedir-agua`
    transfer through the older fixed mapping `PhraseIds.For`, left as it was.
  - **The accented form in the book:** `GuideWord.From` takes the accent note's form (`compré (1 sg pret ind)`, not `compre`).
  - **A scripted run cut at 18 000 ticks says so:** one line to stdout and a `run_truncated` event in the session log (the tick, the steps
    not started, the first of them), and exit code 2.
  - **Runs:** r19, r22 and r23 unchanged; r24 credits `quiero madera` at the stall as `quiero-madera` (was `pedir-agua`) and counts `compré`.
    `try-lines --check` unchanged.
- **V27: the off-prompt lines in the game (2026-11-02, Sonnet 5.5; gated on `feature/village-followups` at `4b01f1d`, 8212 passed, 3 skipped, with the models).**
  The owner's option 2 (D-game-22, 2026-11-02), on M72's `Verdict.OffPromptIntent`. Bundle `e429e60e…`.
  - **The reply:** in free talk a line the prompt does not take but the core understands gets the NPC's one off-prompt line and nothing
    else: no correction, no repair of any kind (`NpcService`, `NpcTurnRules.OffPromptReply`, drawn from the interaction's seed; the pool is
    `NpcEntry.OffPromptPool`, `offPromptPool` in each NPC's file and its schema). Seven pools `<npc>_understood`, the owner's lines verbatim,
    the guide's «Te entiendo. ¿Qué quieres aprender?» and the shopkeeper's «Te entiendo. ¿Quieres comprar algo?» the orchestrator's; 14
    WAVs, English locale lines. `cefr-check a1` reports `lugar` (Mar's line) above A1; within the A2 ceiling.
  - **No failed attempt:** the repeat counts and the learner model are put back as they were before the turn, so the next line not
    understood gets the ladder's first step. A spoken line under the uncertainty threshold (0.55) keeps the uncertain reply. The stall's
    fail-forward does not count it.
  - **The book credits it** (V20 (b)) through `GuideTurn.Taken` (communicated, or an off-prompt intent): its nouns, a phrase of a lesson
    begun or done, its verb forms, and the turn's sound cue. No NPC action.
  - **Lesson steps unchanged** (tested). **The host** prints the NPC's line and `off-prompt: <intent>`, with no `repair:` line, and
    restores the repeat counts too.
  - **Tests:** `OffPromptTests` in Game.Core and Host; M72's two renamed (`ATypedLineThePromptDoesNotTakeIsNoUncertainRecognitionAndMarksNoErrorOnTheLearner`;
    `ALineTheOfferDoesNotTakeButTheStallUnderstoodCountsItsWordsAsCorrectUses`, now 2 of 2: the stall understands `quiero agua, por favor`
    off its offer). The identity baseline took 7 rows, none moved. Try-lines unchanged (their host rows print the intent line only).
    Scripted runs: only the speech counts (538 lines and 1076 files, from 531 and 1062) and the work folders' paths differ.
  - **CI's case:** without models 8169 passed, 45 skipped and one fuzz failure, a new family for M73: O5 `26dVDqElv1l2`, the realiser
    draws the noun `son` (a tune) in `estómago son vencido` and the analyzer reads it as `ser`. The timing lines were not printed.
  - **The orchestrator's checks** in the game at session 10: Domingo `me gusta el río` «Te entiendo. Yo hago hachas.» then `xyzzy qwerty`
    «No te entendí…» (the first step); Luna at `guide-offer`, the stall at `stall-need`, the villager (`el río es grande`) each their line;
    León `la tienda está en el pueblo` «Perdón, ¿qué dices?» (no off-prompt intent); the summary credits `río` 4 of 4 and `gustar` 3 of 3.
  - **Left, to V28:** each NPC's agent file gained an `<npc>.understood` host-event transition in every state only so the lint counts the
    pool as used; the game says the pool directly and nothing fires the event. The learner's route with every NPC was the orchestrator's.
- **V28: the small game points after the follow-ups (2026-11-02, Sonnet 5.5; gated on `feature/village-followups` at `81023b2`,
  8286 passed, 3 skipped, with the models).** The owner's word (D-game-22, 2026-11-02). Bundle `500b3df3…`.
  - **«Sí, es verdad.»** In free talk a statement about the listener gets the NPC's `<npc>_true` line, not a bare «¡Qué bien!».
    - `NpcTurnRules.AboutListener` reads the core's analysis, not the text: a word read as a second-person verb, or `tú`, `usted`,
      `ustedes` not after a preposition (the lexicon reads `usted` as person 3).
    - Three new pools, `baker_true`, `smith_true` and `fisher_true`, each with a normal and a slow WAV and an English line; the
      shopkeeper, the villager and the farmer had theirs.
    - The game and the host call the same `Compose`. An off-prompt line keeps «Te entiendo.».
  - **The name plate behind a building:** a test that launches the game with the beacon on the baker. From the square her plate is
    drawn; from north of the bakery it is not. V22's code needed no change. The test does nothing without `DISPLAY` or the built game,
    as `PlaytestReportTests`.
  - **The `<npc>.understood` transitions** are now the one place that names a pool. The game reads the pool from the agent's
    transition (`NpcTurnRules.PoolSaidOn`, `OffPromptPoolOf`), so `NpcEntry.OffPromptPool`, `offPromptPool` and its schema field are
    gone. No event is fired, so no state or counter moves. The `<npc>.true` transitions declare the new pools the same way.
  - **The speech folder in subfolders:**
    - `speech/<speaker>/` for the seven speakers and `speech/cards/` for the guide's 44 `card.*` files, each slow file beside its
      normal one; 1076 files moved with `git mv`.
    - The index, the provenance and the checklist paths followed.
    - In the code: `SpeechFiles.FolderOf` and `RelativePath`, and a new `SpeechStore` (one core `StaticLineStore` per folder, so no
      core change); `NpcVoice`, `SpeechSummary` and the renderer.
    - In the scripts: `playtest-build.sh`, `content-loop.sh` and `prerender-speech.sh`.
    - The core's `ContentManifest` hashes the subfolders as it is.
    - The start line reads `speech: 541 lines, 1082 files, 0 missing`.
  - **The README's open points** without `buenas tardes`, with the counts, and two new points: the «Sí, es verdad.» scope and the
    display-gated plate test.
  - **Unchanged:** try-lines, and the scripted runs, which differ only in the speech counts (538/1076 to 541/1082) in the same work
    folder. The identity baseline took the three new lines, none moved.
  - **CI's case:** without models, 8243 passed, 45 skipped, one fuzz failure. It is O5 `8ogzm5GU5h-8`, a `word_order` recast of
    `magos relacionados compañero`, a core finding. The orchestrator replayed it on the merged branch (`CsCheck_Seed`). It is the family M73 left recorded (`2tpLGUOSqj41`): the
    pattern `([NOUN:item] [ADJ]|[WORD:qty] [NOUN:spell]) [NOUN:thing]` lets `[WORD:qty]` bind the moved adjective `relacionados`. V28
    changed no intent or lexicon file, so it is older. The owner kept that family recorded, not narrowed (D-game-22, 2026-11-01), so it can
    fail a gate or CI now and then; `docs/handovers/done/2026-11-02-after-followups.md` section 7 carries it.
  - **The orchestrator's checks** in the game at session 10: the start line `speech: 541 lines, 1082 files, 0 missing`; Luz `Tienes veinte panes.` «Sí, es verdad.»,
    `Tengo veinte panes.` «¡Qué bien!»; Domingo `me gusta el río` «Te entiendo. Yo hago hachas.»; León `usted tiene manzanas` «Te
    entiendo. ¿Quieres manzanas?» (off the prompt, so not «Sí, es verdad.»). Not understood, older and outside V28: `usted hace hachas muy
    buenas` at Domingo, `tú pescas mucho` at Mar.
- **V30: the PR #28 review's game fixes (2026-11-02, Sonnet 5.5; gated on `feature/village-followups` at `b455b1c`, 8817 passed,
  3 skipped, with the models).** The owner's review of PR #28, triaged by the orchestrator (D-game-22). No content changed.
  - **A failed `--shot`** keeps the run's exit code (`ScriptedRunEnd.WorstExit`, tested on the helper; a real failed shot needs a device).
  - **An accent fix** takes the person, number, tense and mood from the note's reading (`GuideTurn`). It was a real bug: `luz compro
    pescado` was recorded as `compró` in the first person; the test failed before.
  - **A refused gift is never made,** with or without a transition (`NpcTurnRules`; the no-transition case failed before).
    - `GameDataValidator` now fails on an `instead` target that no agent state takes, and on an `asked` or `instead` noun that is not in
      the catalog.
    - `asked` holds nouns, not events.
  - **An NPC's copy of a shared line with no file** plays the guide's file, counted as `NpcVoice.FellBack` with a log line. There is no
    speech summary to extend.
  - **The save's old flag** is the shopkeeper's (`KeyWordsTaughtBy.Contains(ShopkeeperAgent)`); no fixture pinned it.
  - **The recall message** names `Day`, not `Day - 1`: a load keeps the saved day (the test failed before, "session 2" for 3).
  - **A plural-only phrase** is credited: `PhraseSheet` matches the item's lemma and plural from the noun catalog.
  - **The skip's number** goes with the item as a whole word: 3 apples and 2 loaves in `Compré tres manzanas y dos panes`,
    `treinta y dos panes` 32, `panadería` none.
  - **The host** already called `NpcTurnRules` for misses.
    - The two copies left, the host events and the bottle rule, are shared helpers now (`HostEventsOf`, `SkipLineOfBottleGift`).
    - After a miss the host always updates the agent's state, and an off-prompt line keeps the learner model, as in the game.
    - No host case moved, and no miss test runs through both paths.
  - **The Lessons page's selection** starts at the first row at or after `next:` (`BookModel`; a real bug, tested on synthetic rows).
  - **Dead code:** `NpcService.FeedbackLinePrefix` and `NpcTurnRules.Prepositions` are removed. `NpcTurnRules.FeedbackLinePrefix`, from
    the core's `ReservedPools.FeedbackPrefix`, is the one definition, and `VerdictReport` uses it.
  - **The scripted runs:** 21 of 23 are identical. `r20-sleep` and `r21-skip` differ only in their last picture: the Lessons page now
    highlights the row after `next:`.
  - **Not tested before the fix:** most of the regression tests, which follow from the old code by reading it. The report marks the
    three that failed first.
