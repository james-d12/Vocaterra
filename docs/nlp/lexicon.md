# The lexicon: what ships, where it comes from, how to regenerate it

The analyzer knows only the words in the lexicon (every other word is `vocab.unknown`, except a learner's slip of a lexicon word: a regularized irregular verb
form, `quero`, and since M58 a plural made with the wrong ending, `pans`; see [api.md](api.md), "Irregular forms, plurals and unknown words"). The bundle `content/game-v1/lexicon/` holds five lexicon files and the sidecar of the generated one:

| File | Origin | Entries | Lemmas |
| --- | --- | --- | --- |
| `seed.json` | hand-authored: 32 verbs with the present and preterite, articles, pronouns, numerals 1 to 20, about 150 nouns and 80 adjectives | 1 217 | 395 |
| `game.json` | hand-authored: the words of the first game bundle (its spells and items) | 51 | 9 |
| `world.json` | hand-authored: the words of the 3D world (blocks, lamp, river, mushroom) | 16 | 9 |
| `supplement.json` | hand-authored: seven words that existing content uses and the other files lack (found by the coverage tests) | 7 | 5 |
| `wiktionary-es.json` | **generated** from Wiktionary by `Vocaterra.Nlp.ContentBuilder import-lexicon`, marked `"generated_by": "wiktionary-import"` | 40 263 | 3 000 |
| `wiktionary-es.source.json` | the **sidecar** of the generated file: sources, hashes, parameters, importer version, counts | - | - |

Merged: 41 094 entries, 32 507 distinct forms, 3 379 lemmas (`lexicon-report content/game-v1`; the hand-authored files alone have 1 291 entries). The generated file has, by part of speech
(lemmas / entries): nouns 1 847 / 4 513, verbs 680 / 33 066, adjectives 701 / 2 399, adverbs 137 / 137, interjections 92 / 92, pronouns 28 / 28, prepositions
15 / 15, conjunctions 13 / 13. A verb brings about 50 forms (infinitive, present, preterite, imperfect, future and conditional indicative, present and
imperfect subjunctive, the imperatives), which is why verbs are 80 % of the entries.

Licence: the generated file and the selection of lemmas are CC BY-SA 4.0 (Wiktionary text; the frequency list's content licence is the same); the notice
is `content/game-v1/ATTRIBUTION.md`. [ADR 0008](adr/0008-lexicon-sources-and-frequency-filter.md) records why each choice below was made.

## Sources

| Source | URL | Size, SHA-256 | Retrieved |
| --- | --- | --- | --- |
| kaikki.org Spanish extract of English Wiktionary (wiktextract JSONL, post-processed), **current pin (M90)** | `https://kaikki.org/dictionary/Spanish/kaikki.org-dictionary-Spanish.jsonl` | 1 119 672 965 bytes, `336903d1290968d77bd361c620d7d799ad7a7a9bc6ad353ec35954f2e15fbe6f` | 2026-11-08 (the site's file of 2026-10-30, extracted from the enwiktionary dump of 2026-10-01, wiktextract commits `19f9a30` and `6f96ddf`) |
| kaikki.org Spanish extract, **earlier pin, the one `content/game-v1/lexicon/` was built from** (wiktextract JSONL, post-processed; 811 049 lines, all Spanish) | `https://kaikki.org/dictionary/Spanish/kaikki.org-dictionary-Spanish.jsonl` | 1 054 565 723 bytes, `7b23a5dc004a47b63edc2e79a34b246bbf59d1a3067a1843a5c554c0e252b97c` | 2026-10-20 (the site's file of 2026-10-03, from the enwiktionary dump of 2026-09-02, wiktextract commits `1a05e46` and `e3d6d4e`) || hermitdave/FrequencyWords, OpenSubtitles 2018 counts (50 000 lines) | `https://raw.githubusercontent.com/hermitdave/FrequencyWords/525f9b560de45753a5ea01069454e72e9aa541c6/content/2018/es/es_50k.txt` | 658 626 bytes, `dcff3ad4316192f4dc4ff7d26e637c6ff314ef1ca0f3f720c5649018a71056c0` | 2026-10-20 |
| kaikki.org German extract of English Wiktionary (wiktextract JSONL, post-processed; 371 793 lines, all German; M36) | `https://kaikki.org/dictionary/German/kaikki.org-dictionary-German.jsonl` | 1 095 258 648 bytes, `4fb15662a125085e1eb6b533a210b45edcdffa7b8e72e07634f46e1485e4e715` | 2026-11-08 (the site's file of 2026-10-30, enwiktionary dump of 2026-10-01, wiktextract `19f9a30` and `6f96ddf`) |
| hermitdave/FrequencyWords, OpenSubtitles 2018 counts, German (50 000 lines; M36) | `https://raw.githubusercontent.com/hermitdave/FrequencyWords/525f9b560de45753a5ea01069454e72e9aa541c6/content/2018/de/de_50k.txt` | 662 497 bytes, `d9e50546fd7e8b6fe6542a2b33c51d1331092b2a3916ec09f80d97856068705b` | 2026-11-08 |

Neither file is committed. `lexicon-sources/` is git-ignored, like `models/`. The kaikki.org URL is not versioned (the publisher replaces the file
with every extraction, and marks this post-processed download as deprecated), so the SHA-256 is the pin. The frequency URL names a commit and
cannot change.

**M90 (2026-11-09): the Spanish pin moved.** kaikki.org replaced its Spanish file (1 054 565 723 to 1 119 672 965 bytes), so `fetch-lexicon-sources.sh` refused the
new one; the script now pins the new file (first row above). The committed `content/game-v1/lexicon/wiktionary-es.json` and its sidecar were built from the
**earlier** pin and keep its dump in the sidecar; they were not regenerated. A regeneration from the new pin is a separate, deliberate step (read the report,
review the diff, re-run the whole suite, re-hash the bundle). The importer itself gives the same bytes on both runs of one source file (M36 proves it for
Spanish: the importer before and after the language profile, on the new file).

## Fetch and regenerate

```bash
scripts/fetch-lexicon-sources.sh            # downloads into lexicon-sources/, verifies size and SHA-256, writes lexicon-sources/sources.json
scripts/fetch-lexicon-sources.sh --check    # verifies what is there, downloads nothing

dotnet run --project tools/Vocaterra.Nlp.ContentBuilder -- import-lexicon \
  lexicon-sources/kaikki.org-dictionary-Spanish.jsonl content/game-v1/lexicon/wiktionary-es.json \
  --frequency lexicon-sources/es_50k.txt --top 3000 \
  --hand-authored content/game-v1/lexicon \
  --sidecar content/game-v1/lexicon/wiktionary-es.source.json

dotnet run --project tools/Vocaterra.Nlp.ContentBuilder -- hash content/game-v1       # then the steps of "Recipe: add a prompt..." in changing-the-core.md.md
dotnet run --project tools/Vocaterra.Nlp.ContentBuilder -- validate content/game-v1
dotnet run --project tools/Vocaterra.Nlp.ContentBuilder -- lexicon-report content/game-v1
```

The import reads 1 GB in about ten seconds and needs about 300 MB of memory (500 MB without `--top`, where it writes 585 151 entries, a 68 MB file). Two runs write byte-identical files (and the 5 000-lemma variant had the
same bundle hash in two separate runs). `--hand-authored` takes the folder of the bundle's lexicons; the generated file in that folder is ignored.

Options of `import-lexicon`: `--max N` stops after N imported Spanish entries (a test aid); `--frequency <file>` ranks lemmas by the list; `--top N` keeps the N
most frequent (needs `--frequency`; without it nothing is dropped); `--hand-authored <folder or a.json,b.json>` defers to hand-authored lexicons;
`--sidecar <path>` writes the sidecar, `--sources <path>` names the `sources.json` it copies from (default: next to the input). The sidecar is built
before anything is written, and the importer re-hashes the files it read, so a source that differs from `sources.json` stops the run.

**Re-pinning.** If `fetch-lexicon-sources.sh` reports that the download differs from the pin (kaikki.org published a new extraction), nothing is lost: the
committed lexicon does not depend on the download. To move to the new extraction on purpose, edit the pinned `sha256` and byte count in the script, fetch,
run the import, read the report (`lexicon-report`, the skipped-by-reason table in the sidecar), bump `KaikkiImporter.ImporterVersion` if the rules changed,
and review the diff of the generated file. If the post-processed Spanish file is withdrawn, the raw wiktextract download (all languages, 23.9 GB) is the
replacement; the importer already filters on `lang_code` `es`, but its entry shape was not checked against the importer.

## The German lexicon (M36, `content/game-de-v1/lexicon/`)

Spike R4 (which source): the German extract above is still published but marked deprecated, like the Spanish one ("will be removed in the near future");
the raw wiktextract data (`kaikki.org/rawdata.html`) is 3.1 GB compressed (24.8 GB), all languages, also deprecated, with no per-language file; the
German Wiktionary extract (310 MB) has German glosses and a different entry shape. The per-language German extract won: pinnable by SHA-256, gender,
plural, case forms and verb forms for the A1 and A2 lemmas, and 1.1 GB. If it is withdrawn, the pinned hash identifies what to keep and the raw file is
the fallback (the importer filters on `lang_code`, but its entry shape was not checked against the importer).

```bash
scripts/fetch-lexicon-sources.sh            # now also kaikki-de and frequency-de-50k
dotnet run --project tools/Vocaterra.Nlp.ContentBuilder -- import-lexicon \
  lexicon-sources/kaikki.org-dictionary-German.jsonl content/game-de-v1/lexicon/wiktionary-de.json --language de \
  --frequency lexicon-sources/de_50k.txt --top 8000 \
  --sidecar content/game-de-v1/lexicon/wiktionary-de.source.json
```

`--language de` selects the German profile (`GermanKaikkiProfile`, importer version `de-1.0.0`); the default is the Spanish profile, whose output is byte
for byte what it was before the profiles (M36 proved it on the full Spanish file: the importer before and after, same input, `cmp` identical). The
German entries use these `LexicalFeatures`: `gender` `m f n`; `case` `nom acc dat gen`; `number`; `declension` (for nouns and adjectives `strong weak mixed`,
for verbs `strong weak irregular`); `particle` (the separable prefix: `auf` for `aufstehen`, also on the finite forms); `verbForm` (`inf`, `part1`,
`part2-haben`, `part2-sein`: the Perfekt auxiliary is part of the participle's form). The present, the Präteritum, the Konjunktiv II (as `pret`, mood
`subj`) and the imperative of a verb are imported. Forms are lower case (the lexicon's key folds the case, and the frequency list is lower case).
Decisions: a sense that is "form of" another word is an inflected form only when its tags name a case, number, person or tense (so the agent noun `Lehrer`
and the feminine `Lehrerin` are lemmas); the personal-pronoun table is not read (Wiktionary hangs the whole table on every pronoun and tags `du`, `ihr`
and `Sie` as unrecognised), so a pronoun is its head word and `mich`, `dir`, `dich` are pronouns of their own with their case.

The file: top 8 000 by frequency (7 707 distinct lemmas in the file: the top N counts lemma spellings, and `Essen` and `essen` become one lemma once lower-cased), 108 539 entries (nouns 4 094 lemmas / 40 116 entries, verbs
2 399 / 47 124, adjectives 1 012 / 20 389, adverbs 514, interjections 108, pronouns 142 / 151, prepositions 58, conjunctions 52, articles 2 / 21), 13.4 MB
(the gate is 15 MB), `Lexicon.Load` of the text read beforehand 240 to 250 ms median (Release, one warm-up, five loads; the gate is 300 ms; the top 9 000
is 15.1 MB and about 270 ms, so 8 000 was chosen). Two imports of the full source give identical bytes (`cmp`); `GermanLexiconImportTests` does it on a
fixture. `GermanGeneratedLexiconTests` spot-checks 53 nouns and 30 verbs of the committed file. The folder has no manifest: it becomes the German bundle in T06.

Known gaps, all in the source or by design: no forms table for `ein` in the source; no comparatives or superlatives; reflexive verbs are skipped (their head
word has two words); no Konjunktiv I and no compound tenses; personal pronouns carry no person or number; Wiktionary lists variants the file keeps
(`Mutter` also has the plural `Muttern` of the nut, `Mann` the unit plural `Mann`, `Tag` a plural `Tags`, `aufstehen` and `fahren` both auxiliaries, `Tee` both
genders); a mass noun has no nominative plural (`Obst`, `Fleisch`).

## What the importer keeps and skips (importer version 1.1.0)

Each skip is counted by reason in the sidecar (`report.skippedByReason`). Of the 811 049 Spanish entries, 90 291 produced lexicon entries.

| Skipped | Count | Why |
| --- | --- | --- |
| pages that only inflect another word (`tengo`: "first-person singular present indicative of `tener`"), also when the head template says "noun form" or "verb form" | 687 724 | the lemma's own forms table lists the form; the M12 importer made each of them a lemma |
| multi-word expressions and affixes (14 306); names, phrases, suffixes, prefixes and other parts of speech (11 489) | 25 795 | not words to teach, or not in the schema |
| alternative spellings, abbreviations, clippings (`alt_of`) | 4 843 | the main entry has the word |
| every sense obsolete, archaic, dated, nonstandard, gender-neutral (`amigue`), a neologism or a misspelling | 1 165 | |
| lemmas a hand-authored lexicon defines (non-verbs) | 357 | the hand-authored readings stand |
| words spelled like a hand-authored function word (`la` the note, `si` the noun) | 151 | they would corrupt common sentences |
| numerals | 250 | stay hand-authored (the lemma of a numeral is a digit string) |
| head words with anything but letters | 212 | |
| verbs whose head word is not an infinitive | 178 | Wiktionary files "only used in se persona" as a verb |
| names of letters (`e` with the plural `es`, `te`, `ese`) | 67 | they share their spelling with frequent function words |
| articles | 16 | the analyzer reads the lemmas `el` and `un` |

Kept and not imported at form level: voseo, alternative, misspelled, proscribed, uncommon, dialectal and clitic-combined forms, negative imperatives, the
future subjunctive and the imperfect subjunctive in `-se`. A noun that exists only in the plural has a plural head word. After the read, 164 noun lemmas
that are only the plural of another noun (`diamantes`, `corazones`, the card suits) are dropped. Determiners are mapped to adjectives, as before.

## The frequency filter

A lemma's frequency is the sum of the counts of its distinct forms in `es_50k.txt` (case-folded, accents kept: `él` is not `el`; a form shared by two
entries of one lemma counts once, a form shared by two lemmas counts for both). The top N lemmas are kept, ties broken by ordinal lemma order; with N
given, a lemma with no form in the list is dropped (64 578 of 80 571 considered), and so is a lemma whose own head word is not in the list (1 764): a rare
verb such as `perar` would otherwise rank in the top 40 because its first person is `pero`. 15 993 lemmas have a frequency; the 3 000th has 6 262
occurrences. See the ADR for what this rule cannot do (`casar` ranks 21st because `casa` is its third person).

## Merge policy

`ContentLoader` unions every `lexicon/*.json` except `*.source.json` sidecars. Entries with the same form, lemma, part of speech and features are one
reading and are stored once; entries that differ in any of the four all stay, and the analyzer reads a token as every one of them; the order and names of
the files do not matter (tests: `LexiconLoadingTests`, `LexiconMergeTests`). The loader also checks that a file marked `wiktionary-import` is marked so in
`provenance.json` and has its sidecar, and that the sidecar's entry and lemma counts match the file.

Overlap in the shipped bundle (`lexicon-report content/game-v1`):

| Pair | Shared lemmas | Shared forms | Identical readings (stored once) | Shared forms with different readings |
| --- | --- | --- | --- | --- |
| `seed.json` / `wiktionary-es.json` | 32 | 509 | 421 | 172 |
| `game.json` / `wiktionary-es.json` | 3 | 41 | 39 | 9 |
| `seed.json` / `game.json` | 0 | 2 | 0 | 2 |
| `world.json` / `wiktionary-es.json` | 1 | 3 | 0 | 3 |
| `supplement.json` / the others | 1 and 2 | 0 | 0 | 0 |

The shared lemmas are verbs: the import defers to the hand-authored nouns, adjectives and function words, and adds the missing tenses to the hand-authored verbs.
Without that deference `el espada` (the matador) would sit next to `la espada` and `un espada` would no longer be a gender mistake.

## Measurements

Machine: 16 logical cores, .NET 10.0.402, `dotnet test --no-build`, the baseline built from the commit before this milestone in a separate directory and timed
interleaved with the new tree. Times are wall-clock seconds.

| Measure | Before (baseline) | After, 3 000 lemmas (kept) | After, 5 000 lemmas | Gate |
| --- | --- | --- | --- | --- |
| Generated lexicon file | none (hand-authored files: 125 343 bytes) | 4 718 227 bytes (4.5 MiB) | 6 627 574 bytes (6.3 MiB) | < 15 MB: pass |
| Entries / lemmas, merged | 1 268 / 404 | 41 078 / 3 372 | 57 706 / 5 372 | |
| Bundle load with hash verification, first load in a fresh process, debug build | 8 to 13 ms | 246 to 267 ms | 360 to 373 ms | < 300 ms |
| same, release build | 8 to 9 ms | 217 to 232 ms | 333 to 354 ms | < 300 ms: 3 000 pass, 5 000 fail |
| Bundle load, repeated in one process, median (debug / release) | 8 / 7 to 8 ms | 148 to 213 / 114 to 172 ms | 200 to 207 / 135 to 203 ms | |
| Whole test suite, `dotnet test --no-build` (3 runs each) | 20.6, 20.5, 21.0 | 24.6, 25.5, 25.3 (+21 %) | 24.5, 25.7, 26.1 (+23 %) | at most +25 %: pass |
| Fuzz project alone (3 runs each) | 17.5, 17.1, 18.2 | 20.3, 20.2, 18.7 (+12 %) | 20.8, 20.3, 20.9 | < 60 s: pass |
| Tests run | 2 232 (+5 skipped) | see the report of the milestone | | |

3 000 lemmas is kept: it holds every gate, 5 000 fails the load-time gate (and has no margin on the suite-time gate). The suite time is the sum of many
changes: the wall time of the suite is its slowest project (the fuzz project), and the fuzz project's CPU time went from 68 s to 92 s.

What was done to keep the gates, none of it weakening an oracle: the lexicon reader and index were rewritten for speed (a `Utf8JsonReader`
reader, runs instead of lists in the index, the index of regularized forms built once per lexicon instead of once per `MorphologyGenerator`);
`ContentFiles.Digest` hashes a file that is already canonical without encoding a copy; the fuzz generators index the lexicon instead of
scanning it (a histogram pre-filter replaces 10 000 edit-distance computations per non-word); the oracle that loads 500 damaged copies of the bundle uses
the opt-in `LexiconCache` and four threads, derives each copy's manifest from the committed one, and draws structural damage from the small lexicon files
(the 4.7 MB generated file has the same schema; a flipped byte still reaches it); `TempBundle` in the content tests is the light copy unless a test is
about the imported lexicon.

### Homographs: the cost of the vocabulary, and what M18 did about it

Before M18 the analyzer read each word as everything it can be. The fuzz generators left out the words that would be misread and counted them; the counts are
printed by `CorrectSentenceFuzzTests` (`dotnet test tests/Vocaterra.Nlp.Fuzzing.Tests --logger "console;verbosity=detailed"`). M18 added the part-of-speech resolver
(`ReadingResolver`, described in [pipeline.md](pipeline.md#4-linguistic-analysis-d0)) and took the generators' exclusions out, so the last column is the same table
after it:

| | Before the lexicon | With the lexicon (M17a) | With the resolver (M18) |
| --- | --- | --- | --- |
| Forms of the merged lexicon with more than one reading | 89 of 1 179 (7.5 %) | 7 216 of 32 494 (22.2 %) | same |
| Forms read under more than one lemma | 19 | 1 035 | same |
| Nouns the generators can use | 154 | 1 583 | 1 880 |
| ... of which also a verb form (finite or infinitive) | 3 (2 %) | 339 (21 %) | 379 (20 %), drawn like any other noun |
| ... of which also a noun of the other gender | left out | left out | 136, drawn; no gender mistake is built on them |
| Nouns left out: the form is also the other gender | 3 | 347 | 0 |
| Nouns left out: no noun reading of the generated form (the lemma has an entry for each gender, the form only one) | n/a | n/a | 17 |
| Nouns left out: a form shared with another noun lemma | 0 | 61 | 94 |
| Nouns left out: no distinct plural | 5 | 16 | 16 |
| Nouns left out: spelled like an article, numeral or determiner | 0 | 2 | 2 |
| Sentences skipped as `verb-form-homograph` (per 2 000 recipes of one oracle) | 0 | 1 to 7 | 0 to 1 (after every re-roll) |
| Sentences skipped as `function-word-homograph` | n/a | none | none |

A fifth of the usable nouns is also a verb form (`cuenta`, `trabajo`, `canto`), and one form in five has several readings. The numbers in the last column
count a lexeme once per lemma, and the 94 nouns left out for a shared form are ones where two noun lemmas have the same spelling (`ratos`), which is a homograph
between two nouns and not between parts of speech.

## Analyzer fixes found by the fuzz oracles

The oracles drew thousands of lemmas for the first time and found four weaknesses of the analyzer's first-reading logic, fixed in `src/Vocaterra.Nlp/Language`
with regression tests in `tests/Vocaterra.Nlp.Tests/Language/Analysis/AmbiguousReadingTests.cs`: (1) `tú habla` passed the person check because `habla` is also the imperative of
`hablar`, so a subject pronoun is now checked against the declarative readings; (2) `ella está alta` was corrected to `eres` because the first reading of
`está` is the imperative; (3) `ella es ocupada` passed because `ocupada` is a lemma of its own that sorts before `ocupado`, so the ser/estar class and the
prenominal check consider every adjective reading; (4) `malos corazones` was a number mistake because `corazones` also reads as a singular noun, so an
adjective agrees with a noun that has readings of both numbers unless a determiner fixes the number. The homograph limit itself was fixed in M18.

## Words written without their accent (M26b, CR-013)

`Lexicon.Lookup` is exact first and accent-insensitive only for a form the lexicon lacks (`arbol` gives `árbol`). A plain spelling that is itself a lexicon form (`esta`, `el`,
`tu`, `mi`, `se`, `te`, `si`, `que`, `mas`: 2 514 forms have an accented sibling in the bundle, a few dozen of them function words) therefore hid the accented word, and
`dónde esta el arbol` read `esta` as the demonstrative and reported `gender.agree`. `Lexicon.LookupWithAccentVariants` returns what `Lookup` returns plus the readings of the accented
spellings (`AccentVariant`: the spelling and its reading); `Lookup` itself is unchanged, and so are the grammar matcher, the intent stage and the returned tokens, which see the readings of the
spelling as typed. `LinguisticAnalyzer` passes the variants to the reading resolver, whose step 0 (`AccentVariantChoice`) keeps the word as typed unless the neighbouring words contradict every
reading it has: an article with no noun phrase after it (`el` at the end or before a verb is `él`), a demonstrative or possessive with no noun after its adjectives or with an article right after it
(`esta` before `el`, an adverb, a preposition or the end is `está`; `tu` before a verb is `tú`; `mi` at the end is `mí`), a conjunction or preposition at the end (`si` alone is `sí`), an object
pronoun with no verb after it (`no se` is `no sé`, `quiero te` is `quiero té`); and a determiner right after `dónde`, `donde` or `cómo` is the verb when it has one. Open classes (nouns,
verbs, adjectives, adverbs, other pronouns) always fit, so `mas` and `como` stay as typed.

Policy: nothing is reported to the learner. There is no accent error tag (the taxonomy has none, and a player without accents writes every word this way), the analysis only stops
reporting the false errors, and starts reporting the true ones that the old reading hid (`el esta simpatico` is `él está simpático`: `ser_estar`). A recast that is built for another
reason restores the accent on a word the analysis read as accented (`AccentRestoration`, used by `RecastBuilder`), as it already did for a word the lexicon lacks (`arbol` gives
`árbol`); a word read as typed keeps its spelling.

### A typed `n` for `ñ` (M70)

The owner's answer of 2026-11-01 (D-game-22, item 2; QA-language-7): a word typed with `n` for `ñ` is a typing slip, read as a word written without its accent is,
when the spelling with `n` is not itself a word. Before M70 `manana compraré pan` at `lesson-will-buy` gave `vocab.unknown` (expected `mañana`, correctness 0.6),
and on the CLI `manana venderé fruta` at `lesson-will-sell` lost its intent. The fold lives in `Lexicon.Lookup`'s accent-insensitive fallback: after the exact form
and the form with its accents ignored, a spelling that no lexicon form folds to is looked up among the *unmarked spellings* of the lexicon's forms, which the language
pack gives through `ILexiconFolding.UnmarkedSpellings` (default none; the Spanish pack: each `ñ` typed as `n`, accents stripped). The readings come back flagged
accent-insensitive, as `arbol` does, so the analysis, the intent stage and the recast treat `manana` as `mañana`, and `SpellingsIgnoringAccents("manana")` gives
`mañana` for a recast. The index is built once per lexicon, on first use.

How the lexicon is asked whether the plain spelling is a word: first the lexicon itself (a spelling that is a lexicon form, accents ignored, keeps its own readings:
`una` not `uña`, `pena`, `mono`, `suena` and the other forms of `sonar` beside `soñar`, `campana`, `dona`), then a closed list of the Spanish pack
(`TextFolding.IsWordSpelledWithN`) for the words a bundle's lexicon may lack: `ano`, `cana`, `cano`, `canon`, `cono`, `cuna`, `mana`, `mona`, `mono`, `panal`,
`pena`, `pina`, `pino`, `sena`, `tina`, `una` and their plurals, and `ceno`. The game's lexicon lacks `ano`, `cana`, `peña`, `caña` and `moño`, so without the
list `ano` would have been read as `año`; with it `tengo diez anos` keeps its `vocab.unknown`, as before. Accepted: `manana`, `pequeno`, `senor`, `espanol`, `nino`,
`anadio` (`añadió`, an accent left out as well). Graded as before: `ano`/`año`, `pena`/`peña`, `cana`/`caña`, `una`/`uña`, `mono`/`moño`, `suena`/`sueña`. The
answer matcher is unchanged: it keeps `ñ` (`Accents.Strip`), so `manana compraré pan` is a fuzzy match of distance 1 to `mañana compraré pan`, which is
communicated as before. The content lint's coverage check uses `Lookup`, so a content word with `n` for `ñ` counts as covered, as one without its accent already did.

## Words the content uses and the lexicon lacked

The coverage tests (`LexiconCoverageTests`) found `saeta` (an alias in the `items-basic` answer set), `género`, `ayudarte`, `inténtalo` and `mmm` (dialogue lines).
They are in `lexicon/supplement.json`. `se` was added to `cefr/a1.txt`: it counted as covered only because the accent-insensitive lookup read `se` as `sé`.
`cefr-check` for A1 and A2 lists no template word that it did not list before, with the same word or its lemma (A1: 32 to 27 not in the list, A2: 31 to 15).

M66 (2026-11-01) added `ti`, the prepositional pronoun of `tú` (`es para ti`, `tengo pan para ti`), which the import lacks and a learner of the A2 lessons says:
`para ti` gave `vocab.unknown`. It is in `lexicon/supplement.json` as the import holds `mí`: a pronoun with its own lemma and no features, so no check takes it
for a subject.

## Known limits

- The post-processed Spanish file is deprecated by its publisher (see Re-pinning).
- Gender and number tags in Wiktionary are uneven; `GeneratedLexiconTests` spot-checks the paradigms of common words.
- Proper names and multi-word expressions are not imported; profanity is not filtered (recognition only; a blocklist is a one-line follow-up).
- There is no CEFR level per entry; the CEFR lists remain lemma lists for `cefr-check`.
- The OpenSubtitles terms behind the frequency list are the owner's to confirm (ADR 0008).
