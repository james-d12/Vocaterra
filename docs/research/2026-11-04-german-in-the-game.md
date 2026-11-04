# German as a second language to learn: what the game and the core would need

Research report, 2026-11-04, written on `feature/typo-slip` at `4dad3977`. It answers the owner's request: "Come up with a plan … about
adding German as a language that can be learnt in this game. What would need to change, and if there are any difficulties in adding
another language to learn instead of just Spanish. Any issues or things that make adding another language like German harder, propose
potential solutions or areas to research." No code was changed and no `dotnet` command was run. The facts come from reading the source
with `grep`, `sed` and `wc` and from the web sources in [section 9](#9-sources). Numbers marked **est.** are estimates.

It builds on two earlier reports and does not repeat them:

- [Multilingual core](2026-10-22-multilingual.md) (2026-10-22): the German linguistics, the core's German pack (milestones M36 to M48),
  the new error tags and the German resources. Its sections 2 and 2.2 are still the reference for what German needs from the analyzer.
- [One engine and language packs](2026-10-26-engine-and-language-packs.md) (2026-10-26): the one-engine layout with one project per
  language.

That earlier work stopped at the core. Its decision 1 recommended "core and a core sample bundle only", and the game left German out
(D14, and `docs/game/implementation-plan.md` lines 144 and 255). This report adds three things:

1. what changed since 2026-10-22, now that the core's Phase A is built;
2. the **game** half, which nobody has studied yet;
3. a joint plan, with the difficulties and the questions to research.

## 1. The short answer

**The core is ready to take a second language. The game is not.** Phase A (M31 to M35, M52, M53) is done:
- the engine `src/Vocaterra.Nlp` ships no language;
- Spanish is a pack in `src/Vocaterra.Nlp.Es` (6,804 lines);
- a toy "xx" pack has proved that the engine runs with no Spanish registered;
- the manifest can name its language (ADR 0011).

A German pack (`src/Vocaterra.Nlp.De`) can therefore be added beside the Spanish one, as research 2026-10-22 planned. Six gaps in the
pack interfaces (section 3.2) need small, additive changes first.

The game was built around one target language and has not been through a "Phase A" of its own:

- it loads only Spanish (`SpanishContent.Load` in `GameBundle.cs:39` and `Host/Program.cs:56`);
- it calls Spanish classes directly (`SpanishMorphology()` at 8 call sites, `SpanishNumbers`, `SpanishTextNormalizer`,
  `SpanishAccentNotes`);
- some world data uses Spanish words as ids (recipes, objectives);
- the English locale mixes UI strings with glosses of the Spanish lines;
- saves and learner skill ids do not say which language they belong to;
- the speech models and the 1,082 pre-rendered voice lines are Spanish.

None of this is hard, but all of it has to change before German content can load.

**The real cost of German is content and its review, not code.** The Spanish slice holds:
- 155 phrases, 30 intents, 10 lessons, 87 checks, dialogue for 7 NPCs and 22 nouns;
- hand-written CEFR lists;
- 1,082 recorded lines (about 101 MB).

German needs its own version of each, written by someone who can judge German. It cannot be a translation: the lessons follow the
Spanish curriculum ("ayer y mañana" is a Spanish-tense lesson; German teaches the Perfekt and case instead).

**German is also harder than a second Romance language would be.** It has:
- case on articles and adjectives;
- V2 and verb-final word order;
- separable verbs;
- capitalised nouns, which speech input cannot see;
- umlauts and ß, which learners type as `ae` and `ss`.

Research 2026-10-22 showed that hand-written checks can still carry A1 and A2 with closed prompts, provided the German analyzer works
on noun-phrase chunks. Its false-positive rate is the main technical risk.

**One thing got easier:** sherpa-onnx v2.0.0 (2026-10-23) removed espeak-ng (PR #4055), which was the GPL obstacle behind D2. A
CC0 Piper German voice (`thorsten`, `kerstin`) may now be usable in a distributed build, but whether sherpa-onnx's Piper path still
needs `espeak-ng-data` is unverified (section 7, R1).

**Recommended route** (section 6):
1. Make the game language-neutral first, with Spanish byte-identical, in four milestones that are useful even if German never ships.
2. Then the core's German pack (M36 to M43, refreshed).
3. Then a German A1 slice of the village (sessions 1 to 6) as its own bundle.
4. A2 after that.

Rough effort (**est.**): 2 weeks of orchestrated work to make the game neutral, 4 to 5 weeks for the core pack (as research
2026-10-22 estimated), 3 to 4 weeks for the German A1 content and its speech, and an unknown amount of review time from a German speaker.

## 2. What is generic and what is German

Some of the difficulty comes with *any* second target language, and some only with German. Doing the generic part first is worthwhile
whichever language comes next.

| Any second language (French, Italian, German …) | German in particular |
| --- | --- |
| The game loads a pack chosen by the bundle, not `SpanishContent` | Case, three genders and adjective declension in the morphology, the templates and the Book pages |
| Language-neutral ids for world data (items, recipes, objectives) | V2, the verbal bracket and verb-final order in grammars and checks |
| Saves and the learner model keyed by language | Separable verbs (`ich rufe dich an`) across a gap |
| Glosses of target lines split from the L1 UI strings | Capitals carry meaning when typed, and speech input loses them |
| One ASR model and one set of voices per language; about 100 MB of speech per language | Umlaut and ß input (`ae`, `ss`), and folding that keeps `schon` and `schön` apart |
| A full content bundle, CEFR lists, a phrase sheet and a reviewer | No German Kokoro voice; Piper is needed |
| Typing aids (`AccentShortcuts`) and the font's glyphs per language | Compounds, five plural classes, `du` and `Sie` |
| Choosing the language in the UI | Larger lexicon per lemma (declension tables) |

## 3. The core: what changed since 2026-10-22 and what is left

### 3.1 Done (Phase A)

| Area | State (file) |
| --- | --- |
| Pack interface | `ILanguagePack` (`src/Vocaterra.Nlp/Language/Packs/ILanguagePack.cs:18-78`). Its members: normalizer, folding, numbers, phonetic key, punctuation, optional G2P, grammar defaults, lint vocabulary, repair lines, recast policy, sample words, lexicon profile and error tag set, plus `CreateMorphology`, `CreateAnalysisRules` and `CreateTemplateFunctions` |
| Registry | `LanguagePacks` is empty by default. A host registers packs (`SpanishContent.Options`), and the loader refuses an unregistered language (`ContentLoader.cs:108-123`) |
| Manifest | `language` (BCP-47, hashed when present) and `supportLanguage` (not hashed) (`ContentManifest.cs:36-46`). `game-v1` has no `language`, so it is Spanish |
| Features | `LexicalFeatures` has optional `Case`, `VerbForm`, `Declension`, `Particle` and `Register` (`Core/Stages/AnalysisResult.cs:24-36`). `LexiconProfile` lets a pack add gender `n`, cases and so on |
| Tag sets | `ErrorTagSet` per pack; `ErrorTagSet.V1` is Spanish |
| Sentence type | `NormalizedText.FinalMark`, opt-in through `NormalizationOptions.CaptureFinalMark` |
| Speech | `KokoroSynthesizer` takes a `lang` (default `es`) |
| Tests | `FuzzLanguage` harness; the toy pack `tests/Vocaterra.Nlp.Fuzzing.Tests/Languages/Xx/` runs O1, O2, O7 and O14 with no Spanish registered |
| Plan | M36 to M48 are rows in `docs/nlp/implementation-plan.md` (lines 2049 to 2061), all "proposed"; D14 keeps them "recorded only" until the owner says otherwise |

### 3.2 Interface gaps a German pack will hit

The interfaces were extracted from the Spanish code, so they are Spanish-shaped where Spanish has no counterpart. Each fix is additive,
with a default that keeps Spanish unchanged, and should land at the start of M38 to M40 (or in a small M35b before them).

| # | Gap | Where | Proposed change |
| --- | --- | --- | --- |
| 1 | `IMorphology.Article(def, gender, number[, nounLemma])` has no case | `Language/Morphology/IMorphology.cs:43,50` | An overload with `string? grammaticalCase`, which defaults to the existing call; `ITemplateFunctions.Article` the same; template syntax `article(def,acc)` |
| 2 | `INumberWords.Agree(words, gender)` knows only `m` and `f`, with no case (`ein`, `einen`, `einem`) | `Packs/TextSideInterfaces.cs:37-64` | Pass an optional case; German needs it only for `ein` |
| 3 | `GrammarDefaults(OptionalSubjectPronoun, SubjectPronouns, TrailingPoliteWords)` can only express a *trailing* polite word, and has no V2 | `Packs/TextSideInterfaces.cs:113`; `SlotGrammarMatcher.cs:94-103` | Add `PolitePositions` (trailing, leading or after the finite verb: `Ich hätte bitte ein Brot`) and a load-time `order: "v2"` expansion of grammars (research M40) |
| 4 | Recasts are built from lowercased text; `RecastBuilder` never reads `Token.Original`; `IRecastPolicy` restores accents only | `TextTokenizer.cs` around line 110, `RecastBuilder` | `IRecastPolicy` also restores capitals from the resolved reading (noun, formal `Sie`, sentence start). Typed input only can raise `orth.capital` |
| 5 | `ITemplateFunctions` has no `decl()` (adjective ending) and no `particle()` (separable verb) | `Packs/ITemplateFunctions.cs` | Optional members with default "not supported" errors at template parse time |
| 6 | `IAnalysisRules` is about 20 Spanish-shaped predicates (clitic, preverbal and so on) that the shared resolver consults | `Analysis/IAnalysisRules.cs:16-150` | German returns `false` for most of them and does its work in its own agreement checker (the chunker). **Research question R5**: whether the shared resolver's three steps fit German, or the pack needs a hook to run the chunker before resolution |

Smaller leftovers from Phase A:

- `PredictedErrorMatcher.cs:109-117` ranks tags by `ErrorTags.All`, not by the bundle's `ErrorTagSet`.
- `Lexicon.ReadFile` defaults to the V1 profile (`Lexicon.cs:135,149`).
- `LexiconSourceInfo.SpanishEntries` is a JSON field name.
- `KokoroSynthesizer.DefaultLanguage = "es"`.
- No `PiperSynthesizer` exists.
- `KaikkiImporter` keeps only `lang_code == "es"` and has no language profile (`tools/Vocaterra.Nlp.ContentBuilder/KaikkiImporter.cs:203,317`).
- `scripts/fetch-lexicon-sources.sh` pins only Spanish sources.
- The Spanish pack embeds the game's `content/game-v1/lexicon/seed.json` (`Vocaterra.Nlp.Es.csproj:30`), so a pack depends on a game
  file. A German pack should own its seed lexicon, or the seed should come from the bundle.

### 3.3 The core milestones, refreshed

The rows M36 to M48 stand as research 2026-10-22 section 5 wrote them, with these changes:

- **M36 (lexicon import)** must start from the raw wiktextract data. kaikki's German JSONL is marked deprecated and "will be removed in
  the near future" (checked 2026-11-04). Pin the file by SHA-256 on the day it is fetched. `german-nouns` (CC BY-SA 4.0, about 100,000
  Wiktionary nouns with gender and plural) is a useful cross-check for the gender and plural spot-check.
- **M37 to M40** take the six gaps of section 3.2.
- **M43 (German speech)**:
  - Pin Vosk `vosk-model-small-de-0.15` (45 MB, Apache-2.0), confirmed still published.
  - Parakeet v3, already pinned, covers German (CC-BY-4.0).
  - For TTS, try Piper `de_DE-thorsten-medium` on sherpa-onnx v2.0.1, and record the D2 outcome (section 7, R1).
- **M79 (typo slip)**, proposed on this branch, reads an unknown word as one Damerau-Levenshtein edit away from a known one. In German,
  `schon` and `schön` are one edit apart and both are words, and `ae` for `ä` is two edits. The German folding (`ä`→`ae`, `ß`→`ss`)
  must run before the slip reading, and the slip must not fire when the folded form is a known word. Worth a sentence in M79's brief
  now, so the slip's design does not assume Spanish accents only.

## 4. The game: every Spanish assumption

Collected from `src/Vocaterra.Game.Core`, `src/Vocaterra.Game`, `src/Vocaterra.Host`, `tools/Vocaterra.SpeechRender` and
`content/`. Effort: S is under a day of agent work, M one to three days, L more (**est.**).

### 4.1 Code

| # | Area | Where | What is Spanish | Change | Effort |
| --- | --- | --- | --- | --- | --- |
| G1 | Loading | `Session/GameBundle.cs:39`, `Host/Program.cs:56`; project references to `Vocaterra.Nlp.Es` | `SpanishContent.Load` is the only loader | A game-owned registry `GameLanguages` (es, later de) passed as `ContentLoadOptions.LanguagePacks`; the bundle's manifest picks the pack | S |
| G2 | Morphology | `bundle.SpanishMorphology()` in `GuideSession.cs:205`, `BookPages.cs:44,83,175`, `NounCatalog.cs:227`, `VerbReading.cs:37`; `ConjugationTable.cs`; `LessonBook.cs:232` takes a `MorphologyGenerator` | Casts to the Spanish generator | Use `bundle.LanguageMorphology` (`IMorphology`) throughout; the Book's conjugation table reads persons and tenses from the pack, not from a Spanish table | M |
| G3 | Numbers, normalizer, accent notes | `GuideFacts.cs:49`, `SessionSkip.cs:152,186` (`SpanishNumbers`); `WordProgress.cs:222,481` (`SpanishTextNormalizer`); `NpcService.cs:772`, `GuideTurn.cs:197`, `Host/VerdictReport.cs:85` (`SpanishAccentNotes`) | Direct Spanish calls | `bundle.Language.Numbers` and `.Normalizer`; accent notes become a pack member (`IOrthographyNotes`: Spanish accents, German umlauts and capitals) | S |
| G4 | Words in C# | `Npc/Vocative.cs:32,37` and `Npc/RecastNames.cs:17` (article and preposition lists, `¿¡`); `NpcTurnRules.cs:277` (`tú`/`usted`); `GuideSession.cs:79,82,265,747`; `VoxelSession.cs` (río, agua, fuego, antorcha); `BookVerbLabels.cs:86`; `CheckLayer.cs:44` ("no sé"); `PhraseIds.Hello = "hola"` | About 15 literals left after C05 | Into `game/words.json` (C05's design) or pack data (articles and pronouns come from the pack's lint vocabulary and morphology) | S |
| G5 | Typing aids | `Input/AccentShortcuts.cs:17-34` (Alt + vowel gives an accented vowel, Alt+n gives ñ, `¿¡`); English hints in `DialoguePanel.cs:23,32` and `ReportLayer.cs:25` list "á é í ó ú ñ ¿ ¡" | Spanish map in code | A per-language map in `words.json` (German: Alt+a/o/u give ä/ö/ü, Alt+s gives ß); the hint reads the map | S |
| G6 | Speech engine | `Language/Speech/SpeechEngine.cs:24-33`: Vosk `vosk-model-small-es-0.42`, Kokoro speaker 28 `ef_dora`; `SpeechRules.cs:18,28` thresholds tuned on Vosk small Spanish | Model ids and the guide's voice as constants | A per-language speech table in the bundle or a game file (ASR model id, TTS engine, voices); thresholds per model; re-measure them for German | M |
| G7 | Tag list | `GameDataValidator.cs:95` and `LocaleText.cs:493` iterate `ErrorTags.All` | Assumes the Spanish tag set | Iterate `bundle.Language.ErrorTags` | S |
| G8 | Saves and learner model | `save-v1.json` / `save-v4.json` record only `bundleHash`; skill ids `word.<lemma>`, `phrase.<id>`, `intent.<id>` (`NpcService.cs:167,170`); `LearnerWords.Gender` expects `m`/`f` | No language anywhere | Add `language` to the save as a required field (no migration: older saves are refused, section 6); one save profile per target language (section 5.3); allow gender `n` | S |
| G9 | Language choice | none; only `--locale` for the L1 (`LaunchOptions.cs:38`) | One bundle path | `--language de` first, a title-screen choice later | S, then M |
| G10 | Font | `PixelOperator.ttf` via FontStashSharp | — | **Already has** ä ö ü Ä Ö Ü ß „ “ ‚ ‘ « » (cmap read on 2026-11-04). It lacks capital ẞ (U+1E9E), which is rare; avoid it in content or add a fallback. Extend a German locale's `glyphs` list so the frontend glyph tests cover it | S |

### 4.2 Content and data

| # | Area | Where | What is Spanish | Change | Effort |
| --- | --- | --- | --- | --- | --- |
| G11 | Spanish words as ids | `game/recipes.json` (`"output":"antorcha"`, `"item":"madera"`, `"station":"mesa"`); objective steps (`"noun":"madera"`, `"detail":"madera"`); host event details | World data refers to Spanish lemmas | Neutral ids (`torch`, `wood`, `table`) in world data; `game/nouns.json` maps each id to the target-language lemma per bundle. The `ItemId` enum is already neutral | M |
| G12 | Locale mixes L1 UI and L2 glosses | `content/locale/en/`: `ui.json`, `controls.json`, `keys.json` and `tips.json` are UI; `lines.json` (glosses keyed by Spanish line id), `phrases.json`, `lessons.json` (explains Spanish grammar), `checks.json`, `verbs.json` and `nouns.json` depend on the target language | An English learner of German needs different glosses and lesson text | Split: UI strings stay `content/locale/<l1>/`; target-dependent support text moves to `content/locale/<l1>/<l2>/` (or `support/<l1>/` inside each target bundle). The fallback chain gains the target language | M |
| G13 | The bundle itself | `content/game-v1` (106 MB): lexicon 4.7 MB, 30 intents with Spanish slot grammars, 64 answer sets, dialogue for 7 NPCs, 155 phrases, 87 checks, 10 lessons (6 A1, 4 A2), `cefr/a1.txt` (402) and `a2.txt` (663), 22 nouns, `words.json`, NPC labels ("Panadera") | All of it | A German bundle (section 5.1), authored rather than translated | L |
| G14 | Phrase sheet | `content/PHRASES.md` (137 KB), the source of truth (D-game-10), kept in step by `PhraseSheetTests` | Spanish, with a Spanish-variety section | `content/PHRASES-de.md`, or one sheet per bundle folder; the test is parameterised by bundle | S (code), L (content) |
| G15 | Speech assets | `content/game-v1/speech/` 1,082 WAVs (about 101 MB), `content/speech-index.json`, `scripts/prerender-speech.sh`, `tools/Vocaterra.SpeechRender` (Kokoro voices per NPC in `game/npcs/<id>.json`) | Spanish recordings and voices | A per-bundle index; SpeechRender gains a Piper (or German Kokoro) back end; voices per NPC per language | M (code), M (rendering) |
| G16 | Content gates | `lint --strict --level a2 --host-pools 'guide.*'`, `cefr-check --level a2`, `GameDataValidator`'s level check, LanguageTool on `content/grammar/game-v1-*` | The scripts name `game-v1` | Run each gate per bundle; LanguageTool at `de-DE`; `merge-gate.sh` loops over bundles | S |
| G17 | Core tests on game content | R02: the core's tests, fuzz suite and CLI run on `content/game-v1` | One bundle | The German pack's tests run on the German bundle; a German edit then re-runs the German suite only (keep R02's "any edit re-runs the core" rule per bundle) | S |

### 4.3 Stale documents found along the way

- `docs/game/content-plan.md` section 2.3 cites `Npc/NpcNames.cs` and `Navigation/PointLabels.cs`, which no longer exist (C05 moved them).
- `docs/game/implementation-plan.md` line 144 parks "the German milestones (M31 to M48, D14)", but M31 to M35 are done; only M36
  to M48 are parked.

## 5. Design choices for the game

### 5.1 Bundle shape

| Option | How | For | Against |
| --- | --- | --- | --- |
| A. Fork | Copy `content/game-v1` to `content/game-de-v1` and replace the language | Fastest start; no refactor | World data (positions, recipes, objectives, NPC figures, sessions, scene) is duplicated and drifts; every world fix lands twice; 106 MB per language |
| **B. Split (recommended)** | `content/world-v1`: language-neutral world data (scene, NPC figures and positions, objectives with neutral ids, recipes, items, sessions, tips' triggers). `content/game-v1` (es) and `content/game-de-v1` (de): only target-language content (lexicon, intents, grammars, dialogue, phrases, lessons, checks, nouns map, words, speech, CEFR) | One world, many languages; matches D-game-21's spirit and the core's one-language-per-bundle rule (ADR 0011) | A refactor of `GameData` loading and of the bundle hash in saves; the first step (G11) has to keep Spanish identical |
| C. One bundle, many languages | Per-language subfolders inside one bundle | One hash | Breaks ADR 0011 (one bundle, one language) and the core's loader; no |

With B, the save records both hashes (world and language). Lessons stay in the language bundle, because what a lesson teaches depends
on the language. A lesson's *world* hooks (NPC, place, objective) refer to neutral ids.

### 5.2 Curriculum: authored, not translated

The Spanish lessons follow Spanish grammar: `ayer-y-manana` (preterite and `ir a`), `como-es` (`ser`), `el-mercado` (gender
agreement). A German A1 path for the same village would be:

| Session | Spanish lesson | German counterpart (proposal) |
| --- | --- | --- |
| first words | greetings, yes/no, `qué es esto`, `yo`/`tú` with three verbs | greetings, `ja`/`nein`, `Was ist das?`, `ich`/`du` with `haben`, `sein`, `möchten` |
| nouns and the village | `el`/`la`, plurals | `der`/`die`/`das` (nominative only), noun capitals, the commonest plurals |
| the market | numbers, prices, `quiero` | numbers, `Ich möchte …`, `einen`/`eine`/`ein` (**accusative introduced here**) |
| people | `ser`, adjectives after the noun | `sein`, predicate adjectives (no endings yet), `du` and `Sie` |
| yesterday and tomorrow | preterite, `ir a` | Perfekt with `haben` (A1), `morgen` + present for the future |
| A2 | future, description | dative after `mit`/`zu`/`von`, adjective endings, separable verbs, `weil` clauses |

Adjective declension and the dative wait for A2, so the A1 analyzer only needs nominative and accusative chunks, plus the dative after
the few prepositions content uses. That keeps M39's risk down. The Book's pages need a German layout: an article table by case and
gender, and conjugation with stem changes and separable verbs.

### 5.3 Saves and the learner model

Recommended: **one save profile per target language.** A player learning both keeps two progress records. The learner model, words,
phrases and lessons do not mix, and skill ids stay unqualified inside a profile. The save gains a required `language` and moves under
`saves/<language>/`. The alternative (one profile, skill ids `word.de.Apfel`) touches every skill id and the fixtures, for no gain
until a player learns two languages at once.

### 5.4 L1 and L2 together

D-game-21 built the locale layer for "someone speaking French learning Spanish". German doubles the matrix:

- the UI text needs one translation per L1;
- the glosses and lesson explanations need one per pair (L1, L2).

The split in G12 keeps that tractable. English-for-German is the only new pair this plan needs.

## 6. Proposed plan

Milestone ids: core rows keep their M numbers (core plan). The game work needs a new series: **T** (target
language), which no document uses yet (L is taken by the adaptation design's L01). Each game milestone keeps Spanish byte-identical
until T06, guarded as M31 guarded the core: the game's tests, plus scripted runs (`--say`, `--walk-to`, `--interact`) whose
transcripts must not change.

**Saves may break (owner, 2026-11-04).** The game is unreleased, so no milestone here writes save migration code. A milestone that
changes the save format raises the save version, refuses older saves with a clear message, and re-records the fixtures
(`tests/Vocaterra.Game.Core.Tests/Fixtures/save-v1.json`, `save-v4.json`). "Spanish byte-identical" applies to behaviour (tests and
scripted transcripts), not to the save format. The existing `SaveMigration` (`docs/game/saves.md`) then guards nothing a player needs;
removing it is a possible clean-up for T04, which is the owner's call.

### Phase 1: make the game language-neutral (Spanish only, no German yet)

Useful even if German never ships: it removes the hard-coded Spanish and readies the game for any second language.

| Id | Milestone | Deliverables | Gate | Tier | Size |
| --- | --- | --- | --- | --- | --- |
| T01 | Pack through the bundle | G1, G2, G3, G7: a game `GameLanguages` registry; `IMorphology`, `bundle.Language.Numbers`/`.Normalizer`; tag set from the pack; `IOrthographyNotes` in the core (a small core milestone, M80) | No `Spanish*` and no `Vocaterra.Nlp.Es` reference outside `GameLanguages` (grep gate); scripted transcripts unchanged | H | M |
| T02 | Words out of code | G4, G5: the last literals into `words.json`; typing map as data | Same | H | S |
| T03 | Neutral world ids | G11: recipes, objectives and host events keyed by neutral ids, with `nouns.json` mapping id to lemma | Same; save fixtures re-recorded | H | S |
| T04 | Saves and speech table | G6 (speech table from data), G8 (required `language` in saves, profile folders) | Same; save fixtures re-recorded | H | M |
| T05 | Split world and language bundles | Section 5.1 B: `content/world-v1` and a slimmer `content/game-v1`; G12's locale split; G16 and G17 per bundle | Same; R02 core suite green on the Spanish bundle; hashes re-recorded once, in their own commit | S | L |

### Phase 2: the core's German pack

M36 to M43 from research 2026-10-22, with section 3.3's changes and section 3.2's interface gaps. M36 (lexicon) and M43 (speech) can
start while the game's Phase 1 runs. M41's sample bundle becomes the seed of the game's German bundle rather than a separate
`content/de-v1`.

### Phase 3: a German A1 slice in the game

| Id | Milestone | Deliverables | Gate | Tier | Size |
| --- | --- | --- | --- | --- | --- |
| T06 | German bundle skeleton | `content/game-de-v1` on `world-v1`: lexicon from M36, nouns map, words (greetings, pronouns, typing map), NPC labels and names, `PHRASES-de.md`; `--language de` | Loads; lint strict and `cefr-check --level a1` clean; LanguageTool `de-DE` clean or waived | H | M |
| T07 | Sessions 1 to 3 in German | Intents, grammars (V2), answer sets, dialogue, phrases, checks and the `first-words` path (section 5.2) | Scripted run through session 3 with typed German; M39's corpus gate holds on the bundle's own lines | S | L |
| T08 | German speech | Vosk de in the speech table; German voices per NPC; prerendered lines; thresholds re-measured | A scripted run with `--speech` on German clips; D2 recorded for the chosen voice | H | M |
| T09 | Book and UI for German | Article table by case, German conjugation pages, ä/ö/ü/ß hints, title-screen language choice | Frontend glyph and layout tests for `de` | H | M |
| T10 | Sessions 4 to 6, review and QA | The rest of A1; a QA pass (`qa` agent, focus `language`) on the German slice; review findings fixed | QA report triaged; reviewer sign-off (section 8, decision 5) | S | L |

A2 for German follows the core's Phase C (M44 to M48) and mirrors the Spanish village's Phase B.

### Totals (**est.**)

| Phase | Orchestrated time | Main risk |
| --- | --- | --- |
| 1, game neutral (T01 to T05) | about 2 weeks | T05's bundle split moves many files and every hash |
| 2, core pack (M36 to M43) | 4 to 5 weeks, as research 2026-10-22 estimated | M39's false positives |
| 3, German A1 slice (T06 to T10) | 3 to 4 weeks, plus review | Content quality without a reviewer |

## 7. Questions to research

Each is a spike, run before the milestone that depends on it.

| # | Question | Why it matters | How to answer it | Before |
| --- | --- | --- | --- | --- |
| R1 | Does Piper `de_DE-thorsten-medium` (or `kerstin`) run on sherpa-onnx v2.0.1 without espeak-ng-data, and with what phonemizer? | sherpa-onnx v2.0.0 removed espeak-ng (PR #4055), which may settle D2 for German and Spanish alike. The sherpa Piper page still says `--vits-data-dir=./espeak-ng-data` is required | Bump the pin on a branch; render 20 German lines; listen; read the v2.0 release notes and the Piper model loader | M43, T08 |
| R2 | Is a German Kokoro voice usable? | One TTS engine for both languages keeps SpeechRender simple and the voices consistent. `Thorsten-Voice/Kokoro` (Apache-2.0, CC0 data) is PyTorch only; a third-party ONNX export (`DocXAssist/voices`, `de/model.onnx`) exists, with unverified provenance and licence | Check the export's licence and whether sherpa-onnx's Kokoro loader accepts it; compare with Piper on the same 20 lines | T08 |
| R3 | How well does Vosk small German handle the game's closed grammars and learner accents? | Compounds and separable verbs in a constrained Vosk grammar are untested; `SpeechRules` thresholds were tuned on Spanish | Record 50 German learner-style clips (the owner, or synthetic plus noise); measure as M24 did; compare with Parakeet v3 | T08 |
| R4 | Which German lexicon source survives? | kaikki's German JSONL is deprecated. The raw wiktextract dump (English Wiktionary, filtered to `lang_code` `de`) or de.wiktionary's dump (2.9 GB) have different schemas | Fetch both; count A1/A2 lemmas with gender, plural and full paradigms; check the importer cost; check size against the 300 ms load gate | M36 |
| R5 | Does the shared `ReadingResolver` fit German, or does the pack need a pre-resolution chunker hook? | `IAnalysisRules` is Spanish-shaped (section 3.2, gap 6) | Prototype the chunker on 100 sentences from the planned German phrases, outside the pipeline; see which resolver steps help or get in the way | M39 |
| R6 | What false-positive rate do chunked case checks reach on the game's own German? | The main technical risk; D-game-9 forbids shaming correct speech | Write the German phrase sheet first (T06), and use its model answers as the corpus of correct sentences for M39's gate | M39, T07 |
| R7 | Is there an openly licensed German CEFR list? | The Goethe A1/A2 lists are © Goethe-Institut; CEFRLex's DAFlex states no licence | Ask CENTAL (UCLouvain) about DAFlex's terms; otherwise hand-author `cefr/a1.txt` as Spanish did | T06 |
| R8 | How should the game teach three genders and case without overload? | Gender is the most-cited difficulty for English learners of German; case colours or article tables are common aids | Review how existing courses sequence case at A1 (accusative first, dative late); try a gender colour on nouns in the Book and HUD; check against D-game-13 (on-screen English fades) | T07, T09 |
| R9 | What do typed capitals and `ae`/`ss` mean for grading? | Speech has no capitals; typed input does. `orth.capital` at weight 0 is the earlier proposal | Decide per input mode: typed lowercase nouns get a recast with capitals but no penalty at A1 | T07 |
| R10 | Download size per language | Speech is 101 MB of the 106 MB Spanish bundle; a second language doubles it | Measure Opus/OGG compression of the prerendered lines (MonoGame and the speech cache would need a decoder), or ship one language per install | T08 |

## 8. Decisions for the owner

| # | Decision | Options | Recommendation |
| --- | --- | --- | --- |
| 1 | German in the game, not only in the core | Keep D14's "core only, recorded only"; or amend D14 and add a game decision (D-game-26) | Amend: German as a game target, staged as section 6; Phase 1 can start alone |
| 2 | Bundle shape | Fork; split world and language; one multi-language bundle | Split (section 5.1 B) |
| 3 | Saves | One profile per language; one shared profile with qualified skill ids | One profile per language |
| 4 | Variety and register | de-DE, de-AT, de-CH; `du` everywhere, `Sie` everywhere, per NPC | de-DE content and voice, Austrian and Swiss spellings accepted (Swiss `ss`); per-NPC register (`du` for Luna and the children, `Sie` for the shopkeeper) as research 2026-10-22 proposed. A village of neighbours may argue for `du` throughout at A1, with `Sie` introduced at the shop |
| 5 | Content trust | LLM review only (the D-game-12 pattern); a paid or volunteer German reviewer | An LLM pass plus LanguageTool `de-DE` for development; a human reviewer before anything is published |
| 6 | Voice route | Piper `thorsten`/`kerstin` (CC0 data, MIT model); a German Kokoro export; prerendered only | Decide after R1 and R2; prerendered lines either way |
| 7 | Scope of the first German release | A1 only (sessions 1 to 6); A1 and A2 | A1 only, then A2 with the core's Phase C |

D2 (the GPL pin) may change because of R1. D10's MPL-2.0 question matters only if M47's tagger spike runs. D13 (pronunciation
scoring) gates a German G2P (M46) as before.

## 9. Sources

Accessed 2026-11-04 unless marked.

**Speech**
- sherpa-onnx releases (v2.0.0 on 2026-10-23, v2.0.1 on 2026-10-26): https://github.com/k2-fsa/sherpa-onnx/releases
- sherpa-onnx PR #4055, "Remove espeak-ng and piper-phonemize" (merged 2026-10-20): https://github.com/k2-fsa/sherpa-onnx/pull/4055 ;
  issue #3731: https://github.com/k2-fsa/sherpa-onnx/issues/3731
- sherpa-onnx Piper documentation (still names `espeak-ng-data`): https://k2-fsa.github.io/sherpa/onnx/tts/piper.html
- Piper German voices and model cards (thorsten and kerstin: MIT model, CC0 data; pavoque: CC BY-NC-SA 4.0 data):
  https://huggingface.co/rhasspy/piper-voices/tree/main/de/de_DE
- Kokoro-82M `VOICES.md` (nine languages, no German): https://huggingface.co/hexgrad/Kokoro-82M/blob/main/VOICES.md
- Thorsten-Voice German Kokoro fine-tune (PyTorch only): https://huggingface.co/Thorsten-Voice/Kokoro ; third-party ONNX export
  (unverified): https://huggingface.co/DocXAssist/voices
- Excluded TTS: MMS-TTS deu (CC BY-NC 4.0): https://huggingface.co/facebook/mms-tts-deu ; XTTS-v2 (Coqui Public Model License):
  https://huggingface.co/coqui/XTTS-v2
- Vosk models (`vosk-model-small-de-0.15`, 45 MB, Apache-2.0): https://alphacephei.com/vosk/models
- Parakeet TDT 0.6B v3 (German included, CC-BY-4.0): https://huggingface.co/nvidia/parakeet-tdt-0.6b-v3

**Lexicon and word lists**
- kaikki.org German extract (deprecated): https://kaikki.org/dictionary/German/ ; raw data: https://kaikki.org/rawdata.html
- german-nouns (CC BY-SA 4.0): https://github.com/gambolputty/german-nouns
- hermitdave FrequencyWords (content CC BY-SA 4.0): https://github.com/hermitdave/FrequencyWords
- Goethe-Zertifikat A1 Wortliste (© Goethe-Institut): https://www.goethe.de/pro/relaunch/prf/ca/A1_SD1_Wortliste_02.pdf
- CEFRLex DAFlex (no licence stated): https://cental.uclouvain.be/cefrlex/daflex/

**Repository (read at `4dad3977`)**
- `src/Vocaterra.Nlp/Language/Packs/`, `src/Vocaterra.Nlp/Content/ContentManifest.cs`, `ContentLoader.cs`, `src/Vocaterra.Nlp.Es/`
- `src/Vocaterra.Game.Core/` (files as cited in section 4)
- `content/game-v1/`, `content/locale/`, `content/PHRASES.md`
- `assets/fonts/pixel-operator/PixelOperator.ttf` (cmap)
- [ADR 0011](../nlp/adr/0011-target-language-as-a-bundle-property.md), [ADR 0012](../nlp/adr/0012-one-engine-assembly-and-language-packs.md)
- [core decisions](../nlp/decisions.md) (D2, D10, D13, D14), [game decisions](../game/decisions.md) (D-game-8, 10, 12, 13, 21, 22, 25)
- [content plan](../game/content-plan.md), [core implementation plan](../nlp/implementation-plan.md) §11
