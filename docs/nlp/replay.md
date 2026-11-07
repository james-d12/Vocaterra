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
- [Host-turn entries](#host-turn-entries-m28a-cr-015)
- [What is and is not stored](#what-is-and-is-not-stored)

## What a log is

A replay log is a text file with one JSON object per line (JSON Lines). There are three kinds of line (the third, a host-initiated turn, is described in
[Host-turn entries](#host-turn-entries-m28a-cr-015)):

| Kind | Type | Contents | Replayable by `Vocaterra.Nlp.Cli replay` |
| --- | --- | --- | --- |
| **Envelope** | `Vocaterra.Nlp.Pipeline.ReplayEnvelope` | the core record, the recognizers' outputs, the conversation context and the full pipeline result | yes |
| **Bare record** | `Vocaterra.Nlp.Core.Replay.ReplayRecord` | clip hash, prompt id, host state hash, version stamp and verdict only | no (see [Bare records](#bare-records)) |
| **Host turn** | `Vocaterra.Nlp.Pipeline.HostTurnEnvelope` | the event, the context and the agent step with its lines (no clip, no verdict) | yes |

The CLI writes envelopes. The core record inside an envelope is the contract from `implementation-plan.md` section 4:

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
| `record.promptId` | string | The prompt that was asked (an id from `content/game-v1/prompts/prompts.json`). The replayer looks it up in the content bundle it is given. |
| `record.hostStateHash` | string | `CanonicalJson` hash of the conversation context (seed, agent, state, time, learner, repeat counts, and the host slots and extra skills when present). Equals `context.hostStateHash`. Two contexts that differ only in a host slot or an extra skill have different hashes. |
| `record.versions.contentHash` | string | The content bundle hash (`content/game-v1/manifest.json`, `bundleHash`). |
| `record.versions.modelHashes` | map | One entry per model that took part, sorted: `asr`, `phoneme`, `embedding`, `tts`. A scripted recognizer reports its own label (`scripted-transcript`). |
| `record.verdict` | object | The `Verdict`: intent, slots, communicated, correctness, errors, pronunciation, confidence, resolvedBy, versions. |
| `asr` | object | What the word recognizer returned: transcript, n-best list, word timings, confidence, model hash, recognition mode. During a replay it is served back by a `ReplayWordRecognizer` keyed by the clip hash, whatever the request asks for. With `PipelineOptions.OpenRecognitionFallback` (M80) it is the recognition the verdict was made from: a line heard on the free second decode is recorded with `mode` `Open` at a constrained prompt. |
| `posteriors` | object or null | What the phoneme recognizer returned (`vocabulary`, `logProbs`, `frameCount`, `frameSeconds`), or `null` when pronunciation scoring did not run. Served back by a `RecordedPhonemeRecognizer`. |
| `context` | object | The `ConversationContext` the utterance was evaluated in: world seed, stored time (`now.unixSeconds`), agent id and state (its `interactionCounter` selects the random stream), the learner model before the utterance, the repeat counts per error tag before the utterance (the pipeline adds this utterance's tags itself), and the host state hash. Two members appear only when the host set them: `hostSlots` (the host facts for the reply and feedback templates, a sorted string map; CR-003) and `extraSkills` (the host-named skills of the learner update, a sorted list; CR-005). An envelope without them is byte-identical to one written before they existed, and the replayer reads them back from the envelope like every other context member. |
| `result` | object | The full `PipelineResult`: `verdict`, `decision` (agent step, updated learner, FSRS rating, feedback plan), `response` (the lines the agent says), `effects`, `repair`, `replay` (the same record as `record`), `words` (each token with its role and the reading the resolver chose; CR-002) and `answer` (the answer match; CR-004). `words` and `answer` are derived, not recorded: they are recomputed on replay and compared. |

The core record (`clipHash`, `promptId`, `hostStateHash`, `versions`, `verdict`) is unchanged by M26c and stays the contract of
`implementation-plan.md` section 4. The words and the answer match are not in it: they are views of the same inputs that the verdict is
built from, and the replayer recomputes and compares them through the envelope's `result`, so a change in how a lemma is resolved shows
up as a `/words` difference even when the verdict does not change.

## A real example

This line was produced by running the CLI on the worked example from the high-level design, in the words of the game's bundle ("yo quiere dos espada"
said to the villager). No model is involved: the word recognizer is scripted from `--transcript`, so `asr.modelHash` is
`scripted-transcript`.

```bash
dotnet run --project tools/Vocaterra.Nlp.Cli -- eval --transcript "yo quiere dos espada" --prompt villager-open --content content/game-v1 --seed 42 --now 1760000000 --replay-out replay.jsonl
```

The command prints the verdict JSON, then the agent lines, then a decision summary (the long `contentHash` is cut to `<the bundle hash>`; it is the hash of
`content/game-v1`, so it changes with every edit of the game's content, and so does a log's content hash):

```text
{"intent":"request_item","slots":{"item":"espada","qty":"2"},"communicated":true,"correctness":0.6,"errors":[{"tag":"conj.person","span":{"start":3,"length":6},"expected":"quiero","actual":"quiere"},{"tag":"noun.number","span":{"start":12,"length":6},"expected":"espadas","actual":"espada"}],"pronunciation":[],"confidence":1,"resolvedBy":"Grammar","versions":{"contentHash":"<the bundle hash>","modelHashes":{"asr":"scripted-transcript"}}}
agent: ¡Eso es! «Yo quiero dos espadas».
agent: Aquí tienes.
decision: rating Good, learner skills 3, feedback items 2
```

`replay.jsonl` now holds one line of about 4.5 KB. Here it is, pretty-printed for reading (the file itself has no
whitespace between tokens):

<details>
<summary>The complete envelope (343 lines when pretty-printed)</summary>

```json
{
  "record": {
    "clipHash": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
    "promptId": "villager-open",
    "hostStateHash": "ef1f6cd97924f568ad427c7713c811a5a780f61c0ebe52b323ea00c6e7100fc7",
    "versions": {
      "contentHash": "<the bundle hash>",
      "modelHashes": {
        "asr": "scripted-transcript"
      }
    },
    "verdict": {
      "intent": "request_item",
      "slots": {
        "item": "espada",
        "qty": "2"
      },
      "communicated": true,
      "correctness": 0.6,
      "errors": [
        {
          "tag": "conj.person",
          "span": {
            "start": 3,
            "length": 6
          },
          "expected": "quiero",
          "actual": "quiere"
        },
        {
          "tag": "noun.number",
          "span": {
            "start": 12,
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
        "contentHash": "<the bundle hash>",
        "modelHashes": {
          "asr": "scripted-transcript"
        }
      }
    }
  },
  "asr": {
    "transcript": "yo quiere dos espada",
    "nBest": [
      {
        "text": "yo quiere dos espada",
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
    "agentId": "villager",
    "agentState": {
      "agentId": "villager",
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
    "hostStateHash": "ef1f6cd97924f568ad427c7713c811a5a780f61c0ebe52b323ea00c6e7100fc7"
  },
  "result": {
    "verdict": {
      "intent": "request_item",
      "slots": {
        "item": "espada",
        "qty": "2"
      },
      "communicated": true,
      "correctness": 0.6,
      "errors": [
        {
          "tag": "conj.person",
          "span": {
            "start": 3,
            "length": 6
          },
          "expected": "quiero",
          "actual": "quiere"
        },
        {
          "tag": "noun.number",
          "span": {
            "start": 12,
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
        "contentHash": "<the bundle hash>",
        "modelHashes": {
          "asr": "scripted-transcript"
        }
      }
    },
    "decision": {
      "agent": {
        "next": {
          "agentId": "villager",
          "stateId": "idle",
          "interactionCounter": 1
        },
        "actions": [
          {
            "dialogueIntent": "villager_give",
            "hostEvent": "villager.give",
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
            "skillId": "intent.request_item",
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
      "rating": "Good",
      "feedback": {
        "items": [
          {
            "tag": "conj.person",
            "type": "Recast",
            "expected": "quiero",
            "actual": "quiere"
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
          "lineId": "feedback.recast.3",
          "text": "¡Eso es! «Yo quiero dos espadas».",
          "isRecast": true
        },
        {
          "lineId": "villager_give.1",
          "text": "Aquí tienes.",
          "isRecast": false
        }
      ]
    },
    "effects": [],
    "repair": null,
    "replay": {
      "clipHash": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
      "promptId": "villager-open",
      "hostStateHash": "ef1f6cd97924f568ad427c7713c811a5a780f61c0ebe52b323ea00c6e7100fc7",
      "versions": {
        "contentHash": "<the bundle hash>",
        "modelHashes": {
          "asr": "scripted-transcript"
        }
      },
      "verdict": {
        "intent": "request_item",
        "slots": {
          "item": "espada",
          "qty": "2"
        },
        "communicated": true,
        "correctness": 0.6,
        "errors": [
          {
            "tag": "conj.person",
            "span": {
              "start": 3,
              "length": 6
            },
            "expected": "quiero",
            "actual": "quiere"
          },
          {
            "tag": "noun.number",
            "span": {
              "start": 12,
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
          "contentHash": "<the bundle hash>",
          "modelHashes": {
            "asr": "scripted-transcript"
          }
        }
      }
    },
    "words": [
      {
        "token": {
          "text": "yo",
          "span": {
            "start": 0,
            "length": 2
          },
          "original": "yo"
        },
        "role": "Word",
        "reading": {
          "lemma": "yo",
          "pos": "pron",
          "person": "1",
          "number": "sg",
          "tense": null,
          "mood": null,
          "gender": null
        }
      },
      {
        "token": {
          "text": "quiere",
          "span": {
            "start": 3,
            "length": 6
          },
          "original": "quiere"
        },
        "role": "Word",
        "reading": {
          "lemma": "querer",
          "pos": "verb",
          "person": "3",
          "number": "sg",
          "tense": "pres",
          "mood": "ind",
          "gender": null
        }
      },
      {
        "token": {
          "text": "2",
          "span": {
            "start": 10,
            "length": 1
          },
          "original": "dos"
        },
        "role": "Word",
        "reading": {
          "lemma": "2",
          "pos": "num",
          "person": null,
          "number": "pl",
          "tense": null,
          "mood": null,
          "gender": null
        }
      },
      {
        "token": {
          "text": "espada",
          "span": {
            "start": 12,
            "length": 6
          },
          "original": "espada"
        },
        "role": "Word",
        "reading": {
          "lemma": "espada",
          "pos": "noun",
          "person": null,
          "number": "sg",
          "tense": null,
          "mood": null,
          "gender": "f"
        }
      }
    ],
    "answer": {
      "answerId": null,
      "type": "None",
      "distance": 13,
      "likelyPronunciation": false,
      "confidence": 0
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
  draw that chose the recast line (`feedback.recast.3`) and the reply (`villager_give.1`, "Aquí tienes."). Without `--seed`, the seed is 0 and the
  same utterance gets a different recast ("Claro, «yo quiero dos espadas». Muy bien."), which is why the seed is part of the log.
- `decision.agent.next` keeps the villager in `idle` and `decision.agent.actions` carries the host event `villager.give`. `decision.learner` is the learner
  model after this utterance (three skills touched: `conj`, `noun`, `intent.request_item`).
- `response.lines[0]` is the recast ("¡Eso es! «Yo quiero dos espadas»."), built from the player's own sentence; `isRecast` is `true`.
- `posteriors` is `null`: no phoneme recognizer ran, so `verdict.pronunciation` is empty.

## Writing a log

Pass `--replay-out <file>` to `eval`, as in the example above. `--seed`, `--now`, `--agent-state`, `--counter` and `--repeat <tag>=<n>`
set the parts of the conversation context that a command line can express; they all end up in the log's `context`.
Nothing is written unless you ask for it, and the file is only ever appended to. In code, `ReplayLog` wraps a `TextWriter`:
`new ReplayLog(writer).Append(ReplayEnvelope.From(trace, context))`, where `trace` comes from `UtterancePipeline.Trace(clip, prompt, context)`
(`Evaluate` returns only the result; `Trace` also returns the recognizer outputs that an envelope needs).

## Replaying a log

```bash
dotnet run --project tools/Vocaterra.Nlp.Cli -- replay replay.jsonl --content content/game-v1
```

```text
OK
```

For every entry the replayer (`Vocaterra.Nlp.Pipeline.Replayer.Replay`):

1. looks up the recorded prompt id in the content bundle given with `--content`;
2. builds a pipeline over the same content with the recorded recognizer outputs in place of the models;
3. evaluates the clip again in the recorded `context`;
4. serializes the recorded and the new core record, and the recorded and the new downstream parts (`decision`, `response`,
   `effects`, `repair`, `words`, `answer`), and compares the two JSON documents.

It prints `OK` and exits 0 when every entry is identical. Otherwise it prints one line per differing entry with the JSON
Pointer paths (RFC 6901) of the values that differ, and exits 1:

```text
DIFF entry 1: /verdict/correctness
```

(That output comes from editing the correctness in the file from 0.6 to 0.9.)

**Logs recorded before M26c** carry no `words` or `answer` in `result`. Replaying one reports `/words` and `/answer` as differing
(the recorded value is absent, the new one is not); the verdict and the other downstream parts still compare as before. Re-record such a
log to get a clean replay. No log is committed in the repository.

The same thing happens when the content bundle is not the one the log was recorded with, because the content hash is part of the
version stamp (here the hash in the file was replaced by 64 zeros):

```text
DIFF entry 1: /verdict/versions/contentHash, /versions/contentHash
```

Exit codes: 0 everything matches, 1 a difference or an unreadable log (`error: Replay log line N: ...`), 2 a malformed command line.

What a difference means:

- A **verdict or downstream difference with the same version stamp** is a regression in the code (or a nondeterminism bug).
  This is what the replay exists to catch.
- A **version stamp difference** (`/versions/contentHash`) means the content bundle changed since the recording. Any edit to
  `content/game-v1` changes the hash, so old logs report a difference until they are re-recorded. That is expected after an
  intentional content change; it is not expected otherwise.
- The replay does not re-check the models. It trusts the recorded `asr` and `posteriors`. Whether a model still produces the
  same output is the job of the model-gated tests and of the golden audio set (`tests/golden/README.md`).

## A model-backed log

With the Vosk model fetched (`scripts/fetch-models.sh vosk-model-small-es-0.42`), `eval --wav ... --models models` records the
real recognizer output. This is the `asr` part of an envelope recorded for a short English clip on the constrained prompt `lesson-hello`. The clip
(`Front_Center.wav` from the `alsa-utils` sample sounds) is not Spanish, so the constrained recognizer finds nothing from the greeting's answer set and
answers `[unk]`:

```bash
dotnet run --project tools/Vocaterra.Nlp.Cli -- eval --wav /usr/share/sounds/alsa/Front_Center.wav --prompt lesson-hello --content content/game-v1 --models models --replay-out vosk-replay.jsonl
```

```json
{
  "transcript": "[unk]",
  "nBest": [
    {
      "text": "[unk]",
      "score": 0.7808811578363563
    },
    {
      "text": "más [unk]",
      "score": 0.07185390580557677
    },
    {
      "text": "por [unk]",
      "score": 0.058497087788082595
    },
    {
      "text": "[unk] [unk]",
      "score": 0.0514905333125128
    },
    {
      "text": "sé [unk]",
      "score": 0.03727731525747155
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
  "confidence": 0.7808811578363563,
  "modelHash": "529f298625af25f8d94240c49ff87eaee8f925eeb0c55dcb16768268d6f80d0f",
  "mode": "Constrained"
}
```

Here `modelHash` is the model hash from `models/manifest.json` (the combined hash of the model's 14 pinned files), `mode` is
`Constrained`, and `nBest` holds up to five alternatives with their scores. The verdict of that envelope has `communicated: false`
and the pipeline's repair step asked "¿Cómo?". This log replays with no model present:

```bash
dotnet run --project tools/Vocaterra.Nlp.Cli -- replay vosk-replay.jsonl --content content/game-v1
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

## Host-turn entries (M28a, CR-015)

A host-initiated agent turn (`UtterancePipeline.Advance`) has no clip, no recognizer output and no verdict, so it is not an envelope. It is a second
kind of line, `Vocaterra.Nlp.Pipeline.HostTurnEnvelope`:

```csharp
public sealed record HostTurnEnvelope(string Kind, string HostEvent, ConversationContext Context, HostTurnResult Result);
public sealed record HostTurnResult(string HostEvent, AgentStepResult Agent, ResponsePlan Response, string HostStateHash, string ContentHash);
```

| Path | Meaning |
| --- | --- |
| `kind` | Always `"hostTurn"`. The discriminator: an utterance envelope has no `kind` property (its first property is `record`). |
| `hostEvent` | The event name the host raised. |
| `context` | The context `Advance` ran in (the same `ConversationContext` JSON as in an envelope, host slots included when set). |
| `result.hostEvent`, `result.agent`, `result.response` | The event, the agent step (next state with the advanced `interactionCounter`, and the actions with their host events and variant indexes) and the rendered lines. |
| `result.hostStateHash` | `context.hostStateHash`. |
| `result.contentHash` | The content bundle hash. No model takes part in a host turn, so no model hashes are stored and replaying needs no recognizer. |

A turn that did nothing (`Advance` returned null) is not written.

**Replaying.** `Replayer.Replay(HostTurnEnvelope, pipeline)` runs `Advance` again in the recorded context and compares the recorded and the new `result`;
the paths it reports are relative to `result` (`/agent/next/stateId`, `/response/lines/0/text`, `/contentHash`). A turn that no longer happens (the
state or the event is no longer handled) is reported as `/result`. The CLI `replay` command replays both kinds in file order.

**Reading and compatibility rules.**

- The log format has no version number; it changes only by adding entry kinds and members, and a reader tells the kinds apart by property. Old logs contain
  only envelopes and bare records and read exactly as before: nothing in `ReplayEnvelope`, `ReplayRecord` or the context changed, and the goldens do not move.
- `ReplayReader.ReadEntries` returns every line as a `ReplayEntry` with exactly one of `Utterance` (an envelope) or `HostTurn`. A bare record is still an error there.
- `ReplayReader.Read` and `ReplayReader.ReadEnvelopes` keep their contracts (core records, utterance envelopes) and **skip** host-turn lines, which have neither.
  A host that records host turns and reads the log with an older build of the core gets an error (the older reader does not know `kind` and rejects the
  line as not an envelope); use one log file per kind, or read with `ReadEntries`.
- A line is a host turn when it is a JSON object whose `kind` is `"hostTurn"`; every other line is read as before.

## The open-recognition fallback and replay (M80)

The envelope has no field for the second decode, and its format did not change. With `OpenRecognitionFallback` on, `asr` is the recognition that was chosen:
- **The free recognition was chosen** (the constrained one was not taken and the free one was). It is recorded with `mode` `Open`. On replay the recorded recognition
  is served for the first request; its mode is `Open`, so the trigger (which needs a `Constrained` first recognition) never fires, and the verdict is the one recorded.
  A replay through a pipeline with the option off gives the same result, because the stages read the recorded recognition the same way.
- **The constrained recognition was kept** (the free one was not taken, or had a word under `OpenPassWordFloor`). It is recorded with `mode` `Constrained`. A replay through a pipeline with the option on serves the same recognition
  again for the second (open) request; it is not taken either, so the first reading is kept and the verdict is the same. The unchosen free recognition
  (`UtteranceAnalysis.OtherPass`) is not recorded.

A replay never needs the model for either case, and old logs replay as before.

## What is and is not stored

- **Audio is never stored.** A log identifies a clip by its hash only. Replays do not need the audio, and the replayer is given
  an empty clip carrying the recorded hash.
- **Logs are opt-in and local.** The pipeline writes nothing by itself (`implementation-plan.md` section 1, "Mic privacy"), and
  nothing in the repository uploads a log.
- **Logs can still be personal.** They contain what the recognizer heard (the transcript, which includes the player's
  mistakes), the host state, and, for pronunciation scoring, the phone posteriors. Treat them like the recordings in
  `tests/golden/README.md`: do not attach a log from a real person to a public issue without their consent.
- A replay log is the thing to attach to a bug report about a wrong verdict: it reproduces the verdict on the maintainer's
  machine without any model.
