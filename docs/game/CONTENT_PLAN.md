# Content as data and the L1 locale layer (the C series)

A plan written on 2026-10-27, at the owner's request. It has three goals:
- make `content/` easier to manage;
- move the learner's native language (L1) out of the code and the target-language data, into locale data;
- build objectives, NPCs and target-language words from content data, as the ground for custom authoring tools later.

**Status:** proposed. Nothing in this plan is implemented, and the milestones are not yet in `IMPLEMENTATION_PLAN.md` or `DECISIONS.md`.

**The owner's answers (2026-10-27):**
- English moves into a locale layer, but no second locale is added yet.
- Content stays JSON, with JSON Schema added.

## 1. The request

> Investigate how `content/` can be made easier to manage and how it can be improved. Also investigate how we load any content currently and
> anything that is hardcoded within the game code. [...] the native language for Vocaterra is currently English, and that means English
> words are hardcoded in the code, but what if someone speaking French wanted to learn Spanish? [...] `Objective.cs` is a hardcoded
> objective, really we want to be able to construct objectives with content data and load them in. My long term plan is some supporting
> custom tools that allow us to build objectives, NPCs, etc.

## 2. What the investigation found

### 2.1 How content loads today

- **One bundle, one loader.** There is one bundle, `content/game-v1`, and its `manifest.json` pins the sha256 of every file.
  `GameBundle.Load()` (`src/Vocaterra.Game.Core/Session/GameBundle.cs`) calls the core's `ContentLoader.Load`.
- **The game can add folders with no core change.** The core parses only its own files (intents, prompts, dialogue, agents, answer-sets,
  lexicon, cefr, effects, feedback, evaluation). Any other file in the manifest is a **host file** (`ContentPaths.IsLoaderOwned`,
  `src/Vocaterra.Nlp/Content/ContentPaths.cs:49`), and the game reads it through `ContentBundle.TryReadHostFile`. So new folders such as
  `locale/` and `game/objectives/` need nothing from the core.
- **Seven separate loaders.** Each game file has its own hand-written `JsonDocument` loader, created at its own call site, and each does
  its own cross-checks. No single place loads and checks all of them.

  | Loader | File | Created at |
  | --- | --- | --- |
  | `Language/Words/NounCatalog.cs` | `game/nouns.json` | `Vocaterra.Host/Program.cs:60` |
  | `Items/RecipeBook.cs` | `game/recipes.json` | `VoxelSession.cs:59`, `NpcService.cs:230` |
  | `Language/Phrases/PhraseSheet.cs` | `game/phrases.json` | `GuideSession.cs:138`, `NpcService.cs:232`, `CheckBank.cs:86` |
  | `Tip/EnglishText.cs` | `game/english.json` | `GuideSession.cs:139` |
  | `Language/Words/VerbGlosses.cs` | `game/glosses.json` | `GuideSession.cs:140` |
  | `Language/Lessons/LessonBook.cs` | `game/lessons.json` | `GuideSession.cs:141` |
  | `Language/Checks/CheckBank.cs` | `game/checks.json` | `Screens/CheckBattery.cs:131` |

- **The scene skips the hash check.** `scene.json` (JSON with comments) is read with `File.ReadAllText` (`World/Layout/SceneLayout.cs:125`
  via `Utilities/SceneLocator.cs:7`), not through the hash-checked host-file path.
- **`GrammarStringsExtractor` bypasses the bundle.** `Vocaterra.Host/GrammarStringsExtractor.cs:59-61` reads english.json, checks.json
  and speech-index.json straight from disk.
- **Every edit costs several steps.** After any edit: `content.sh hash`, validate, the full suite, maybe re-recording goldens, and updating
  the bundle hash in `tests/Vocaterra.Game.Tests/Fixtures/save-v1.json`. The sandbox refuses the word `hash`, so that step needs a wrapper.

### 2.2 English (L1)

**In content, English sits inside the Spanish data.**
- **`game/english.json`** (25 KB, `EnglishText`) holds the UI words, the controls, the tips, the objective and noun glosses, the phrase
  explanations, the feedback and the NPC line glosses. The line glosses (`lines`) are keyed by **normalised Spanish text**, not by line id,
  so editing a Spanish line silently drops its gloss.
- **Inline English in other files:**
  - `lessons.json`: `title`, `english`, `summary`, `objective`, `translation`, `help`, `retry`, `hud`, `options`;
  - `phrases.json`: `english`, `card`;
  - `glosses.json`: `english`, `third`, `past`;
  - `checks.json`.

**In code, about 120 player-facing English literals in the UI** (paths under `src/Vocaterra.Game` unless marked Core).

| Area | Where |
| --- | --- |
| HUD and toasts | `UI/HudLayers.cs:127,201-221`, `UI/VoxelTalkLayers.cs:15`, `UI/LoadingLayer.cs:15`, `UI/VoxelLayers.cs:117`, `Graphics/GreyscaleWorld.cs:83`, `Flow/ObjectiveAnnouncer.cs:20,26`, `VoxelGame.cs:823,827`, Core `Session/VoxelSession.cs:86` |
| Dialogue panel | `UI/Dialogue/DialoguePanel.cs:21-45,222`, `Screens/DialogueScreen.cs:295`, `UI/DialogueModel.cs:554` |
| Inventory, crafting, workbench | `UI/InventoryLayer.cs:16-22,55`, `UI/CraftingLayer.cs:17-98`, `UI/CraftingScreen.cs:26-29`, `UI/RecipePanelLayer.cs:17-23` |
| Book and journal | `UI/Book/Book.cs:28-37`, `UI/Book/BookLayer.cs`, `UI/Book/BookPages.cs`, `UI/Book/BookPractise.cs:13`, `UI/Book/ConjugationTable.cs:66-68`, `UI/Journal.cs:30,45-53`, `UI/PhraseJournal.cs:39-51,107` |
| Lessons, tips, controls | `UI/LessonStrip.cs:359`, `UI/English/TipLayer.cs:42`, `UI/English/ControlsLayer.cs:56,62` |
| Checks, mic check, playtest | `UI/CheckLayer.cs`, `Screens/CheckBattery.cs:168,372`, `Sound/MicCheckModel.cs:38-59,105`, `UI/MicCheckLayer.cs:97`, `Screens/MicCheckScreen.cs:186`, `Flow/PlaytestFlow.cs:25,28,131` |
| Report dialog | `UI/ReportLayer.cs:17-26,63-64`, `UI/ReportPanel.cs:85,103`, `Reporting/ReportScreen.cs:257,263`, Core `Reporting/ReportKind.cs:29-30` |

**Logic that assumes English as L1:**
- **Names:** `EnglishText`, the `UI/English/` folder, `DialoguePanel.EnglishPrefix`, `ObjectivePieceKind.English`, the JSON keys
  `english`/`spanish`, and the required keys kept as a C# list (`EnglishText.UiKeys`).
- **English grammar in code:** `UI/Book/BookVerbLabels.cs:23-156` builds English verb forms by rule (do/does, -s/-es/-ies, was/were,
  let's), and `Language/Words/VerbGlosses.cs:74` requires every gloss to start with "to".
- **Key names in the text:** "Hold V", "W A S D", "Enter goes on, H helps". The text breaks when keys are remapped or on an AZERTY keyboard.

**Rendering limits:**
- One font with no fallback (`assets/fonts/PixelOperator.ttf`, `UI/FontProvider.cs`), and its glyph coverage is unchecked.
- Wrapping breaks only at ASCII spaces (`UI/DialogueModel.cs:618-660`), and `Graphics/DrawHelpers.cs:105-119` cuts text by character
  index.
- Typing offers only the Spanish accents (`UI/TextEntry.cs:156-176`) and refuses characters outside the basic plane (`:47`).

**Developer text stays English and is out of scope:** the command-line usage and errors (`Utilities/LaunchOptions.cs`), the host console
(`Vocaterra.Host/Program.cs`, `VerdictReport.cs`), exceptions, logs, the session log, and the headings of the saved report file
(`Core/Reporting/ReportMarkdown.cs`).

### 2.3 Spanish (target language) in code

CLAUDE.md says the game's words live in content. These do not:

| What | Where |
| --- | --- |
| NPC labels | Core `Npc/NpcNames.cs:9-11` ("Guía", "Tendero", "Aldeano"; this duplicates `npcNames` in english.json), `UI/Dialogue/DialoguePanel.cs:42`, `UI/DialogueModel.cs:110,187` |
| The guide's name | "Luna" at Core `Session/VoxelSession.cs:188`, but "Rosa" in a comment at `Language/Phrases/PhraseReview.cs:31` |
| UI labels | `UI/SpeechPanel.cs:79-91` (Repetir, Más despacio, Ayuda, No sé, Escribir), `UI/DialogueModel.cs:80`, `UI/SupportLayer.cs:20-26`, `UI/Journal.cs:27`, `Flow/BeaconDirector.cs:77` |
| Place words | `UI/PointLabels.cs:13-39` (río, tienda, casa, campamento, mi campamento, cascada) |
| Directions and joiners | Core `Npc/WorldFacts.cs:44-53,331`, `Npc/NpcService.Guide.cs:33` ("bloque"), `:206` (" y ") |
| Pronouns | `UI/Book/ConjugationTable.cs:55-59`, `UI/Book/BookVerbLabels.cs:88` |
| Words in logic | Core `Session/GuideSession.cs:77,80` ("ayuda", "no sé"), `:263` (`KeyWords = ["agua","pan"]`), `:745` ("¿Quieres agua?"); `Language/Speech/MicCheck.cs:18` and `Npc/NpcService.cs:324` ("hola"); `Session/VoxelSession.cs:234,268,271,404,433,485` (río, agua, fuego, antorcha); `Npc/NpcHostEvents.cs:84,172` |
| Sign text | `Graphics/BlockArtVillage.cs:46-59,211-220` (a 3x5 pixel font with only the letters it needs, plus accent and tilde hacks), Core `World/Generation/SceneBuilder.cs:565-579`, `Voxel/BlockId.cs:153-159`, `Voxel/BlockDefinition.cs:85-88` |

### 2.4 Game design hardcoded

**The camp objective** (`Core/Session/Objective.cs`):
- `Objective.Camp()` fixes the step keys, the Spanish labels (madera, frutas, antorcha, agua), the targets, the title `Campamento` and the
  event `objective.water`.
- Its triggers are C# methods: `RecordPickup` switches on `ItemId`, and there are `RecordTorchPlaced` and `RecordHostEvent`.
- `VoxelSession.Step` (`:243-283`) wires simulation events to it by hand.
- `EnglishText.Parse` (`:211`) calls `Objective.Camp()` to check the glosses.
- The save keeps steps by key (`Save/SaveGame.cs:151,330`, `Save/SaveMigration.cs:222`).

**The session flow is a hardcoded quest chain.** `SessionStage` (FindGuide, Camp, LightFire, Complete; `Session/SessionStage.cs`) is worked
out in `VoxelSession.Stage` (`:93`) from `_guideMet`, `Objective.Complete` and `_fireLit`. The chain also hardcodes:
- the closing pool `guide.lesson.end`;
- the points `firering`, `firering.player` (`VoxelSession.cs:399,529-530`, `Sound/AmbientBeds.cs:79`) and `camp.guide`
  (`Flow/BeaconDirector.cs:17`);
- the Book granted at the start (`VoxelSession.cs:66`).

**Three trigger systems, each built its own way:**
- **tips** use `when` (`start`, `aimed_npc` or a world event; `EnglishText.TipTriggers`);
- **lesson `do` steps** use `event` + `detail` + `point` (`LessonBook.cs:455`);
- **the objective** uses C# methods.

**NPCs:**
- `Npc/NpcHostEvents.cs:48-86` holds the host-event-to-effect table (give a bottle, a bread or water; point at "río").
- `:165-213` holds the intent-to-phrase, prompt-to-scaffold and phrase-to-home tables.
- Agent and prompt ids are fixed in `Npc/NpcService.cs:116-158`, `Language/Speech/SpeechLines.cs:14-20` and `LaunchOptions.cs:26`.
- Figures are fixed in `Graphics/NpcFigures.cs:129-174`.
- The phrase-to-pool table is fixed in `Language/Phrases/PhraseReview.cs:22-30`.

**Items, blocks and sounds:**
- the `ItemId` enum and the `ItemDefinition` table (`Items/ItemDefinition.cs:9-53`; numeric ids are saved, so they must stay stable);
- `Voxel/BlockDefinition.cs:39-88` (about 50 rows), and `Graphics/BlockAtlasLayout.cs:343` for the art;
- `Sound/SoundCatalog.cs:36-88`, and footsteps per block (`Sound/Footsteps.cs`).

**Lessons:** `LessonBook.cs:142` (`NearFuture = "ir-a"`) and `LessonRunner.cs:956-1004` (checks for "mañana", lemma "ir" + "a").

## 3. The direction

### 3.1 Two language roles, named apart

- **The target language** (Spanish) belongs to the core: its language pack, `ContentBundle.Language` and the manifest's language id (M32).
- **The support language** (L1) belongs to the game, as a locale.

Target-language data never carries L1 text; it carries ids. L1 text lives in `content/game-v1/locale/<code>/*.json` string tables keyed by
stable ids. Examples:

| Id | `en` |
| --- | --- |
| `ui.continue` | Continue |
| `lesson.first-words.title` | First words with Luna |
| `lesson.first-words.u1-say-hola.text` | Say hello back: “Hola” (OH-lah). Hold {key:talk} to speak into the microphone, or press {key:type} to type. |
| `phrase.pedir-agua.meaning` | I want water, please. |
| `line.guide.lesson.hello.1` | Hello. |
| `noun.madera` | wood |
| `feedback.error.conj.person` | ... |
| `npc.shopkeeper.role` | The shopkeeper |
| `verb.tener` | `{ "base": "to have", "third": "has", "past": "had" }` |

`locale/locales.json` lists the locales (code, name, the glyph set each needs). Only `en` ships for now. NPC lines are keyed by the dialogue
variant's line id (the speech index already names them), not by their Spanish text.

### 3.2 `LocaleText` replaces `EnglishText`

- **Lookup:** the chosen locale, then `en`, then a visible `⟦key⟧` marker, so a missing string shows on screen and is never empty.
- **Placeholders:** `{name}` stays. `{key:talk}` is filled from the input map, so remapped keys and other keyboard layouts show the right
  key.
- **Choosing the locale:** `--locale <code>` or the settings file; `en` by default.
- **Verb forms:** L1 verb forms are listed in the locale (base, third, past). `BookVerbLabels`' English rules and the "to" check go.
- **Renames:** `ObjectivePieceKind.English` becomes `Support`, and the `UI/English/` folder becomes `UI/Support/`.
- **Tests:**
  - every id that content or code refers to exists in every shipped locale, and no locale has orphans;
  - every glyph of every locale's strings is in the font;
  - a scan finds no player-facing string literals in `src/Vocaterra.Game/UI`, `Screens` and `Flow`, with an allowlist for developer text.

### 3.3 Target-language words out of code

`game/words.json` holds the words the game itself uses in the target language: directions, the speech-button labels, the support headings,
pronoun labels, `KeyWords`, the help phrases (`ayuda`, `no sé`), the block unit and the list joiner. NPC labels and names go into the NPC
data (3.6). Place labels and sign text go into the scene's sites. This is also what lets a later target language (core M33 onwards) use the
game without code changes.

### 3.4 One trigger model

A `Trigger` record is a world event (`WorldEvents`, plus `start` and `aimed_npc`) with optional filters: `item`, `noun`, `npc`, `site`,
`host` (a host event name) and `count`. One matcher serves tips, lesson `do` steps and objectives:

```json
{ "event": "item_picked_up", "noun": "madera" }
{ "event": "block_placed", "item": "torch", "site": "firering" }
{ "event": "host", "host": "objective.water" }
{ "event": "talk_opened", "npc": "guide" }
```

### 3.5 Objectives as data

`game/objectives/<id>.json`, one file each. The camp objective as data:

```json
{
  "$schema": "../../../schema/objective.schema.json",
  "id": "camp",
  "title": { "target": "Campamento", "noun": null },
  "requires": ["meet-guide"],
  "steps": [
    { "id": "wood",  "noun": "madera",   "target": 5, "trigger": { "event": "item_picked_up", "noun": "madera" } },
    { "id": "berry", "noun": "fruta",    "target": 3, "trigger": { "event": "item_picked_up", "noun": "fruta" } },
    { "id": "torch", "noun": "antorcha", "target": 1, "trigger": { "event": "block_placed", "item": "torch", "site": "firering" } },
    { "id": "water", "noun": "agua",     "target": 1, "trigger": { "event": "host", "host": "objective.water" } }
  ],
  "onComplete": { "worldEvent": "objective_complete" }
}
```

- **Labels and titles.** A step's checklist label is the noun's plural or singular from the catalogue, or explicit `label` text. Its L1
  gloss comes from the locale (`noun.<lemma>`), and fades once the noun is known (D-game-6), as it does today.
- **The chain.** The session flow becomes four objectives, `meet-guide` → `camp` → `light-fire` → `done`, linked by `requires`.
  `SessionStage` and the stage lines come from the active objective (its locale line `objective.<id>.hud`). The closing line becomes the
  `light-fire` objective's `onComplete.pool`.
- **Saves.** A save keeps the counts by `objective.step` id. Save v3 maps the old keys (`wood` becomes `camp.wood`) through the existing
  `SaveMigration`.
- **What stays in code.** The simulation still raises the events. Only the mapping from events to steps moves into data.

### 3.6 NPCs and host events as data

`game/npcs/<id>.json` gives:
- the agent file (`agents/<id>.json`, the core's);
- the target-language label (`Guía`) and the personal name (`Luna`);
- the placement site, the voice and the figure colours;
- the opening host event and the prompts it uses.

The L1 role name lives in the locale (`npc.<id>.role`). `game/host-events.json` replaces the `NpcHostEvents` tables:

```json
{ "npc": "shopkeeper", "event": "stall.give_water", "effect": { "kind": "give_item", "item": "bottle" } }
{ "npc": "villager",   "event": "villager.point",   "effect": { "kind": "point_at_noun", "noun": "río" } }
```

The phrase tables (intent to phrase, prompt to scaffold, phrase to home NPC, phrase to review pool) move onto the phrase or NPC data.

### 3.7 Easier management

- **JSON Schema** for every host-file kind, in `content/schema/`. Each file names its schema in `$schema`, so Rider and VS Code validate and
  autocomplete as you type. A test validates every file against its schema.
- **Split the large files:** `game/lessons/<id>.json`, `game/objectives/<id>.json`, `game/npcs/<id>.json`, and
  `locale/en/{ui,tips,controls,feedback,lessons,phrases,lines,nouns,verbs}.json`.
- **`GameContent.Load(bundle)`:** one call builds the whole object graph (nouns, recipes, phrases, lessons, checks, glosses, the locale,
  objectives, NPCs, host events, and the scene, which is now hash-checked). It runs every cross-check in `GameContentValidator`: noun refs,
  phrase refs, line ids, locale ids, trigger names, objective `requires` with no cycles, and NPC agent files. Call sites take `GameContent`
  instead of loading files themselves. The host's `--validate-content` runs it from the command line.
- **`scripts/content.sh refresh`:** hash, validate, lint `--strict --level a1`, schema and cross-checks in one command. It avoids the
  sandbox word, and no step can be forgotten.
- **`content/README.md`:** what lives where; who owns what (the core's files and the game's host files); how to add an NPC, an objective,
  a lesson or a locale string; and the edit-then-refresh loop.

### 3.8 The way to custom tools

The schemas, `GameContent` and the validator are the contract any tool builds on.
- **First tool:** an authoring CLI with `new npc|objective|lesson` (scaffolds a schema-valid file and its locale entries), `refs <id>`
  (where an id is used) and `check` (the validator).
- **Later:** a GUI editor (objectives, NPCs, lessons), whose technology stays open. It reads and writes the same files through the same
  schemas.

## 4. Milestones

Branch `C<nn>-<name>`, commit prefix `C<nn>:`, gated into a feature branch (`feature/content`), never `main`.

| ID | What | Model | Depends |
| --- | --- | --- | --- |
| C01 | Content contract: JSON Schemas for every `game/*.json`, `$schema` refs, a schema test, `content/README.md`, `content.sh refresh` | Haiku | none |
| C02 | `GameContent` + `GameContentValidator`: one load of every host file (the scene now hash-checked; `GrammarStringsExtractor` reads through it), the cross-checks moved there, host `--validate-content` | Sonnet | C01 |
| C03 | Locale layer for content (`en` only): `locale/en/*.json` keyed by id; `LocaleText` replaces `EnglishText`; the L1 fields leave lessons, phrases, glosses and checks; NPC lines keyed by line id; verb forms listed | Sonnet | C02 |
| C04 | The English literals of the UI into the locale (section 2.2), `{key:…}` placeholders, the no-literal scan, the glyph test | Haiku | C03 |
| C05 | Target-language words out of code (`game/words.json`, place labels, sign text as data; section 2.3) | Haiku | C03 |
| C06 | One trigger model shared by tips and lesson `do` steps (ready for objectives); lessons split one file each | Haiku | C02 |
| C07 | Data-driven objectives and the session chain from data; `Objective.Camp()` and the `SessionStage` logic retired; save v3 + migration | Sonnet | C05, C06 |
| C08 | NPCs and host events as data (`npcs/*.json`, `host-events.json`); `NpcNames`, the `NpcHostEvents` tables, `PhraseReview` and the figure table retired | Haiku | C05 |
| C09 | Items, blocks and sounds as data with stable numeric save ids (assess first; parts may stay code) | proposed | C08 |
| C10 | Authoring CLI: scaffold, references, check (the first custom tool) | proposed | C07, C08 |

**Every content-moving milestone:**
- runs `content.sh refresh`, then the whole suite (the core runs on `game-v1`, R02);
- re-records the end-to-end goldens if they moved;
- updates the bundle hash in `tests/Vocaterra.Game.Tests/Fixtures/save-v1.json`;
- does one run with models.

C03 to C08 change no behaviour. The scripted runs (`docs/game/SCRIPTED_RUNS.md`) and screenshots show the same text before and after.

## 5. Risks and open points

- **Save migration (C07).** Old saves keep step keys without an objective id. The migration must map them, and the save fixtures need new
  versions.
- **Golden churn.** Moving the English fields out of `phrases.json` and `lessons.json` changes the bundle hash and maybe the goldens. Do
  each move in one milestone, not spread out.
- **`PHRASES.md`.** D-game-10 keeps it as the source of truth, and `PhraseSheetTests` checks it against `phrases.json`. Once the English
  moves to the locale, the test must read the meaning from there. Whether `PHRASES.md` should become generated from data is an owner
  decision.
- **Font and scripts.** PixelOperator's coverage limits which locales can ship, and the glyph test makes that visible. Wrapping and cutting
  assume a script with spaces; a CJK locale would need line-breaking work first (out of scope).
- **Decision record.** When the owner adopts this plan: add it as D-game-21 in `DECISIONS.md` and section 4.7 of `IMPLEMENTATION_PLAN.md`,
  record the "Luna"/"Rosa" mismatch and the unchecked scene load in section 5, and add a line to `CLAUDE.md`.
