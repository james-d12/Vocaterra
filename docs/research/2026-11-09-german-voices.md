# German voices: spikes R1 and R2 (M43)

**Date:** 2026-11-09. **Area:** game (voice route of D-game-27 item 7). **Question:** can the German NPC voice avoid espeak-ng
(Piper on sherpa-onnx 2.x), and if not, is a German Kokoro export clean enough? Rows R1 and R2 of
[German in the game](2026-11-04-german-in-the-game.md) section 7. A record, not a decision.

## Answers

- **R1: no, not as the owner framed it.** sherpa-onnx 2.0.x has no phonemizer at all: Piper (and Kokoro) models load without
  `espeak-ng-data`, but **text input fails** until the caller supplies phonemes (`OfflineTtsGenerationConfig.PhonemeCodepoints`) or a
  `word || phones` lexicon. `de_DE-thorsten-medium` and `kerstin-low` load and speak on 2.0.1 once phonemes are given, so "runs without
  espeak-ng-data" is true, and the GPL code is gone from the v2 native library. But somebody still has to turn German text into
  espeak-style IPA, and today that is espeak-ng or piper-phonemize, both GPL. A GPL-free German text-to-IPA (for example from the
  German pack's lexicon, unverified) is a new piece of work.
- **R2: licence chain clean, loader not.** `Thorsten-Voice/Kokoro` (Apache-2.0; Kokoro-82M Apache-2.0, kikiri-tts recipe Apache-2.0,
  Thorsten-Voice data CC0) is PyTorch only. `DocXAssist/voices` `de/model.onnx` is an ONNX export of exactly that checkpoint by a third
  party (the DocXAssist app; account 5 weeks old, 0 downloads). Its LICENSE file is Apache-2.0; the "CC BY 4.0" in the card covers only
  the WeSpeaker speaker model in `speaker/`, which we do not need. It does **not** load in sherpa as shipped (`'sample_rate' does not exist
  in the metadata`); after appending seven metadata entries to the file (`sample_rate`, `n_speakers`, `version`, `style_dim`,
  `speaker_names`, `voice`, `has_espeak`) it loads and speaks on 1.13.8 and 2.0.1. Its token vocabulary is identical to sherpa's Kokoro
  `tokens.txt`. Its phonemes also come from espeak-ng (misaki `EspeakG2P`), so it has the **same GPL front-end problem** as Piper.

## Table

20 A1 lines (the brief's), Vosk `vosk-model-small-de-0.15`, resampled to 16 kHz. WER is on 95 words; real-time factor is synthesis
time over audio time, model load excluded, on a busy machine, one thread. "ext phonemes" means the IPA came from the system `espeak-ng`
CLI (`-q --ipa -v de`, clause punctuation re-attached), a scratch stand-in for piper-phonemize, so it is an upper bound on error: the real
phonemizer will differ a little.

| Engine | sherpa | Loads? | Phonemizer | GPL left? | Licence chain | WER | RTF |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Piper `thorsten-medium` | 1.13.8, espeak-ng-data | yes | espeak-ng inside sherpa | yes (D2 private build) | model MIT, data CC0; fine-tuned from the English lessac checkpoint (its data licence not checked) | 4.2% | 0.08 |
| Piper `thorsten-medium` | 2.0.1, text only | loads, **speaks nothing** | none: "no frontend available" | no | same | n/a | n/a |
| Piper `thorsten-medium` | 2.0.1, ext phonemes | yes | external espeak-ng (GPL) | in the sherpa library no, in the pipeline yes | same | 12.6% | 0.08 |
| Piper `kerstin-low` | 1.13.8 | yes | espeak-ng inside | yes | model MIT, data CC0; from the English ryan checkpoint; low quality, 16 kHz | 24.2% | 0.06 |
| Piper `kerstin-low` | 2.0.1, ext phonemes | yes | external espeak-ng | as above | same | 17.9% | 0.06 |
| Kokoro DocXAssist `de` (thorsten) | 1.13.8, patched metadata | yes, after patch | espeak-ng inside | yes | Apache-2.0 chain, third-party export | 5.3% | 0.67 |
| Kokoro DocXAssist `de` (thorsten) | 2.0.1, patched, ext phonemes | yes, after patch | external espeak-ng | as above | same | 3.2% | 0.72 |

Most remaining errors are words Vosk's small model lacks (`Luna`, `Servus`, `Tschüss`) or short-clause pairs; the 1.13.8 `thorsten`
lines are the reference. The RTF figures include contention from other builds. Kokoro is about eight times slower than Piper but still
faster than real time, and German lines are prerendered anyway.

## Things found on the way

- **Moving the repository's pin to 2.x breaks the Spanish Kokoro path silently.** `KokoroSynthesizer` compiles and loads against 2.0.1
  (the `DataDir` and `Lang` properties still exist, ignored with a warning), but `Generate` logs "No lexicon available" and returns an
  empty result: no exception. The Oct 26 `kokoro-multi-lang-v1_0` archive still ships `espeak-ng-data`, no Spanish lexicon. Spanish on 2.x
  needs a Spanish lexicon or an external phonemizer, the same piece of work as German. Nothing was changed in the repository.
- v2 licence: the packages declare Apache-2.0; the native library holds no espeak strings or phondata path, only the "deprecated and
  ignored" warnings. v2.0.0's notes: PR #4055 "Remove espeak-ng and piper-phonemize"; 2.0.1 changed nothing here.
- The Piper archives sherpa publishes (Dec 2025) are unchanged v1-style: they still bundle `espeak-ng-data`.

## Recommendation (for the owner)

The evidence points to the **third branch of D-game-27 item 7**: Piper (or the DocXAssist Kokoro German, which sounds measurably as
intelligible and has a cleaner base) **under D2's private-build terms on the repository's current sherpa pin 1.13.8**. Neither of the
first two branches holds as worded: R1's condition ("runs without espeak-ng-data") is met only if we also supply a GPL-free German
phonemizer, and R2's export is licence-clean but is not a sherpa-ready file and needs the same espeak phonemes. If the owner wants the
GPL-free path, the one piece of work that serves Spanish, German and any pin bump is a **text-to-IPA step of our own** (a lexicon in the
German pack, with the model's espeak phone set as the target), a spike of its own to be sized before T08. Between the voices: Thorsten
Piper `medium` is the safer pick today (a published, sherpa-supported archive, fast); `kerstin-low` is clearly worse. **Listen first**:
the WER says intelligible, not pleasant.

## Where things are

Renders (one folder per engine and pin, `01.wav` to `20.wav`, `timings.tsv` with text, phonemes, duration and times):
`~/.cache/vocaterra/voice-spikes/renders/`
(`piper-thorsten-medium_sherpa-1.13.8_espeak-ng-data`, `piper-thorsten-medium_sherpa-2.0.1_ext-phonemes`,
`piper-kerstin-low_sherpa-*`, `kokoro-docxassist-de-thorsten_sherpa-*`). Models and the patched Kokoro file are beside it in
`~/.cache/vocaterra/voice-spikes/` (about 750 MB in all). The harness (a console per sherpa version, the Vosk scorer) is in
`/tmp/claude-1000/m43/` and is not kept in the repository.
