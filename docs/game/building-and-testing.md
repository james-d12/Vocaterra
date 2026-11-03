# Building, testing and CI

How to build the game, run its tests and the host, where the speech models come from, and what CI checks. The short version is the
[README](README.md)'s quick start.

## Clone, build and run the host

All commands below run from the repository root.

```bash
git clone https://github.com/james-d12/Vocaterra.git
cd Vocaterra
dotnet build Vocaterra.sln -warnaserror     # the core (Vocaterra.Nlp.*) and the game in one solution
dotnet test Vocaterra.sln
```

The .NET SDK is pinned in `global.json` (10.0.402, `latestPatch`). A user-local install is enough. The core's projects are built from the
same commit; there is no pack step and no local feed.

On the development machine the tests run through `scripts/test.sh` ([scripts/README.md](../../scripts/README.md)), which keeps them inside
`scripts/limited.sh`'s caps. `scripts/test.sh fast` (under a minute) is the check after every step: it leaves out the tests marked
`[Trait(TestSpeed.Trait, TestSpeed.Slow)]` (the world-generation classes, the model-gated tests, the playtest report's process tests, the
whole-route meshing and a few other long single tests, and the core's fuzz suite and CLI tests). `scripts/test.sh full` is the whole suite, as
the merge gate and CI run it, and `scripts/test.sh project Vocaterra.Game.Core.Tests` one project. The tests share one bundle load
(`TestGame.Bundle`) and one build of the route per seed (`RouteScene.Build`, `TestGame.Route`: each test gets a clone it may edit); a test of
the scene builder itself calls `SceneBuilder.Build`.

The speech models are not in the repository. Without them 27 model-gated tests of the game are skipped (44 in the whole suite with the core's, against 3 of the core's with
the models; measured at V09 after phase B: 7218 passed and 44 skipped without them), and the speech runs (the mic check, `--speak`,
`--playtest`) cannot hear. Fetch the pinned Vosk model and the Kokoro model once into a folder outside the repository, then point
`VOCATERRA_MODELS_DIR` at it for the tests and the runs:

```bash
export VOCATERRA_MODELS_DIR="$HOME/.local/share/vocaterra/models"
scripts/fetch-models.sh vosk-model-small-es-0.42 kokoro-multi-lang-v1_0   # needs curl, python3, sha256sum, unzip or tar with bzip2
```

On a Linux x64 build host the build copies only the linux-x64 native libraries (about 400 MB less per build); the commands and the output
paths are the same. `dotnet build -p:VocaterraAllRuntimes=true` copies every platform's natives again (for a release build). See
[docs/game/architecture.md](architecture.md).

Run the host (the default bundle is `content/game-v1` in the current directory, else the game's bundle found above the executable; `--content <dir>`
names another):

```bash
# one line, evaluated once and printed (the form the tests and scripts use)
dotnet run --project src/Vocaterra.Host -- --transcript "quiero agua, por favor" --prompt stall-need
# a number mistake (noun.number), and the stall's other prompt: stall-offer, then the villager: villager-open
dotnet run --project src/Vocaterra.Host -- --transcript "quiero dos agua por favor" --prompt stall-need
dotnet run --project src/Vocaterra.Host -- --transcript "¿dónde está el río?" --prompt villager-open

# the text loop: shows each prompt (and the agent's line, when the prompt has one), then reads a line;
# /prompt <id> switches prompt, /quit leaves
dotnet run --project src/Vocaterra.Host
```

The host makes these calls of the core's API, in order, on the packages (the details are in
[docs/game/architecture.md](architecture.md)): `ContentLoader.Load` for the bundle; `PipelineServices` and `UtterancePipeline.Evaluate`
for each typed line; `ConversationContext.Create` for the utterance's context and `ConversationContext.Next` after it, which applies the core's
`RepeatCounts.Next` to the error tags. The host owns the world seed, the stored time (fixed for `--transcript`, the clock in the loop),
the learner model and the repeat counts.

## Continuous integration

`.github/workflows/ci.yml` runs on pushes to `main`, on pull requests and by hand: the job `build-and-test` builds with `-warnaserror`, runs the whole
suite without the speech models (`VOCATERRA_MODELS_DIR` unset, so the model-gated tests skip) and replays the grammar check from the committed cache; the
job `languagetool` checks the strings against a live LanguageTool server. Neither runs the content gate's lint at A2 or its CEFR check (the merge gate does).

**CI's case, measured at V24** (2026-11-02, `VOCATERRA_MODELS_DIR` set to an empty folder, `scripts/test.sh full`): 8091 passed, 45 skipped, 0 failed (137 s).
With the models, the same tree: 8133 passed, 3 skipped (150 s; before this milestone 8133 and 3). The six tests that time themselves, measured alone on this
machine (`dotnet test --filter`, the whole test's time as the runner reports it, which includes its set-up; the asserted measure is inside it), and four times
them for the GitHub runner (about four times slower; PR #20's lint took about 6 s there, against 1.6 s here at the time):

| Test | Measure here (whole test) | Budget | Four times the test | Likely on CI |
| --- | --- | --- | --- | --- |
| `GeneratedLexiconTests.LoadingTheBundleIsFastEnough` (median of three loads, asserted) | 1 s; the game's own loads 1.1 to 1.7 s | 3000 ms (median) | a load of 1.1 to 1.7 s is 4.4 to 6.8 s | fails |
| `SceneBuildTimeTests.Seed42SceneMatchesTheGoldenAndLoadsInUnderThreeSeconds` (build and digest, fastest of up to three) | 668 ms (the game's build 686 ms) | 3000 ms | about 2.7 s | at the edge |
| `WorldFactsTests.ASnapshotOfTheLargeWorldIsQuickAndAnswersEveryNounOfTheRoute` (copy under 1500 ms; six searches under 3000 ms) | 923 ms | 1500 ms; 3000 ms | about 3.7 s; the copy's share is not split out here | at risk |
| `ChunkIndexTests.ADigestOfALargeWorldIsCachedUntilTheNextEdit` (a digest after one edit, asserted under 100 ms) | 105 ms | 100 ms | about 0.4 s | at risk (the digest alone is a part of it) |
| `LintTests.TheBundleLintTakesUnderFiveSeconds` | 1 s | 5 s | about 4 s | under, narrowly |
| `GuideTurnTests.ATurnTakesPartOfAFrameAndNeverTheSimulationTick` (per turn) | 1 s for ten turns; about 8 ms a turn (V09) | 250 ms a turn | under | under |

The tests that wait for the worker's turns give up after 30 or 120 s, far above what a turn takes. At V09, under the full suite's load, the bundle load was
1438 of 3000 ms, the world snapshot 0.50 of 1.5 s and the scene build and digest 708 of 3000 ms; the first is the likely CI failure, the second and the third
the next. The timed tests were within four times of their budgets at V16 too (1676 ms, 1275 ms and 0.49 s then).

## The core's native libraries

The core depends on third-party native libraries, and one of them carries GPL-3.0 code (core D2); the game does not distribute a build that
includes them until D2 is settled in the core. The playtest folder (`scripts/playtest-build.sh`) leaves that native library out, and its check
says so; the owner decides whether handing the private folder to testers counts as distribution under D2.
