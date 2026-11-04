# Vocaterra Studio: a desktop editor for the game's content

Research report, 2026-11-04, written on `feature/typo-slip` at `4dad3977`. It answers the owner's request: "come up with a plan" for "a
creation kit or something to manage the creation of the game assets", so that "humans like me" can add content, since "currently all the
content in `content/` has been AI generated". No code was changed and no `dotnet` command was run. The facts come from reading the source
and the content with `grep`, `sed` and `wc`, and from the web sources in [section 10](#10-sources).

The owner set the scope in the same conversation (2026-11-04):
- "keep it simple and focus entirely on whatever is kit ready, so lessons objectives orders, npcs, voices, dialogue, english text";
- "it should be a desktop app".

It replaces the authoring CLI, C10, which `docs/game/content-plan.md` section 3.8 specifies and nobody built. C10's library and its
`refs` and `check` survive here, inside the editor (section 4.1).

## 1. The short answer

- **Build a desktop app with Avalonia** (MIT, C#, runs on Linux, Windows and macOS). Call it **Vocaterra Studio**, in
  `tools/Vocaterra.Studio`, on a library `tools/Vocaterra.Authoring` that has no UI and does all reading, writing and checking.
- **It edits the same files on disk, in place.** The JSON in `content/` stays the source of truth, so git, review, the gate and
  AI-written content keep working beside it. The Studio has no database and no export step.
- **It never decides what is valid.** The core's loader, the game's validator, the lint and the CEFR check do, as they do today. The
  Studio runs them and shows their findings next to the thing they are about.
- **Its value is the links, not the forms.** Adding an NPC touches about ten places in seven files ("Adding an NPC" in
  `content/README.md`). The Studio shows them as one thing: an NPC, its agent's states, the prompts it waits on, its lines with their
  English glosses and recordings, and the lessons and objectives that name it.
- **Eight milestones, ST01 to ST08**, on a branch `feature/studio`. The first three give a read-only browser with findings and a Build
  button; each one after adds an editor the owner can use the day it lands.
- **Three small changes outside the Studio** come first: structured problems from `GameDataValidator`, a load that returns problems
  instead of throwing, and a one-line render option in SpeechRender (section 6).

## 2. Scope

### 2.1 In (the owner's list)

| Kind | Files the Studio reads and writes |
| --- | --- |
| NPCs | `game/npcs/<id>.json`, the index `game/npcs.json` |
| Voices | the `voice` block of each NPC file; the recordings in `speech/<npc>/` (read and played, written only by SpeechRender) |
| Dialogue | the core's `agents/<id>.json`, `prompts/prompts.json` and `dialogue/dialogue.json` |
| Lessons | `game/lessons/<id>.json`, the index `game/lessons.json` |
| Objectives and orders | `game/objectives/<id>.json`, the list in `game/session.json` |
| English text | `content/locale/en/*.json` (11 files, 2,550 lines) |
| Bookkeeping | `provenance.json` (one item per new file) |

All paths are under `content/game-v1/` except the locale. The amount today: 7 NPCs and 7 agents, 70 prompts, 272 dialogue pools with 349
lines, 10 lessons, 7 objectives.

### 2.2 Out

- **The scene:** `game/scene.json` (placement, sites, points, labels). It is JSONC with comments, and its coordinates need a map.
- **NPC figures:** drawn by code, `Graphics/Figures/NpcFigures.cs`. A figure key the game does not draw shows a plain grey figure.
- **Phrases, nouns and items:** `PHRASES.md`, `game/phrases.json`, `game/nouns.json`. The Studio picks existing ones. A lesson that needs
  a new phrase is still a hand edit (D-game-10: the row goes in `PHRASES.md` first).
- **Objective hooks:** `onComplete` and `restore` name code. The Studio offers the names the validator knows and writes nothing else.
- **Intents:** `intents/intents.json` (1,072 lines of slot grammars and keyword rules). A prompt's allowed intents are picked from the
  existing ones. Open question 2 (section 9).
- **`host-events.json`:** not on the owner's list, but orders and NPC trades need it (section 6.4, open question 1).
- **Anything run-time:** the Studio does not run the game, play a conversation, or touch saves.

## 3. What there is to build on

### 3.1 Loading and checking

- **The core's bundle.** `SpanishContent.Load(dir, options)` (`src/Vocaterra.Nlp.Es/SpanishContent.cs:27`) calls `ContentLoader.Load`
  (`src/Vocaterra.Nlp/Content/ContentLoader.cs:33`). The loader checks the cross-references: prompts against answer sets, intents, agents
  and slot grammars; agents' conditions and effects against intents; pool sizes; the `not_understood` pool; template rendering. It throws
  `ContentValidationException`, whose `Problems` is an `IReadOnlyList<string>`. `VerifyHashes = false` skips the hashes, as `lint` does, so
  edited content loads before `hash`.
- **The game's data.** `GameData.Load(bundle, locale)` (`src/Vocaterra.Game.Core/Content/GameData.cs:139`) builds the whole object
  graph and runs `GameDataValidator` (`GameDataValidator.cs:54`). `Validate(GameData)` returns `IReadOnlyList<string>` and never throws;
  `Load` throws `InvalidDataException` with every problem. Each problem is a sentence that names its file and id (`"game/phrases.json:
  the phrase '...' belongs to the prompt '...'"`, `"locale en: the string '...' is missing."`). There is no structured type, no line
  number and no severity. The unchecked `Read` (line 162) is internal.
- **The lint.** `ContentLinter.Lint(bundle, options)` (`src/Vocaterra.Nlp/Content/ContentLinter.cs:28`) returns a `LintReport` of
  `LintFinding(Severity, CheckId, Path, Ids, Message, WaivedBy?, Suggestion?)` (`LintModel.cs:29`). The findings are already structured.
- **CEFR.** `CefrChecker` (core) lemmatizes and checks against `cefr/a1.txt` and `a2.txt`. The game's validator holds each lesson to
  its own `level`.
- **Schemas.** `content/schema/` has a draft-07 schema for every game file and the locale. Only a test reads them today (NJsonSchema, MIT,
  in `tests/Vocaterra.Game.Core.Tests`).

### 3.2 The pipeline after an edit

`scripts/content.sh refresh` runs, in order, and stops at the first failure: `hash` of game-v1 and of the locale, a provenance check
(every file has an item, no item names a missing file), `validate`, the gate's lint (`--strict --level a2 --fail-on-level --host-pools
'guide.*'`), `cefr-check` at A2 (failing) and A1 (report), the host's `--validate-content`, the host's `--grammar-strings`, then the
offline grammar check, which names the LanguageTool step on a cache miss. `scripts/content-loop.sh` runs the whole loop: refresh, the
LanguageTool cache, prerender, the bundle hash in saves, the build, the goldens and the fast tests.

Speech: `scripts/prerender-speech.sh` runs `tools/Vocaterra.SpeechRender`. A recording is skipped when its text, speaker, voice, speed,
gain and rate match `content/speech-index.json`, so it renders only what changed. It needs the Kokoro model (`kokoro-multi-lang-v1_0`
under `VOCATERRA_MODELS_DIR`), writes `speech/<npc>/<lineId>.wav` and `.slow.wav`, their provenance items, and
`docs/game/speech-checklist.md`, which keeps the owner's marks (ok, cut-off, mismatch). It has no option to render one line.

### 3.3 The files' layout

2-space indent, LF, UTF-8 and a final newline (`.editorconfig`). The files mix two styles: one object per line (`agents/`, `prompts.json`,
`provenance.json`) and block style (the game's files, the locale). Key order is meaningful to a reader, and the indexes' order is meaningful
to the game (`npcs.json` is the saves' order). No code writes these files today; the only writers in the repository are `hash`
(`manifest.json`), SpeechRender (`speech-index.json`, provenance items) and the grammar tools (their cache and JSONL).

### 3.4 The rules a new project lives under

- **ADR 0009 and CLAUDE.md:** game words live in the game. The Studio is a game tool, like SpeechRender. Dialogue lines, prompts and an
  NPC's agent are game content in the core's format, so writing them is allowed; changing what the formats mean is a core milestone.
- **D-game-1:** engine code lives only in `src/Vocaterra.Game`. The Studio references no MonoGame.
- **`Vocaterra.Game.Core` references no package** (`ArchitectureRulesTests.cs:29-42`). The Studio's packages are its own and never
  reach Game.Core.
- **The build:** net10.0, `TreatWarningsAsErrors`, nullable, analyzers, generated XML docs (`Directory.Build.props`), central package
  versions (`Directory.Packages.props`), nuget.org only.
- **Licences:** `scripts/license-scan.sh` scans the core by default and takes a `--target`; the allow-list
  (`scripts/license-allowlist.txt`) accepts MIT, Apache-2.0, BSD and a few others, and refuses GPL, LGPL, AGPL and SSPL.
- **The machine:** every heavy command runs through `scripts/limited.sh` (6 GB, 10 cores, one at a time), builds with `-m:4`, tests with
  `-m:2`. The Studio's Build button obeys the same rule.

## 4. The design

### 4.1 Two projects and a test project

| Project | What it holds | References |
| --- | --- | --- |
| `tools/Vocaterra.Authoring` | the workspace, the documents, the writer, the references index, the checks, the pipeline runner | `Vocaterra.Game.Core`, `Vocaterra.Nlp.Es`; NJsonSchema |
| `tools/Vocaterra.Studio` | the Avalonia app: windows, views, view models | `Vocaterra.Authoring`; Avalonia packages |
| `tests/Vocaterra.Authoring.Tests` | the library on a copy of `content/` in a temporary folder | the library; xunit |

The library is where the work is tested. The app is thin: it binds views to the library's model and has few tests of its own (Avalonia's
headless test package exists, but v1 does not need it). All three join `Vocaterra.sln`, so the gate builds them; the licence scan gains
`--target tools/Vocaterra.Studio`.

### 4.2 Documents first, checks second

The real loaders refuse a broken bundle: an NPC whose agent is missing, a prompt naming a state that is not there yet. An editor must open
exactly that content, halfway through an edit. So the Studio has two layers:

1. **Documents.** The library reads every in-scope file as JSON (`System.Text.Json`'s `JsonDocument`) and builds a light model of its
   own: NPC, agent, state, transition, prompt, pool, line, lesson, step, objective, locale string. It needs only well-formed JSON, so it
   always opens. A file that is not valid JSON is shown as one finding with its line and column.
2. **Checks.** After each save (and on request), the library runs the real authorities on the files as they are on disk: the core's
   loader with `VerifyHashes = false`, `GameDataValidator`, the lint with the gate's options, the CEFR check and the schemas. Each
   problem becomes a **finding** attached to the item it names.

This keeps the rule of content-plan section 3.7: the loaders are the authority, the Studio's model only serves the editor.

### 4.3 Writing files in place

The Studio changes only the bytes it means to change. It does not re-serialize a file.

- **How:** `Utf8JsonReader` gives each token's byte offset. To change a value, the writer replaces that value's span. To add an entry
  (a line to a pool, an id to an index, a string to a locale file), it copies the layout of its neighbour: the same indent, inline or
  block, comma placement. To remove one, it removes the entry and its separator.
- **When it cannot:** a layout it cannot place an edit in (an empty array written inline, a hand-aligned table) is refused with a clear
  message, as C10 specified. The owner then edits that file by hand once.
- **New files** (a new NPC, lesson, objective, agent) are written whole, in the layout of the existing files of their kind.
- **Tests:** every edit kind runs on a copy of `content/`; the test checks that the file still parses, validates against its schema,
  that the bundle still loads, and that every byte outside the intended span is unchanged.
- **Saving:** edits collect in memory, with undo and redo, until **Save**, which writes every touched file. A **Changes** panel lists the
  files touched since the last commit (`git status --porcelain content/`). Git is the history; the Studio never commits.
- **Outside edits:** a file watcher notices when a file changes on disk (a Claude session, a `git checkout`). With no unsaved edits the
  Studio reloads; with unsaved edits it asks.

### 4.4 Findings

| Source | Shape today | What the Studio shows |
| --- | --- | --- |
| Core loader | strings in `ContentValidationException.Problems` | errors, matched to items by the ids in the text |
| Game validator | strings from `Validate` | errors, structured once section 6.1 lands |
| Lint | `LintFinding` with severity, check id, path and ids | errors, warnings and info, with the suggestion and any waiver |
| CEFR | lemmas above a level, by text | a level badge on each Spanish line, and errors where the lesson's level is broken |
| Schemas | NJsonSchema errors with a JSON path | errors at the field |
| Grammar (Build only) | the grammar command's findings | warnings on the line, from the cache |

The **Findings** panel lists them all; a click opens the item. Each editor marks its fields in place.

### 4.5 The Build button

**Build** runs `scripts/limited.sh scripts/content.sh refresh` and streams its output into a log panel, then parses the findings it can
place. Two follow-ups, each a button the panel offers when it applies:

- **Render speech** when lines or voices changed: `scripts/limited.sh scripts/prerender-speech.sh`, which renders only what changed.
- **Check grammar online** on a cache miss: `scripts/languagetool.sh start`, then the online grammar check, then refresh again.

A third, **Full loop**, runs `scripts/content-loop.sh` for the steps a developer runs before a merge (goldens, the bundle hash in
saves, the tests). It is offered, never run automatically.

The Studio calls the scripts, so it inherits their order and their limits, and a change to the pipeline needs no Studio change.

### 4.6 The screens

The window: a **navigator** on the left (NPCs, Lessons, Objectives, English text), the **editor** in the middle, and on the right a
**Used by** panel for the selected item. Findings, Changes and the build log are tabs along the bottom. Search (Ctrl+K) finds any id or
any text, Spanish or English.

- **NPC.** Label, name, figure key (a choice of the keys the game draws, with a warning for any other), prompts by state, opening
  (`always`, `first`, `never` and its event), focus (phrases picked per prompt), key words, pool prefix. The English role
  (`npc.<id>.role`) sits beside them. A to-do list shows what the Studio cannot do: placing the NPC in `scene.json`, a figure the game
  does not draw yet, its host events.
- **Voice** (part of the NPC). Speaker and voice name from the list in `docs/nlp/api.md` section 6, speed, gain. Play buttons for each
  existing recording, normal and slow. **Try voice** renders one chosen line with the new settings into a temporary folder (section 6.3).
  Recordings that the change makes stale are marked, and Build offers to render them. The speech checklist's marks are shown read-only.
- **Dialogue** (part of the NPC; an agent belongs to one NPC). Three tabs:
  - *States:* each state with its transitions as a table: on intent or on host event, condition, target state, actions (dialogue intent,
    host event, pool). Validator rules shown as hints: every state takes `<npc>.delivered` when the NPC has an order; one state takes
    `cant_pay`, `wont_sell`, `none_asked` and `no_room` when it sells. A drawn graph waits for a later version.
  - *Prompts:* each prompt of the agent: text (Spanish), mode, difficulty, answer set, allowed intents (picked), slot grammars.
  - *Lines:* each pool, each line's Spanish beside its English gloss (`line.<lineId>` in `locale/en/lines.json`), its CEFR badge, its
    recording's play button and checklist mark. Adding a line adds its gloss key in the same edit.
- **Lesson.** Title, text and level; then the steps in order, each with the fields its type allows (`explain`, `model`, `listen`,
  `repeat`, `answer`, `do`, `recap`, `review`; `person`, `tense`, `point` or `pointPlace`, the trigger). Phrases and lines are picked; a
  step's English texts (`lesson.<id>.<step>.text`, `.personRetry`, `.tenseRetry`, ...) sit under it. Reorder by dragging. The lesson's
  place in `lessons.json` is set in the navigator.
- **Objective and order.** Kind first (checklist, one line, order). Checklist: Spanish title, English gloss, steps with trigger, noun
  and target. Order: NPC, goods (item and count), session, lesson. Hooks from the validator's list. Both show the save key of each step
  and warn that renaming it drops its count from older saves. The order in `session.json` is set in the navigator.
- **English text.** Every string of `locale/en/`, by file and key, with search, the item that uses it, and two reports: keys asked for
  but missing, and keys nothing asks for. Strings that belong to an item are also edited inside that item's editor.

### 4.7 New items

Each **New** command writes a minimal valid item and every entry it needs, then opens it:

- **New NPC** (id, label, name, voice): the NPC file, its id at the end of `npcs.json`, an agent with an `idle` state and a greeting
  transition, an `<id>-open` prompt, a `<id>_greet` pool with one line, the role and the line's gloss, and provenance items. The bundle
  loads after it.
- **New lesson** (id, NPC, level, title): the file with one `explain` step, its id in `lessons.json`, its English texts, provenance.
- **New objective** (id, kind): the file with one step (or the order's goods), its id in `session.json`, its English line, provenance.
- **New state, prompt, pool, line, step:** inside their editors.

**Rename** an id changes every use the references index knows (files, locale keys, `{npc:<id>}` in English text) in one edit, after
listing them. Renaming an NPC, a lesson step or an objective step warns about saves. **Delete** lists the uses first and refuses while
any remain.

### 4.8 Spanish while typing

Each Spanish field shows its CEFR level as you type (the core's `CefrChecker` on the text) and a mark for a lemma above the lesson's or the
bundle's level. Grammar comes from the grammar cache when the exact text is in it; new text shows "not checked yet" until Build. A live
LanguageTool check while typing is left for later (it needs the server running).

## 5. Technology

- **Avalonia 12.1** (the current stable line in mid-2026, [1]). The framework is MIT ([2]). Since 2025 the paid Accelerate tier holds
  the new DevTools, the Visual Studio and VS Code extensions, and some components, among them WebView, Media Player and the Accelerate
  TreeDataGrid ([2], [3]). The Studio uses none of them: the built-in TreeView, ListBox, ItemsControl, TextBox and the MIT DataGrid
  package, checked by the licence scan before each package is added. The community MIT diagnostics fork ([4]) can replace DevTools in
  debug builds if wanted.
- **MVVM** with CommunityToolkit.Mvvm (MIT).
- **Audio playback:** Avalonia has none outside Accelerate. v1 plays a WAV by running the system player (`pw-play` on Fedora's PipeWire,
  `aplay` as the fallback, configurable). An in-process player (OpenAL through an MIT binding) can come later if the external player is
  awkward.
- **Why not the alternatives:** MAUI has no Linux desktop; WPF and WinForms are Windows only; a Blazor page in a desktop shell would work
  but adds a web stack to a pure C# repository. An in-game editor (MonoGame with ImGui) only pays off for the scene, which is out of scope.

## 6. Preconditions and gaps

### 6.1 Structured problems from the game's validator (Game.Core change)

`GameDataValidator.Validate` returns sentences. The Studio needs `(file, ids, message)` to attach a problem to an item. A small change:
a `GameDataProblem` record with those fields, `Validate` returning them, and `ToString()` keeping today's sentence, so the host's
`--validate-content` output and the tests do not change. Part of ST02.

### 6.2 A load that returns problems (Game.Core change)

`GameData.Load` throws, and `Read`, which does not, is internal. The Studio needs "build what you can, then list every problem". A public
`GameData.TryLoad(bundle, locale, out problems)` (or a result type) over the existing `Read` and `Validate`. Part of ST02.

### 6.3 One-line render in SpeechRender (game tool change)

SpeechRender renders the whole bundle against the index. **Try voice** needs `--line <lineId> --speaker <n> --voice <name> --speed <s>
[--gain <g>] --out <file>`, which writes one WAV and touches neither the index, the checklist nor the bundle. Part of ST04. It loads the
Kokoro model each time (a few seconds), which is fine for a button.

### 6.4 `host-events.json` (scope question)

An order's reward is a `give_item` in `game/host-events.json` (`baker.pay`), and an NPC's sale or purchase is a `sell` or `buy` action
there. Without it the Studio can create an order whose NPC thanks the player but pays nothing, and an NPC who cannot trade. The file is
small and has a schema. The recommendation is to add it to ST07 as "what this NPC's host events do", edited in the NPC's Dialogue view
beside the transitions that raise them.

### 6.5 What stays by hand

- Placing a new NPC in `scene.json`.
- A figure for a new figure key (code, `NpcFigures.cs`).
- New phrases and nouns (`PHRASES.md`, `phrases.json`, `nouns.json`), and new intents.
- New hooks (code).

The Studio lists each as a to-do on the item that needs it, so nothing is forgotten silently.

## 7. Milestones (the ST series)

On a branch `feature/studio` from `main`, prefix `ST<nn>:`. Each ends with the gate. Models follow the owner's rule: Haiku 5.5 by
default, Sonnet 5.5 where the design is heavy (marked).

| Id | What lands | Usable result |
| --- | --- | --- |
| ST01 | `Vocaterra.Authoring`: the workspace, the document model of every in-scope file, the in-place writer with its byte tests, the references index. The tests run on a copy of `content/`. | none yet (library) |
| ST02 | The checks: core loader, game validator, lint, CEFR, schemas, as findings on items. Game.Core: 6.1 and 6.2. The pipeline runner (refresh, render, grammar, full loop) through `limited.sh`. | none yet (library) |
| ST03 (Sonnet) | `Vocaterra.Studio`: the Avalonia app, navigator, read-only views of every kind, Used by, Findings, Changes, Build, search. Licence scan target. | browse the content, see every problem, run Build |
| ST04 | NPC and voice editing, New NPC, playback, Try voice (6.3). | add an NPC and hear it |
| ST05 (Sonnet) | Dialogue: states and transitions, prompts, pools and lines with glosses, the validator's transition hints. | write what an NPC says |
| ST06 | Lessons: steps by type, phrase and line pickers, English texts, New lesson, ordering. | write a lesson |
| ST07 | Objectives and orders, New objective, ordering, and `host-events.json` if the owner agrees (6.4). | write an order with its reward |
| ST08 | English text: the locale browser, missing and unused keys. Rename and Delete across files. | manage every string; rename safely |

ST01 and ST02 are where the risk is (the writer and the findings). ST03 is the first time the owner sees the app; the editors after it can
come in any order the owner prefers.

## 8. Risks

- **The writer.** Editing JSON in place across two layouts is the hardest part. The tests on real content (every edit kind, every file
  kind, byte comparison) are what make it safe, and a refusal is always better than a reformatted file.
- **Two people editing.** A Claude session and the Studio can change the same file. The file watcher and the "reload or keep" question
  cover it; Save refuses when a file changed on disk since it was read.
- **Findings by text.** Until 6.1 lands, game-validator problems are matched to items by the ids in their sentences. ST02 lands 6.1 first.
- **Avalonia's licensing drifts.** More of it may move to Accelerate. The Studio uses only the core framework and checks each package
  with the licence scan; the library holds everything but the views, so a change of UI stack would cost only ST03's layer.
- **Build time.** Avalonia's XAML compiler adds to every build of the solution on a limited machine. If it hurts, the Studio can leave
  `Vocaterra.sln` for its own filter (`Vocaterra.Studio.slnf`) and a gate step of its own.
- **The game moves on.** New step types, actions and validator rules land in the village and W series. The Studio's schema check catches
  fields it does not know; an editor shows them as raw JSON until it learns them, rather than dropping them.

## 9. Open questions for the owner

1. **`host-events.json` in v1?** Recommended yes, in ST07 (6.4).
2. **Intents:** pick existing ones only (recommended for v1), or edit them too? Editing slot grammars is closer to the core's work.
3. **The speech checklist:** let the owner set the marks (ok, cut-off, mismatch) in the Studio, as the file is written by SpeechRender
   and keeps them? Not in v1 as planned.
4. **The name** (Vocaterra Studio) and the series prefix (ST).
5. **C10:** mark `content-plan.md` section 3.8 and `implementation-plan.md` as superseded by this plan once accepted, and record the
   decision (the Studio, its scope, and that a game tool writes the core's dialogue files) as D-game-26.

## 10. Sources

1. Avalonia's NuGet profile, the `Avalonia` package at 12.1.x (July and August 2026): https://www.nuget.org/profiles/AvaloniaUI
2. "Building a sustainable future for Avalonia" (Avalonia's blog): the framework stays MIT; the new tooling and some components are paid.
   https://avaloniaui.net/blog/building-a-sustainable-future-for-avalonia
3. Avalonia's licence schedules (Community and Accelerate tiers): https://avaloniaui.net/legal/1A, https://avaloniaui.net/legal/1C
4. ClassicDiagnostics.Avalonia, a community MIT port of the Avalonia 11 diagnostics: https://www.nuget.org/packages/ClassicDiagnostics.Avalonia
