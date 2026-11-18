# Content

Everything the game says, shows and builds its world from, outside the code. The plan behind this layout is `docs/game/content-plan.md`
(D-game-21).

## What lives where

| Path | What it is | Hashed |
| --- | --- | --- |
| `game-v1/` | The content bundle: the core's files and the game's files, in the target language (Spanish), with ids for everything the learner reads in their own language. Its `manifest.json` pins the sha256 of every file; a save records the bundle's hash. | yes, by its manifest |
| `locale/` | The learner's support language (L1): `locales.json` and one folder per locale (`en/` only), every string by its id. Outside game-v1's hash, so an edit here touches no save, fixture or golden; game-v1's `supportLanguage` names the default locale. | yes, by its own manifest |
| `schema/` | The JSON Schemas of the game's files (draft-07), for editors and for one test. | no |
| `grammar/` | The grammar check's strings, its committed LanguageTool cache and its waivers (`docs/game/content.md`). | no |
| `PHRASES.md` | The phrase sheet's source of truth (D-game-10); `PhraseSheetTests` keeps `game-v1/game/phrases.json` in step with it. | no |
| `speech-index.json` | What each pre-rendered WAV of `game-v1/speech/<speaker>/` (the word cards in `speech/cards/`) says, and how it was rendered. | no |

## Who owns what in `game-v1/`

- **The core's files.** The core's loader parses and checks them: `intents/`, `prompts/`, `dialogue/`, `agents/` (every `.json` at any depth
  is an agent), `answer-sets/`, `lexicon/`, `cefr/`, `effects/`, `feedback/`, `evaluation/`, `provenance.json` and `ATTRIBUTION.md`. Their formats
  are the core's (`docs/nlp/`); they carry no `$schema`.
- **The game's files (host files).** Any other file in the manifest is the game's. The core hashes it and hands its verified bytes over
  (`ContentBundle.TryReadHostFile`). They are `game/*.json` and the speech WAVs in `speech/`. The game reads them all once, from those bytes, in
  `GameData.Load` (`Vocaterra.Game.Core/Content/`), which also checks them against each other. Each game file names its schema:

  | File | Schema | Loaded by (`Vocaterra.Game.Core`) |
  | --- | --- | --- |
  | `game/items.json` | `items.schema.json` (the item kinds, I01: one row per item, see "Items" below) | `ItemCatalogue` (an `IItemCatalogue`) |
  | `game/nouns.json` | `nouns.schema.json` | `NounCatalog` |
  | `game/recipes.json` | `recipes.schema.json` | `RecipeBook` |
  | `game/phrases.json` | `phrases.schema.json` | `PhraseSheet` |
  | `game/lessons.json` | `lessons.schema.json` (the lessons' ids, in order) | `LessonBook` |
  | `game/lessons/<id>.json` | `lesson.schema.json` (one lesson each, C06; schema 2, village-plan V08: the lesson's `level`, `a1` or `a2`) | `LessonBook` |
  | `game/checks.json` | `checks.schema.json` (schema 2, village-plan V07: each item's `lesson`, null for none; a battery asks the items of the lessons its play session has unlocked, and always those of the first `coreSessions`) | `CheckBank` |
  | `game/tips.json` | `tips.schema.json` | `TipBook` |
  | `game/controls.json` | `controls.schema.json` | `TipBook` |
  | `game/words.json` | `words.schema.json` | `GameWords` (the target-language words the game itself uses: the guide's directions, the buttons, the pronouns, the greeting...; the key words moved to the NPC files in schema 2) |
  | `game/session.json` | `session.schema.json` (the objectives in the HUD's order, the closing line's pool, the starting items, the beacon's start) | `ObjectiveBook` |
  | `game/objectives/<id>.json` | `objective.schema.json` (one objective each, C07: its steps and their triggers, its hooks; V03: the play session it starts in, and the orders; D-game-27: an order the learner hands over in Spanish, `handOver`, whose NPC needs a `receiving` state and a `<npc>-receive` prompt) | `ObjectiveBook` |
  | `game/npcs.json` | `npcs.schema.json` (the NPCs' ids, in order; the saves keep this order) | `NpcRoster` |
  | `game/npcs/<id>.json` | `npc.schema.json` (one NPC each, C08: its label, name, figure and the phrases its prompts practise; schema 2, V01: the prompt of each state, the opening, the voice, the key words, the pool prefix) | `NpcRoster` |
  | `game/host-events.json` | `host-events.schema.json` (what each NPC host event does: give items, point the beacon at a noun, a place or the place the learner names; schema 2, V02; W08: the teacher's kinds `procedure_repeat`, `procedure_slower`, `procedure_help`, `give_missing`, `teach_recipe`, `start_procedure`; Y05: `grant_lodging`; Y11: `sell_plot`) | `HostEventTable` |
  | `game/needs.json` | `needs.schema.json` (the player's needs, N01: the three drives' rates, effort factors and four stages with their effects, health's recovery and warnings, stamina's costs and refill, rest at a fire, a night and a collapse, the foods, the sleep places (Y01: where the player sleeps; not `rest`) and the death rules (`wakeAt` names a sleep place); the stage lines' keys, not their text) | `NeedsData` |
  | `game/day.json` | `day.schema.json` (the day and night cycle, E01: its length in seconds, the hours dawn, day, dusk and night begin, the start hour and the wake hour) | `DayData` |
  | `game/weather.json` | `weather.schema.json` (the weather, E04: its kinds as rows of channels (cloud cover, sunlight, fog, wind, precipitation: a kind and an intensity from 0 to 1; since E06 the kind names a row of the game's style table, `PrecipitationStyles` in `src/Vocaterra.Game/Graphics/Weather/`, which says how it looks, so a new precipitation kind needs a row there as well as in this file, and a test fails until it has one; the kind of a style the table lacks draws nothing), the range of a spell, the weighted next kinds, a morning weight; the kind a new game starts in and the transition time) | `WeatherData` |
  | `game/temperature.json` | `temperature.schema.json` (the temperature where the player stands, I06: the numbers of the base, the day's swing, the weather's cooling per channel, a roof's shelter and a fire's warmth, all in tenths of a degree Celsius, and the bands warm, cool, cold and very cold from the warmest, each with a lower bound, a walk factor above 0 and at most 1, a chill and the HUD line said on entering the cold) | `TemperatureData` |
  | `game/equipment.json` | `equipment.schema.json` (the slot groups gear is worn in, I03a: per group its id, its drawn layer, its slots (named body slots, or a count with a wear kind), the item tags it accepts, `noDuplicates` and `statsApply`, and the Spanish noun of each slot; see "Equipment" below) | `EquipmentData` |
  | `game/damage.json` | `damage.schema.json` (the damage causes and the modifiers that apply to each, I05a: `drowning` with none; see "Stat keys and damage" below) | `DamageData` |
  | `game/underground.json` | `underground.schema.json` (the underground, B11 and B11b: the rock layers and their pockets, the bedrock floor, the caves, the entrances' passages, the lava, the ores, the cave linings and the surface rock; blocks by their names in `BlockDefinition`; every number a whole number, noise values in thousandths; see "Editing the underground" below) | `UndergroundData` |
  | `game/land.json` | `land.schema.json` (the land rules, Y09: the discs round scene sites and the squares round the road and bridge blocks where the player may not place blocks (a torch is always allowed), each with the protection that names its hint (village, camp, road) and a margin; the discs round `meadow.north` and `meadow.south` where a plot may lie; the plot's width, depth, price in coins and the slope it allows. Site names are scene sites: the validator refuses one the scene lacks) | `LandData` |
  | `game/scene.json` | `scene.schema.json` (the file is JSON with `//` comments) | `SceneLayout` |
  | `game/procedures.json` | `procedures.schema.json` (the procedures' ids, in order; W05) | `ProcedureBook` |
  | `game/procedures/<id>.json` | `procedure.schema.json` (one procedure each: its station, teacher, level, what it makes and uses, its steps, chunks and goal; W05) | `ProcedureBook` |
  | `game/stations.json` | `stations.schema.json` (each station: its scene prop, its NPC and its fixtures; W05) | `StationBook` |
  | `game/actions.json` | `actions.schema.json` (for each use and fixture, the verbs a step may use; W05) | `ActionTable` |

## The support language: `locale/`

- **One id per string.** Each file of `locale/<code>/` is `{ "$schema", "about", "strings": { "<id>": "<text>" } }` (`locale.schema.json`). The ids:

  | Id | What | File of `en/` |
  | --- | --- | --- |
  | `ui.<key>` | an interface word (`LocaleText.UiKeys`) | `ui.json` |
  | `npc.<id>.role`, `objective.<id>.title`, `objective.<id>.hud` | an NPC's role, a checklist's title, another objective's line | `ui.json` |
  | `tip.<id>`, `control.<id>.keys`, `control.<id>.text` | a tip; a line of the controls help | `tips.json`, `controls.json` |
  | `feedback.outcome.<key>`, `feedback.error.<tag>`, `feedback.unknownError` | the feedback after a turn | `feedback.json` |
  | `noun.<lemma>` | a catalogue noun's word | `nouns.json` |
  | `phrase.<id>.meaning`, `.card`, `.explain` | a phrase's meaning, support card and explanation | `phrases.json` |
  | `line.<lineId>` | the gloss of an NPC line, by its speech-line id (`line.prompt.<promptId>` for a prompt's text) | `lines.json` |
  | `lesson.<id>.title`, `.text`, `.summary`, `.objective`; `lesson.<id>.<step>.text`, `.translation`, `.help`, `.retry`, `.option.<n>`, `.hud`, ... | a lesson's text | `lessons.json` |
  | `check.<id>.question`, `.option.<n>`, `.situation` | a check item's text | `checks.json` |
  | `verb.<lemma>` | `{ "gloss", "base", "third", "past" }`: a verb's forms, listed | `verbs.json` |
  | `key.<GameKey>` | a key's name (`key.Escape`: `Esc`), for `{key:…}` | `keys.json` |

- **Placeholders.** `{name}` is filled by the code (`{npc}`, `{count}`, `{noun}` ...). Two are filled from data (C04):
  - `{npc:<id>}` is what the NPC is called (`{npc:guide}` is `Luna`, `game/npcs/guide.json`'s name, else its label). Use it in the support
    language's own text (a lesson's text, a tip); a gloss of a Spanish line that says the name keeps it written out, as the Spanish does.
  - `{key:<command>}` is the key bound to a command (`InputKeys`: `listen`, `talk`, `book`, `inventory`, `crafting`, `save`, ...), spelled with
    `key.<GameKey>`; a rebound key then shows in every text. A screen's own keys (the dialogue panel's Enter and Tab) are written out.
- **Plurals.** A string with a count has two ids, `<id>.one` and `<id>.other` (`ui.bookUses.one`: `{count} time`).
- **The code holds no player-facing English** (C04): `NoLiteralTextTests` scans `Vocaterra.Game` and `Vocaterra.Game.Core`, with an allowlist for
  developer text. A new on-screen text is a `ui.<key>` string (and its key in `LocaleText.UiKeys`).
- **Glyphs.** `locales.json` lists each locale's characters beyond ASCII; a test checks that the font draws every character of every string.
- **Lookup.** The chosen locale (`--locale <code>`; default game-v1's `supportLanguage`), then `en`, then the marker `⟦id⟧` on screen.
- **Checks.** `--validate-content` (and so the refresh and the tests) refuses a string the content or the code asks for that a locale lacks, a
  string no content or code asks for (an orphan, left behind by a renamed id), and a `{npc:…}` or `{key:…}` that names no NPC or command.
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
3. Run `scripts/content-loop.sh --grammar --speech` (V18). Run it directly, never under `scripts/limited.sh`: it calls `limited.sh` itself for
   each heavy step, and refuses to start under it. In order, it:
   1. runs `scripts/content.sh refresh` (below);
   2. with `--grammar`, starts the local LanguageTool server, checks the strings the cache lacks and prunes the entries no string uses, checks
      that a cache rebuilt from nothing equals it (CI's `languagetool` job), prints every new finding, and stops the server, even on a failure
      (`docs/game/content.md`, "Refreshing the cache"). Read each finding it prints: fix the content, or add a waiver with its reason to
      `grammar/game-v1-waivers.json`. Without `--grammar`, new Spanish stops the loop at step 1;
   3. asks the renderer which speech files are new or changed (`scripts/prerender-speech.sh --check`) and, with `--speech`, renders them and
      refreshes again (the manifest after the new WAVs, provenance, the strings);
   4. writes the bundle's new `bundleHash` into every file under `tests/` that holds the old one (the save fixture `save-v10.json`); an edit of `locale/` alone moves none;
   5. builds the solution, then checks the end-to-end goldens and the identity baseline (`tests/Vocaterra.Nlp.Golden.Tests`). When the edit means
      to move them, run it again with `--record`: it records them and prints what moved (commit them in a commit of their own). It then checks
      that the fuzz suite's stream digests (`FuzzLanguageStreamTests`) did not move: since the core's M57 they run on frozen inputs, a copy of
      the bundle's grammars and words in the fuzz suite's fixtures, so a content edit never moves them, and a move means a generator changed;
   6. runs the fast tier (`scripts/test.sh fast`), then the fuzz suite's grammar-sensitive classes at their full depth, so a grammar that
      generates a wrong sentence fails in minutes; `--tier full` runs the whole suite instead, as the gate does.

   It stops at the first failure with the next thing to do, and ends with a summary of at most 30 lines (each step, what it changed, its log);
   each step's whole output is in `.claude/content-loop/<stamp>/`. The options and the steps are in the script's header and `scripts/README.md`.
   A content milestone's brief needs only that one line.

   Its first step also runs alone, `scripts/limited.sh scripts/content.sh refresh` (the content checks without the build and the tests). In
   order, it:
   - rewrites `game-v1/manifest.json` and `locale/manifest.json`;
   - lists any file with no provenance item;
   - validates;
   - runs the gate's lint at the bundle's ceiling, A2 (`--strict --level a2 --fail-on-level --host-pools 'guide.*'`: a lemma above A2 in a
     dialogue line, a prompt's text or an answer set's canonical answer fails), then `cefr-check` at A2, which fails on any lemma it lists, and
     at A1, a report of what is above A1 that never fails (village-plan 3.9, D-game-22 (b));
   - runs the game's own checks between its files (`dotnet run --project src/Vocaterra.Host -- --validate-content`): a phrase, line, prompt,
     noun or NPC that one file names and another lacks, and a lesson above its own level (below), every problem at once (`GameDataValidator`);
   - writes the grammar strings again and runs the grammar check offline from the committed cache. New Spanish text misses the cache, and the
     message names the LanguageTool steps.
4. Commit the edit with what the loop changed (the manifests, the grammar strings and the cache, the speech files, `speech-index.json` and the
   listening checklist, the save fixtures), and the re-recorded goldens in a commit of their own. Run the whole suite (`scripts/test.sh full`,
   or the loop with `--tier full`) before the milestone ends. The core's tests run on `game-v1` too (R02), so a failing core test is the signal
   that the edit broke something the core relies on.

## Items (I01)

`game/items.json` holds one row per item the player can hold, and nothing in code lists items: `GameData.Items` (an `IItemCatalogue`) is read once and passed to what needs an item's name, stack limit,
tool or picture. A row has `name` (the stable key every other file and the saves use: `wood`, `camp-kit`; never renamed), `id` (a stable number, used by the state digest and the code's `ItemId`: the
ids below 100 are the first game's, a new item takes the next free number from 100, and an id is never reused), `stack` (how many one slot holds), `icon` (a key of the block atlas, which has art for
the keys `BlockAtlasLayout.IconKeys` lists; an unknown key draws the plain fallback tile and the content check reports it), `held` (how the hand shows it: `sprite`, the default, is the icon extruded into
voxels; `log`, `torch` and `bottle` are modelled in code), `tags` (an open list of words: `tool`, `food`, `material`, `container`, `currency`, `placeable`) and, for a tool, `tool` (`class`, the material
it works on, and `speed`, how many times faster than bare hands). The other files name an item by its `name` (the nouns' `items`, a shop row, a recipe, a gift, a procedure). The code's `ItemId` enum keeps
only the items that behaviour refers to (the bottle fills, the book opens, coins pay, the stake and the camp kit place); the loader checks each against its row and stops on a mismatch, and an item
that is a row alone works everywhere an item can. The gear fields `wear`, `stats`, `look` and `capacity` are described under "Equipment (I03a)" below; `durability` is added when the wear milestone needs it. The full steps for adding an item (the picture, the noun, the word, the lexicon) are I10's.

## Adding things

These steps are by hand. An authoring CLI that does them (`new npc|objective|lesson`, `refs <id>` to find an id's uses, and `check`) is
specified for later in `docs/game/content-plan.md` section 3.8 (C10); it is not built.

- **A trigger.** A tip, a lesson's `do` step and an objective's step wait for a trigger: `{ "event": "<world event>", "npc": "<agent>", "detail":
  "<exact detail>" }` (only `event` is required; a tip may also wait for `start` or `aimed_npc`). The events are `WorldEvents` (`Event/WorldEvent.cs`), and the
  session log shows each one with its NPC and detail. Since V02: `beacon_reached` (the player reached the beacon; the detail is the scene's name of the place,
  `stall`, or the word of an arrow on a noun's block) and `place_asked` (an NPC answered directions; the place found or `unknown`). Since E01: `day_phase` (the day clock crossed into Since Y05: `lodging_granted` (the player was first given lodging by the host action `grant_lodging`; no NPC, no detail). The host-event kinds are listed in `HostEventTable.Kinds`; `grant_lodging` takes no fields beyond `kind`. Since Y11: `plot_bought` (the player bought the plot from the mayor by the host action `sell_plot`; the NPC is the seller, the detail the plot's corner `x,z`; an autosave reason like `lodging_granted`).
  another phase; the detail is `dawn`, `day`, `dusk` or `night`; a sleep's jump to the morning and a load raise none) and, since E04, `weather_changed` (the weather
  changed kind: a spell's end, a morning's roll; the detail is the new kind's id, a row of `game/weather.json`; a load raises none).
  An NPC's file may list `weatherAnswers` (`["ask_weather"]`, E07): that intent is then answered with the pool its agent names on the host event `<id>.<key>`
  (a transition the game never fires, as `<id>.weather`), the key being the `say` of the weather kind now (`game/weather.json`; `day` and `night` keys); the
  validator needs a `say` on every kind and that transition and its pool for every key.
- **A lesson.** Add a file `game-v1/game/lessons/<id>.json` (`lesson.schema.json` lists each step type's fields), its id to `game-v1/game/lessons.json`
  where it should come, a provenance item, and its text to
  `locale/en/lessons.json` (`lesson.<id>.title`, `.text`, and each step's `lesson.<id>.<step>.text`, ...). Its phrases must be rows of
  `game/phrases.json`, its lines recorded speech lines, and its `do` events world events (`WorldEvents`). A `do` step may point the beacon when it begins, at
  a word (`point`: the labelled place of the scene that shows it, nearest first, else the catalogue noun's nearest block, #43) or at a named place of the scene (`pointPlace`, V02; one of the two); a walk to a place waits for its arrival:
  `{ "type": "do", "pointPlace": "stall", "trigger": { "event": "beacon_reached", "detail": "stall" } }`.
- **A lesson's level.** Every lesson declares the level it keeps to, `"level": "a1"` or `"a2"` (schema 2, village-plan 3.9 and V08; sessions 1
  to 6 are `a1`). The bundle's ceiling is A2 (the gate's lint), and the game's validator (`--validate-content`, in `refresh` and the gate)
  holds each lesson to its own level:
  - the Spanish it teaches and says, which is its phrases, the lines its steps name (a review step's candidates too) and the text of the prompts its
    turns are evaluated at, has no lemma above its level in `game-v1/cefr/a1.txt` (and, at `a2`, `a2.txt`; the lists are cumulative). The words are
    lemmatized by the core's `CefrChecker`, as the lint does;
  - a phrase's variants, which the learner may say instead and no lesson teaches, are held to the ceiling, A2, whatever the lesson's level;
  - the lists hold lemmas, so a form's tense is invisible to them: the tenses its `verbs` and its steps name are the level's, `pres`, `pret`
    and `ir-a` at `a1`, and `fut` (the simple future, V16) as well at `a2`.

  A finding names the lesson's file, the text, what says it and the word above the level. A word the world needs that the lists put above A1 (a
  noun the player handles, a place the beacon names) joins the marked block at the end of `cefr/a1.txt` with a one-line reason.
- **A step's person and tense.** A `repeat` or `answer` step may declare the `person` of the verb its answer must have (`1sg`, `2sg`, `3sg`, `1pl`,
  `2pl`, `3pl`) and its `tense`: `pres`, `pret`, `ir-a` (the near future, `voy a comprar`) or `fut` (the simple future, `compraré`; an `a2` lesson
  only, V16). Their lines are the locale's: `lesson.<id>.<step>.personRetry` and `.tenseRetry` (shown when the right phrase comes with another
  person or tense and the core flags nothing). Correct Spanish is never a miss: an `ir-a` step credits a plan said in the present with a time word
  or in the simple future and shows its `.presentNote`; a `fut` step credits the near future or a present with a time word and shows its
  `.futureNote`. Each note names the form the step practises (`That is right too. Here we practise “compraré”: Mañana compraré pan.`).
- **A time word.** The words that make a present a plan are `game/words.json`'s `nearFuture.timeWords`. An entry of several words (`semana que
  viene`, `próxima semana`) is matched as a whole, in order. Keep them to the core's future markers (`docs/nlp/api.md`, "Tense errors") and to the
  adverbs that say later (`luego`, `después`), which the core does not grade: a verb inside an entry (`viene`) is no longer the answer's verb.
  The intents that take a time word list the same markers as phrases of their grammars: `state_action`'s take `pasado mañana`, `la semana que viene`,
  `la próxima semana`, `el próximo día` (and `mes`, `año`), `la semana pasada` and `el año pasado` at either end since V17, so the item of `la semana
  que viene compro pan` is `pan`, not `semana` (a grammar that lacks a marker binds its noun as a slot).
- **A phrase.** Add the row to `PHRASES.md` first (D-game-10), then to `game-v1/game/phrases.json`, and its meaning, card and explanation to
  `locale/en/phrases.json`. Render its recordings (`scripts/prerender-speech.sh`). A phrase the guide reviews may name a `reviewLine`, the dialogue
  line she asks it with while the bundle has no review pool for it (`guide.review.phrase.<id>`).
- **A noun.** Add it to `game-v1/game/nouns.json` and its word to `locale/en/nouns.json` (`noun.<lemma>`). The lexicon must have a noun
  reading of it with a gender.
- **An item and its word (the stake, Y10).** An item is a row of `Items/ItemDefinition.cs` (its id, a stable English key, its stack limit; an `ItemId` number is never reused), its icon a
  cell of the block atlas (`BlockCell.Icon…`, drawn in code in `BlockArt`, CC0 by construction; `BlockAtlasLayout.IconFor`), its Spanish noun a row of `game/nouns.json` whose `items` names the key
  (`{"lemma": "estaca", "items": ["stake"]}`) with the English word in `locale/en/nouns.json`, so `--give estaca` and the word card work. A word the lists lack (`estaca`: in neither of the core's
  lists) joins the end of `cefr/a1.txt` with its reason and `lexicon/game.json` with its forms (`estaca`, `estacas`, feminine); the A2 gate and `cefr-check` stay clean. A first card of a new noun
  needs its two recordings (`content-loop.sh --grammar --speech`). `estaca` reads in the request forms (`Quiero una estaca`, `¿Me das una estaca?`: `request_item`, slot `estaca`); a bare «una estaca»
  is `unknown`, as «una manzana» is. The camp kit (Y11, `ItemId.CampKit`, `camp-kit`, stack of one) is the second such item: its noun is `equipo` (A2, in the lexicon; `{"lemma": "equipo", "items": ["camp-kit"],
  "labels": {"camp-kit": "equipo de campamento"}}`), so the inventory and the word card read «equipo de campamento» and the line says «Toma el equipo»; `tienda` stays the stall's word.
- **The mayor and the plot (Y11).** Tomás, `villager`, is the `alcalde` (`label` «Alcalde», `npc.villager.role` "The mayor"; `alcalde` and `terreno` joined the end of `cefr/a1.txt` like `estaca`, but not `lexicon/game.json`: the Wiktionary import already has both as nouns, and `GeneratedLexiconTests.TheImportLeavesTheHandAuthoredWordsAlone` refuses a hand-authored entry for a lemma the import defines; `terreno` is a catalogue
  noun with no item so a request for it can be answered). His agent keeps `idle` and `pointing` and has two states for a yes-or-no question, `offering_stake` (prompt `mayor-stake`) and `offering_plot` (prompt `mayor-sell`), the shape of an order's offer:
  from the play session the checklist `my-land` is shown from (day 2), when the talk opens, `villager.offer_stake` while the player has no stake and no plot, and `villager.offer_plot` once a plot is marked (`LandOfferAt`; a stale offer is
  taken back silently by `villager.offer_gone`). «Sí» in the first raises `villager.give_stake` (`give_item` of the stake; no second stake is given while one is held or once land is owned; the sale takes the stake from the bag with the coins, Y15); his `patience` (prompt `mayor-stake`, two misses, one after help) gives it
  after «no sé» or two missed lines. «Sí» or a request for the plot in the second raises `villager.buy_plot` (the kind `sell_plot`, `{"item": "camp-kit"}`: the price is `game/land.json`'s). A request for `terreno` or `tierra` at any state goes
  through his `villager.give` row (`instead: {"terreno": "sell_plot", "tierra": "sell_plot"}`). The sale's misses are `villager.already_owned`, `no_plot` (a stake in the bag, nothing marked or the marked plot no longer passes the land rules),
  `no_stake` (none: his answer gives one), `cant_pay` and `no_room`; the validator wants a state that takes each, the two offers in the initial state and the two offering states with a prompt that takes «sí» and «no». «No sé» at the sale
  gets help, never the land: the coins are the game's rule, not the language's. The checklist `my-land` («Mi terreno», `"session": 2`: it never ends a session, so it never holds sleep back) has the step `stake` (the stake in the bag, counted
  as the camp's wood is), `buy` (`plot_bought`, restored from `systems.land.owned` by the hook `plot-owned`), `camp` (`camp_placed`, noun `campamento`, restored by `camp-placed` from the save's camp) and `fire` (`fire_lit` with the detail `camp.fire`,
  noun `fuego`, restored by `camp-fire-lit` from the ring in the world): the checklist is done when all four are, and it still never holds sleep back. A marked plot points the arrow at the mayor, the purchase at the plot's middle.
- **The camp the kit places (Y12).** One rigid piece, the `camp` stamp of `game/scene.json` (the stamps' format is that of `tent` and `inn_rooms`: a legend and layers from the ground up, north row first), drawn facing north: a
  fire ring (the legend's `fire-ring` round an empty middle), a free row of ground, the tent's A-frame (the `tent` stamp's canvas cells, rows 4 to 7) and a bedroll two cells long on the ground in it (`bedroll`, the head at the back, then
  `bedroll-foot`). It is five wide and eight deep, so it fits the 10 by 10 plot with ground to walk round it whichever way it turns. A stamp may carry `"anchors": {"name": [column, row]}`, named cells of the first layer that turn with it; the
  camp needs `fire` (the ring's middle: the cell the player aims at and the sleep place at the fire), `bed` (the bedroll's head: the sleep place in the tent) and `wake` (the free cell, with head room, a death wakes on). I chose a stamp with
  anchors over a new data file: the scene's own build and the placing in play turn stamps with one helper (`StampSpec.Turn`/`Cells`), and a second file would have meant a second schema, loader and validator for the same grid of characters. The validator
  (`LandValidator`) checks the three anchors hold what the code expects and that the footprint, turned either way, leaves a block of ground on every side of the plot. The kit is used with the right click on the ground; the camp faces the player.
  Gathering any block of it for three quarters of a second packs the whole camp back into the kit. The hint lines are `ui.camp*` in `locale/en/ui.json`.
- **A place the beacon names.** Give the site, the point or the named prop a `label` in `game/scene.json` (`"label": "río"`); a place with no label shows
  its own name. The label is also the word an NPC's directions answer to (below), so a place the learner may ask for needs one.
- **A river and its current.** The `river.path` in `game/scene.json` is listed from **upstream to downstream** (the first waypoint at the waterfall's pool, the last in
  the lake). The player in the river is carried along its stretches by `RiverCurrents` (water-plan H08), worked out when the scene is built, from the layout, and never
  saved. Each stretch between two neighbouring points of the river's curve runs from its end with the **higher water surface** to the lower; where both ends are level
  (the usual case: the surface steps down in whole blocks), it runs in path order. So a river added to the layout flows with no code change, as long as its path is
  listed from the high end to the low end. Only the river's columns have a current: the lake and the pool at the waterfall's foot are still. The drift is about
  0.6 blocks a second; water that flows by the flow rules (a poured source, a break in a dam) pushes at 1.0 and falling water pulls down.
- **What a host event does** (`game/host-events.json`, schema 2 since village-plan V02; `take_item` and `sell` since V03). Each entry names the NPC, the
  host event its agent raises and an `action`; an event the file does not map changes nothing:
  - `give_item` (`item`, by the name the save gives it, and optionally `count`: a number, or `"qty"`, the number the learner said, `quiero dos panes`, at most
    5, so no talk makes gifts out of nothing; one when the line has none, none for `cero`). The items go in all together or not at all (a full bag: the NPC keeps
    them); the bottle is handed over once.
  - `point_at_noun` (`noun`): the beacon goes to the nearest block of a catalogue noun.
  - `point_at_place` (`place`): the beacon goes to a named place of the scene (a site, a point or a named prop: `stall`). The validator refuses a place the
    scene does not have.
  - `point_at_slot` (`slot`, `"place"`): the beacon goes to the place a slot of the learner's line names. The word is looked up among the scene's labels
    (`tienda` is the stall; of several points with one label, the nearest: `casa`); a labelled place made of its word's blocks is its nearest block joined to its point (the river,
    `río`: its water, not the well's, V04); a catalogue noun no label shows is its nearest block (`árbol`); any other word is no place. Then the NPC does not say the line of the action
    that pointed (`Mira, está allí.`) and is raised `<npc>.unknown_place` as a host turn, so its agent needs that transition (`villager.unknown_place`:
    `No sé dónde está.`; the validator checks one state takes it). Every answer logs `place_asked` with the place found (`stall`, `river`, `árbol`) or
    `unknown`.
  - `take_item` (`item`, and `count` as for `give_item`): the NPC takes the items, all of them or none; logged as `item_taken` (`madera, request`).
  - `sell` (`item`, `price` in coins): a purchase. Map it to the host event of the action that answers `request_item` at the NPC's prompt; the learner's
    number (`quiero dos panes`, the verdict's `qty`, one when none; no cap since V19, the coins and the bag are the bound) of the item for that many times the
    price. The coins go (`item_taken`) and the items come (`item_given`), or nothing changes hands: with too few coins the NPC does not say that action's line
    (`Aquí tienes el pan.`) and is raised `<npc>.cant_pay` as a host turn, which its agent answers (`Necesitas más monedas.`). Since V15 a request names its
    item: asked for another one (`quiero madera` at the bakery), or since V19 for a catalogue noun of no item (`quiero casa`), the NPC says neither line and is
    raised `<npc>.wont_sell` (`Solo vendo pan.`); a word the catalogue lacks, or a line with no item, asks for the NPC's own. Since V19 a request for none
    (`quiero cero panes`) is raised `<npc>.none_asked` (Luz asks how many with her `Aquí hay pan. ¿Cuántos panes quieres?`), and a bag with no room for the
    items once their coins are paid `<npc>.no_room` (`Tu bolsa está llena.`). The validator checks one state of the agent takes each of the four. For example:

    ```json
    { "npc": "baker", "event": "baker.sell_bread", "action": { "kind": "sell", "item": "bread", "price": 2 } }
    ```
  - `buy` (`item`, `price` in coins; village-plan V15): a sale, the purchase turned round. Map it to the host event of the action that answers `offer_item`
    (`vendo madera`, `quiero vender madera`) at the NPC's prompt. The NPC takes the item and pays the price for each one: as many as the learner said (`qty`,
    at most what the bag holds; wood as `tres bloques de madera`, and `un`/`una` before the item is one), or with no number every one the bag holds. The items go (`item_taken`, `madera, request`) and the coins come (`item_given`, `moneda, request`),
    or nothing changes hands: a bag with none of the item, a line that offers another item (or since V19 a catalogue noun of no item, `vendo piedra`), an
    offer of none (`vendo cero bloques de madera`, V19) or a bag with no room for the coins once the items are taken (V19) keeps the NPC from saying that action's
    line, and its agent is raised `<npc>.cant_sell` (`No tienes madera.`), `<npc>.wont_buy` (`No, gracias. Solo compro madera.`), `<npc>.none_offered`
    (`¿Cuántos bloques de madera me vendes?`) or `<npc>.no_room` (`Tu bolsa está llena.`) as a host turn. The validator checks that one state of the agent
    takes each. There is no cap: the goods are the cap (each coin is a piece of
    wood chopped or a fruit picked, and nobody buys what an NPC sells, so nothing can be bought and sold again at a profit). Luz and Domingo buy wood, León
    fruit, a coin each:

    ```json
    { "npc": "baker", "event": "baker.buy_wood", "action": { "kind": "buy", "item": "wood", "price": 1 } }
    ```
- **An objective.** Add `game-v1/game/objectives/<id>.json` and its id to `game-v1/game/session.json` where the HUD should show it (the order gates
  nothing: every objective counts from the start, unless it names a `session`, V03: the play session it is shown and counted from; before it the HUD skips
  it and its steps count nothing). A checklist has a `title` (target language) and steps that name a catalogue `noun`; its gloss is
  `objective.<id>.title` in `locale/en/ui.json`, and each step's label is the noun's lemma for a mass noun or a target of 1, otherwise its plural (a
  `label` overrides it). Another objective is shown by one line, `objective.<id>.hud`. Each step counts the world events its `trigger` matches, up to
  its `target`; a step whose trigger is a pickup of a noun (`item_picked_up` with a `detail`) counts the noun's items in the bag instead, so
  crafting, eating, giving or dropping them takes it back until the objective is done (#32). What the data cannot say is a named hook the code implements: `onComplete` (`guide-met`; `point-inn`: the beacon goes to the place `session.json`'s `innBeacon` names and the guide says `guide.camp.inn`, Y07) and a step's
  `restore` (`lesson-begun`; `lodging-given`: the lodging the save's sleep entry keeps, Y07; `plot-owned`, Y11; `camp-placed` and `camp-fire-lit`, Y12: the camp the sleep entry keeps and the ring of it that burns in the world), which works the step out again from a loaded world; the save keeps its count too, so a step done before the save stays done when the world no longer shows it (a camp packed up after its fire was lit). `alsoDoneBy` lists
  objectives whose progress completes this one. The validator refuses an unknown hook. A save keys a step `<objective>.<step>`, so renaming one
  drops its count from older saves. The session's ending (S17's closing moment) waits for the objectives with no `session` that are not orders.
- **An order** (village-plan V03) is an objective that names the NPC the goods are brought to (`deliverTo`) and the goods (`deliver`: items by the name
  the save gives them, each once, and a count), usually from a play `session`; it has no `title` and no `steps`, and its line is `objective.<id>.hud`:

  ```json
  { "$schema": "../../../schema/objective.schema.json", "schemaVersion": 1, "id": "oven-wood", "session": 5,
    "deliverTo": "baker", "deliver": [ { "item": "wood", "count": 5 } ] }
  ```

  An order may ask for several items: the smith's `axe-handle` (V12) takes two wood and two coins, and is delivered only when the inventory holds both.

  An order is given before it counts (issue #54, D-game-30): its NPC offers it when the talk opens and a «sí» gives it, so the NPC's agent needs an
  `offering_order` state: the initial state takes `<npc>.offer_order` into it (the offer line), it raises `<npc>.accept_order` on `confirm` (the thanks) and
  `<npc>.decline_order` on `deny`, takes `<npc>.offer_gone` back to the initial state, and repeats the offer on `ask_help` and `dont_know`; its other
  transitions are the initial state's. `game/npcs/<npc>.json` maps the state to a prompt (`<npc>-order-offer`, the offer's text, taking `confirm` and `deny`
  beside the open prompt's intents). The validator checks all of it (`CheckOrderOffer`). An order whose lesson tells it can name the step where the NPC asks
  for the goods (`"lessonStep": "n3-oven"`): reaching that step gives the order, and while the teacher would offer that class the NPC does not offer the order beside it.

  An order may name the lesson that tells it (`"lesson": "como-es"`, village-plan V13): while that lesson is under way before its `do` step that waits for the
  delivery (`order_delivered` at the order's NPC with the order's id), or is about to start at the talk (an `autoStart` lesson available at that NPC), walking up
  with the goods does not deliver them, so the plan comes first even for a learner who carries the goods already (the farmer's `water-for-plants`: the bottle
  filled at the river in session 1 is still full). Once the lesson reaches that step, ends, or does not start there, walking up is enough again; every step can be
  skipped, so the order never waits behind speech. The validator checks that the lesson has the step. Since V14 the baker's `oven-wood` and the smith's `axe-handle`
  name theirs too (`numeros-y-pan`, `gente-del-pueblo`): a learner with five wood heard Luz's thanks before saying `Voy a buscar madera`, and one with the axe's goods was
  given the axe before Domingo offered it.

  Its one step, `delivered` (saved as `oven-wood.delivered`), is made by the loader. When the panel opens on that NPC and the inventory holds the goods,
  nothing more is asked of the learner (D-game-8, D-game-9): the goods are taken (`item_taken`, `madera, order`), the NPC's agent is raised
  `<npc>.delivered` as the talk's opening (its thanks, in its own pool and voice), the host event of that line is the reward (a `give_item` in
  `host-events.json`: `baker.pay`, four coins), `order_delivered` is logged (the NPC; the detail is the order's id) and the order is done. So the agent
  needs `{ "onHostEvent": "baker.delivered", "to": ..., "actions": [{ "dialogueIntent": "baker_delivered", "hostEvent": "baker.pay", ... }] }` in **every**
  state (the validator checks), and the line and its gloss as any NPC line. Without all the goods the talk opens as usual and nothing is taken.
- **An NPC.** See "Adding an NPC" below: since schema 2 (V01) an NPC needs data only.
- **An NPC line.** Add it to `game-v1/dialogue/dialogue.json` (the core's file) and its gloss to `locale/en/lines.json` (`line.<lineId>`). The line
  speaks with the voice of the NPC whose pool it is in (`<id>_*`, or `guide.lesson.<id>.*` for a lesson line; every other pool is the guide's).

## Adding an NPC

An NPC is data alone since schema 2 of `game/npcs/<id>.json` (village-plan V01): no code names it. A fisher, `fisher`, takes these files, in this order (the
fisher Mar of the lesson `el-pueblo`, village-plan V10, was added exactly so; his files are the worked example, and the baker Luz of `numeros-y-pan`, V11, with
her purchase and her order, is the second; the smith Domingo and the farmer León of `gente-del-pueblo`, V12, are two more, met in the villager's lesson; the innkeeper Rosa, Y06, has no lesson: an offer on a yes-or-no prompt, `patience` and a host event that grants the room, like the shopkeeper's bottle):

1. **The core's agent and prompts** (`game-v1/agents/fisher.json`, `game-v1/prompts/prompts.json`): the agent's states and transitions, with an
   `onHostEvent` transition for the event it opens a talk with (`fisher.greet`) in each state that greets; one prompt per state it waits in
   (`fisher-open`, ...), each with `"agentId": "fisher"`, an answer set and the allowed intents. Its lines go in `game-v1/dialogue/dialogue.json`
   as pools named `fisher_*` (its own) and `guide.lesson.fisher.*` (its lesson lines, which the content lint counts as the game's).
2. **`game-v1/game/npcs/fisher.json`** (`npc.schema.json`, schema 2):

   ```json
   {
     "$schema": "../../../schema/npc.schema.json",
     "schemaVersion": 2,
     "id": "fisher",
     "label": "Pescador",
     "name": "Mar",
     "figure": "fisher",
     "focus": { "fisher-open": [ "donde-esta-la-panaderia", "donde-esta-la-herreria" ] },
     "prompts": { "idle": "fisher-open", "pointing": "fisher-more" },
     "opening": { "event": "fisher.greet", "when": "always" },
     "voice": { "speaker": 53, "name": "em_santa", "speed": 0.9 }
   }
   ```

   - `name` (optional): what the NPC is called (`{npc:<id>}` in the support language). A learner's line that names the NPC it is said to, at its start or its
     end (`Mar, ¿dónde está la panadería?`, `quiero pan, Luz`), is read without the name, typed or spoken, at every prompt (V14, `Vocative`): the name is no part
     of the sentence, and no grammar has room for it. A name a preposition or an article binds (`el pan de Luz`) or another joins (`Luz y Mar comieron pan`) stays.
   - `prompts`: by state of the agent, the prompt the NPC waits on there; a state not listed waits on the initial state's, which must be listed.
     The key `review` is no state: it names the prompt of the guide's review question (only the guide has one).
   - `opening`: `always` raises the event at every talk (the villager's "Hola."), `first` at the first talk only, while the agent is in its
     initial state (the shopkeeper's "¿Qué necesitas?"), `never` opens with the prompt's text (the guide; no `event`).
   - `voice`: the Kokoro speaker id and the voice's name (`docs/nlp/api.md` section 6: 28 `ef_dora`, 29 `em_alex`, 53 `em_santa`), and the speed of
     the normal recordings (the slow ones are 0.8 of it). The renderer records every line of the NPC with it. The other voices of the model that read
     Spanish, and the village's provisional choices, were compared in the voice trial (village-plan V06), deleted on 2026-11-03; its clips and
     `trial.json`, with the speaker ids, are in git history (`git log -- docs/game/voice-trial`). An optional
     `gain` (default 1, at most 4; V11) scales the voice's samples, for a voice the model renders louder or quieter than the Spanish ones: the baker's
     `if_sara` is about 10 dB louder, so it takes 0.32. The speech index records a gain that is not 1, and changing it renders that NPC's lines again.
     So does any change of the voice: `scripts/limited.sh scripts/prerender-speech.sh` renders only that NPC's files (no `--force`), and their rows
     of `docs/game/speech-checklist.md` lose the owner's marks. The fisher's, the baker's, the smith's and the farmer's voices are provisional until
     the owner has heard them (D-game-22 (d); the checklist's preface lists them).
   - `keyWords` (optional): catalogue nouns whose cards the NPC shows alone before its first question (the shopkeeper's `agua` and `pan`). The save
     keeps one flag, so the key words are taught once a game, at the first NPC that has them.
   - `poolPrefix` (optional): the name its pools carry when it is not the id (the shopkeeper's pools are `shop_*` and `guide.lesson.shop.*`).
   - `focus` (needed where a prompt it waits on is the home of rows): the learner phrases each such prompt practises (their home, with a scaffold to
     fade), from the rows of `game/phrases.json` whose `prompt` it is (`fisher-open`: his two directions; the baker's `baker-open`: `quiero-dos-panes`,
     `cuanto-cuesta`, `hay-pan`). Outside a lesson the panel shows their cards, and a turn of one of their intents is credited to that phrase; of two with
     one intent (the fisher's), the one that names the place asked; and of one that names another place than the turn asks about (the villager's `¿Dónde está el
     río?` for `¿dónde está la panadería?`, V14), the row another NPC practises that names it (a transfer), else none. A prompt that is the home of no row may practise another NPC's (the farmer's
     `farmer-open` takes the smith's `como-te-llamas` and `que-haces`; a phrase at home with two NPCs is no transfer at either). Its lesson prompts need
     none: each step names its own phrase. Without a focus a turn is still credited to the prompt's own row (village-plan V12b; before it the baker's
     `quiero dos panes` was the stall's `pedir-agua`), but no card shows; the validator refuses that (below).
3. **`game-v1/game/npcs.json`**: its id **at the end** (the saves keep the NPCs in this order).
4. **The scene placement**: `game/scene.json` `npcs` (where it stands and faces).
5. **The locale role**: `npc.fisher.role` in `locale/en/ui.json`, and each new line's gloss in `locale/en/lines.json`.
6. **The figure**: the figure key is drawn by `Graphics/Figures/NpcFigures.cs` (its colours, geometry and face cell); the keys drawn are `guide`,
   `shopkeeper`, `villager` and, since village-plan V05, `fisher`, `baker`, `smith` and `farmer`, and, since Y06, `innkeeper`. A key the game does not draw yet gets a plain grey
   figure until a milestone adds it.
7. **What its host events do** in the world (`game/host-events.json`; an event not listed changes nothing), a provenance item for each new file,
   the recordings (`scripts/prerender-speech.sh`, which reads the voice from the NPC file), and the refresh.

`--validate-content` (and so the refresh and the tests) refuses an NPC with no agent or no prompts, a prompt the bundle lacks or that is another
agent's, a state its agent lacks, no prompt for the initial state, an opening event no state of its agent takes, no voice or a voice with no name, a
key word that is no catalogue noun, a pool prefix another NPC has, and a prompt it waits on that is the home of rows the learner practises with a
focus there that names none of them (V12b). `--talk fisher` then walks up to it in a scripted run. Behaviour the data
cannot say stays code, by id: the shopkeeper's fail-forward (the bottle after four misses, and on a later visit) and the guide's review, transfer
offer and world lines (`docs/game/content-plan.md` section 3.6).

## Temperature (I06)

`game-v1/game/temperature.json` holds the numbers of the temperature where the player stands (`temperature.schema.json`; `GameDataValidator` and `TemperatureValidator`
check the values). Every temperature is a whole number of tenths of a degree Celsius (`125` is 12.5). The place's temperature is the sum of five terms: `base` (the
valley's mean), `day` (plus the amplitude in full daylight, minus it in the dark, from `day.json`'s daylight curve), `weather` (the cooling at the strongest cloud
cover, precipitation and wind: each channel of the sky times its coefficient, so a new weather kind needs no row here), `shelter` (the bonus under a roof, in proportion
to the covered columns; `onlyInPrecipitation` limits it to when something falls) and `fire` (the bonus within `radiusBlocks` of a lit fire ring). The player's warmth is
added to give the feeling, and the feeling falls in the first band, warmest first, whose lower bound it reaches (the coldest band takes everything below). A band has a
`walkFactor` (above 0, at most 1: what it multiplies the walking speed by), a `chill` (0 for warm and cool; above 0 shows the cold icon with that many notches) and, on a chilled
band, the `line` it says once on the feeling's first entry into the cold: a key of `words.json`'s `hudLines` (`temperature.cold`, «Tengo frío.») with its English in the
locale under the same id. Bounds must fall, chills must not, ids are unique. The loop is the usual one (`scripts/content-loop.sh --grammar`). A new term (the biomes'
climate) is code with its own data, not a row here.

## Equipment (I03a)

`game-v1/game/equipment.json` (`equipment.schema.json`; `EquipmentData`, checked by `EquipmentValidator` inside `GameDataValidator`) lists the slot groups gear is worn in, in this order: `clothing`,
`armour`, `cosmetic` (each with the body slots `head`, `body`, `legs`, `feet`), `accessories` (3), `offhand` (1) and `backpack` (1). A group is a row and code never names one: it has `slots` (named; an
item's `wear` must equal the slot's name) or `count` with a `wear` kind; `accepts`, the item tags the group takes; `noDuplicates` (one of a kind in the group, the accessories); `statsApply` (false for
the cosmetic layer, whose items give nothing); `layer` (where the avatar draws it, left out when it is not a layer); and the Spanish noun of each slot (`labels`, or `label` for counted slots; left out
when the lexicon has no word yet, as for the accessories). The order of the groups and slots is part of the bundle: commands and the digest carry indexes, so a new group goes at the end. A new group,
say for a ring finger or a cape, is one more row (and an item tag it accepts); no code changes.

An item becomes gear with three optional fields of `items.json`: `wear` (the kind of slot: `head`, `body`, `legs`, `feet`, `accessory`, `offhand` or `back`, a `wear` of some slot), `stats` (open
camelCase keys to numbers, combined into the player's stat sheet while worn; the keys and their units are in "Stat keys and damage" below; `light` is read from I05b on) and `look` (a `#rgb` or `#rrggbb` colour for each body part the item covers; I09 draws it, nothing reads it yet). The validator requires that a worn item has a stack limit of 1,
that some group with a slot of its `wear` accepts one of its tags (so give it `clothing`, `armour`, `accessory`, `offhand` or `bag`), that stat keys are well formed and look colours parse, and that an
item with stats or a look can be worn. A `look` only dresses the avatar; the stats count only in a group whose `statsApply` is true. Run the usual loop after an edit.

**The bag (I03b).** `equipment.json`'s `bagSlots` (30) is the inventory with no bag worn: the hotbar's 10 and 20 more. A bag is an item with a fourth optional field, `capacity` (1 to 100): the slots it adds while
worn in the `back` slot (the plan's small bag adds 10 and the large one 20, so at most 50). Only an item tagged `bag` and worn on the `back` may have one; the catalogue refuses any other, and the inventory is sized to
`bagSlots` plus the largest capacity of `items.json`, so a new bag is one row and no code. A bag cannot come off, or be swapped for a smaller one, while a slot it would drop holds an item (the player empties those slots
first); swapping for one at least as large is always allowed. A save records the worn bag by name and the bag at its active size, so changing a bag's `capacity` in the data between a save and a load makes the save
load only across bundles (the migration resizes the bag and reports what was dropped).

## Stat keys and damage (I05a)

An item's `stats` (items.json) are open keys; the ones the code reads, each with its unit, how items combine and the range one item may give (`StatRules`; `EquipmentValidator` refuses a value outside it):

| Key | Unit | Items | Range per item | Read by |
| --- | --- | --- | --- | --- |
| `warmth` | tenths of a degree | sum | -2000 to 2000 | the temperature (I06) |
| `walkSpeed` | factor on walking, 1 changes nothing | multiply | 0.5 to 1.5 | the tick's `WalkPermille`, beside the needs', the cold's and the sprint's; the product is clamped to 10% to 250% |
| `hungerDrain`, `thirstDrain` | factor on how fast that need falls, 1 changes nothing | multiply | 0.25 to 2 | `PlayerNeeds`, on top of the needs rate and relaxed mode |
| `maxStamina` | stamina points (the meter is 0 to 100) | sum | 0 to 100 | `PlayerNeeds`: added to the ceiling where the drives' penalties are taken off it |
| `armour` | a whole-number rating | sum | 0 to 100 | the damage pipeline's armour modifier |

A factor key multiplies so that two items never add up past what one could (two 1.1 boots are 1.21), and a sum key adds. Walk speed applies with `--needs off` too (it is gear, not a need; nothing worn leaves 1).

`game-v1/game/damage.json` (`damage.schema.json`; `DamageData`, checked by `DamageValidator`) lists each cause the code raises (`drowning`) and, per cause, the ids of the damage modifiers that change its damage, in order.
`modifiers: []` takes the damage as it is: armour applies to no cause today. **To make armour soften a cause**, put `"armour"` in that cause's `modifiers`; armour takes 4% per point off, at most 80%, and a hit
is never reduced below 1. A new cause needs a constant where the code raises it (`DamageCause`) and a row; a new modifier is a class registered in `DamageModifiers.Standard`. Run the usual loop after an edit.

## Adding a weather kind

A weather kind is a row of `game-v1/game/weather.json` (E04, D-game-31). The sky, the light, the fog, the rain and the sound read the blended channels
and never a kind's id, so a kind made of the channels the game already has needs no code. The example below is an invented fog, written here only:
it is not in the bundle.

1. **The row** in `game/weather.json` (`weather.schema.json`; `GameDataValidator` and `WeatherValidator` check the values):

   ```json
   {
     "id": "fog",
     "cloudCover": 0.6,
     "sunlight": 0.5,
     "fog": 0.9,
     "wind": 0.1,
     "minSeconds": 150,
     "maxSeconds": 360,
     "next": { "cloudy": 2, "sunny": 1 },
     "morningWeight": 0,
     "say": { "day": "fog", "night": "fog" }
   }
   ```

   - `id`: lowercase kebab-case, unique in the file.
   - `cloudCover`, `sunlight`, `fog`, `wind`: each from 0 to 1. They are the channels every consumer reads: the clouds and the dome, the sun's light
     on the world, the horizon's fog, the wind's sound and the slant of the rain.
   - `precipitation`: `{ "kind": "rain", "intensity": 0.8 }` when something falls, and absent otherwise. The intensity is above 0 exactly when the
     kind is set, and the kind must have a style row in the game (step 4). The fog has none.
   - `minSeconds`, `maxSeconds`: the range a spell of this kind lasts, in seconds; `minSeconds` is above 0 and not above `maxSeconds`. The file's
     `transitionSeconds` (how long a change blends) must not exceed the shortest spell of any kind.
   - `next`: the kinds that may follow this one, each with a positive whole-number weight; the next kind is drawn in proportion to the weights. Every
     name must be a kind of the file, and every kind needs a `next` unless it is the only kind.
   - `morningWeight`: the weight of this kind in the draw made when the player wakes (a sleep or a collapse); 0 means never. At least one kind needs a
     weight above 0, and none may be negative.
   - `say`: `{ "day": <key>, "night": <key> }`, the dialogue key an NPC that answers the weather uses in the day phases and at night (dusk and
     dawn count as day). Optional for the file, but every NPC that answers the weather needs it (step 2). A key may repeat (`rain` says `rain` at both
     hours); the sunny kind says `sun` by day and `fair` at night.

2. **The three things each NPC with `weatherAnswers` needs for every key.** The seven NPCs that answer «¿Qué tiempo hace?» are the farmer, the
   fisher, the baker, the shopkeeper, the smith, the villager and the guide. For each of them, for the key `fog` (`smith` as the example):
   - **a dialogue pool** `smith_fog` in `game-v1/dialogue/dialogue.json`, in the NPC's Spanish:

     ```json
     "smith_fog": { "poolId": "smith_fog", "variants": [ { "lineId": "smith_fog.1", "text": "Hoy hay niebla." } ] }
     ```

   - **an agent transition** in `game-v1/agents/smith.json`, on the host event `smith.fog` (the NPC's id, a dot, the key), in each state that already
     takes `smith.sun`, `smith.cloudy`, `smith.rain` or `smith.fair`:

     ```json
     { "onHostEvent": "smith.fog", "to": "idle", "actions": [{ "dialogueIntent": "smith_fog", "variantPoolId": "smith_fog" }] }
     ```

   - **the English line** in `content/locale/en/lines.json`, under the line's id with the prefix `line.`: `"line.smith_fog.1": "It is foggy today."`.
     The locale check requires it for every dialogue line.

   Nothing is listed in `game/host-events.json` for a weather kind; the transition is the whole of it.

3. **The speech and the grammar** (`docs/game/content.md`). Run `scripts/content-loop.sh --grammar --speech` directly, never under
   `scripts/limited.sh`. `--grammar` starts LanguageTool and checks the new Spanish lines; `--speech` renders their clips (the voice comes from the
   NPC's file) and refreshes the manifest, the strings and the speech index. The loop also rewrites the bundle's hash, so the save fixtures are updated
   by it, and it runs the whole chain (the validation, the goldens, the fast tier). Commit what it changed, as the loop's step 4 says.

4. **Only for a new precipitation** (not for fog). The style table `src/Vocaterra.Game/Graphics/Weather/PrecipitationStyles.cs` needs one row whose
   `Id` is the precipitation's kind (length, width, fall speed, colour, opacity, slant and share of the drops). This is code. Apart from the rain's bed
   name, this table is the only place in the source where a precipitation's id is written (`NoWeatherKindIdsInSourceTests`). The rain's sound bed already follows the intensity of whatever falls, so a new precipitation is heard
   as rain until it has a bed of its own: a name and a weight in `src/Vocaterra.Game.Core/Sound/AmbientBeds.cs`, which is also code.

5. **What the checks say when a step is missed.** `scripts/content.sh refresh` (and the loop) runs `--validate-content` and lists every problem at once:
   - a value out of range, a `next` name that is no kind, a missing `next`, a zero `morningWeight` for every kind: a `game/weather.json:` line in the
     validator's words (for example `'fog' has no next kind.`);
   - a kind with no `say` for an NPC that answers the weather: `game/weather.json: the kind 'fog' has no 'say', and game/npcs/smith.json answers the
     weather (...)`;
   - a key with no transition or no pool: `game/npcs/smith.json: the weather kind 'fog' says 'fog', but no transition of agents/smith.json on the
     host event 'smith.fog' names a dialogue pool for the NPC's answer.`;
   - a missing English line: `locale en: the string 'line.smith_fog.1' is missing.`;
   - new Spanish that the grammar cache lacks: the message names the LanguageTool steps (step 3);
   - a precipitation with no style row: the core's and the game's tests fail, `PrecipitationStylesTests` (`EveryPrecipitationKindHasAStyleRow`) among them,
     since the tests read the shipped `weather.json`;
   - a kind's id typed into a source file under `src/`: `NoWeatherKindIdsInSourceTests` names the file and the line.
   The core's tests run on `content/game-v1` too (R02), so a `weather.json` error that the validator misses shows as a failing test.

## Adding a biome

A biome is a row of `game-v1/game/biomes.json` (B01, D-game-34; `biomes.schema.json`; `BiomeValidator` checks the values, `GameDataValidator` runs it). The rows are tried in file order and the first whose `when` holds wins,
so the last must have none (it catches every column); no file under `src/` names a biome's id (`NoBiomeIdsInSourceTests`), so a new biome is one row and no code. The cloud layer's height is `cloudAltitude` in `game/weather.json` (B14), not here.

1. **The row.** An example, `birchwood`, as it stands in the file:

   ```json
   {
     "id": "birchwood",
     "when": { "humidity": { "min": 150 }, "temperature": { "max": 100 } },
     "ambient": { "birds": 1000, "insects": 700 },
     "top": "grass", "filler": "dirt", "fillerDepth": 3,
     "trees": { "density": 50, "species": [{ "species": "birch", "weight": 5 }, { "species": "oak", "weight": 1 }] },
     "decor": { "density": 150, "blocks": { "fern": 2, "tall-grass": 2 } }
   }
   ```

   - `id`: lowercase kebab-case, unique. `when`: what the row asks of a column; every part is optional and a row matches when all its parts do. The parts are `water` (the ground is below the sea), and ranges (`min`, `max`, either optional, in thousandths of the field's range; heights in blocks) of `continentalness`, `erosion`, `ridges`, `temperature`, `humidity`, `river` (the distance from a river's centre line, 0 on it), `height` and `surfaceTemperature` (the temperature at the surface height, so it falls with altitude: snow and the tree line follow it). The dither in the file's head jitters the temperature and the humidity before the rows are tried, so borders are ragged. Put the row where its conditions belong in the order: water and coast first, then the mountains from the highest and coldest down, then the lowlands' temperature and humidity lookup; a row placed above one that holds for the same columns hides it.
   - `top`, `filler`, `fillerDepth` (0 to 16) and `underwater`: block names of the game's own table (`BlockDefinition`; the ground must be solid). `trees`: a `density` (percent of a chunk's 4 x 4 cells) and species by weight, each optionally with a `crown`; the species and crowns are code strategies (`StrategyRegistry`), so a species the code does not have is a code change. `decor`: plants by weight at a density in thousandths of the columns; `snowCap` lets snow cover the top where the surface temperature is under the world's snow threshold. `surfaceStrategy` and `decorStrategy` name code strategies that shape the biome's own ground or dress it (never a biome id); `baseTemperature` is data for the temperature system to come.
   - **`ambient` (B14): the sound.** The beds heard there, each a weight in thousandths (0 to 1 000) at the heart of the biome: `wind`, `birds` (by day, quieter in rain), `river`, `insects` (by night) and `surf`; no other bed may be named (the cave bed follows the depth under the ground; the waterfall, village and fire follow the scene's places; the rain follows the weather). The sound blends five columns round the listener (`PlaceAmbience`), so a border is a slope of about twelve blocks and the mixer then cross-fades over 1.5 seconds. A row with no `ambient` is silent of place beds, which `PlaceAmbienceTests` forbids for the shipped rows. A new bed needs code (`AmbientBeds`, `AmbientLoops`) and an entry in the schema's `propertyNames`.
2. **No other file.** The ground, the trees, the decor, the far view's colours (`land-colours.json` follows the climate, not the biome), the sound and the weather's cooling read the row; nothing else names it. Words for the guide (a place's name in speech) are not in the row: there is no Spanish here.
3. **What the checks say.** `scripts/content.sh refresh` (the host's `--validate-content`) lists a problem as a `game/biomes.json:` line: an id that is not kebab-case or is used twice, a range out of its field's bounds or inverted, a block the game does not have or cannot stand on, an unknown species, crown, strategy or ambient bed, a weight out of range, and a last row that has conditions. The tests: `BiomeDataTests` (the parse and each validator rule, and that the schema's beds are the ones a biome may name), `BiomeTableTests` (the order and the first match), `NoBiomeIdsInSourceTests`, `PlaceAmbienceTests` (the bed a place in each biome hears, for every row, and a column of each biome of three seeds' real worlds). **A biome changes the world**: a new row moves the 18 procedural goldens (`ProceduralGoldenTests`) and the state digest of `tests/Vocaterra.Game.Core.Tests/Fixtures/save-v10.json`, since `game/biomes.json` is hashed into it; re-record them knowingly (the owner reviews the pictures), and re-hash the bundle (`scripts/content.sh refresh`).

## Adding an ore

An ore is a row of `ores` in `game-v1/game/underground.json` (B11, D-game-34; `underground.schema.json`; `UndergroundValidator` checks the values). The row says where and how much; the ore itself is a block of the game's table. No file under `src/` names an ore's id or block (`NoOreIdsInSourceTests`).

1. **The block.** A vein of an ore the game already has (`copper-ore`, `iron-ore`, `tin-ore`; `BlockDefinition`) is one row and nothing else. A new kind of ore is a new block first, which is code: a `BlockId`, its row in `BlockDefinition` (a solid `Material.Stone` block, with the gather ticks and drop a mined ore may have), its tile in the atlas (`BlockAtlasLayout`, `BlockArtNature`), its sound surface (`Footsteps`, `SoundEventMapper`) and its Spanish noun in `game/nouns.json` and its English in `locale/`. Assets are CC0 or code-drawn (D-game-5).
2. **The row.** An example, `iron-veins`, as it stands in the file:

   ```json
   {
     "id": "iron-veins",
     "block": "iron-ore",
     "replaces": ["stone", "slate", "granite", "limestone", "basalt"],
     "size": { "min": 3, "max": 8 },
     "bands": [
       { "minY": -24, "maxY": 56, "peakY": 16, "veins": 14 },
       { "minY": 80, "maxY": 320, "peakY": 160, "veins": 8 }
     ]
   }
   ```

   - `id`: kebab-case, unique. `block`: a block name. `replaces`: the rocks a vein may take (never air, water, lava, bedrock or another ore: the validator refuses an ore or the floor here).
   - `size`: the fewest and most blocks a vein tries to hold, `1 <= min <= max <= 64`; a vein is a compact blob kept inside its chunk.
   - `bands`: each has `minY` below `maxY` inside the world, `peakY` between them, and `veins` from 0 to 64 tried in each 16 x 16 chunk column over all its layers; the number of veins at a height falls off in a triangle from the peak, so a band is a hump, and an ore may have several (iron has a deep and a high one).
3. **What the checks say.** `UndergroundValidator` names the ore row and the problem (`the ore 'iron-veins' has no band`, `a band of the ore ... needs minY below maxY inside the world and its peakY between them`, `the ore ... may not replace 'bedrock'`, an unknown block). The tests: `OreTableTests` (each ore inside its rows' ranges with the shape of its distribution, veins inside their chunk and band, never replacing air, water, lava, bedrock or an ore), `UndergroundDataTests` (the parse and the validator), `NoOreIdsInSourceTests`, and `ValleyUndergroundTests` for the valley's reach. The ores change blocks under the ground only: the 18 procedural goldens and the state digest move when the ore rows do (the digest hashes `game/underground.json`); the valley's own digest moves with the valley's ores. Re-record the goldens, re-hash the bundle (`scripts/content.sh refresh`) and put the new bundle hash into `save-v10.json`.

## Stations and items that share a noun (N07)

A recipe's `station` is a catalogue noun that names a block (`mesa` the table, `fuego` the fire-ring stones; the fire counts only while its ring burns). When the output noun names several items
(`manzana`: the apple and the roast apple), the recipe's `makes` gives the item's key (`roast-apple`), and the noun's `labels` gives the text that labels it (`manzana asada`). A new item key joins the enums
of the schemas that list items (`nouns`, `host-events`, `objective`) and, if it is food, `foods` in `game/needs.json`.

## Editing the underground (B11, B11b)

`game-v1/game/underground.json` (`underground.schema.json`; `UndergroundValidator` checks the values) holds the numbers of the rock layers, the bedrock floor, the ores, the caves, their entrances and the lava. The generator is code (the passes in `World/Generation/Procedural`, the types in `World/Underground`); the file says how much and where, and names blocks by their `BlockDefinition` names. Lengths are blocks, noise values thousandths of their range (-1 000 to 1 000, a 3D gradient noise rarely passes 800). The sea is at y 32, the world runs from -64 to 320 and the valley's own chunks start at y 0. After any edit: `scripts/content.sh refresh` (or `hash` and `validate`), then the core's and the game's tests, and re-record the procedural goldens if the edit meant to move the underground (`ProceduralGoldenTests` prints the new digests); the world's state digest hashes this file, so a save made before the edit is refused.

- **A rock** (`strata`): `stone` runs from the surface layers down to `ditherToY`, `deep` (slate) below `ditherFromY`, and between them the deep rock's share falls in a straight line, cell by cell, so the boundary is ragged. A pocket row (`blobs`) is a patch of another rock: `cell` and `cellY` are the noise's size across and up (bigger is a bigger patch), `octaves` its detail, `threshold` where it starts (higher is rarer and smaller: 330 gives about 8 in 100 blocks, 240 about 15), `in` the base rocks it may replace (the layer's stone when absent: a row with `"in": ["slate"]` lies in the deep rock) and `minY` and `maxY` the band of heights it lies in (the whole world when absent). The first row that passes wins, so the order is a priority. The shipped rows (B11b, D-game-34 item 27) break the grey up: gravel, sand and dirt pockets in the upper stone (each in its own band: gravel from y -12 to 210, sand from -4 to 110, dirt from 8 to 170), larger and more frequent granite and limestone, and granite and a darker stone, basalt, among the slate. A new rock is a row and a block (a block is code, drawn in `BlockArtNature`); no source file names one.
- **The floor** (`floor`): `bedrock` from `bottomY` (the world's lowest row) up, `minThickness` to `maxThickness` blocks, ragged column by column.
- **An ore** (a row of `ores`): `block`, `replaces` (the rocks a vein may take; never air, water, lava, bedrock or another ore, and the validator refuses an ore or bedrock here), `size` (the fewest and most blocks a vein tries to hold; it is a compact blob kept inside its chunk) and `bands`. A band is `minY`, `maxY`, `peakY` and `veins` (tried in every 16 x 16 chunk column over all its layers: the height of each vein's centre is drawn from a triangular spread over the band that peaks at `peakY`, so a chunk at the peak gets most of them, and a band that must hold no ore at a height ends there). Iron has two bands. The row's `id` (kebab-case) is the key of its random draws: renaming it moves the ore.
- **The caves** (`caves`): the caverns (`cheese`) and tunnels (`spaghetti`) are noises sampled every `latticeXz` x `latticeY` x `latticeXz` blocks and interpolated (4 x 8 x 4 is what keeps a chunk cheap; the numbers must divide 16). `cheese`: a cavern where its noise is above `deepThreshold` at and under `deepY`, rising in a line to `shallowThreshold` at and over `shallowY` (1 000 means none), so the large chambers lie deep. `spaghetti`: a tunnel where two noises are both within `thickness` of zero (a larger `thickness` is wider tunnels and a denser net; 95 gives tunnels about 5 blocks across). `topY` is the highest a cave reaches, `surfaceCover` the rock kept between a cave's top and the ground, `underRegionCeiling` the highest a cave reaches under the valley (always under 0, where its own chunks start), `waterMargin` the blocks round any water cell that stay solid and `bedClearance` the blocks under a water column's bed that stay solid (not less than the margin): **do not lower these to make more caves near water, the tests for water that pours in fail**.
- **Entrances** (`entrances`, B11b): natural passages a body walks down from the surface into the caves. The land is cut into sites `cell` blocks across; `chance` thousandths of them try to open one: the site tries `attempts` spots at least `margin` blocks inside it and keeps the passage the ground covers most (a hillside beats level ground). A passage is a winding **path** of cells, each next to the one before and never more than a block lower: it bends (`bend` is the sharpest turn a block, in sixteenths of a step of a 64-step turn; it changes its mind every `bendSegmentMin` to `bendSegmentMax` blocks and never strays more than `maxTurn` steps from its first heading), and its floor drops in stretches of `segmentMin` to `segmentMax` blocks, each of its own slope (one block down every 1 to `runMax` blocks, or level, `levelShare` thousandths of the time); the first `plunge` blocks fall at nearly every block, so the opening is a hole and not a long trench. Round the path a **tube** is carved: it is `widthMin` to `widthMax` blocks across and `headroomMin` to `headroomMax` high, both drifting along the path, wider by `funnel` thousandths for the first `funnelLength` blocks (the mouth), and its walls and roof are moved by a noise of `roughness` thousandths of a block (cell `roughnessCell`), so they bulge and are never flat planes; `ledges` thousandths of the side columns stand a block higher than the floor. Where the ground lies at most `bowlDepth` blocks over the floor the mouth may be cut in a bowl rising one block for each `bowlSlope` thousandths of a block outwards (0 turns it off; the shipped data does). The floor in the meadow's own layers is `floorBlocks` (loose gravel, stone, mossy stone), a gap in a floor is filled with them. The path runs on until it meets a cave at least `minDrop` blocks under the mouth (within `maxLength` blocks), then a landing. A site opens only on dry procedural land at least `minHeightAboveSea` over the sea, with no water, river carve or lake within `waterReach` columns of the passage (more than the caves' `waterMargin`, so it never floods), on plain ground, and never in a region or its margin; a passage stays inside its own site, so two never meet. No tree or plant stands at the mouth, and the damp zone of the linings gives it moss. The passage is planned from the height model and the cave field, never from a chunk's window, so it is the same whichever chunk makes it.
- **Linings** (`linings`, B11b): rules laid over the rock a cave exposes, in order, the first that applies winning. A rule says the `block` the rock becomes, the rocks it replaces (`in`), the `face` it shows (`floor`: air above, `wall`, `ceiling` or `any`), the `zone` (`any`; `damp`: within `dampReach` blocks of an entrance's passage; `wet`: a water column within `wetReach` columns), the share `chance` of the qualifying cells, an optional `patch` (a noise that must pass a threshold, so the block lies in clumps) and the band `minY` to `maxY`. The shipped rules lay mossy stone near the entrances and near water, sand and gravel where a cave lies near water, and gravel, loose stone and dirt on floors. Exposure is read inside a chunk, so a block on a chunk's edge is judged by its inner faces.
- **Surface rock** (`surfaceRock`, B11b): bare stone on the surface. `cliff`: a column that stands `drop` blocks over its lowest neighbour (water counts as its surface) shows banded rock on its exposed layers: `bands` (the rocks, by weight) in bands `bandThickness` blocks thick, tilted by `tilt` blocks across the face, drawn apart per zone of `zoneCell` blocks; the top of such a column is rock too. `scree`: gravel (the `rules`, each with a `chance` and a `patch`) on columns that stand `steepDrop` blocks over a neighbour, or lie within `footReach` columns of a wall of the cliff's drop (a cliff's foot). `streambed`: the same for columns a river carved by at least `minWeight` thousandths, in the bed and on the bank. Snow is never covered, and a column that is none of these keeps the biome's own layers. The valley's own surface is its template's and does not change.
- **The valley** (`valley`, B13): how far the strata, ores and caves reach into the valley's own stone. A block of the valley changes only where plain stone lies at least `surfaceDistance` blocks from any air cell and `waterDistance` from any water cell (distances are cubes), `borderMargin` columns in from the valley's edge, and `siteMargin`, `pathMargin`, `propMargin`, `villageMargin` (round the footprint of the site `villageSite`) and `passMargin` (round the paths `passPaths` and the props `passProps`) blocks from the surface features they name; so nothing the player sees, walks on or edits changes, and no entrance opens in the valley. The validator keeps the distances in range and checks the named site, paths and props are the scene's. `caves.underRegionCeiling` is -1, so the caves under the valley run up to its floor and meet its own. A change to the valley section, or to anything the valley's underground reads, changes the valley's blocks: re-hash, re-run the suite, and the valley's golden (`SceneTests.GoldenDigest`), the template digest and `save-v10.json` move.
- **Lava** (`lava`): cave air at or under `level` is lava (above the floor, under the strata's dither).
- **Checks**: `NoOreIdsInSourceTests` fails when a source file names an ore, a deep rock or a pocket's rock; the validator names a block the game does not have, a band that peaks outside its range, a passage too narrow to walk or too steep to step, a lining of an unknown face or zone, and a lava level that the floor or the dither would swallow. `RockVarietyTests` measures the walls (plain stone at most 68 in 100 of the exposed faces above y 0, four kinds of rock, no huge blob) and `EntrancePassageTests` walks every passage of a sample from its mouth to a cave 20 blocks down and checks that the paths wind, the slopes, widths and heights vary, and every cross-section is walkable.

## Editing the rivers and lakes (B02c)

`game-v1/game/world.json` (`world.schema.json`; `WorldGenValidator` checks the values) holds the numbers of the procedural world; its rivers and lakes are the **drainage of the land**, not a noise field, and their numbers are in `height.drainage`, `height.rivers`, `height.water` and `height.lakes`. Lengths are blocks, field values thousandths. After any edit: `scripts/content.sh refresh` (or `hash` and `validate`), then the core's tests, and re-pin the 18 procedural goldens and `save-v10.json`'s bundle hash and state digest (the world's state digest hashes `world.json`, so a save made before the edit is refused); bump `ProceduralGenerator.Version` when the same seed and data make different chunks.

- **How it works.** `drainage.spacing` blocks apart there are nodes. Each has the level its water would stand at (the smooth ground less the hills' amplitude, `height.water.clearance` and the valley's depth, in steps of `height.water.step`), and the water is routed over that surface moved by a smooth relief noise (`relief` blocks at most, `reliefCell` wide) and dented where the lake field is high (`lakeBasin` blocks). A priority flood from the sea fills every basin to the level of its outlet (a lake) and gives every other node a neighbour at its level or lower to run to, so **the water along a river never rises**. A landmass (the land nodes connected to the one asked for, up to the sea round them) is read whole the first time any of its nodes is asked for (about a second for a large continent), so the water of a basin that mountains enclose runs out over its true pass and nothing depends on a border; `maxNodes` cuts a world with no sea round its land.
- **Rivers.** A river begins where more than `sourceArea` nodes drain through a node (smaller: more, shorter streams; larger: fewer, longer rivers) and grows with the ground that drains into it: its carve fades out over `rivers.width` blocks at its source and `drainage.widthMax` at `fullArea` nodes (the water is about half as wide), and the water is `rivers.depth` deep at the source and `drainage.depthMax` at full size. `jitter` moves the nodes off the grid and `bow` bends each reach sideways by that share of its length (thousandths). `rivers.fadeFromHeight` to `fadeToHeight`, `seaFadeFrom` to `seaFadeTo` and `peakFadeFrom` to `peakFadeTo` fade the carve out on high ground, towards the sea and where the ranges' peaks add height (the river is gone before a crest); `rivers.valley` deepens the water under the ground and widens the river from the height `from` up. Near a region (the valley) no node within the blend margin and `drainage.regionClearance` blocks beyond it holds water, and the headwaters thin out over `regionBlend.headwater.distance` (D-game-34 item 18).
- **Lakes.** The lake field (`height.lakes`: `cell`, `octaves`, `threshold`, the calm, height and inland ramps) dents the land low, calm and inland by up to `drainage.lakeBasin` blocks; a dent the slope does not fill is a basin, which floods to its outlet and drains on from there. A lake is flat at the level of its outlet, `height.lakes.depth` blocks deep at its middle, and at least nine nodes across its flooded area (a smaller flood is left to the river that runs through it, so there are no puddles); its shore is where the blended depth of the four nodes round a column passes zero, not on the grid. More dent or a lower `threshold` is more and larger lakes (the owner's measure: 1.5 to 3.5% of the columns of a 16 384-block square).
- **Checks.** `scripts/limited.sh dotnet run --project tools/Vocaterra.WorldPreview -- stats --seed 42 --size 16384 --scale 8` prints the `network:` line (reaches, sources, rises along a reach and a chain, the share of the river whose chain ends in the sea or a lake, the share of the land within 500 blocks of a river) and the river humps; `drainage` draws the network over the heights (`--basin-x --basin-z` picks one river's basin) and `river` the section of one river from its source to the sea. `DrainageNetworkTests` holds the rules (no reach rises, every river ends in the sea or a lake, a river that crosses a border continues, lakes are flat, the same network whichever column asks first).
