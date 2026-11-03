# Building and testing the core

The prerequisites, the build, the tests and the fuzz suite, and the speech benchmark. The rules for changes are in
[CONTRIBUTING.md](CONTRIBUTING.md).

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
[docs/nlp/FUZZING_PLAN.md](FUZZING_PLAN.md); the oracles, the bounds and the bugs found so far are in
[tests/Vocaterra.Nlp.Fuzzing.Tests/README.md](../../tests/Vocaterra.Nlp.Fuzzing.Tests/README.md).

## Benchmark and golden audio

`tools/Vocaterra.Nlp.Bench` runs a recognizer over the golden learner manifest and reports word error rate, learner-error preservation (does the
transcript keep the mistake the learner made?) and latency:

```bash
dotnet run --project tools/Vocaterra.Nlp.Bench -- --manifest tests/golden/manifest.jsonl --content content/game-v1 --models models
```

The three committed entries are synthetic, so the clips are reported as `missing-clip` on a plain checkout. The recording protocol,
consent rules and manifest schema are in [tests/golden/README.md](../../tests/golden/README.md).
