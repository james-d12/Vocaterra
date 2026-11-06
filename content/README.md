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
  | `game/objectives/<id>.json` | `objective.schema.json` (one objective each, C07: its steps and their triggers, its hooks; V03: the play session it starts in, and the orders) | `ObjectiveBook` |
  | `game/npcs.json` | `npcs.schema.json` (the NPCs' ids, in order; the saves keep this order) | `NpcRoster` |
  | `game/npcs/<id>.json` | `npc.schema.json` (one NPC each, C08: its label, name, figure and the phrases its prompts practise; schema 2, V01: the prompt of each state, the opening, the voice, the key words, the pool prefix) | `NpcRoster` |
  | `game/host-events.json` | `host-events.schema.json` (what each NPC host event does: give items, point the beacon at a noun, a place or the place the learner names; schema 2, V02) | `HostEventTable` |
  | `game/needs.json` | `needs.schema.json` (the player's needs, N01: the three drives' rates, effort factors and four stages with their effects, health's recovery and warnings, stamina's costs and refill, rest at a fire, a night and a collapse, the foods, the death rules; the stage lines' keys, not their text) | `NeedsData` |
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
   4. writes the bundle's new `bundleHash` into every file under `tests/` that holds the old one (the save fixtures `save-v1.json` and
      `save-v4.json`); an edit of `locale/` alone moves none;
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

## Adding things

These steps are by hand. An authoring CLI that does them (`new npc|objective|lesson`, `refs <id>` to find an id's uses, and `check`) is
specified for later in `docs/game/content-plan.md` section 3.8 (C10); it is not built.

- **A trigger.** A tip, a lesson's `do` step and an objective's step wait for a trigger: `{ "event": "<world event>", "npc": "<agent>", "detail":
  "<exact detail>" }` (only `event` is required; a tip may also wait for `start` or `aimed_npc`). The events are `WorldEvents` (`Event/WorldEvent.cs`), and the
  session log shows each one with its NPC and detail. Since V02: `beacon_reached` (the player reached the beacon; the detail is the scene's name of the place,
  `stall`, or the word of an arrow on a noun's block) and `place_asked` (an NPC answered directions; the place found or `unknown`).
- **A lesson.** Add a file `game-v1/game/lessons/<id>.json` (`lesson.schema.json` lists each step type's fields), its id to `game-v1/game/lessons.json`
  where it should come, a provenance item, and its text to
  `locale/en/lessons.json` (`lesson.<id>.title`, `.text`, and each step's `lesson.<id>.<step>.text`, ...). Its phrases must be rows of
  `game/phrases.json`, its lines recorded speech lines, and its `do` events world events (`WorldEvents`). A `do` step may point the beacon when it begins, at
  a catalogue noun's nearest block (`point`) or at a named place of the scene (`pointPlace`, V02; one of the two); a walk to a place waits for its arrival:
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
- **A place the beacon names.** Give the site, the point or the named prop a `label` in `game/scene.json` (`"label": "río"`); a place with no label shows
  its own name. The label is also the word an NPC's directions answer to (below), so a place the learner may ask for needs one.
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
  its `target`. What the data cannot say is a named hook the code implements: `onComplete` (`guide-met`, `spare-torch`, `point-own-camp`) and a step's
  `restore` (`lesson-begun`, `scan-fire-ring`), which works the step out again from a loaded world, so the save does not keep it. `alsoDoneBy` lists
  objectives whose progress completes this one. The validator refuses an unknown hook. A save keys a step `<objective>.<step>`, so renaming one
  drops its count from older saves. The session's ending (S17's closing moment) waits for the objectives with no `session` that are not orders.
- **An order** (village-plan V03) is an objective that names the NPC the goods are brought to (`deliverTo`) and the goods (`deliver`: items by the name
  the save gives them, each once, and a count), usually from a play `session`; it has no `title` and no `steps`, and its line is `objective.<id>.hud`:

  ```json
  { "$schema": "../../../schema/objective.schema.json", "schemaVersion": 1, "id": "oven-wood", "session": 5,
    "deliverTo": "baker", "deliver": [ { "item": "wood", "count": 5 } ] }
  ```

  An order may ask for several items: the smith's `axe-handle` (V12) takes two wood and two coins, and is delivered only when the inventory holds both.

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
her purchase and her order, is the second; the smith Domingo and the farmer León of `gente-del-pueblo`, V12, are two more, met in the villager's lesson):

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
   `shopkeeper`, `villager` and, since village-plan V05, `fisher`, `baker`, `smith` and `farmer`. A key the game does not draw yet gets a plain grey
   figure until a milestone adds it.
7. **What its host events do** in the world (`game/host-events.json`; an event not listed changes nothing), a provenance item for each new file,
   the recordings (`scripts/prerender-speech.sh`, which reads the voice from the NPC file), and the refresh.

`--validate-content` (and so the refresh and the tests) refuses an NPC with no agent or no prompts, a prompt the bundle lacks or that is another
agent's, a state its agent lacks, no prompt for the initial state, an opening event no state of its agent takes, no voice or a voice with no name, a
key word that is no catalogue noun, a pool prefix another NPC has, and a prompt it waits on that is the home of rows the learner practises with a
focus there that names none of them (V12b). `--talk fisher` then walks up to it in a scripted run. Behaviour the data
cannot say stays code, by id: the shopkeeper's fail-forward (the bottle after four misses, and on a later visit) and the guide's review, transfer
offer and world lines (`docs/game/content-plan.md` section 3.6).

## Stations and items that share a noun (N07)

A recipe's `station` is a catalogue noun that names a block (`mesa` the table, `fuego` the fire-ring stones; the fire counts only while its ring burns). When the output noun names several items
(`manzana`: the apple and the roast apple), the recipe's `makes` gives the item's key (`roast-apple`), and the noun's `labels` gives the text that labels it (`manzana asada`). A new item key joins the enums
of the schemas that list items (`nouns`, `host-events`, `objective`) and, if it is food, `foods` in `game/needs.json`.
