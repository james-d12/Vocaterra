# Content as data and the L1 locale layer (the C series)

A plan written on 2026-10-27, at the owner's request, and revised on 2026-10-28 after a review against the code. It has three goals:
- make `content/` easier to manage;
- move the learner's native language (L1) out of the code and the target-language data, into locale data;
- build objectives, NPCs and target-language words from content data, as the ground for custom authoring tools later.

**Status:** adopted on 2026-10-29 and in progress on `feature/content` (section 4 has each milestone's state). It is D-game-21 in
`DECISIONS.md` and section 4.7 of `IMPLEMENTATION_PLAN.md`.

**The owner's answers:**
- English moves into a locale layer, but no second locale is added yet (2026-10-27).
- Content stays JSON, with JSON Schema added (2026-10-27).
- The locale is a bundle of its own, `content/locale/`, beside `content/game-v1` and outside its hash (2026-10-28; section 3.1).
- R16 phase 2 (`GAME_PROJECTS_LAYOUT.md`) runs first, and the series starts on its layout (2026-10-28; section 4).
- Proceed (2026-10-29). One session (Opus 5.5) does all the work itself, with no subagents, in a worktree of `feature/content` made from `main`.

**What the review changed (2026-10-28):**
- The locale moved out of the hashed bundle, so an L1 edit no longer touches saves, fixtures or the core's suite. game-v1's
  `supportLanguage` names the default locale.
- Objectives keep today's order-free progress, their side effects are named code hooks, and the save goes to v5, not v3.
- Triggers read world events only, with one new event for the fire rings. The NPC data is trimmed to what no other file holds.
- The per-milestone checklist gained provenance, the grammar strings and a scripted-run compare with the bundle hash masked. Wrong
  facts, paths and line numbers were corrected.

**What the check against `main` changed (2026-10-29, at `604903f`, after R16 phase 2 landed by pull request #12):**
- Every path is on R16's layout now. `Core` is `src/Vocaterra.Game.Core` and `Game` is `src/Vocaterra.Game`, and line numbers are as of `604903f`.
- Many front-end files moved to Core: the dialogue model, the speech buttons, the book's pages and tables, the screens, the check
  battery, the beacon and the point labels. Their literals therefore count as Core's.
- The facts that had gone wrong were corrected: 15 of 175 dialogue variants have `{slots}` (not 35), and the speech-button labels live in
  Core's `SpeechButtons`.
- The JSON Schema library is NJsonSchema (MIT), not JsonSchema.Net. JsonSchema.Net 9.x ships without a licence expression, the case the
  plan warned about (3.7).

Each milestone finds a file again by its symbol when a path here has gone stale.

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

  | Loader (in Core) | File | Created at |
  | --- | --- | --- |
  | `Language/Words/NounCatalog.cs` | `game/nouns.json` | Game `Program.cs:62`, `tools/Vocaterra.SpeechRender/Program.cs:71` |
  | `Items/Crafting/RecipeBook.cs` | `game/recipes.json` | Core `Session/VoxelSession.cs:60`, `Npc/NpcService.cs:231` (a fallback) |
  | `Language/Phrases/PhraseSheet.cs` | `game/phrases.json` | Core `Session/GuideSession.cs:140`, `Npc/NpcService.cs:233`, `Language/Checks/CheckBank.cs:86`, SpeechRender `Program.cs:72` |
  | `Tip/EnglishText.cs` | `game/english.json` | Core `Session/GuideSession.cs:141` |
  | `Language/Words/VerbGlosses.cs` | `game/glosses.json` | Core `Session/GuideSession.cs:142` |
  | `Language/Lessons/LessonBook.cs` | `game/lessons.json` | Core `Session/GuideSession.cs:143` |
  | `Language/Checks/CheckBank.cs` | `game/checks.json` | Core `Language/Checks/CheckBattery.cs:118` |

- **The scene is read twice.**
  - `scene.json` is in the manifest, so the bundle load checks its hash.
  - The game then parses a second read from disk (`SceneLayout.Load`, Core `World/Layout/SceneLayout.cs:122`, via Game
    `Startup/SceneLocator.cs`), and `--scene` skips the check entirely.
  - The file is JSON with comments (32 `//` lines).
- **The speech WAVs are read twice too.** They are hashed at load, then read again from disk by the core's `StaticLineStore`
  (`src/Vocaterra.Nlp/Response/Speech/StaticLineStore.cs`). That is core code and out of scope here.
- **`GrammarStringsExtractor` bypasses the bundle.**
  - It is handed the bundle, yet reads `english.json` and `checks.json` straight from disk (`src/Vocaterra.Host/GrammarStringsExtractor.cs:59-60`).
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
  - Of the 175 dialogue variants (109 pools), 15 have `{slots}`. Two glosses belong to prompts, not dialogue lines, and some lines are
    built in code.
- **Inline English in other files:**
  - `lessons.json`: `title`, `english`, `summary`, `objective`, `translation`, `help`, `retry`, `personRetry`, `tenseRetry`,
    `presentNote`, `hud`, `options`. Some titles mix the two languages ("Personas: he, she, we, they").
  - `phrases.json`: `english`, and `card` (a mixed "phrase = meaning · word = meaning" format that `PhraseSupport` splits).
  - `glosses.json`: `english`, `third`, `past`.
  - `checks.json`: `question`, `options[].label`, `situation`.

**In code, about 130 player-facing English literals.** R16 moved many of them to Core with their models.

| Area | Where (Game is `src/Vocaterra.Game`, Core is `src/Vocaterra.Game.Core`) |
| --- | --- |
| HUD and toasts | Game `UI/Hud/HudLayers.cs`, `UI/Hud/VoxelLayers.cs`, `UI/Hud/TipLayer.cs`, `UI/Dialogue/VoxelTalkLayers.cs`, `UI/Overlays/LoadingLayer.cs`, `VoxelGame.cs` (the window title); Core `Language/Words/GreyscaleWorld.cs`, `Session/ObjectiveAnnouncer.cs`, `Session/VoxelSession.cs` (`NeedBottleHint`) |
| Dialogue panel | Game `UI/Dialogue/DialoguePanel.cs`; Core `Dialogue/DialogueScreen.cs`, `Dialogue/DialogueModel.cs`, `Dialogue/SpeechButtons.cs` |
| Inventory, crafting, workbench | Game `UI/Inventory/InventoryLayer.cs`, `UI/Crafting/CraftingLayer.cs`, `UI/Crafting/RecipePanelLayer.cs`; Core `Items/Crafting/CraftingRow.cs` |
| Book and journal | Game `UI/Book/BookLayer.cs`; Core `Language/Book/BookPages.cs`, `BookTabs.cs`, `BookVerbLabels.cs`, `ConjugationTable.cs`, `BookScreen.cs`, `Language/Words/JournalRows.cs`, `Language/Phrases/PhraseJournalRows.cs` |
| Lessons, tips, controls | Game `UI/Lesson/LessonStripLayout.cs`, `UI/Overlays/ControlsLayer.cs`; Core `Language/Lessons/LessonActions.cs` (key labels), `Language/Lessons/LessonRunner.cs`, `Session/SessionSummary.cs` |
| Checks, mic check, playtest | Game `UI/Checks/CheckLayer.cs`, `UI/Checks/MicCheckLayer.cs`; Core `Language/Checks/CheckBattery.cs`, `Language/Speech/MicCheckModel.cs`, `Language/Speech/MicCheckScreen.cs`, `Session/PlaytestFlow.cs`, `Session/PlaytestEnd.cs` |
| Report dialog | Game `UI/Report/ReportLayer.cs`, `UI/Report/ReportScreen.cs`; Core `Reporting/ReportPanel.cs`, `Reporting/ReportKind.cs` |
| Core messages the player sees | Core `Session/TurnWorker.cs`, `Language/Speech/SpeechEngine.cs`, `Save/SaveMigration.cs`, `Save/SaveGame.cs` (the strict-save refusal), `Input/PushToTalkSpeechCapture.cs` |

**Logic that assumes English as L1:**
- **Names:** `EnglishText` (Core `Tip/`), Game's `DialoguePanel.EnglishPrefix`, `ObjectivePieceKind.English`, the JSON keys
  `english`/`spanish`, and the required keys kept as C# lists (`EnglishText.UiKeys`, `OutcomeKeys`). R16 removed the `UI/English/` folder.
- **English grammar in code:**
  - Core `Language/Book/BookVerbLabels.cs` builds English verb forms by rule (do/does, -s/-es/-ies, was/were, let's);
  - Core `Language/Words/VerbGlosses.cs:72` requires every gloss to start with "to";
  - English plurals are built in code (Core `Language/Book/BookPages.cs:334` "1 time"/"N times", `Save/SaveMigration.cs`).
- **Key names in the text.**
  - The text says "Hold V", "W A S D", "Enter goes on, H helps", so it breaks when keys are remapped or on an AZERTY keyboard.
  - No remapping exists yet. R16 added Core's `Input/GameKey.cs` and Game's `Input/KeyMap.cs` (D-p2-1a), but the keys are still fixed in
    Core `Input/KeyBindings.cs` and `Dialogue/DialogueScreen.cs` and in Game `Input/WorldInput.cs`.
  - Their labels are written out again in Core `Language/Lessons/LessonActions.KeyLabel`, `Session/PlaytestEnd.EndKeyName`, the key
    hints of Game's panels (`DialoguePanel`, `InventoryLayer`, `CraftingLayer`, `ReportLayer`), and `english.json` `controls[].keys`.
- **No settings file.** Settings live in the save (`SaveSettings` in Core `Save/SaveDocument.cs`: sound, text scale).

**The core and L1.**
- The core produces no English text for the learner; `feedback/feedback-policy.json` maps error tags to feedback types only.
- The manifest already has an optional **`supportLanguage`** (M32, ADR 0011): a BCP-47 tag, recorded and passed through, never
  interpreted, and not hashed.
- One English-as-L1 assumption remains in the core: `PredictedErrorVariants` predicts an English speaker's pronunciation errors. That is
  a core item for a later, non-English locale, and out of scope here.

**Rendering limits:**
- One font with no fallback (`assets/fonts/pixel-operator/PixelOperator.ttf`, Game `UI/Common/FontProvider.cs`), and its glyph coverage is
  unchecked. FontStashSharp can take fallback fonts.
- Wrapping breaks only at ASCII spaces (Core `Dialogue/TextWrap.cs`), and Game `Graphics/DrawHelpers.cs` cuts text by character index.
- Typing offers only the Spanish accents (Core `Input/TextEntry.cs`, `Input/AccentShortcuts.cs`) and refuses characters outside the basic plane. Typing is in the target
  language, so this is not an L1 limit.

**Developer text stays English and is out of scope:** the command-line usage and errors (Game `Startup/LaunchOptions.cs`), the host console
(`src/Vocaterra.Host/Program.cs`, `VerdictReport.cs`), exceptions, logs, the console lines of the scripted runs, the session log, the debug
overlay (Game `UI/Hud/VoxelDebugText.cs`), and the headings of the saved report and crash files (Core `Reporting/ReportMarkdown.cs`,
`Reporting/CrashReport.cs`).

### 2.3 Spanish (target language) in code

CLAUDE.md says the game's words live in content. These do not:

| What | Where |
| --- | --- |
| NPC labels | Core `Npc/NpcNames.cs:9-11` ("Guía", "Tendero", "Aldeano"; english.json `npcNames` holds the English roles under the same keys), Core `Dialogue/DialogueModel.cs:62,139`, Game `UI/Dialogue/DialoguePanel.cs:44` (`Title`), Game `Graphics/Figures/NpcFigures.cs:99` (the labels over the heads) |
| The guide's name | "Luna" in code at Core `Session/VoxelSession.cs:189`, and in six content files (`lessons.json` 29 times, `english.json`, `phrases.json`, `checks.json`, `dialogue.json`, `prompts.json`); "Rosa" in comments at Core `Language/Phrases/PhraseReview.cs:31` and `Language/Lessons/LessonRunner.cs:444` |
| UI labels | Core `Dialogue/SpeechButtons.cs:10-22` (Repetir, Más despacio, Ayuda, No sé, Escribir), `Dialogue/DialogueModel.cs:32` ("¿Escribes?"), Game `UI/Dialogue/SupportLayer.cs:21-27` (Puedes decir, Escuchar la frase, Palabras nuevas), Core `Language/Words/JournalRows.cs:10` and `Navigation/BeaconDirector.cs:75` ("¿?") |
| Place words | Core `Navigation/PointLabels.cs:13-25` (río, tienda, casa, campamento, mi campamento, cascada, puente, mesa, camino, lago, pueblo, fuego, letrero) |
| Directions and joiners | Core `Npc/WorldFacts.cs:44-53`, `Npc/NpcService.Guide.cs:33` ("bloque"), `:206` (" y ") |
| Pronouns | Core `Language/Book/ConjugationTable.cs:23-27`, `Language/Book/BookVerbLabels.cs` |
| Grammar built in code | Core `Language/Words/WordProgress.cs:210` (the articles), Core `Language/Lessons/LessonBook.cs:160` and `Language/Book/ConjugationTable.cs:35,111` (the near future, "ir a"), Core `Npc/NpcHostEvents.cs:123` `PhraseIds.Hello = "hola"` |
| Words in logic | Core `Session/GuideSession.cs:79,82` ("ayuda", "no sé"), `:265` (`KeyWords = ["agua","pan"]`), `:747` ("¿Quieres agua?"); `Language/Speech/MicCheck.cs:18` and `Npc/NpcService.cs:325` ("hola"); `Session/VoxelSession.cs:235,269,272,405,434,486` (río, agua, fuego, antorcha); `Npc/NpcHostEvents.cs:84,172` |
| Spanish inside English hints | Game `UI/Dialogue/DialoguePanel.cs:23,32` and `UI/Report/ReportLayer.cs:25` ("á é í ó ú ñ ¿ ¡"), Game `UI/Checks/CheckLayer.cs:44` ("(no sé)") |
| Sign text | Game `Graphics/Blocks/BlockArtVillage.cs` (`Glyph`, a 3x5 pixel font with only the letters it needs, plus accent and tilde hacks), Core `World/Generation/SceneBuilder.cs:556-580` (`BuildSignpost`), `Voxel/BlockId.cs:148-160`, `Voxel/BlockDefinition.cs:85-89`. Each sign board is its own block kind with baked art, and block kinds are saved by name, so sign text as data needs a sign mechanism, not just a string table. |

### 2.4 Game design hardcoded

**The camp objective** (Core `Session/Objective.cs`):
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
  - The save format is **v4** (Core `Save/SaveGame.cs:42`). Format upgrades are written inline in `SaveGame`.
  - `SaveMigration` handles a save from another bundle, and **drops** step keys it does not know (Core `Save/SaveMigration.cs:222`).

**The session flow is order-free, not a chain.**
- `SessionStage` (FindGuide, Camp, LightFire, Complete; Core `Session/SessionStage.cs`) is worked out in `VoxelSession.Stage` from three flags:
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
  - the points `firering`, `firering.player` and `camp.guide` (Core `Navigation/BeaconDirector.cs:15`), and the ambient fire bed
    (Core `Sound/AmbientBeds.cs`);
  - the readers of the stage: `VoxelSession.ObjectiveHud` and `EnglishText.StageLine`.

**Three trigger systems, each built its own way:**
- **Tips** use `when` (`start`, `aimed_npc` or a world event; `EnglishText.TipTriggers`) and have no filters.
- **Lesson `do` steps** use `event` plus two filters: `detail` (a case-insensitive substring) and `at` (the NPC, with `talk_opened`).
  `point` is not a filter: it aims the beacon when the step begins. `hud` is the step's HUD line. The matcher is `LessonRunner.OnEvent`.
- **The objective** uses C# methods fed by simulation events.

**What the events carry:**
- **World events** (Core `Event/WorldEvent.cs`: 21 names in `WorldEvents`) are `WorldEvent(Tick, Time, Name, Npc?, Detail?)`, with no
  position and no amount.
- There is no world event for a placed block.
- `item_picked_up` carries the noun lemma as its detail.
- Filling the bottle also raises `ItemPickedUp(WaterBottle)`, so a pickup trigger on `agua` would fire on a fill.

**Sites:**
- Sites exist in `scene.json` (`SiteSpec`), and `Scene.Points` merges sites, points and named props.
- `firering` and `firering.player` are the names of fire-ring props, not sites.
- "At the ring" means within the 3×3 square around a ring stone (`VoxelSession.AtRing`). A scene with no named ring counts every ring.

**NPCs:**
- Core `Npc/NpcHostEvents.cs` holds:
  - the host-event-to-effect table (give a bottle, a bread or water; point at "río");
  - in `PhraseIds`, the intent-to-phrase, focus, scaffold and phrase-to-home tables.
- Agent and prompt ids are fixed in Core `Npc/NpcService.cs:117-144`, Core `Language/Speech/SpeechLines.cs:14-20` and Game
  `Startup/LaunchOptions.cs:29` (`TalkTargets`).
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
- Game `Graphics/Figures/NpcFigures.cs` holds each figure's colours and geometry (apron, hat, long hair); the faces are pixel art in
  Game `Graphics/Blocks/BlockArtVillage.cs`.
- The phrase-to-pool table is fixed in Core `Language/Phrases/PhraseReview.cs`.
- **Rules that are behaviour, not data:**
  - the shopkeeper gives up and hands over after 4 failed lines, or 2 after help;
  - the bottle is given once;
  - the stall key words have a shopkeeper-only delay;
  - the river flag drives `found_without_spanish`;
  - the transfer offer ("¿Quieres agua?", `GuideApproach`);
  - the guide is special-cased in the front end: `DialogueScreen`, `GuideGreeting`, `DialogueModel`, `TurnWorker`, `CheckGrader`.

**Items, blocks and sounds:**
- The `ItemId` enum and the `ItemDefinition` table (Core `Items/ItemDefinition.cs`, 7 rows):
  - Saves store items and blocks by **enum name** (`CanonicalJson` writes enums as strings), so the names must stay stable.
  - The numbers are not saved. The comment in `ItemDefinition.cs` that says they are is out of date.
- Core `Voxel/BlockDefinition.cs` (52 rows), and Game `Graphics/Blocks/BlockAtlasLayout.cs` for the art.
- Core `Sound/SoundCatalog.cs` (about 52 cues), footsteps per block (Core `Sound/Footsteps.cs`) and block materials (`SoundEventMapper`).

**Lessons:**
- Core `Language/Lessons/LessonBook.cs:142` (`NearFuture = "ir-a"`) and `:160` build the near future.
- Core `Language/Lessons/LessonRunner.cs` checks for "mañana" and the lemma "ir" + "a" (`:962-1004`).

### 2.5 Found along the way

- **Bug:** a non-guide NPC's beacon label is its raw agent id ("shopkeeper"), not its label (Core `Session/VoxelSession.cs:189`).
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
- **Rename:** `ObjectivePieceKind.English` becomes `Support`. R16 already removed the `UI/English/` folder.
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
  by the test project: NJsonSchema (MIT, with Newtonsoft.Json and Namotion.Reflection, both MIT), in `tests/Vocaterra.Game.Core.Tests`.
  JsonSchema.Net was ruled out because 9.x has no licence expression (usage-fee terms). The licence scan takes a `--target`, and the gate
  scans that test project as well as the core. The schemas are draft-07, which Rider, VS Code and NJsonSchema all read. The test keeps
  the schemas and the loaders in step.
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

Every milestone's work is committed straight to `feature/content` with the prefix `C<nn>:`, and pushed often, never to `main` (the owner,
2026-10-29: "make sure all work is to feature/content"; there are no `C<nn>-<name>` branches). R16 phase 2 landed on `main` by pull request
#12 (2026-10-29), and `feature/content` starts from `main` at `604903f`. On the owner's instruction one session (Opus 5.5) does every
milestone itself, with no subagents. At the end of each milestone the gate runs on `feature/content` itself
(`GATE_TARGET=feature/content scripts/merge-gate.sh "C<nn>:feature/content"`, where the merge is a no-op), and its result goes into
section 4's status column.

| ID | What | Depends | Status |
| --- | --- | --- | --- |
| C01 | Content contract: JSON Schemas for every game file and the locale format, `$schema` refs, the schema test (test-only library), `content/README.md`, `content.sh refresh`, the bundle hash masked in `scripts/compare-runs.py` | R16 phase 2 | done, gated with C02 and C03 (2026-10-29, `9e7960d`) |
| C02 | `GameData` + `GameDataValidator`: one load of every host file (the scene from the verified bytes; `GrammarStringsExtractor` reads through it), the cross-checks moved there, host `--validate-content` | C01 | done, gated with C01 and C03 (2026-10-29, `9e7960d`) |
| C03 | The locale bundle (`content/locale/`, `en` only, its own manifest, checked at load, its hash in the log and the report); game-v1's `supportLanguage`; the turn path keeps line ids; `LocaleText` replaces `EnglishText`; glosses keyed by speech-line ids; the L1 fields leave lessons, phrases, glosses and checks; verb forms listed | C02 | done, gated with C01 and C02 (2026-10-29, `9e7960d`) |
| C04 | The English literals of Game and Game.Core into the locale (section 2.2); `{key:…}` from R16's `KeyBindings`, `{npc:…}`, plurals; the no-literal scan; the glyph test | C03 | done (2026-10-29) |
| C05 | Target-language words out of code (`game/words.json`, place labels into the scene; section 2.3); the grammar extractor reads them 
| C06 | One trigger model shared by tips and lesson `do` steps (exact `detail`, ready for objectives); the `torch_at_ring` world event; lessons split one file each | C02 | done, gated with C05 and C08 (2026-10-29, `19a328e`) |
| C07 | Objectives and the session from data (`game/objectives/`, `game/session.json`, named hooks); `Objective.Camp()` and the `SessionStage` logic retired; save v5 with the v4 keys mapped; a v4 fixture and the out-of-order test | C05, C06 | done (2026-10-29) |
| C08 | NPC data (`npcs/*.json`: label, name, figure key, phrase tables), `host-events.json`, `NpcService.AgentIds` from data; `NpcNames`, the `NpcHostEvents` table and `PhraseReview`'s table retired; the beacon label fixed | C05 | done, gated with C05 and C06 (2026-10-29, `19a328e`) |
| C09 | Items, blocks, sounds and signs as data (assess first; parts may stay code). Enum names stay stable because saves store them; signs need a sign mechanism | C08 | assessed (2026-10-29), awaits the owner |
| C10 | Authoring CLI: scaffold, references, check (the first custom tool) | C07, C08 | proposed, awaits the owner |

**The order of the rest (2026-10-29).** C05, C06, C08 and C07, then C04 last: its `{npc:…}` placeholder is filled from C08's NPC data, and its
no-literal scan is easiest once every other milestone has moved its strings.

**What C01 to C03 did, and how it differs from section 3 (2026-10-29).**
- **C01.** Draft-07 schemas for every game file and the locale format, in `content/schema/`. Each step type's required fields are written as
  `anyOf` with `not`, because NJsonSchema does not apply `if`/`then`. The licence scan takes `--target`, and the gate scans
  `tests/Vocaterra.Game.Core.Tests` too. `compare-runs.py` masks the bundles' hashes, the checkout's path and the lines `limited.sh` prints
  while it waits.
- **C02.** `GameData.Load` reads every game file once, from the verified bytes, scene included. `GameDataValidator` reports every problem
  between files at once. The reference checks moved there from `EnglishText` and `CheckBank`; `LessonBook` keeps its own, because it resolves
  them into its model. The grammar strings came out byte-identical.
- **C03.** `content/locale/en` holds 815 strings in 10 files, keyed by id, with its own manifest, which `LocaleBundle` checks at load. A new
  locale needs only its strings: the ids and the fallback are fixed.
  - **Lines.** The data keys each gloss by speech-line id (`line.<lineId>`), so a Spanish edit keeps it. The panel still finds the line from
    its text, through the bundle's own speech lines and without case, accents or punctuation. The turn records were not given line ids, which
    3.2 asked for; the effect is the same, and a test checks that every NPC line has a gloss.
  - **The rest.** The model types keep their `English` property names (`PhraseRow.English`, `LessonStep.English`); C04 may rename them.
    `english.json` and `glosses.json` left game-v1, and `game/tips.json` and `game/controls.json` keep the tips' timing and the controls'
    order. The orphan gloss "Necesitas una botella." was dropped.
  - **Core.** Two core findings became core milestones: M54 (the identity guard masks the bundle's hash, which it had pinned) and M55 (the
    manifest tests no longer assume that game-v1 names no support language).
  - **Runs.** The scripted runs differ from the baseline only by the new `locale:` line in `stdout.txt`, the `locale` and `locale_bundle`
    fields of `session_start`, and the report's Locale row.

**What C05, C06 and C08 did, and how it differs from section 3 (2026-10-29).**
- **C05.** `game/words.json` (`GameWords`) holds the target-language words the code held: the guide's directions, the block unit and the list
  joiner, the speech buttons, the offer to type, the support headings, the unseen label, the conjugation tables' pronouns, the stall's key
  words, the help and no-sé lines, the near future, the greeting and the river noun. The validator checks them against the catalogue, the
  lexicon and the sheet, and the grammar check reads them (26 new strings). The articles are the lexicon's `art` readings. `PointLabels` is
  gone: sites, points and named props carry a `label` in the scene. The runs did not change.
- **C06.** `Trigger` (`Event/Trigger.cs`) is one matcher for the tips and the lessons' `do` steps, with an exact `detail`; `count` was not
  added, since an objective step's `target` does the counting (C07). The session raises `torch_at_ring` with the ring's name. The lessons are
  one file each under `game/lessons/`, listed in order by `game/lessons.json`. The runs differ only by the new `torch_at_ring` lines (r01 and
  r10).
- **C08.** `game/npcs.json` lists the NPCs in order, and `game/npcs/<id>.json` gives each one's label, name, figure key and `focus` (the
  phrases each of its prompts practises). The home NPCs and the scaffolded phrases are derived from the focus. `game/host-events.json`
  (`HostEventTable`) replaces the code's table; `NpcHostEvents` keeps only the event names the code waits for. The review placeholder lines are
  each phrase row's `reviewLine`. The lesson loader checks its NPCs against the roster; the validator checks the roster against the agents and
  the prompts, and the tips' and host events' NPCs against the roster. The beacon on an NPC shows the NPC's name or label (`Tendero`), where it
  used to show the raw id. The NPCs' labels and names joined the grammar strings (4 new, all clean).

**What C07 did, and how it differs from section 3 (2026-10-29).**
- `game/session.json` orders `meet-guide`, `camp` and `light-fire`, and names the closing pool, the starting book and the beacon's start
  (`camp.guide`). Each objective is `game/objectives/<id>.json`, read by `ObjectiveBook`. The camp's labels follow the rule in 3.5 and match
  the old words.
- **Triggers.** `light-fire` waits for `fire_lit`, not `torch_at_ring` with `firering.player`: the code still lights the fire and logs
  `fire_lit`, and the session-complete line must follow it. `meet-guide` waits for `guide_met`; `alsoDoneBy: ["camp"]` completes it once the
  checklist moves, and its `guide-met` hook logs `guide_met` then, as the code did.
- **Hooks.** Completion hooks: `guide-met`, `spare-torch`, `point-own-camp`. Restore hooks: `lesson-begun` (the guide is met once a lesson was
  begun) and `scan-fire-ring`. On load the completion hooks put their state back without acting.
- **Logging.** Only a checklist logs `objective_step` and `objective_complete`. The other objectives complete silently: their events are
  already in the log.
- **Stage.** `SessionStage` and `Stage` are gone; `CurrentObjective` (the first not done, `null` when all are) picks the HUD line. The tests
  of `OpeningAndEndingTests` keep every assertion, written against the objective's id.
- **The locale.** `ui.objectiveFindGuide` and `ui.objectiveLightFire` became `objective.meet-guide.hud` and `objective.light-fire.hud`.
- **Save v5.** Steps are keyed `<objective>.<step>`, and only steps without a restore hook are kept. A save older than v5 has its dotless keys
  mapped to `camp.<step>` before the migration. `Fixtures/save-v4.json`, written by the C08 code (fire lit first, then wood 5 and fruit 2),
  loads, keeps its counts across bundles, and still ends when the checklist is done. The test API changed too: `Objective.RecordPickup` and its
  kind became `Objective.Record(event, detail, amount)`, and `HostEvent` takes a world event's name.
- **Runs (C07 and C08 against C06).** The only differences are the saves: version 5 with `camp.*` keys (r01's autosave, r09's save), and the
  stdout lines that report them, 20 bytes longer. C08 changed no run: none points the beacon at an NPC other than the guide.

**What C04 did, and how it differs from section 3 (2026-10-29).**
- **Moved to the locale.** About 150 of the player's English strings are now `ui.*` strings in `locale/en/ui.json`, each listed in
  `LocaleText.UiKeys`, which the validator holds every locale to. They cover the dialogue panel, the HUD (the talk and playtest hints, the
  tips' heading, the new-word card and toasts, the beacon's distance, the loading line, the save toasts), the inventory, the recipes and the
  workbench, the book (its tabs, pages, journal rows and tense columns), the checks, the mic check, the report panel, the crafting rows, the
  bottle hint and the playtest's thanks. The constants that held them became methods that take the locale.
- **Placeholders.** `{npc:<id>}` is what the NPC is called, from C08's data; `GameData` gives the locale the shipped NPCs. Only the support
  language's own text uses it (26 lesson strings, a tip, a check situation). A gloss of a Spanish line that says "Luna" keeps the name written
  out, because the Spanish does. `{key:<command>}` comes from `InputKeys`, a table that `KeyBindings` and the push-to-talk keys now read, and is
  spelled with `locale/en/keys.json`. The tips, the controls help, three lesson texts and the hints use it. A screen's own keys (the dialogue
  panel's Enter and Tab, the lesson strip's letters) stay written out: those screens fix them.
- **Plurals.** `LocaleText.Plural` picks `<id>.one` or `<id>.other`. The beacon now says "1 block".
- **Tests.**
  - `NoLiteralTextTests` scans the two projects. Its allowlist names each file of developer text with its reason, and a second test fails
    an entry that is no longer needed.
  - The font test reads every string of every locale, and checks the glyph list of `locales.json` (`¡`, `«` and `»` joined it).
  - The validator refuses a `{npc:…}` or `{key:…}` that names nothing.
- **Open points (left in code, on the allowlist):**
  - the speech and microphone problem messages: they come from the turn worker, the speech engine and the capture, which have no locale;
  - `BookVerbLabels`' English verb grammar (do/does, am/is/are, will, used to): rules over the listed forms, which a second locale needs
    its own version of;
  - the saved report's and crash file's headings, and the strict-save refusal on the console: these are developer text.
- **Runs.** The text on screen is unchanged; the runs' stdout and session logs (developer text) are the same.

**C09: the assessment (2026-10-29; no code, it awaits the owner).**
- **Sounds (32 cues in `SoundCatalog`).** This is the easiest and safest move: a `game/sounds.json` mapping each cue to a file, a volume and
  a pan, checked against the files and the provenance. The cues stay an enum, because the code raises them. It is worth doing first: it lets
  a sound be swapped without code.
- **Items (8 in `ItemDefinition.All`).** The behaviour stays code: the bottle fills, the torch lights, the book opens. A data file could hold
  only the stack limits and the icon keys, and `nouns.json` already ties each item to its noun. That is little gain for the risk to the saves,
  which store the enum names, so it is not recommended now.
- **Blocks (52 in `BlockDefinition`).** Hardness, drops and the atlas are tied to the meshing and to five `BlockArt*` painters in the game.
  Data would need the art as data too (a texture atlas file), which is a project of its own. Not recommended before the art pipeline.
- **Signs.** There is no sign block or text yet. That is a world feature first (a block, its text by id, target language or locale), then
  data. It needs a design decision.
- **Recommendation.** Do C09a, the sounds as data, alone; leave items and blocks in code; design signs separately.

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
- **Decision record (done, 2026-10-29).** The plan is D-game-21 in `DECISIONS.md` and section 4.7 of `IMPLEMENTATION_PLAN.md`. Section 5
  there records the "Luna"/"Rosa" mismatch, the scene's second read and the beacon-label bug, and `CLAUDE.md` has a line for it.
