# Implementation plan: Vocaterra (the game)

This is the plan for the game repository. The language core (`Vocaterra.NLP`, submodule `external/Vocaterra.NLP`) has its own plan and
orchestrator; this file never plans core internals. Owner decisions are in `docs/DECISIONS.md`. Status values: `planned`, `in progress`,
`in review` (on its branch, not yet merged), `done` (merged to main and gated), `blocked` (names the blocker).

## 1. Decisions taken

- **Packages only.** The game references the core's `Vocaterra.Nlp.*` 0.1.0 packages, never its projects, and never copies its source
  (core ADR 0009). A call the packages do not expose is a row in `docs/CORE_REQUESTS.md`.
- **The core is a git submodule** (`external/Vocaterra.NLP`, core D5). Its `scripts/pack.sh` fills `external/Vocaterra.NLP/artifacts/feed`,
  which `nuget.config` reads. No hosted feed. The pinned commit is `db39999` at setup (G0).
- **Console host until the engine is decided** (D-game-1). The host is engine-agnostic: no engine code under `src/Vocaterra.Host`.
- **Game words live here**, not in the core. The core knows effect kinds and host event names only as opaque strings.
- **Content tool:** the core's `vocaterra-content` 0.1.0 installed from the feed under `.tools/` by `scripts/content.sh`.
- **Model policy:** Haiku 5.5 by default, Sonnet 5.5 for design-heavy milestones, Opus only on the owner's request.

## 2. Repository layout

```
Vocaterra.sln                    the solution: the host and its tests (no core project is in it)
Directory.Build.props            build settings: net10.0, nullable, warnings as errors, deterministic
Directory.Packages.props         central versions: Vocaterra.Nlp.* 0.1.0 and the test packages
nuget.config                     the submodule's local feed, then nuget.org
global.json                      the .NET SDK pin (10.0.402, latestPatch)
external/Vocaterra.NLP/          the core, pinned; its artifacts/ and build output are git-ignored there
src/Vocaterra.Host/              the console host: loads a bundle, evaluates lines, runs the text loop
tests/Vocaterra.Host.Tests/      smoke tests: the demo utterances, the bundle hash, the carried conversation
content/game-v1/                 the game's content bundle (the core's sample for now; G2 and later replace it)
scripts/content.sh               validate, hash, cefr-check, pack, verify-pack through the core's content tool
scripts/merge-gate.sh            the orchestrator's merge gate
docs/                            SPECIFICATION (design), ARCHITECTURE (boundary), DECISIONS, CORE_REQUESTS
.claude/agents/orchestrator.md   the orchestrator's role file (the entry point for a session)
.github/workflows-pending/       the parked CI job (D-game-3, deferred)
```

## 3. Rules every agent follows

1. **Packages only.** No `ProjectReference` into `external/`, no copied core source, no edits inside `external/Vocaterra.NLP` (the
   orchestrator bumps the pin). A missing call or a bug in the core goes to `docs/CORE_REQUESTS.md` with a row and a reason.
2. **Determinism is the host's job.** The core is deterministic when the host supplies its inputs: the world seed, the stored time (Unix
   seconds, passed in), the learner model and the repeat counts (`RepeatCounts.Next`, core M19d). The host never draws its own random
   numbers, except world generation in `Vocaterra.Simulation`, which is a pure function of the world seed through its own seeded generator
   (`WorldRng`); never `System.Random`, never the clock, and no `Math.Sin` or `Math.Pow` in generation. Only the interactive text loop reads the clock; a `--transcript` run uses a fixed time. Dictionaries that reach output are
   ordered with `StringComparer.Ordinal`. Culture is invariant.
3. **Engine code only in `src/Vocaterra.MonoGame`** (D-game-1, later a Godot front-end of its own). Never in `src/Vocaterra.Host`,
   `src/Vocaterra.Simulation` or `src/Vocaterra.Game`. `Vocaterra.Simulation` takes no packages at all.
4. **Content procedure.** Every edit to `content/game-v1`: `scripts/content.sh validate`, then
   `scripts/content.sh hash content/game-v1 --bundle-id game-v1` (rewrites `manifest.json`), then `scripts/content.sh validate` and the tests.
   Files that come from the Wiktionary import keep `generatedBy: wiktionary-import` in `provenance.json` and their CC BY-SA notice in
   `ATTRIBUTION.md`. New hand-authored files are listed in `provenance.json` with `generatedBy: hand-authored`.
   A content milestone also runs `scripts/content.sh cefr-check content/game-v1 --level a1` and `--level a2`; the tool checks dialogue
   templates only (core finding), so prompt text is checked by hand.
5. **Scenes are design, not code.** A scene is intents, slot grammars, prompts, dialogue pools, agent state machines and effects, all in the
   bundle. The host maps the core's host events and effect kinds to game state; the core does not learn game words.
6. **Build hygiene.** `dotnet build Vocaterra.sln -warnaserror` and `dotnet format Vocaterra.sln --verify-no-changes` stay clean. Public
   types have `///` summaries. No new package outside `Directory.Packages.props`.
7. **Branches and commits.** One branch per milestone (`G<nn>-<name>`, `P<nn>-<name>` or the orchestrator's name), commit prefix `G<nn>: <title>` or `P<nn>: <title>`, trailers
   as the orchestrator's brief says. Agents do not push.

## 4. Milestones

### Handover (2026-10-21, after P4)

Where things stand, for the next orchestrator session:
- **Done and pushed:** G0, G1, G2, R1, P1, P2, P3, P4. Core pin `db39999` (no request is `done`). Tests on `main`: 329.
- **Next:** P4b (spaced colour and the guide's review, owner's amendment of D-game-6; Sonnet 5.5), then P6 (with the wider word list), P6b (crafting; owner 2026-10-21: crafting before caves, with stations), then P5 (spoken input; Sonnet 5.5) and P7; previously P5 or P6 (mining, building, inventory; Haiku 5.5). Both touch `src/Vocaterra.MonoGame`: one after
  the other. Then P7, P9; P8 waits on CR-006; P10 optional; G3 and G4 after P9.
- **Core requests:** the core accepted CR-002 to CR-008 and CR-010 to CR-013 into its M26a to M26f (2026-10-21); CR-014 (P4) is `open`.
  When rows turn `done`, bump the submodule; CR-012's fix (M26a) lets the guide drop `EvaluateCountingThisUtterance`, which is a
  G-milestone (it changes the calls the game makes).
- **Package cache (P4 finding):** NuGet caches by id and version, and the core's worktrees pack 0.1.0 into the shared `~/.nuget/packages`,
  so a game restore can silently use the core's unmerged work. The gate restores into `~/.nuget/vocaterra-game-packages` and clears the core
  packages there each run (`scripts/merge-gate.sh`); every brief sets `NUGET_PACKAGES` to that folder.
- **Owner items not yet answered:** (1) nothing in the prototype is gated behind language (orchestrator default); (2) `flecha`, `guía`,
  `bloque` (P3) and `algo`, `casi`, `eso` (P4) were added to the game's `cefr/a1.txt`: review in a content pass; (3) the linux-x64-only
  native-asset trim (saves about 1.5 GB per build); (4) D-game-2 (licence); (5) P4 credits the noun the player names even when it names the
  wrong thing (`es una piedra` with the cursor on a tree earns `piedra` a third, and the guide says "Casi. Mira: es un árbol."): keep or
  withhold (P4b withholds it by default: a `name_object` the guide marks wrong earns no colour).
- **How agents work now** (put this in every brief): no `git submodule update` and no core pack in a worktree; set
  `NUGET_PACKAGES=$HOME/.nuget/vocaterra-game-packages` and restore with
  `-p:RestoreSources="/home/user/Dev/Vocaterra/external/Vocaterra.NLP/artifacts/feed;https://api.nuget.org/v3/index.json"`, then build with
  `--no-restore`; `VOCATERRA_CONTENT_TOOL_DIR=/home/user/Dev/Vocaterra/.tools`; `scripts/content.sh hash` through a wrapper script outside
  the repository; screenshots need `DISPLAY=:0`.

| ID | Milestone | Status | Model | Depends on |
| --- | --- | --- | --- | --- |
| G0 | Repository setup: submodule at `db39999`, local feed, content tool, role file, requests file, gate, this plan | done (merged 2026-10-21) | Haiku 5.5 | core M19e |
| G1 | Adopt the core's M19d API: `RepeatCounts.Next` and `ConversationContext.Next` replace `NextRepeatCounts`; show `PromptSpec.Text` before each turn; README and ARCHITECTURE for the submodule route | done (merged 2026-10-21, `0eece5c`) | Haiku 5.5 | G0 |
| G2 | Re-sync `content/game-v1` with the core sample (prompt `text`, the generic intents, the blacksmith's `confirming` state, the stale `ATTRIBUTION.md` wording); decide D-game-4 | done (merged 2026-10-21, `1452c4c`); D-game-4 decided: diverge | Haiku 5.5 | G1 |
| P1 | Simulation skeleton and surface world: `Vocaterra.Simulation` (no packages, no engine, `net8.0;net10.0`), seeded `WorldRng` (SplitMix64), noise, 32 x 32 chunks, heightmap terrain, grass and trees, spawn, fixed 60 Hz `Step(commands)`, player gravity, walking, jumping, tile collision | done (merged 2026-10-21, `9e40db9`) | Sonnet 5.5 | D-game-1 |
| P2 | MonoGame front-end skeleton: `Vocaterra.MonoGame` (3.8.6 DesktopGL), no content pipeline, placeholder tiles drawn in code, camera, input to commands, CC0 font with the Spanish letters | done (merged 2026-10-21) | Sonnet 5.5 | P1 |
| P3 | **The guide, typed (smallest slice proving the core in game):** `Vocaterra.Game` (`GameSession`, `GuideService`, `WorldFacts`), the guide in the bundle (`ask_directions`, `ask_what`, `ask_help`), dialogue panel; `¿dónde está el árbol?` gets a reply and an arrow | done (merged 2026-10-21) | Sonnet 5.5 (scene and session API), Haiku 5.5 (content) | G1, G2, P2 |
| P4 | Nouns, word progress, greyscale: `game/nouns.json`, `lexicon/world.json`, `NounCatalog`, `WordProgress`, the D-game-6 rule, CPU-made grey atlas, `name_object` intent, journal | done (merged 2026-10-21, `00c6c44`) | Haiku 5.5 | P3 |
| P4b | Spaced colour and the guide's review (D-game-6 amended): growing gaps (10 min, then 1 h, or a new session), early uses logged without colour, the guide asks "¿Qué es esto?" about a due word with the pointer on its nearest example; scripted time skip for the demo | in progress (2026-10-21) | Sonnet 5.5 | P4 |
| P5 | Spoken input: push-to-talk, microphone (MonoGame `Microphone`, PortAudioSharp2 fallback), Vosk small, evaluation on a worker thread | planned | Sonnet 5.5 | P3 |
| P6 | Mining, building, inventory (durability, drops, hotbar placement, 40 slots with a 10-slot hotbar; the player starts with `hacha` and `pico`) and the **wider world of things** (section 4.3: new surface tiles and items, each in `game/nouns.json` with the colour rule) | planned, after P4b | Haiku 5.5 | P1, P2, P4b |
| P6b | **Crafting and the guide's crafting answers** (owner, 2026-10-21; section 4.3): recipes in `game/recipes.json`, stations (`mesa`, `horno`), crafting panel; the guide answers "¿cómo hago una mesa?" (recipe, station, what is missing) and "¿dónde hay arena?" (where a material comes from and the nearest source) | planned, after P6 | Sonnet 5.5 (design), Haiku 5.5 (content) | P6 |
| P7 | Caves (worms, cellular automata), ores, full forest vegetation, flood-fill lighting; ores `carbón`, `cobre`, `hierro`, `oro` join the catalogue and the recipes (`antorcha` from `madera` and `carbón`, metal tools at the `horno`) | planned | Haiku 5.5 | P1, P6, P6b |
| P8 | The guide speaks (Kokoro through the core) | blocked: CR-006 | Haiku 5.5 | P5, CR-006 |
| P9 | Save and load, playtest build and checklist | planned | Haiku 5.5 | P4, P6, P7 |
| P10 | Godot spike (optional): the same seed and guide turn in Godot 4 .NET; a note on the cost of the move | planned (optional) | Sonnet 5.5 | P4 |
| G3 | Innkeeper scene (section 4.1) | planned, after P9 | Sonnet 5.5 for the scene design, Haiku 5.5 for the content checklist | G2, P9 |
| G4 | Quest-giver scene (section 4.2) | planned, after G3 | Haiku 5.5 | G3 |
| R1 | Research: Terraria-inspired prototype (guide NPC by text and speech, mining, building, movement, forest biome, map and cave generation, language progress tracking, known nouns in colour and unknown in greyscale, inventory) on MonoGame; findings in `docs/research/` | done (merged 2026-10-21, `0e8d601`); proposals await the owner | Opus 5.5 (owner's request) | G0 |

Each milestone ends with the gate in section 6 and a line in section 5.

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

## 6. Definition of done (every milestone)

1. `dotnet build Vocaterra.sln -warnaserror` with 0 warnings and `dotnet test Vocaterra.sln` green, with the test count in the report.
2. `dotnet format Vocaterra.sln --verify-no-changes` clean.
3. `scripts/content.sh validate` passes; after a content edit the bundle is hashed and its manifest committed; `cefr-check` is clean at A1 and A2
   for any content that adds templates.
4. The host demo runs: `dotnet run --project src/Vocaterra.Host -- --transcript "yo tiene dos espada" --prompt offer-items-open` prints the
   two errors `conj.person` and `noun.number`; the README demo for the milestone's scene runs as written.
   From P3 on, also the game demo (needs a display, `DISPLAY=:0`): `dotnet run --project src/Vocaterra.MonoGame -- --seed 42 --say
   "¿dónde está el árbol?" --screenshot <scratch>/demo.png --frames 240` prints `ask_directions, entendido, 100 %` and the guide's line
   `El árbol está allí, a la derecha, a ocho bloques.`, and the PNG shows the dialogue panel, the arrow and the marker on the trunk. Look at
   the PNG. From P4 on, also `--seed 42 --point-nearest árbol --say "es un árbol"` three times with `--screenshot <scratch>/known.png
   --frames 300` prints `known: el árbol`, and the PNG shows the trees in full colour, the rest grey, and the "¡Nueva palabra: el árbol!"
   toast; one `--say` gives trees a third coloured.
5. No violation of section 3: no project reference or copied source from the core, no edit under `external/`, nothing under
   `.github/workflows/`, no engine code outside `src/Vocaterra.MonoGame`.
6. `git status` is clean after the commit: no `.tools/`, `bin/`, `obj/` or submodule build output is staged.
7. The milestone's line in section 5 and its row in section 4 are updated by the orchestrator, not by the agent.
