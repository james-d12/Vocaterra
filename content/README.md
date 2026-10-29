# Content

Everything the game says, shows and builds its world from, outside the code. The plan behind this layout is `docs/game/CONTENT_PLAN.md`
(D-game-21).

## What lives where

| Path | What it is | Hashed |
| --- | --- | --- |
| `game-v1/` | The content bundle: the core's files and the game's files, in the target language (Spanish), with ids for everything the learner reads in their own language. Its `manifest.json` pins the sha256 of every file; a save records the bundle's hash. | yes, by its manifest |
| `locale/` | The learner's support language (L1): `locales.json` and one folder per locale (`en/` only), every string by its id. Outside game-v1's hash, so an edit here touches no save, fixture or golden; game-v1's `supportLanguage` names the default locale. | yes, by its own manifest |
| `schema/` | The JSON Schemas of the game's files (draft-07), for editors and for one test. | no |
| `grammar/` | The grammar check's strings, its committed LanguageTool cache and its waivers (`docs/game/README.md`). | no |
| `PHRASES.md` | The phrase sheet's source of truth (D-game-10); `PhraseSheetTests` keeps `game-v1/game/phrases.json` in step with it. | no |
| `speech-index.json` | What each pre-rendered WAV of `game-v1/speech/` says, and how it was rendered. | no |

## Who owns what in `game-v1/`

- **The core's files.** The core's loader parses and checks them: `intents/`, `prompts/`, `dialogue/`, `agents/` (every `.json` at any depth
  is an agent), `answer-sets/`, `lexicon/`, `cefr/`, `effects/`, `feedback/`, `evaluation/`, `provenance.json` and `ATTRIBUTION.md`. Their formats
  are the core's (`docs/nlp/`); they carry no `$schema`.
- **The game's files (host files).** Any other file in the manifest is the game's. The core hashes it and hands its verified bytes over
  (`ContentBundle.TryReadHostFile`). They are `game/*.json` and the speech WAVs in `speech/`. The game reads them all once, from those bytes, in
  `GameData.Load` (`Vocaterra.Game.Core/Content/`), which also checks them against each other. Each game file names its schema:

  | File | Schema | Loaded by (`Vocaterra.Game.Core`) |
  | --- | --- | --- |
  | `game/nouns.json` | `nouns.schema.json` | `NounCatalog` |
  | `game/recipes.json` | `recipes.schema.json` | `RecipeBook` |
  | `game/phrases.json` | `phrases.schema.json` | `PhraseSheet` |
  | `game/lessons.json` | `lessons.schema.json` (the lessons' ids, in order) | `LessonBook` |
  | `game/lessons/<id>.json` | `lesson.schema.json` (one lesson each, C06) | `LessonBook` |
  | `game/checks.json` | `checks.schema.json` | `CheckBank` |
  | `game/tips.json` | `tips.schema.json` | `TipBook` |
  | `game/controls.json` | `controls.schema.json` | `TipBook` |
  | `game/words.json` | `words.schema.json` | `GameWords` (the target-language words the game itself uses: the guide's directions, the buttons, the pronouns, the key words, the greeting...) |
  | `game/npcs.json` | `npcs.schema.json` (the NPCs' ids, in order; the saves keep this order) | `NpcRoster` |
  | `game/npcs/<id>.json` | `npc.schema.json` (one NPC each, C08: its label, name, figure and the phrases its prompts practise) | `NpcRoster` |
  | `game/host-events.json` | `host-events.schema.json` (what each NPC host event does: give an item, point the beacon at a noun) | `HostEventTable` |
  | `game/scene.json` | `scene.schema.json` (the file is JSON with `//` comments) | `SceneLayout` |

## The support language: `locale/`

- **One id per string.** Each file of `locale/<code>/` is `{ "$schema", "about", "strings": { "<id>": "<text>" } }` (`locale.schema.json`). The ids:

  | Id | What | File of `en/` |
  | --- | --- | --- |
  | `ui.<key>` | an interface word (`LocaleText.UiKeys`) | `ui.json` |
  | `npc.<id>.role`, `objective.<id>.title` | an NPC's role, an objective's title | `ui.json` |
  | `tip.<id>`, `control.<id>.keys`, `control.<id>.text` | a tip; a line of the controls help | `tips.json`, `controls.json` |
  | `feedback.outcome.<key>`, `feedback.error.<tag>`, `feedback.unknownError` | the feedback after a turn | `feedback.json` |
  | `noun.<lemma>` | a catalogue noun's word | `nouns.json` |
  | `phrase.<id>.meaning`, `.card`, `.explain` | a phrase's meaning, support card and explanation | `phrases.json` |
  | `line.<lineId>` | the gloss of an NPC line, by its speech-line id (`line.prompt.<promptId>` for a prompt's text) | `lines.json` |
  | `lesson.<id>.title`, `.text`, `.summary`, `.objective`; `lesson.<id>.<step>.text`, `.translation`, `.help`, `.retry`, `.option.<n>`, `.hud`, ... | a lesson's text | `lessons.json` |
  | `check.<id>.question`, `.option.<n>`, `.situation` | a check item's text | `checks.json` |
  | `verb.<lemma>` | `{ "gloss", "base", "third", "past" }`: a verb's forms, listed | `verbs.json` |

- **Placeholders.** `{name}` is filled by the code (`{npc}`, `{count}`, `{noun}` ...).
- **Lookup.** The chosen locale (`--locale <code>`; default game-v1's `supportLanguage`), then `en`, then the marker `⟦id⟧` on screen.
- **Checks.** `--validate-content` (and so the refresh and the tests) refuses a string the content or the code asks for that a locale lacks, and a
  string no content or code asks for (an orphan, left behind by a renamed id).
- **A new locale.** Add a folder `locale/<code>/` with the same ids, list it in `locales.json` (with the characters beyond ASCII it needs in `glyphs`),
  and run the refresh. A string it lacks falls back to `en`.

## Schemas

- **What they are for.** Rider and VS Code read the `$schema` of a file and check and complete it as you type. The loaders stay the
  authority: they also check what a schema cannot, such as a phrase that is not in the sheet, a line with no recording, or a noun the
  lexicon lacks.
- **The test.** `tests/Vocaterra.Game.Core.Tests/Content/ContentSchemaTests.cs` checks that every game file names its schema and matches it,
  that every schema is used, and that the item and block names in `nouns.schema.json` are the code's tables.
- **Changing a format.** Change the loader, the schema and the files together. A field the loader reads but the schema lacks fails the test
  for the file that uses it.

## The edit loop

1. Edit the files (the schema checks them in the editor).
2. A new file in `game-v1/` needs a provenance item in `game-v1/provenance.json` (`generatedBy`, `approvedBy`, `approvedOn`). The locale bundle
   has no provenance file.
3. Run `scripts/limited.sh scripts/content.sh refresh`. In order, it:
   - rewrites `game-v1/manifest.json` and `locale/manifest.json`;
   - lists any file with no provenance item;
   - validates;
   - runs the gate's lint (`--strict --level a1 --host-pools 'guide.*'`) and the CEFR check at A1 and A2;
   - runs the game's own checks between its files (`dotnet run --project src/Vocaterra.Host -- --validate-content`): a phrase, line, prompt,
     noun or NPC that one file names and another lacks, every problem at once (`GameDataValidator`);
   - writes the grammar strings again and runs the grammar check offline from the committed cache.

   New Spanish text misses the cache. The message then names the LanguageTool steps (`docs/game/README.md`, "Refreshing the cache").
4. Run the whole test suite. The core's tests run on `game-v1` too (R02), so a failing core test is the signal that the edit broke
   something the core relies on.
5. If the end-to-end goldens moved, record them again (`VOCATERRA_RECORD_GOLDEN=1 dotnet test tests/Vocaterra.Nlp.Golden.Tests`).
   When `game-v1` changed, put its new bundle hash into `tests/Vocaterra.Game.Core.Tests/Fixtures/save-v1.json`; an edit of `locale/` alone
   needs neither.
6. Commit the edit with the manifest, the grammar strings and the cache.

## Adding things

- **A trigger.** A tip, a lesson's `do` step and (from C07) an objective's step wait for a trigger: `{ "event": "<world event>", "npc": "<agent>", "detail":
  "<exact detail>" }` (only `event` is required; a tip may also wait for `start` or `aimed_npc`). The events are `WorldEvents` (`Event/WorldEvent.cs`), and the
  session log shows each one with its NPC and detail.
- **A lesson.** Add a file `game-v1/game/lessons/<id>.json` (`lesson.schema.json` lists each step type's fields), its id to `game-v1/game/lessons.json`
  where it should come, a provenance item, and its text to
  `locale/en/lessons.json` (`lesson.<id>.title`, `.text`, and each step's `lesson.<id>.<step>.text`, ...). Its phrases must be rows of
  `game/phrases.json`, its lines recorded speech lines, and its `do` events world events (`WorldEvents`).
- **A phrase.** Add the row to `PHRASES.md` first (D-game-10), then to `game-v1/game/phrases.json`, and its meaning, card and explanation to
  `locale/en/phrases.json`. Render its recordings (`scripts/prerender-speech.sh`). A phrase the guide reviews may name a `reviewLine`, the dialogue
  line she asks it with while the bundle has no review pool for it (`guide.review.phrase.<id>`).
- **A noun.** Add it to `game-v1/game/nouns.json` and its word to `locale/en/nouns.json` (`noun.<lemma>`). The lexicon must have a noun
  reading of it with a gender.
- **A place the beacon names.** Give the site, the point or the named prop a `label` in `game/scene.json` (`"label": "río"`); a place with no label shows
  its own name.
- **An NPC.** The core's `agents/<id>.json` and its prompts (each prompt's `agentId`) come first. Then add `game-v1/game/npcs/<id>.json` (its label over the
  head, its name if it has one, its figure key, and `focus`: the learner phrases each of its prompts practises), its id to `game-v1/game/npcs.json`
  (at the end: the saves keep the NPCs in this order), its place to `game/scene.json` `npcs`, its role to `locale/en/ui.json` (`npc.<id>.role`) and a
  provenance item. A figure key the game does not draw yet gets a plain grey figure. What its host events do in the world goes in
  `game/host-events.json`; an event not listed there changes nothing. The validator checks the NPC against the agents, the prompts and the phrases.
- **An NPC line.** Add it to `game-v1/dialogue/dialogue.json` (the core's file) and its gloss to `locale/en/lines.json` (`line.<lineId>`).
