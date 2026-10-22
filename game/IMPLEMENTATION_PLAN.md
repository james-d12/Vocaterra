# Implementation plan: Vocaterra (the game)

This is the plan for the game, the folder `game/` of the repository. The language core is the sibling folder `nlp/` with its own plan
(`nlp/IMPLEMENTATION_PLAN.md`) and decisions; this file never plans core internals. Since 2026-10-21 (D-game-7) one orchestrator runs both. Owner decisions are in `docs/DECISIONS.md`. Status values: `planned`, `in progress`,
`in review` (on its branch, not yet merged), `done` (merged to main and gated; for the S series, merged to `feature/prototype` and gated), `blocked` (names the blocker).

## 1. Decisions taken

- **The core by project reference** (D-game-7, 2026-10-21; before it: packages from a submodule's local feed). `Vocaterra.Game` and
  `Vocaterra.Host` reference the projects under `nlp/src`; the game never copies core source (core ADR 0009). A call the core does not
  expose is a core milestone in `nlp/IMPLEMENTATION_PLAN.md`, never a workaround in the game. (`docs/CORE_REQUESTS.md`, the request file of
  the two-repository era, was closed when the repositories merged; its open rows became M28b to M28d in the core's plan.)
- **Console host until the engine is decided** (D-game-1). The host is engine-agnostic: no engine code under `src/Vocaterra.Host`.
- **Game words live here**, not in the core. The core knows effect kinds and host event names only as opaque strings.
- **Content tool:** the core's ContentBuilder (`nlp/tools/Vocaterra.Nlp.ContentBuilder`), run from source by the root `scripts/content.sh`.
- **Model policy:** Haiku 5.5 by default, Sonnet 5.5 for design-heavy milestones, Opus only on the owner's request.
- **The prototype brief** (D-game-8, owner, 2026-10-21): a 3D first-person voxel slice on MonoGame (forest, path, village stall, river; three
  NPCs; five target phrases), speech rules D-game-9, learning measurement D-game-10, full colour (D-game-6 amended), the Spanish reviewer
  deferred with a provisional workaround (D-game-12). The S series is built on the integration branch `feature/prototype`.

## 2. Repository layout

```
../Vocaterra.sln                 the solution for the whole repository: the core's projects (nlp/) and the game's (game/)
../Directory.Build.props         shared build settings: net10.0, nullable, warnings as errors, deterministic
../Directory.Packages.props      central versions for both halves (third-party packages only)
../nuget.config                  nuget.org only
../global.json                   the .NET SDK pin (10.0.402, latestPatch)
../nlp/                          the language core: its own plan, decisions, docs, content/v1, models manifest, scripts
Directory.Build.props            the game's additions: the Linux-only native trim (G7)
src/Vocaterra.Simulation/        the world and physics (no packages, no engine; net8.0 and net10.0)
src/Vocaterra.Game/              the engine-free game layer (references the core's projects and the simulation)
src/Vocaterra.MonoGame/          the MonoGame front-end (the only engine code)
src/Vocaterra.Host/              the console host: loads a bundle, evaluates lines, runs the text loop
tests/                           one test project per source project
content/game-v1/                 the game's content bundle (diverged from the core's sample, D-game-4)
assets/                          CC0 art, fonts and sounds (assets/ATTRIBUTION.md)
docs/                            SPECIFICATION (design), ARCHITECTURE (boundary), DECISIONS, research/
../scripts/content.sh            validate, hash, cefr-check, lint, pack, verify-pack through the core's content tool
../scripts/merge-gate.sh         the orchestrator's merge gate (both halves)
../.claude/agents/orchestrator.md   the orchestrator's role file (the entry point for a session)
../.github/workflows-pending/    the parked CI job (D-game-3, deferred)
```

The S series adds `src/Vocaterra.Simulation/Voxel/` (S1; the 2D simulation, generator and light map are removed at S4),
`content/game-v1/game/scene.json` (the hand-authored route), `content/PHRASES.md` (the phrase sheet, S5), `content/game-v1/speech/` (pre-rendered
lines, S9), `docs/PROTOTYPE_BRIEF.md` and `docs/SCENARIO_PLAN.md` (the brief and its plan) and `docs/research/noun-town/` (D-game-11).

## 3. Rules every agent follows

1. **The core as it is.** A game milestone edits `game/` only: no copied core source, no edit under `nlp/`. A missing call or a bug in
   the core is reported in the milestone's report with the call and the reason, and the orchestrator plans it as a core milestone.
2. **Determinism is the host's job.** The core is deterministic when the host supplies its inputs: the world seed, the stored time (Unix
   seconds, passed in), the learner model and the repeat counts (`RepeatCounts.Next`, core M19d). The host never draws its own random
   numbers, except world generation in `Vocaterra.Simulation`, which is a pure function of the world seed through its own seeded generator
   (`WorldRng`); never `System.Random`, never the clock, and no `Math.Sin` or `Math.Pow` in generation. Only the interactive text loop reads the clock; a `--transcript` run uses a fixed time. Dictionaries that reach output are
   ordered with `StringComparer.Ordinal`. Culture is invariant.
3. **Engine code only in `src/Vocaterra.MonoGame`** (D-game-1, later a Godot front-end of its own). Never in `src/Vocaterra.Host`,
   `src/Vocaterra.Simulation` or `src/Vocaterra.Game`. `Vocaterra.Simulation` takes no packages at all.
4. **Content procedure.** Every edit to `content/game-v1` (from the repository root): `scripts/content.sh validate`, then
   `scripts/content.sh hash game/content/game-v1 --bundle-id game-v1` (rewrites `manifest.json`), then `scripts/content.sh validate` and the tests.
   Files that come from the Wiktionary import keep `generatedBy: wiktionary-import` in `provenance.json` and their CC BY-SA notice in
   `ATTRIBUTION.md`. New hand-authored files are listed in `provenance.json` with `generatedBy: hand-authored`.
   A content milestone also runs `scripts/content.sh cefr-check game/content/game-v1 --level a1` and `--level a2`; the tool checks dialogue
   templates only (core finding), so prompt text is checked by hand.
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

### Handover (2026-10-21, end of session; the owner plans to merge the game and the core into one repository)

**Done, 2026-10-21 (later the same day):** the repositories are one (D-game-7). The core is `nlp/`, the game is `game/`; the notes below
were followed as listed (see the section 5 entry "One repository"). P8 and later are unblocked.

Where things stand (as written before the merge):
- **Done and pushed:** G0, G1, G2, R1, P1, P2, P3, P4, P4b, P6, G6, P6b, G7, P5, P7. Core pin `53c52b7` (M26a to M26e). Tests on `main`:
  625 (8 need the speech models in `VOCATERRA_MODELS_DIR`). Bundle `game-v1` `17ae6d14…`, 28 files. Licence MIT (D-game-2). No worktrees,
  no agent branches, no agent running.
- **The owner stopped after P7** (2026-10-21): P8 (the guide speaks), P9 (save and load), P10, G3 and G4 wait for the merged repository
  (D-game-7). The models P8 needs are fetched (`~/.local/share/vocaterra/models`: Vosk small es, Kokoro).
- **Core requests:** done in the core but not in the pin: CR-010, CR-011 (M26f, content tool), CR-015 (`UtterancePipeline.Advance` with
  `onHostEvent` transitions: the guide's review question can become a core turn), CR-017 (`Analyze` then `Complete`: the guide can evaluate
  once and drop the double evaluation). Accepted, not done: CR-014, CR-016, CR-018 (M28). Open: CR-019 to CR-022 (P5), CR-024 (fresh-clone
  pack). Each of these becomes ordinary work once the repositories are one.
- **Fresh clone today:** see `CLAUDE.md` (CR-024): the core's pack fails on a clean machine unless `external/Vocaterra.NLP/nuget.config`
  (uncommitted, `<clear/>` + nuget.org) exists first; after a pull that moves the submodule, `git submodule update --init`, delete
  `~/.nuget/packages/vocaterra.nlp.*` and pack again. The README's build steps (line 46) still lack this note.
- **Owner items still open:** the orchestrator defaults in the P4b entry (wrong names earn nothing; Esc ends a review); the ASR confidence
  choice in P5 (CR-022: Vosk asked for one hypothesis, so the core's confidence gate is off); live push-to-talk with real speech is untried;
  content pass items in the P4, P5, P6b and G6 entries (recast wording, "Habla más despacio" for an unknown intent, "¿de dónde sale la
  madera?" while carrying wood, "Usa mesa" without the article).

**Notes for merging the game and the core into one repository (D-game-7).** What exists only because they are two repositories, and what
to do with each:
1. *Packages only* (core ADR 0009, CLAUDE.md, section 3 rule 1): becomes project references inside one solution; `Directory.Packages.props`
   drops the `Vocaterra.Nlp.*` entries; the direct `Vosk` and `Microsoft.ML.OnnxRuntime` (`ExcludeAssets="native"`) references (P5,
   CR-021) are still needed in the executables and test projects that run Vosk and Kokoro.
2. *The submodule, the local feed and `nuget.config`*: remove `external/`, `.gitmodules` and the local source; `scripts/content.sh` installs
   the content tool from the feed (`.tools/`) and becomes a `dotnet run --project` of the core's ContentBuilder (or keep the tool, packed
   in-tree). The stale 0.1.0 cache problem (P4, CR-024) and the pack-before-build step disappear.
3. *The merge gate*: `scripts/merge-gate.sh` (game) and the core's `scripts/merge-gate.sh` become one; keep the game's package-folder
   isolation only if packages remain, the `VOCATERRA_MODELS_DIR` export, the content validation, and the core's own checks (licence scan,
   goldens).
4. *Two orchestrators and their files*: `.claude/agents/orchestrator.md` (game) and the core's role file become one; `docs/CORE_REQUESTS.md`
   rows that are still open become plan items; the two `IMPLEMENTATION_PLAN.md` files and the two `DECISIONS.md` files merge (the core's
   D1 to D13 and the game's D-game-1 to D-game-7; ids stay as they are).
5. *Determinism and replay*: unchanged; `HostDigest.Of` and the game's host slots stay as they are.
6. *Bundles*: `content/game-v1` (the game's) and the core's sample `content/v1` both stay; D-game-4 (diverge) still holds.
7. *Models*: `ModelStore` reads `manifest.json` from the models folder; in one repository the manifest path is fixed and CR-019's fallback
   (`VOCATERRA_MODELS_MANIFEST`, the submodule path in `SpeechEngine`) changes to the in-repo path.
8. *Licences*: the game is MIT, the core Apache-2.0 (core D3): the merged repository needs one decision (or per-directory licences).
9. *CI*: still deferred (D-game-3, core D1).

**Handover (2026-10-22, morning): the S series is done** on `feature/prototype` (pushed; `main` untouched). S0 to S12 and S4b merged
through the gate; core M29c, M29e, M29f, M29g, M29h merged; 3,889 tests pass (5 model-gated skips without models). Bundle `game-v1`
`cd63077b…` (253 files, 226 pre-rendered WAVs). The playtest: `--playtest` (mic check, pre-check, the route, post-check after the transfer
or F10, `playtest.save`, thank-you), recall with `--playtest --load <folder>/playtest.save`, `scripts/playtest-report.sh`, the private
linux-x64 folder from `scripts/playtest-build.sh` (no sherpa-onnx or ONNX Runtime native; Vosk model copied; 193 MiB).
**Waiting for the owner:** (1) listen to every WAV and mark `docs/SPEECH_CHECKLIST.md` (D-game-12 step 4); (2) the pre-flight in
`docs/PLAYTEST.md` section 12 — real keyboard, mouse look, E/T/V, push-to-talk with a real microphone, F10 and Esc were never exercised
by an agent (no input automation on this machine); (3) core D2: does handing the private build folder to testers count as distribution
(the WAVs were rendered by Kokoro through sherpa-onnx, whose native runtime carries GPL code; the folder ships only the managed wrapper);
(4) the native-speaker check stays deferred (D-game-12; rows provisional, reviewed by llm-opus); (5) whether to merge `feature/prototype`
into `main`; (6) freeze the bundle before testers' recall sessions (a save refuses another bundle hash). Proposed, not started: core M29b
(retry with the repair's request), M29d and M29i (fuzz findings), M28b (item-dependent replies: today "quiero pan" at the stall also gets
the bottle line), M28c, M28d.

**Now (2026-10-21, night): the S series is running** on `feature/prototype` (owner: through S12 overnight, push after each merge; the Opus
review of the phrase sheet and the content hash step approved; the owner's own steps, listening to the S9 WAVs and following the S12
checklist, wait for the owner). Design per milestone: section 4.4 and `docs/SCENARIO_PLAN.md`.

**Previously (2026-10-21, evening): the Prototype Design Brief.** The owner's brief (`docs/Vocaterra_Prototype_Design_Brief.docx`, v1.2) replaces the
Terraria design: a 3D first-person voxel slice on MonoGame (forest, path, village stall, river; two or three NPCs; five phrases; speech UX and
learning checks). The plan for it is `docs/SCENARIO_PLAN.md` (milestones S0 to S12, decisions D-game-8 to D-game-12, D-game-6 amended). The
orchestrator runs **S0** first: it folds that document into this section, records the decisions, and marks P8, P9, G3 and G4 superseded.
Until S0 is done, `docs/SCENARIO_PLAN.md` is the plan of record for the scenario.

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
| R1 | Research: Terraria-inspired prototype (guide NPC by text and speech, mining, building, movement, forest biome, map and cave generation, language progress tracking, known nouns in colour and unknown in greyscale, inventory) on MonoGame; findings in `docs/research/` | done (merged 2026-10-21, `0e8d601`); proposals await the owner | Opus 5.5 (owner's request) | G0 |
| S0 | Plan and docs for the prototype brief: D-game-8 to D-game-12, D-game-6 amended, this section, the brief as Markdown, READMEs, Noun Town research moved | done (merged to `feature/prototype` 2026-10-21, `0a60928`) | Haiku 5.5 (docs) | `docs/SCENARIO_PLAN.md` |
| S1 | 3D voxel simulation, headless (`Vocaterra.Simulation.Voxel`, `scene.json`, `PlayerBody3D`, `BlockRay`) | done (merged to `feature/prototype` 2026-10-21, `d2329cb`) | Sonnet 5.5 | S0 |
| S2 | First-person voxel front-end (mesher, renderer, camera, figures, beacon; 2D UI over the scene) | done (merged to `feature/prototype` 2026-10-22, `c5af3d6`) | Sonnet 5.5 | S1 |
| S3 | The scenario world and the guide in 3D (`WorldFacts3D`, the route layout, new nouns) | done (merged to `feature/prototype` 2026-10-22, bundle `5b9d0dab…`) | Haiku 5.5 | S2 |
| S4 | Gather, craft, the camp objective; retire the 2D slice (tag `2d-slice-p7`) | done (merged to `feature/prototype` 2026-10-22, bundle `1e13e231…`) | Haiku 5.5 | S3 |
| S4b | The `--greyscale` A/B switch in 3D (D-game-6 amended; S4 left a stub) | done (merged to `feature/prototype` 2026-10-22) | Haiku 5.5 | S4 |
| S5 | Content: `content/PHRASES.md`, the stall and the villager, `request_item`, constrained prompts; Opus review pass (D-game-12) | done (merged to `feature/prototype` 2026-10-22, bundle `c3cac003…`) | Sonnet 5.5 (design), Haiku 5.5 (content) | S0 |
| S6 | NPC exchanges in the world (`NpcService`), filling the bottle, the transfer moment | done (merged to `feature/prototype` 2026-10-22, bundle `eb4f0951…`) | Sonnet 5.5 | S3, S5 |
| S7 | Speech UX: mic check, cancel, watchdog, heard-vs-expected, buttons, choices, uncertainty rule, constrained prompts | done (merged to `feature/prototype` 2026-10-22, bundle `c3ad62fe…`) | Sonnet 5.5 | S6 |
| S8 | Phrase progress and fading scaffolds; the review extended to phrases | done (merged to `feature/prototype` 2026-10-22) | Sonnet 5.5 | S6 |
| S9 | NPCs speak: pre-rendered Kokoro lines in the bundle | done (merged to `feature/prototype` 2026-10-22, bundle `f5f28b88…`); **the owner listens to every WAV** (`docs/SPEECH_CHECKLIST.md`, D-game-12 step 4) | Haiku 5.5 | S5, S6 |
| S10 | Save, sessions, resume (P9 re-scoped) | done (merged to `feature/prototype` 2026-10-22) | Haiku 5.5 | S4, S8 |
| S11 | Checks (pre, post, recall), the session log and the report script | done (merged to `feature/prototype` 2026-10-22, bundle `cd63077b…`) | Haiku 5.5 | S8, S10 |
| S12 | Playtest build and facilitator checklist | done (merged to `feature/prototype` 2026-10-22); **the owner's pre-flight** (`docs/PLAYTEST.md` section 12) | Haiku 5.5 | S7, S9, S11 |
| S13 | The large world: 384 x 384 x 128 generated terrain, mountains, cliffs, the river and waterfall, sites, the player's cliff camp and trail, view-distance rendering (`docs/SCENARIO_PLAN.md` section 8) | done (merged to `feature/prototype` 2026-10-22) | Sonnet 5.5 | S12, D-game-14 |
| S14 | The look: code-drawn pixel-art textures, tree species, undergrowth, sun shading, AO, sky, clouds, haze, water, `--quality` presets | done (merged to `feature/prototype` 2026-10-22, `849fd52`) | Sonnet 5.5 | S13 |
| S15 | The village, props, humanoid characters, the reference-B dialogue panel, the HUD icons and recipe panel, the F5 view, the first-person arm without its outline | done (merged to `feature/prototype` 2026-10-22, `788f5a1`) | Sonnet 5.5 | S14 |
| S16 | The English guidance layer (D-game-13) and the data-driven lesson runner | done (merged to `feature/prototype` 2026-10-22, bundle `4be20d61…`) | Sonnet 5.5 | S12, D-game-13 |
| S17 | The opening at the cliff camp, the guide's 5 to 10 minute lesson, the ending; new phrases, checks, Opus pass, speech | done (merged to `feature/prototype` 2026-10-22, `b0ebf5f`, bundle `9582e14f…`); **the owner listens to the 72 new WAVs** (`docs/SPEECH_CHECKLIST.md`) | Sonnet 5.5 | S13, S16, D-game-15 |
| S17b | The talk key (V) shown in the dialogue in every state; the microphone check, the game's own errors and key hints in English (owner's playtest notes) | done (merged to `feature/prototype` 2026-10-22, `678e5a2`) | Haiku 5.5 | S17 |
| S19 | The learner's record: every spoken and typed input, the nouns and verbs used (from the core's word readings), the lessons completed; save version 2 (D-game-16) | done (merged to `feature/prototype` 2026-10-22, `c92e486`) | Haiku 5.5 | S17, D-game-16 |
| S20 | The book: the journal as an openable book with Nouns, Verbs, Phrases, Lessons and What I said pages (D-game-16) | done (merged to `feature/prototype` 2026-10-22, `ee2ec79`) | Haiku 5.5 | S15, S19 |
| S21 | F8: the in-game bug and feature report, saved as Markdown with a screenshot, game state, recent turns and the session log tail; local only (D-game-17) | done (merged to `feature/prototype` 2026-10-22, `917d2c4`) | Haiku 5.5 | S19, S20, D-game-17 |
| S18 | The playtest refresh (build, checklist, report, launcher message; the consent text mentions that the book keeps what the player said; the facilitator collects the F8 reports) | done (merged to `feature/prototype` 2026-10-22, `3c9a741`); **the owner's pre-flight** (`docs/PLAYTEST.md`) | Haiku 5.5 | S15, S17, S17b, S20, S21 |
| S22 | The second pass's known issues: an uncrowded conversation screen, the F10 label, measured session durations, a Vosk route step and test, thin pergola posts, the book in greyscale, the book item for old saves | done (merged to `feature/prototype` 2026-10-22, `8609183`) | Sonnet 5.5 | S18 |
| S23 | LanguageTool over the game's Spanish: strings extractor, first waiver round, the `si quiero` and `traveller` findings, an advisory gate step (required after the waiver round) | planned | Haiku 5.5 | S22, core M30, D10 |
| S24 | The audio system (mixer, ducking under the voices, silence while the microphone records), interaction and progress sounds; CC0 packs plus code, no music (D-game-18) | planned | Sonnet 5.5 | S23, D-game-18 |
| S25 | Ambient beds by place (birds, wind, river and waterfall, village, fire, dusk insects) and footsteps per surface | planned | Haiku 5.5 | S24 |

Each milestone ends with the gate in section 6 and a line in section 5. The S series is gated with `GATE_TARGET=feature/prototype
scripts/merge-gate.sh "S<nn>:<branch>"` from the `feature/prototype` checkout.

Prototype order (owner, 2026-10-21): G1 and G2 run alongside P1 and P2 (P1 and P2 do not use the packages); then P3, the smallest vertical
slice; then P4 to P9; P10 is optional. Details, demos and test approaches per milestone: `docs/research/PROTOTYPE_PLAN.md`; reasons and
sources: `docs/research/PROTOTYPE_RESEARCH.md`. G5 is replaced by D-game-1 and P2.

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
core's bundle format has `agents/`, not `npcs/`; check the core's `docs/PIPELINE.md` and the loader before adding any file kind); effects
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

The design of record is `docs/SCENARIO_PLAN.md`: section 2 maps the brief to the codebase, section 4 gives each milestone's deliverables and
demo, section 5 the determinism and boundary rules that carry over, section 6 the possible core gaps (M29a, M29b), section 7 verification.
The brief itself is `docs/PROTOTYPE_BRIEF.md`. Order: S0, then S1 beside S5 (disjoint folders), then S2, S3, S4, S6, then S7 beside S8 only if
their files stay disjoint (otherwise one after the other), S9, S10, S11, S12. P8, P9, G3 and G4 are superseded (section 4 table); the
blacksmith and spell material leaves `content/game-v1` in S5 (the core's sample keeps it). The route: spawn in a forest clearing with the camp
(a `mesa`, an unlit fire ring, the guide), a path south-east to the village (a stall with an awning, three houses, a signpost `Tienda / Río`),
the riverbank (water, a bridge). The five phrases: "¿Qué necesitas?", "Quiero agua, por favor.", "¿Quieres pan?", "Sí, por favor." /
"No, gracias.", "¿Dónde está el río?".

## 5. Findings log

Facts that shape the plan. Each one names its source; the core's own findings stay in the core's plan.

- **Origin of this repository.** The game design (`docs/SPECIFICATION.md`) moved here from the core on 2026-10-20 with its text unchanged
  (core M19c). The game-side text the core removed on 2026-10-21 (M19e) is kept here: the mechanics the core's design listed (spells, trade,
  quests, agent dialogue; sections 4.1 and 4.2 build on them), the engine and language questions (D-game-1 and the findings below), the
  open product questions (agent dialect, free conversation mode), the roadmap wording (the first target a host can use is the design's
  Phase 1), and the Terraria lineage note (XNA to MonoGame). The removed text was `docs/NOTES_FROM_CORE.md`, deleted at G0.
- **Engine.** The core's research report suggested Godot .NET; MonoGame, Bevy, macroquad and Ebitengine were the alternatives. The core is
  C# (ADR 0001). D-game-1 is open (`docs/DECISIONS.md`).
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

- **R1 (prototype research, 2026-10-21).** `docs/research/PROTOTYPE_RESEARCH.md` and `PROTOTYPE_PLAN.md` propose three new projects
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
  overrode `HostStateHash` with the bare simulation digest, against the core's contract (API.md section 4: a host's own digest must cover
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
  `content/game-v1` instead of `Vocaterra.sln` and check for project references into `nlp/src`. `SpeechEngine` looks for
  `nlp/models/manifest.json`. One orchestrator role file, one gate (`scripts/merge-gate.sh`, now also the core's licence scan), one parked
  CI file. `docs/CORE_REQUESTS.md` closed: CR-014, CR-016, CR-018 were already M28b and M28c; CR-019 to CR-022 became M28d; CR-010, CR-011,
  CR-015 and CR-017 (done in the core after the last pin) are now available to the game without a bump. Licences stay per folder (MIT
  here, Apache-2.0 in `nlp/`). `content/game-v1/ATTRIBUTION.md` reworded (the manifest's path) and the bundle re-hashed to `fa8fb030…`;
  the core's sample attribution keeps its old repository links because its hash is pinned in the goldens. After the merge: build 0
  warnings, 3,585 tests pass (25 skipped, with the models folder set), format clean, licence scan 28 of 28, the sherpa
  `libonnxruntime.so` still wins next to the executable (CR-021 holds through project references), the host and the content tool run
  from the root. The host now falls back to the bundle found above the executable when `content/game-v1` is not in the current directory.

- **The prototype brief (2026-10-21, S0, orchestrator).** The owner's brief v1.2 replaces the Terraria design (D-game-8). Plan:
  `docs/SCENARIO_PLAN.md`, folded into section 4 (table and 4.4); decisions D-game-8 to D-game-12 recorded, D-game-6 amended; core M29a and
  M29b proposed, conditional; the merge gate takes `GATE_TARGET` so the S series lands on `feature/prototype`. The
  docs part (Haiku 5.5): `docs/PROTOTYPE_BRIEF.md` extracted from the docx by a standard-library script (every paragraph and table
  cell present, headings set by hand; the docx has no heading styles), `SPECIFICATION.md` a pointer, Noun Town research moved, R1 notes,
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
  (no swimming; the bridge is not enforced); `Interact` checks reach but not line of sight (the front-end picks with `BlockRay`).
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
- **S9, the NPCs speak (2026-10-22, Haiku 5.5).** `scripts/prerender-speech.sh` with `game/tools/Vocaterra.SpeechRender` (the core's
  `KokoroSynthesizer`; re-runnable, an index outside the bundle) rendered 109 slot-free lines (88 dialogue variants, 6 prompt texts, 6
  word cards, 9 model answers) at speed 1.0 and 0.8: 218 WAVs, 16-bit mono 24 kHz, 16.8 MiB, in `content/game-v1/speech/`, provenance
  `model-output` (core M29g; model `kokoro-multi-lang-v1_0`, guide 28 `ef_dora`, shopkeeper 29 `em_alex`, villager 53 `em_santa`).
  `NpcVoice` (an `ILinePlayer` over `DynamicSoundEffectInstance`) plays each NPC line, `Repetir` and `Más despacio` (the slow file), and
  the word cards; silent without a device; `--mute`. Startup line `speech: 109 lines, 218 files, 0 missing` (checked by the orchestrator).
  Kokoro is constructed only for `--speak` (and the scripted mic check without a microphone). Content fix (D-game-9): not-communicated
  feedback is `SlowRepeat` with the help lines ("Di «ayuda» y te ayudo."), so "Casi. Piensa otra vez." no longer follows a recognition
  failure; the core plans that help line only when the verdict has an expected form, otherwise only "No te entendí. ¿Me lo dices otra
  vez?" (accepted). **Owner's step:** listen to every file and mark `game/docs/SPEECH_CHECKLIST.md` (cut-offs, text-audio mismatches).
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
  checked against the manifest; 519 files, 193 MiB; every check passes); `game/docs/PLAYTEST.md` (recruitment of beginners, consent and
  what is logged, the provisional content-trust note, setup, the session, a 5-minute rule before helping, observer notes, recall, the A/B
  arm, the gates and their evidence, the owner's pre-flight, the bundle freeze). The orchestrator moved the playtest-only runtime pin into
  `Directory.Packages.props` (the agent had used a `VersionOverride`). Fresh clone: builds with 0 warnings and passes 3,889 tests; the README
  now says the speech models must be fetched. Checked by the orchestrator: the playtest world frame (no overlay, F10 label). Tests 3,889.

- **The owner's playtest of S12 (2026-10-22).** "It's ok", with changes asked for: English guidance for a player with no Spanish (D-game-13),
  a more gradual start with a 5 to 10 minute guide lesson and a start alone at the player's own cliff camp (D-game-15), and a much larger,
  more realistic world with high mountains, dense forest and vegetation, close to the brief's reference images (D-game-14). The gap against the
  references (`docs/SCENARIO_PLAN.md` section 8): the S12 build has a dark grey sky, untextured dull blocks, a small flat map and cube figures.
  This machine renders through llvmpipe (a VM with a virtio GPU); the owner set a real GPU as the target, with presets. The launcher in
  `game/playtest/` is only meant to run from a built folder; its missing-binary message is unhelpful (S18 fixes it).

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
      VOCATERRA_MODELS_DIR (see nlp/scripts/fetch-models.sh). You can still type.`
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
    - `scripts/playtest-report.sh` lists and copies the reports. PLAYTEST.md's consent text names what a report holds.
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
    - PLAYTEST.md and the tester README describe the current game: the opening, the lesson, the 20-item checks, the book, F5, F6, F8, J,
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

## 6. Definition of done (every milestone)

1. `dotnet build Vocaterra.sln -warnaserror` with 0 warnings and `dotnet test Vocaterra.sln` green, with the test count in the report.
2. `dotnet format Vocaterra.sln --verify-no-changes` clean.
3. `scripts/content.sh validate` passes; after a content edit the bundle is hashed and its manifest committed; `cefr-check` is clean at A1 and A2
   for any content that adds templates.
4. The demos run (since S4 the 3D voxel world is the only world; the 2D demos retired with the slice, tag `2d-slice-p7`):
   the host `dotnet run --project src/Vocaterra.Host -- --transcript "quiero agua, por favor" --prompt stall-need` prints `request_item`,
   communicated, and `--transcript "quiero dos agua por favor" --prompt stall-need` prints `noun.number` (expected 'aguas'); the core demo
   (`nlp/README.md`) still yields exactly `conj.person` and `noun.number`. The game (needs a display, `DISPLAY=:0`): `dotnet run --project
   src/Vocaterra.MonoGame -- --seed 42 --look 120,-5 --say "¿dónde está el río?" --screenshot <scratch>/s3.png --frames 300` prints
   `ask_directions, entendido` and "El río está allí, a la izquierda, a sesenta bloques."; the PNG shows the dialogue panel and the beacon
   arrow. Each later milestone's demo is in `docs/SCENARIO_PLAN.md` section 4. Look at every PNG.
5. No violation of section 3: no copied core source, no edit under `nlp/` in a game milestone, nothing under `.github/workflows/`, no
   engine code outside `src/Vocaterra.MonoGame`.
6. `git status` is clean after the commit: no `bin/`, `obj/` or `nlp/artifacts/` output is staged.
7. The milestone's line in section 5 and its row in section 4 are updated by the orchestrator, not by the agent.
