# Vocaterra.NLP: the language core

> This document covers the language core: the language-neutral engine `Vocaterra.Nlp` (one assembly, `src/Vocaterra.Nlp`, ADR 0012), the Spanish language pack `Vocaterra.Nlp.Es` (`src/Vocaterra.Nlp.Es`, M53), their tests and its tools (`tests/`, `tools/`) in the Vocaterra repository. The game
> (`docs/game/README.md`) sits beside it in the same solution and references the core's projects directly. Paths below are relative to the
> repository root.

Vocaterra.NLP is a standalone Spanish voice-and-language evaluation core, written in C# on .NET. Given an utterance (audio, or a transcript
for development), it works out what the learner meant and how well they said it, and returns one deterministic **verdict**, with tagged
errors such as `conj.person` or `pron.r`, a feedback plan, a conversation step and the response text. It is a library first: a host loads a
content bundle, builds the services, evaluates utterances and plans responses; [docs/nlp/api.md](api.md) lists the calls in order. The
`Vocaterra.Nlp.Cli` tool is a demo host that drives the same API from the command line. Hosts live outside the core; nothing host-specific (scenes, items, prices, rewards) is in the core. There is one content bundle in the
repository, the game's `content/game-v1`; the core's demo CLI and its tests run on it (R02, [ADR 0009](adr/0009-core-and-game-boundary.md)), and a game edit that
breaks the core fails a core test.

The verdict separates whether the learner *communicated* (the agent understood) from whether they were *correct* (grammar, vocabulary,
pronunciation). The guiding principle is to reward communication and coach correctness: if the learner was understood the conversation
responds, and the correction rides along on the response, for example as a recast ("Claro, «yo quiero dos espadas»").

**Every runtime step is deterministic.** The same audio clip, host state and content version always produce the same verdict, feedback and outcome. Algorithmic stages are plain code with fixed-order math; the neural models (speech recognition, phoneme recognition, embeddings, speech synthesis) are frozen, run greedily on one CPU thread, and are pinned by SHA-256, with a margin on every threshold so floating-point drift cannot flip an outcome. Every graded interaction can be written to a replay log and re-checked later without the models. The libraries are engine-agnostic; the host engine is still an open decision ([docs/nlp/decisions.md](decisions.md)).

## Quick start

From the repository root. The .NET SDK is pinned in `global.json` (10.0.402); a user-local install is enough
([prerequisites](building-and-testing.md#prerequisites)). `Vocaterra.Nlp.slnf` builds the core alone; `Vocaterra.sln` builds it with the game.

```bash
dotnet build Vocaterra.Nlp.slnf -warnaserror
dotnet test Vocaterra.Nlp.slnf
dotnet run --project tools/Vocaterra.Nlp.Cli -- eval --transcript "yo quiere dos espada" --prompt villager-open --content content/game-v1
```

The last line is the design's worked example in the words of the game's bundle: a wrong verb form and a noun left singular after a number. The
CLI prints the verdict, with `conj.person` and `noun.number` and the corrected forms, then the agent's recast and reply.
[cli-examples.md](cli-examples.md) explains the output and runs many more.

## How it is built

The engine `Vocaterra.Nlp` (one assembly, its stages as folders) knows no particular language; the Spanish pack `Vocaterra.Nlp.Es` supplies
everything Spanish, and a host registers it (ADR 0012). [architecture.md](architecture.md) has the diagram of the stages, the dependency rules, how a
host uses the core and where its files are; [pipeline.md](pipeline.md) follows one utterance through every stage.

## Determinism rules in brief

The full rules, with where each is enforced, are in [ADR 0002](adr/0002-determinism-rules.md); the pull request checklist is in
[changing-the-core.md](changing-the-core.md). In short, code under `src/` must:

- use no `System.Random` (only `SeededRng.ForInteraction(worldSeed, agentId, counter)`) and no wall clock (time arrives as a `StoredTimestamp`);
- hash with `DeterministicHash` (SHA-256) only, and never let `Dictionary` or `HashSet` order reach any output;
- compare ordinally, use the invariant culture and Unicode NFC everywhere;
- put a `Margin` on every threshold and handle all three outcomes (accept, reject, uncertain);
- keep models behind `Vocaterra.Nlp.Core` interfaces, run them on one thread on the CPU, and gate real-model tests on `VOCATERRA_MODELS_DIR`;
- contain no LLM or other non-deterministic code, and pin every model and content version by hash.

## Documents

| Document | What it covers |
| --- | --- |
| [architecture.md](architecture.md) | The stages, their dependencies, using the core from a host, the repository layout |
| [building-and-testing.md](building-and-testing.md) | Prerequisites, build, tests, the fuzz suite, the benchmark and golden audio |
| [cli-examples.md](cli-examples.md) | The demo CLI with and without models, worked runs, replay logs, the content builder |
| [pipeline.md](pipeline.md) | The pipeline stage by stage, with the worked example and the CLI's verdict |
| [api.md](api.md) | What a host calls, in order: load a bundle, build the services, evaluate, plan a response, record and replay |
| [replay.md](replay.md) | The replay log and envelope format, with a real example |
| [lexicon.md](lexicon.md) | The lexicon files, their sources, hashes and licences, how to regenerate them |
| [changing-the-core.md](changing-the-core.md) | Conventions, the determinism checklist, the definition of done, recipes for error tags, content, golden clips and model adapters, gotchas |
| [tools.md](tools.md) | The third-party tools, libraries, models and data sources, with their licences |
| [known-gaps.md](known-gaps.md) | What the core does not do yet |
| [fuzzing-plan.md](fuzzing-plan.md), [the fuzz suite's README](../../tests/Vocaterra.Nlp.Fuzzing.Tests/README.md) | The fuzz design, its oracles and iteration budgets; the bugs it found and the known findings |
| [implementation-plan.md](implementation-plan.md), [decisions.md](decisions.md) | The milestones (§5) and the findings log (§5b); the owner's decisions |
| [adr/](adr/) | The architecture decision records, 0001 to 0012 |
| [diagrams/README.md](diagrams/README.md) | The system diagram in draw.io, and how to open it |
| [models/README.md](../../models/README.md), [tests/golden/README.md](../../tests/golden/README.md), [scripts/README.md](../../scripts/README.md) | The model manifest, the recording protocol, the helper scripts |

The original design note is archived at [docs/archive/nlp/history/high-level-docs.md](../archive/nlp/history/high-level-docs.md); the research
behind the core is in [docs/research/](../research/README.md).

## Licence

- **Code and hand-authored content** are licensed under the [Apache License 2.0](../../LICENSE). The copyright line and the statement about the
  third-party data are in [NOTICE](../../NOTICE).
- **The Wiktionary-derived lexicon** `content/game-v1/lexicon/wiktionary-es.json` is derived from Wiktionary and is licensed under CC BY-SA 4.0, not
  under Apache-2.0. Its attribution, and the terms of the frequency list it was selected with, are in
  [content/game-v1/ATTRIBUTION.md](../../content/game-v1/ATTRIBUTION.md) and [docs/nlp/lexicon.md](lexicon.md).
- **Models** are downloaded, not committed. Each model's licence is recorded in [models/manifest.json](../../models/manifest.json).

Third-party checks:

- **Code dependencies** are checked by `scripts/license-scan.sh [--offline]`, which reads every NuGet package (direct and transitive) of the
  solution and fails on GPL, LGPL, AGPL and SSPL or on any license that is not in `scripts/license-allowlist.txt`.
  `scripts/license-overrides.txt` holds reviewed exceptions for packages whose metadata names no license.
- **Two GPL components must stay out of anything shipped**: espeak-ng (GPL-3.0) and the current Piper runtime. espeak-ng is
  only a development-time oracle. Kokoro's bundled phonemizer is espeak-ng, and the sherpa-onnx native library appears to
  contain it too; see [ADR 0007](adr/0007-kokoro-phonemizer-licensing.md) and [known gaps](known-gaps.md). This is an engineering note, not legal advice.
