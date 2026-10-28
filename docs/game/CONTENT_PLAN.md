# Content as data and the L1 locale layer (the C series)

A plan written on 2026-10-27, at the owner's request, and revised on 2026-10-28 after a review against the code. It has three goals:
- make `content/` easier to manage;
- move the learner's native language (L1) out of the code and the target-language data, into locale data;
- build objectives, NPCs and target-language words from content data, as the ground for custom authoring tools later.

**Status:** proposed. Nothing in this plan is implemented, and the milestones are not yet in `IMPLEMENTATION_PLAN.md` or `DECISIONS.md`.

**The owner's answers:**
- English moves into a locale layer, but no second locale is added yet (2026-10-27).
- Content stays JSON, with JSON Schema added (2026-10-27).
- The locale is a bundle of its own, `content/locale/`, beside `content/game-v1` and outside its hash (2026-10-28; section 3.1).
- R16 phase 2 (`GAME_PROJECTS_LAYOUT.md`) runs first, and the series starts on its layout (2026-10-28; section 4).

**What the review changed (2026-10-28):**
- The locale moved out of the hashed bundle, so an L1 edit no longer touches saves, fixtures or the core's suite. game-v1's
  `supportLanguage` names the default locale.
- Objectives keep today's order-free progress, their side effects are named code hooks, and the save goes to v5, not v3.
- Triggers read world events only, with one new event for the fire rings. The NPC data is trimmed to what no other file holds.
- The per-milestone checklist gained provenance, the grammar strings and a scripted-run compare with the bundle hash masked. Wrong
  facts, paths and line numbers were corrected.

Paths are as of 2026-10-28, before R16 phase 2. R16 moves many of the files named here (its target tree is in
`GAME_PROJECTS_LAYOUT.md`), so each milestone brief finds them again by symbol.

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
- **The core hashes everything.**
  - `ContentManifest.Compute` hashes every file under the bundle, the game's files and the speech WAVs included. The loader refuses a
    file that is not in the manifest. So any edit changes the bundle hash.
  - A save records that hash. Under `--strict-save` and `--playtest`, `SaveGame.Check` refuses a save made with another hash; the
    default mode carries the progress over through `SaveMigration`.
- **Every file needs provenance.** `provenance.json` must hold an item for each file in the bundle, or the load fails ("no provenance
  item for", `ContentValidator`). `content.sh hash` does not add these items. `provenance.json` and `ATTRIBUTION.md` are core files.
- **The game can add folders with no core change.**
  - The core parses only its own files: intents, prompts, dialogue, agents, answer-sets, lexicon, cefr, effects, feedback, evaluation,
    provenance and attribution.
  - Any other file in the manifest is a **host file** (`ContentPaths.IsLoaderOwned`, `src/Vocaterra.Nlp/Content/ContentPaths.cs:49`),
    which the game reads through `ContentBundle.TryReadHostFile`. So new folders such as `game/objectives/` need nothing from the core.
  - NPC files must stay out of `agents/`, where the core parses every `.json` at any depth as an agent.
  - With hash checking off, `TryReadHostFile` serves nothing. The ContentBuilder's lint and grammar commands load that way, and the
    ContentBuilder references only the core. So checks of the game's files have to run from the host, not from the ContentBuilder.
- **Seven separate loaders.** Each game file has its own hand-written `JsonDocument` loader, created at its own call site, and each does
  its own cross-checks. No single place loads and checks all of them.

  | Loader | File | Created at |
  | --- | --- | --- |
  | `Language/Words/NounCatalog.cs` | `game/nouns.json` | `src/Vocaterra.Game/Program.cs:60` |
  | `Items/RecipeBook.cs` | `game/recipes.json` | `VoxelSession.cs:59`, `NpcService.cs:230` (a fallback) |
  | `Language/Phrases/PhraseSheet.cs` | `game/phrases.json` | `GuideSession.cs:139`, `NpcService.cs:232`, `CheckBank.cs:86` |
  | `Tip/EnglishText.cs` | `game/english.json` | `GuideSession.cs:140` |
  | `Language/Words/VerbGlosses.cs` | `game/glosses.json` | `GuideSession.cs:141` |
  | `Language/Lessons/LessonBook.cs` | `game/lessons.json` | `GuideSession.cs:142` |
  | `Language/Checks/CheckBank.cs` | `game/checks.json` | `Screens/CheckBattery.cs:131` |

- **The scene is read twice.**
  - `scene.json` is in the manifest, so the bundle load checks its hash.
  - The game then parses a second read from disk (`SceneLayout.Load`, `World/Layout/SceneLayout.cs:125`, via
    `Utilities/SceneLocator.cs`), and `--scene` skips the check entirely.
  - The file is JSON with comments (32 `//` lines).
- **The speech WAVs are read twice too.** They are hashed at load, then read again from disk by the core's `StaticLineStore`
  (`src/Vocaterra.Nlp/Response/Speech/StaticLineStore.cs`). That is core code and out of scope here.
- **`GrammarStringsExtractor` bypasses the bundle.**
  - It is handed the bundle, yet reads `english.json` and `checks.json` straight from disk (`Vocaterra.Host/GrammarStringsExtractor.cs:59-60`).
  - It also reads `content/PHRASES.md` and `content/speech-index.json`, which sit outside the bundle on purpose.
- **Every edit costs several steps.** After any edit:
  - `content.sh hash`, adding provenance items for new files, then validate;
  - the gate's content checks: `lint --strict --level a1 --host-pools 'guide.*'`, `cefr-check` at A1 and A2, and the required grammar
    check;
  - the grammar check reads `content/grammar/game-v1-strings.jsonl` (written by the host's `--grammar-strings`) offline, with
    `--strict`. A cache miss fails it, so new Spanish text needs the local LanguageTool server to refresh the cache;
  - the full suite, maybe re-recorded goldens, and the bundle hash updated in `tests/Vocaterra.Game.Core.Tests/Fixtures/save-v1.json`.

  The sandbox refuses the word `hash`, so that step needs a wrapper.

### 2.2 English (L1)

**In content, English sits inside the Spanish data.**
- **`game/english.json`** (25 KB, `EnglishText`) has these sections:
  - `ui`: the UI words;
  - `npcNames`: the English roles ("The guide");
  - `controls` and `tips`;
  - `objective`: the step glosses, plus each step's link to its noun;
  - `nouns` (glosses) and `phrases` (explanations);
  - `lines`: the NPC line glosses;
  - `feedback`: outcomes, and errors keyed by the core's error tags. A test keeps them complete against `ErrorTags.All`.

  The `lines` are keyed by **normalised Spanish text** (`PhraseSheet.Plain`), not by line id, so editing a Spanish line silently drops
  its gloss. At least one gloss is an orphan ("Necesitas una botella." matches no line).
- **The game drops the line ids.**
  - The core's `ResponseLine` carries a `LineId`, but `NpcService` keeps only the `.Text`.
  - These all find a line by its Spanish text:
    - the gloss: `GuideSession.EnglishForNpcLine`, which falls back on the lessons' `TranslationOf`;
    - the dialogue panel and the ending overlay;
    - the voice: `SpeechLines`, `SpeechIndex`.
  - Of the dialogue variants, 35 have `{slots}`. Two glosses belong to prompts, not dialogue lines, and some lines are built in code.
- **Inline English in other files:**
  - `lessons.json`: `title`, `english`, `summary`, `objective`, `translation`, `help`, `retry`, `personRetry`, `tenseRetry`,
    `presentNote`, `hud`, `options`. Some titles mix the two languages ("Personas: he, she, we, they").
  - `phrases.json`: `english`, and `card` (a mixed "phrase = meaning · word = meaning" format that `PhraseSupport` splits).
  - `glosses.json`: `english`, `third`, `past`.
  - `checks.json`: `question`, `options[].label`, `situation`.

**In code, about 130 player-facing English literals** (paths under `src/Vocaterra.Game` unless marked Core).

| Area | Where |
| --- | --- |
| HUD and toasts | `UI/HudLayers.cs`, `UI/VoxelTalkLayers.cs`, `UI/LoadingLayer.cs`, `UI/VoxelLayers.cs`, `Graphics/GreyscaleWorld.cs`, `Flow/ObjectiveAnnouncer.cs`, `VoxelGame.cs`, Core `Session/VoxelSession.cs` |
| Dialogue panel | `UI/Dialogue/DialoguePanel.cs`, `Screens/DialogueScreen.cs`, `UI/DialogueModel.cs` |
| Inventory, crafting, workbench | `UI/InventoryLayer.cs`, `UI/CraftingLayer.cs`, `UI/CraftingScreen.cs`, `UI/RecipePanelLayer.cs` |
| Book and journal | `UI/Book/Book.cs`, `UI/Book/BookLayer.cs`, `UI/Book/BookPages.cs`, `UI/Book/BookPractise.cs`, `UI/Book/ConjugationTable.cs`, `UI/Journal.cs`, `UI/PhraseJournal.cs` |
| Lessons, tips, controls | `UI/LessonStrip.cs`, `UI/English/TipLayer.cs`, `UI/English/ControlsLayer.cs` |
| Checks, mic check, playtest | `UI/CheckLayer.cs`, `Screens/CheckBattery.cs`, `Sound/MicCheckModel.cs`, `UI/MicCheckLayer.cs`, `Screens/MicCheckScreen.cs`, `Flow/PlaytestFlow.cs` |
| Report dialog | `UI/ReportLayer.cs`, `UI/ReportPanel.cs`, `Reporting/ReportScreen.cs`, Core `Reporting/ReportKind.cs` |
| Core messages the player sees | Core `Session/TurnWorker.cs`, `Language/Speech/SpeechEngine.cs`, `Language/Lessons/LessonRunner.cs`, `Save/SaveMigration.cs`, `Input/PushToTalkSpeechCapture.cs` |

**Logic that assumes English as L1:**
- **Names:** `EnglishText`, the `UI/English/` folder, `DialoguePanel.EnglishPrefix`, `ObjectivePieceKind.English`, the JSON keys
  `english`/`spanish`, and the required keys kept as C# lists (`EnglishText.UiKeys`, `OutcomeKeys`).
- **English grammar in code:**
  - `UI/Book/BookVerbLabels.cs` builds English verb forms by rule (do/does, -s/-es/-ies, was/were, let's);
  - `Language/Words/VerbGlosses.cs:72` requires every gloss to start with "to";
  - English plurals are built in code (`BookPages.cs:392` "1 time"/"N times", `SaveMigration`).
- **Key names in the text.**
  - The text says "Hold V", "W A S D", "Enter goes on, H helps", so it breaks when keys are remapped or on an AZERTY keyboard.
  - No remapping exists yet. Keys are fixed in `Input/KeyBindings.cs`, `Screens/DialogueScreen.cs` and `Input/WorldInput.cs`, and their
    labels are written out again in `LessonStrip.KeyLabel`, `PlaytestEnd.EndKeyName` and `english.json` `controls[].keys`.
- **No settings file.** Settings live in the save (`SaveSettings`: sound, text scale).

**The core and L1.**
- The core produces no English text for the learner; `feedback/feedback-policy.json` maps error tags to feedback types only.
- The manifest already has an optional **`supportLanguage`** (M32, ADR 0011): a BCP-47 tag, recorded and passed through, never
  interpreted, and not hashed.
- One English-as-L1 assumption remains in the core: `PredictedErrorVariants` predicts an English speaker's pronunciation errors. That is
  a core item for a later, non-English locale, and out of scope here.

**Rendering limits:**
- One font with no fallback (`assets/fonts/pixel-operator/PixelOperator.ttf`, `UI/FontProvider.cs`), and its glyph coverage is
  unchecked. FontStashSharp can take fallback fonts.
- Wrapping breaks only at ASCII spaces (`UI/DialogueModel.cs`), and `Graphics/DrawHelpers.cs` cuts text by character index.
- Typing offers only the Spanish accents (`UI/TextEntry.cs`) and refuses characters outside the basic plane. Typing is in the target
  language, so this is not an L1 limit.

**Developer text stays English and is out of scope:** the command-line usage and errors (`Utilities/LaunchOptions.cs`), the host console
(`Vocaterra.Host/Program.cs`, `VerdictReport.cs`), exceptions, logs, the session log, and the headings of the saved report file
(`Core/Reporting/ReportMarkdown.cs`).

### 2.3 Spanish (target language) in code

CLAUDE.md says the game's words live in content. These do not:

| What | Where |
| --- | --- |
| NPC labels | Core `Npc/NpcNames.cs:9-11` ("Guía", "Tendero", "Aldeano"; english.json `npcNames` holds the English roles under the same keys), `UI/Dialogue/DialoguePanel.cs:42`, `UI/DialogueModel.cs:110,187` |
| The guide's name | "Luna" in code at Core `Session/VoxelSession.cs:188`, and in six content files (`lessons.json` 29 times, `english.json`, `phrases.json`, `checks.json`, `dialogue.json`, `prompts.json`); "Rosa" in comments at `Language/Phrases/PhraseReview.cs:31` and `Language/Lessons/LessonRunner.cs:444` |
| UI labels | `UI/SpeechPanel.cs:79-91` (Repetir, Más despacio, Ayuda, No sé, Escribir), `UI/DialogueModel.cs:80`, `UI/SupportLayer.cs:20-26`, `UI/Journal.cs:27`, `Flow/BeaconDirector.cs:77` |
| Place words | `UI/PointLabels.cs:13-25` (río, tienda, casa, campamento, mi campamento, cascada, puente, mesa, camino, lago, pueblo, fuego, letrero) |
| Directions and joiners | Core `Npc/WorldFacts.cs:44-53,331`, `Npc/NpcService.Guide.cs:33` ("bloque"), `:206` (" y ") |
| Pronouns | `UI/Book/ConjugationTable.cs:56-60`, `UI/Book/BookVerbLabels.cs:88` |
| Grammar built in code | Core `Language/Words/WordProgress.cs:210` (the articles), Core `Language/Lessons/LessonBook.cs:160` and `UI/Book/ConjugationTable.cs:154,166` (the near future, "ir a"), Core `Npc/NpcHostEvents.cs` `PhraseIds.Hello = "hola"` |
| Words in logic | Core `Session/GuideSession.cs:78,81` ("ayuda", "no sé"), `:264` (`KeyWords = ["agua","pan"]`), `:746` ("¿Quieres agua?"); `Language/Speech/MicCheck.cs:18` and `Npc/NpcService.cs:324` ("hola"); `Session/VoxelSession.cs:234,268,271,404,433,485` (río, agua, fuego, antorcha); `Npc/NpcHostEvents.cs:84,172` |
| Spanish inside English hints | `UI/Dialogue/DialoguePanel.cs:21,30` and `UI/ReportLayer.cs:23` ("á é í ó ú ñ ¿ ¡"), `UI/CheckLayer.cs:42` ("(no sé)") |
| Sign text | `Graphics/BlockArtVillage.cs:46-59,211-220` (a 3x5 pixel font with only the letters it needs, plus accent and tilde hacks), Core `World/Generation/SceneBuilder.cs:565-579`, `Voxel/BlockId.cs:148-160`, `Voxel/BlockDefinition.cs:85-89`. Each sign board is its own block kind with baked art, and block kinds are saved by name, so sign text as data needs a sign mechanism, not just a string table. |

### 2.4 Game design hardcoded

**The camp objective** (`Core/Session/Objective.cs`):
- `Objective.Camp()` fixes the step keys (`wood`, `berry`, `torch`, `water`), the Spanish labels (madera, frutas, antorcha, agua), the
  targets (5, 3, 1, 1), the title `Campamento` and the event name `objective.water`.
- Its triggers are C# methods fed by **simulation events** in `VoxelSession.Step`:
  - `RecordPickup` switches on `ItemId`;
  - `RecordTorchPlaced` takes "at the camp's fire ring", decided by `AtFireRing`;
  - the water step comes from `SimulationEventKind.BottleFilled` through `RecordHostEvent(Objective.WaterEvent)`. No agent raises
    `objective.water`, and `VoxelSession.HostEvent()` has no caller outside the tests.
- The label is written in `Camp()`. The step's noun is linked only in `english.json` (`objective.steps.<key>.noun`; berry → `fruta`).
- `EnglishText.Parse` calls `Objective.Camp()` to check the glosses.
- **Saves.**
  - The save keeps the counts by step key (`ObjectiveSave(Key, Count)`), and `Objective.Restore` throws on an unknown key.
  - The save format is **v4** (`Save/SaveGame.cs:41`). Format upgrades are written inline in `SaveGame`.
  - `SaveMigration` handles a save from another bundle, and **drops** step keys it does not know (`SaveMigration.cs:222`).

**The session flow is order-free, not a chain.**
- `SessionStage` (FindGuide, Camp, LightFire, Complete; `Session/SessionStage.cs`) is worked out in `VoxelSession.Stage` from three flags:
  `Objective.Complete ? (_fireLit ? Complete : LightFire) : _guideMet ? Camp : FindGuide`. The flags are set like this:
  - `_guideMet`: talking to the guide, or any checklist progress;
  - `_fireLit`: a torch on the player's own fire ring (`firering.player`);
  - `Objective.Complete`: the checklist.
- The order is free on purpose, and a test pins it: `OpeningAndEndingTests` lights the fire before the checklist is done. The stage
  stays FindGuide, then jumps to Complete, and no spare torch is given.
- **The stage is not saved.** `RestoreStage` works it out again after a load: any lesson started, or any step count, means the guide
  was met, and the fire is found by scanning the world around `firering.player`.
- **Finishing the checklist** does more than raise an event:
  - it logs `objective_complete` with the summary;
  - it gives a spare torch, unless the fire is lit, a torch is held, or there is no player ring, and it handles a full bag;
  - it points the beacon at the player's own camp;
  - it checks whether the session is complete.
- **The rest of the chain also hardcodes:**
  - the Book granted at the start, and the "start" tips;
  - the closing line: the first variant of `guide.lesson.end`, shown in the ending overlay, not spoken;
  - the points `firering`, `firering.player` and `camp.guide` (`Flow/BeaconDirector.cs:17`), and the ambient fire bed
    (`Sound/AmbientBeds.cs`);
  - the readers of the stage: `VoxelSession.ObjectiveHud` and `EnglishText.StageLine`.

**Three trigger systems, each built its own way:**
- **Tips** use `when` (`start`, `aimed_npc` or a world event; `EnglishText.TipTriggers`) and have no filters.
- **Lesson `do` steps** use `event` plus two filters: `detail` (a case-insensitive substring) and `at` (the NPC, with `talk_opened`).
  `point` is not a filter: it aims the beacon when the step begins. `hud` is the step's HUD line. The matcher is `LessonRunner.OnEvent`.
- **The objective** uses C# methods fed by simulation events.

**What the events carry:**
- **World events** (`Core/Event/WorldEvent.cs`: 21 names in `WorldEvents`) are `WorldEvent(Tick, Time, Name, Npc?, Detail?)`, with no
  position and no amount.
- There is no world event for a placed block.
- `item_picked_up` carries the noun lemma as its detail.
- Filling the bottle also raises `ItemPickedUp(WaterBottle)`, so a pickup trigger on `agua` would fire on a fill.

**Sites:**
- Sites exist in `scene.json` (`SiteSpec`), and `Scene.Points` merges sites, points and named props.
- `firering` and `firering.player` are the names of fire-ring props, not sites.
- "At the ring" means within the 3×3 square around a ring stone (`VoxelSession.AtRing`). A scene with no named ring counts every ring.

**NPCs:**
- `Npc/NpcHostEvents.cs` holds:
  - the host-event-to-effect table (give a bottle, a bread or water; point at "río");
  - in `PhraseIds`, the intent-to-phrase, focus, scaffold and phrase-to-home tables.
- Agent and prompt ids are fixed in `Npc/NpcService.cs:116-158`, `Language/Speech/SpeechLines.cs:14-20` and `LaunchOptions.cs:26`.
  Lessons check NPC ids against `NpcService.AgentIds`, and saves store them.
- **The opening event is logic, not a field.** `NpcService.OpeningEvent` picks it from the visit count, the bottle-given and offered
  flags, the agent's state and whether the objective is complete. `PromptAfter` maps each state to a prompt.
- The guide's `GuideEvents` are dispatched in C# (`NpcService`, `GuideSession`) to build the world-fact answers.
- **Already held elsewhere:**
  - the placement, in `scene.json` `npcs`;
  - each prompt's agent, in `prompts.json` `agentId`;
  - the voices, fixed in the offline renderer (`tools/Vocaterra.SpeechRender/Program.cs:154-156`), which takes a line's speaker
    from its pool id's prefix (`SpeechSpeakers.OfPool`).
- The core's `agents/*.json` hold only states, transitions and actions. The core's `effects/effects.json` maps intents to correctness
  curves, a different meaning from the game's "effect".
- `Graphics/NpcFigures.cs` holds each figure's colours and geometry (apron, hat, long hair); the faces are pixel art in
  `BlockArtVillage.cs`.
- The phrase-to-pool table is fixed in `Language/Phrases/PhraseReview.cs`.
- **Rules that are behaviour, not data:**
  - the shopkeeper gives up and hands over after 4 failed lines, or 2 after help;
  - the bottle is given once;
  - the stall key words have a shopkeeper-only delay;
  - the river flag drives `found_without_spanish`;
  - the transfer offer ("¿Quieres agua?", `GuideApproach`);
  - the guide is special-cased in the front end: `DialogueScreen`, `GuideGreeting`, `DialogueModel`, `TurnWorker`, `CheckGrader`.

**Items, blocks and sounds:**
- The `ItemId` enum and the `ItemDefinition` table (`Items/ItemDefinition.cs`, 7 rows):
  - Saves store items and blocks by **enum name** (`CanonicalJson` writes enums as strings), so the names must stay stable.
  - The numbers are not saved. The comment in `ItemDefinition.cs` that says they are is out of date.
- `Voxel/BlockDefinition.cs` (52 rows), and `Graphics/BlockAtlasLayout.cs` for the art.
- `Sound/SoundCatalog.cs` (about 52 cues), footsteps per block (`Sound/Footsteps.cs`) and block materials (`SoundEventMapper`).

**Lessons:**
- `LessonBook.cs:142` (`NearFuture = "ir-a"`) and `LessonBook.cs:160` build the near future.
- `LessonRunner.cs` checks for "mañana" and the lemma "ir" + "a" (around `:962-1004`).

### 2.5 Found along the way

- **Bug:** a non-guide NPC's beacon label is its raw agent id ("shopkeeper"), not its label (`VoxelSession.cs:188`).
- **Names:** the guide is "Luna" in code and content, but "Rosa" in two comments.
- **Stale doc comments:** `SaveDocument.cs` says the format is v3, and `ItemDefinition.cs` says item numbers are saved.
- **Stale fixture path:** the save fixture moved to `tests/Vocaterra.Game.Core.Tests/Fixtures/`. The old path was corrected on 2026-10-28
  in `CLAUDE.md`, the orchestrator, `IMPLEMENTATION_PLAN.md` and `docs/nlp/CONTRIBUTING.md`.

## 3. The direction

### 3.1 Two language roles, named apart

- **The target language** (Spanish) belongs to the core: its language pack, `ContentBundle.Language` and the manifest's `language` (M32).
- **The support language** (L1) belongs to the game, as a locale bundle. game-v1's manifest names the default locale in
  `supportLanguage`: `hash content/game-v1 --bundle-id game-v1 --support-language en`. The field is not hashed, and the core never
  reads it.

Target-language data never carries L1 text; it carries ids. L1 text lives in a **bundle of its own**:

```
content/
  game-v1/      the core bundle: hashed, saves pin its hash; ids only, no L1 text
  locale/       the L1 bundle, with its own manifest.json
    locales.json
    en/ui.json, tips.json, controls.json, feedback.json, lessons.json, phrases.json, lines.json, nouns.json, verbs.json, keys.json
  schema/       the JSON Schemas (for editors and one test)
```

- **Hashed like a bundle.**
  - `scripts/content.sh hash content/locale --bundle-id locale` writes the locale's manifest with the core's `ContentManifest.Compute`.
  - At load, the game checks the files against that manifest with the public `ContentManifest.FromJson` and `ContentManifest.Compute`,
    and refuses a file that differs. No core change is needed.
- **Outside game-v1's hash.**
  - An L1 edit or a new locale changes no save, no fixture and no golden, and it does not re-run the core's suite.
  - The session log and the F8 report record the locale bundle's hash beside game-v1's, so a playtest can be reproduced.
  - The core's provenance rule does not apply to it.
- **`locales.json`** lists the locales: code, name, and the glyph set each needs. Only `en` ships for now.
- **One id per spoken line.**
  - `lines.json` uses the ids the speech index already uses (`SpeechLines`): the dialogue `lineId`, `prompt.<promptId>`,
    `card.<lemma>` and `phrase.<id>`.
  - So a line's voice and its gloss have one key, and it no longer depends on the Spanish text.
  - The gloss of a line with `{slots}` has the same slots. A noun slot is filled with that noun's `noun.<lemma>` gloss.
- **File format.** Each file is `{ "$schema": "...", "strings": { "<id>": "<text>" } }`. The strings sit under `strings`, so `$schema`
  is never read as an id.
  - Plurals are two keys, `<id>.one` and `<id>.other`.
  - A name is a placeholder, `{npc:guide}`, filled from the NPC data (3.6). Renaming the guide is then one edit, not forty.

Examples:

| Id | `en` |
| --- | --- |
| `ui.continue` | Continue |
| `lesson.first-words.title` | First words with {npc:guide} |
| `lesson.first-words.u1-say-hola.text` | Say hello back: “Hola” (OH-lah). Hold {key:talk} to speak into the microphone, or press {key:type} to type. |
| `phrase.pedir-agua.meaning` | I want water, please. |
| `guide.lesson.hello.1` (in `lines.json`) | Hello. |
| `noun.madera` | wood |
| `book.uses.one` / `book.uses.other` | 1 time / {count} times |
| `feedback.error.conj.person` | ... |
| `npc.shopkeeper.role` | The shopkeeper |
| `verb.tener` | `{ "base": "to have", "third": "has", "past": "had" }` |
| `key.Space` (in `keys.json`) | Space |

### 3.2 `LocaleText` replaces `EnglishText`

- **First, keep the line ids.**
  - The turn path (`NpcService` and the turn presenter) passes `ResponseLine.LineId` on to the panel, and lines built in code get ids
    (3.3).
  - Only then can glosses and voices be found by id. The lookup by text stays only for lines with no id; a test lists them, and the
    list should end empty.
- **Lookup:** the chosen locale, then `en`, then a visible `⟦key⟧` marker, so a missing string shows on screen and is never empty.
- **Placeholders:**
  - `{name}` stays.
  - `{npc:<id>}` is filled from the NPC data.
  - `{key:<command>}` gives the key bound to a command, from R16's `GameKey` and `KeyBindings`, spelled with the locale's
    `key.<GameKey>` names. So remapped keys and other keyboard layouts show the right key.
- **Choosing the locale:** `--locale <code>`. The default is game-v1's `supportLanguage` (`en`). Storing the choice (in `SaveSettings`,
  or in a settings file that does not exist yet) comes later.
- **Verb forms:** L1 verb forms are listed in the locale (base, third, past). `BookVerbLabels`' English rules and the "to" check go.
- **Rename:** `ObjectivePieceKind.English` becomes `Support`. R16 already removes the `UI/English/` folder.
- **Tests:**
  - every id that content or code refers to exists in every shipped locale, and no locale has orphans;
  - every glyph of every locale's strings is in the font;
  - a scan finds no player-facing string literals in `Vocaterra.Game` or `Vocaterra.Game.Core`, with an allowlist for developer text.

### 3.3 Target-language words out of code

`game/words.json` holds the words the game itself uses in the target language:
- the directions, the block unit and the list joiner;
- the speech-button labels, the support headings and the pronoun labels;
- `KeyWords` and the help phrases (`ayuda`, `no sé`);
- the articles, and the near-future joiner ("ir a");
- the greeting `PhraseIds.Hello`;
- the letters the typing help lists ("á é í ó ú ñ ¿ ¡").

Some words go elsewhere:
- NPC labels and names go into the NPC data (3.6).
- Place labels (`PointLabels`) go into the scene's sites and props.
- Sign text waits for C09, because it needs a sign mechanism (2.3).

`GrammarStringsExtractor` reads `words.json`, so the grammar check covers these words for the first time. That also means cache misses,
which need the local LanguageTool server once. This is also what lets a later target language (core M36 onwards) use the game without
code changes.

### 3.4 One trigger model

A `Trigger` matches one world event (`WorldEvents`, plus `start` and `aimed_npc`), with optional filters: `npc`, `detail` and `count`.
One matcher serves tips, lesson `do` steps and objectives. The matcher reads world events only, so every trigger can be seen in the
session log.

```json
{ "event": "item_picked_up", "detail": "madera" }
{ "event": "torch_at_ring", "detail": "firering" }
{ "event": "bottle_filled" }
{ "event": "talk_opened", "npc": "guide" }
```

- **One new world event.** C06 adds `torch_at_ring`, raised by the session when a torch is placed at a fire ring (today's `AtRing` rule,
  fallback included), with the ring's prop name as its detail. It adds lines to `session.jsonl`.
- **`detail` matches exactly.** Exact matching keeps `firering` apart from `firering.player`. The shipped lessons' only detail filter
  (`fruta`) equals the whole detail, so nothing changes, and a test pins that.
- **Lesson fields:** `at` becomes `npc`. `point` stays a step action (it aims the beacon) and `hud` stays the step's line.
- **Counting:** `count` counts matching events. Every gatherable block drops one item today, so a pickup event is one item. If a drop
  ever gives more, the event needs an amount.

### 3.5 Objectives as data

`game/objectives/<id>.json`, one file each, and `game/session.json` for the order. The camp objective as data:

```json
{
  "$schema": "../../../schema/objective.schema.json",
  "id": "camp",
  "title": "Campamento",
  "steps": [
    { "id": "wood",  "noun": "madera",   "target": 5, "trigger": { "event": "item_picked_up", "detail": "madera" } },
    { "id": "berry", "noun": "fruta",    "target": 3, "trigger": { "event": "item_picked_up", "detail": "fruta" } },
    { "id": "torch", "noun": "antorcha", "target": 1, "trigger": { "event": "torch_at_ring", "detail": "firering" } },
    { "id": "water", "noun": "agua",     "target": 1, "trigger": { "event": "bottle_filled" } }
  ],
  "onComplete": ["spare-torch", "point-own-camp"]
}
```

```json
{
  "$schema": "../../schema/session.schema.json",
  "objectives": ["meet-guide", "camp", "light-fire"],
  "closingPool": "guide.lesson.end",
  "grants": ["book"],
  "beaconStart": "camp.guide"
}
```

- **Labels and titles.**
  - A step's checklist label comes from its noun: the lemma for a mass noun or a target of 1, otherwise the plural from the catalogue
    (`NounEntry.Plural`). An explicit `label` overrides it. This rule reproduces today's labels.
  - The step's L1 gloss comes from the locale (`noun.<lemma>`), and fades once the noun is known (D-game-6), as it does today.
  - A title's gloss is `objective.<id>.title`.
- **Order, not gates.**
  - `session.json` sets the order the HUD shows; it does not gate anything.
  - Every objective counts from the start, so the fire can be lit first (as `OpeningAndEndingTests` pins).
  - `meet-guide` is also done once `camp` has any progress or a lesson has started, as `_guideMet` is today. Its file says so with
    `"alsoDoneBy": ["camp"]`. The fire alone does not count: in the test above, the stage stays FindGuide after the fire is lit.
  - The stage line is the locale's `objective.<id>.hud` for the first objective not yet done. `ObjectiveHud` keeps its lesson-first
    order.
  - The session is complete when every objective is done. Then the first line of `closingPool` is shown.
- **Side effects are named code hooks.**
  - Completing an objective always logs `objective_complete` with its summary and checks whether the session is complete.
  - Anything more is a hook named in `onComplete`. The hooks today are `spare-torch` (its three conditions stay in code) and
    `point-own-camp`.
  - A step whose state lives in the world names a restore hook: the fire is found by scanning the ring (`scan-fire-ring`), as
    `RestoreStage` does today.
  - An unknown hook name fails validation. The data names the behaviour, and the code implements it.
- **Saves.**
  - **Save v5** keeps the counts by `<objective>.<step>` id.
  - Loading a v4 save maps the old keys (`wood` becomes `camp.wood`) inline in `SaveGame`, before `SaveMigration`, which drops keys it
    does not know.
  - The stage flags stay derived, not saved.
  - A v4 fixture and a migration test are added, the out-of-order ending among them.
- **What stays in code:** the simulation still raises the events, `AtRing` still decides the ring, and the hooks are C#. Only the
  mapping from events to steps, the targets, the labels and the order move into data.

### 3.6 NPCs and host events as data

`game/npcs/<id>.json` holds only what no other file holds:
- the target-language label (`Guía`) and the personal name (`Luna`). The panel and the beacon use them, which fixes the raw-id label
  (2.5), and `{npc:<id>}` fills names from them;
- the figure key (`NpcFigures` keeps the geometry and the faces in code, keyed by it);
- the phrase tables that belong to an NPC (phrase to home NPC, prompt to scaffold).

**Not repeated there:**

| What | Where it stays |
| --- | --- |
| The agent | `agents/<id>.json`, the core's file; the NPC file has the same id |
| The prompts | each prompt's `agentId` in `prompts.json` |
| The placement | `scene.json` `npcs` |
| The voice | the renderer. Open point: the renderer could read the voice from the NPC file, so that one place holds it. |
| The L1 role name | the locale, `npc.<id>.role` |

`NpcService.AgentIds` is built from the NPC files, and the lessons' NPC check and the saves use it.

`game/host-events.json` replaces the `NpcHostEvents` table. The field is `action`, because the core's `effects.json` already uses
"effect" for something else.

```json
{ "npc": "shopkeeper", "event": "stall.give_water", "action": { "kind": "give_item", "item": "bottle" } }
{ "npc": "villager",   "event": "villager.point",   "action": { "kind": "point_at_noun", "noun": "río" } }
```

The phrase-to-review-pool table (`PhraseReview`) moves onto the phrase data.

**What stays in code, said plainly:**
- the choice of the opening event (`NpcService.OpeningEvent`);
- the guide's `GuideEvents` dispatch;
- the shopkeeper's give-up and once-only rules, and the stall key words' delay;
- the river flag and the transfer offer;
- the guide's special cases in the front end.

They are behaviour, and moving them would need a scripting layer, which this plan does not propose.

### 3.7 Easier management

- **JSON Schema** for every game host-file kind and for the locale files, in `content/schema/`.
  - Each file names its schema in `$schema`, so Rider and VS Code validate and autocomplete as you type. Paths are relative:
    `../../schema/nouns.schema.json` from `game-v1/game/`, `../../../schema/objective.schema.json` from `game-v1/game/objectives/`,
    `../../schema/locale.schema.json` from `locale/en/`.
  - **Only the game's files** get `$schema`. The core's `dialogue.json` refuses unknown root properties, and the core's files stay as
    they are.
  - A schema that sets `additionalProperties: false` declares `$schema`.
  - Content shaped as a map keeps its map under a named key, because the loaders read every key of a map: `EnglishText.Map`, the
    scene's `sites` and `points`.
  - `scene.json` has comments: the schema test reads it with comments skipped, and editors treat it as JSONC.
- **Schemas serve editors; the loaders are the authority.** One test validates every file against its schema, with a library used only
  by the test project. It is licence-scanned like any package (check any usage-fee terms too). The test keeps the schemas and the
  loaders in step.
- **Split the large files:** `game/lessons/<id>.json`, `game/objectives/<id>.json`, `game/npcs/<id>.json`, and the locale's
  `en/*.json`. Each new file in game-v1 gets a provenance item.
- **`GameData.Load(bundle, locale)`.** (`GameContent` is already the name of a test helper in `tests/Vocaterra.Host.Tests`.)
  - One call builds the whole object graph: nouns, recipes, phrases, lessons, checks, glosses, words, the locale, objectives, the
    session, NPCs, host events and the scene.
  - It runs every cross-check in `GameDataValidator`: noun refs, phrase refs, line ids, locale ids, trigger events, hook names, the
    session's objective list, and the NPC agent files.
  - Call sites take `GameData` instead of loading files themselves.
  - The scene is parsed from the verified bytes. `--scene` stays as a developer override and warns that it skips the check.
  - `GrammarStringsExtractor` reads the game's files through it. `PHRASES.md` and `speech-index.json` stay disk reads, because they
    sit outside the bundle.
  - The host's `--validate-content` runs it from the command line (the ContentBuilder cannot, 2.1).
- **`scripts/content.sh refresh`.** It does all of these, avoids the sandbox words, and leaves no step to forget:
  - hashes game-v1 and the locale;
  - lists the files that have no provenance item;
  - runs validate, lint with the gate's arguments (`--strict --level a1 --host-pools 'guide.*'`), and cefr-check at A1 and A2;
  - runs the host's `--validate-content`;
  - regenerates the grammar strings (`--grammar-strings`) and runs the offline grammar check. A cache miss means new Spanish, and
    the message names the LanguageTool step (`scripts/languagetool.sh start`).
- **`content/README.md`:**
  - what lives where: game-v1, the locale, the schemas;
  - who owns what: the core's files and the game's host files;
  - how to add an NPC, an objective, a lesson or a locale string;
  - the edit-then-refresh loop.

### 3.8 The way to custom tools

The schemas, `GameData` and the validator are the contract any tool builds on.
- **First tool:** an authoring CLI with three commands:
  - `new npc|objective|lesson` scaffolds a schema-valid file with its provenance item and its locale entries;
  - `refs <id>` shows where an id is used;
  - `check` runs the validator.
- **Later:** a GUI editor (objectives, NPCs, lessons), whose technology stays open. It reads and writes the same files through the same
  schemas.

## 4. Milestones

Branch `C<nn>-<name>`, commit prefix `C<nn>:`, gated into a feature branch (`feature/content`), never `main`. The series starts after
R16 phase 2 has landed (owner, 2026-10-28), from the branch R16 lands on. Each brief finds the files named here again on R16's layout.

| ID | What | Model | Depends |
| --- | --- | --- | --- |
| C01 | Content contract: JSON Schemas for every game file and the locale format, `$schema` refs, the schema test (test-only library), `content/README.md`, `content.sh refresh`, the bundle hash masked in `scripts/compare-runs.py` | Haiku | R16 phase 2 |
| C02 | `GameData` + `GameDataValidator`: one load of every host file (the scene from the verified bytes; `GrammarStringsExtractor` reads through it), the cross-checks moved there, host `--validate-content` | Sonnet | C01 |
| C03 | The locale bundle (`content/locale/`, `en` only, its own manifest, checked at load, its hash in the log and the report); game-v1's `supportLanguage`; the turn path keeps line ids; `LocaleText` replaces `EnglishText`; glosses keyed by speech-line ids; the L1 fields leave lessons, phrases, glosses and checks; verb forms listed | Sonnet | C02 |
| C04 | The English literals of Game and Game.Core into the locale (section 2.2); `{key:…}` from R16's `KeyBindings`, `{npc:…}`, plurals; the no-literal scan; the glyph test | Haiku | C03 |
| C05 | Target-language words out of code (`game/words.json`, place labels into the scene; section 2.3); the grammar extractor reads them | Haiku | C03 |
| C06 | One trigger model shared by tips and lesson `do` steps (exact `detail`, ready for objectives); the `torch_at_ring` world event; lessons split one file each | Haiku | C02 |
| C07 | Objectives and the session from data (`game/objectives/`, `game/session.json`, named hooks); `Objective.Camp()` and the `SessionStage` logic retired; save v5 with the v4 keys mapped; a v4 fixture and the out-of-order test | Sonnet | C05, C06 |
| C08 | NPC data (`npcs/*.json`: label, name, figure key, phrase tables), `host-events.json`, `NpcService.AgentIds` from data; `NpcNames`, the `NpcHostEvents` table and `PhraseReview`'s table retired; the beacon label fixed | Haiku | C05 |
| C09 | Items, blocks, sounds and signs as data (assess first; parts may stay code). Enum names stay stable because saves store them; signs need a sign mechanism | proposed | C08 |
| C10 | Authoring CLI: scaffold, references, check (the first custom tool) | proposed | C07, C08 |

**Every content-moving milestone:**
- runs `content.sh refresh` (hash, provenance, validate, lint, cefr-check, the game validator and the grammar check), then the whole
  suite (the core runs on `game-v1`, R02);
- refreshes the grammar cache with the LanguageTool server when it adds Spanish text;
- re-records the end-to-end goldens if they moved;
- updates the bundle hash in `tests/Vocaterra.Game.Core.Tests/Fixtures/save-v1.json` when game-v1 changed (an edit of the locale alone
  does not need it);
- does one run with models.

**Behaviour.** C03 to C06 and C08 change no behaviour, and C07 changes only the save format. How each kind of run proves it:

| Check | What it must show |
| --- | --- |
| The 7 byte-exact scripted runs (`docs/game/SCRIPTED_RUNS.md`), bundle hash masked (C01) | identical output |
| C06's new event | the only new lines in `session.jsonl` |
| The loose runs and the screenshots | the same text, checked by eye. The fire, the spare torch and the transfer offer appear only in r10. |
| C07 | `OpeningAndEndingTests` stays as it is, and the v4-save migration test passes |

## 5. Risks and open points

- **Save migration (C07).** The next format is v5. The old step keys are mapped inline in `SaveGame` before `SaveMigration`, which would
  drop them. The save fixtures need a v4 file.
- **Order with R16.** R16 phase 2 moves most of the files the series edits. Running the two at once would have them fight over the
  same files, so the series waits.
- **Golden churn.**
  - C03 moves the English out of game-v1 once; after that, an L1 edit never touches game-v1.
  - The Spanish-side moves (C05 to C08) still change game-v1's hash. Do each move in one milestone, not spread out.
- **The grammar gate.**
  - The 127 `english.json` `lines` strings repeat dialogue and prompt texts. They leave the strings file, and the cache, which is keyed
    by text, still hits.
  - `words.json` brings new strings, and they need one cache refresh with the server.
- **New world events** add lines to `session.jsonl`, which the playtest report and its fixtures read.
- **`PHRASES.md`.**
  - D-game-10 keeps it as the source of truth, and `PhraseSheetTests` checks it against `phrases.json`: English, card, situation and
    audio ids.
  - Once the English moves to the locale, the test must read the meaning from there.
  - Whether `PHRASES.md` should become generated from data is an owner decision.
- **Font and scripts.** PixelOperator's coverage limits which locales can ship, and the glyph test makes that visible. Wrapping and
  cutting assume a script with spaces; a CJK locale would need line-breaking work first (out of scope).
- **Core items, out of scope.** Each becomes a core milestone if a locale or a fix needs it:
  - `StaticLineStore` reads the WAVs a second time;
  - `PredictedErrorVariants` assumes an English speaker. A non-English locale would make this one a core milestone.
- **Decision record.** When the owner adopts this plan:
  - add it as D-game-21 in `DECISIONS.md`, and as section 4.7 of `IMPLEMENTATION_PLAN.md`;
  - record the "Luna"/"Rosa" mismatch, the scene's second read and the beacon-label bug in section 5;
  - add a line to `CLAUDE.md`.
