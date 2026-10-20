# The lexicon: what ships, where it comes from, how to regenerate it

The analyzer knows only the words in the lexicon (every other word is `vocab.unknown`). The bundle `content/v1/lexicon/` holds four files:

| File | Origin | Entries | Lemmas |
| --- | --- | --- | --- |
| `seed.json` | hand-authored: 32 verbs with the present and preterite, articles, pronouns, numerals 1 to 20, about 150 nouns and 80 adjectives | 1 217 | 395 |
| `game.json` | hand-authored: the words the host itself uses (spells, items) | 51 | 9 |
| `supplement.json` | hand-authored: seven words that existing content uses and the other files lack (found by the coverage tests) | 7 | 5 |
| `wiktionary-es.json` | **generated** from Wiktionary by `Vocaterra.Nlp.ContentBuilder import-lexicon`, marked `"generated_by": "wiktionary-import"` | 40 263 | 3 000 |
| `wiktionary-es.source.json` | the **sidecar** of the generated file: sources, hashes, parameters, importer version, counts | - | - |

Merged: 41 078 entries, 32 494 distinct forms, 3 372 lemmas (before: 1 268 entries, 404 lemmas). The generated file has, by part of speech
(lemmas / entries): nouns 1 847 / 4 513, verbs 680 / 33 066, adjectives 701 / 2 399, adverbs 137 / 137, interjections 92 / 92, pronouns 28 / 28, prepositions
15 / 15, conjunctions 13 / 13. A verb brings about 50 forms (infinitive, present, preterite, imperfect, future and conditional indicative, present and
imperfect subjunctive, the imperatives), which is why verbs are 80 % of the entries.

Licence: the generated file and the selection of lemmas are CC BY-SA 4.0 (Wiktionary text; the frequency list's content licence is the same); the notice
is `content/v1/ATTRIBUTION.md`. [ADR 0008](adr/0008-lexicon-sources-and-frequency-filter.md) records why each choice below was made.

## Sources

| Source | URL | Size, SHA-256 | Retrieved |
| --- | --- | --- | --- |
| kaikki.org Spanish extract of English Wiktionary (wiktextract JSONL, post-processed; 811 049 lines, all Spanish) | `https://kaikki.org/dictionary/Spanish/kaikki.org-dictionary-Spanish.jsonl` | 1 054 565 723 bytes, `7b23a5dc004a47b63edc2e79a34b246bbf59d1a3067a1843a5c554c0e252b97c` | 2026-10-20 (the site's file of 2026-10-03, from the enwiktionary dump of 2026-09-02, wiktextract commits `1a05e46` and `e3d6d4e`) |
| hermitdave/FrequencyWords, OpenSubtitles 2018 counts (50 000 lines) | `https://raw.githubusercontent.com/hermitdave/FrequencyWords/525f9b560de45753a5ea01069454e72e9aa541c6/content/2018/es/es_50k.txt` | 658 626 bytes, `dcff3ad4316192f4dc4ff7d26e637c6ff314ef1ca0f3f720c5649018a71056c0` | 2026-10-20 |

Neither file is committed. `lexicon-sources/` is git-ignored, like `models/`. The kaikki.org URL is not versioned (the publisher replaces the file
with every extraction, and marks this post-processed download as deprecated), so the SHA-256 is the pin. The frequency URL names a commit and
cannot change.

## Fetch and regenerate

```bash
scripts/fetch-lexicon-sources.sh            # downloads into lexicon-sources/, verifies size and SHA-256, writes lexicon-sources/sources.json
scripts/fetch-lexicon-sources.sh --check    # verifies what is there, downloads nothing

dotnet run --project tools/Vocaterra.Nlp.ContentBuilder -- import-lexicon \
  lexicon-sources/kaikki.org-dictionary-Spanish.jsonl content/v1/lexicon/wiktionary-es.json \
  --frequency lexicon-sources/es_50k.txt --top 3000 \
  --hand-authored content/v1/lexicon \
  --sidecar content/v1/lexicon/wiktionary-es.source.json

dotnet run --project tools/Vocaterra.Nlp.ContentBuilder -- hash content/v1       # then the steps of "Recipe: add a prompt..." in CONTRIBUTING.md
dotnet run --project tools/Vocaterra.Nlp.ContentBuilder -- validate content/v1
dotnet run --project tools/Vocaterra.Nlp.ContentBuilder -- lexicon-report content/v1
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

Overlap in the shipped bundle (`lexicon-report content/v1`):

| Pair | Shared lemmas | Shared forms | Identical readings (stored once) | Shared forms with different readings |
| --- | --- | --- | --- | --- |
| `seed.json` / `wiktionary-es.json` | 32 | 509 | 421 | 172 |
| `game.json` / `wiktionary-es.json` | 3 | 41 | 39 | 9 |
| `seed.json` / `game.json` | 0 | 2 | 0 | 2 |
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
(`ReadingResolver`, described in [PIPELINE.md](PIPELINE.md#4-linguistic-analysis-d0)) and took the generators' exclusions out, so the last column is the same table
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

The oracles drew thousands of lemmas for the first time and found four weaknesses of the analyzer's first-reading logic, fixed in `src/Vocaterra.Nlp.Language`
with regression tests in `tests/Vocaterra.Nlp.Language.Tests/Analysis/AmbiguousReadingTests.cs`: (1) `tú habla` passed the person check because `habla` is also the imperative of
`hablar`, so a subject pronoun is now checked against the declarative readings; (2) `ella está alta` was corrected to `eres` because the first reading of
`está` is the imperative; (3) `ella es ocupada` passed because `ocupada` is a lemma of its own that sorts before `ocupado`, so the ser/estar class and the
prenominal check consider every adjective reading; (4) `malos corazones` was a number mistake because `corazones` also reads as a singular noun, so an
adjective agrees with a noun that has readings of both numbers unless a determiner fixes the number. The homograph limit itself was fixed in M18.

## Words the content uses and the lexicon lacked

The coverage tests (`LexiconCoverageTests`) found `saeta` (an alias in the `items-basic` answer set), `género`, `ayudarte`, `inténtalo` and `mmm` (dialogue lines).
They are in `lexicon/supplement.json`. `se` was added to `cefr/a1.txt`: it counted as covered only because the accent-insensitive lookup read `se` as `sé`.
`cefr-check` for A1 and A2 lists no template word that it did not list before, with the same word or its lemma (A1: 32 to 27 not in the list, A2: 31 to 15).

## Known limits

- The post-processed Spanish file is deprecated by its publisher (see Re-pinning).
- Gender and number tags in Wiktionary are uneven; `GeneratedLexiconTests` spot-checks the paradigms of common words.
- Proper names and multi-word expressions are not imported; profanity is not filtered (recognition only; a blocklist is a one-line follow-up).
- There is no CEFR level per entry; the CEFR lists remain lemma lists for `cefr-check`.
- The OpenSubtitles terms behind the frequency list are the owner's to confirm (ADR 0008).
