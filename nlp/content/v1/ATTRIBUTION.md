# Attribution and licenses

This notice ships with content bundle `v1`. It lists the licenses of the data derived from third parties and of the
machine-learning models the game loads at run time. The hand-authored content in this bundle (sample intents, prompts,
dialogue, NPCs, answer sets, CEFR lemma lists and the seed lexicon) has no third-party text in it; the repository owner
still has to choose its license.

## Wiktionary data (CC BY-SA)

Lexicon files generated with `Slg.ContentBuilder import-lexicon` are derived from Wiktionary (https://en.wiktionary.org),
extracted with wiktextract (https://github.com/tatuylonen/wiktextract) and published as JSONL by kaikki.org.
Wiktionary text is available under the Creative Commons Attribution-ShareAlike 4.0 license (CC BY-SA 4.0; older revisions
under CC BY-SA 3.0, https://creativecommons.org/licenses/by-sa/3.0/) and the GNU Free Documentation License. Wiktionary
contributors are credited in each entry's page history at https://en.wiktionary.org.

Any file imported this way is marked `wiktionary-import` in `provenance.json` and `"generated_by": "wiktionary-import"`
in the file itself. Such files and works derived from them must be shared under CC BY-SA 3.0 or 4.0 with this notice kept.
No imported file is part of the sample bundle yet: `lexicon/seed.json` and `lexicon/game.json` are hand-authored.

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
