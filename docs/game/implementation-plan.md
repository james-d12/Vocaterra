# Implementation plan: Vocaterra (the game)

> **Layout note (R01, 2026-10-23):** the repository now has one root layout. `nlp/src`, `nlp/tests`, `nlp/tools` and `game/src`, `game/tests`, `game/tools` are `src/`, `tests/` and `tools/`; `nlp/docs` is `docs/nlp/`, `game/docs` is `docs/game/`; `nlp/content/v1` is `content/v1`, `game/content/*` is `content/`, `nlp/golden` is `tests/golden`, `nlp/models` is `models/`, `game/assets` is `assets/`, `nlp/scripts` is `scripts/`, `game/playtest` is `scripts/playtest`; `LICENSE` and `NOTICE` are at the root and cover the whole repository (Apache-2.0). The core is the `Vocaterra.Nlp.*` projects, the game is `Vocaterra.{Simulation,Game,MonoGame,Host,SpeechRender}`; `Vocaterra.Nlp.slnf` builds the core alone. Paths and links below were updated to match; the narrative is otherwise unchanged. `.github/workflows-pending/ci.yml` still uses the old `nlp/` paths.

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
src/Vocaterra.Nlp.*/          the language core (its own plan, decisions and docs under docs/nlp/)
tests/                        one test project per source project, their folders mirroring the sources' (R16)
tools/Vocaterra.SpeechRender/ the speech prerender tool (the core's tools are beside it)
content/game-v1/              the game's content bundle (diverged from the core's sample, D-game-4)
assets/                       CC0 art, fonts and sounds (assets/ATTRIBUTION.md)
docs/game/                    SPECIFICATION (design), ARCHITECTURE (boundary), DECISIONS, research/
scripts/content.sh            validate, hash, cefr-check, lint, pack, verify-pack through the core's content tool
scripts/merge-gate.sh         the orchestrator's merge gate (both halves)
.claude/agents/orchestrator.md   the orchestrator's role file (the entry point for a session)
.github/workflows-pending/    the parked CI job (D-game-3, deferred)
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
| X06 | Review deck: a session warm-up of due items, a "Review now" button in the book, and a `review` lesson step that brings earlier phrases back inside later lessons (R06, R07) | in review (2026-10-30): the WIP `0ab3300` ported onto main on branch `X06-review-deck` (the deck, the warm-up offer, Review now, the `review` step type, tests; full suite green); still to do: `review` steps in Personas and Ayer y mañana (they move the lessons' step counts) and the scripted runs of a warm-up | Sonnet 5.5 | X03, X04 |
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
| U06 | The panel scrolls (#5, step 1): the mouse wheel opens and scrolls the older lines, a scrollbar while lines are out of view, one page per NPC | U05 | S/M | Sonnet 5.5 medium | planned |
| U07 | The book's hierarchy (#7): line kinds, a type and ink scale, one row per entry with the details on the right page, each page's layout; a mockup for the owner first | | M/L | Sonnet 5.5 high | planned |
| U08 | The conversation history kept in the save (#5, step 2): each NPC's lines saved, capped per NPC, shown again after a restart | U06 | M | Sonnet 5.5 medium | planned |

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

- **First a mockup** of the five pages for the owner (a picture or an artifact), before the build; the owner's answer goes into the
  design `.docx` (the owner's rule for design work).
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

## 5. Findings log

Facts that shape the plan. Each one names its source; the core's own findings stay in the core's plan.

- **Origin of this repository.** The game design (`docs/game/specification.md`) moved here from the core on 2026-10-20 with its text unchanged
  (core M19c). The game-side text the core removed on 2026-10-21 (M19e) is kept here: the mechanics the core's design listed (spells, trade,
  quests, agent dialogue; sections 4.1 and 4.2 build on them), the engine and language questions (D-game-1 and the findings below), the
  open product questions (agent dialect, free conversation mode), the roadmap wording (the first target a host can use is the design's
  Phase 1), and the Terraria lineage note (XNA to MonoGame). The removed text was `docs/game/NOTES_FROM_CORE.md`, deleted at G0.
- **Engine.** The core's research report suggested Godot .NET; MonoGame, Bevy, macroquad and Ebitengine were the alternatives. The core is
  C# (ADR 0001). D-game-1 is open (`docs/game/decisions.md`).
- **Agent speech dialect.** The default dialect for agent speech is decided in the core (ADR 0005). Whether a player chooses another is the
  game's decision and is open. Recorded for the game's design, not yet decided.
- **Free conversation mode.** The design's Phase 3 (cache and advisory) and whether it ships at all is a product decision for the game and is
  open. The core calls it the optional conversation mode.
- **Core follow-ups for the game (M19c to M19e).** The repeat-count helper and prompt text arrived in core M19d (requests row CR-001, done).
  Remaining game-side items: G1 (adopt the helpers and show `PromptSpec.Text`), the submodule route in the README and ARCHITECTURE (G1),
  and the bundle re-sync (G2). The core's own follow-ups (the lexicon, the tool's natives, the README count) are not the game's.
- **The bundle.** `content/game-v1` has 19 files and hash `d45b8012dde06bdd54039c4857849709a88e96e11802b960f5c5b36795a26b3f`. The core's
  sample at `db39999` has 23 files and hash `cedb97c4d068221c58b86c4a69dc23fa081d6eda8ecb96dc068361bc5c3afad1`, so the game's copy predates
  core M17b-core and M19d. The sample's `ATTRIBUTION.md` says the game loads at run time and the licence is not chosen; the game's copy keeps
  that wording until G2 (a content edit, through the procedure in section 3).
- **`cefr-check`** scans dialogue templates only, not prompt text (core M19d finding).
- **The content tool** is 218 MB as a package, because `PackAsTool` bundles every native runtime (core D2 applies: local installation only).
  Installed under `.tools/` it takes about 0.8 GB. The script installs it once per version (`TOOL_VERSION` in `scripts/content.sh`).
- **Build and package size.** A full build writes about 3 GB into the submodule's `artifacts/` and `bin/`; a worktree needs its own pack
  (the submodule is not initialised in a fresh worktree). Plan disk use before launching several agents.
- **NuGet cache.** NuGet keeps every restored version under `~/.nuget/packages/vocaterra.nlp.*`, so a rebuilt 0.1.0 is ignored. A pin bump
  must delete those folders before restore (the role file's bump procedure does this).
- **The sandbox** refuses command lines containing the shell builtin words `eval` and `hash`, which the content tool's `hash` subcommand
  also uses. Run such a command from a wrapper script kept outside the repository.
- **The parked workflow** `.github/workflows-pending/ci.yml` is a copy of the core's CI, not the game's: it names core test projects and the
  core's feed. It is left as it is (D-game-3, deferred); rewrite it before anything is activated.
- **Setup (G0).** The old `scripts/content.sh` refused any option after the bundle directory, so `hash --bundle-id` could not run; the new
  script passes options through. The old `nuget.config` pointed at a folder named `SpanishLearningGame`, which no longer exists.

- **R1 (prototype research, 2026-10-21).** `docs/research/2026-10-21-prototype-research.md` and `prototype-plan.md` propose three new projects
  (`Vocaterra.Simulation` with no packages and no engine, `Vocaterra.Game` on the packages, `Vocaterra.MonoGame` for the only engine code),
  milestones P1 to P10 with P3 (the guide, typed) as the smallest slice that proves the language core in game, core requests CR-002 to CR-009 (only
  P8, the guide speaking, is blocked, on CR-006), and decisions D-game-5 (CC0-only assets) and D-game-6 (when a noun counts as known). None of
  it is adopted until the owner answers. Not verified by running: MonoGame 3.8.6 DesktopGL on SDK 10.0.402, MonoGame's `Microphone` on Linux,
  the Spanish glyphs in the candidate fonts, Kokoro's Spanish speaker ids. Greyscale is planned as a CPU-made grey atlas, because MonoGame
  shader compilation on Linux still needs Wine.

- **Prototype adopted (owner, 2026-10-21).** P1 to P10 and the three-project layout are in section 4; D-game-1 (MonoGame for the prototype),
  D-game-5 (CC0 only) and D-game-6 (three correct spoken uses in three turns; typed input full credit until P5, then half) are decided. Core
  requests CR-002 to CR-008 are open; CR-009 (`net8.0` target) is filed only if the P10 spike fails on `net10.0`. Voice in: Vosk small first,
  Parakeet after the core's M24 speech check. Nothing in the prototype is gated behind language (research recommendation, the orchestrator's
  default until the owner says otherwise).
- **Worktrees and the feed.** A worktree's submodule is empty, so agents restore from the main checkout's feed with
  `-p:RestoreSources="/home/user/Dev/Vocaterra/external/Vocaterra.NLP/artifacts/feed;https://api.nuget.org/v3/index.json"` instead of packing
  the core again (about 3 GB each).

- **G1 (2026-10-21).** The host's `NextRepeatCounts` is gone; `ConversationSession.Evaluate` carries the context with
  `ConversationContext.Next` (which calls `RepeatCounts.Next`). Bug fixed: a repeat count at `int.MaxValue` wrapped to `int.MinValue` under the
  old rule (test `RepeatCountsStopAtIntMaxValueInsteadOfWrapping`). The text loop prints `PromptSpec.Text` as an `agent:` line before each
  turn (`VerdictReport.WritePrompt`); `--transcript` output is unchanged. The bundle has no prompt text yet, so nothing shows until G2. The
  session keeps its per-agent state map, because `Next` carries only the utterance's agent. Tests: 10.

- **G2 (2026-10-21).** `content/game-v1` now carries the core sample's generic intents (`confirm`, `deny`, `farewell`, `thank`,
  `introduce_self`), four new answer sets, the blacksmith's `confirming` state (buy, then confirm or deny) and text on all 18 prompts; bundle
  hash `88c4edb5…`, 23 files. No merge conflicts: the game's copy was a strict subset of the sample. Recurring cost of tracking the sample:
  three of the sample's new templates fail the game's A1 check (reworded: `confirm_prompt`, `thank_reply.1`, `introduce_reply.1`), the sample's
  `ATTRIBUTION.md` must be rewritten for `game-v1` each time, and the bundle id reset. `cefr-check` only lists unlisted lemmas and never fails;
  27 A1 and 15 A2 lemmas in pre-G2 lines are unlisted (e.g. `herrería`, `moneda`, `oro`), left for a content pass. Core requests CR-010
  (`cefr-check` reads prompt text) and CR-011 (the sample's `ATTRIBUTION.md` is written for the game) added. Tests: 14.

- **D-game-4 decided (owner, 2026-10-21): diverge fully.** `README.md` (Content section) and `content/game-v1/ATTRIBUTION.md` still say the
  bundle tracks the sample; P3, the next content milestone, rewords both (the notice is bundle content, so it goes through the hash procedure).

- **P1 (2026-10-21).** `src/Vocaterra.Simulation` (`net8.0;net10.0`, no package or project references; only the net10.0 build is run here,
  since only the .NET 10 runtime is installed). Facts later briefs must carry:
  - The class is **`GameSimulation`**, not `Simulation`: a type named like its namespace breaks name lookup from `Vocaterra.Game`.
  - Position is integer units of 1/4096 tile, y grows down, `PlayerState.X/Y` is the box's top-left; the front-end converts to pixels.
  - Commands are held levels sent every tick (`PlayerCommand` kinds `MoveLeft`, `MoveRight`, `Jump`; two int arguments reserved for P6);
    jump has a rising edge, a 6-tick buffer and coyote time. `Tick / 60` is game time; the front-end owns the clock and the accumulator.
  - Out of the grid is solid; trunks and leaves are walk-through; 1-tile step-up.
  - Generation passes (`IGenerationPass`, `WorldGenerator.DefaultPasses`: terrain, layers, grass, forest, spawn) and `TileGrid.SetTile` are
    internal, so P6 and P7 add code inside the assembly. The grass pass scans the grid, so caves added before it get grass at their mouths.
    Any pass change bumps `WorldGenerator.Version` and the golden.
  - Seed 42 golden (1024 x 512, version 1): `0505865b…58a2`; spawn (512, 182). `Chunk.Dirty` is cleared after generation, for saves.
  - `ComputeStateDigest()` recomputes everything per call: cache it before it becomes a per-frame cost.
  - `DeterminismRulesTests` scans the source for banned tokens (`System.Random`, `DateTime`, `Math.Sin`, `Math.Sqrt`, `Math.Floor`,
    `Math.Round`, ...) and checks the project has no references.
  - Not done: the research's "a tree within 20 tiles of spawn" (P3 needs a tree for its demo; P3 or P7 adds it), caves and ores (P7).
  Tests: 82 (68 simulation).

- **P2 (2026-10-21).** `src/Vocaterra.MonoGame`: MonoGame.Framework.DesktopGL 3.8.6 and FontStashSharp.MonoGame 1.6.2 (both ship `net8.0`/
  `netstandard2.0` and run unchanged from `net10.0` on Linux: the research's open point is settled). Font: Pixel Operator (CC0, checked on
  three sources), `assets/fonts/pixel-operator/`, recorded in `assets/ATTRIBUTION.md`. Layout: `Frontend/` holds engine-free, tested classes
  (`TickAccumulator` with integer micro-ticks and a 5-step cap, `IFrameClock` real and fixed, `CommandMapper`, `ScriptedInput`, `Camera` with
  `ScreenToTile` for P6, `TileArt`); the root holds the MonoGame wiring (`VocaterraGame`, `WorldRenderer`, `TileAtlas`, `FontProvider`) and
  the plug-in point **`IFrameLayer`** reading a `FrameView` (the debug overlay is the first layer; P3's dialogue panel is the next). MonoGame
  calls `Update` once before the first `Draw`; the game skips it so tick counts match frames. Verification: `--screenshot <png> --frames <n>
  --walk-right <ticks>` needs a display (`DISPLAY=:0` here, or `xvfb-run -a`). Not built yet: text entry and input focus (P3), mouse
  commands (P6). Tests: 140 (58 front-end, including a hand-written TrueType cmap reader that checks the Spanish letters).

- **P3 (2026-10-21): the language core works in game.** Walk to the guide, T, type `¿dónde está el árbol?`: the packages evaluate it
  (`ask_directions`, 100 %, about 14 ms warm in the running game, 77 ms for a cold first turn, hence `GuideService.WarmUp()`), the guide's
  state machine moves to `pointing`, and she answers `El árbol está allí, a la derecha, a ocho bloques.` with an arrow and a marker on the
  nearest trunk. Facts later briefs must carry:
  - `src/Vocaterra.Game`: `GameSession` (owns the simulation, bundle, stored time = fixed start + `Tick/60`, conversation, guide, pointer),
    `GuideService.Say` → `GuideTurn` (plain values, no time), `WorldFacts` (nearest by squared distance, then row, then column; distance is
    the larger axis; Spanish number words), `GuideNpc` (lives in the game layer, placed 8–28 columns from spawn away from trees),
    `TypedUtteranceEvaluator` (the shared call sequence; **the console host now references `Vocaterra.Game`** and its demo is unchanged).
  - The namespace `Vocaterra.Game` hides `Microsoft.Xna.Framework.Game` inside `Vocaterra.*`: the front-end names the base class in full.
  - The guide's world line is rendered by the game from bundle pools outside the replay record (the CR-003 workaround).
  - Generator version 2 adds a `spawn-tree` pass (a tree within 20 columns of spawn); the seed-42 golden is unchanged, since seed 42 already
    had one.
  - Bundle `b293879d…`, 26 files: agent `guide` (`idle`, `talking`, `pointing`), intents `ask_directions`, `ask_what`, `ask_help`, prompt
    `guide-open`, host events `guide.point`, `guide.identify`, `guide.help`; `lexicon/world.json` (accepted by the 0.1.0 loader, which bears on
    CR-007). The agent added `árbol`, `piedra`, `flecha`, `guía` and `bloque` to the game's `cefr/a1.txt` so the guide's templates pass; the
    list is game-owned since D-game-4, but `flecha`, `guía` and `bloque` are not standard A1 words: review in a content pass.
  - Core gaps: **CR-012** (the pipeline reads repeat counts as including the current utterance, but `Next` carries them as before, so the
    second identical mistake still gets a first-time recast; the guide works around it by evaluating twice, which is pure, the console host
    keeps the old behaviour) and **CR-013** (accent-insensitive lookup is only a fallback, so `dónde esta el arbol` gave a false
    `gender.agree`; worked around with a lexicon entry for `esta` as a form of `estar`, and recasts keep the unaccented `esta`).
  - Text entry: Spanish characters through text events, plus Alt+vowel for á é í ó ú, Alt+N for ñ, Alt+/ for ¿, Alt+1 for ¡; input focus
    moves to the panel while it is open. `--say "<line>"` (repeatable) and `--content <dir>` script the demo for screenshots.
  - Not done: evaluation is synchronous (P5 moves it to a worker); only árbol, hoja, hierba, tierra, piedra and roca map to tiles; `¿qué es
    esto?` with the mouse is tested but not run live; a real Spanish keyboard layout was not tried. Tests: 290.

- **P4 (2026-10-21, merged `00c6c44`, Haiku 5.5).** Catalogue nouns gain colour as the player uses them. `game/nouns.json` (hand-authored,
  in the content hash: trunk and leaves `árbol`, dirt `tierra`, grass `hierba`, stone `piedra`, air colourless; items empty until P6, a test
  enforces it); `NounCatalog` (articles, gender, plural from the bundle; reads the file after `ContentLoader.Load`, the CR-007 workaround;
  `CatalogueNounsOf` is the CR-002 workaround); `WordProgress` (heard, read, spoken, correct, known; credit in units, 6 per use and 18 to
  know, input kind credit in `CreditRules`, one credit per lemma per turn); `name_object` intent graded in the game against the cursor's
  tile (core gap **CR-014**). Front-end: grey atlas made on the CPU (`Greyscale.ToLuma`, integer), drawn under colour at alpha =
  saturation; journal (J), "¡Nueva palabra!" toast; `--point`, `--point-nearest <noun>`, `--journal`. Bundle `b4c5c674…`, 27 files.
  Verified by the orchestrator: three `es un árbol` turns at a tree make trees fully coloured and `el árbol` known ("conocida", three
  pips); one turn gives a third. Facts later briefs must carry:
  - `WorldFacts.NounOfTile(Leaves)` is now `árbol` (was `hoja`), so colour and the guide's identify agree.
  - `GameSession` takes a `NounCatalog` (fourth argument); `GuideTurn` has `Slots`, `GuideError` has `Span`.
  - Any communicated turn credits the catalogue nouns it uses correctly, so the P3 demo line `¿dónde está el árbol?` also prints
    `word: el árbol, 1 de 3`. `es un arbol` (no accent) is `name_object` with no error and earns credit; `la árbol` is an unknown intent with
    `gender.agree` on `la` and earns nothing.
  - For P5: D-game-6 gives spoken input full credit (three turns to know) and typed input half credit from P5 (six turns); change
    `CreditRules.Default` for typed input only.
  - Deviations: "read" counts once per journal opening (no clock in the front-end); unseen nouns are listed as `¿?`; "heard" includes
    feedback lines; art is still the code-drawn placeholders (no Kenney files fetched).
  - Content pass item: the core sample's recast pool praises after a recast ("Ah, el árbol. ¡Perfecto!"), and a bare noun phrase (`la
    árbol`) gets that praise followed by "No te entiendo"; reword `feedback.recast` and consider an intent for a bare noun.
  - Environment bug (found by the agent): the shared `~/.nuget/packages` held a core worktree's 0.1.0 (source
    `Vocaterra.NLP/.claude/worktrees/agent-a70557e8…`), which turned three P3 recasts into hints. Fixed in the gate (game-only package
    folder, core packages cleared each run; the build log names the package source). Tests: 290 to 329.

- **P4b (2026-10-21, merged `b433647`, fix `9ce62ef`, Sonnet 5.5).** D-game-6 as amended by the owner: spaced colour. `CreditRules`
  holds the gaps (`DefaultGapSeconds = [600, 3600]`, the last repeats); `WordRecord` has `LastCreditedAt`, `LastCreditedSession`,
  `Lapses`; `WordProgress` takes a `Session` (1 for a new game; P9 bumps it on load) and returns a `CreditOutcome` per noun (`Credited`,
  `TooEarly` with seconds remaining, `Wrong`, `NamedWrong`, `NoTarget`); `DueWords` orders most overdue first, then ordinal lemma.
  `GameSession.SkipTime`, `BeginTalk` (the guide asks "¿Qué es esto?" about the first due word with an example in range, arrow and a `¿?`
  marker; prompt `guide-review`, pool `guide.review.ask`; the answer is graded against the pointed tile), `EndReview` (Esc ends a review).
  Front-end: "Bien. Repasa más tarde." toast and panel line; journal shows `otra vez en 8 min` / `lista para repasar`; launch options
  `--skip <minutes>` and `--talk` (ordered script steps). `cefr/a1.txt` gained `esto`. Bundle `6a3750bc…`. Tests 329 to 371.
  - Orchestrator spot-check found a loophole (fixed in `9ce62ef`): after a wrong review answer the guide says the word ("No, es un
    árbol.") and copying it earned the spaced credit; and naming with no target earned colour while the guide said "No sé qué es". Now a
    word the guide reveals has its gap restarted (a lapse, no credit lost; untouched if it has no credit or is known), and a name with no
    target earns nothing (`NoTarget`). Verified: the copy is `TooEarly`, the same answer 11 minutes later is `2 de 3`.
  - Orchestrator defaults the owner may overturn: a wrong name earns no colour and does not raise "bien"; uses after a word is known are not
    spaced (only counts change); Esc during a review ends it.
  - Core gap **CR-015**: the core has no host-initiated agent turn, so the review question is a host line outside the replay record.

- **P6 (2026-10-21, merged `57244dd`, Haiku 5.5).** Mining, placing, inventory and the wider world of things (section 4.3, P6 row).
  Simulation: tiles `Sand`, `Flower`, `Mushroom`, `Planks`, `Door`, `Table`, `Chair`, `Bed`, `Chest` (ids 6 to 14); `TileDefinition`
  mining ticks, tool and drop; `Items/` (`ItemId`, `ToolKind`, `ItemDefinition` with the stack limit 99, tools 1, `ItemStack`,
  `Inventory` 40 slots, hotbar 10); `SelectSlot`, `Mine` (held), `Place`, `GrantItems`, reach 5 tiles; the state digest includes the
  inventory. Generator version 3 (passes `sand`, `flora` after `spawn-tree`, kept clear of trees and spawn); seed-42 golden
  `6d148dca…` (the 65 tree bases and the spawn are unchanged). Game: `nouns.json` covers every tile and item (`mass`, `sources`),
  `lexicon/world.json` gained `seta`. Front-end: hotbar, inventory (I or E; label reads once per opening), mouse mine and place, number
  keys and wheel; script steps `--walk`, `--mine`, `--place <item>@<col,row>`, `--select`, `--inventory`. Bundle `9e2cd3be…`. Tests 371 to
  437. Orchestrator spot-check: trunk 90 ticks with the axe, stone 75 with the pickaxe, placed plank, inventory labels; the guide finds
  `arena` ("a once bloques") and `flor`. For later briefs:
  - Stone lies at depth 4 or more and reach is 5 tiles from the body's middle: the player digs down to reach it.
  - Cosmetic, for P6b: the `madera` count in the inventory draws nearly black; a tree mined at its base leaves the trunk above floating
    (Terraria fells the whole tree); "¿dónde está el hacha?" gets "Aquí no hay un hacha" while the axe is in the inventory (P6b's
    `ask_find` should say "Tienes un hacha.").
  - Inventory labels show the lemma without its article; no debug starting kit (`GrantItems` exists).
- **Core bump prepared (2026-10-21).** The core marked CR-002 to CR-008 done (M26c, M26d, M26e); its main `53c52b7` also holds M26a
  (CR-012) and M26b (CR-013). M26a changes the repeat counts' meaning, so the guide's double evaluation would count one turn ahead: the
  bump goes through G6, not a bare bump. Disk: the core pack and its build output took about 7 GB; the submodule's `bin`/`obj` (0.9 GB) and
  main's `bin`/`obj` (2 GB) were removed after it (free 13 GB).

- **G6 (2026-10-21, merged, Haiku 5.5): core bump `db39999` → `53c52b7` (M26a to M26e) and adoption.** The bump alone failed three
  `GuideTurnTests`: the CR-012 workaround now counted one turn ahead. Adopted: CR-012 (`EvaluateCountingThisUtterance` removed; the
  ladder for the same mistake is now recast, hint, hint, for the host and the guide), CR-013 (the `esta` entry left `supplement.json`;
  `dónde esta el arbol` is clean), CR-007 (`NounCatalog.Load(bundle)` through `TryReadHostFile`; a missing file throws), CR-002
  (`GuideTurn.Words` from `PipelineResult.Words`; `CatalogueNounsOf` removed), CR-005 (`word.<lemma>` through `extraSkills`, one learner
  update). Partly: CR-003 (the guide's facts are host slots in the replay record, but the line is still the game's render: the core
  cannot pick the found / here / none sentence from a host fact, **CR-016**). Not adopted: CR-004 (the right answer is the cursor's
  tile, which no static answer set expresses). The guide evaluates each line twice, because the facts depend on the noun the core reads
  and host slots are taken only before `Evaluate` (pure; only the second result kept): **CR-017**. Orchestrator review: the agent first
  overrode `HostStateHash` with the bare simulation digest, against the core's contract (api.md section 4: a host's own digest must cover
  the host slots and skills); fixed with `HostDigest.Of` (SHA-256 over the simulation digest, ordinal slots, distinct ordinal skills).
  Bundle `8e2913da…`. Tests 437 to 446. For P5 and P8 (from the CR-006 and CR-008 answers): one pipeline may evaluate on one thread while
  `Synthesize` runs on another; Vosk, wav2vec2 and the embedding model need one instance per thread; recognizers and ONNX sessions load in
  their constructors (preload behind a loading screen); Kokoro loads on the first `Synthesize`, 1.5 to 3.7 s a line, so wrap it in
  `CachedSynthesizer`; a build with both the ONNX embedding model and sherpa-onnx carries two clashing `libonnxruntime.so`. P5 must not run
  the recognizer twice for the double evaluation: evaluate the second time from the recognized transcript.
  - Content pass: the recast pool renders "Claro, dónde está el árbol. Muy bien." (no question marks, lower-case inside).
  - Agents in a worktree cannot run git against the core checkout (the worktree guard): briefs must paste the core API passages they need.

- **P6b (2026-10-21, merged, Sonnet 5.5).** Crafting and the guide's crafting answers (section 4.3). Simulation: `Crafting/Recipe.cs`
  (`Recipe`, `Ingredient`, plain values handed in with `GameSimulation.SetRecipes`, not in the digest), `CanCraft` (`Done`,
  `NoSuchRecipe`, `MissingIngredients`, `MissingStation`, `InventoryFull`), `Craft`, `PlayerCommand.Craft`, event `ItemCrafted`; a
  station counts within `CraftingRules.StationReachInTiles` (6); tiles `Furnace` 15, `Glass` 16, `Brick` 17, items `Sword` 13 to
  `Brick` 16; felling (mining a trunk removes the trunk above and the canopy, one `Wood` per trunk tile). Game: `RecipeBook` reads
  `game/recipes.json` through `TryReadHostFile`. Guide: `ask_craft` (ingredients with "bloques de" for mass nouns, station or "No
  necesitas nada más.", shortfall or "¡Ya lo tienes todo!", "Eso no se hace.") and `ask_find` (carried: "Tienes …"; else the source
  sentence and the nearest source with the arrow); `ask_directions` for a carried tool says "Tienes un hacha."; "dónde hay" moved from
  `ask_directions` to `ask_find`. Front-end: crafting panel (C), counts on dark badges (contrast at least 7, tested), `--craft`,
  `--crafting`, and an `arrow:` line in the turn output. Bundle `29931388…`, 28 files. Tests 446 to 518. Core gap **CR-018** (a template
  cannot render a variable-length list; the game joins the ingredient parts).
  - Orchestrator spot-check: three felled trees, `mesa` crafted and placed, then "Para una puerta necesitas seis bloques de madera. Usa la
    mesa. ¡Ya lo tienes todo!" and the door crafted; "¿dónde encuentro piedra?" gives "abajo, a la izquierda, a seis bloques"; the panel
    shows nine recipes with have/need, station and status.
  - Content pass items: "¿de dónde sale la madera?" while carrying wood answers only "Tienes cuatro bloques de madera." (it should say the
    source too); the panel reads "Usa mesa" (no article); `falta`, `faltar`, `nada` are A2, not in the A1 list (owner's call);
    `ask_directions` for `madera` points at placed planks, only `ask_find` goes to trees.

- **G7 (2026-10-21, merged, Haiku 5.5).** Native-asset trim and licence. `Directory.Build.props` sets `RuntimeIdentifier=linux-x64`,
  `SelfContained=false` and `AppendRuntimeIdentifierToOutputPath=false` on a Linux x64 host unless `-p:VocaterraAllRuntimes=true` (pass it
  to restore too; toggling needs a restore). Output paths are unchanged (`bin/Debug/net10.0/`), the linux-x64 natives land flat next to the
  executable. bin+obj of the solution: 2.0 GB to 217 MB; the MonoGame project's bin 405 MB to 42 MB. `LICENSE` (MIT), README licence
  section (MIT; Wiktionary lexicon CC BY-SA 4.0; core Apache-2.0; MonoGame Ms-PL; CC0 assets; no distributed build with sherpa-onnx
  natives while core D2 is open). Rejected: stripping `runtimes/*` from the copy-local items after `ResolvePackageAssets` (relies on NuGet
  internals).
  - **For P5:** Vosk 0.3.38 copies `libvosk.so` through `build/Vosk.targets` (`build/lib/linux-x64`), which NuGet imports only into a
    project that references `Vosk` directly (`build/` assets do not flow transitively). No game project does, so `libvosk.so` is not in any
    output (not a G7 regression: it was never there). P5 must make the speech project carry it (a direct `Vosk` package reference in
    `Directory.Packages.props`, or ask the core for `buildTransitive` in CR form) and test that the recognizer loads.

- **Speech models (2026-10-21, orchestrator).** `vosk-model-small-es-0.42` (58 MB) and `kokoro-multi-lang-v1_0` (387 MB) fetched with the
  core's `scripts/fetch-models.sh` into `~/.local/share/vocaterra/models` (all files verified against the core's manifest); the gate exports
  `VOCATERRA_MODELS_DIR` to that folder when it exists. Owner, the same day: the words added to the game's A1 list are fine (including
  `falta`, `faltar`, `nada`, which P5 adds).

- **P5 (2026-10-21, merged `96d4907`, Sonnet 5.5).** Spoken input. `TurnWorker` (Vocaterra.Game) is the one thread that owns the Vosk
  recognizer and the conversation; `GameSession.SubmitTyped/SubmitClip/SubmitWav/SubmitSpeak` fix every input at submission (stored time,
  digest, inventory copy, `WorldFacts.Snapshot` radius 96) and `Collect` applies turns in order on the game thread. A spoken guide line is
  `Trace` with Vosk, then `EvaluateRecognized` (the core's `ScriptedWordRecognizer` over the first `AsrResult`): Vosk decodes once (CR-017
  handled). `CreditRules.Default` typed half, spoken full (`--typed-credit full|half`); journal pips show a started word. Microphone:
  MonoGame `Microphone` works (44.1 kHz, `AudioClips.Prepare` to 16 kHz, trimmed, 8 s cap; it needs a `BufferReady` subscriber); hold Tab
  in the panel or V near the guide; "Escuchando…", "Pensando…", loading screen "Cargando la voz…". Script steps `--wav`, `--speak` (Kokoro
  speaker 28 through `CachedSynthesizer`, cache `~/.cache/vocaterra/tts`), `--listen`, `--no-speech`, `--status`. Packages: `Vosk` 0.3.38
  (Apache-2.0) referenced directly so `libvosk.so` is copied; `Microsoft.ML.OnnxRuntime` 1.31.0 (MIT) named with `ExcludeAssets="native"`
  so sherpa-onnx's `libonnxruntime.so` is the one in the output (Kokoro failed with "VERS_1.28.2 not found"). `cefr/a1.txt` gained
  `falta`, `faltar`, `nada`. Bundle `0bd4c8ce…`. Tests 518 to 566 (8 model tests; skipped with a reason without models).
  - Timings: Vosk load 0.25 to 0.39 s; recognition 0.16 to 0.37 s a clip; Kokoro 1.9 to 3.4 s a line cold, under 0.1 s cached; evaluation
    15 to 20 ms; frames keep running (median 2.2 ms, longest 30 ms) during a 2.9 s spoken turn.
  - Orchestrator spot-check: spoken "¿dónde hay arena?" → `ask_find` and the arrow; "¿cómo hago una puerta?" → the recipe (Vosk heard "y
    cómo hago una puerta"); six spaced typed uses make the tree known in half steps; `libvosk.so` in the output.
  - **Choices the owner may want to revisit:** Vosk is asked for one hypothesis, so `AsrResult.Confidence` is 1.0 and the core's ASR
    confidence gate is effectively off (with five n-best, clean Kokoro speech scored 0.34 to 0.66, under the 0.6 threshold; per-word
    confidences were 0.85 to 1.0): garbage is stopped only by the intent stage (**CR-022**). The models folder has no `manifest.json`
    (`fetch-models.sh` does not copy it), so `SpeechEngine` falls back to `VOCATERRA_MODELS_MANIFEST`, then the submodule's
    `models/manifest.json` (**CR-019**). Also **CR-020** (`WavCodec` internal; the game has a 60-line WAV reader), **CR-021** (the two
    `libonnxruntime.so` copies).
  - Not done: the bundle still loads before the window (the loading screen covers the pipeline and Vosk); live Tab/V and real speech were not
    tried (the microphone recorded room noise, heard as ""); a spoken line shows the raw Vosk transcript (no punctuation); content pass:
    an unknown intent at the guide ("quiero una espada") gets "Habla más despacio, por favor", which suggests a speed problem.
- **Core requests (2026-10-21):** the core delivered CR-010 and CR-011 (M26f, `27747ea`), not yet in the pin (content tool only; take in at
  the next bump), and accepted CR-014 to CR-018 into M28a to M28c.

- **P7 (2026-10-21, merged, Haiku 5.5).** Caves, ores, vegetation, lighting. Generator version 4 (caves and ores run last; tree bases
  and spawn unchanged), seed-42 golden `d96279bf…`. Caves: per chunk a worm (20 %) and a cellular-automata cavern (12 %), only at depth 7
  or more below the surface column. Ores by random walks in stone within bands: coal 8–60, copper 20–90, iron 40–150, gold 110–260 (seed
  42: 71, 124, 142, 120 tiles). Ore mining 60/75/90/105 ticks with the pickaxe. `Torch` (`antorcha` ← 1 madera + 1 carbón by hand, 4);
  `lámpara` ← hierro + cristal (mesa), `cubo` ← 3 hierro (horno), `anillo` ← 2 oro (horno), not placeable; tall grass (`hierba`). Nouns
  `deep` flag: "Está muy abajo." when none is within the search radius. `LightMap` (Simulation, integers, levels 0–16: sky columns, torch
  16 losing 2 a step, rock at least 6), incremental recompute of 19 columns (median 0.9 ms), full map 65 ms at load; frame medians 1.1 to
  2.3 ms. `--teleport`, `--grant`. `abajo` added to the A1 list (it is A2: owner review). Bundle `17ae6d14…`. Tests 566 to 625.
  - Orchestrator spot-check: "¿dónde hay hierro?" → "abajo, a la derecha, a cuarenta y cinco bloques" with the arrow; gold → "Está muy
    abajo."; a placed torch lights a warm disc in a dark cave. Cosmetic: a faint ghost label on the right of the cave screenshots.
  - Not done: caves have no surface entrances; lamp, bucket and ring have no use yet; no day and night; metal tool tiers (multi-word nouns).
- **Fresh-clone check (2026-10-21, Haiku 5.5, owner's request).** A clean clone with an empty package cache fails at the core's pack
  (`NU1301`: the core restores with the game's `nuget.config`, whose local feed does not exist yet); with an uncommitted
  `external/Vocaterra.NLP/nuget.config` (`<clear/>` + nuget.org) it packs, builds with 0 warnings and passes 558 tests (8 skipped
  without models). Core packs of the same commit are not byte-identical. **CR-024**; workaround in `CLAUDE.md`. Closed by the merge below.
- **One repository (2026-10-21, D-game-7, the owner with Claude).** The core's history was rewritten under `nlp/` (`git filter-repo`) and
  merged; the game moved under `game/`. One `Vocaterra.sln` at the root (35 projects), shared `Directory.Build.props` and
  `Directory.Packages.props`, `nuget.config` with nuget.org only; `Vocaterra.Game` and `Vocaterra.Host` reference the core's projects, so
  the feed, the pack step, the submodule bump, the package-cache isolation and CR-024 are gone. The game's rule tests find `game/` by
  `content/game-v1` instead of `Vocaterra.sln` and check for project references into `src`. `SpeechEngine` looks for
  `models/manifest.json`. One orchestrator role file, one gate (`scripts/merge-gate.sh`, now also the core's licence scan), one parked
  CI file. `docs/game/CORE_REQUESTS.md` closed: CR-014, CR-016, CR-018 were already M28b and M28c; CR-019 to CR-022 became M28d; CR-010, CR-011,
  CR-015 and CR-017 (done in the core after the last pin) are now available to the game without a bump. Licences stay per folder (MIT
  here, Apache-2.0 in `nlp/`). `content/game-v1/ATTRIBUTION.md` reworded (the manifest's path) and the bundle re-hashed to `fa8fb030…`;
  the core's sample attribution keeps its old repository links because its hash is pinned in the goldens. After the merge: build 0
  warnings, 3,585 tests pass (25 skipped, with the models folder set), format clean, licence scan 28 of 28, the sherpa
  `libonnxruntime.so` still wins next to the executable (CR-021 holds through project references), the host and the content tool run
  from the root. The host now falls back to the bundle found above the executable when `content/game-v1` is not in the current directory.

- **The prototype brief (2026-10-21, S0, orchestrator).** The owner's brief v1.2 replaces the Terraria design (D-game-8). Plan:
  `docs/game/scenario-plan.md`, folded into section 4 (table and 4.4); decisions D-game-8 to D-game-12 recorded, D-game-6 amended; core M29a and
  M29b proposed, conditional; the merge gate takes `GATE_TARGET` so the S series lands on `feature/prototype`. The
  docs part (Haiku 5.5): `docs/game/design/prototype-brief.md` extracted from the docx by a standard-library script (every paragraph and table
  cell present, headings set by hand; the docx has no heading styles), `specification.md` a pointer, Noun Town research moved, R1 notes,
  READMEs. Gate: 3,605 passed, 5 skipped (models set), lint 0 errors, licence scan passed. Agent worktrees branch from `main`: every
  S brief starts with a fast-forward to `feature/prototype`. The orchestrator's status
  line saves the plan's usage (`~/.claude/usage-snapshot.json`): 25 % of the session window and 57 % of the week at the start of the run.
- **S1, the voxel simulation (2026-10-21, Sonnet 5.5).** `Vocaterra.Simulation.Voxel`: `BlockId` 0 to 21 (the brief's list plus `FireRing`,
  `Sign`, `Bridge`), 16³ chunks with SHA-256 chunk and world digests (`Version` per chunk for re-meshing), a JSON layout (base-36 height rows,
  a feature legend, props, NPCs, points, tree limits) and a pure `SceneBuilder` (trees from `WorldRng.For(seed, "trees")`, 3 columns clear of
  the route), `PlayerBody3D` in 1/4096-block units (walk 4.3 blocks/s, one-block step-up, `SteppedUp` flag for the camera), `Angles` with
  4,096 steps per turn from an integer sine table (yaw 0 north, clockwise; no `Math.Sin`), `MoveRelative` for mouse look, exact-integer
  `BlockRay` (reach 4), `VoxelSimulation` (trunk 60 ticks gives `madera`, berry bush 45 ticks gives a berry and becomes a bush; a torch crafted
  at the `mesa` and placed in the fire ring). The draft route is `content/scenes/route-draft.json` (96 x 32 x 96, 72 chunks, 160 trees; spawn
  (17,11,17), guide (25,11,21), shopkeeper (62,9,58), villager (66,9,64), river z about 80 to 85 with a bridge at x 66); S3 moves it into the
  bundle. Seed-42 golden `dc14aa93…`; build 5.5 ms. Tests 3,678 passed (93 new; physics property over 24 seeded worlds x 2,500 ticks).
  Open for later rows: `ItemId.Berry` (25) sits in `ItemDefinition.VoxelOnly`, not in the noun catalogue (S3/S4 name it); water is wadable
  (no swimming, superseded by the water series, `docs/game/water-plan.md`; the bridge is not enforced); `Interact` checks reach but not line of sight (the front-end picks with `BlockRay`).
  Disk: each worktree build is about 4.4 GB; a leftover 3 GB P4 build copy in `/tmp/claude-1000/p4` was deleted.
- **S5, the phrase sheet and the stall (2026-10-22, Sonnet 5.5; review Opus 5.5).** `content/PHRASES.md`: 11 rows (the brief's phrases plus
  hola, gracias, adiós, no sé, ayuda), each with intent, prompt, accepted variants, a learner error with the code the pipeline gives, NPC and
  situation, card, audio line ids; a test (`PhraseSheetTests`) runs every row through the pipeline. Prompts `stall-need` and `stall-offer`
  (Constrained), `villager-open` (Open), `guide-offer` ("¿Quieres agua?"); agents `shopkeeper` (opening question on host event
  `shopkeeper.ask`; `stall.give_water`, `stall.give_bread`), `villager` (`villager.greet`, `villager.point`, `villager.give`), the guide's
  `offering` state; intents `request_item` and `dont_know` (with `no entiendo`); `ask_help` and `dont_know` on every prompt; the blacksmith and
  spells removed (effects now empty). M29a is not a gap: `(por favor)?` parses (and the matcher already adds a trailing "por favor").
  Provisional review (D-game-12 step 3, Opus, 2026-10-22): the Spanish correct and A1, tú consistent; applied: "Quiero agua, por favor" glossed
  "I want water, please" (the brief said "I would like"; the reviewer: *quiero* is "I want" and polite in Spanish), realistic learner
  errors (`no, mucho gracias`, `no sabo`), more variants (`un agua, por favor`, `sí, quiero`, `¿me ayudas?`), `de acuerdo` narrowed, seven NPC
  lines reworded (e.g. "No te entendí. ¿Me lo dices otra vez?"), the berry item is `la fruta` (HUD `frutas 3/3`; `baya` is not A1).
  Findings: a bare plural ("quiero aguas") is valid Spanish, so the demo is `quiero dos agua por favor` (`noun.number`, expected 'aguas'); a
  bare noun as a `request_item` example makes `ayuda` and `gracias` resolve to it (lint `confusion.prompt`); Wiktionary's `rio` (reír)
  shadows `río` typed without the accent (a hand noun reading in `lexicon/world.json`); recasts lose ¿ ? (the recast builder works on
  normalised text); a one-shot `--transcript` starts each agent in its initial state. **Core bug found:** on a quantifier or determiner
  number/gender error the core blames the noun: "no, mucho gracias" gives `noun.number` expected 'gracia' and the recast «no mucha gracia»
  ("buenos día" recasts «Bueno día») — wrong Spanish on screen; filed as core **M29c**. The gate now runs lint `--strict --level a1
  --host-pools 'guide.*'` and `cefr-check` at A1 and A2. Tests 3,701 passed.
- **S2, the first-person front-end (2026-10-22, Sonnet 5.5).** Spike passed: MonoGame 3.8.6 DesktopGL 3D on this machine (llvmpipe software
  GL, Depth24): depth, point-sampled atlas, alpha water and back-buffer read-back work. `--voxel` runs the 3D scene (the 2D slice stays the
  default until S4). `ChunkMesher` (culled faces, per-face shade, water in its own mesh), a code-drawn 31-cell atlas, `VoxelRenderer`
  (`BasicEffect`, fog 48 to 110 blocks, per-chunk frustum culling, re-mesh on chunk `Version` and touching neighbour layers),
  `FirstPersonCamera` (relative mouse through SDL; 3.8.6 lacks the API; live mouse untested), crosshair, gather bar, highlight, held item,
  cube-assembly NPC figures with labels, the beacon column and edge arrow; script steps `--look`, `--walk-to`, `--interact`, `--place
  item@x,y,z`, `--beacon`. Hotbar, inventory, toasts and journal draw over the scene; the guide's dialogue is not wired (S3). Scene 33,118
  faces, 132,472 vertices; 72 chunks meshed in about 0.1 s; scene draw median 3 to 6 ms on software GL. Screenshots checked by the
  orchestrator: the camp (guide, workbench, berry bush, fire ring, beacon "río 78 m") and the village (stall with awning, timber houses);
  the shopkeeper stood in front of the counter facing away and the debug overlay carried a stale 2D line (both passed to S3). The first S2
  gate failed on a fuzz coverage flake (core M29e), not on S2; the merge stayed unpushed until the M29e gate passed (3,849 tests).
- **S3, the world and the guide in 3D (2026-10-22, Haiku 5.5).** The route is `content/game-v1/game/scene.json` (hashed; seed-42 golden
  `d27347a6…`); the shopkeeper stands behind the counter facing north (62,59), the villager in the plaza (63,66), the signpost label
  "Tienda / Río". `IGuideFacts` with `WorldFacts` (2D) and `WorldFacts3D` (nearest block by horizontal distance, reading-order ties,
  relative direction from integer `Angles` math, distance in words); `GuideSession` base with `GameSession` (2D) and `VoxelSession` (3D).
  Nouns `agua`, `río` (share the water block; `agua` identifies it, either name is right), `camino`, `casa`, `tienda`, `puente`, `fruta` (the
  berry bush and item); ores stay until S4 (a 2D test needs a noun per 2D item). In `--voxel` the turn worker runs (typed and spoken), E/T/V
  on the guide opens the panel ("E: hablar"), the reply sets the beacon, "¿qué es esto?" uses the crosshair block. Demo checked by the
  orchestrator: "El río está allí, a la izquierda, a sesenta bloques." (the `--say` script walks the player to the guide, facing west; the
  river is due south, so left is right). Real key events are untested (no X automation here); the camera ends inside the guide's figure
  when `--say` walks up to it (S6: a stand-off distance). Tests 3,887.
- **S4, gather, craft, objective; the 2D slice retired (2026-10-22, Haiku 5.5).** `recipes.json` is one recipe (`antorcha` ← 2 `madera`
  at the `mesa`); the torch goes on the fire ring's rim (the centre hid it); `Objective` (engine-free) "Campamento" madera 5, frutas 3,
  antorcha 1, agua 1 (host event `objective.water`, raised from S6); HUD top right, completion toasts (pickups no longer toast); word cards
  on first pickup (`la madera`, `la fruta`, `la antorcha`; heard and read, never a quiz; audio slot for S9). The 2D slice is gone: 66 files
  (2D simulation, generator, lighting, `GameSession`, `WorldFacts`, the 2D renderer and layers, 19 test files); `--voxel` accepted and
  ignored; `WorldRng` moved to the simulation root; nouns are the 16 the scene reaches. Headless `CampObjectiveTests` (a BFS walker) reaches
  3/4; screenshot checked by the orchestrator (HUD 3/4, the flame, the card). Tests 3,887 → 3,562. Open: **`--greyscale` is a stub** (the
  grey A/B of D-game-6 needs per-block saturation in the mesher; due before the playtest, S12); the guide's `ask_craft` lines still name
  2D recipes (`mesa`, `puerta`, `espada`: "Eso no se puede hacer."; content pass with S6); "La piedra está aquí" for stone under the feet
  (horizontal distance); toast queue lags; crafting clicks and E at the mesa untested with real input; `JournalRows` lost its direct test.
- **S6, NPC exchanges, the bottle, the transfer (2026-10-22, Sonnet 5.5).** Design (orchestrator): the stall has no water; on
  `stall.give_water` the shopkeeper hands an empty bottle with "Toma la botella. El agua está en el río.", which sends the player to the
  villager and the river; the bottle filled at the river (Interact 30 ticks) raises `objective.water`; fail-forward: the bottle is handed
  over as a core host turn (`shopkeeper.give`) after the repair ladder gives up, after "no sé"/"ayuda" plus one more non-request, after four
  non-requests, or on a second visit, logged `fail_forward`. `NpcService` replaces `GuideService` (one `ConversationContext` per agent,
  openings through `Advance`, `Analyze` then `Complete`, `Records` of utterance and host-turn envelopes); `NpcHostEvents` is the one table
  from host events to the world; `GuideSession.Events` holds `PhraseUse` (supported, unaided, choice, dont_know, transfer, fail_forward),
  `VoiceEvent` and `WorldEvent` for S8 and S11; `NpcStandOff` keeps the camera about 2 blocks from an NPC; `--talk`, `--say`, `--leave`,
  `--shot` script steps. The end-to-end run reaches `Campamento ... agua 1/1` and logs the guide's transfer offer and the villager's bread.
  Provisional review (orchestrator, llm-opus, 2026-10-22): the two new shopkeeper lines and the hint "Necesitas una botella." are correct
  A1; `botella` was added by hand to the game's `cefr/a1.txt` (it was only in the A2 list): accepted as provisional (a concrete, everyday
  item word that A1 syllabuses commonly include, "una botella de agua"), to be re-checked with D-game-12. Open: transitions are chosen by
  intent only, so "quiero pan" at the stall also hands the bottle with the bottle line, and every request is logged as `pedir-agua` (core
  M28b, CR-016 host-fact replies, would allow item-dependent replies); the word card is drawn under the dialogue panel (S8); real input
  for E and push-to-talk at the stall untested. Tests 3,620.
- **S7, speech UX (2026-10-22, Sonnet 5.5).** `game/phrases.json` (the runtime copy of PHRASES.md, a sync test); the mic check (level
  meter, "Di: hola", Constrained then Open decode, pass/retry/skip, logged; `--mic-check`, `--no-mic-check`); Esc cancels newest-first and a
  cancelled turn is rolled back (`NpcService.Checkpoint`/`Rollback`); a 5 s front-end watchdog offers typing ("¿Escribes?"), as do two
  uncertain turns in a row; the persistent `Oí: «…» · Esperaba: «…»` line; buttons `Repetir`, `Más despacio` (through `ILinePlayer`, silent
  until S9), `Ayuda`, `No sé`, `Escribir` (Tab no longer holds push-to-talk: V or Space); `OfferChoices` as buttons logged `choice`
  (recognition only, no word credit); the uncertainty rule (`UncertaintyRule`: not communicated, or mean non-`[unk]` Vosk word confidence
  under 0.55; an understood-but-unsure line is not passed to phase 2 and gets "No te entendí bien. ¿Lo dices otra vez?"; a not-understood
  line restores the learner model; logged `uncertain`, never an error); `--speak` (Kokoro → Vosk) and `--wav` script steps. Constrained vs
  Open at the stall prompts gave the same intents for every scripted line (Constrained pads with `[unk]`). Core: CR-022 confirmed (the
  per-word confidences in `AsrResult.Words` suffice), M29b confirmed (low priority), **M29f** filed (poor offered choices). Screenshots
  checked by the orchestrator: the choices and the five buttons at the stall; after a garbled line the shopkeeper still says "Casi. Piensa
  otra vez." (a language-failure hint after a recognition failure, against D-game-9: content fix in S9). New UI strings reviewed
  (llm-opus, provisional): correct A1. Real keyboard and microphone untested here. Tests 3,677.
- **S9, the NPCs speak (2026-10-22, Haiku 5.5).** `scripts/prerender-speech.sh` with `tools/Vocaterra.SpeechRender` (the core's
  `KokoroSynthesizer`; re-runnable, an index outside the bundle) rendered 109 slot-free lines (88 dialogue variants, 6 prompt texts, 6
  word cards, 9 model answers) at speed 1.0 and 0.8: 218 WAVs, 16-bit mono 24 kHz, 16.8 MiB, in `content/game-v1/speech/`, provenance
  `model-output` (core M29g; model `kokoro-multi-lang-v1_0`, guide 28 `ef_dora`, shopkeeper 29 `em_alex`, villager 53 `em_santa`).
  `NpcVoice` (an `ILinePlayer` over `DynamicSoundEffectInstance`) plays each NPC line, `Repetir` and `Más despacio` (the slow file), and
  the word cards; silent without a device; `--mute`. Startup line `speech: 109 lines, 218 files, 0 missing` (checked by the orchestrator).
  Kokoro is constructed only for `--speak` (and the scripted mic check without a microphone). Content fix (D-game-9): not-communicated
  feedback is `SlowRepeat` with the help lines ("Di «ayuda» y te ayudo."), so "Casi. Piensa otra vez." no longer follows a recognition
  failure; the core plans that help line only when the verdict has an expected form, otherwise only "No te entendí. ¿Me lo dices otra
  vez?" (accepted). **Owner's step:** listen to every file and mark `docs/game/speech-checklist.md` (cut-offs, text-audio mismatches).
  Tests 3,719.
- **S8, phrase progress and fading scaffolds (2026-10-22, Sonnet 5.5).** `PhraseProgress` per sheet row: supported, unaided and transfer
  credit (spoken 1, typed 0.5 by default), slips (communicated with a mistake: no credit, no step), choice_only, dont_know (on the phrases in
  play), fail_forward, card shows, heard/answered for the two NPC phrases, last use. Classification (`GuideSession.Classify`): help/no sé,
  then choice, then the review answer, then transfer, then the scaffold rule: supported while a card or audio is offered or after Ayuda,
  unaided at level None (the orchestrator accepted that answering the shopkeeper at level None is unaided: otherwise fading would never
  show). Support levels `CardAndAudio` → `AudioOnly` → `None` for pedir-agua, si-por-favor, no-gracias, donde-esta-el-rio, stepped down by a
  full credited use once the D-game-6 gap has passed (or a new session), up by no sé, a choice or a fail-forward. Support card above the
  panel ("Puedes decir", the phrase, its English and glosses, audio); key words `el agua` and `el pan` before the shopkeeper's first question;
  `phrase.<id>` through extraSkills and `StepSkills` when a card shows (no core gap); the journal's phrases page (J, then Tab); the guide's
  review asks a due phrase first (placeholders from existing pools until `guide.review.phrase.*` pools exist); console `phrase:` lines and a
  summary; `--wait`, `--skip`. Cues play S9's files by text (`ILinePlayer.Append` queues them). Screenshot checked by the orchestrator (the
  support card and key words over the stall). The first S8 gate conflicted with S9 in `VoxelGame.cs` (resolved by the agent) and hit a
  second rare fuzz coverage flake (core M29h). Content wanted later: `guide.review.phrase.*` pools and a guide-review prompt accepting
  request_item, confirm and deny. S10 must save the 11 phrase records, the play session, the carded nouns and the key-words-taught flag.
  Tests 3,780.
- **S10, save and sessions (2026-10-22, Haiku 5.5).** `vocaterra-save` v1 in canonical JSON: seed, scene, bundle hash, stored time and
  skipped seconds, session, tick, state digest, player, inventory, objective, word and phrase progress, carded nouns, the key-words flag,
  per-NPC contexts (agent state, learner model with `phrase.*`, repeat counts) and bookkeeping, and only the edited chunks run-length
  encoded (the demo save: 4 of 72 chunks, 21.9 KB). `--save` (end of a scripted run, quit, F5) and `--load` (refuses another seed, bundle,
  version, digest or stored time); loading bumps the session, which lifts the D-game-6 gaps; stored time continues from the save (a
  next-day check uses `--skip` after the load). Byte-identical double writes and continuation determinism tested. Checked by the
  orchestrator: the loaded camp shows "Sesión 2", 4/4 and the same inventory; the console prints the digest equality. Not saved by
  design: an open panel, support cards, audio queue, the event log and replay records (S11's session log). F5 and window-close untested
  interactively. Tests 3,801.
- **S4b, the greyscale A/B switch (2026-10-22, Haiku 5.5).** The atlas holds a grey (Rec. 601 luma) copy of every cell; the mesher picks
  grey or colour per block by a 64-bit mask of block kinds whose noun is not known (`WordColours.GreyKinds` from `NounCatalog.ForBlock` and
  WordProgress); only chunks holding a kind whose state changed are re-meshed; figures, sky and UI stay in colour; "¡Nueva palabra: …!" toasts
  in greyscale mode; `--assume-known <nouns>` is a test-only option. Default mode is byte-identical to before. Checked by the orchestrator
  (`--assume-known árbol`: trees in colour, the rest grey). Open: water is named by `agua` and `río` and turns colour only when `agua` is
  known (orchestrator: either name should count, as in S3's naming rule; S12); the live transition is covered by tests only. Tests 3,816.
- **S11, checks, the session log and the report (2026-10-22, Haiku 5.5).** `--check pre|post|recall`: a battery outside the world from
  `game/checks.json` (6 comprehension items: hear a sheet line through S9's WAV, pick one of three English meanings, some with item icons;
  4 production items: an English situation, graded by the core at the row's prompt in a throwaway context; "no sé" and typing allowed),
  item and option order from `WorldRng` per check kind; a check never touches progress, the learner model or the world (tested). Pre is
  followed by the world; post and recall end the run. `SessionLog` (front-end; `WallClock.cs` is the one file allowed the clock, an
  `ArchitectureRulesTests` exemption the orchestrator confirmed): `--session-dir`, `session.jsonl` (session_start, check_*, turn, phrase,
  voice, world, session_end) and the core's `replay.jsonl`. `scripts/playtest-report.sh` (bash and Python standard library) prints
  completion, retries, support used, ASR latency, check scores by kind, transfer, found-without-Spanish, and one evidence line per decision
  gate (Enjoyable: observer notes needed). The S8 review pools exist (pedir-agua "¿Qué necesitas?", the river "¿Qué buscas?", the offers
  "¿Quieres agua?"; the agent's first "Pídeme agua." / "Pregúntame por el río." failed cefr-check as A2 clitic imperatives and were replaced
  by the orchestrator's A1 choice) and the guide accepts request_item, confirm and deny with `answer_accepted`. Speech: 113 lines, 226
  files, 0 missing. The check screen checked by the orchestrator. Open: a save refuses another bundle hash, so the bundle must be frozen
  before testers' next-day recall (S12); the first frame of a window can be drawn at 800 x 480 before the resize (a `--wait 2` before
  `--shot`); the two new review lines are not in PHRASES.md's reviewed set (they reuse reviewed texts). Tests 3,877.
- **S12, the playtest build (2026-10-22, Haiku 5.5).** `--playtest` (no debug overlay, mic check on, session log on, pre-check implied on a
  fresh start and recall on a load, the post-check once the camp is complete and the guide's transfer offer has been made, or on F10;
  `playtest.save` in the session folder; a thank-you screen held until Esc); the water rule (a block named by several nouns is in colour
  when any of them is known); a screenshot gate that waits for the final back-buffer size; `scripts/playtest-build.sh` (self-contained
  linux-x64 publish with `-p:VocaterraPlaytest=true`, which excludes the sherpa-onnx and ONNX Runtime natives; the Vosk model copied and
  checked against the manifest; 519 files, 193 MiB; every check passes); `docs/game/playtest.md` (recruitment of beginners, consent and
  what is logged, the provisional content-trust note, setup, the session, a 5-minute rule before helping, observer notes, recall, the A/B
  arm, the gates and their evidence, the owner's pre-flight, the bundle freeze). The orchestrator moved the playtest-only runtime pin into
  `Directory.Packages.props` (the agent had used a `VersionOverride`). Fresh clone: builds with 0 warnings and passes 3,889 tests; the README
  now says the speech models must be fetched. Checked by the orchestrator: the playtest world frame (no overlay, F10 label). Tests 3,889.

- **The owner's playtest of S12 (2026-10-22).** "It's ok", with changes asked for: English guidance for a player with no Spanish (D-game-13),
  a more gradual start with a 5 to 10 minute guide lesson and a start alone at the player's own cliff camp (D-game-15), and a much larger,
  more realistic world with high mountains, dense forest and vegetation, close to the brief's reference images (D-game-14). The gap against the
  references (`docs/game/scenario-plan.md` section 8): the S12 build has a dark grey sky, untextured dull blocks, a small flat map and cube figures.
  This machine renders through llvmpipe (a VM with a virtio GPU); the owner set a real GPU as the target, with presets. The launcher in
  `scripts/playtest/` is only meant to run from a built folder; its missing-binary message is unhelpful (S18 fixes it).

- **S16, the English guidance layer and the lesson runner (2026-10-22, Sonnet 5.5).**
  - **English text.** `game/english.json` (`EnglishText`) holds the UI words, the F1 controls, first-time tips, the bilingual objective
    (`Campamento (camp): madera (wood) 0/5 ...`; each English word goes when its noun is known), one explanation per phrase, the English
    under each NPC line (matched by the Spanish without case, accents or punctuation), and feedback keyed by outcome and by every core error
    code, with a fallback. An uncertain spoken line is blamed on the microphone.
  - **The ladder.** `SupportLevel` runs `EnglishAndCard`, `CardAndAudio`, `AudioOnly`, `None`.
    - One full-credit use at the English rung steps down at once. Without this, the D-game-6 gaps would keep the English up for a whole lesson.
    - The lower rungs keep the 10-minute and 1-hour gaps.
    - The help key shows card plus English at every rung.
  - **Lessons.** `game/lessons.json` (`LessonBook`, `LessonRunner`): steps `explain`, `model`, `listen`, `repeat`, `answer`, `do`, `recap`,
    validated at load against the phrase sheet, prompts, speech lines and world events.
    - Every step has skip (K) and help (H).
    - A failed turn moves on after `attempts` (default 2: one retry with help; 1 moves on at once), never counted as a language failure.
      Accepted by the orchestrator as the reading of fail-forward for lessons.
    - A lesson takes over the NPC's conversation only while engaged (`L` or `autoStart`).
    - Progress is saved (`LessonsSave`, `TipsSave`; older saves load), and every step is logged (`lesson` lines).
    - A new world event, `item_picked_up`, serves `do` steps.
    - A two-step sample lesson (hola, gracias) is on the guide.
  - **Front-end.** `LessonLayer`, `TipLayer`, `ControlsLayer` (F1); English drawn in orchid prefixed `English:`; tips hidden in scripted runs
    unless `--tips`. Script flags: `--lesson`, `--lesson-next`, `--lesson-skip`, `--lesson-help`, `--lesson-pick`.
  - **Tests:** 3,984 passed, 5 skipped. No core gaps.
  - **Checked by the orchestrator:** the gate; the demo `--talk guide --lesson sample --say hola --say gracias` completes both steps; a
    screenshot shows the English under each Spanish line and the bilingual HUD.
  - **Left for S17 and S18:**
    - the speech buttons are still Spanish only (`Repetir`, `Más despacio`, `Ayuda`, `No sé`, `Escribir`) and need English while the
      English rung is up;
    - the guide's world lines have no fixed English (a pointer feedback line stands in);
    - phrase reviews still cover only the four stall and river phrases;
    - the sample lesson's offer also shows in `--playtest` (S17 replaces it);
    - `playtest-report.sh` ignores `lesson` lines (S18).

- **S13, the large world and its terrain (2026-10-22, Sonnet 5.5).**
  - **The map.** 384 x 384 x 128 (4,608 chunks, 1,114 empty). `scene.json` schemaVersion 2 holds no block list: a 24 x 24 relief grid,
    named sites (`camp.player`, `camp.ledge`, `camp.guide`, `village`, `stall`, `river`, `bridge`, `lake`, `waterfall`, `trail`; `camp`
    aliases `camp.guide`), the river, lake, waterfall, two paths and stamps.
  - **The generator.** `TerrainGenerator`, `Polyline`, `IntMath` and `TerrainNoise` are integer-only.
    - Peaks reach 124, with stone above 85 and snow above 100 (new `BlockId.Snow` = 22).
    - The river never runs uphill. A graded switchback trail leads down, and a road follows the valley.
    - The forest is dense: 3,189 trees at seed 42.
    - Another seed never moves a site, NPC or spawn (tested block for block).
    - Generation takes 0.42 s and the first digest 0.09 s.
  - **Rendering.** `--quality low|medium|high` gives view distances of 224, 304 and 400 (default high; `--view-distance`
    overrides). Chunks are limited to the view distance, empty and buried ones skipped, frustum-culled and meshed to a per-frame budget;
    fog follows.
    - On this VM at low, from the camp: 1,250 chunks meshed, about 2.2 million vertices, 452 drawn.
    - Frame medians are 32 to 49 ms (about 20 to 30 fps).
  - **Walking times at 4.3 blocks/s.** Camp to guide by the trail 53 s, guide to village 36 s, village to river 12.8 s. Dropping down the
    hillside skips the trail in about 22 s, because falls are free (no-death rule).
  - **Script additions.** An A* `WalkPlanner` for `--walk-to` and `--talk`, plus `--teleport x,z` and `--beacon none`. A new game starts
    with the beacon on `camp.guide`. The cold fire ring `firering.player` and a tent stamp stand on `camp.ledge`.
  - **Tests.** Many front-end tests use a flat fixture (`Fixtures/clearing-route.json`). The pinned world line now rounds the Euclidean
    distance. Tests: 4,049 passed, 5 skipped. Seed-42 world digest `4a8ad02d…`.
  - **Bugs fixed:**
    - `IntMath` names matched the source scan's banned `Math.Sqrt`/`Math.Floor` substrings;
    - the waterfall pool cut the plateau;
    - a site's rim depended on the seed;
    - `KindMask` was 0 on never-edited chunks;
    - several planner arrival and canopy faults.
  - **Checked by the orchestrator:** the gate and two screenshots.
    - The valley view north reads well: terraced forested slopes, grey cliffs, snow, the waterfall.
    - **The camp view does not.** Trees on the ledge block the vista, the valley is a thin band, and a pale fog wall stands mid-picture.
      The village and lake are not visible.
    - Carried into S14: a clear sightline from `camp.player` to the valley, village, river and lake (reference A); the fog and haze; the
      boxy valley outline.
- **S14, the look (2026-10-22, Sonnet 5.5; merged `849fd52`).**
  - **History.** The first agent finished the work, but the desktop crashed twice from CPU and RAM overload (S14 and S17 built and tested at
    once). The orchestrator snapshotted the worktree as a WIP commit. A resumed agent, run alone under `scripts/limited.sh`, found every
    deliverable present, added nothing and took the screenshots. From now on there is one agent at a time and every heavy command is capped
    (orchestrator "Machine resources").
  - **Built.** A code-drawn 16 x 16 atlas (`BlockArt`, `BlockArtNature`, `BlockAtlasLayout`); oaks, spruces and birches; crossed-quad
    undergrowth from `WorldRng`, in the digests and thinned near paths and sites; per-face sun shading and per-vertex AO in `ChunkMesher`;
    `SkyModel`/`SkyRenderer` (gradient, square sun, flat clouds, bluish fog); translucent water and a waterfall face; `QualityPreset` low,
    medium and high (view 224, 304 and 400; plants 64, 112 and 192).
  - **Measured** (software GL, 1280 x 720, high): 1762 chunk meshes in about 2.0 s; draw median 47.5 ms at the camp; 47,168 plant quads
    drawn from the camp. Gate: 4,108 passed, 5 skipped.
  - **Checked by the orchestrator:** three screenshots. The camp view now has the sightline S13 lacked: the valley, village, river, lake
    and waterfall, with clouds and flowers. The valley view and the forest floor (trunks, ferns, a birch) read as reference A: textured,
    bright and dense. Host demo `quiero dos agua por favor` gives `noun.number`.
  - **Not checked:** dusk and the low and medium presets by eye; the valley standpoint is near, not identical to, S13's.
  - **Seen for S15 and S17b:** the beacon label reads `campamento 74 bloques` (the unit is the game's own text: S17b makes it `blocks`).
- **S17, the opening, the guide's lesson and the ending (2026-10-22, Sonnet 5.5; merged `b0ebf5f`, bundle `9582e14f…`).**
  - **History.** The first agent's work was snapshotted after the crashes. A resumed agent merged S14 in (only `manifest.json` conflicted:
    re-hashed) and found the WIP complete. Then the Opus pass (D-game-12 step 3) reviewed the eleven rows and the unrowed lines; the agent
    applied every item, and every "verify" variant and error passed `PhraseSheetTests` with the stated codes.
  - **Built.** The opening (tips, the note with `camino`, the objective `Go down the trail and find the guide`). The lesson `first-words`:
    six units, 34 steps, started within five blocks of the guide (Luna). `Otra vez` and `Más despacio` route to `ask_repeat` and
    `ask_slower` in content (no core gap). The ending: a spare torch, the beacon home, the fire ring lit, `Session complete`. Ten new rows
    plus `hola`, all `llm-opus (provisional)`; `cefr/a1.txt` gained `madera`, `antorcha`, `fuego` and `despacio`. The check battery has 20
    items (11 comprehension, 9 production). 72 WAVs (36 lines, normal and slow).
  - **The review's main changes:**
    - the owner chose `Necesitas madera y frutas.` (the list and the fire are the player's; row `necesitas-madera`);
    - `u5-quiz` no longer answers with the bottle Unit 3 taught: Luna points at a stone (`Esto es una piedra.`);
    - `Toma una fruta.`, `¡Muy bien! Tienes una fruta.` and `¡Muy bien! Ya tienes fuego.`;
    - the `sí`/`si` help text;
    - `más lento` and `¿puedes repetir?` accepted;
    - `qué son esto` removed (ungrammatical, was accepted);
    - a yes/no answer set apart from the offer set, so `no quiero` is no longer accepted at `¿Es agua?`.
  - **Measured.** Lesson about 4.8 minutes (quick) to 7.5 minutes (slow), with the real WAV lengths; plus about 1 minute of walking. Gate:
    4,149 passed, 5 skipped.
  - **Checked by the orchestrator:** the gate, and the scripted `--lesson-run first-words --journal --journal-phrases` after the review: the
    lesson reaches `Necesitas…` and opens the recap on the phrases page.
  - **Seen for S17b and S20:** the journal's own labels are Spanish (`Diario de frases`, `con ayuda`, `ayuda: inglés, tarjeta y audio`,
    `lista para repasar`, `J o Esc: cerrar el diario`). They are the game's own text, so they become English (D-game-13): the key hint
    in S17b, the rest when S20 rebuilds the journal as the book.
- **S17b, the talk key and the game's own text in English (2026-10-22, Haiku 5.5; merged `678e5a2`).**
  - **Built.**
    - The dialogue footer names the talk key: `Hold V or Space: speak   Esc: cancel or close` with the buttons, and
      `Tab or Esc: buttons, then hold V or Space: speak` while typing (V types a letter there). With speech off it reads
      `Speech off: type your answer`.
    - The microphone check is English (`Microphone check`, `Say "hola"`, `Got it, I can hear you.`, `I didn't catch that. Try again?`,
      `Continue / Try again / Skip`, `Esc: skip`).
    - Every model and microphone problem is plain English with what to do, for example `Speech models not found: set
      VOCATERRA_MODELS_DIR (see scripts/fetch-models.sh). You can still type.`
    - The journal, inventory, crafting, HUD, save, status and beacon text is English (`74 blocks`).
    - The heard line reads `Heard: «…» · Expected: «…»`.
  - **Left Spanish:** what is learnt or said: NPC names, taught nouns, the speech buttons with their English beside them, NPC lines, the
    guide's `Puedes decir` card tag.
  - **Fixed on review:** the first pass changed `VOCATERRA_MODELS_DIR` inside a test while other test classes run in parallel. `SpeechEngine`
    now takes the variable reader, and no test writes the environment. The orchestrator overruled keeping `Oí`/`Esperaba` in Spanish.
  - **Not done:** the content key `btnHold` in english.json is no longer drawn but is still required by `EnglishText.UiKeys`. It is
    harmless, and S20 can drop it with the journal rebuild.
  - **Checked by the orchestrator:** the gate (4,161 passed, 5 skipped) and the three screenshots: the V hint, the English model message
    with the speech-off hint, and the English microphone check. Host demo `quiero dos agua por favor` gives `noun.number`.
- **S15, the village, props, characters and the HUD (2026-10-22, Sonnet 5.5; merged `788f5a1`).**
  - **Built.**
    - 18 new block ids (34 to 51; the kind mask has 12 left).
    - Timber-framed houses with stepped roofs and chimneys, a stone tower, a fenced crop field, lamp posts.
    - A signpost (`Tienda`, `Río`, `Montaña`, a 3 x 5 pixel font with accents). It reads from the south and west only.
    - The stall: a red and white awning, with bread, apples and bottles in crates.
    - The guide's pergola over the workbench, with a post torch (a separate `PostTorch` id, so the torch noun ignores scenery).
    - The player's tent, chest and fire ring.
    - Lantern glow baked into vertex colours; `--dusk` is a static evening look (the world has no time of day).
    - Humanoid figures with faces and clothes, idle, talking and walking poses, turning to the player.
    - F5 shoulder view (`--third-person`). **Save moved from F5 to F6** (the spec gives F5 to the view); help, english.json and README
      follow.
    - The first-person arm in 3D, without its outline, lit by the scene, holding the item, with sway and swing.
    - The hotbar with icons and counts. The workbench recipe panel with icons and have/need, shown only within reach.
    - The rounded dialogue panel, and a speaker bubble over the head.
  - **Measured** (llvmpipe): village scene draw median 23 ms; four figures, arm and rig 0.29 ms CPU with no per-frame allocation. Gate:
    4,215 passed, 5 skipped. Scene digest `397e58bf…`.
  - **Checked by the orchestrator.**
    - The gate, and the README's full S17 route on the merged build: 34 lesson steps, then `fire_lit` and `session_complete`.
    - The screenshots against references B and C. The stall, the village entrance, the signpost, the F5 view and the workbench panel read
      as the references' ideas, simpler and coarser.
  - **Seen, not fixed, for later:**
    - In a conversation the support cards and new-word cards cover the speaker and the bubble; the screen is crowded.
    - The cards' tag `Puedes decir` is Spanish (S17b left it, as the guide's phrase hint; a candidate for `You can say`).
    - The dialogue log shows a debug line (`request_item, understood, 100 %, 2.2 ms`) in a scripted run; S18 checks whether `--playtest`
      hides it.
    - The pergola posts are full blocks and dominate the workbench view.
- **S19, the learner's record (2026-10-22, Haiku 5.5).**
  - **Built.**
    - `Learner/InputEntry`: every spoken, typed or button line in dialogues, lessons and checks, with source, place, verdict and the
      core's word readings.
    - `LearnerWords.Fold`: noun and verb rows with forms (person, number, tense, mood), times used and correct, first and last use.
    - `LearnerRecord` (`session.Learner`): `Nouns`, `Verbs`, `Rows`, `Recent(n)`, `History`.
    - `LessonRunner.Record`: per lesson, status, units, completed session, turns, help, "no sé" and repeats.
    - Save version 2 keeps the newest 500 lines and the whole word rows (stored, not rebuilt, so older uses survive the bound). Version 1
      loads (fixture `save-v1.json`). An exit summary prints `book: …`.
  - **Choices (accepted by the orchestrator):**
    - "correct" is communicated and no error span over the word;
    - check answers count;
    - button and held (unsure) lines stay in the history but are not folded.
  - **Bugs fixed:** the lesson's own "No sé" answer was not counted; the synchronous `Say` path skipped the history.
  - **Core gaps → M29j:** `está` read as an imperative in «¿dónde está el río?»; «gracias» as the noun `gracia`; `favor` as a noun in
    every «por favor». Numerals are digits, as documented.
  - **Checked by the orchestrator:** the gate (4,232 passed, 5 skipped). The agent's lesson run prints 3 nouns and 2 verbs with the core
    readings above; the full S17 route completes.
- **S20, the book (2026-10-22, Haiku 5.5; merged `ee2ec79`).**
  - **Built.**
    - The journal became the book, opened with J, with the book item (right-click on the hotbar), or with `--book` (`OpenBook`).
    - Five pages: Nouns (the catalogue plus any noun used, with its article, forms, counts and last sentence), Verbs, Phrases, Lessons and
      What I said.
    - Verbs: the infinitive with its gloss, from the new `glosses.json`, and each form with an English label (`VerbLabels`). A question is
      a form of its own (`FormRow.Question`), and each form has its own last use (`FormRow.Last`).
    - Lessons: state, units ticked, phrases, help, and an English summary from `lessons.json`.
    - What I said: `Recent(500)` with the English feedback.
    - Scrolling, and the page models engine-free in `Frontend/BookPages`, `Book` and `VerbLabels`.
  - **The book item is a taught noun:** `libro` (m), with its word card pre-rendered. The catalogue grew from 18 to 19, because every item
    needs a noun (NounCatalogTests). The owner may prefer the book not to count; that would change the NounCatalog rule instead.
  - **Bugs fixed:** an engine `Rectangle` in Frontend (ArchitectureRulesTests); a mid-enum atlas cell that shifted seeded noise; "1 times";
    an overlong summary.
  - **Checked by the orchestrator:** the gate (4,327 passed, 5 skipped); the Nouns, Verbs, Lessons and What I said screenshots
    (`saber — to know`, `sé — I know`, `¿es? — is he/she/it?`, the six ticked units and the summary; `por favor` and `gracias` absent
    from Nouns and Verbs, thanks to M29j); the full S17 route on the merged build; the host demo.
  - **Seen, not fixed:**
    - the inventory still says `Ocupadas: 1 de 40` (S17b missed it; S21 fixes it);
    - saves from before S20 have no book item;
    - `--greyscale` on the book was not checked by eye.
- **S21, F8 bug and feature reports (2026-10-22, Haiku 5.5; merged `917d2c4`).**
  - **Built.**
    - `Vocaterra.Game/Reports/` is engine-free: `ReportKind`, `ReportNaming` (`report-s<n>-t<tick>-<kind>`, a counter if taken, never
      the clock), `ReportSnapshot` (bounds of 10 turns and 200 log lines) and `ReportMarkdown`. The save time arrives as a preformatted
      string, so the game layer stays clock-free (ArchitectureRulesTests).
    - The panel (`ReportPanel`, `ReportEntry`, `ReportLayer`):
      - F8 works anywhere and pauses the game: no ticks, no turns, no watchdog, and the microphone dropped.
      - The F8 frame is held in memory and written only on save; Esc drops it.
      - The report and its PNG are saved to `<session>/reports/` or `--report-dir`.
      - Scripted flags `--report`, `--report-open`, `--report-kind` and `--report-dir`.
    - `scripts/playtest-report.sh` lists and copies the reports. playtest.md's consent text names what a report holds.
    - `Slots used`, `Recipes` and `a station` are English now.
  - **Bug fixed:** the Alt+vowel accents shown in the hints were never applied (`AccentShortcuts` was unused); now applied in the dialogue,
    check and report boxes.
  - **Checked by the orchestrator.**
    - The gate: 4,356 passed, 5 skipped.
    - The panel screenshot: English, Bug or Feature request, Cancel and Save report.
    - On the merged build, the full S17 route (34 steps, session complete) and a scripted F8 report at the guide (Markdown and PNG
      written, kind, tick and the open panel recorded).
  - **For S18:** the build commit reads `unknown`. The csproj stamps it from `VOCATERRA_BUILD_COMMIT`, which
    `scripts/playtest-build.sh` must set.
- **S18, the playtest refresh (2026-10-22, Haiku 5.5; merged `3c9a741`).** This closes the second pass (S13 to S21, with M29j).
  - **Changed.**
    - `--playtest` hides the verdict line (`greet, understood, 100 %, 2.2 ms`); it was showing.
    - F6 in a playtest writes the session folder's `playtest.save`, not the build folder.
    - `scripts/playtest-build.sh` stamps `VOCATERRA_BUILD_COMMIT` (F8 reports from the built binary name the commit) and publishes with
      `-m:4`. Its speech-WAV check compares against the bundle manifest; it had 226 hard-coded and failed on 300.
    - `playtest.sh` says to build first when the binary is missing.
    - playtest.md and the tester README describe the current game: the opening, the lesson, the 20-item checks, the book, F5, F6, F8, J,
      collecting reports with `--copy`, and the consent text naming the book and the reports.
  - **Build:** 602 files, 199.7 MiB (S12: 519, 193 MiB). Frozen bundle `393548666329…`, 300 speech files.
  - **Smoke run of the built folder** (scripted): pre-check 20 of 20, lesson 34 of 34, session complete, post-check 20 of 20, save
    written.
  - **Checked by the orchestrator:** the gate (4,358 passed, 5 skipped), the launcher message from an empty folder, and the full S17 route on
    the merged build.
  - **Not fixed:**
    - the facilitator's `F10: end the run` label overlaps the dialogue footer in a playtest;
    - the session length (15 to 25 minutes) is an estimate for the first real session to measure;
    - scripted runs do not exercise Vosk (the owner's pre-flight does).
- **S22, the second pass's known issues (2026-10-22, Sonnet 5.5; merged `8609183`).**
  - **Conversation screen.** `Frontend/ConversationLayout` (engine-free) keeps the speaker's head and the bubble clear. Cards stack in a
    column beside them, and older cards collapse to one line, then hide. The new-word card is a toast. `SupportLayer`, `DialogueBubbleLayer`
    and `WordCardLayer` draw from the model. Tested: no overlap at 1280 x 720 and 800 x 600, with 0 to 5 cards and 6 head positions.
  - **F10 label.** It moved top left, in a place the layout reserves.
  - **Beacon label.** `Frontend/BeaconLabel` keeps it inside the window and clear of the panel, the speaker and the cards. It was clipped:
    `campamento 111 bloc…`.
  - **Check card.** `Frontend/CheckCardLayout` wraps the text. All 20 items fit at both sizes; "Say no politely" overflowed before.
  - **Measured durations.** `scripts/playtest-report.sh` prints the whole session, the lesson, the walk and each check from the session
    log. A scripted route took 4 min 46 s; the first human session gives the real figure.
  - **Vosk end to end.** The README route says `hola` from the bundle's Kokoro WAV, and Vosk hears `hola` (96 %, `greet`). Two
    model-gated tests cover the WAV and push-to-talk paths.
  - **Other fixes.**
    - Pergola posts are now fence posts (scene digest `80486440…`).
    - The book's icons honour `--greyscale`; they ignored it, and block nouns were dark squares.
    - Saves from before S20 get the book once (`BookGranted`).
    - The README `--playtest` route matches the 20-item checks and now lights the fire, so the post-check runs; it hung before.
    - The legacy `SupportLayout` placement is removed.
  - **Checked by the orchestrator:**
    - the gate, 4,373 passed and 5 skipped;
    - the before and after conversation shots, the playtest dialogue, the beacon, the wrapped check card and the greyscale book;
    - the README route on the merged build, with the Vosk step.
- **S23, the LanguageTool check over the game's Spanish (2026-10-22, Haiku 5.5; merged `ddb834a`).**
  - **Extractor.** The Host's `--grammar-strings <file>` (`GrammarStringsExtractor`) writes 749 strings: 456 learner, 258 npc and 35
    template. The sources are dialogue, intents, english.json's Spanish sides, PHRASES.md, answer sets, prompts, checks and speech cards.
    lessons.json holds dialogue ids only. `scripts/content.sh grammar` runs the core's command. The cache, waivers (12, each with a reason)
    and strings file are in `content/grammar/`. A test keeps the committed strings file equal to the extractor's output.
  - **The run.** 0 errors.
  - **Waived.**
    - `si quiero` without the accent: answer matching strips accents on both sides, and the yes aliases come in accented and unaccented
      pairs on purpose.
    - The `de el` template: the guide always passes `many = 2`.
    - 13 lowercase fragments: a quoted `¿tiene?`, recipe parts joined into one sentence, and noun card labels.
  - **Fixed:** `traveller` became `traveler` in checks.json and english.json.
  - **Oracle:** 19 flagged and tagged, all agreeing on span and tag; 7 tagged but silent; 0 untagged but flagged.
  - **Checked by the orchestrator.**
    - The gate: 4,423 passed, 5 skipped.
    - The offline run with `--strict`: 0 errors, 0 warnings, 16 waived, 0 unchecked.
  - **The gate.** The game-v1 grammar step is **required** from now on, after its first waiver round (owner, D10). content/v1 stays
    advisory until M27a2 fixes or tags its three untagged examples.
  - **After a content change:** refresh the strings file and the cache with the local server (`scripts/languagetool.sh start`), as
    docs/game/README.md says. Otherwise the required step fails on a cache miss.
- **`main` caught up with the prototype (2026-10-22, owner: "yes pls merge prototype into main").** `main` had no commit of its own (it was
  156 behind), so the orchestrator fast-forwarded it to `feature/prototype` at `75bc5ea`. That is the tree the S23 gate passed, plus the
  gate-script change, which the orchestrator tested on its own. `main` was not merged through its own gate script, which predates the
  resource limits. Both branches now carry `scripts/limited.sh` and the limits; the S series keeps `feature/prototype` as its integration
  branch.
- **S24, the audio system, interaction and progress sounds (2026-10-22, Sonnet 5.5; merged to `main` `9291bab`, the first milestone merged
  straight to `main`).**
  - **The mixer.** It is engine-free in `Frontend/` (`SoundMixer`, `SoundCatalog`, `SoundSynth`, `SoundEventMapper`, `PresentationRng`);
    the output is `SoundEffectOutput`, or `NullSoundOutput` in scripted runs.
    - Volumes: voice 1.0, sfx 0.8, ui 0.8, ambient 0.6. sfx and ambient duck to 35 % under any voice line.
    - Microphone rule: from `Started` to `Stopped` sfx, ambient and ui are silent and in-flight sounds stop. The start beep is trimmed from
      the recording, so it never reaches Vosk.
    - A bed API for S25 (`SetBeds`, a 1.5 s cross-fade).
    - Each frame's cues are folded, so a step's chime replaces an answer's.
    - Flags `--sfx-volume`, `--ambient-volume`, `--no-sounds` and `--sound-log`; the volumes are kept in the save's settings.
  - **Assets.** 47 Kenney CC0 files (Impact, RPG and Interface packs) as 22.05 kHz mono WAV, 0.82 MB in all, plus synthesized chimes,
    beeps, sparkle and whoosh. The source of each file is in `assets/audio/SOURCES.md`. They are game assets, not in the content
    bundle.
  - **Checked by the orchestrator.**
    - The gate: 4,477 passed, 5 skipped; both grammar steps clean.
    - On merged `main`, the README route with `--sound-log`: session complete, 121 cues, and none inside the recording window.
  - **Not fixed:**
    - a scripted `--wav` answer's chime falls inside the fixed scripted recording window and is held back (real push-to-talk is fine);
    - in-flight one-shots are cut hard when recording starts;
    - the real audio device was not exercised headless;
    - the cue choices rest on file names, so the owner should listen once.
- **S19b, check answers out of the word record (2026-10-23, Haiku 5.5; merged to `main` `7fda074`).**
  - **The fold.** `LearnerWords` skips `InputPlace.Check` entries, which stay in the history and in What I said.
  - **Save version 3** (versions 1 to 3 load). A version 2 save is migrated on load:
    - if its history holds every entry, the rows are folded again, exactly;
    - otherwise the check uses the history still holds are subtracted, and first and last uses are repointed.
    The bump stops a re-saved version 2 save from being subtracted twice.
  - **Stale references to M50's removed files** fixed in `nuget.config`, `scripts/content.sh`, `scripts/README.md`, the root README,
    `.gitignore` comments and `docs/game/architecture.md`.
  - **Gate:** 4,481 passed, 5 skipped.
  - **Not fixed:**
    - a check that was trimmed out of a version 2 history stays counted;
    - `docs/research/2026-10-21-prototype-research.md` (a dated snapshot) still cites the removed files;
    - the parked `.github/workflows-pending/ci.yml` still calls `pack.sh` and `sample-host.sh`. CI is deferred (D1), so it is noted for
      when CI returns.
- **S20b, replay from the book (2026-10-23, Haiku 5.5; merged to `main` `b9c64de`, bundle `e244285b…`).**
  - **Built.** A speaker button on every seen catalogue noun, every phrase, and each phrase a lesson taught. Click or Enter plays the
    recording, Shift the slow one. Up and Down select rows. The logic is engine-free (`BookRecordings`, `BookSpeechControls`,
    `BookSpeaker`). Playback goes through the NPC voice and the mixer's ducking, and `--mute` keeps it silent.
  - **Recordings.** All 19 nouns and 21 phrases now resolve:
    - the 12 block nouns got cards (24 WAVs, `ef_dora`);
    - the 7 heard-only phrases play their NPC lines (`audioLineIds`);
    - `card.río` is stored as `card.rio`, because line ids are ASCII (`SpeechFiles.CardId`).
  - **Scripted flags:** `--book-play <noun|phrase:id>` and `--book-play-slow`.
  - **Grammar refresh:** 758 strings and 9 new card-label waivers. Offline strict is clean.
  - **Checked by the orchestrator:** the gate (4,493 passed, 5 skipped) and the Nouns screenshot (selected row and speaker buttons on seen
    nouns only).
  - **Note.** Every bundle change makes older saves refuse to load (the content-hash rule since S10). A candidate for the experiment
    branch.
- **S25, ambient sound and footsteps (2026-10-23, Haiku 5.5; merged to `main` `6c73d63`).**
  - **Beds.** Wind on the heights, the river and lake, the waterfall (6 to 40 blocks), the village murmur and dusk insects are synthesized
    10 s loops with equal-power seams. Birds and fire crackle are scheduled one-shots from the presentation RNG. All of them go through
    S24's `SetBeds` (1.5 s cross-fade), panned by bearing.
  - **Footsteps.** Kenney CC0 for grass, snow, wood, stone, jump and land; synthesized dirt, gravel, sand and water splash.
    `SurfaceOf` covers every block kind.
  - **Landing.** It plays only after a jump or a fall of more than 1.5 blocks. The orchestrator sent this back: the first version thumped
    40 times walking down the trail; now it is 4.
  - **Assets:** 57 WAVs, 1.0 MB in all (S25 adds 0.2 MB).
  - **Checked by the orchestrator:** the gate (4,557 passed, 5 skipped). The agent's route log: beds change from the heights' wind to the
    valley's birds and back, no cue plays in the recording window, and grass 216, gravel 47, wood 7, sand 1.
  - **Intermittent test found:** `VillageFrontendTests.DrawingTheFiguresTheArmAndTheShoulderCameraAllocatesNothingOnceWarm` failed once
    under a loaded parallel run. It measures per-thread allocations over 1,000 frames after a 200-frame warm-up; tiered-compilation
    promotion inside the window is the likely cause. The fix (measure up to three times and take the least, so a real per-frame
    allocation still fails) goes into the next brief, S26.
  - **Not fixed:** the pace is distance-based (no run pace); lamps stand in for torches; fades run on frame time.
- **S26, verbs in sessions 1 and 2 (2026-10-23, Sonnet 5.5; merged to `main` `7748348`, bundle `becd2669…`).**
  - **Session 1.** A 5-step verb unit (`u4v-*`): quiero, tengo and necesito, answered with the yo form. The pattern is named once,
    truthfully: -o for I and -as/-es for you, and "some verbs also change in the middle" (tengo against tienes).
  - **Session 2.** "Personas", 20 steps:
    - the villager: él/ella, nosotros and ellos, plus `es`, `somos`, `está` and `vamos` as chunks;
    - a walk to the stall;
    - the shopkeeper: usted and ustedes.
    The conjugation of ir and estamos/están move to session 3.
  - **Sessions, multi-NPC lessons and replay.**
    - Lessons unlock by session (`session`, `objective`, `verbs` in lessons.json; `--session <n>`). A step may name its NPC (`npc`, `at`).
    - Practise again from the book (a button or P; `--book-practise`).
    - Save version 4 (`EverCompleted`, a pending replay); versions 1 to 3 load.
  - **The person check** (added by the orchestrator, from the review). A verb step declares `person`. The runner reads the finite verb's
    person from the turn's words, and a mismatch with no `conj.*` from the core is a Miss whose retry names the ending. `quieres madera`
    at "¿Qué quieres?" was credited before.
  - **The Opus pass applied**, with these fallbacks:
    - `yo tieno` is `vocab.unknown` in the lexicon, so the error example stays `yo teno`;
    - `quiero pan, por favor` collided with the stall's repair choices and was dropped;
    - two variants rely on the keyword rule and are waived as `allowRuleOnly`.
  - **Content fixes from the review.** Session 1 asks "¿Tienes una fruta?", not wood the learner does not have. "Ella está en el
    campamento." replaces a false line. Six persons plus three irregular verbs was too much for one session, and is trimmed.
  - **Speech.** 62 WAVs (31 lines). The check bank has 27 items. 13 rows are `llm-opus (provisional)`.
  - **Measured from the real WAVs.** first-words is 37 steps: about 5.4 min quick, 8.7 min slow, about 9.7 min with the walk. Personas
    is about 5.3 to 7.5 min with walks. To get under 10 minutes, `u3-table`, `u3-torch` and `u4v-i-have` were cut.
  - **Checked by the orchestrator.** The gate: 4,595 passed, 5 skipped; both grammar steps clean. On merged `main`,
    `--session 2 --lesson-run personas` completes 20 of 20.
  - **For S27:**
    - a wrong-person Miss is still credited as a phrase use in phrase tracking (`AfterTurn` runs before the lesson decides);
    - the README's scripted `--playtest` answers are written for 20 check items, but there are now 27.
- **S27, "Ayer y mañana", session 3 (2026-10-23, Sonnet 5.5; merged to `main` `ca27650`, bundle `492cbe4b…`).**
  - **The lesson.** 19 steps, the villager then the shopkeeper:
    - ir's present (voy, vas, va, vamos, van) and ir a + infinitive for plans;
    - the preterite comí/comiste, bebí/bebiste and fui/fuiste;
    - ayer, hoy, mañana.
    About 5.2 to 7.6 minutes, measured from the real WAVs.
  - **The tense check.** A step may declare `tense` (pres, pret, ir-a), as S26's person check does; a wrong tense with no core error is a
    Miss. Present-for-plan is credited, as the orchestrator decided ("correct Spanish is never a Miss"): `mañana voy al río` at an ir-a
    step ends Done, with a gentle `presentNote` naming the practised form.
  - **Fixed along the way.**
    - A form Miss no longer counts as a phrase use. This was S26's leftover.
    - A form Miss no longer shows the core's praising recast (`¡Eso es! «Como pan».`), which also affected S26.
    - The newest session's lesson now has priority at a shared NPC; session 3 was blocked by Personas.
    - The README `--playtest` and `--check` answers now cover 31 items.
  - **The Opus pass applied.**
    - `tuve` became `bebí`: preterite possession is unnatural, and beber follows comer's -í/-iste. The plan's `tuve` was the orchestrator's
      wording.
    - estar left the lesson.
    - `al` is explained, and fui = ir/ser is said once.
    - Trailing time words are accepted (a trailing `(mañana|ayer|hoy)?` on every state_action grammar).
    - The `tomé` variants were dropped: no grammar for tomar.
  - **Taught forms for S28.** lessons.json declares `{lemma, person, number, tense}`, with `ir-a` periphrastic (ir's present + a +
    infinitive) and built by `TaughtVerbForm.Text(morphology)`.
  - **Speech.** 44 WAVs; 12 rows are `llm-opus (provisional)`; the check bank has 31 items.
  - **Checked by the orchestrator.** The gate: 4,676 passed, 5 skipped. On merged `main`, `--session 3 --lesson-run ayer-y-manana`
    completes.
  - **Core finding:** `voy comer pan` (ir with a bare infinitive, no `a`) passes the core clean. The lesson's tense check still catches it.
    Proposed as core M51.
  - **For S28:** docs/game/playtest.md and scripts/playtest/tester-readme.md still say 20 check items, but there are 31.
- **S28, the book as a book, with conjugation tables (2026-10-23, Sonnet 5.5; merged to `main` `f966590`). This closes the owner's queue
  for `main`.**
  - **The spread.** A leather cover, cream paper, page edges and a stitched spine, all drawn in code (`Frontend/BookGeometry`). Five ribbon
    bookmarks replace the tabs, with the same `TabRect`/`TabAt` API. A 0.3 s page-turn leaf plays S24's `book_page`. The left page lists;
    the right page shows the selected item in full.
  - **Conjugation tables** (`Frontend/ConjugationTable`, `ConjugationLayout`).
    - Rows yo, tú, él/ella/usted, nosotros, ustedes/ellos; columns present, past, future (ir a).
    - Used forms are highlighted with counts, taught forms are plain, and the rest are dashes. A column shows once that verb has a form in
      it that was taught or said (per verb, a little stricter than the spec, so querer's table has no empty columns).
    - Forms come from `Morphology` or `TaughtVerbForm.Text`. `vamos` is counted as ir present 1pl, which fixes S27's imperative-reading quirk.
  - **Flags:** `--book-verb`, `--size WxH`.
  - **The playtest docs now give the right counts:** 31 check items, 7 units and 37 steps.
  - **Checked by the orchestrator:**
    - the gate, 4,709 passed and 5 skipped;
    - the before and after spreads;
    - the ir table (voy ×2, fui ×1, voy a ir ×1, vamos ×1 highlighted; vas, va, van, fuiste plain);
    - the Lessons spread;
    - the comer table at 800 x 600.
  - **Not fixed:**
    - the right page does not scroll, so long blocks clip at 800 x 600;
    - narrow rows drop their right-hand note;
    - ir-a counts come from the bounded history;
    - the "What you said" list glosses `vamos` as "let's go";
    - ser and estar are both glossed "to be".
- **X01, saves survive content updates (2026-10-23, Sonnet 5.5; merged to `feature/experiment` `bf90201`, with X00's backlog).**
  - **Across bundles.** A save of another bundle now loads unless strict. These are kept wherever their ids still exist:
    - the learner record (kept whole), settings, the session, words, phrase records, the objective and NPC states with their skills;
    - tips and inventory;
    - lessons, mapped by step id: a completed lesson stays completed, and an in-progress lesson keeps its longest finished run from the
      first step.
    Gone ids are dropped and logged. New rows start as in a fresh game.
  - **World edits.** They are applied by position, and only where the player could have made them (a gathered block, or planks or a torch
    placed). Other differences belong to the new generation and are kept. A player standing inside a new block starts at the spawn.
  - **Reporting.** `SaveMigrationReport` (engine-free), `migrate:` console lines, a `save_migration` session-log line, and the toast "The
    game was updated. Your progress was kept; N old records no longer in the game were dropped."
  - **Strict mode** (`--strict-save`, implied by `--playtest`) keeps the refusal, which the frozen-bundle recall needs. A same-bundle load is
    byte-identical to before.
  - **Checked by the orchestrator.**
    - The gate: 4,727 passed, 5 skipped.
    - The agent's demo: a scratch bundle without `u6-recap` migrated 136 records and dropped 1; strict refused.
    - The README's full scripted `--playtest` run on the merged branch: pre 31/31, session complete, post 31/31, save written.
  - **Not fixed:**
    - placeable items are listed in `SaveMigration.IsPlaceable`, so a new placeable needs adding there;
    - edits are not remapped when a layout moves.
- **X04, the session summary card and a second try after "No sé" (2026-10-23, Haiku 5.5; merged to `feature/experiment` `6e14821`).**
  - **The card** (`SessionSummary`, engine-free). The closing card adds a "This session" block: the lessons done; new words (nouns only);
    the phrases said without help; the verb forms used; at most two to practise next time; how to replay. It has counts of things done,
    no scores, and a `session_summary` session-log line. The card stays up 45 s or until Esc.
  - **The second try** (`RetryQueue`). A "no sé" at a phrase or lesson step queues the phrase (at most 2), which is offered once at the next
    talk with the same NPC as a review, with "Let's try that one again: …". It is never offered inside a check or for service lines. The
    answer is logged as `second_try_answered recovered|not_yet`, a world event, not a new phrase-use kind, so D-game-10's measurement
    is unchanged.
  - **Checked by the orchestrator:** the gate (4,743 passed, 5 skipped) and the summary card screenshot.
  - **Not fixed:**
    - the card is not shown on F6, F10 or quit;
    - the retry waits for a talk with the same NPC, never between lesson steps;
    - the retry does not step one rung lower on the support ladder.
- **X05, autosave and a crash report (2026-10-23, Haiku 5.5; merged to `feature/experiment` `001d75a`).**
  - **Autosave** (`AutosavePolicy`, engine-free).
    - It saves when a lesson step, a lesson, an objective step or the session completes, and when a dialogue closes, plus every
      18,000 ticks (5 minutes of play, never the wall clock). It waits for a frame at rest: no turn, dialogue, recording, check or panel.
    - The file is `autosave.save` in the session folder (`playtest.save` in a playtest), written atomically: a temp file is flushed to disk,
      then renamed over the save. A "Saved" toast shows.
    - `--no-autosave` turns it off.
    - Determinism: session logs with autosave on and off are identical apart from timestamps.
  - **The crash handler** (`CrashHandler`, `CrashReport`).
    - It covers `game.Run()`, AppDomain and unobserved tasks.
    - It writes `reports/crash-s<n>-t<tick>.md` (the exception, the stack, the game state, 200 log lines, the build commit), then a
      last-chance `crash.save` that never overwrites the good save. Then it exits 1. It never throws.
    - The test-only hook is `--debug-crash-after <tick>`. `playtest-report.sh` lists crashes beside the F8 reports.
  - **Bugs fixed:**
    - a crash before the session folder existed lost `crash.save`;
    - every autosave rebuilt the scene (about 600 ms hitch; now 12 to 22 ms after the first);
    - a save was renamed without flushing to disk.
  - **Checked by the orchestrator:** the gate (4,781 passed, 5 skipped); a forced crash on the merged branch (exit 1, the report and
    `crash.save` written).
  - **Not fixed:**
    - the first autosave of a run costs about 0.7 s;
    - saves run on the game loop;
    - a crash inside a dialogue leaves no `crash.save` (the last autosave stands).
- **X03, book polish (2026-10-23, Haiku 5.5; merged to `feature/experiment` `08152fb`, bundle `2156744b…`).**
  - **Scrolling.** The right page scrolls: the wheel over it, Shift+Up/Down, Shift+PgUp/PgDn, a "more below" marker, and a reset on a new
    selection or page (`--book-detail <lines>` for screenshots). Long blocks are readable at 800 x 600.
  - **Glosses.** ser "to be: what", estar "to be: where or how" (glosses.json).
  - **Narrow rows** shorten their note with "..." instead of dropping it.
  - **One reading rule** (`VerbReading.Normalize`, engine-free, shared by the labels and the conjugation table, added after an orchestrator
    review). A used form the core read as an imperative, or with no tense, whose text is an indicative present of the same verb is read
    as that present: `está — he/she/it is`, `vamos — we go`, `come — he/she/it eats`. A form with no indicative twin keeps its command
    label (`ven — come!`). This replaces S28's vamos-only special case.
  - **Checked by the orchestrator:** the gate (4,816 passed, 5 skipped) and the scrolled lesson block at 800 x 600.
  - **Not fixed:**
    - `vamos a comer` is not labelled "we are going to"; the table's future column counts it;
    - a genuine command that has an indicative twin reads as the present.
- **X08, UI scale and readable text (2026-10-23, Sonnet 5.5; merged to `feature/experiment` `f3cf721`).**
  - **The scale.** 100, 125 or 150 % (`--text-scale`, F9, and the save's `SaveSettings.TextScale`). The 2D layers lay out on a window/scale
    canvas, so every engine-free layout model just gets a smaller canvas, down to 533 x 400. `UiScale.Effective` lowers the scale for a
    window too small. Atlas pictures are drawn in window pixels. The font floor is 16 px.
  - **Layouts degrade predictably.**
    - Dialogue: drops its hints, then the buttons' English, then collapses the log.
    - Lesson strip: pages long English (`[M] More`), wraps its buttons, and two rows of Spanish. The longest step is 431 characters, which
      were silently cut before.
    - Check card: compact metrics.
    - Book: narrow ribbons and stacked tenses.
    - Checklist and toast: wrap.
    - F1 help: scrolls.
    - Crafting: drops its station column.
    - F3 overlay: hidden on small canvases.
  - **Contrast.** `UiPalette` has 35 pairs, all at least 4.5 : 1 on their panel and over a bright sky (WCAG AA; tested). Raised: dialogue
    notes 5.69 to 7.35, the book's unseen ink 3.25 to 5.04, the support card's hint about 2.7 to 7.35, and the ribbons.
  - **Bugs fixed:**
    - the check card's option 3 overlapped the footer at 800 x 600 and 150 %;
    - a ribbon fell off the cover;
    - the strip ran over the checklist and the panel;
    - the beacon label crossed the strip;
    - the workbench panel overlapped the checklist;
    - F1 help overflowed even at 100 %;
    - the hotbar icons bled.
  - **Checked by the orchestrator:**
    - the gate (4,908 passed, 5 skipped; 92 new tests on six canvases);
    - the conversation at 800 x 600 and 150 %;
    - the README route on the merged branch (38 lesson steps, the Vosk step, session complete).
  - **Not fixed:**
    - colour-only signals are only partly addressed;
    - fractional magnification gives uneven pixel strokes;
    - the word card and the workbench panel may overlap on the smallest canvas;
    - `--report-open` does not end a scripted run, by S21's design.

- **R16, splitting `VoxelGame` (2026-10-26, Opus 5.5 with the owner; on `feature/organise`).**
  - **Done:** the camera, figures, arm, beacon and canvas; the saves, autosave and crash source; the report; the frame's input snapshot;
    push-to-talk; the check battery, mic check, dialogue, book, inventory, crafting, controls help and playtest flow; the screen stack; the
    keys as commands; the tick's observers and the turns' presenter; the scripted run's end; the sound's set-up, the session's start, the
    drawing, the world's input, the ending and the greeting. `VoxelGame.cs` went from 2,957 lines to about 850: the composition root and
    MonoGame's loop, which calls the components in the frame's order (a `FrameLoop` class was planned; it would only have taken every
    component to call them in the same order, so the order stays in `Update`). The checks are the three game test projects and the scripted
    runs of `scripted-runs.md`, compared with a baseline taken before the first step.
  - **Not done (phase 2, handed over in `docs/archive/game/r16-phase2.md`):** moving the engine-free components (the screens' flows, `ListenSession`'s logic, the report's state
    and the like) to `Vocaterra.Game.Core`; renaming the test projects to `*.Unit.Tests` and `*.Component.Tests`.
  - **Findings:**
    - Every scripted run with a turn for the worker lands its turns 1 to 8 ticks apart from one capture to the next, with the same binary
      (r02 to r06, r10, r11, r16, r17). A scripted run does not freeze the world exactly while a turn is out. Those runs compare only in
      the order of what they print.
    - The report's table names "check (pre)" while a playtest's mic check is on top and takes the keys: the report names screens in a
      different order from the one that takes the keys. Kept as it was (`ScreenStack`'s name order), to fix later.
    - F6 and `--save` rebuilt the fresh scene at every save (about 0.6 s); the autosave and the crash save used a cached one. All saves now
      use the cache, and they come out the same (r09).
    - The hotbar's and the panels' wheel baselines were two fields with the same value at every frame the panels can see; they are one.
- **R16 phase 2 and the game's folders (2026-10-28, Opus 5.5; on `feature/game_refactor`, `docs/archive/game/game-projects-layout.md`).**
  - **Done, batches 1 to 8:** the leaves, the screens' models, the flows and the scripts moved to `Vocaterra.Game.Core` (batches 1 to 4);
    `GameKey`, `IInputFrame` and Game's `KeyMap` (D-p2-1a), the screens, the key bindings, the push-to-talk, `WorldStepper`, `PlaytestFlow`
    and `CheckBattery` after it (batch 5); Game regrouped into `Startup/`, `Diagnostics/`, `Graphics/` and `UI/` by feature (batch 6); the
    tests mirroring the sources, with `Component/`, `TestSupport/` and `Architecture/` (batch 7); the docs and the stale project names
    (batch 8). One commit per step, each built with warnings as errors, format-checked and tested; `Utilities/`, `Screens/`, `Frame/`,
    `Persistence/`, `Reporting/` and `UI/English/` are gone from Game, `Utilities/` and the root-level file from Core.
  - **Seams (small code changes, each in its commit's body):** `AutosaveController` and `CheckBattery` take the clock as `Func<double>`;
    `CheckBattery` writes to the new Core interface `ICheckLog` (`SessionLog` implements it); `SoundSetup` takes a `SoundOptions` record
    (`LaunchOptions.Sound`); `LookControl` owns `RadiansPerStep` (`CameraBasis` reads it); the strip's key rules are `LessonActions`; the
    screens' click hit-tests are delegates from Game (the lesson strip, the speech buttons, the mic check's buttons, the crafting rows, the
    book's geometry). `BookGeometry` and `BookRect` went to Core with the book (integer arithmetic on the viewport, needed by the book's clicks).
  - **Checks:** the scripted runs of every batch against a baseline of `e932b20`: `compare-runs.py` exit 0 for batches 1 to 6 (batch 7 moved
    tests only, batch 8 docs only). Tests: Game.Core.Tests 863 -> 1208, Game.Tests 872 -> 529, Host.Tests 36 (with the models; two new tests,
    `KeyMapTests` and an `InputSnapshotTests` case, so the total went from 1735 to 1737). `core-move-scan.py`: 45 MOVABLE, 49 BLOCKED-self,
    7 BLOCKED-deps in Game (172 files at the start, 101 now); every MOVABLE file left is a layout, a mesher, graphics or the command line,
    which section 5.2 of `r16-phase2.md` and D-p2-3 keep in Game.
  - **Not done:** the live run by hand of `scripted-runs.md` (no scripted run presses a key, and batch 5 moved the keys): for the owner.
    The batch branches were not gated one by one into `feature/game_refactor` (the owner asked for all the work on that one branch); the
    gate's checks ran once at the end.
  - **Findings:**
    - `compare-runs.py` masks no path: a baseline taken from another checkout differs in the scene's path, and a capture taken while a build
      waits on `limited.sh` has its queue line in `stdout.txt`. Both were masked by hand here before the comparison.
    - The namespace `Vocaterra.Game.UI.Inventory` hides Core's `Inventory` type from code under `UI/`: `FrameView`, `HotbarLayer` and
      `InventoryLayer` write `Core.Items.Inventory`.
    - `scripts/playtest-build.sh` and `scripts/playtest/playtest.sh` still published `src/Vocaterra.MonoGame/Vocaterra.MonoGame.csproj` and ran
      `./Vocaterra.MonoGame`, which no longer existed (since the projects were renamed, before R16), so no playtest folder could be built.
      They now name `src/Vocaterra.Game` and `Vocaterra.Game` (and its `deps.json`). Checked 2026-10-28 at `bc6ee39`: the build's checks all
      pass (773 files, 210.7 MiB, 430 speech WAVs, no sherpa-onnx or ONNX Runtime native library), and `./playtest.sh` with a scripted
      screenshot opens the pre-check, writes the session log and the playtest save, and exits 0.
    - A worktree's `obj/` was left corrupt by an earlier session (a generated `AssemblyInfo.cs` and a reference assembly of binary garbage):
      the build failed with CS1056 and CS0009 until `bin/` and `obj/` were deleted.
- **G8 (2026-10-27, Haiku 5.5, merged to `feature/language-packs` `00ff1e5`): the spoken-mistake test.**
  - Gate with models: 5257 passed, 5 skipped, 0 failed (5262 in all, as at M52); build, format, content, grammar and licence scan clean.
    The two extra skips are the Parakeet tests: `sherpa-onnx-nemo-parakeet-tdt-0.6b-v3-int8` is not in the shared models folder on this
    machine (only Kokoro and Vosk are); `scripts/fetch-models.sh` restores it.
  - **Cause: an acoustic onset, not the cache and not a phrase set.** The guide's prompt is open (`RecognitionRequest.Open`, no grammar),
    and `content/game-v1` has no `a dónde`. Some Kokoro renderings (speaker 28 at 0.9, speaker 53 at 1.0) start with about 80 ms of
    silence and then a weak «d» onset at 80 to 120 ms, which the 120 ms trim pad (`AudioClips.PaddingMilliseconds`) keeps and Vosk hears
    as «a» (0.38 to 0.42). The cached clip and speaker 28 at 1.0 are heard right (0.82 to 0.95).
  - Pad sweep: 105 and 75 ms clear the four clips tried, 90 ms does not, and 60 ms breaks `hola.wav` (0.42, intent unknown, 3 failures in
    `SpokenRouteTests`). The curve is not monotone, so the pad was not changed. Choosing it is a playtest question.
  - Fix: the test accepts an optional leading «a » (`^(a )?dónde está la árbol$`) and still asserts `gender.agree` and the feedback. Accepted
    by the owner on 2026-10-27, although the cause is acoustic and not the bundle's phrasing (the brief's condition for loosening the test).
  - **Core finding (no milestone planned):** `KokoroSynthesizer.Synthesize` (sherpa-onnx `OfflineTts`, CPU, one thread) gives the same
    audio within a process but different audio across processes (SHA-256 prefixes `736AD51E…` and `F93AD1C0…`, same code and model). So a
    `DirectorySynthesisCache` entry cannot be checked against a fresh render. The cached clip for this line matches no fresh rendering.
    Owner, 2026-10-27: keep it logged for later; no milestone now.
- **Test performance, the game's half (2026-10-29, Opus 5.5, `feature/test-performance`; plan `docs/process/test-performance-plan.md`, T1, T2, T4).**
  - **The route was rebuilt for almost every test.** A build of the 384×128×384 route takes about 0.6 s alone and up to 2.5 s beside the
    other tests. `VoxelGrid`, `VoxelChunk` and `Scene` get an internal `Clone` (blocks copied; version, edit count and cached digests kept);
    `RouteScene` builds each seed once per test process and hands out clones, and `TestGame.Route`, `VoxelTestSupport.BuildRoute` and seven
    direct builds go through it. The builder's own determinism test and the seed-42 golden still build for real; two new tests pin the clone.
  - **34 test classes loaded the bundle themselves** (since R16 most of them in Game.Core.Tests), plus five uncached calls: they use
    `TestGame.Bundle`, `Nouns` and `Phrases` (`TestGame` is linked into Game.Tests).
  - **Effect (with models):** Game.Core.Tests 146 s to 37 s, Game.Tests 39 s to 15 s, the whole run 186 s to 87 s with the core's half
    (153 s to 84 s without models); 5,449 passed (two new), 3 skipped, as before. Game.Core.Tests' peak memory fell from 3.6 to 2.8 GB.
  - **Fast tier:** `scripts/test.sh fast` (26 s) leaves out the tests marked `[Trait(TestSpeed.Trait, TestSpeed.Slow)]`: for the game the
    world-generation classes, the 14 model-gated facts, `PlaytestReportTests` and seven long single tests. `full`, the gate and CI run all.
  - **Finding:** xUnit runs a class's tests one after another, so Game.Core.Tests now ends with its largest classes (`VillageTests`,
    `SceneTests`, `SaveMigrationTests`, `GuideTurnTests`) while most of its cores are idle; splitting them by topic is the next lever (T6).
    `docs/game/README.md` said five model-gated tests are skipped without models; it is fourteen.
  - **The seed-42 timing test failed under the new load pattern** (two of fifteen runs, very likely a third whose log was not kept: the fastest
    of three builds 3.5 to 4.0 s against a 3 s budget, about 0.6 s alone), because a run now starts by building the route for every seed at once. It moved unchanged to
    `SceneBuildTimeTests` in a serial `TimingCollection` that runs after the rest of Game.Core.Tests, with `ChunkIndexTests` (a 100 ms
    budget); the budgets are unchanged.

- **The C series begins (2026-10-29, Opus 5.5; on `feature/content`, `docs/game/content-plan.md`, D-game-21).**
  - The plan was checked against `main` at `604903f`, and its paths were moved onto R16's layout (section 2 of the plan).
  - Baseline with the models at `604903f`: 5447 passed, 3 skipped, 0 failed. The per-project counts are Host 36, Game 529, Game.Core 1208,
    Nlp 2452 (3 skipped), Nlp.Es 1129, Fuzzing 76 and Golden 17. The build has 0 warnings.
  - Found while planning, and left for the series to fix:
    - the guide is "Luna" in code and content, but "Rosa" in two comments (`PhraseReview.cs:31`, `LessonRunner.cs:444`);
    - the scene is read twice: once hashed in the bundle, then again from disk by `SceneLayout.Load`, and `--scene` skips the check;
    - a non-guide NPC's beacon label is its raw agent id ("shopkeeper"), not its label (`VoxelSession.cs:189`).
  - The main checkout held 4.5 GB of stale `game/` and `nlp/` build output from before R01, and the merged worktree `game_refactor-work`
    held 3.5 GB of `bin/` and `obj/`. Both were deleted to make room for the series' worktree; the free disk had fallen to 9.8 GB.

- **C01 to C03: the content contract, one load and the locale bundle (2026-10-29, Opus 5.5; gated on `feature/content` at `9e7960d`).**
  - **Gate:** 5482 passed, 3 skipped, 0 failed, with the models; the build has 0 warnings; format, content, grammar and both licence scans are
    clean. That is 35 new tests: Game.Core 1208 to 1243, the rest unchanged.
  - **Scripted runs.** After C01 and C02 all 16 runs are the same as the baseline once the hash is masked (`compare-runs.py` now masks it).
    After C03 they differ only where expected: the new `locale:` line in `stdout.txt` and the `session_start` locale fields (all 16), and the
    report's Locale row (r12).
  - **Found:**
    - The core's identity guard pinned game-v1's bytes, because the verdicts stamp the bundle's hash. It is fixed as core milestone M54.
    - The core's manifest tests assumed game-v1 names no support language; fixed as M55.
    - NJsonSchema ignores draft-07 `if`/`then`, so the schemas write those rules as `anyOf` with `not`.
    - JsonSchema.Net 9.x has no licence expression, so the schema test uses NJsonSchema (MIT).
    - The old `english.json` held one orphan gloss, "Necesitas una botella.".
  - **Disk:** the stale `game/` and `nlp/` build output of the main checkout (4.5 GB from before R01) was deleted.

- **C05, C06 and C08: the game's words, one trigger model and the NPCs as data (2026-10-29, Opus 5.5; gated on `feature/content` at `19a328e`).**
  - **Gate:** 5520 passed, 3 skipped, 0 failed, with the models; the build has 0 warnings; format, content, grammar and both licence scans are
    clean. That is 38 new tests, all in Game.Core (1243 to 1281).
  - **Scripted runs.** After C05 all 16 runs are the same as after C03. After C06 they differ only by the new `torch_at_ring` lines in
    `session.jsonl` and `stdout.txt` (r01: one, at the camp's ring; r10: two, the camp's and the player's).
  - **Found:**
    - The beacon on a non-guide NPC showed the raw agent id (`shopkeeper`); it now shows the NPC's label (`Tendero`).
    - The four scaffolded phrases were wired into `PhraseProgress` itself; they now come from the NPCs' `focus`, so a test that builds a
      progress passes them in.
    - The NPCs' labels and names were not grammar-checked; they are now (4 strings, clean). C05's 26 words gave 5 capitalisation infos on
      fragments, none an error.

- **C07 and C04: the objectives as data, save v5, and the player's English in the locale (2026-10-29, Opus 5.5; gated on `feature/content` at `59b7f01`).**
  - **Gate:** 5554 passed, 3 skipped, 0 failed, with the models; the build has 0 warnings; format, content, grammar and both licence scans are
    clean. That is 34 new tests: Game.Core 1281 to 1314, Game 529 to 530.
  - **C07.** `game/session.json` and `game/objectives/<id>.json` replace `Objective.Camp()` and the `SessionStage` rules. The HUD's line is the
    first objective not done. Hooks name the code's behaviour (`guide-met`, `spare-torch`, `point-own-camp`; `lesson-begun`,
    `scan-fire-ring`). Save v5 keys the steps by objective, and an older save's keys are mapped before the migration. `Fixtures/save-v4.json`
    tests the out-of-order ending. The runs differ only by the saves' version and keys.
  - **C04.** About 150 player strings moved to `locale/en/ui.json`, with `{npc:…}`, `{key:…}` (`InputKeys`, which `KeyBindings` reads) and
    plurals. `NoLiteralTextTests` and the locale glyph test keep it that way.
  - **Found:**
    - The light-fire objective has to wait for `fire_lit`, not the torch at the ring, or the session-complete line would come before the
      fire's.
    - The tests made older saves by relabelling a new one, so the v4 key map leaves a key that has a dot.
    - Three comments, not literals, held Spanish (`"Hola."`, `"¿En qué te ayudo?"`, `"Otra vez, por favor"`); the scan skips comments.
    - The speech and microphone problem messages are made where no locale is at hand (the worker, the engine, the capture), and the English
      verb labels are grammar rules. Both are open points on the scan's allowlist.
    - The beacon now says "1 block", not "1 blocks".
    - The scripted runs caught a console line that interpolated a property turned method (`{heard.Text}` printed `System.Func…`); it compiled
      because a method group has a natural type. Fixed with `DialogueModel.HeardText`; the runs are then all the same as after C07.
- **V01: NPC behaviour from data (2026-10-30, Opus 5.5 xhigh; gated on `feature/village` at `0ad929d`).**
  - **Gate:** 5591 passed, 3 skipped, 0 failed, with the models; 0 warnings; format, content, grammar and both licence scans clean (16 new
    tests). Bundle `4b183de5…`; the goldens did not move; `prerender --check` renders nothing (430 files unchanged).
  - **Built.** `game/npcs/<id>.json` schema 2: `prompts` by state, `opening` (`event`, `when`), `voice` (`speaker`, `name`, `speed`), `keyWords`
    (out of `words.json`) and `poolPrefix` (default the id; the shopkeeper's pools are `shop_*`). `NpcService` takes prompts, the opening and
    the warm-up from the roster; the shopkeeper's fail-forward and the guide's review and transfer offer stay by id. `SpeechSpeakers` and the
    renderer's voices come from the NPC files; `--talk` is checked against the roster. The shopkeeper is "he" in the locale and the sheet.
  - **Found:**
    - The opening events are raised by the game into the agent and are not in `host-events.json`, so the validator checks them against the
      agent's `onHostEvent` transitions instead.
    - `PhraseSheet`'s guide-offer-as-stall-offer is now a rule: a prompt an NPC names that no row names takes the rows of the other NPC prompt
      with the same answer set and allowed intents (exactly `guide-offer` to `stall-offer` today).
    - The save keeps one `KeyWordsTaught` flag, so only the first NPC with key words shows the cards; a second needs a save field.
- **V02 and V03: host actions, the beacon by place, items and orders (2026-10-30, Opus 5.5 xhigh; gated on `feature/village` at `d53b957`).**
  - **Gate:** 5663 passed, 3 skipped, 0 failed, with the models; 0 warnings; format, content, grammar and both licence scans clean (72 new
    tests). Bundle `5b4adae6…`; the identity golden gained the villager's new line.
  - **V02.** `host-events.json` schema 2: `give_item` with `count` (a number or `"qty"`, 1 to 5), `point_at_place`, `point_at_slot`. A slot
    resolves to a scene label first (the nearest point of that label; when the word is also the catalogue noun of the block there, the
    nearest such block, so the river is pointed at exactly as before), then a catalogue noun, else `<npc>.unknown_place`. World events
    `beacon_reached` and `place_asked`; a `do` step's `pointPlace`. The villager points from the slot; new line `villager_unknown_place.1`
    "No sé dónde está." (provisional, em_santa).
  - **V03.** `Coin = 30`, `Apple = 31`, `Axe = 32` (placeholder art until V05); the axe fells a trunk in 15 ticks, not 60. `take_item` and
    `sell` (too few coins: nothing changes hands, `<npc>.cant_pay`). Objectives take `session` and `deliverTo` with `deliver` (one generated
    step, `delivered`), delivered when the talk opens; orders never hold back the first session's ending. No save change.
  - **Found:**
    - The session takes its own arrow down before `BeaconDirector.ClearIfReached` sees it, so the session logs those arrivals and the
      director its own beacons (the start, `--beacon`).
    - Section 6's S3 demo line was out of date: the game at seed 42 says "El río está allí, adelante, a la izquierda, a cincuenta y cuatro
      bloques." (a test now pins it; section 6 corrected).
    - The scripted runs are not in `dotnet test` and have no committed baselines; they gain `beacon_reached` and `place_asked` lines (V09).
- **V04 to V07: the village, the figures, the voice trial and the battery by lesson (2026-10-30, Opus 5.5 xhigh; gated on `feature/village`
  at `600b8d7`).**
  - **Gate:** 5687 passed, 3 skipped, 0 failed, with the models; 0 warnings; format, content, grammar and both licence scans clean. Bundle
    `f32a0284…`; world digest `6420e200…`, village digest `1a6c04b2…` (radius 24); seed-42 build about 0.6 s.
  - **V04.** Radius 24; the bakery with its oven, the smithy with a lit forge, anvil and crates, the well, the farmer's apple crate, the
    orchard (at x 19, not 17: a crown at 17 hung over the field's fence and failed the route test), the fisher's crates and lamp, paved
    strips; labels `panadería`, `herrería`, `pozo`, `torre`, `campo`; stand points `npc.baker`, `npc.smith`, `npc.farmer`, `npc.fisher` (no
    placements: the validator refuses an NPC that is not in the roster).
  - **V05.** Four figures and faces (the baker a woman); the coin, apple and axe icons and held sprites.
  - **V06.** `--trial <folder>` in the renderer; `docs/game/voice-trial/` (12 WAVs, 2.5 MB, `trial.json`, README); speaker indices ef_dora 28,
    em_alex 29, ff_siwis 30, if_sara 35, im_nicola 36, pf_dora 42, pm_alex 43, pm_santa 44, em_santa 53. **`if_sara` is about three times
    louder than the Spanish voices, with peaks near clipping** (for the owner's listening).
  - **V07.** `lesson` on every check item; the battery asks the lessons with `Session <= max(PlaySession, coreSessions)`, `coreSessions: 3` in
    `checks.json`, because the plain rule would have cut the pilot's pre- and post-check to 24 items and the recall to 27; a test pins the
    31. Eight items belong to no lesson (the session-1 free run at the stall and the villager).
  - **Found:**
    - The well's water became the nearest water to the villager, so "¿Dónde está el río?" would have pointed at the well: a labelled place
      made of its word's blocks now resolves only to blocks joined to its point (`NearestOfNounJoinedTo`).
    - The renderer never kept the owner's marks in the speech checklist (no row starts with `| speech/`); fixed.
    - The well's rim is two blocks high, so from eye height it reads as a stone cube and hides its water (the orchestrator's screenshot);
      V10 lowers it.
- **V10: session 4 "El pueblo" (2026-10-30, Opus 5.5 xhigh; gated on `feature/village` at `3f1ce84`).**
  - **Gate:** 5808 passed, 3 skipped, 0 failed, with the models; 0 warnings; format, content, grammar and both licence scans clean. Bundle
    `e9fcbe59…`; the identity golden and the fuzz suite's two stream digests (`FuzzLanguageStreamTests`, generators unchanged) re-recorded.
  - **Built.** The fisher Mar (`fisher-open`, `places-village`; `fisher.point` and `fisher.show` from the slot, `fisher.pay` 2 coins); the
    lesson `el-pueblo` (24 steps, session 4, after `ayer-y-manana`: bridge, bakery, smithy, the signpost, back to the bridge; two reviews);
    `ask_far` (no keyword rule and no subject-first grammar: the core's unrelated-sentence test would resolve "el cielo está muy lejos"),
    `ask_availability`; `state_action` gains `comprar`, `buscar`, `luego`, `después`; the order `fruit-for-mar`; the X06 review steps in
    `personas` and `ayer-y-manana`; 8 check items; the well's rim one block high, a bottle fills there. 16 world words in the marked block of
    `a1.txt`. Every said row heard word for word by the spoken round trip. Estimated 7 to 8 minutes.
  - **Rows.** Said: `donde-esta-la-panaderia`, `donde-esta-la-herreria`, `esta-lejos`, `hay-un-pozo`, `voy-a-la-panaderia`,
    `voy-a-comprar-pan`, `fui-a-la-panaderia`; the close is a `gracias` review and a repeat of `adiós` (no lesson had taught it). Heard: the
    plan's eight, with `hay-tres-casas` for `hay-dos-casas`. The Opus pass moved "a la izquierda de la torre" to "mira la torre. La panadería
    está a la izquierda." (from the bridge the bakery stands in line before the tower) and the lake to the mountain for "lejos".
  - **Found:**
    - Core gaps, filed as core M56: `es lejos` and `¿dónde es la herrería?` give no `ser_estar`; `¿dónde están la herrería?` gives no
      `conj.person`. The sheet's examples use `gender.agree` meanwhile.
    - The fuzz generator knows only `mañana` as a future marker, and `pasado mañana` after a plural noun reads as an adjective: the later
      markers stay out of `state_action` (core M57; phase B's V17 waits on it).
    - `ayer voy a la panadería` is the core's `conj.tense` slip at a `pret` step (S27's design), not a Miss; the Miss with `tenseRetry` is a
      tense the core cannot see (`voy a la panadería` at that step).
    - `LessonDriver` could not answer review steps; fixed. The fisher's stand point carries the label `pescador`, so the walk back points
      at him and the arrow clears on arrival.
- **V11: session 5 "Números y pan" (2026-10-31, Opus 5.5 xhigh; gated on `feature/village` at `6097249`).**
  - **Gate:** 5900 passed, 3 skipped, 0 failed, with the models; 0 warnings; format, content, grammar and both licence scans clean. Bundle
    `c0a9377f…`; the identity golden (26 new lines; 153 entries at `stall-need` moved, its prompt now wider) and the fuzz suite's sentence
    digest (`FuzzLanguageStreamTests`, generators unchanged, the word digest unmoved) re-recorded.
  - **Built.** The baker Luz (`baker-open` and three lesson prompts; `baker.sell_bread` sells bread at 2 coins, `baker.cant_pay` "Necesitas
    más monedas."); `ask_price`; the stall answers `ask_price` ("El pan cuesta dos monedas.") and "¿Hay pan?", its session-1 exchange still
    free (tested); the lesson `numeros-y-pan` (23 steps, session 5, after `el-pueblo`: two reviews, four `do` steps for the wood, the
    delivery, the purchase and the walk to the stall); the order `oven-wood` (5 wood, 4 coins); an optional voice `gain` in the NPC file,
    applied by the renderer (the baker 0.32: voiced RMS −13.7 dBFS and peaks −0.6 in the trial, −23.9 and −9.9 after, against −23.8 for
    `ef_dora` and −23.2 for `em_alex`; no existing WAV re-rendered); 8 check items; `horno`, `luz` in `a1.txt`. Every said row heard word
    for word, `dos` and `cuatro` too. Estimated 6 to 9 minutes.
  - **Rows.** Said: `quiero-dos-panes`, `cuanto-cuesta`, `voy-a-buscar-madera`, `tengo-cuatro-monedas` (for the plan's `tengo-madera`: an
    order is delivered the moment the talk opens, so "Tengo madera" would be false wherever Luz can ask), `compre-dos-panes`, `hay-pan`
    (its home prompt `baker-open`, its step at `stall-need`). Heard: the plan's ten. The Opus pass made the oven line "Necesito cinco
    bloques de madera para el horno. Te pago cuatro monedas. ¿Qué vas a hacer?", `cant_pay` "Necesitas más monedas." (the plan's line was
    true only of two loaves and no coins), and added "¡Hola! ¿Qué vas a hacer hoy?" (a review of session 4's "Voy a comprar pan") and the
    stall's "Sí, hay pan. Es pan de Luz."
  - **Found:**
    - Core gaps: `cuánto costa` is read as the noun `costa` (`gender.agree`, no intent) and `hay pans` gives `vocab.unknown` and no intent
      (filed as core M58); `¿cuánto cuestan el pan?` and `cuánto cuesta los panes` give no `conj.person` (added to core M56). The sheet uses
      `cuánto costan los panes` (`conj.irregular`) and `hay dos pan` (`noun.number`) meanwhile.
    - `--lesson-run` looped forever on a `do` step with no arrow (it closed the panel, then tried to skip, which needs it open); fixed with a
      regression test. A delivery step waited forever if the goods came before the lesson asked: the runner now ends it at once
      (`OrderDelivered`).
    - The coin path leaves 2 coins after session 5 (2 from `fruit-for-mar`, 4 from `oven-wood`, 4 spent on two loaves), so the plan's axe
      order (3 coins) could not be finished: V12 makes it 2 wood and 2 coins. A learner with 4 coins before the oven order buys at the first
      ask and never hears "Necesitas más monedas."; the arrow on `árbol` can point at a canopy the scripted run cannot gather (a learner aims
      at the trunk). No `level` field until V08.
- **V12: session 6 "Gente del pueblo" (2026-10-31, Opus 5.5 xhigh; gated on `feature/village` at `c325b67`).**
  - **Gate:** 6012 passed, 3 skipped, 0 failed, with the models; 0 warnings; format, content, grammar and both licence scans clean. Bundle
    `f606e71e…`; the identity golden and the fuzz suite's two stream digests (`FuzzLanguageStreamTests`, generators unchanged) re-recorded.
  - **Built.** The smith Domingo (`smith-open`, `im_nicola` with gain 0.6: voiced RMS −19.1 dBFS in the trial, −23.0 rendered) and the
    farmer León (`farmer-open`, `pm_santa` at −23.3, no gain; no sale and no order until session 7), their stand points labelled so the walks
    end on arrival; `ask_name`, `ask_activity` (`ask_help`'s `¿qué hago?` is now literal, so `¿qué haces?` reaches `ask_activity`);
    `state_action` gains `hacer`, `trabajar en`, `vivir en` and a closed list of names before the verb (`domingo|luz|mar|león`); the lesson
    `gente-del-pueblo` (28 steps, session 6, after `numeros-y-pan`), which starts at the villager so that `va a hacer un hacha` comes before
    the first talk with the smith (tested for a learner who arrives with the goods); the order `axe-handle` (2 wood and 2 coins, the axe;
    with 1 coin nothing is delivered); 8 check items; 90 WAVs; `herrero`, `granjero`, `hacha`, `león` in `a1.txt`. Every said row heard;
    "Va a hacer un hacha." once heard as "va va a hacer un hacha", still `state_action`, error-free. Estimated 8 to 9.5 minutes.
  - **Rows.** Said: `como-te-llamas`, `que-haces`, `es-el-herrero`, `va-a-hacer-un-hacha`, `vivo-en-el-campamento`,
    `trabaja-en-la-herreria`, `hizo-un-hacha`; the review `necesito-pan` asked by the villager (the bakery is off the route and the axe takes
    the coins). Heard: the plan's nine. Not added: `donde-vive` and `ask_residence` (no room). The Opus pass made the introduction "Mira,
    allí está Domingo. Es el herrero." (he stands across the square), added "Domingo te quiere hacer un hacha.", made Luz's house "Domingo
    vive en una casa cerca de la herrería." (no house stands by the stall; `lado` is A2) and the offer "dos monedas".
  - **Found:**
    - The coin path left 2 coins after session 5, so the axe order is 2 wood and 2 coins, not the plan's 3 coins (village-plan 3.4 updated).
    - Core gaps, filed as core M59: `conj.person` only after a subject pronoun (`Domingo trabajo en la herrería` gives nothing); a reflexive
      clitic not matched to the verb (`cómo te llama`); `va a hace` not flagged (`va a hizo` is); a literal grammar word binds its first
      reading (`hace` as in `hace dos días`). The sheet's examples put a pronoun before the verb; the lesson's person check catches the rest.
    - The fuzz suite's correct-sentence oracle caught `qué tú hace`, generated from an optional pronoun before the verb: the grammars dropped
      it, no oracle changed.
    - At the open prompts a correct sentence of another intent is forced into the prompt's intents: `Domingo hizo un hacha` at `smith-open`
      gives `ask_activity` with `vocab.wrong` expected `''` (the orchestrator's spot check). The lesson asks it at `lesson-made`, where it
      is clean; phase B may let the open prompts take `state_action`.
    - `--lesson-run` looped for ever when the panel was open on another NPC than the step's; fixed with a regression test. A question the
      lesson takes gets no reply from the NPC, so the smith and the farmer say their names in steps of their own. `verb.hablar`'s past
      read "speaked"; fixed. LanguageTool reads `Domingo` as the day: text waivers, and the waiver test accepts a text as well as an id.
- **V09: documents and runs, phase A (2026-10-31, Opus 5.5 xhigh; gated on `feature/village` at `9ec051d`).**
  - **Gate:** 6021 passed, 3 skipped, 0 failed, with the models; 0 warnings; format, content, grammar and both licence scans clean. The bundle
    is unchanged (`f606e71e…`).
  - **The scripted runs against `main` before phase A** (`f3a242f`, all sixteen, captured in the one worktree): every run differs, and every
    difference traces to phase A: six lessons in the closing summary and the review steps V10 added; speech 215/430 → 340/680 files; 1967 →
    1958 trees and a new scene digest (the village's radius 24 widens the flattened ground to 56 blocks, so the hillside behind moves in every
    far view); seven figures (372 → 882 quads); `beacon_reached` and `place_asked` in the session logs; the bakery, smithy, well, orchard and
    the four NPCs in the screenshots; the book's counts (46 → 93 phrases, 19 → 22 nouns, 3 → 6 lessons); the "E to talk" tip now fires on the
    road at the fisher. The playtest run (r10) has the same ticks, the same 31 items and a pixel-identical thank-you screen.
  - **Built.** Two new runs, both deterministic across two captures: `r18-bakery` (session 5, the oven wood, the delivery, "quiero dos
    panes, por favor": two loaves in, four coins out) and `r19-directions` (the fisher asked "¿dónde está la panadería?", the arrow, the walk,
    `beacon_reached`); `scripted-runs.md` at eighteen runs. `README.md` (a short section on the village, every NPC list, `--talk` with the
    seven ids, `--session 4` to `6`, the battery by session 31/39/47/55, 680 speech files, 17 model-gated tests skipped without models),
    `playtest.md` (the pilot stays at 31 items, the 24 village items outside it), the speech checklist's preface (written by the renderer:
    the four provisional voices and how to replace one), the prerender script's header and `content/README.md`.
  - **Found:**
    - r03 showed the farmer's name plate pinned to the window's left edge while he stood out of the picture (the X08 clamp): a plate wholly
      outside the window is now left out (`NpcLabelLayer.Place`, with tests from r03's camera).
    - The renderer kept the owner's mark on a row re-recorded in a new voice: a re-recorded row now loses its mark. Changing an NPC's voice
      re-renders only that NPC's files (checked on a copy: 20 rendered, 660 unchanged); `--force` is not needed.
    - Free talk with the baker credited "quiero dos panes" as a use of `pedir-agua`: none of the four new NPC files has a `focus`. Fixed by
      the follow-up V12b.
    - Name plates show through figures (the farmer's over the shopkeeper's face in r17): a design question for the owner, not a bug.
    - The three-NPC lists left in the `--talk` help, `NpcService`, `GuideSession`, `InputEntry`, `SaveDocument`, `VoxelGame` and both game
      projects' READMEs are brought up to seven; the two "Rosa" comments were already gone.
- **V12b: the village NPCs' focus (2026-10-31, Opus 5.5 xhigh; gated on `feature/village` at `29188ce`; phase A ends here).**
  - **Gate:** 6030 passed, 3 skipped, 0 failed, with the models; 0 warnings; format, content, grammar and both licence scans clean. Bundle
    `7ba32270…`; no golden or digest moved.
  - **Cause.** `GuideSession.AfterTurn` asked for the phrase in play (the NPC's focus through `NpcRoster.FocusAt`); the baker's file had
    none, so the turn fell back to `PhraseIds.For`, whose fixed mapping makes every `request_item` the stall's `pedir-agua`;
    `NpcService.PhraseSkillsOf` used the same mapping, so the core's learner model was told `phrase.pedir-agua` too.
  - **Built.** A `focus` at each village NPC's open prompt (the fisher's two directions rows, the baker's `quiero-dos-panes`,
    `cuanto-cuesta`, `hay-pan`, the smith's and the farmer's `como-te-llamas`, `que-haces`), so their rows get cards that fade like the
    stall's; `PhraseSheet.PhraseFor` credits the phrase in play, else the row the prompt teaches for the intent, else the fixed mapping (kept:
    a request at another NPC is still a transfer of `pedir-agua`); of two rows with one intent, the one naming the place asked; a phrase at
    home with two NPCs is a transfer at neither (`NpcRoster.IsHomeOf`); a validator rule: a prompt that is the home of practised rows names one
    of them in its NPC's focus (true of all seven). `VillageFocusTests` (8 cases, failing before the fix). One game run checked each NPC's
    free talk: the cards, the credit and the reply.
  - **Found:** at the villager, "¿dónde está la panadería?" still credits `donde-esta-el-rio` (from before V12b; the check that would fix it
    changes lesson credit, so it is left for phase B).
- **CI on PR #20 (2026-10-31): the core's M60.** The GitHub runner failed `LintTests.TheBundleLintTakesUnderFiveSeconds` (about 6 s):
  phase A's 47 prompts and 385 examples made the lint's confusion check analyse each example about 33 times. The core's M60
  (`docs/nlp/implementation-plan.md`) analyses each once per winning grammar: 1610 to 398 ms, the output byte-identical. `main` (the CI
  workflow with its grammar jobs) was merged into `feature/village`; the offline and the live grammar checks were replayed locally and pass.
- **V12c: the next session in the game (2026-10-31, Opus 5.5 xhigh; gated on `feature/village` at `4b88599`).**
  - **Why.** The owner played `--seed 42`, lit the fire and asked where to go: a session advanced only on a quit and a `--load`, the
    objective said `Session complete: your fire is lit`, and the book listed six lessons `not started`. The phase A lessons were unreachable
    in practice: `--session 4` raises the session but `el-pueblo` waits for `ayer-y-manana` (`after`), and only `--lesson-run` (an automated
    run) reached them. The scripted runs had never tried the route a learner takes from a new world.
  - **Built.** The closing card's `Enter: sleep until morning and start session 2`, and at the lit fire `E: sleep until morning (session n)`
    once the session's work is done (the objective then says `Session complete: sleep at your fire to start session n`, the beacon on the
    fire; after session 6 `Every lesson so far is done: …`); a `Good morning: session n begins` toast; no sleep in a playtest. The sleep runs
    in place through the save path (`SaveGame.Sleep`: the session's own save restored into it with a load's code, the session plus one,
    `BeginPlaySession`); `SleepTests` proves the save after a sleep equals the save after a save and a load in five states, with no field
    differing. The session log writes `session_end` and `session_start`, and the autosave the ending session. The book's Lessons page:
    `next: talk to <NPC>` (gold) for the lesson the objective names, `open: talk to <NPC>` for another unlocked one, `opens in session n,
    <NPC>`, `after “<title>”`. `--skip-to-session n` (a test aid, from the data): the earlier lessons completed in their sessions, session 1's
    ending, the earlier orders delivered and purchases made (0 coins at 4, 2 at 5, 2 coins and 2 loaves at 6), no word or phrase use
    counted, `skippedTo` in the save; it refuses `--load`, `--session` and `--playtest`. Runs `r20-sleep` and `r21-skip`, deterministic.
  - **Gate:** 6079 passed, 3 skipped; the 18 earlier runs unchanged but for r16's line count (116 to 121). Bundle unchanged.
  - **Review:** the first version offered the sleep at the fire on waking, against the new objective; it is offered once the work is done.
  - **Left:** the F1 controls help does not mention sleeping (its lines are in `content/game-v1`; the next content milestone adds it).
- **V08: the A1/A2 gate (2026-10-31, Opus 5.5 xhigh; gated on `feature/village` at `808e0f0`; phase B's first milestone).**
  - **Built.** `lesson.schema.json` 2: `level` (`a1` | `a2`) required; `LessonBook` refuses another schema or level; `LessonLevels` holds the
    levels, the ceiling (A2) and each level's tenses (`fut` from A2). The six lessons are `a1`. The validator (the host's
    `--validate-content`) checks each lesson's phrases, the lines its steps name (review candidates included) and its prompts' text against the
    lists up to its level, with the core's public `CefrChecker` for the lemmas, and its tenses against its level; a finding names the file,
    the text, its source and the word. `LessonLevelTests` (7 cases).
  - **The gate** (`scripts/merge-gate.sh`, `scripts/content.sh refresh`): lint `--strict --level a2 --fail-on-level --host-pools 'guide.*'`
    fails it; `cefr-check --level a2` fails it on any lemma it lists (the tool only prints, so the scripts read its counts); `cefr-check
    --level a1` is a report; the merge gate also runs `--validate-content`. CI runs neither the lint nor the cefr check (only the tests),
    so `ci.yml` is unchanged. The core's tests that pinned the game's bundle to a clean A1 follow the ceiling (the core's M61).
  - **Deviation, accepted:** a phrase's *variants* are held to the A2 ceiling, not the lesson's level. Held to the level, seven variants of
    `a1` lessons fail (`repetir` in four of `otra-vez`, `quedar` in three); the lint never sees variants, and the core treats answer-set
    aliases the same way. Variants are wordings the game accepts, not wordings it teaches.
  - **Gate:** 6087 passed, 3 skipped (6079 before). Bundle `5826d6de…`; no golden or digest moved; `content/grammar/` unchanged. The new
    content gate was run on `feature/village` after the merge (the merge gate itself ran the old script): lint at A2 0 errors, 0 warnings;
    cefr-check A2 and A1 clean; `--validate-content` clean.
  - **Left:** `madera`, `antorcha`, `botella`, `fuego` and `despacio` sit in the body of `a1.txt` rather than the marked block (their
    reasons are in `PHRASES.md`); the `fut`-at-a-step check is tested from V16.
- **V13: session 7 "¿Cómo es?" (2026-10-31, Opus 5.5 xhigh; gated on `feature/village` at `f5933c8`; phase B's first lesson).**
  - **Gate:** 6194 passed, 3 skipped, 0 failed, with the models (6087 before); 0 warnings; format, the content gate at A2, `--validate-content`,
    grammar and both licence scans clean. Bundle `8f4281c8…`; the identity baseline (18 new lines, 36 `request_item` entries moved) and the
    fuzz suite's sentence digest (`FuzzLanguageStreamTests`, generators unchanged) re-recorded; the end-to-end goldens unmoved; the grammar
    cache rebuilt from nothing equals the committed one.
  - **Built.** The lesson `como-es` (20 steps, session 7, after `gente-del-pueblo`, `a2`: the order's line uses `plantas` and `traes`): León at
    the field, the stall on the way to the river, the river bank, back to the field. `describe` and `state_like` (no confusion with
    `name_object` or `state_want` in the lint); `request_item` takes `[ADJ]?` after the noun. The farmer sells apples at 1 coin
    (`farmer.cant_pay` "Necesitas más monedas. Una manzana cuesta una moneda."); the order `water-for-plants` (1 bottle of water, 3 apples, the
    plan's terms; León keeps the bottle, the stall gives another). The F1 help's sleep line (left by V12c); 8 check items; `FarmLessonTests`,
    `FarmSpokenTests`; the skip tests for session 7. Every said row heard word for word (0.99 to 1.00). Estimated 7 to 9 minutes.
  - **Rows.** Said: `la-manzana-es-roja`, `me-gusta-el-pan` "Sí, me gusta el pan.", `me-gustan-las-manzanas`, `quiero-tres-manzanas-rojas`,
    `voy-a-buscar-agua`, `es-grande` (the shopkeeper's loaf), `fui-al-rio` "Sí, fui al río.", `me-gusto-el-pan` "Sí, me gustó el pan de Luz.".
    Heard: `mis-manzanas-son-rojas`, `como-es-la-manzana`, `te-gusta-el-pan`, `mis-plantas-necesitan-agua`, `el-pan-es-de-luz` "El pan es de
    Luz, de la panadería." (`panadera` is in neither list), `toma-tres-manzanas`, `ayer-me-gusto`, `te-gusto-el-pan`. Dropped: `es-pequeno`.
    The Opus pass added the "Sí," of the yes/no answers, the river in the order's line, a `cant_pay` true of any shortfall and "¡Hablas muy
    bien español!" before the thanks review.
  - **Deviations (accepted):**
    - **An order may name its lesson** (`"lesson": "como-es"`, checked by the validator): while that lesson has not reached its delivery step,
      walking up with the goods delivers nothing. Session 1's bottle is never emptied, so nearly every learner would have handed the water over
      on the first talk, before saying the plan. `oven-wood` and `axe-handle` are unchanged (their runs would move); V14 looks at `oven-wood`.
    - `--skip-to-session 7` paid the axe order's 2 coins out again (2 coins at session 7, not 0): an order's coins are now paid from the bag;
      the skip also carries session 1's bottle of water.
    - The book's `verbs` list only the new forms (`son`, `gusta`, `gustan`, `gustó`): a test requires each form to be declared once, and `es`,
      `voy a buscar` and `fui` belong to earlier lessons.
    - The walk to the river ends at the fisher's bank (`npc.fisher`): the river's point is mid-stream, and the scripted run swam to it and stuck.
  - **The coin path:** 0 coins at session 7 (the data and the skip agree). The apple request comes before the order and gets `cant_pay`; the
    apples come from the order, and a learner with coins can still buy.
  - **Found (core gaps, filed as core M62 and added to M56 and M59):** `la manzana es rojo` and `el pan es grandes` give no error (a predicate
    adjective is not agreed across `ser`); `las manzanas es roja` and `la manzana son roja` give none (the noun subject of `ser`, M59 (a));
    `quiero tres rojas manzanas` is not understood, and with a grammar that matches it gives no `word_order`; `me gusta las manzanas` and `me
    gustan el pan` give no error (the postverbal subject of `gustar`, M56 (b); the lesson's person check catches the first as a Miss). The
    rows use the slips the core grades: `quiero tres manzanas rojos` (`gender.agree`), `ayer me gusta el pan` (`conj.tense` expected `gustó`).
  - **Scripted runs:** all 20 keep their ticks and world digests except r21 (the skip now carries the bottle, which moves its state digest);
    every summary lists a seventh lesson; 373 lines and 746 speech files; the Lessons page of r20 and r21 has 141 lines. `scripted-runs.md`
    is left for V09.
- **The bottlenecks study (2026-10-31, Opus 5.5, read-only; `docs/research/2026-10-31-development-bottlenecks.md`).** Asked for by the owner during phase B. The
  cost of a milestone is its turns times its context (phase A's 11 agents: 954M tokens, 97 % cache reads), not the gate (175 s, every phase A
  gate passed first time); follow-ups found late (V12b, V12c, M60) took 22 % of phase A's agent time; each content edit moves seven artefacts
  by hand. The briefs from V14 on read named sections only, send output to logs, test the learner's route and run the suite once without
  models (CI's case). The owner's answers (D-game-22): V18 (the content loop in one command) next, the core's M56 and M59 before V14, M58
  before V15; the questions on two agents at once, effort, split milestones, CI and listening are left as they are.

- **V18: the content loop in one command (2026-10-31, Opus 5.5 xhigh; gated on `feature/village` at `d986adf`; tooling, from
  `docs/research/2026-10-31-development-bottlenecks.md` P2).** `scripts/content-loop.sh`, a new script rather than a mode of `content.sh`, runs the eight steps
  after a content edit, in order: refresh, grammar, speech, fixtures, build, goldens, digests, tests. It stops at the first failure, names
  the next thing to do and prints a summary of at most 30 lines; each step's log goes to `.claude/content-loop/<stamp>/` (ignored).
  - Options: `--grammar` (LanguageTool start, `--prune`, the rebuilt-from-nothing check, the new findings, the server stopped by a trap),
    `--speech` (the `--check` summary, the render, refresh again), `--record` (off by default: the goldens and the identity baseline, which
    needs `VOCATERRA_RECORD_IDENTITY=1` besides `VOCATERRA_RECORD_GOLDEN=1`; it prints the stream digests to set and edits no C#) and
    `--tier fast|full|none`. The fast tier adds the grammar-sensitive fuzz classes at full depth. **A content brief now carries one line:
    `scripts/content-loop.sh --grammar --speech`, with `--tier full` before the report.**
  - The lock: the script calls `limited.sh` per heavy step and `test.sh` directly. It refuses to start (exit 3) under `limited.sh`, which now
    exports `VOCATERRA_LIMITED=1`, or when it inherits the lock's descriptor. Both cases were tested.
  - The save fixtures are found by the old `bundleHash` (the one at HEAD, the one before step 1, or one an earlier run in the worktree saw,
    kept in `.claude/content-loop/bundle-values`), never by a typed list.
  - Proof 1, a real edit on a throwaway branch (`farmer_thank.3` and a `si-por-favor` variant), every option, `--tier full`: passed in 4 min
    52 s. One new cache entry, the rebuild from nothing equal, 2 of 748 WAVs rendered, both fixtures moved, one new identity entry, the
    end-to-end goldens unmoved, 6194 passed. Its earlier attempts stopped at refresh in 10 to 27 s with the problem on screen.
  - Proof 2, V12's grammar fault (`cómo (tú)? te [VERB:llamar] (tú)?`): failed at the fuzz step after 2 min 51 s (64 s of it `--grammar`),
    with O3 on `cómo tú te llamo tú` and the moved `OldSentenceDigest` printed. The fast tier alone was green, so the fuzz classes in the
    loop's fast tier are what catch it. Before V18 this fault showed only at the full run.
  - Two runs with every option on the unedited branch left `git status` clean. `bash -n` clean; shellcheck is not installed.
  - `content.sh refresh` is unchanged apart from its header and cache-miss message, which name the loop. Docs: `content/README.md` ("The
    edit loop"), `docs/game/README.md` ("Refreshing the cache"), `scripts/README.md`.
  - Tests: 6194 passed and 3 skipped, unchanged (no tests added: it is a script).
  - Proposed, not done: a staleness step in the gate (`scripts/content-loop.sh --tier none` then `git diff --exit-code`, about 30 s; it
    would fail a branch that forgot a step of the loop). It needs the gate script changed, which is mine: deferred until a content milestone
    shows the need. CI: no change. The stream digests stay hand-set until the core's M57 (c).

- **V14: session 8 "Ayer en el pueblo" (2026-10-31, Opus 5.5 xhigh; gated on `feature/village` at `78a37fa`).** The villager's lesson
  of session 8, level `a2`: the preterite in every person (`hizo`, `pescó`, `fuimos`, `comieron`, `compró`) and `tener que` (the new
  intent `state_obligation`), 18 steps, 8 check items, 50 WAVs; about 7 to 9 minutes. No order and no sale: a learner arrives with 0 coins,
  3 apples, 2 loaves and the axe, and leaves with the same.
  - Rows: said `luz-hizo-pan`, `mar-pesco-en-el-rio`, `fuimos-al-rio`, `ellos-comieron-pan`, `tengo-que-ir` and the optional
    `luz-compro-pescado`, with the reviews `fui-al-rio` and `voy-a-comprar-pan`; ten heard rows, the day told in three lines among them.
    What the learner says is true in the world: "Fuimos al río" (session 7's walk ended on Mar's bank) and "Tengo que ir al río" (the next
    step walks there). `state_action` gains `pescar en` + a place and a village name before `comprar`.
  - The Opus pass: "le vendí pescado a Luz"; the plural is "Luz y Mar"; "Mar fue al río y te busca" brings in `fue`; "Por la mañana" keeps
    the `fuimos` model apart from the afternoon. The orchestrator read every new line: natural, neutral (`¿Y Mar y tú? ¿Adónde fueron?`).
  - The level is `a2` by the grammar (the past in every person, `tener que`); every word is A1 with `pescar` added to the world words.
  - The stand-in examples are restored (M56, M59): `es lejos` (with `ask_far` given a keyword rule for `ser` + `lejos`/`cerca`, not a
    grammar: a grammar would make the fuzz suite generate it as correct), `¿dónde están la herrería?`, `me gustan el pan`, `me gusta las
    manzanas`, `¿cuánto cuestan el pan?`, `¿cómo te llama?`, and V13's `la manzana son roja` / `las manzanas es roja`.
  - The four open points: (1) `smith-open` takes `state_action` and `state_want` (`Domingo hizo un hacha` error-free there); the fisher's
    and the villager's prompts do not (the lint gave `busqué madera` to `ask_directions` there). (2) `oven-wood` and `axe-handle` name their
    lessons, as `water-for-plants` did in V13 (Luz thanked the learner before the plan; the axe came before Domingo's offer). (3) At the
    villager, "¿dónde está la panadería?" in free talk credits the fisher's row as a transfer; lesson credit is unchanged. (4) **The
    listener's name as a vocative** (`Vocative.cs`): the NPC's own name, as the first word (or after `hola`) or the last, is taken out
    before the core reads the line, typed, spoken and on the host CLI, unless a word joins it to the sentence (`Luz y Domingo comieron pan`,
    `el pan de Luz`). The core leaves a lone name unchecked since M59; the game, which knows the listener, strips it. `Luz hizo pan` said to
    Luz herself becomes `hizo pan` (harmless for the intent).
  - The recognizer heard all six said rows word for word; `fueron` is only in the open vocabulary and is heard as `fuimos` at the step.
  - Bugs found on the learner's route and fixed: the fisher's answer set accepted statements his prompt rejects; the listen step's options
    overflowed at 150 % text scale.
  - Tests: 6431 passed and 3 skipped before; 6602 passed and 3 skipped after, with models (without models, CI's case: 6567 passed and 38
    skipped). One timed test is at the CI margin: the scene build takes 0.75 s of its 3 s budget here, so CI's runner (about four times slower)
    would reach the budget (the scene is unchanged by V14; a CI failure there is that margin, not V14).
  - Bundle `19635716…`; the identity baseline gained 13 entries, none moved; `OldSentenceDigest` set with the generators unchanged; the
    end-to-end goldens did not move. Scripted runs: ticks and world digests unchanged; the differences are the new lesson's (the eighth
    lesson line, speech files 746 → 796, saves +4.7 KB, the Lessons-page screenshots of r20 and r21).
  - Left: `tener que` has no book entry (`tengo` belongs to the first lesson); the NPCs' "¡Muy bien!" still plays in Luna's voice (older);
    the closing review is skipped when nothing is due.
  - The orchestrator's spot-checks: CI's grammar check holds (the cache rebuilt from nothing equal, by `scripts/content-loop.sh --grammar`);
    on the host, `Luz, quiero pan` buys the bread, `Domingo necesito un hacha` is `state_want`, `¿dónde está la panadería, Mar?` is
    `ask_directions`. Two older gaps seen on the way, open: `hola Luz quiero dos panes` (and `hola quiero dos panes`) resolves only the
    greeting, so the purchase does not happen (one intent per line); `quiero el pan de Luz` at `baker-open` resolves no intent.

- **V16: the simple future in the lesson runner and the book (2026-10-31, Opus 5.5 xhigh; gated on `feature/village` at `71573ba`).**
  - `tense: fut` at a repeat or answer step (`TaughtVerbForm.SimpleFuture`, the schema's enum; no schema version bump, as V10 added
    `review`); the validator refuses a `fut` step in an `a1` lesson.
  - The runner (`OtherFuture`, each case with its own log detail): at a `fut` step the near future (`voy a comprar pan`) and the present
    with a time word (`mañana compro pan`) are credited with the step's `futureNote` (one note for both: it names the form the step
    practises); at an `ir-a` step the simple future (`compraré pan`) is credited with the step's `presentNote` (every shipped one already
    reads "That is right too. Here we practise …"). `compré pan` and `compro pan` at a `fut` step are a miss with the `tenseRetry`;
    `mañana compré pan` stays the core's `conj.tense`.
  - The book's fourth column, `ui.bookColumnFuture` "future": present, past, future (ir a), future, time left to right and the futures in
    the order taught; the counts from the word record. 1280x720 fits four columns; 800x600 at 150 % stacks them (the future reached by
    scrolling). The orchestrator looked at the 1280x720 screenshot: the four columns, `compraré` highlighted.
  - The core reads a simple-future form as `fut`, `ind` (a test): compraré, comprarás, comprará, iré, buscaré, hará, tendré, trabajará,
    pescará all found. No core gap.
  - Time words as phrases (`NearFutureWords.Mark`): `semana que viene`, `próxima semana`, `próximo día`, `mes que viene`, `próximo mes`,
    `año que viene`, `próximo año` (`luego`, `después`, `pasado mañana` were there since V10); `el día que viene` left out (it can be a
    relative clause). The bundle's grammars are untouched: they take the later markers in V17, after the core's M57.
  - Bugs found and fixed: `iba a comprar` was credited as the near future (now a tense miss); the `viene` of `la semana que viene` counted
    as the answer's verb in the person check.
  - Tests: 6602 passed and 3 skipped before; 6642 passed and 3 skipped after, with models (6607 and 38 skipped without). Bundle
    `20976ec4…` (`words.json`), in both save fixtures; goldens and stream digests unmoved; 7 new grammar strings, 0 findings.
  - Timing: three older tests are within four times of their budgets here (the bundle load, median 1676 ms of 3000 ms; the scene build
    and digest, 1275 ms of 3000 ms; the world snapshot, 0.49 s of 1.5 s). None changed in V16; with CI's runner about four times slower
    they are the likely CI failures if any appear (open point).
  - Seen by the orchestrator, not a V16 bug: the book counts a use "correct" only when the line was communicated at its prompt, so a
    correct sentence said where its intent is not accepted shows "0 correct".
  - The orchestrator's spot-check: CI's grammar check holds (the cache rebuilt from nothing equal, 7 new strings, 0 findings).

- **V17: session 9 "Mañana en el pueblo" (2026-10-31, Opus 5.5 xhigh; gated on `feature/village` at `cc155d4`).** The baker's lesson
  of session 9, level `a2`, 14 steps, 8 check items: the simple future as the second way to say tomorrow, against `ir a` (an explain step:
  a plan already made, `voy a comprar pan`; a promise or a prediction, `mañana compraré pan`). Luz at the bakery, Domingo at the smithy, Mar
  at the river. The first lesson with `fut` steps: V16's machinery lacked nothing.
  - Rows: said `manana-comprare-pan`, `buscare-madera`, `ire-al-rio`, `luz-hara-pan` (all `tense: fut`); heard `que-haras-manana`,
    `tendre-pan`, `luz-necesita-madera`, `mar-pescara`, `el-dia-de-manana`, `que-hara-luz`; reviews `compre-dos-panes`, then
    `luz-compro-pescado` or `voy-a-comprar-pan`. **Mar tells the day of tomorrow**, of himself in the first person (`Y yo pescaré`), which
    keeps Luz's `hará` and Domingo's `trabajará` in the third person the learner retells; `pescará` is heard from Domingo.
  - The Opus pass: Luz's "¡Qué bien!" and "fresco", Mar's "Y yo pescaré", Domingo's "toda la tarde"; no row dropped. The orchestrator read
    every new line and its English: natural and neutral.
  - M57's later markers are in every `state_action` grammar, at both ends; `buscar`'s item takes `para` + a village name. `baker-open`
    takes plans (`state_action`); `fisher-open` does not (the lint's confusion check gives `buscaré madera` and `busqué madera` to
    `ask_directions` there).
  - The book's future column: `compraré`, `comprarás`, `comprará`, `iré`, `buscaré` taught; `hará`, `tendré`, `trabajará`, `pescará` heard.
  - **No order in session 9:** the plan's four orders are done, and a fifth order to Luz would replay her one thanks-and-reward line and pay
    her 4 coins again. The coin path is unchanged: a learner arrives with 0 coins, 3 apples, 2 loaves and the axe, and leaves with the same.
  - Core tries: every grade right, no new core gap. An older limit seen again: a word outside a constrained prompt's answer set gets
    `vocab.wrong` (`usted comprará pan` at a lesson step). The recognizer heard all four said rows word for word, at 1.00.
  - Tests: 6659 passed and 3 skipped before; 6771 passed and 3 skipped after, with models. Bundle `8f27dffd…`; the identity baseline
    gained 6 entries and 66 moved (the analyze stage only, all `state_action` sentences, from the grammars' markers), in a commit of their
    own (`f59e49e`, whose message wrongly says "and the ones after it"); no end-to-end golden and no stream digest moved. M57's loose ends
    are done: the content loop's step 7 and its documents say the stream digests run on frozen inputs.
  - The agent was stopped early for a session handover. Carried into V15's brief: the scripted runs after the change, the learner's route
    from session 8, the timed lesson run and its minutes, and CI's case without models.
  - The orchestrator's spot-checks: on the host at `baker-open`, `mañana compraré pan`, `Luz, mañana compraré pan` and `voy a comprar pan`
    are `state_action`, communicated, error-free; CI's grammar check holds (`scripts/content-loop.sh --grammar --tier none`: the cache
    rebuilt from nothing equal, 0 new findings); `--skip-to-session 9` shows session 9's objective and the beacon on Luz, with 3 apples, the
    axe and 2 loaves in the hotbar, and `--skip-to-session 9 --lesson-run manana-en-el-pueblo` completes 14 of 14 units (both reviews
    skipped: a skipped start has nothing due).

- **V15: session 10 "El mercado" (2026-11-01, Opus 5.5 xhigh; gated on `feature/village` at `d082f05`, V15b at `557db47`).** The last
  lesson of phase B and of the series, level `a2`, 22 steps, 8 check items: the numbers 11 to 20, the total of a table, comparing
  prices, `poder`, selling, and the market told in the three times. Luz counts to twenty and buys the learner's wood; León's table leads to
  the total, the comparison, paying with fruit and the future; three apples are bought with the wood's coins; the stall hears the report.
  - Rows: said `tienes-veinte-panes`, `quiero-vender-madera`, `vendo-madera`, `cuanto-es-todo`, `es-muy-caro`, `mas-barato-que`,
    `puedo-pagar-con-fruta`, `manana-vendere-fruta` (`tense: fut`), `compre-tres-manzanas`; 11 heard rows; reviews of sessions 9 and 8 at
    the start, 5 and 7 at the end. Four stand-in error examples restored (M58: `cuanto-cuesta`, `hay-pan`; M62: `la-manzana-es-roja`,
    `es-grande`). The prenominal `[ADJ]?` in `request_item` was tried and left out: it moved 43 identity entries and the core's documented
    `quiero una azul espada`.
  - The Opus pass: "Solo compro" (was "Yo compro"), León's total "Quince manzanas, quince monedas." (so "todo" is his table), his answer on
    paying also fits a question about coins, `para ti` dropped. The orchestrator read every new line and its English: natural and neutral,
    apart from Luz's `Te pago una moneda por cada madera` (a mass noun counted), now `… por cada bloque de madera`, as the bundle counts
    wood elsewhere (`9f7a78d`).
  - **`offer_item` and `buy`:** the grammar takes the literal `vendo` and `quiero vender` (only the present sells; `vendí` and `venderé`
    stay `state_action`). Luz and Domingo buy wood, León fruit, one coin each, the number said or else all in the bag; with none, "No
    tienes madera."; another item, "No, gracias. Solo compro madera." No cap: the goods are the cap, and nothing sold can be bought back.
    The lint's confusion check: no overlap with `state_want` or `state_have`, one waived with `state_action`. Bug found and fixed: any
    request at the bakery sold bread (`quiero madera` took coins for bread); Luz now answers "Solo vendo pan.", León "Solo vendo
    manzanas." The host's `--transcript` mode has no bag, so it prints the success line whatever the item; the game decides (`SellingTests`).
  - **The coin path:** 0 coins in; three blocks of wood sold for 3 coins; three apples bought; 0 coins out. A learner with 0 coins finishes from `--skip-to-session 10` (tested). **No order:** the sale and the purchase are the session's
    exchange. `¿Puedo pagar con fruta?`: no barter, but León buys fruit, so fruit pays in two steps. `¿Cuánto es todo?` sums León's table.
  - **One intent per line** (V14's finding): `Greeting.cs` answers a line that opens with a greeting by its rest when the whole line is a
    greeting or not understood and the rest is another intent the prompt takes (the greeting credits nothing); the recognizer's leading
    `[unk]` is passed over, which had also hidden the vocative. `hola, quiero dos panes`, `hola Luz quiero dos panes` and `quiero el pan
    de Luz` (`request_item` takes `de` + a village name) buy bread, typed, spoken and on the host.
    V15b widened it: the rest also answers when the whole line is the rest's own intent with the same errors plus some on the greeting's
    words, so a greeting's words are never flagged and an error after it stays (`buenas tardes, ¿cuánto cuesta el pan?` clean; `buenos
    días, ¿cuánto cuestan el pan?` only `conj.person`).
  - The Lessons page says the series is done once all ten lessons are: "Every lesson of the village is done, all 10: the series ends here."
  - Core tries (not patched; core gaps for the owner's list): `¿puede usted vender manzanas?` gives a wrong `conj.person` (expects
    `vende`); `puedo pago con fruta` and `quiero vendo madera` give no error; `¿cuánto cuestan todo?` gives no `conj.person`; `ti` is not in
    the lexicon (`vocab.unknown`); `¿cuánto son todo?` is communicated with intent `unknown`.
  - Recognizer: every said row heard word for word at 0.91 to 1.00, with `venderé`, `puedo`, `barata` and 11 to 20 (`dieciséis` written
    without its accent). Timing: session 9 about 6 to 7 minutes, session 10 about 9 to 10 (the top of the target).
  - Tests: 7031 passed and 3 skipped before (7034 after M62b); 7218 passed and 3 skipped after, with models; without models (CI's case)
    7175 passed and 43 skipped. Timed tests alone: the scene build 0.65 of 3 s, the bundle load 0.49 of 3 s; V16 measured about 1.3 and
    1.7 s under the full suite's load, within four times of the budget. Bundle `e39e1a42…`; no end-to-end golden moved; the identity
    baseline gained 24 entries and 18 moved (the analyze stage, `de` + a village name in `request_item`), in commits of their own.
  - Scripted runs: ticks and world digests unchanged; the tenth lesson, speech files 824 to 902, saves 5.9 KB larger, the Lessons-page
    shots and Luz's `vendo-madera` card. Nothing wrong in V17's "before". The learner's route: session 9 played, slept into session 10,
    the objective and beacon on Luz, the lesson done, free talk answered right (a scripted run cannot chop a tree, so that step was skipped).
  - Left: the session skip buys one apple, not three, for the market (no step of it is a request); no session follows 10, so it is never
    used.
  - The gate: the first run failed on an O3 fuzz case older than V15 (seed `aeG5w1c3Hvh2`, fixed in the core as M62b) and on the import
    order of `MarketSpokenTests.cs` (`e269e7b`; format had been run before the last edit). Only three commits on the branch at the first
    report, against "commit often".
  - The orchestrator's spot-checks: on the host, the brief's demos as stated (`hola, quiero dos panes` and `hola Luz quiero dos panes`
    buy bread; `vendo madera` and `quiero vender madera` are `offer_item`; `¿cuánto costa el pan?` `ask_price` with `conj.irregular`;
    `¿cuánto es todo?` and `¿puedo pagar con fruta?` León's answers; `mañana compraré pan` clean; `quiero agua, por favor` at `stall-need`
    `request_item`); `--skip-to-session 10 --lesson-run el-mercado` completes (18 done, 4 skipped: two reviews with nothing due, two walks);
    the Lessons page says the series is done. Two gaps went to V15b (`55f3630`, gated at `557db47`, 7258 passed and 3
    skipped; bundle `36b2b8ad…`; no golden or identity entry moved): a two-word greeting before a question was flagged (`buenas tardes,
    ¿cuánto cuesta el pan?` gave `vocab.wrong` on `buenas`), and wood counted in blocks did not sell (`vendo tres bloques de madera`,
    `quiero vender tres bloques de madera` not understood, though Luz says `por cada bloque de madera`): `offer_item` takes `bloque(s) de`
    and `buy` reads the number (`un` is one). After V15b the host gives each line as above; CI's grammar check holds (`scripts/content-loop.sh --grammar --tier none`: the cache rebuilt from nothing equal, 0 new findings).
- **V09 again: documents and runs, phase B (2026-11-01, Opus 5.5 xhigh; gated on `feature/village` at `da8dba6`; phase B ends here).**
  - **Gate:** 7259 passed, 3 skipped, 0 failed, with the models (7258 before); 0 warnings; format, the content gate at A2, `--validate-content`, grammar and the licence scan clean. The bundle is unchanged (`36b2b8ad…`).
  - **The scripted runs against the end of phase A** (`ccf8187`, phase A with V12b and V12c, against `fa22116`, after V15b; all twenty runs, captured in
    the one worktree; `main`'s merge `0cf674b` is older than `ccf8187`, so none of it is in the difference): every difference traces to phase B. Every
    run's closing summary lists ten lessons; speech 340/680 → 451/902 files; every save 18.4 KB larger (155 phrase records, not 93, ten lessons, the
    order `water-for-plants`); the Lessons pages of r20 and r21 at 192 lines (121); r21 carries session 1's bottle of water (V13), which moves its state
    digest; r18 shows Luz's fourth card, `Vendo madera.` (V15). r10 asks the same 31 items; one turn landed a tick earlier (the order-only rule). Ticks,
    world digests, the scene and every other picture are unchanged. No unexplained difference.
  - **Built.** Two new runs: `r22-market` (`--skip-to-session 10`, four trunks cut, `vendo madera` to Domingo: four wood out, four coins in; two loaves
    bought from Luz; `¿cuánto es todo?` to León) and `r23-future` (session 9's lesson, then `mañana compraré pan` to Luz in free talk, then the book's
    Verbs page on `comprar`). Both are order-only runs (they take turns), added to `compare-runs.py`'s list; captured twice, r22 matched byte for byte
    and r23 but for the turn's latency drawn in `reply.png`. `scripted-runs.md` at 22 runs. `README.md`: the village section for sessions 4 to 10 (a
    line a lesson, the futures, the market), the counts measured (10 lessons, 229 steps, 155 phrase rows, 22 nouns, 20 verbs, 29 intents, 70 prompts,
    451 lines in 902 speech files, 87 check items; the battery 31/31/31/39/47/55/63/71/79/87), the learner's route, the open points and CI's case.
    `playtest.md`: the pilot's battery stays at 31 items; a new section on what a playtest of sessions 7 to 10 needs. The speech checklist's preface,
    written by the renderer, now counts what is left to hear (902 files unmarked; phase B added 222). `architecture.md`'s CI line, the `--session` help.
  - **The learner's route through phase B** (from `--skip-to-session 7`, each lesson, free talk with its NPCs, a sleep at the fire): every session
    opens on its lesson (the objective names it and its NPC, the beacon, the morning toast); the four lessons ran to their ends; after session 10 the
    objective and the Lessons page say the series is done.
  - **Found and fixed:** a skipped game left the shopkeeper as if never met: in session 7 his key words stood alone again and his next visit handed
    over a second bottle as a fail-forward, logging a use of `pedir-agua` the learner never made. The skip now counts his bottle as given and his key
    words as taught (`SessionSkip`, `NpcService.SkipShopkeeperBottle`; `SessionSkipTests`, failing before the fix).
  - **Found, not fixed (for the owner's review; the whole list is in `docs/game/village-plan.md` section 5, "Open points after the follow-ups" (it replaced "Open points after phase B")):**
    - In free talk, 9 of phase B's 27 said lines get "No te entendí" from the NPC who taught them: the open prompts of the villager, the fisher and
      the stall take no statements, and Domingo's no `tener que` (`Es grande.`, `Compré tres manzanas.`, `Tengo que ir al río.`, `Luz hará pan.`, …).
    - Free talk credits only an NPC's `focus` rows, so a phase-B line said again in free talk counts no phrase use (r23).
    - An NPC shows its focus cards from the first talk, before their lesson: Luz's `Vendo madera.` in session 5 (r18), León's A2 lines from his first
      talk.
    - A lesson's line at an open prompt whose answer set lacks it is a slip (`buscaré madera para Luz` to Domingo: a small mistake, and the recast
      writes `luz` lower case).
    - The stall answers any request with the bottle (older, S6). The Lessons page opens at its top, far from `next:` (192 lines).
  - **CI's case** (`VOCATERRA_MODELS_DIR` an empty folder): 7218 passed, 44 skipped. Timed tests under the full suite's load against their budgets:
    the bundle load 1438 of 3000 ms and the world snapshot 0.50 of 1.5 s (over at four times: the likely CI failures), the scene build and digest 708
    of 3000 ms (at the edge), the lint 0.78 of 5 s, the chunk digest and a turn well under.
  - **The orchestrator's checks:** r22 and r23 run again after the gate as described (r22's bag after: the book, three apples, the axe, four loaves, no coin, no wood; `smith.buy_wood`, `baker.sell_bread`, León's total; r23's Verbs page on `comprar` with `compraré` said twice, both correct); the diff in scope (`scripts/compare-runs.py` is outside the brief's list: the two runs' names only).
- **V19: the game findings of the review of `feature/village` (2026-11-01, Opus 5.5 xhigh; gated on `feature/village` at `716d469`).** The
  owner asked on 2026-11-01 for every finding of the review (`docs/qa-reports/2026-11-01-review-feature-village.md`) to be fixed before PR #20's
  merge: the core's as M64 (`23af5cd`), the game's here.
  - **R4, the listener's name after any greeting.** `Vocative` reads the name after any of `Greeting`'s openings (`Greeting.OpeningLength`;
    its own list of `hola` alone is gone). A new `LineReading` holds the sequence: the name out, the line read, a greeting's rest answered.
    `NpcService`'s typed and spoken paths and the host's `ConversationSession` all call it, so the three copies cannot drift again.
    - Through the host, `buenos días luz quiero pan` was "No te entendí", `buenos días domingo vendo madera` a statement about Sunday with
      no sale, and `buen día león quiero tres manzanas` had `vocab.wrong`. Each is now its intent at correctness 1.
    - `buenos días luz` still greets.
  - **R7 to R10, the market's misses.** `NpcEffect.ExchangeMiss` is the one rule for a purchase or a sale. The NPC's worker checks it on its
    copy of the bag, and the session checks it again on the bag itself.
    - R7: a catalogue noun with no item is another thing (`quiero casa` at the bakery, `vendo piedra` to Domingo: `wont_sell` or
      `wont_buy`).
    - R8: the new miss `no_room`. `Inventory.CanExchange` counts the slot the payment frees. The NPC says «Tu bolsa está llena.»
    - R9: no cap on a purchase or a sale. The number said, bounded by the coins, the goods held and the room, as the V15 entry said.
      `MaxQuantity` stays only for a `give_item` or `take_item` counted from `qty` (no NPC has one). Twelve digits overflow nothing.
    - R10: zero moves nothing, with `none_asked` (the baker's `baker_hay`, León's `how_many`) or `none_offered` (new: «¿Cuántos bloques de
      madera me vendes?», «¿Cuántas frutas me vendes?»).
    - `ExchangeMissTests`, 13 tests.
    - In the game, from session-10 saves: 9 wood and `vendo ocho bloques de madera` sell 8; 15 coins and `quiero quince manzanas` buy 15
      apples; with a full bag nothing changes and the NPC says why.
  - **R1's game side.** Nine aliases in `lesson-we-went` (`mar`, `tú` or `luz y yo fuimos al río`, with `ayer` before or after). With M64
    they score 1, where they gave `vocab.wrong` at 0.4. The other `1pl` steps' questions invite no subject.
  - **Found and fixed:** `vendo dos frutas` at `farmer-open` got `vocab.wrong` (0.4). León's set takes counted fruit now.
  - **The review's risks.** `SessionSkip`'s purchase throws `InvalidOperationException`, naming the session and the item, where it would
    have left a later order short. `docs/game/README.md`, `content/README.md`, the host-events schema and `host-events.json` describe
    selling and buying as they now work.
  - **Content:** six new lines (`no_room` for the baker, the farmer and the smith; `none_offered` for the three buyers), with an Opus
    pass (`me` added to the questions) and the orchestrator's read. 12 WAVs in the NPCs' provisional voices. Speech is now 457 lines in
    914 files. Grammar: 0 new findings, and the cache rebuilt from nothing is equal. Bundle `0f8faea0…`; the identity baseline took
    the six lines.
  - **Scripted runs:** the only difference is the speech count. Ticks, digests, pictures, `r18-bakery` and `r22-market` are unchanged.
  - **Tests:** 7364 passed and 3 skipped before; 7420 and 3 after, with models. Without models (CI's case), 7378 passed and 45 skipped.
    The bundle load was 1248 of 3000 ms and the scene build 739 of 3000 ms, both within four times of their budgets. The gate passed at
    the first run.
  - **The orchestrator's checks:**
    - the four greetings with names through the host;
    - `lesson-we-went`'s subject answers at correctness 1;
    - `vendo dos frutas`;
    - the host demos (`quiero agua, por favor` at `stall-need`, `mañana compraré pan`);
    - the six new lines read.
  - **Left:** a zero request (`quiero cero panes`) still credits the phrase `quiero-dos-panes` as said, though nothing changes hands.
- **QA pass, `language` (2026-11-01, the follow-ups' first item; the QA agent, Haiku; `docs/qa-reports/2026-11-01-language.md`).** On
  `feature/village-followups` at `9dfa08b`, sessions 7 to 10 in depth: 384 CLI lines, 39 host lines, game runs. The orchestrator
  reproduced every bug below through the host (the game's path) and the CLI.
  - **Praise for the lesson's own error** (major, core):
    - `las manzanas cuesta una moneda` at `farmer-open` gets "¡Muy bien!" (QA-3; to M66);
    - `yo gusto el pan` and `me gusto el pan` get correctness 1 (QA-4; to M70);
    - the present against a past question, at a constrained lesson prompt: `Luz compra pescado` at `lesson-luz-bought` (answer `luz compró
      pescado`), `compro tres manzanas` at `lesson-market`, and `sí fue al río` at `lesson-water-went` all get correctness 1. The answer
      match takes the near answer and reports nothing (QA-5, QA-10; to M67).
  - **Broken recasts** (major, core): an unknown word is blamed on its neighbour, so `compré three manzanas` is recast «compré compré
    manzanas» and `manana iré al río` «al iré al río» (QA-6; to M70).
  - **Not understood:** `me llamo ana` (a learner's own name) gets "No te entendí" (QA-2; to M70). `cuanto son todo?` is read as
    `describe` and praised (QA-8; to V20 (e)).
  - **A preterite spelling slip:** `buscé` (for `busqué`) is an unknown word (core, to M70). The NPC then both praises a recast and
    says "No te entendí" in the same turn (game, to V21) (QA-9).
  - **For the owner:** `manana` for `mañana` is `vocab.unknown` at 0.6 with a correct recast, and `compre` is taken as the preterite
    `compré` (QA-7 and an observation): how strict typed input is about `ñ` and the accent of a tense.
  - **Not a bug:** the CLI and the host differ on lines with a greeting or the listener's name, because the host takes them out first
    (V19). Briefs measure on the host (`scripts/try-lines.sh --host`, M65); the handover's 9.3 counts were taken on the CLI.
  - **Corrected at M67 (2026-11-01):** QA-language-5 and 10 (the present against a past question) do not reach a learner. At a lesson
    step the game makes `Luz compra pescado` a miss with a tense retry and the hint «compró» (V14,
    `YesterdayLessonTests.ThePresentAtAPastStepIsAMissWithATenseRetry`); the host's `--transcript` path runs no lesson. The core change
    that would turn it into a slip is parked (`M67b-answer-verb-forms`).
- **QA pass, `progression` (2026-11-01; the QA agent, Haiku; `docs/qa-reports/2026-11-01-progression.md`).** On `9dfa08b`, 20 game
  runs: the route of sessions 4 to 10 from `--skip-to-session`, the coin path (as planned: 0 coins at session 7), the orders, the
  market, saves.
  - **A load moves the session on** (major, game; QA-progression-1 and 2; the orchestrator confirmed it in `SaveGame.Restore`, which
    sets the session to the saved one plus one, and in the runs' logs). A save in session 5 loads as session 6 while the objective still
    names session 5, and the open order `oven-wood` is refused ("Necesitas más monedas"). A save made just after a sleep into session 9
    loads as session 10 and session 9 is skipped. S10's rule ("a load is the next session") predates V12c's sessions by sleep. The
    owner's answer: two counters (D-game-22, 2026-11-01). To V25, first in the game's queue.
  - **Contradictory exchange lines** (minor, game; QA-progression-3 and 4): the farmer refuses apples with «No, gracias. Solo compro
    fruta.» (apples are fruit); the baker refuses bread with «Necesitas más monedas.» while the order that pays is held back by the
    lesson; `¿Cuánto es todo?` gets «Quince manzanas, quince monedas.» whatever the bag holds. To V21.
  - **Small:** the do step `n4-wood` waits for a pickup when the bag already holds the wood; the objective still says "talk to the baker"
    after the talk started the lesson; no line that the course is over after session 10's sleep; a farewell logged under the villager's
    prompt id; a partly black first frame before any tick. To V23.
  - **Checked and right:** the coin path, the market's wood sale, a mid-lesson save and load (the lesson resumes at its step).
- **V22: name plates (2026-11-01, Sonnet 5.5; gated on `feature/village-followups` at `1ff8a5a`; the owner's design, D-game-22).** Run
  beside the QA passes and M65, in its own worktree with a game-only build (the owner's "do whatever can be done in parallel").
  - `NpcLabelLayer` was a 2D layer that drew every figure's plate on top. A pure `NamePlates.Select` (`UI/Hud/NamePlates.cs`) now
    returns the faced NPC (`FigureDirector.Aimed`, the one E talks to, so no second range) and the beacon's NPC, one plate when they
    are the same; the conversation's speaker is skipped as before.
  - The plates are drawn inside `VoxelRenderer.DrawWorld` (a new `overlay` callback) after the figures, water and beacon and before the
    first-person arm clears depth, with `DepthStencilState.DepthRead`. A plate's depth is its point moved 0.7 block towards the eye, so
    it is not hidden by its own figure's head. The old grid line-of-sight test is gone.
  - Tests: 8 cases in `NamePlatesTests`. 7420 passed and 3 skipped before; 7428 and 3 after, with models. The gate passed at the
    first run.
  - The orchestrator looked at the shots (`/tmp/claude-1000/v22/`): at session 8's morning the unfaced baker's plate is gone; with the
    farmer faced and the villager (the beacon's) behind him, the villager's plate is hidden by the farmer's hat, a thin strip of it
    still showing under the farmer's plate (cosmetic); `--third-person` and the largest text scale are right.
  - **Left:** no shot of the beacon's NPC behind a building (the same depth test covers it).
- **V25: a load keeps its day (2026-11-01, Sonnet 5.5; gated on `feature/village-followups` at `1aeaf53`; the owner's two counters,
  D-game-22).** Fixes QA-progression-1 and 2. Run beside M66 in its own worktree with a game-only build.
  - `PlaySession` is gone. `GuideSession.Day` (the village session) drives lessons, objectives, orders, unlocks, checks, the HUD and
    book's "Session n", `--skip-to-session`, the crash report, the learner history and the session summary; a load restores it as
    saved, and only a sleep or the two test aids move it. `GuideSession.Sitting` wraps `Words.Session` and `PhraseProgress.Session`: the
    spacing rule, `ReviewDeck` (offered from sitting 2) and the session log (its folder, `session_start`'s `session`, and a new `day`
    field). A launch, a load, a sleep, `--skip-to-session` and `--session` each begin a sitting; a sleep moves both, so everything that
    counted as a different play session still does.
  - A load keeps the day, sets the sitting to the saved one plus one and settles the rest (`SettleRest`, not `BeginPlaySession`).
    `SaveDocument` gets a trailing `int? Sitting` (format version 5 kept); an older save uses its `Session` as the sitting and loads on
    its saved day. The `save-v1` and `save-v4` fixtures load unchanged.
  - Tests: `Save/LoadKeepsDayTests.cs` (day 5 loads as day 5 and the oven order is delivered; a save after a sleep keeps its new day; a
    load moves only the sitting; a use in a new sitting passes the spacing gap; an older save loads on its day); about 20 tests that
    asserted "a load is the next session" now expect the saved day. 7432 passed and 3 skipped before; 7437 and 3 after, with models;
    the gate passed at the first run.
  - The orchestrator's check: the QA's run (`--skip-to-session 8 --sleep`, save, load) now prints "saved in session 9, now session 9,
    sitting 10" (it was 10) and the HUD reads Session 9.
  - **Left:** the game run of QA-1 does not deliver the order after the load, because the lesson still holds it until its `n4-back`
    step (V14's design; QA-progression-4, V21); the unit test proves the delivery. `WorldFactsTests` ran out of memory once under the
    6 GB cap and passed on a rerun: the unbounded flood of `NearestOfNounJoinedTo` (V23), which moves to the front of V23.
- **V23a: the small game items that touch no content (2026-11-01, Sonnet 5.5; merged at `e02b5df`, gated with the core's M71 and M66
  at `53f63e6`).** Split from V23 to run beside M66.
  - `NearestOfNounJoinedTo` bounded at 4 096 visited blocks (a 400 by 400 lake: 798 401 block reads before, under 25 000 after; the
    shipped scene's results unchanged). `KeyWordsTaught` is `KeyWordsTaughtBy`, a set of NPC ids; an older save's `true` loads as
    `{shopkeeper}`. `SessionSkip.Purchases` gives the market's three apples (the bag already held three). The host's `--bag
    <item:count,…>` (without it, the host is unchanged). A toast when the closing review has nothing due; the objective names a lesson
    under way; the sleep prompt and closing card after the last lesson say the lessons are done for now (locale lines
    `ui.reviewNothingDue`, `ui.lessonUnderwayHud`, `ui.sleepPromptDone`, `ui.sleepCardDone`). A `do` step waiting on a pickup the bag
    already holds is done at once (also `first-words` `u4-pick`, `el-mercado` `k3-wood`, `gente-del-pueblo` `g4-wood`). The first
    screenshot waits for two whole frames.
  - Left as it is: the farewell logged under `villager-open` is accurate (the lesson's repeat step evaluates the phrase at its own
    prompt).
  - The agent ran `scripts/limited.sh scripts/test.sh …`, which deadlocked on the lock for 21 minutes (M66 queued behind it); it cleared
    its own processes. The rule is in every brief now, and M67 (c) makes `limited.sh` re-entrant.
  - Its first gate failed on one O3 test only, "the catalog grammar … was never realised", twice in a row with no core change: a
    flake of the fuzz suite, fixed by the core's M71 (§5b), with which it was gated. 7437 passed before; 7603 passed and 3 skipped
    after V23a, M71 and M66, with models. The orchestrator's checks: `--bag coin:0` gives «Necesitas más monedas.».
- **V20: free talk, the NPCs understand what they teach (2026-11-02, Opus 5.5 xhigh; gated on `feature/village-followups` at `92c2763`).**
  Run beside M67 in the `village` worktree; M67 merged in before its final checks (`d9865cf`, the conflicts resolved by the content loop
  with `--record`). Bundle `91e6e967…`.
  - **(a)** Open prompts take the intents of their NPC's lessons: the villager `state_action` and `name_object`; the stall `state_have`,
    `state_action`, `describe`; the fisher `state_action`; Luz `state_have`; Domingo `state_obligation`; Luna and her review prompt
    `state_want`, `state_have`. Each constrained open prompt's answer set holds its lessons' rows (graded 1, heard when spoken). Lint
    clashes fixed: wants stay requests at the stall and the villager; the two `buscar` grammars are told apart by the article (directions
    name a place with one), so `buscaré madera` at the fisher is `state_action`. `TaughtInFreeTalkTests` runs all 75 said steps through
    their NPC's open prompt, and every focus row. **All 27 of phase B's practised sentences are understood** by the NPC who taught them
    (9 got "No te entendí" before); replies «¡Qué bien!», «Sí, es verdad.», «Está bien.» in ten per-NPC pools, each in its NPC's voice
    (20 WAVs; the Opus pass dropped «Ah, ¿sí?»: `ah` is in no CEFR list).
  - **(b)** Free talk credits any phrase of a lesson begun or done, at any NPC, and the learner model gets it too. **(c)** A focus card
    shows once its lesson is unlocked or done. **(d)** Names in correction lines take the roster's spelling unless a determiner precedes
    them (`para Luz`, `la luz`), in the game and the host. **(e)** `¿cuánto son todo?` and `cuanto son todo?` are `ask_total`, through a
    keyword rule (`cuánto` with `todo`), not `son` in the grammar: that made O3 generate `cuánto son todo esto` as correct, which the
    core rightly flags; `describe` takes a noun subject only after an article or a name. **(f)** One line in `docs/game/README.md`.
  - **Scripted runs:** 467 lines and 934 speech files (457 and 914 before); r09's replays show the new pools; r18 and r19 show no cards
    (their sessions do not unlock the cards' lessons: in r18 that removes Luz's `Vendo madera.`, (c)'s bug); r23 credits
    `manana-comprare-pan` and answers «¡Qué bien!».
  - **Tests:** 7612 passed and 3 skipped before; 7682 and 3 after, with models (7640 and 45 without). The bundle load 1185 ms and the
    scene build 651 to 675 ms of 3000, within four times of their budgets. The identity baseline took 10 entries and moved 417 (333 at
    the merge); the goldens and stream digests did not move. `scripts/try-lines.sh --check` differs in 38 lines, all at the prompts V20
    opened and now understood (the baselines are re-recorded by M70). The gate passed at the first run.
  - **The orchestrator's checks** through the host: `Luz compró pescado.` at `fisher-open`, `Tengo que ir al río.` at `smith-open`,
    `Fuimos al río.` at `villager-open`, `cuanto son todo?` at `farmer-open` all understood; the stall demo unchanged. The new Spanish read.
  - **Left, to V21:** the villager hands over bread for any request (as the stall its bottle); `quién`/`qué`/`cómo es grande` read as
    `describe` at León's and the stall's prompts; `como es la manzana` and `ellas adónde van` read as `state_action` through keyword rules.
    **To the core (M69):** `cuanto son todo?` is understood but `son` is not corrected and the recast keeps «cuanto» (the unaccented
    question word). Replies match on intent only, so Luz says «¡Qué bien!» to `Tienes veinte panes.` (accepted).
- **N01, the needs model (2026-11-05, Sonnet 5.5; gated on `feature/adaptive_design` at `3a63832e`, 9158 passed, 3 skipped).**
  The five needs in `Vocaterra.Game.Core/Needs/` (`PlayerNeeds`, stages and effects computed, never stored), their data in `game/needs.json`
  with a schema and `NeedsValidator`, stepped once per world tick only in active play (`PlayPauses == None`), `need_stage` raised; seven need
  events registered; save version 6 with an optional `systems.needs` record kept out of the digest; `--needs on|off` and `--set-need`;
  every scripted run takes `--needs off` (r01, r09, r20 and r21 differ only in the save version and size). Values chosen under D-game-23's
  answers: gathering 0.0278 stamina a gather tick (a minute by hand is about 10 s of sprint, the axe a quarter); a night or collapse never
  lowers a drive below 10 and leaves one already below 10; at full hunger or thirst the food or drink is kept. Stage lines are keys; their
  Spanish (`Tengo un poco de hambre` ... `Me muero de hambre`, the same for sed and sueño, `Estoy débil`, `Estoy muy débil`) waits for N05
  and the grammar cache. **Left:** the night does not apply `PlayerNeeds.Sleep` yet (N03, with `SleepTests`); rest only at the player's own
  fire (N02: the forge and the other rings); sprint (N02). The orchestrator's check: `--set-need thirst 25.2 --walk-to 176,186 --wait 8`
  raises `need_stage (sed)` and saves thirst 24.03. The agent met a ContentSchemaFuzzTests failure (seed `41s2qgWEC-M9`, O15: `describe`
  with its `slotGrammars` removed is accepted) that reproduces on the base: planned as core M81. Report `.claude/briefs/n01-report.md`.
- **N02, eating, drinking and sprinting (2026-11-05, Sonnet 5.5; gated on `feature/adaptive_design` at `3a4345f8`, 9195 passed, 3 skipped).**
  Right click eats the selected food (`meal_eaten` with its noun; at full hunger it is kept with a hint), the bottle comes back empty, and E at
  water within reach drinks by hand with nothing held (`water_drunk`: `botella`, `pozo`, `lago` or `río`; the lake is a radius round its
  point, so water near the river mouth can read `lago`). Sprint is a control (Left Shift, 1.6 times, from `needs.json`); a stage's walk factor
  now slows walking. At zero stamina gathering takes twice as long and never stops; filling the bottle is no labour. Rest finds fire rings by
  shape (`Needs/FireRings.cs`): a ring is lit when a torch stands in its 3x3 square, so the player's ring, the guide's camp and the forge count.
  New scripted steps `--eat <item>` and `--drink`. No Spanish added: the hints are English keys (`ui.foodKept`, `ui.drinkKept`), their Spanish
  («No tengo hambre.», «No tengo sed.») N05's. The 23 scripted runs are unchanged. The orchestrator's check: `--set-need hunger 40 --eat
  manzana` gives hunger 52 and `meal_eaten (manzana)`; `--drink` at the river gives thirst 30 to 40 and `water_drunk (río)`. Report
  `.claude/briefs/n02-report.md`.
- **N03, sleep and the collapse (2026-11-05, Sonnet 5.5; gated on `feature/adaptive_design` at `7cf2e115`, 9219 passed, 3 skipped).** Every
  night path (E at the fire, Enter on the card, `--sleep`) applies the night to the needs (`VoxelSession.Night.cs`; `SleepTests` keeps "a sleep
  equals a load" for all but the needs). After 60 active seconds at the sleep drive's last stage the player collapses where they stand (a fade,
  the collapse night, waking in place; `player_collapsed`, `player_woke`, a first-time tip in English); it is the sleep drive's alone, as
  N01's `CollapseDue` and 9.5 read (scripted: `--set-need sleep 0 --wait 62`). Nights and collapses advance the day and the sitting, a load only
  the sitting; one seam, `RunDayResets`, runs the day's resets for N04 and N06. A tick-sum rounding delayed the collapse by a tick
  (`CollapseDue` tolerates 1e-6 s). The fire's offer rule is unchanged, for L01 (`SleepOffer`, `CanSleep`). Runs unchanged. The orchestrator's
  check: day 2, sleep 40 and thirst 15, `--sleep`: thirst stops at the floor of 10, sleep 100, day 3. Report `.claude/briefs/n03-report.md`.
- **N04, health and death (2026-11-05, Sonnet 5.5; gated on `feature/adaptive_design` at `b72b9184`, 9246 passed, 3 skipped).** At zero
  health the player wakes 2 to 5 blocks from the camp ring with health and the drives at 50 and stamina full (`player_died` with `hambre` or
  `sed`, `player_woke (death)`); a death is not a night. The bag minus the items that stack to 1 goes into one Chest on the nearest free ground
  (a second death merges into it), E takes back what fits (`belongings_recovered`), the next night removes it; saved as `systems.chest`, the
  save version stays 6. **Design against code:** only the book and the axe stack to 1; the bottle stacks to 99, so it goes into the chest.
  The no-dead-ends test floods the walkable cells from the banks and times the 30 farthest walks against the health left (240 s). New runs
  r25-thirst and r26-death, new steps `--take-belongings` and `--need <need> <n>` (mid-script); the 22 other runs unchanged. English keys for
  N05's Spanish: `ui.beaconBelongings` («tus cosas»), `ui.diedHunger`, `ui.diedThirst`, `ui.chestRest`. The orchestrator's check: day 3,
  hunger 0 and health 4, a wait: `player_died (hambre)`, woke at the camp with 50s, the chest placed. Report `.claude/briefs/n04-report.md`.
- **R, the follow-ups synced into the adaptation (2026-11-05, Haiku 5.5; gated on `feature/adaptive_design` at `ce52e231`, 9358 passed, 3
  skipped).** `feature/village-decisions` (V29, M78, the refreshed grammar cache) merged into the adaptation; only the generated manifests and
  save fixtures and the core plan conflicted. The content loop re-hashed the bundle (`eec0472c…`), the grammar cache needed no new entry and
  the goldens and digests did not move; no test needed a fix where the two sides meet.
- **N06, food that grows back and fair gifts (2026-11-05, Sonnet 5.5; gated on `feature/adaptive_design` at `468a1b29`, 9382 passed, 3
  skipped).** Built ahead of N05 (it needed no new Spanish). `needs.json` takes optional `regrowth` (`berry-bush`, after a night) and `gifts`
  (`shopkeeper:bread`, `villager:bread`, `guide:water-bottle`, per day), checked by `GameDataValidator.CheckRenewal`; `RunDayResets` regrows
  each picked bush still a plain bush and clears the gifts given. A second ask the same day raises `<npc>.gift_used` and the NPC answers with
  an existing line (stall `shop_price`, villager `villager_bye`, guide `guide_offer_no`); a sale is not limited, a gift the active lesson's
  `do` step needs is neither refused nor counted, and with `--needs off` every gift is given and nothing is recorded. Saved as
  `systems.bushes` and `systems.gifts`, the save version stays 6. **Design against code:** Luna is the `guide` agent and her water is the
  one-time transfer offer, so her daily rule bites only once more ways to ask her exist; no shipped lesson has an `item_given` step at these
  NPCs yet; a bush picked in a save made before N06 stays picked; the host's `ConversationSession` has no days and keeps the old rule. **For
  N05:** «Hoy no tengo más pan.» (stall, villager) and «Hoy ya no tengo agua.» (Luna) in place of the borrowed lines. The 25 scripted runs did
  not move. The orchestrator's check: bread asked twice on day 2 (given, then «El pan cuesta dos monedas.»), given again after a night; a
  bush picked, refused, picked again after a night. Report `.claude/briefs/n06-report.md`.
- **W01, `item_crafted` (2026-11-05, Haiku 5.5; gated on `feature/adaptive_design` at `56f88b4b`, 9385 passed, 3 skipped).** A craft logs
  `world item_crafted (<noun>)` with the recipe's output count, after `LearnItem`; `WorldEvents.All` takes it; the schemas list no event
  names, so only their detail descriptions changed. Three runs gained exactly that line (r01-route, r10-playtest, r20-sleep, the runs that
  craft the torch) and were re-baselined; the other 22 did not move. Tested: the torch's event, an objective step on it; a lesson `do` step
  on it is not tested directly (it matches through the same trigger, and no shipped lesson has one). The orchestrator's check: the camp
  route's torch craft prints the event. Report `.claude/briefs/w01-report.md`.
- **N05, the HUD and the opening (2026-11-05, Sonnet 5.5, two attempts; gated on `feature/adaptive_design` at `6dfa8566`, 9433 passed, 3
  skipped).** The first agent wrote the code and stopped before compiling (out of context); the orchestrator committed its work as WIP and a
  second agent finished it (one missing `using`). Health and stamina bars beside the hotbar, drive icons with one to four notches, the stage
  line once above the hotbar with a soft synthesised sound, the needs in the inventory, gentle view effects with F7 and `--view-effects`
  to turn them off, the English tip on the first `need_stage`, a death card «Estás en tu campamento.», «tus cosas» on the chest's beacon.
  **Relaxed needs** (design decision 17, provisional): `--relaxed-needs` and a setting, half the drive rates, a faint at zero health with no
  chest. N06's lines «Hoy no tengo más pan.» and «Hoy ya no tengo agua.» replace the borrowed ones (six WAVs). **Day 1 is safe with room to
  spare:** the route takes 81 s of active play at the intended pace and 204 s at 2.5 times (the novice pace), the lowest meter is thirst
  91.4, every drive stays healthy and no recovery is needed: at N01's rates the needs barely touch day 1, a number for the needs check.
  **Open:** `morir` («Me muero de …») is above A2 in the lists and the lint does not read the HUD lines (`hudLines`); left unwaived for the
  owner. The HUD is hidden under `--needs off`, so the language runs did not move; r25 and r26 gained the tip. The orchestrator's check: a
  day-2 screenshot with thirst 20 and hunger 45 shows the bar and both icons. Report `.claude/briefs/n05-report.md`.
- **L01, lessons as optional classes (2026-11-05, Sonnet 5.5, then a Haiku sync with N05; gated on `feature/adaptive_design` at
  `971b1058`, 9451 passed, 3 skipped).** D-game-25 built. `first-words` keeps its id and units u1, u2, u3, u5 and u6 and alone starts by
  itself; u4 and u4v are a new lesson `first-class` (`after: first-words`); an old save splits the old lesson on load. Every other lesson
  is a class: the teacher offers it in one line («¿Quieres una clase? ¿Sí o no?»; the villager «¿Una clase hoy? ¿Sí o no?», since
  «conmigo» is not A2), once per NPC per day; «Sí» starts it, «No» or «No, gracias» cost nothing (no NPC reply, the trade prompt kept);
  «¿Me enseñas?» and the Lessons page's "Ask for a class" start one too. A class opens on its teacher met (saved `metNpcs`, no format bump)
  and its `after` lesson, never the day. Esc pauses a class at a talk step and frees the NPC; at a `do` step it stays engaged (the panel
  closes for the walk). From day 2 the fire offers sleep below 75 on the sleep drive, otherwise «No tengo sueño.» (a toast, with a WAV:
  it is a pool line); `--sleep` and the scripted step skip the check. Orders show once their NPC is met; `HoldsOrder` is gone and the
  oven order no longer waits for `n4-back` (Luz pays at the first talk). Cards from rung 1; the beacon follows the tracked objective; the
  Lessons page lists each class with what it needs. New run `r30-no-classes`; r20 and r21 re-baselined. **Core gap:** «¿Me enseñas?» takes
  no intent (the game reads its forms from `classAnswers` in `game/words.json`): a core milestone if the owner wants it understood.
  **Design against code:** the design's Domingo (the smith) teaches nothing, so the villager offers; the «Primero, la lección»
  order-hold lines stay in the content, unreachable. **Not checked:** 12 of the 26 scripted runs were not compared by the agent; save
  versions 2, 3, 5 and 6 with an engaged class not tested separately. The sync with N05 needed no test fix (the pool count 309). The
  orchestrator's check: day 2 at the villager the offer appears after the greeting (a moment later: a line typed before it is read as
  ordinary talk); after a wait, «No, gracias.» gets no reply and «¿Dónde está el río?» is answered. The gate's summary lines showed
  `limited.sh`'s new peak-memory line in place of the lint and licence results; `scripts/merge-gate.sh` now skips those lines (the
  results themselves were read from the logs: lint 0 errors, 149 warnings, unused `guide.*` pools; licences passed). Report
  `.claude/briefs/l01-report.md`.
- **N07, cooking at the fire (2026-11-05, Sonnet 5.5, then a Haiku sync with L01; gated on `feature/adaptive_design` at `20a8da0c`,
  9461 passed, 3 skipped).** A lit fire ring is the station `fuego` (a catalogue noun): with the crosshair on its stones or the ground
  inside, E opens the crafting panel ("Fire"), as at the workbench; an unlit ring does nothing. The roast apple is item 33 (`roast-apple`),
  listed under `manzana` with the label «manzana asada» (a new optional noun field `labels`); its recipe needs a new optional `makes`, since
  a noun's first item is the raw apple. Hunger +20, healing 5; raw food keeps its values. The craft logs `item_crafted (manzana)`. A test aid
  `--give <item>`; `--eat` prints health for a food that heals. **Design against code:** E needs the crosshair on the ring, though the
  panel's hint shows within 6 blocks; on a lit ring crafting comes before the sleep offer. Scripted runs: the speech count (the `fuego`
  card), a recipe row in r08, the Fire panel in runs that end at the camp with apples; no re-baseline. The orchestrator's check: the camp
  route, a torch by the ring, `--give manzana --craft roast-apple`, `item_crafted (manzana)`, eaten from hunger 40 to 58.8. Report
  `.claude/briefs/n07-report.md`.
- **U01, clean defaults (2026-11-05, Sonnet 5.5; the orchestrator merged N07 into it; gated on `feature/adaptive_design` at `dd01a6e9`,
  9476 passed, 3 skipped).** Ordinary play starts clean: the debug overlay and the dialogue's developer note are off unless `--dev`; F3
  toggles both (still off in `--playtest`, as before); the "Heard / Expected" line stays. The tip box sits under the overlay when F3 is on.
  The dialogue footer drops "hold V: speak" once the speak tip has shown, and the first lesson's English no longer repeats it. The objective
  tracker shows one line per objective, three at most, the last advanced first, the rest "+n"; a lesson `do` step stays out of it while the
  lesson strip is up; it moves under the tip block when they would touch and hides in a conversation; its recency order is not saved.
  `scripts/scripted-runs.sh` passes `--dev` to every run but `r10-playtest`, so no capture moved. **Timing under load:** with two agents
  building, the no-models suite failed `GeneratedLexiconTests.LoadingTheBundleIsFastEnough` once (9 s against its 3 s budget; it passes
  alone and passed in the gate): a wall-clock test that two parallel agents can starve, to watch. The orchestrator's check: a day-4 world
  with thirst 40 shows no debug text, a one-line tracker, the drop icon and the health bar. Report `.claude/briefs/u01-report.md`.
- **N09, the drive rates as a setting (2026-11-05, Sonnet 5.5, then a Haiku sync with U01; gated on `feature/adaptive_design` at
  `37c7640b`, 9501 passed, 3 skipped).** The owner's answer of 2026-11-05 built: a settings panel on **F8** (keyboard only, a menu pause)
  with the drive rate as a slider (×0.5 to ×4 in steps of 0.25, default ×1), the view effects and relaxed needs; `--needs-rate <x>` for runs
  (an off-step value is refused); `SaveSettings.NeedsRate` written only when not 1. The rate multiplies hunger, thirst and sleep only, and
  with relaxed needs (×0.5); stamina, health, food and the collapse delay are unchanged; no meter, digest or replay holds it. The session
  summary and the console name the rate when it is not ×1, so a playtest's numbers say what rate they ran at. Measured: 60 s of play at ×3
  drops each drive exactly three times as much as at ×1. A helper property was being serialised and would have changed default saves; now
  ignored. **Machine:** one of the agent's full runs was killed at the shared 8 GB cap of two slots (exit 143) and passed on the rerun:
  the cap held the desktop, as designed. The sync caught a stale link in the handovers README left by the orchestrator's handover move
  (the doc-link test failed on the merged tree); fixed in the sync. The orchestrator's check: the panel at ×2.5 (screenshot), `--needs-rate
  2.3` refused. Report `.claude/briefs/n09-report.md`.
- **U02, the conversation surface (2026-11-05, Sonnet 5.5, then a Haiku sync with N09; gated on `feature/adaptive_design` at
  `fe986862`, 9546 passed, 3 skipped).** The dialogue panel shows the current exchange (the learner's last line, the NPC's reply with its
  English, one coaching line); older lines collapse behind a "N earlier lines hidden" row and come back with **PageUp** (any state, the
  text box too) or **O** (Tab stays Escribir, D-game-9). The book's "What I said" history keeps every line. One coaching line replaces the
  stack of correction, accent and English notes: uncertain speech not understood, then off prompt (typed turns, not a lesson turn; new
  `ui.coachOffPrompt`), then a slip (the turn's feedback, else the first error line); a plain "understood" turn shows none (the reply is
  the outcome). The lesson heading sits in the panel's header (`Lesson · First words · step 1 of 7 · Say it`, English from the locale) and
  the strip stands flush on the panel without its own heading; a new scripted step `--expand`. Every D-game-9 key (R, S, H, N, W, Tab,
  choices, Enter, Esc) is tested in idle, after a turn, thinking and after the typing offer. **Design against code:** the design's heading
  example is Spanish, the code's English; the strip's step text and buttons stay above the panel. Scripted runs: stdout unchanged in all
  26; only r30's lesson pictures moved, as intended. The sync kept both `--expand` and N09's `--settings` in the one flag line both sides
  edited. The orchestrator's check: three turns with the shopkeeper collapsed ("10 earlier lines hidden") and expanded (PageUp's view), and
  «yo quiere dos pan» with the single coaching line «Sí, «yo quiero dos panes». ¡Muy bien!». **Seen:** the row reads "1 earlier lines
  hidden" for one line (a plural to fix in a later polish), and the collapsed panel leaves its upper half empty (the rows sit at the
  bottom). Report `.claude/briefs/u02-report.md`.
- **N08, a berry planted at the camp (2026-11-05, Haiku 5.5; the orchestrator merged U02 into it; gated on `feature/adaptive_design`
  at `209049db`, 9561 passed, 3 skipped).** Design decision 14 built, adopted provisionally. The rule is data: an optional `planting` in
  `content/game-v1/game/needs.json` (item `berry`, block `bush`, grown block `berry-bush`, radius 8, at most 6 bushes), its schema, and a
  validator check (unknown item or block, a grown block with no regrowth rule or not regrowing from the planted block, radius or count not
  positive). A right click with the berry held on grass or dirt within 8 blocks (horizontal) of the guide camp's ring (`firering`) plants a
  bare bush; a click in the air or on another block still eats. Too far, an occupied cell or the cap: a toast, the berry kept. The planted
  cell is recorded as picked, so N06's regrowth bears it at night; the planted cells are saved in a new optional `systems.planted` (save
  version stays 6; an older save loads with none). Event `bush_planted (fruta)`; a scripted `--plant x,y,z`. **Design against code:** the
  planting radius is about the guide's camp ring, while sleep needs the player's own fire, so planting and sleeping happen at different
  fires. Scripted runs: 25 of 26 identical; r30's `declined.png` differs by timing noise (two runs of the same binary differed too).
  **Machine:** after the merge, the content loop's full suite was killed twice by the OOM killer: `Vocaterra.Game.Core.Tests` alone now
  peaks at 5.4 GB (2.80 GB at the test-performance plan's T1), on the base as on N08, and beside a second assembly it exceeds the 6 GB cap
  of one `limited.sh` command; xUnit at two threads still peaks at 4.8 GB. The gate passed when the assemblies did not overlap that way. A
  fix is proposed as T7 (the test-performance plan), not yet authorised. The orchestrator's check: the camp route, `--give berry --plant
  170,42,186` (planted, `bush_planted`), the player's fire lit as in r20, `--sleep` (session 2), and the same cell gathered as a
  `berry-bush` (+1 berry). Report `.claude/briefs/n08-report.md`.
- **U02b, the conversation surface's QA fixes (2026-11-06, Haiku 5.5, then a Sonnet continuation; gated on `feature/adaptive_design` at
  `565554f3`, 9586 passed, 3 skipped).** The QA pass `docs/qa-reports/2026-11-06-ui.md` (triage in `docs/qa-reports/README.md`). Which rows
  show is now Core logic (`PanelFit.cs`: `PanelFitter.Fit`, the blocks in order of keep: the learner's line and the reply's Spanish, then
  the coaching line, then the reply's English, then the hidden-lines row; a `--dev` note goes first); the panel only draws. Expanded, Up and
  Down scroll (not while typing; PageUp and O still toggle; Tab stays Escribir) with "N earlier lines above" / "N later lines below"
  rows; the counts take singular and plural strings ("1 earlier line hidden"). The lesson heading shortens its title first
  (`LessonHeading.FitTo`), so "step n of m · kind" stays. **ui-3 and ui-4 corrected:** «gracias» at the shopkeeper's prompt is an
  off-prompt line the NPC took (`OffPromptIntent`, never `Communicated`), not a line it failed to understand, so U02's off-prompt coaching
  was right (ui-4 not a bug); its English said "Good… understood you" and now says "{npc} understood, but that is not what was asked."
  (`feedback.outcome.off_prompt`). The Haiku attempt read the verdict the other way (it made the coaching say "did not understand" beside
  the NPC's «Te entiendo») and stopped before its checks; a Sonnet continuation corrected it, finished the checks and found the `--dev`
  note pushing out the learner's line at 800x600. Scripted runs: 26 of 27 equal; r30's dev note differs by its wall-clock figure only.
  **Machine:** the first gate was killed by the OOM killer (exit 143, the known T7 cause); the rerun passed; the continuation saw two
  `OutOfMemoryException`s in Game.Core.Tests while the baseline ran beside it. The orchestrator's check: 800x600 at 150 %, three turns,
  collapsed (the learner's line and the reply) and expanded; four turns expanded at 1280x720 ("9 earlier lines above (Up to scroll)").
  **Seen:** at 800x600 and 150 % the coaching line, two rows when wrapped, is dropped while about one row stays empty; acceptable. Reports
  `.claude/briefs/u02b-report-1.md` and `u02b-report.md`.
- **T7, the game tests' memory (2026-11-06, Sonnet 5.5; launched by the orchestrator after a third OOM kill; gated on
  `feature/adaptive_design` at `56094f20`, 9586 passed, 3 skipped).** The test-performance plan's T7 section has the numbers. The cause
  was test sessions never disposed: a session's turn worker is a thread that keeps the session, its 38 MB scene clone and a language
  engine alive, and five test classes (`ConversationSurfaceTests`, U02's, built ten) left them running; the live heap grew about 450 MB
  per such test. They now dispose their sessions, and `Vocaterra.Game.Core.Tests` runs three xUnit threads (`xunit.runner.json`; ten
  still peaked at 4 to 4.5 GB with the leak fixed, three peak at 2.4 to 2.5 GB in the same wall time). Game.Core.Tests alone: 5.0 GB
  to 2.5 GB; `scripts/test.sh full`: killed at 87 s before, passed three times after at 4.6 to 4.8 GB (the gate's run 4.8 GB); no test,
  assertion, budget or production code changed. The remaining full-run peak is Nlp.Tests (2.8 GB) beside the fuzz suite (2.5 GB), both
  core; a further cut would be a core milestone. Report `.claude/briefs/t7-report.md`.
- **W05, procedures, stations and actions as data (2026-11-06, Haiku 5.5; gated on `feature/bread-pilot` at `28c5becf`, 9614 passed,
  3 skipped, with the models).** Four schemas (`procedure`, `procedures`, `stations`, `actions`), three empty bundle files, the loaders
  `Activities/ProcedureBook`, `StationBook`, `ActionTable` in `GameData`, the phrase sheet's `does` kind (documented, read, no rows yet) and
  the validator's activity rules. Values chosen where the design is open or disagrees with the code (D-game-23's provisional 18.2 values
  stand): a step may name a `use` (the design's `sacar` pair is `mano`/`horno`, which its step cannot express); `anyOrder` is a group name;
  `what` is an item noun or a fixture, `where`/`from`/`with` are fixtures; the design's `flour` is refused until W10 adds the item, and
  `mano` must become a catalogue noun in W10. The tú imperative comes from the lexicon (mood `imp`, person 2, `sg`): no core gap. The agent
  stopped twice with work uncommitted while its own background runs went on, and was resumed by message; the scripted runs moved only
  `r30-no-classes`' `declined.png`, which also differed between two captures of the same build (nondeterministic, not this change).
  Report `.claude/briefs/w05-report.md`.
- **W06, the procedure runner (2026-11-06, Sonnet 5.5; gated on `feature/bread-pilot` at `b1579284`, 9645 passed, 3 skipped, with the
  models).** `Activities/ProcedureRunner` (start, act, collect, repeat, slower, help, leave, abandon, the view, drained events, capture and
  restore) and `ProcedureProgress`; `PhraseProgress.RecordAct`, with `does` rows kept out of the review deck; the four `procedure_*` world
  events; `systems.procedures` in save version 6 (absent when empty; versions 1 to 6 tested); `GuideSession.Activities.cs` with the mastered
  recipe's one-click bake through `CraftingRules`. **Design decision 19 is provisional and the owner's** (orchestrator's value, brief of
  2026-11-06): an act records whether its instruction was heard and read, apart; rung moves, folding and mastery treat both alike
  (`InstructionEvidencePolicy`; `RequirePresentation` is the alternative). Other values chosen: `anyOrder` steps follow the drawn order
  strictly (the instruction names one, so a group-mate first is out of order); the order is `WorldRng.For(seed, "proc." + id, bake)`;
  mastery counts distinct play sessions; chunk and goal rows start at rung 2 (18.2 decision 3); a full bag leaves `ReadyToCollect` with
  nothing consumed; the one-click bake has no bonus; a rung-4 act earns nothing. Left to W07: the per-noun read count of 10.10. Report
  `.claude/briefs/w06-report.md`.
- **W07, the station view (2026-11-06, Sonnet 5.5; gated on `feature/bread-pilot` at `23d139e3`, 9737 passed, 3 skipped, with the
  models).** Core `Activities/StationScreen`, `StationPanel`, the view records and `InstructionStrip` (a model of its own, drawn by
  `InstructionStripLayer`, for W30's recipe book); Game `UI/Station/` layers and layouts; E at a station's prop (priority NPC, station,
  workbench; `Scene.PropCells`); rows through `NpcVoice` with the slow file; muted or unrecorded rows show the Spanish card and record the act
  as `Read`; the bowl, spoon and oven icons with a plain fallback tile; `--station`, `--act`, `--proc-help`. W08's hook is
  `StationPanel.OnTalk`. The orchestrator looked at the screenshots, made from a throwaway bundle copy with a test procedure on the
  `oven` prop: the first rung, a slip's highlight, help's highlight and a finished bake, all as 10.8 and 10.10 say. **Bug found and fixed:**
  the new atlas cells moved eleven scripted runs' far horizon, because the top mip levels average the whole atlas; interface-only cells are
  now left out of the mips (`IsUiOnly`, tested), and every run matches its baseline again. Small points for W10's content review: the
  panel's title is the word "Station", not the station's name; after help the selection stays on the empty hand while the highlight shows
  what to hold; the mastered recipe's one-click bake is built (W06) but not yet offered at the station. Report `.claude/briefs/w07-report.md`.
- **`r30-no-classes`' `declined.png` is nondeterministic** (W05 and W07, 2026-11-06): two captures of one build differ in that image's
  bytes. Not caused by either milestone; fixed by T8 (below).
- **W07b, the station view's small points (2026-11-06, Haiku 5.5; gated on `feature/bread-pilot` at `1d511d76`, 9758 passed, 3 skipped,
  with the models).** The panel's title is the station's name (`ui.station.<id>`, else the noun with its article, else the id); help and a
  third slip's highlight also select the highlighted held thing and target; a mastered recipe's one-click bake is offered at the station
  (design 10.15: B, or Enter on the choice; the guided bake first and selected when the goal row is due, through a new
  `PhraseProgress.IsScaffoldDue`). No screenshot: the agent's throwaway bundle failed the content checks, so the view is checked by its tests
  only until W10's content; W10's spot-check looks at it. Report `.claude/briefs/w07b-report.md`.
- **T8, the stable `r30-no-classes` capture (2026-11-06, Haiku 5.5; gated on `feature/bread-pilot` at `937f4a44`, 9759 passed, 3 skipped,
  with the models).** The differing box (x 384 to 428, y 556 to 565 of `declined.png`) was the turn latency on the `--dev` note
  (`DialogueModel` prints `LastTurnMilliseconds`, the worker's wall time). Fixed at the source: `LaunchOptions.TurnClock` gives a scripted
  run no clock, so its turns time at 0.0 ms; play keeps `MonotonicClock`. Pinned by a test in `VoxelLaunchOptionsTests`. Three captures of
  the fixed build are identical; the whole set before and after: 25 runs same, r30 moves only in that box. Left: `CheckBattery`'s
  `check: picked … ms` and `SessionLog` timings still read the clock; no current run shows them, but a future scripted run with checks
  could move (fix the same way if it does). Report `.claude/briefs/t8-report.md`.
- **W08, the teacher in the loop (2026-11-06 to 07, Sonnet 5.5 medium, two agents: the first stopped when the session ended; gated on
  `feature/bread-pilot` at `5091891e`, 9794 passed, 3 skipped, with the models).** Six host kinds (`procedure_repeat`, `procedure_slower`,
  `procedure_help`, `give_missing`, `teach_recipe`, and `start_procedure`, added beyond the plan so the accepted offer opens the station);
  `<npc>.teach_start` and `teach_end` as host turns (new `NpcService.HostTurn`); the station's talk (V, Space and W reach the teacher with the
  station open; scripted step `--station-say`); Luz's `offering_bake` and `teaching` states, prompts `baker-offer` and `baker-proc`, the
  answer set `baker-proc`, seven new Spanish lines with 18 recordings; `host-events` schema version stays 2 (the bump to 3 is W04's).
  **Decision 20's values, provisional, for the owner:** a grant only while a run is in progress; it gives what the recipe uses and the bag
  lacks, all or nothing; open for the first bake, otherwise once per in-game night (the first bake's grants do not count); no room gives
  `baker_no_room`, nothing missing «Ya tienes todo.», already granted «Hoy no tengo más.»; the offer is made once per day until the player
  has baked, and also whenever asked (`ask_craft`); W10's seam is `GuideSession.GrantRequiredByLesson`. The offer is silent while the bundle
  has no procedure (tested; 26 scripted runs unchanged but for the speech count). **The new lines** («¿Quieres hacer pan?», «¡Vamos!»,
  «Escucha.», «Te ayudo.», «Toma. Aquí tienes.», «Ya tienes todo.», «Hoy no tengo más.») were reviewed by the orchestrator: idiomatic, `tú`
  throughout, A2; the design's «…conmigo?» was cut because it fails the A2 lint. **Core gaps, not patched** (core milestones if the owner
  wants them): «¿me enseñas a hacer pan?» is unknown at `baker-open`; «¿Tienes harina?» is not `ask_availability` (only «hay X» is); and,
  found in the spot-check, two intents in one line («Gracias, adiós.», «Gracias. Adiós.») are unknown at every prompt while each alone
  resolves. Reports `.claude/briefs/w08-report.md` and `w08-report-2.md`; screenshots of the station's talk were taken on a throwaway bundle.
- **W10, session 11 "En la panadería" (2026-11-07, Sonnet 5.5 medium in two phases with the orchestrator's review of the Spanish
  between them, then a second agent for two bugs; gated on `feature/bread-pilot` at `366a6038`, 9852 passed, 3 skipped, with the
  models).** The `pan` procedure (six actions, chunks `la-masa` and `el-horno`, the goal), the `horno` station (fixtures `bol`, `cuchara`,
  `horno`), 9 `does`, 4 `says` and 5 `hears` rows, flour (item 34, `IconFlour` after the interface-only atlas cells so no world mip moved),
  the stall's fixed `stall.sell_flour` and `stall.buy_bread` at one coin with `instead` sending `harina` to the sale, the optional lesson
  `en-la-panaderia` (a2, after `ayer-y-manana`, 14 steps, its do step opening the grant through W08's `GrantRequiredByLesson`), 8 checks,
  70 recordings, run `r27-bread`. **bol** over tazón (a cognate, the design's rows). **The review** changed «Primero.» to «Todavía no.»
  (now played as Luz's line on an out-of-order act), `shop_buy` to «¡Gracias! Aquí tienes.», `shop_cant_pay` to «La harina cuesta una
  moneda.», and **reworded two lines of sessions 1 to 10**: `shop_hay` «Hay agua, pan y harina.» and `shop_wont_sell` «No tengo eso. Tengo
  agua, pan y harina.» (flour is sold from the first day). **Code:** `NpcTurnRules.AnswerMisses` chains the misses of an `instead` target
  (flour with no coin or a full bag is answered, not sold); `GameDataValidator.IsMadeThing` (the dough a step makes); `PhraseSheet.Closest`
  answers the prompt's first row when nothing is close; `SessionSkip` stops at session 10 (skipping to 11 would need the market's coins);
  `phrases.schema.json` allows `prompt: null` for `does` rows. **Two bugs found in its report and fixed before the gate:** W08's offer, live
  now that the bundle has a procedure, swallowed the first line of the day («quiero dos panes, por favor» unknown, no bread bought in r22);
  `baker-offer` now takes `baker-open`'s intents and answers them as `idle` does, and the offer counts as declined. `--lesson-run
  en-la-panaderia` hung at the do step (nothing drives the station); it now stops with a message (`ScenarioScript`), and `r27-bread` uses
  `--skip-to-session 10` and the offer. **Side effects for the owner:** the error example «no, mucho gracias» (deny at `stall-offer`) was
  removed, because with `thank` allowed at the offer it tied and the lint's confusion error cannot be waived; a line answered at the offer
  is credited `unaided` (no support card there) where it was `supported`. **Left:** Luz does not repeat the instruction after «Todavía
  no.» (R does); the recordings are generated, not owner-listened (the pilot's listening gate, W11). The spot-check: the bread run's
  captures show the station at rung 1 with Luz's grant and the done bake (two loaves after two slips); at `stall-need` «¿Hay harina?» and
  «Vendo pan.» answer correctly; «¿me enseñas a hacer pan?» and «quiero hacer pan» stay unknown at `baker-open` in the CLI (W08's core gap;
  the game reads «¿Cómo hago pan?» off-prompt as `ask_craft`, tested). Reports `.claude/briefs/w10-report-a.md` (both phases and the fixes).

- **W02, tools and materials (2026-11-07, Haiku 5.5; gated at `2bd4728e` on `feature/bread-pilot`, 9855 passed, 3 skipped).** A
  `Material` per block (`Voxel/BlockDefinition.cs`: none, wood, plant, stone, soil, water; an optional last record parameter, not positional
  as the paragraph had it) and `ToolSpec(Material Class, int Speed, int Yield = 1)` on items, the axe `(Wood, 4)`; `GatherTicksOf` divides a
  gatherable block's ticks by the held tool's speed when the classes match (at least one tick), and `AxeSpeedUp` is gone. Trunks are wood
  and the berry bush plant; the agent also gave the other blocks a material (stone, soil, water, the wooden props and the plants), which
  changes nothing because only trunks (60 ticks) and the berry bush (45) are gatherable. `Yield` is recorded and unused. Tests: 60 by hand
  and 15 with the axe, the axe on a berry bush or on stone changes nothing, the progress event the HUD reads carries the same total.
  Scripted runs unchanged (26 of 27 identical outright; `r27-bread`'s first baseline capture was one tick late, 2461 for 2460, and every
  later capture, including a rebuilt baseline, gave 2460: an unexplained one-off of the run, not of W02). **Found:** the fuzz oracle O7
  fails on CsCheck seed `8eu5g1sTe_A8` on the branch before W02 (a core issue, the core plan's §5b). Report `.claude/briefs/w02-report.md`.

- **W03, fail-forward as data (2026-11-07, Sonnet 5.5 medium; gated at `bc12153e` on `feature/bread-pilot`, 9901 passed, 3 skipped).**
  The stall's patience rule is data: `NpcPatienceRule(Misses, AfterHelp, Event, Prompt, Once)`, loaded by `NpcRoster` from an optional
  `patience` in the NPC file (`npc.schema.json` stays at schema 2: the object is optional), and the shopkeeper's values in
  `shopkeeper.json` (4, 2, `shopkeeper.give`, `stall-need`, once). `NpcService.FailForward` applies any NPC's rule at its prompt (not
  off-prompt); `StallPatience`, `StallPatienceAfterHelp` and the shopkeeper-only branch are gone; `once` maps onto the saved
  `BottleGiven`, so saves load unchanged. The validator: positive numbers, `afterHelp` at most `misses`, the event taken by a state of the
  NPC's agent (`onHostEvent`; the paragraph's "exists in `host-events.json`" did not fit, since `shopkeeper.give` is the agent's input and
  `stall.give_water` the world event its reply makes), the prompt one of the NPC's. The stall's tests pass unedited; `NpcPatienceTests`
  covers a custom rule, help, another prompt, once, no rule and the save. Bundle re-hashed (`save-v1.json`, `save-v4.json`); no golden moved.
  Scripted runs unchanged except **`r27-bread`'s first capture, which alternates between tick 2460 and 2461 from build to build** (seen by
  W02 and W03; a determinism leak in that run to plan as a T milestone). The agent's suite failed only
  `GeneratedLexiconTests.LoadingTheBundleIsFastEnough` (9 s against 3 s while M80's gate ran beside it; 1 s alone). Report
  `.claude/briefs/w03-report.md`.

- **W04, price lists (2026-11-07, Sonnet 5.5 medium; gated with V31 at `796585af` on `feature/bread-pilot`, 9938 passed, 3 skipped).**
  A `sell` or `buy` in `host-events.json` may carry `"slot": "item"` and `"prices"` (catalogue noun to coins) instead of one `item` and
  `price` (schema stays 2; `oneOf` the two forms). `NpcEffect.Prices`, `For(slots, nouns)` and `Priced(noun)` turn a verdict into the fixed
  effect; `ExchangeMiss` judges a priced entry first: an unlisted item, a noun naming no item or no item gives the existing `<npc>.wont_sell`
  (the paragraph's `dont_sell`; no duplicate added) or `wont_buy`; a listed item keeps the other misses' order. `VoxelSession.ApplyHostEvents`
  resolves through `For`; the exchange stays atomic (`CanExchange`); `SessionSkip`'s purchase finder reads priced sells. The validator: each
  listed key is a catalogue noun naming an item; the slot is only checked to be `item` (no prompt-to-slot map exists). Tests on a test bundle
  (two items, `qty`, `cant_pay`, `wont_sell`, `no_room`, a priced buy); existing sells and buys unedited. No bundle data: **live use needs a
  content milestone** (a priced entry, agent states with lines such as «No vendo …»; the success line cannot name the item without a
  templated slot). Scripted runs unchanged. Report `.claude/briefs/w04-report.md`.
- **V31, spoken lines heard on the open decode (2026-11-07, Sonnet 5.5 medium; gated with W04 at `796585af`, 9938 passed, 3 skipped).**
  `SpokenUtteranceEvaluator` holds a second pipeline with M80's fallback on (the core's 0.5 floor), used only in free talk
  (`PromptOverride is null` and not a review) at the seven constrained prompts (`villager-open` and `guide-open` are open prompts and decode
  once). `LineReading` re-reads `OtherPass` for the vocative and greeting. A line heard open is credited as typed (`GuideTurn.HeardOpen`,
  `CreditKind`); the turn's decodes are summed (`TurnTimings.Decodes`); `heard_open` is a voice event and a session-log field. 14 tests
  (`OpenDecodeTests`, scripted and Kokoro); `ConstrainedRecognitionTests` unchanged. **Heard (Kokoro, `ef_dora`):** «me gusta el río» and
  «tengo dos manzanas» get «Te entiendo»; «mañana voy a comprar pan» at the smith is understood as typed; **a limit:** the constrained decode
  can force a confident wrong row (baker «el río es grande» heard «es gracias», `thank`; shopkeeper «me gusta el río» heard «me cuesta el
  río», `ask_price`), which never reaches the open decode; «mi hermano vive en Madrid» and «la luna brilla» are not heard. The accent risk
  is unmeasured. All 27 scripted runs identical (the agent first ran four; sent back for the full set). D-game-9 note added. Report
  `.claude/briefs/v31-report.md`.
- **Main merged in: PR #45, the water series (2026-11-08, Sonnet 5.5 high; gated at `bc881c58` on `feature/bread-pilot`, 10328
  passed, 3 skipped, peak 6.0 GB).** The owner merged `feature/water` into `main` (2026-11-07) and asked for it on this branch. 19
  conflicts, resolved as `docs/game/water-plan.md` section 3 writes: `PlayerBody.Step(..., RiverCurrents? currents = null, int
  speedPermille = 1000)` passes the walk speed to the swim step; the session steps needs and water events; `LayerStack` puts the bubbles
  just above the needs HUD; `SoundCue` keeps `NeedStage`, `WaterSplash`, `UnderwaterBubbles`; `limited.sh` keeps the two-slot slice and
  H08a's OOM detection; **save version 7** holds both sides' version-6 fields (`Systems`, `MetNpcs`, `Air`, `Flow`), no migration between
  the two version-6 shapes. `xunit.runner.json` stays at 3 threads (ours) over water's 4. Breaks the merge exposed: drinking by hand aimed
  with `Simulation.Target`, which looks through water since H05 (now `InteractTarget`); the hotbar's `Gap` and `Bottom` stay public for
  the bubble meter. Scripted runs: every difference is water's (digests, the save version, the waterfall, the animated water, the target
  through water); `r27-bread`'s tick 2460 both times. The lake run (wade, swim, dive, breath, bubbles beside the needs bars, hop out)
  works; drowning does not hurt yet (H11). **Open:** `--walk-to 212,262` back to the bank stopped 0.8 blocks short after 14400 ticks with
  375 jump cues, not compared with a water-only build; `docs/game/saves.md`, `architecture.md` and the water plan still say version 6.
  Both go to H11. Report `.claude/briefs/water-merge-report.md`.
- **T9, the jittering `r27-bread` capture (2026-11-08, Haiku 5.5; gated at `ed1aa937` on `feature/bread-pilot`, 10329 passed, 3
  skipped).** The moving value was the autosave tick (2460 or 2461, about 1 base run in 9), and every later capture shifted with it. A
  script's `Say` step waits for `PendingTurns == 0`, checked after each frame's collect; the worker's outcome (milliseconds) landed before
  or after the next frame's collect (about 32 ms) at random. The cause is inferred from the timings: no instrumented run caught the late
  collect. Fix: `TurnWorker.WaitForOutcomes(timeout)` waits on a monitor pulsed per outcome; `VoxelGame.Update` calls it at the end of a
  frame in scripted screenshot runs only (60 s cap), play and the playtest unchanged, no clock read. Test in `TurnWorkerTests`. Proof:
  twenty runs of the fixed builds, ten beside three busy loops, all 2460; the full set, all 27 the same. **Watch:** the agent's own full
  run once threw `OutOfMemoryException` in `GuideTurnTests.TheCursorTileIsNamedWhenTheGuideIsAskedWhatThisIs` (the assembly passed on
  rerun; the gate did not see it; the suite peaks at 5.6 to 6.0 GB of the 6 GB cap). Report `.claude/briefs/t9-report.md`.
- **W04b, the stall's price list (2026-11-08, Sonnet 5.5 medium; gated at `4ad3a5c0` on `feature/bread-pilot`, 10368 passed, 3
  skipped).** `stall.sell_flour` is a priced `sell` (`"slot": "item"`, `"prices": { "harina": 1 }`); water and bread stay free (the owner,
  D-game-23 item 6). W10's `instead` entry `harina` → `sell_flour` stays: it is the only route from `stall.give_water` to the sale. **A
  bug fixed:** a gift with `asked` gave its item to a request naming a word the noun catalogue does not know («Quiero una espada.» got the
  bottle); `NpcEffect.ExchangeMiss` now answers it with `<npc>.wont_sell` (the stall's «No tengo eso. Tengo agua, pan y harina.», and the
  villager's own line for its bread gift); «quiero comida» now gets that line too. No Spanish changed; no recordings; scripted runs all the
  same. **Core gaps (for a later core milestone):** «¿Tienes manzanas?» and «¿Tienes una espada?» read as `state_have` at `stall-need`
  (answered «Sí, es verdad.»); «quiero algo de beber» and «quiero una botella de agua» are `unknown`. Report `.claude/briefs/w04b-report.md`.
- **H11, the water series reconciled with the bread pilot (2026-11-08, Sonnet 5.5 medium; gated at `d1562c75` on
  `feature/bread-pilot`, 10393 passed, 3 skipped, suite peak 4.0 GB).** Drowning goes through the needs: `NeedsHealth` (the `IPlayerHealth`
  adapter) takes 10 health per `OutOfAir`, health does not recover at 0 air, and death runs the bread pilot's flow with the
  `player_drowned` hint («You ran out of air. Swim up to breathe next time.», `content/locale`); with needs off or paused, no damage. The
  bubble meter rises above the needs stage line where they overlap (only in "beside" mode). The underwater blur is a settings row saved as
  the optional `SaveSettings.UnderwaterBlur` (save version stays 7), overridden by `--underwater-blur`, forced off by `ViewEffectsOff`.
  `saves.md` and `architecture.md` now say version 7. **Not a walker bug:** `--walk-to 212,262` stops short because that sand bank stands
  2 blocks above a shelf too shallow to swim; no body can climb out there (a climbable bank works); the cliff is open in D-game-26. Seen:
  "your things wait where you fell" shows with an empty bag too, as for every death. Scripted runs all 27 the same. The owner's water
  decisions are D-game-26. Report `.claude/briefs/h11-report.md`.
- **H12, the lake bank (2026-11-08, Sonnet 5.5 medium; gated at `95249425` on `feature/bread-pilot`, 10395 passed, 3 skipped, with
  M85).** The owner: "Fix the lake bank" (D-game-26). Not one bank: every shore had a rim of one-block-deep water, too shallow to swim,
  under a bank 2 blocks over the bed, so a player could leave no water body but by a path. A scan with the body's own numbers found 582
  such edges (the whole lake rim, about 275 river columns, the waterfall pool's rim, the bridge landings; no pond). `TerrainGenerator`:
  `BankHeight` starts each bank at the water's level; `BankBesideWater` brings each dry column beside shallow water to at most 1 over its
  bed (paths and faces over 4 kept, water never spills). Water cells, spawn, NPCs, props and paths identical; 3514 columns (2.4%) moved;
  the scene digest is `b699324f…`, save fixtures' state digests re-recorded. `WaterEdgesAreExitableTests` holds the world at 0 edges
  (exempt: bridge floors, the river staircase's two steps). Scripted runs: 13 differ, all from the world (digests, a teleport landing 1
  lower, r27's walk 38 turns instead of 36 at the same tick); new run `r31-lake`. Report `.claude/briefs/h12-report.md`.
- **U05, the NPC's name as the panel's title (2026-11-08, Sonnet 5.5 medium; gated at `1f56f6b9` on `feature/ui-issues`, 10402 passed, 3
  skipped; GitHub issue #6).** `DialogueLayout.Header` (46 px, 34 compact; `HeaderHeightFor`) places the lines; `DialoguePanel` draws the name
  at 32 px (16 px compact) with the rule under it, the lesson heading beside it in the muted colour, and `LinePrefix` gives the NPC's lines no
  `Name:` prefix (the learner's and the English keep theirs). Nothing depended on the prefix. 800 x 600 keeps 6 rows of conversation. Scripted
  runs: stdout and digests identical in all 28; pictures differ only in `r27-bread` (`offer.png`) and `r30-no-classes` (`offer.png`,
  `declined.png`, `paid.png`), the header. The orchestrator fixed one spliced sentence in `running.md`. 7 tests in `PanelTitleTests`.

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
