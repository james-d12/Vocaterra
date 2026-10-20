# Attribution and licenses

This notice ships with content bundle `v1`. It lists the licenses of the data derived from third parties and of the
machine-learning models the game loads at run time. The hand-authored content in this bundle (sample intents, prompts,
dialogue, NPCs, answer sets, CEFR lemma lists and the hand-authored lexicons `lexicon/seed.json`, `lexicon/game.json` and
`lexicon/supplement.json`) has no third-party text in it; the repository owner still has to choose its license.

## Wiktionary data (CC BY-SA)

`lexicon/wiktionary-es.json` is derived from Wiktionary (https://en.wiktionary.org). It was generated with
`Slg.ContentBuilder import-lexicon` from the Spanish extract of English Wiktionary (wiktextract data of 2026-10-03, from the
dump of 2026-09-02) that kaikki.org publishes at https://kaikki.org/dictionary/Spanish/. It holds the lemmas, the inflected
forms and the gender and number tags of the 3 000 most frequent Spanish lemmas, filtered as `docs/LEXICON.md` describes. The
extraction tool, wiktextract (https://github.com/tatuylonen/wiktextract, Tatu Ylonen), is licensed under the MIT license; the
extracted data is Wiktionary text. The file `lexicon/wiktionary-es.source.json` beside it records the source URLs, the retrieval
date, the SHA-256 and size of every input file, the import parameters and the importer version.

Wiktionary text is available under the Creative Commons Attribution-ShareAlike 4.0 license (CC BY-SA 4.0; older revisions
under CC BY-SA 3.0, https://creativecommons.org/licenses/by-sa/3.0/) and the GNU Free Documentation License. Wiktionary
contributors are credited in each entry's page history at https://en.wiktionary.org. If you use this data in academic work,
kaikki.org asks to cite Tatu Ylonen, "Wiktextract: Wiktionary as Machine-Readable Structured Data", Proceedings of the 13th
Conference on Language Resources and Evaluation (LREC), pp. 1317-1325, Marseille, 20-25 June 2022.

Any file imported this way is marked `wiktionary-import` in `provenance.json` and `"generated_by": "wiktionary-import"` in the
file itself (the loader refuses a bundle in which the two disagree). Such files and works derived from them must be shared under
CC BY-SA 3.0 or 4.0 with this notice kept.

## Word frequencies (OpenSubtitles, CC BY-SA)

Which lemmas `lexicon/wiktionary-es.json` contains was decided with a word frequency list: `es_50k.txt` of
https://github.com/hermitdave/FrequencyWords (Hermit Dave; commit 525f9b560de45753a5ea01069454e72e9aa541c6, file
`content/2018/es/es_50k.txt`). The repository states "MIT License for code. CC-by-sa-4.0 for content". The list counts the words of
the OpenSubtitles2018 corpus (http://opus.nlpl.eu/OpenSubtitles2018.php; subtitles from https://www.opensubtitles.org, collected
and aligned by Pierre Lison and Jörg Tiedemann; P. Lison and J. Tiedemann, "OpenSubtitles2016: Extracting Large Parallel Corpora
from Movie and TV Subtitles", LREC 2016, and P. Lison, J. Tiedemann and M. Kouylekov, "OpenSubtitles2018: Statistical Rescoring of
Sentence Alignments in Large, Noisy Parallel Corpora", LREC 2018). Only the ranking was used: no count and no subtitle text is part
of the bundle. The selection of lemmas is a work derived from the list, so it is shared under the same terms (CC BY-SA 4.0) as
the Wiktionary data.

## Models

The models are downloaded, not committed. `models/manifest.json` pins the exact files and hashes; the licenses below are
the ones recorded there.

| Model id | Role | License | Source |
| --- | --- | --- | --- |
| `vosk-model-small-es-0.42` | constrained speech recognition | Apache-2.0 | https://alphacephei.com/vosk/models/vosk-model-small-es-0.42.zip |
| `sherpa-onnx-nemo-parakeet-tdt-0.6b-v3-int8` | open speech recognition | CC-BY-4.0 | https://github.com/k2-fsa/sherpa-onnx/releases/download/asr-models/sherpa-onnx-nemo-parakeet-tdt-0.6b-v3-int8.tar.bz2 |
| `wav2vec2-xlsr-53-espeak-cv-ft` | phoneme recognition | Apache-2.0 | https://huggingface.co/facebook/wav2vec2-xlsr-53-espeak-cv-ft |
| `multilingual-e5-small` | sentence embeddings | MIT | https://huggingface.co/intfloat/multilingual-e5-small |
| `kokoro-multi-lang-v1_0` | speech synthesis | Apache-2.0 | https://github.com/k2-fsa/sherpa-onnx/releases/download/tts-models/kokoro-multi-lang-v1_0.tar.bz2 |

The NVIDIA Parakeet model requires attribution to NVIDIA under CC-BY-4.0. The Kokoro archive also contains espeak-ng
data; the licensing of that phonemizer is an open item for an architecture decision record.
