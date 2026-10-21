# Vocaterra.NLP packages

Vocaterra.NLP is the language core of a Spanish learning application: a learner speaks, the core returns a verdict (what was said, whether it was
understood, how well it was said), the learner model and the feedback plan, and the agent's response text. The core knows nothing about the
host application; effects are opaque outcome kinds the host maps to its own rules.

Every package is version 0.1.0 and references the others at that exact version. The packages are:

| Package | What it holds |
| --- | --- |
| `Vocaterra.Nlp.Core` | Contracts: verdicts, prompt specs, error tags, replay envelopes, determinism primitives |
| `Vocaterra.Nlp.Audio` | WAV files, resampling, gain, energy voice-activity detection, push-to-talk sessions |
| `Vocaterra.Nlp.Asr` | Speech recognizers: constrained Vosk, open sherpa-onnx, scripted and replay recognizers |
| `Vocaterra.Nlp.Phonology` | Spanish grapheme-to-phoneme conversion and dialect configuration |
| `Vocaterra.Nlp.Pronunciation` | Pronunciation scoring with greedy and CTC forced-alignment scorers |
| `Vocaterra.Nlp.Language` | Lexicon, normalization, answer matching, intent resolution, slot grammars, morphology |
| `Vocaterra.Nlp.Evaluation` | Verdict assembly and outcome bands |
| `Vocaterra.Nlp.Decision` | Learner model (BKT, FSRS), feedback policy, conversation agent state machine |
| `Vocaterra.Nlp.Response` | Response templates, recasts, speech synthesis adapters, effect resolver |
| `Vocaterra.Nlp.Content` | Content bundle format, loader, validator, provenance, lexicon import |
| `Vocaterra.Nlp.Pipeline` | Utterance pipeline, conversation context, replay log |

A host loads a content bundle (`ContentLoader.Load(directory)`), builds the services and a pipeline, and evaluates an utterance. Without model
files every stage runs with the scripted recognizer and the fakes, so a host needs no model to start. The host API, call by call, is in
`docs/API.md` of the repository.

Licence: Apache-2.0. Each package sets the licence expression `Apache-2.0` and ships the repository's `LICENSE` and `NOTICE`. The
`Vocaterra.Nlp.Asr` and `Vocaterra.Nlp.Response` packages depend on sherpa-onnx, whose native build contains GPL code unless a GPL-free release
is pinned (see `docs/DECISIONS.md`, D2).
