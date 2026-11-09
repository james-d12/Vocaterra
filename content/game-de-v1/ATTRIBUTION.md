# Attribution: the German lexicon (M36)

This folder is the future German content bundle (T06); it has no manifest yet. The repository's root has no `ATTRIBUTION.md`, so the German
sources are credited here, in the form `content/game-v1/ATTRIBUTION.md` uses for the Spanish ones.

## Wiktionary (CC BY-SA)

`lexicon/wiktionary-de.json` is derived from Wiktionary (https://en.wiktionary.org). It was generated with
`Vocaterra.Nlp.ContentBuilder import-lexicon --language de` from the German extract of English Wiktionary (wiktextract data of 2026-10-30, from the
dump of 2026-10-01) that kaikki.org publishes at https://kaikki.org/dictionary/German/. It holds the lemmas, the inflected forms and the gender,
number, case, declension, separable-prefix and participle tags of the 8 000 most frequent German lemmas (the lemmas of the file, not its forms),
filtered as `docs/nlp/lexicon.md` describes. The extraction tool, wiktextract (https://github.com/tatuylonen/wiktextract, Tatu Ylonen), is licensed
under the MIT license; the extracted data is Wiktionary text. The file `lexicon/wiktionary-de.source.json` beside it records the source URLs, the
retrieval date, the SHA-256 and size of every input file, the import parameters and the importer version.

Wiktionary text is available under the Creative Commons Attribution-ShareAlike 4.0 license (CC BY-SA 4.0; older revisions under CC BY-SA 3.0,
https://creativecommons.org/licenses/by-sa/3.0/) and the GNU Free Documentation License. Wiktionary contributors are credited in each entry's page
history at https://en.wiktionary.org. If you use this data in academic work, kaikki.org asks to cite Tatu Ylonen, "Wiktextract: Wiktionary as
Machine-Readable Structured Data", Proceedings of the 13th Conference on Language Resources and Evaluation (LREC), pp. 1317-1325, Marseille,
20-25 June 2022. Works derived from this file must be shared under CC BY-SA 3.0 or 4.0 with this notice kept.

## Word frequencies (OpenSubtitles, CC BY-SA)

The ranking of the lemmas uses the German word list of hermitdave/FrequencyWords (https://github.com/hermitdave/FrequencyWords, commit
`525f9b560de45753a5ea01069454e72e9aa541c6`, `content/2018/de/de_50k.txt`), built from the OpenSubtitles 2018 corpus (https://www.opensubtitles.org).
The list's content is licensed CC BY-SA 4.0, its code MIT. The list itself is not in the repository.
