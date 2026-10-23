# Attribution and licenses

This notice ships with content bundle `game-v1`. It lists the licenses of the data derived from third parties and of the
machine-learning models the game is built to load at run time. The hand-authored content in this bundle (intents, prompts,
dialogue, agents, answer sets, CEFR lemma lists and the hand-authored lexicons `lexicon/seed.json`, `lexicon/game.json`,
`lexicon/supplement.json` and `lexicon/world.json`) has no third-party text in it.

The generic intents (`confirm`, `deny`, `farewell`, `thank`), the feedback and acknowledgement dialogue pools (`feedback.*`, `answer_accepted`,
`farewell_reply`) and the feedback policy were copied from the sample bundle `content/v1` of the language core (Vocaterra.NLP, Apache-2.0), in
places reworded to pass this bundle's A1 check, and stay credited to that sample under its Apache-2.0 licence. Since decision D-game-4
(`docs/DECISIONS.md`, decided 2026-10-21) this bundle diverges from the sample: the game owns every file, nothing is re-synced when the
core's sample changes, and a milestone that wants another piece of the sample copies only that piece and records it in `provenance.json`.
The sample's blacksmith agent, its spells, items and trade intents, answer sets, prompts and effects were copied in the first milestones and
left the bundle in S5 (they remain in the core's sample). Everything added since is written for the game and has no third-party text in it:
the guide (`agents/guide.json`, the intents `ask_directions`, `ask_what` and `ask_help`, the prompts `guide-open` and `guide-review`, and the
guide's dialogue pools), `answer-sets/places-basic.json`, `lexicon/world.json`, the noun catalogue `game/nouns.json`, the intent `name_object`
and the guide's name pools (P4), and, from S5, the shopkeeper and the villager (`agents/shopkeeper.json`, `agents/villager.json`), the intents
`request_item` and `dont_know`, the answer set `answer-sets/supplies.json`, the prompts `stall-need`, `stall-offer`, `villager-open` and
`guide-offer`, and their dialogue pools. The Spanish of the phrases the learner is taught is listed in `content/PHRASES.md` (beside this
bundle).

The code and the hand-authored content of this repository, the game and the language core alike, are licensed under the Apache License,
Version 2.0 (`LICENSE` and `NOTICE` at the repository root). The third-party data below keeps the licence given for it.

## Wiktionary data (CC BY-SA)

`lexicon/wiktionary-es.json` is derived from Wiktionary (https://en.wiktionary.org). It was generated with
`Vocaterra.Nlp.ContentBuilder import-lexicon` from the Spanish extract of English Wiktionary (wiktextract data of 2026-10-03, from the
dump of 2026-09-02) that kaikki.org publishes at https://kaikki.org/dictionary/Spanish/. It holds the lemmas, the inflected
forms and the gender and number tags of the 3 000 most frequent Spanish lemmas, filtered as the core's `docs/LEXICON.md` describes. The
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

The models are downloaded, not committed. The core's `nlp/models/manifest.json` (the sibling folder of `game/` in the repository) pins
the exact files and hashes; the licenses below are the ones recorded there. The prototype loads none of these files yet: its text
loop runs on the core's scripted recognizer and stand-in models. The table is the licence record for the models the game will load
when speech and the guide's voice land (milestones P5 and P8 in `IMPLEMENTATION_PLAN.md`).

| Model id | Role | License | Source |
| --- | --- | --- | --- |
| `vosk-model-small-es-0.42` | constrained speech recognition | Apache-2.0 | https://alphacephei.com/vosk/models/vosk-model-small-es-0.42.zip |
| `sherpa-onnx-nemo-parakeet-tdt-0.6b-v3-int8` | open speech recognition | CC-BY-4.0 | https://github.com/k2-fsa/sherpa-onnx/releases/download/asr-models/sherpa-onnx-nemo-parakeet-tdt-0.6b-v3-int8.tar.bz2 |
| `wav2vec2-xlsr-53-espeak-cv-ft` | phoneme recognition | Apache-2.0 | https://huggingface.co/facebook/wav2vec2-xlsr-53-espeak-cv-ft |
| `multilingual-e5-small` | sentence embeddings | MIT | https://huggingface.co/intfloat/multilingual-e5-small |
| `kokoro-multi-lang-v1_0` | speech synthesis | Apache-2.0 | https://github.com/k2-fsa/sherpa-onnx/releases/download/tts-models/kokoro-multi-lang-v1_0.tar.bz2 |

The NVIDIA Parakeet model requires attribution to NVIDIA under CC-BY-4.0. The Kokoro archive also contains espeak-ng
data; the licensing of that phonemizer is open with the GPL-3.0 native libraries (the core's decision D2), and the game does not
distribute a build that includes them until D2 is settled.

## Speech recordings (Kokoro model output)

The files `speech/*.wav` (and `speech/*.slow.wav`) are the NPCs' voices, rendered with `kokoro-multi-lang-v1_0` (Kokoro, Apache-2.0, through
sherpa-onnx) by `scripts/prerender-speech.sh` (S9). They are model output, not recordings of a person: the voices are `ef_dora` (speaker 28, the
guide, and the word cards and model answers), `em_alex` (speaker 29, the shopkeeper) and `em_santa` (speaker 53, the villager), at speed 1.0 and,
for the `.slow` files, 0.8. They are listed in `provenance.json` with `generatedBy: model-output` (the core's kind for host-owned files a model rendered), the model
`kokoro-multi-lang-v1_0`, the voice, the speaker id and the line they render. They are shipped under the private-build terms of the core's decision D2: the Kokoro archive's espeak-ng data
is GPL-3.0 code, so a distributed build with these files waits on D2 as well. The Spanish text they say is the bundle's own (the lines of
`dialogue/dialogue.json`, the prompts and `content/PHRASES.md`); the owner's listening check is `docs/SPEECH_CHECKLIST.md`.
