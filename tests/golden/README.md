# Golden learner audio

The golden set is a small collection of recorded learner utterances with hand-written annotations. `tools/Vocaterra.Nlp.Bench` runs a speech
recognizer over it and reports word error rate, learner-error preservation and latency. It exists to answer one question with real
data: does the recognizer keep the mistakes a learner makes, so the grader can see them?

## What is committed

| Path | Committed | Contents |
| --- | --- | --- |
| `golden/README.md` | yes | this protocol |
| `golden/manifest.jsonl` | yes | one JSON object per clip: the annotations, never the audio |
| `golden/audio/*.wav` | **no** (gitignored) | the recordings; they stay on the machine that made them |

The three entries committed today are **synthetic**. Their `synth` clips are one-second tones that `tests/Vocaterra.Nlp.Golden.Tests` writes into
its own test output directory so the manifest and the benchmark plumbing can be tested; no file is written to `golden/audio/`, and a
tone does not recognize as words. Replace or extend them with real recordings only on your own machine.

## Manifest schema

One JSON object per line (JSON Lines, UTF-8, LF). Property names are camelCase and every property is required, except that
`expectedIntent` may be `null`.

```json
{"clip":"audio/synth-offer-items-open-1.wav","promptId":"offer-items-open","speaker":"synth","l1":"en",
 "expectedTranscript":"yo tiene dos espada",
 "expectedErrors":[{"tag":"conj.person","actual":"tiene"},{"tag":"noun.number","actual":"espada"}],
 "expectedIntent":"offer_item"}
```

| Property | Meaning |
| --- | --- |
| `clip` | Path of the WAV file, relative to the directory of `manifest.jsonl` (or to `--audio-dir`). Use `/` as the separator. |
| `promptId` | A prompt id from the content bundle (`content/v1/prompts/prompts.json`). It selects the answer set and the default recognition mode. |
| `speaker` | An opaque label such as `s01`. Never a name, never anything that identifies the person. |
| `l1` | The speaker's first language as a language code (`en`, `de`, ...). |
| `expectedTranscript` | The words actually spoken, **mistakes included**. This is what a perfect recognizer returns, not the corrected sentence. |
| `expectedErrors` | The annotated learner errors: `tag` is a taxonomy tag (`conj.person`, `noun.number`, `gender.agree`, ...) and `actual` is the wrong word or words as spoken. Use `[]` for a clean utterance. |
| `expectedIntent` | The intent the utterance should resolve to, or `null` for prompts without intents (quizzes). |

## Recording protocol

1. **Consent first.** Participation is opt-in. Before recording, the speaker reads the consent sentence aloud on the clip's own
   recording or agrees to it in writing:
   *"Acepto que esta grabación se use localmente para probar el reconocimiento de voz y que no se comparta."*
   (I agree that this recording is used locally to test speech recognition and is not shared.) Recordings made without consent are deleted.
2. **Push-to-talk.** Record one utterance per clip: press the key, speak, release. Do not leave the microphone open, and do not edit
   the audio afterwards (no trimming, no noise removal); the audio front-end does its own trimming and gain.
3. **Format.** 16 kHz, mono, 16-bit PCM WAV. Other rates and stereo are accepted by the front-end and converted, but record at 16 kHz
   mono so the benchmark measures the recognizer and not the resampler.
4. **Say what a learner would say.** For each prompt, record the natural learner answer, including its mistakes; the annotation
   records them. Say each prompt several times with different speakers, a quiet room and an ordinary headset or laptop microphone.
5. **Naming.** `<speaker>-<prompt>-<n>.wav`, for example `s01-offer-items-open-2.wav`: the speaker label, the prompt id, and a counter
   starting at 1 for that speaker and prompt.
6. **Annotate.** Add the manifest line for every clip. Write `expectedTranscript` from what you hear, not from what the prompt expects.

## Privacy

- Audio is **local only**: `golden/audio/` is gitignored, and nothing in this repository uploads recordings. Do not attach them to
  issues, pull requests or CI artifacts.
- The pipeline never persists audio by default. Replay logs (`--replay-out`) are opt-in and hold recognizer output and verdicts, not audio.
- Speakers are identified by an opaque label only. Keep any key that maps labels to people outside the repository, or do not keep one.
- A speaker can withdraw at any time: delete their clips and their manifest lines.

## Running the benchmark

```
dotnet run --project tools/Vocaterra.Nlp.Bench -- --manifest tests/golden/manifest.jsonl --content content/v1 --models models [--mode constrained|open]
```

For every entry the benchmark runs the recognizer in the prompt's mode (or the forced `--mode`) and reports:

- **WER**: word-level Levenshtein distance over normalized tokens against `expectedTranscript`, per entry and per model and mode as total
  edits over total reference words.
- **Learner-error preservation**: the share of `expectedErrors[].actual` words that are still present in the hypothesis. A constrained
  recognizer that "corrects" `tiene` to `tengo` scores low here even if its WER against the corrected sentence looks good.
- **Latency**: p50 and p95 recognition time in milliseconds (nearest rank). Timing is measured only by this tool, never in `src/`.

Entries whose clip file is missing are listed with the status `missing-clip` and left out of the statistics, so the benchmark also
runs on a checkout without any recordings and prints just its table headers when the manifest has no entries.
