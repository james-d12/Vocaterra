# ADR 0008: Where the imported lexicon comes from, and how a frequency list decides what ships

> **Note (M19e, 2026-10-21):** game-side matters in this record, such as the engine choice and the game's scenes, are now decided in the Vocaterra game repository, not in this one. The text below is unchanged.

> **Note (M19a, 2026-10-20):** this record names the project `Slg.*` (and the `SLG_*` variables). Those names are now `Vocaterra.Nlp.*` and `VOCATERRA_*` (the old variable names are read for one release); see [ADR 0009](0009-core-and-game-boundary.md). The text below is unchanged.

- Status: accepted
- Date: 2026-10-20
- Deciders: the implementation plan (`IMPLEMENTATION_PLAN.md` section 7, milestone M17a); the repository owner confirms the licensing conclusion

This ADR records engineering decisions and the evidence behind them. It is not legal advice; the repository owner has to get the licensing
conclusion confirmed before release, as `docs/archive/nlp/history/HIGH_LEVEL_DOCS.md` ("Licensing") already says. The practical recipe is in [docs/nlp/LEXICON.md](../LEXICON.md).

## Context

The bundle understood a scripted slice of Spanish: 1 268 lexicon entries and 404 lemmas, all hand-authored. Any other word is `vocab.unknown`, and
the analyzer skips every grammar check around it. The lexicon loader, the Kaikki importer and the slot-grammar DSL were already catalog-driven, so
widening the vocabulary needed data and two contained tool changes, not a new pipeline. Three questions had to be answered:

1. Which data, under which licence, and how is an import made reproducible when the source file is several gigabytes and replaced by its publisher?
2. How many lemmas, chosen how? An unfiltered Wiktionary import is 80 571 lemmas and 585 151 entries (a 68 MB file); the loader, the fuzz suite and
   the analyzer all scale with the number of entries.
3. What happens when imported readings meet the hand-authored ones the game was built on?

## Decision

### 1. Sources, pinned by hash, fetched and never committed

| Source | Pin | Licence |
| --- | --- | --- |
| kaikki.org Spanish extract of English Wiktionary (wiktextract JSONL, post-processed), `https://kaikki.org/dictionary/Spanish/kaikki.org-dictionary-Spanish.jsonl` | 1 054 565 723 bytes, SHA-256 `7b23a5dc004a47b63edc2e79a34b246bbf59d1a3067a1843a5c554c0e252b97c`, retrieved 2026-10-20 (the site's file of 2026-10-03, from the enwiktionary dump of 2026-09-02) | Wiktionary text: CC BY-SA 4.0 (older revisions 3.0) and GFDL |
| hermitdave/FrequencyWords `content/2018/es/es_50k.txt`, `https://raw.githubusercontent.com/hermitdave/FrequencyWords/525f9b560de45753a5ea01069454e72e9aa541c6/content/2018/es/es_50k.txt` | 658 626 bytes, SHA-256 `dcff3ad4316192f4dc4ff7d26e637c6ff314ef1ca0f3f720c5649018a71056c0`; a commit URL, so immutable | repository README at that commit: "MIT License for code. CC-by-sa-4.0 for content"; counts OpenSubtitles2018 |

`scripts/fetch-lexicon-sources.sh` downloads both into the git-ignored `lexicon-sources/`, verifies size and SHA-256 against the pins written in
the script, and records URL, hash, size and retrieval date in `lexicon-sources/sources.json`. The importer copies those facts into the sidecar
`lexicon/wiktionary-es.source.json` after re-hashing the files it actually read, so the sidecar can only say what the importer saw.

The kaikki.org URL is not versioned: the publisher replaces the file with every extraction, and the page itself marks this post-processed file
"DEPRECATED, will be removed in the near future" (the raw all-languages download is 23.9 GB). The hash is therefore the pin, a mismatch stops the
fetch script with an explanation, and re-pinning is a deliberate edit. The committed lexicon does not depend on the download: it ships in the
bundle, and only regenerating it needs the source.

wiktextract, the extraction tool, is MIT-licensed (its repository README and LICENSE); it is a tool and nothing of it ships. OpenSubtitles is the
corpus behind the frequency list. No count and no subtitle text ships; the list only ranks lemmas. The selection is a work derived from a CC BY-SA 4.0
list and is shared on the same terms as the Wiktionary data (`content/v1/ATTRIBUTION.md`). The terms of the underlying subtitle corpus are the one
licensing point this ADR could not settle from the files: the list's author licenses his counts, not the subtitles. Since only a ranking is used,
the exposure is small, and it is flagged for the owner.

### 2. The importer skips what is not a lemma worth teaching

Measured on the real file (811 049 entries, all `lang_code` `es`): 687 724 pages are inflected forms of another entry ("`tengo`: first-person
singular present indicative of `tener`"), which the lemma's own forms table already lists. The M12 importer would have made each of them a lemma.
Version 1.1.0 of the importer (the constant `KaikkiImporter.ImporterVersion`, recorded in every sidecar) skips, and counts in the sidecar by reason:

- inflected-form pages, including pages whose head template says "noun form" or "verb form" even when they also have a sense of their own (`huevos`);
  alternative spellings, abbreviations and clippings; entries whose every sense is obsolete, archaic, dated, nonstandard, gender-neutral
  (`amigue`), a neologism or a misspelling;
- names of letters (`e` with the plural `es`, `te`, `ese`), which share their spelling with frequent function words and verb forms;
- articles, because the analyzer reads the lemmas `el` and `un` and Wiktionary also has `la`, `lo`, `les` and `unx` as lemmas of their own;
- verb headwords that do not look like an infinitive (Wiktionary files "only used in se persona" as a verb), head words with anything but letters,
  multi-word expressions, numerals (as before);
- forms tagged voseo (`vos-form`), alternative, misspelling, proscribed, uncommon, dialectal, error, combined with clitics, and negative imperatives;
  the future subjunctive (obsolete) and the imperfect subjunctive in `-se` (a literary variant of `-ra`; together a sixth of all verb forms).

A noun that exists only in the plural (`pantalones`) gets a plural head word. A noun lemma that is only the plural of another noun
(`diamantes`, the card suit, next to the plural of `diamante`) is dropped: as a lemma of its own it would also read as a singular, and
"1 diamantes" would no longer be a number mistake.

### 3. Lemma frequency, and who may enter the top N

A lemma's frequency is the sum of the counts, in the list, of its distinct forms (case-folded, accent-sensitive: `él` and `el` are different
words), summed over all its entries of every part of speech. The top N lemmas are kept; ties break by ordinal lemma order; with N given, a
lemma with no form in the list is dropped, and without N nothing is dropped. This is the rule of the plan. One condition was added after looking
at the result of the plain rule: a lemma is eligible only if its **own head word is in the list**. A form that is shared with a frequent word gives
a rare lemma the frequent word's count: without the condition the rare verb `perar` ranks in the top 40 because its first person is `pero`, and
`aforar` ranks high because its subjunctive is `afuera`. The condition removes 1 764 such lemmas at N = 3 000 (`afuera`'s verb, `abajar`, `historiar`, ...)
and costs nothing for real vocabulary, because the head word of a real lemma is in a list of 50 000 words.

What it does not remove, and what any ranking on spelling alone cannot: a lemma whose forms are shared with a frequent word of another lemma still
inherits the count (`casar` ranks 21st because `casa` is its third person, `crear` 15th because `cree` and `creen` are also forms of `creer`, `coser` 30th
because of `cosa`). They are genuine Spanish verbs, so they stay; the rank is inflated, the membership is not wrong. Fractional attribution of a
shared count (split by each lemma's unshared evidence) was considered and rejected for this milestone: it would drop common nouns whose every form is
also a verb form (`casa`, `cuenta`) because the verb has more unshared forms; telling those apart needs part-of-speech data, which is M18. The
spellings of hand-authored function words are taken out of the count by decision 4 (`unir` no longer inherits `un`, `una`, `uno`).

The unit is the lemma string across parts of speech (the schema's lemma is a string, and `Lexicon.Lemmas` is distinct strings), so a kept
lemma brings all its entries (`sobre` the preposition and `sobre` the envelope).

### 4. The hand-authored lexicons win

`import-lexicon --hand-authored <folder>` makes the importer defer to the files the game was written with:

- A lemma that a hand-authored file defines gets no entry from the import, except that verbs still add their forms (the seed has the present and the
  preterite of 32 verbs; the import adds the other tenses, and identical cells are stored once).
- The spelling of a hand-authored article, pronoun, numeral, preposition, conjunction or adverb is reserved, for head words and for inflected
  forms: no Wiktionary noun `la` (the musical note) or `si`, no verb form `una` or `uno` (of `unir`) or `para` (of `parar`).

The reason is concrete. Without the first rule `espada` also became masculine (`el espada`, the matador), `un espada` stopped being a gender mistake,
and the committed golden clip "quiero comprar un espada" lost its `gender.agree`. Without the second, `ayer compré una espada` was reported as a
tense mistake on `una` (read as a present form of `unir`), and `cuál es el precio` as a word-order mistake (`es` read as the plural noun of the letter `e`).
The merge policy of the loader is unchanged (below); this rule keeps conflicting readings out of the generated file instead of weakening the policy.

### 5. Merge policy of the loader

All `lexicon/*.json` files are unioned (`ContentLoader`, tests in `LexiconLoadingTests`): readings with the same form, lemma, part of speech and features
are stored once; readings that differ in any of them all stay and the analyzer reads a token as all of them; file order and names do not matter.
`lexicon/*.source.json` are sidecars, not lexicons. The loader refuses a bundle in which a file marked `generated_by: wiktionary-import` is not
marked so in `provenance.json` (and the reverse), has no sidecar, or has a sidecar whose entry or lemma count differs from the file.
`lexicon-report` prints each file, their overlaps (identical readings, shared forms with different readings) and the homograph numbers.

### 6. Size: 3 000 lemmas

At 3 000 lemmas the file is 4.7 MB (limit 15 MB), there are 41 078 entries in the merged lexicon, the first load of the bundle in a fresh process takes
217 to 267 ms (limit 300 ms), the fuzz suite is green at default depth in about 20 s (limit 60 s), and the whole suite is about 21 % slower (limit 25 %). At 5 000
lemmas the file is 6.6 MB and the suites are green, but the first load takes 333 to 373 ms and the suite time has no margin left, so 3 000 is kept. The
measurements are in [docs/nlp/LEXICON.md](../LEXICON.md). To keep the gates at 3 000
the loader reads the lexicon with a hand-written `Utf8JsonReader` instead of the reflection deserializer and builds its indexes without
intermediate lists, and the fuzz oracle that loads 500 damaged copies of the bundle uses an opt-in cache of the parsed lexicon files (`LexiconCache`)
and four threads; the generators index the lexicon instead of scanning it.

## Consequences

- The lexicon grows from 404 to 3 372 lemmas and from 1 268 to 41 078 entries. A noun is also a verb form in 21 % of the usable nouns
  (339 of 1 583; before: 3 of 154), and 7 216 of 32 494 forms of the merged lexicon have more than one reading (before: 89 of 1 179). The analyzer is not part-of-speech aware, so
  such words trip the person, tense and number checks on correct sentences. The fuzz generators leave those words out of the sentences they build
  and count them; making the analyzer choose is M18. This is the cost of the milestone, and the numbers that decide M18 are in LEXICON.md. *Update:* M18 did it
  (`ReadingResolver`, [PIPELINE.md](../PIPELINE.md#4-linguistic-analysis-d0)), and the generators now draw these words.
- Analyzer fixes that the new words forced (regression tests in `Slg.Nlp.Tests/Analysis/AmbiguousReadingTests.cs`): a subject pronoun is checked
  against the declarative readings of its verb, because an imperative is spelled like the third person (`tú habla` is a person mistake); the copula
  check uses the declarative reading (`está` is also the imperative of `estar`, so the correction of `ella está alta` is `es`, not `eres`); a form
  that is an adjective under two lemmas (`ocupada`, a lemma of its own next to `ocupado`) is classified as ser or estar by all of them; an adjective agrees
  in number with a noun form that is read as singular and as plural, unless a determiner fixes the number.
- Five words that existing content uses and the lexicon lacked (`saeta` in an answer set; `género`, `ayudarte`, `inténtalo`, `mmm` in dialogue
  lines) are in a new hand-authored file `lexicon/supplement.json` (seven entries), found by the new coverage tests. `se` was added to the A1 list: it was
  "covered" only because the lexicon lacked it and the accent-insensitive lookup read it as `sé`.
- A regeneration needs the 1 GB download and about 10 seconds. Two runs give byte-identical files; the importer version, the pins and the
  parameters are in the sidecar.

## Alternatives considered

- **Importing every lemma.** 81 000 lemmas, about 3.5 million entries; loading the bundle would take seconds and the analyzer would read
  almost every common word as five parts of speech.
- **A hand-curated CEFR vocabulary.** The CEFR lists in the bundle are lemma lists for `cefr-check` and no external list is cited; a frequency list is
  reproducible and has a stated licence.
- **Importing closed classes (articles, pronouns, determiners) too.** The articles broke the `el`/`un` convention the analyzer relies on. Determiners
  (`det`, mapped to adjectives as before) are imported; the analyzer treats a few of them as plain adjectives, which is a known limit.
- **Keying on (lemma, part of speech).** It does not separate a rare lemma from a frequent form either; the surface count is the same.
- **Raw wiktextract data instead of the post-processed file.** It is the supported download, but 23.9 GB for all languages and its entry shape was
  not checked against the importer. If the post-processed file disappears, switching is the first thing to try.

## Open items

- Part-of-speech disambiguation in the analyzer (M18), decided on the numbers in LEXICON.md.
- The OpenSubtitles terms (point 1) and the choice of a licence for the repository are the owner's.
- If kaikki.org withdraws the post-processed Spanish file, regenerate from the raw data with a new pin and a bumped importer version.
