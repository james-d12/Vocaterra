# Content

Everything the game says, shows and builds its world from, outside the code. The plan behind this layout is `docs/game/CONTENT_PLAN.md`
(D-game-21).

## What lives where

| Path | What it is | Hashed |
| --- | --- | --- |
| `game-v1/` | The content bundle: the core's files and the game's files. Its `manifest.json` pins the sha256 of every file; a save records the bundle's hash. | yes, by its manifest |
| `schema/` | The JSON Schemas of the game's files (draft-07), for editors and for one test. | no |
| `grammar/` | The grammar check's strings, its committed LanguageTool cache and its waivers (`docs/game/README.md`). | no |
| `PHRASES.md` | The phrase sheet's source of truth (D-game-10); `PhraseSheetTests` keeps `game-v1/game/phrases.json` in step with it. | no |
| `speech-index.json` | What each pre-rendered WAV of `game-v1/speech/` says, and how it was rendered. | no |

## Who owns what in `game-v1/`

- **The core's files.** The core's loader parses and checks them: `intents/`, `prompts/`, `dialogue/`, `agents/` (every `.json` at any depth
  is an agent), `answer-sets/`, `lexicon/`, `cefr/`, `effects/`, `feedback/`, `evaluation/`, `provenance.json` and `ATTRIBUTION.md`. Their formats
  are the core's (`docs/nlp/`); they carry no `$schema`.
- **The game's files (host files).** Any other file in the manifest is the game's. The core hashes it and hands its verified bytes over
  (`ContentBundle.TryReadHostFile`). They are `game/*.json` and the speech WAVs in `speech/`. Each game file names its schema:

  | File | Schema | Loaded by (`Vocaterra.Game.Core`) |
  | --- | --- | --- |
  | `game/nouns.json` | `nouns.schema.json` | `NounCatalog` |
  | `game/recipes.json` | `recipes.schema.json` | `RecipeBook` |
  | `game/phrases.json` | `phrases.schema.json` | `PhraseSheet` |
  | `game/lessons.json` | `lessons.schema.json` | `LessonBook` |
  | `game/checks.json` | `checks.schema.json` | `CheckBank` |
  | `game/glosses.json` | `glosses.schema.json` | `VerbGlosses` |
  | `game/english.json` | `english.schema.json` | `EnglishText` |
  | `game/scene.json` | `scene.schema.json` (the file is JSON with `//` comments) | `SceneLayout` |

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
2. A new file in `game-v1/` needs a provenance item in `game-v1/provenance.json` (`generatedBy`, `approvedBy`, `approvedOn`).
3. Run `scripts/limited.sh scripts/content.sh refresh`. In order, it:
   - rewrites `game-v1/manifest.json`;
   - lists any file with no provenance item;
   - validates;
   - runs the gate's lint (`--strict --level a1 --host-pools 'guide.*'`) and the CEFR check at A1 and A2;
   - writes the grammar strings again and runs the grammar check offline from the committed cache.

   New Spanish text misses the cache. The message then names the LanguageTool steps (`docs/game/README.md`, "Refreshing the cache").
4. Run the whole test suite. The core's tests run on `game-v1` too (R02), so a failing core test is the signal that the edit broke
   something the core relies on.
5. If the end-to-end goldens moved, record them again (`VOCATERRA_RECORD_GOLDEN=1 dotnet test tests/Vocaterra.Nlp.Golden.Tests`).
   Then put the new bundle hash into `tests/Vocaterra.Game.Core.Tests/Fixtures/save-v1.json`.
6. Commit the edit with the manifest, the grammar strings and the cache.

## Adding things

- **A lesson.** Add it to `game-v1/game/lessons.json` (`lessons.schema.json` lists each step type's fields). Its phrases must be rows of
  `game/phrases.json`, its lines recorded speech lines, and its `do` events world events (`WorldEvents`).
- **A phrase.** Add the row to `PHRASES.md` first (D-game-10), then to `game-v1/game/phrases.json`. Render its recordings
  (`scripts/prerender-speech.sh`).
- **A noun.** Add it to `game-v1/game/nouns.json` and its English to `game-v1/game/english.json` (`nouns`). The lexicon must have a noun
  reading of it with a gender.
