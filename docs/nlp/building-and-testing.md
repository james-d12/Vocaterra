# Building and testing the core

The prerequisites, the build, the tests and the fuzz suite, and the speech benchmark. The rules for changes are in
[changing-the-core.md](changing-the-core.md).

## Prerequisites

- **.NET SDK 10.0.402**, pinned in `global.json` (`rollForward: latestPatch` allows later patch releases). Projects target `net10.0`.
- A user-local install is enough; no administrator rights are needed:

  ```bash
  curl -sSL https://dot.net/v1/dotnet-install.sh -o dotnet-install.sh
  bash dotnet-install.sh --version 10.0.402 --install-dir "$HOME/.dotnet"
  export DOTNET_ROOT="$HOME/.dotnet"
  export PATH="$HOME/.dotnet:$PATH"
  ```

  Put the two `export` lines in your shell profile, or run them at the start of every shell. `dotnet --version` inside the
  repository should print `10.0.402`.
- On Linux the ICU libraries (`libicu`) must be installed: invariant globalization is deliberately off ([ADR 0006](adr/0006-globalization-icu.md)).
- To fetch models: `bash`, `python3`, `curl`, `sha256sum` (or `shasum`), and `unzip` or `tar` with bzip2.
- A full build writes roughly 3 GB of `bin/` and `obj/` output because of the native speech packages. Make room, and delete
  the output with `git clean -fdX -- src tests tools` when you are done with a checkout.

## Build, test, format

```bash
dotnet restore
dotnet build -warnaserror
dotnet test
dotnet format                       # apply the formatting rules
dotnet format --verify-no-changes   # what CI checks
```

Tests that need a real model are skipped, not failed, unless `VOCATERRA_MODELS_DIR` points at a directory holding the verified model
(see below).

`dotnet test tests/Vocaterra.Nlp.Tests` runs the engine's unit tests and `dotnet test tests/Vocaterra.Nlp.Es.Tests` the Spanish pack's (M53); add `--filter "FullyQualifiedName~LinguisticAnalyzer"` for one class.
`dotnet format` (without `--verify-no-changes`) fixes formatting.

The suite has two tiers ([scripts/README.md](../../scripts/README.md), `test.sh`). `scripts/test.sh fast` builds and runs every test that is
not marked slow, in under a minute: run it after every step. It leaves out the tests marked `[Trait(TestSpeed.Trait, TestSpeed.Slow)]`:
the fuzz suite (an assembly trait), the model-gated tests, the content tool's full-bundle classes (`CliTests`, `ProgramTests`, `LintTests`,
`LintCommandTests`), `SpanishIdentityTests` and a few long single tests. `scripts/test.sh full` runs the whole suite, as the merge gate and
CI do: run it before you report a milestone, and `scripts/test.sh project Vocaterra.Nlp.Tests` when you change an area whose tests are
marked slow. Both go through `scripts/limited.sh`. A new test that takes more than a few seconds on its own, needs a model or starts a
process is marked slow (the constant is in `tests/Shared/TestSpeed.cs`); a test that only reads the bundle uses
`GameBundleLocator.Bundle`, the one load of the test process, instead of loading its own.

### Fuzz tests

`tests/Vocaterra.Nlp.Fuzzing.Tests` generates inputs from the lexicon, the intent grammars and `content/game-v1`, and checks sixteen oracles against the
real stages. It needs no model, and it runs at full depth as part of the normal test run:

```bash
dotnet test tests/Vocaterra.Nlp.Fuzzing.Tests
```

To soak it, raise the iteration count of the oracle groups with environment variables (CI runs the suite at its default
depth in the `build-and-test` job; there is no soak job):

```bash
VOCATERRA_FUZZ_ITERATIONS=50000 VOCATERRA_FUZZ_ITERATIONS_GRAMMAR=50000 VOCATERRA_FUZZ_ITERATIONS_SIGNAL=10000 \
VOCATERRA_FUZZ_ITERATIONS_PIPELINE=3000 VOCATERRA_FUZZ_ITERATIONS_CONTENT=5000 dotnet test tests/Vocaterra.Nlp.Fuzzing.Tests
```

A failing run prints a CsCheck seed. Replay that exact case with the same project:

```bash
CsCheck_Seed=<seed> dotnet test tests/Vocaterra.Nlp.Fuzzing.Tests
```

The failure message also carries the generated input, which is what a regression test needs. The design is in
[docs/nlp/fuzzing-plan.md](fuzzing-plan.md); the oracles, the bounds and the bugs found so far are in
[tests/Vocaterra.Nlp.Fuzzing.Tests/README.md](../../tests/Vocaterra.Nlp.Fuzzing.Tests/README.md).


The rules for the fuzz suite:

- Before you open a pull request that touches the engine folders `Language`, `Evaluation`, `Response`, `Audio`, `Phonology`,
  `Pronunciation`, `Decision`, `Content` or `Pipeline`, run `dotnet test tests/Vocaterra.Nlp.Fuzzing.Tests`.
- A bug the fuzzer finds is fixed in the owning module, with a regression test in that module's test project. List the bug with its
  CsCheck seed in the "Bugs found and fixed" section of the fuzz README.
- A documented exception may be waved through only in `tests/Vocaterra.Nlp.Fuzzing.Tests/Oracles/KnownTolerances.cs`, and it needs a reason.
- Never weaken an oracle to make a failure go away.

## Benchmark and golden audio

`tools/Vocaterra.Nlp.Bench` runs a recognizer over the golden learner manifest and reports word error rate, learner-error preservation (does the
transcript keep the mistake the learner made?) and latency:

```bash
dotnet run --project tools/Vocaterra.Nlp.Bench -- --manifest tests/golden/manifest.jsonl --content content/game-v1 --models models
```

`--open-fallback` (M80) also runs the open decode of `PipelineOptions.OpenRecognitionFallback` after every constrained one, timed on its own and reported as an
extra row in mode `Open` (for every entry; the pipeline decodes again only when the first reading is not taken or looks forced, M84), and `--open-vosk` decodes open mode with Vosk
even when Parakeet is present, so that both passes run on the model the game loads. The bench is the only place a clock is read. The model-gated `OpenFallbackMeasurementTests` runs with the default free-pass floor (`OpenPassWordFloor` 0.5), must show zero false takes, and prints what the floor removed (understood lines lost, wrong and false takes stopped, by voice) by running the same pipeline with the floor at 0. That test runs the pipeline with the M84 trigger off (M80 as it was); the forced-row part of the class (`OpenFallbackMeasurementTests.ForcedRows.cs`, about two minutes with the clips cached) runs the defaults over every line at every free-talk prompt and every answer row in all eight voice variants, prints the signals of correctly heard rows against forced lines by voice, and fails on a correct row replaced by the free reading, a false take, or fewer than 100 forced lines rescued.

The three committed entries are synthetic, so the clips are reported as `missing-clip` on a plain checkout. The recording protocol,
consent rules and manifest schema are in [tests/golden/README.md](../../tests/golden/README.md).
