# Replay logs

Every graded interaction can be written to a replay log and checked again later, on any machine, without the speech models.
This is how the determinism promise ("same clip, host state and content version give the same verdict") is tested instead of
assumed. A replay that differs from its recording fails and says which values changed.

- [What a log is](#what-a-log-is)
- [File format](#file-format)
- [The envelope, field by field](#the-envelope-field-by-field)
- [A real example](#a-real-example)
- [Writing a log](#writing-a-log)
- [Replaying a log](#replaying-a-log)
- [A model-backed log](#a-model-backed-log)
- [Bare records](#bare-records)
- [What is and is not stored](#what-is-and-is-not-stored)

## What a log is

A replay log is a text file with one JSON object per line (JSON Lines). There are two kinds of line:

| Kind | Type | Contents | Replayable by `Vocaterra.Nlp.Cli replay` |
| --- | --- | --- | --- |
| **Envelope** | `Vocaterra.Nlp.Pipeline.ReplayEnvelope` | the core record, the recognizers' outputs, the conversation context and the full pipeline result | yes |
| **Bare record** | `Vocaterra.Nlp.Core.Replay.ReplayRecord` | clip hash, prompt id, host state hash, version stamp and verdict only | no (see [Bare records](#bare-records)) |

The CLI writes envelopes. The core record inside an envelope is the contract from `IMPLEMENTATION_PLAN.md` section 4:

```csharp
public sealed record ReplayRecord(string ClipHash, string PromptId, string HostStateHash, VersionStamp Versions, Verdict Verdict);
```

The envelope adds what is needed to re-run the evaluation with no model and no recording:

```csharp
public sealed record ReplayEnvelope(ReplayRecord Record, AsrResult Asr, PhonePosteriors? Posteriors, ConversationContext Context, PipelineResult Result);
```

## File format

- UTF-8 without a byte-order mark, one entry per line, every line (including the last) ends in a single LF.
- Each line is `CanonicalJson` (`Vocaterra.Nlp.Core.Determinism.CanonicalJson`): property names in camelCase, properties in declaration order,
  every dictionary with its keys sorted ordinally, enums as strings, numbers in their shortest round-trip form (`1`, `0.5`),
  no whitespace. Non-ASCII text such as `¿` is written as is, not as `\u00bf`.
- Equal inputs therefore give byte-identical lines. A log written twice from the same run can be compared with `cmp`.
- The CLI appends: writing to an existing file adds lines, so one log can hold many utterances. Blank lines are ignored when reading.
- Reading is strict. A line that is not valid JSON, or lacks `record`, `asr`, `context` or `result`, stops the replay with an
  error that names the line number.

## The envelope, field by field

| Path | Type | Meaning |
| --- | --- | --- |
| `record.clipHash` | string | SHA-256 (hex) of the 16 kHz mono 16-bit samples of the clip, little-endian. Identifies the audio without storing it. A run with `--transcript` and no `--wav` has an empty clip, whose hash is the SHA-256 of zero bytes (`e3b0c442...b855`). |
| `record.promptId` | string | The prompt that was asked (an id from `content/v1/prompts/prompts.json`). The replayer looks it up in the content bundle it is given. |
| `record.hostStateHash` | string | `CanonicalJson` hash of the conversation context (seed, agent, state, time, learner, repeat counts). Equals `context.hostStateHash`. |
| `record.versions.contentHash` | string | The content bundle hash (`content/v1/manifest.json`, `bundleHash`). |
| `record.versions.modelHashes` | map | One entry per model that took part, sorted: `asr`, `phoneme`, `embedding`, `tts`. A scripted recognizer reports its own label (`scripted-transcript`). |
| `record.verdict` | object | The `Verdict`: intent, slots, communicated, correctness, errors, pronunciation, confidence, resolvedBy, versions. |
| `asr` | object | What the word recognizer returned: transcript, n-best list, word timings, confidence, model hash, recognition mode. During a replay it is served back by a `ReplayWordRecognizer` keyed by the clip hash. |
| `posteriors` | object or null | What the phoneme recognizer returned (`vocabulary`, `logProbs`, `frameCount`, `frameSeconds`), or `null` when pronunciation scoring did not run. Served back by a `RecordedPhonemeRecognizer`. |
| `context` | object | The `ConversationContext` the utterance was evaluated in: world seed, stored time (`now.unixSeconds`), agent id and state (its `interactionCounter` selects the random stream), the learner model before the utterance, the repeat counts per error tag, and the host state hash. |
| `result` | object | The full `PipelineResult`: `verdict`, `decision` (agent step, updated learner, FSRS rating, feedback plan), `response` (the lines the agent says), `effects`, `repair` and `replay` (the same record as `record`). |

## A real example

This line was produced by running the CLI on the worked example from the high-level design ("yo tiene dos espada" at the
blacksmith). No model is involved: the word recognizer is scripted from `--transcript`, so `asr.modelHash` is
`scripted-transcript`.

```bash
dotnet run --project tools/Vocaterra.Nlp.Cli -- eval --transcript "yo tiene dos espada" --prompt offer-items-open --content content/v1 --seed 42 --now 1760000000 --replay-out replay.jsonl
```

The command prints the verdict JSON, then the agent lines, then a decision summary:

```text
{"intent":"offer_item","slots":{"item":"espada","qty":"2","verb":"tener"},"communicated":true,"correctness":0.5,"errors":[{"tag":"conj.person","span":{"start":3,"length":5},"expected":"tengo","actual":"tiene"},{"tag":"noun.number","span":{"start":11,"length":6},"expected":"espadas","actual":"espada"}],"pronunciation":[],"confidence":1,"resolvedBy":"Grammar","versions":{"contentHash":"cedb97c4d068221c58b86c4a69dc23fa081d6eda8ecb96dc068361bc5c3afad1","modelHashes":{"asr":"scripted-transcript"}}}
agent: Ah, ¿tienes dos espadas? ¡Muy bien!
agent: ¡Dos espadas! Trato hecho.
decision: rating Hard, learner skills 3, feedback items 2
```

`replay.jsonl` now holds one line of 3,705 bytes. Here it is, pretty-printed for reading (the file itself has no
whitespace between tokens):

<details>
<summary>The complete envelope (257 lines when pretty-printed)</summary>

```json
{
  "record": {
    "clipHash": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
    "promptId": "offer-items-open",
    "hostStateHash": "6b9e4914018ba5c025378256d2bc473fbab48e4db7cbf773baae073e42d76b21",
    "versions": {
      "contentHash": "cedb97c4d068221c58b86c4a69dc23fa081d6eda8ecb96dc068361bc5c3afad1",
      "modelHashes": {
        "asr": "scripted-transcript"
      }
    },
    "verdict": {
      "intent": "offer_item",
      "slots": {
        "item": "espada",
        "qty": "2",
        "verb": "tener"
      },
      "communicated": true,
      "correctness": 0.5,
      "errors": [
        {
          "tag": "conj.person",
          "span": {
            "start": 3,
            "length": 5
          },
          "expected": "tengo",
          "actual": "tiene"
        },
        {
          "tag": "noun.number",
          "span": {
            "start": 11,
            "length": 6
          },
          "expected": "espadas",
          "actual": "espada"
        }
      ],
      "pronunciation": [],
      "confidence": 1,
      "resolvedBy": "Grammar",
      "versions": {
        "contentHash": "cedb97c4d068221c58b86c4a69dc23fa081d6eda8ecb96dc068361bc5c3afad1",
        "modelHashes": {
          "asr": "scripted-transcript"
        }
      }
    }
  },
  "asr": {
    "transcript": "yo tiene dos espada",
    "nBest": [
      {
        "text": "yo tiene dos espada",
        "score": 1
      }
    ],
    "words": [],
    "confidence": 1,
    "modelHash": "scripted-transcript",
    "mode": "Open"
  },
  "posteriors": null,
  "context": {
    "worldSeed": 42,
    "now": {
      "unixSeconds": 1760000000
    },
    "agentId": "blacksmith",
    "agentState": {
      "agentId": "blacksmith",
      "stateId": "idle",
      "interactionCounter": 0
    },
    "learner": {
      "parameters": {
        "priorKnown": 0.3,
        "learn": 0.1,
        "slip": 0.1,
        "guess": 0.2
      },
      "skills": []
    },
    "repeatCountsByTag": {},
    "hostStateHash": "6b9e4914018ba5c025378256d2bc473fbab48e4db7cbf773baae073e42d76b21"
  },
  "result": {
    "verdict": {
      "intent": "offer_item",
      "slots": {
        "item": "espada",
        "qty": "2",
        "verb": "tener"
      },
      "communicated": true,
      "correctness": 0.5,
      "errors": [
        {
          "tag": "conj.person",
          "span": {
            "start": 3,
            "length": 5
          },
          "expected": "tengo",
          "actual": "tiene"
        },
        {
          "tag": "noun.number",
          "span": {
            "start": 11,
            "length": 6
          },
          "expected": "espadas",
          "actual": "espada"
        }
      ],
      "pronunciation": [],
      "confidence": 1,
      "resolvedBy": "Grammar",
      "versions": {
        "contentHash": "cedb97c4d068221c58b86c4a69dc23fa081d6eda8ecb96dc068361bc5c3afad1",
        "modelHashes": {
          "asr": "scripted-transcript"
        }
      }
    },
    "decision": {
      "agent": {
        "next": {
          "agentId": "blacksmith",
          "stateId": "trading",
          "interactionCounter": 1
        },
        "actions": [
          {
            "dialogueIntent": "offer_item_reply",
            "hostEvent": "trade.offer",
            "variantIndex": 0
          }
        ]
      },
      "learner": {
        "parameters": {
          "priorKnown": 0.3,
          "learn": 0.1,
          "slip": 0.1,
          "guess": 0.2
        },
        "skills": [
          {
            "skillId": "conj",
            "pKnown": 0.14576271186440679,
            "attempts": 1,
            "errors": 1
          },
          {
            "skillId": "intent.offer_item",
            "pKnown": 0.6926829268292684,
            "attempts": 1,
            "errors": 0
          },
          {
            "skillId": "noun",
            "pKnown": 0.14576271186440679,
            "attempts": 1,
            "errors": 1
          }
        ]
      },
      "rating": "Hard",
      "feedback": {
        "items": [
          {
            "tag": "conj.person",
            "type": "Recast",
            "expected": "tengo",
            "actual": "tiene"
          },
          {
            "tag": "noun.number",
            "type": "Recast",
            "expected": "espadas",
            "actual": "espada"
          }
        ]
      }
    },
    "response": {
      "lines": [
        {
          "lineId": "feedback.recast.conj.person.2",
          "text": "Ah, ¿tienes dos espadas? ¡Muy bien!",
          "isRecast": true
        },
        {
          "lineId": "offer_item_reply.1",
          "text": "¡Dos espadas! Trato hecho.",
          "isRecast": false
        }
      ]
    },
    "effects": [],
    "repair": null,
    "replay": {
      "clipHash": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
      "promptId": "offer-items-open",
      "hostStateHash": "6b9e4914018ba5c025378256d2bc473fbab48e4db7cbf773baae073e42d76b21",
      "versions": {
        "contentHash": "cedb97c4d068221c58b86c4a69dc23fa081d6eda8ecb96dc068361bc5c3afad1",
        "modelHashes": {
          "asr": "scripted-transcript"
        }
      },
      "verdict": {
        "intent": "offer_item",
        "slots": {
          "item": "espada",
          "qty": "2",
          "verb": "tener"
        },
        "communicated": true,
        "correctness": 0.5,
        "errors": [
          {
            "tag": "conj.person",
            "span": {
              "start": 3,
              "length": 5
            },
            "expected": "tengo",
            "actual": "tiene"
          },
          {
            "tag": "noun.number",
            "span": {
              "start": 11,
              "length": 6
            },
            "expected": "espadas",
            "actual": "espada"
          }
        ],
        "pronunciation": [],
        "confidence": 1,
        "resolvedBy": "Grammar",
        "versions": {
          "contentHash": "cedb97c4d068221c58b86c4a69dc23fa081d6eda8ecb96dc068361bc5c3afad1",
          "modelHashes": {
            "asr": "scripted-transcript"
          }
        }
      }
    }
  }
}
```

</details>

What to notice:

- `record.verdict`, `result.verdict` and `result.replay.verdict` are the same verdict. The envelope carries it three times on
  purpose: `record` is the contract that is also valid on its own, `result` is everything the pipeline produced.
- `record.verdict.versions` and `record.versions` are identical; both carry the content hash.
- `context.worldSeed` is 42 and `context.agentState.interactionCounter` is 0. Together with the agent id they seed the random
  draw that chose the second agent line (`offer_item_reply.1`, "¡Dos espadas! Trato hecho."). Without `--seed`, the seed is 0 and the
  same utterance gets a different variant ("Vale, dos espadas. Gracias, amigo."), which is why the seed is part of the log.
- `decision.agent.next` moves the blacksmith from `idle` to `trading` and `decision.agent.actions` carries the host event
  `trade.offer`. `decision.learner` is the learner model after this utterance (three skills touched: `conj`, `noun`,
  `intent.offer_item`).
- `response.lines[0]` is the recast ("Ah, ¿tienes dos espadas? ¡Muy bien!"), built from the player's own slots; `isRecast` is `true`.
- `posteriors` is `null`: no phoneme recognizer ran, so `verdict.pronunciation` is empty.

## Writing a log

Pass `--replay-out <file>` to `eval`, as in the example above. `--seed`, `--now`, `--agent-state`, `--counter` and `--repeat <tag>=<n>`
set the parts of the conversation context that a command line can express; they all end up in the log's `context`.
Nothing is written unless you ask for it, and the file is only ever appended to. In code, `ReplayLog` wraps a `TextWriter`:
`new ReplayLog(writer).Append(ReplayEnvelope.From(trace, context))`, where `trace` comes from `UtterancePipeline.Trace(clip, prompt, context)`
(`Evaluate` returns only the result; `Trace` also returns the recognizer outputs that an envelope needs).

## Replaying a log

```bash
dotnet run --project tools/Vocaterra.Nlp.Cli -- replay replay.jsonl --content content/v1
```

```text
OK
```

For every entry the replayer (`Vocaterra.Nlp.Pipeline.Replayer.Replay`):

1. looks up the recorded prompt id in the content bundle given with `--content`;
2. builds a pipeline over the same content with the recorded recognizer outputs in place of the models;
3. evaluates the clip again in the recorded `context`;
4. serializes the recorded and the new core record, and the recorded and the new downstream parts (`decision`, `response`,
   `effects`, `repair`), and compares the two JSON documents.

It prints `OK` and exits 0 when every entry is identical. Otherwise it prints one line per differing entry with the JSON
Pointer paths (RFC 6901) of the values that differ, and exits 1:

```text
DIFF entry 1: /verdict/correctness
```

(That output comes from editing `record.verdict.correctness` in the file from 0.5 to 0.9.)

The same thing happens when the content bundle is not the one the log was recorded with, because the content hash is part of the
version stamp:

```text
DIFF entry 1: /verdict/versions/contentHash, /versions/contentHash
```

Exit codes: 0 everything matches, 1 a difference or an unreadable log (`error: Replay log line N: ...`), 2 a malformed command line.

What a difference means:

- A **verdict or downstream difference with the same version stamp** is a regression in the code (or a nondeterminism bug).
  This is what the replay exists to catch.
- A **version stamp difference** (`/versions/contentHash`) means the content bundle changed since the recording. Any edit to
  `content/v1` changes the hash, so old logs report a difference until they are re-recorded. That is expected after an
  intentional content change; it is not expected otherwise.
- The replay does not re-check the models. It trusts the recorded `asr` and `posteriors`. Whether a model still produces the
  same output is the job of the model-gated tests and of the golden audio set (`golden/README.md`).

## A model-backed log

With the Vosk model fetched (`scripts/fetch-models.sh vosk-model-small-es-0.42`), `eval --wav ... --models models` records the
real recognizer output. This is the `asr` part of an envelope recorded for a short English clip on the `spell-cast-constrained`
prompt. The clip (`Front_Center.wav` from the `alsa-utils` sample sounds) is not Spanish, so the constrained recognizer finds
nothing from the spell grammar and answers `[unk]`:

```bash
dotnet run --project tools/Vocaterra.Nlp.Cli -- eval --wav /usr/share/sounds/alsa/Front_Center.wav --prompt spell-cast-constrained --content content/v1 --models models --replay-out vosk-replay.jsonl
```

```json
{
  "transcript": "[unk]",
  "nBest": [
    {
      "text": "[unk]",
      "score": 0.6757861855264654
    },
    {
      "text": "el [unk]",
      "score": 0.124123089736703
    },
    {
      "text": "[unk] lanza",
      "score": 0.07843825324564244
    },
    {
      "text": "[unk] [unk]",
      "score": 0.07478697452210112
    },
    {
      "text": "[unk] lanza [unk]",
      "score": 0.04686549696908803
    }
  ],
  "words": [
    {
      "text": "[unk]",
      "startSeconds": 0.69,
      "endSeconds": 0.72,
      "confidence": 1
    }
  ],
  "confidence": 0.6757861855264654,
  "modelHash": "529f298625af25f8d94240c49ff87eaee8f925eeb0c55dcb16768268d6f80d0f",
  "mode": "Constrained"
}
```

Here `modelHash` is the model hash from `models/manifest.json` (the combined hash of the model's 14 pinned files), `mode` is
`Constrained`, and `nBest` holds up to five alternatives with their scores. The verdict of that envelope has `communicated: false`
and the pipeline's repair step asked "¿Cómo?". This log replays with no model present:

```bash
dotnet run --project tools/Vocaterra.Nlp.Cli -- replay vosk-replay.jsonl --content content/v1
```

```text
OK
```

When a phoneme recognizer ran, `posteriors` holds its output: the phone vocabulary and a row-major `[frame, phone]` matrix of
log-probabilities (`logProbs`), which can be large. That path needs the wav2vec2 model, whose ONNX export is not part of the
repository yet, so this repository has no sample of it.

## Bare records

`ReplayLog.Append(ReplayRecord)` writes only the core record. `ReplayReader.Read` reads both bare records and envelopes (an
envelope contributes its `record`), and `ReplayReader.ReadEnvelopes` insists on envelopes. A bare record is enough to compare
verdicts across builds when the caller can re-run the real recognizers on the audio:

```csharp
Replayer.Replay(ReplayRecord record, ConversationContext context, UtterancePipeline pipeline, Func<string, PcmClip> clipByHash)
```

This overload re-evaluates through the pipeline's own recognizers, using the clip returned by `clipByHash` for the recorded
clip hash, and compares only the core record. The CLI does not use it.

## What is and is not stored

- **Audio is never stored.** A log identifies a clip by its hash only. Replays do not need the audio, and the replayer is given
  an empty clip carrying the recorded hash.
- **Logs are opt-in and local.** The pipeline writes nothing by itself (`IMPLEMENTATION_PLAN.md` section 1, "Mic privacy"), and
  nothing in the repository uploads a log.
- **Logs can still be personal.** They contain what the recognizer heard (the transcript, which includes the player's
  mistakes), the host state, and, for pronunciation scoring, the phone posteriors. Treat them like the recordings in
  `golden/README.md`: do not attach a log from a real person to a public issue without their consent.
- A replay log is the thing to attach to a bug report about a wrong verdict: it reproduces the verdict on the maintainer's
  machine without any model.
