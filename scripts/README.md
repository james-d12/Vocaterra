# Scripts

Repository helper scripts, for the language core and the game. All are bash and can be run from any directory.

Heavy commands (dotnet restore, build, test, format, run; `content.sh`; the speech prerender) are run through `limited.sh`, which
serialises them machine-wide and caps their memory and CPU; see its header.

## limited.sh

`scripts/limited.sh <command> [args...]` runs one heavy command at a time on the machine (a lock shared by every worktree), waits while
memory is short, and runs it in a systemd user scope with a 6 GB memory cap, no swap and 10 of 16 cores. Exit status 137 means the cap
killed the command. Use `-m:4` for builds and `-m:2` for tests. It sets `VOCATERRA_LIMITED=1` for its command, so a script that calls
`limited.sh` itself (`content-loop.sh`) can refuse to run under it instead of waiting for ever on the lock.

## test.sh

Runs the test suite, or a tier of it, as the gate does (the build with `-m:4`, the tests with `-m:2`, both through `limited.sh`):

- `fast` runs every test that is not marked slow (`--filter "Speed!=Slow"`): the step-by-step check. It leaves out the fuzz suite, the
  model-gated tests, the world-generation tests, the full-bundle CLI and lint tests, the process-spawning tests and the few long single
  tests, and names them at the end.
- `full` runs the whole suite, as the merge gate and CI do: the check at a milestone and before a merge.
- `project <name>` runs one test project in full (`project Vocaterra.Game.Core.Tests`, or a project's path).

A test is marked slow with `[Trait(TestSpeed.Trait, TestSpeed.Slow)]` on its class or method (the constant is in `tests/Shared/TestSpeed.cs` for
the core and `tests/Vocaterra.Game.Core.Tests/TestSupport/TestSpeed.cs` for the game). Options after the mode go to `dotnet test`: `--no-build`
skips the build, and a `--filter` is combined with the fast tier's. Model-gated tests run when `VOCATERRA_MODELS_DIR` is set or the shared
models folder exists. The TRX files go to `.claude/test-timings/<mode>-<stamp>/` (git-ignored), and the run ends with each assembly's wall time
and counts.

## test-timings.py

`python3 -I scripts/test-timings.py RUN [BASE] [--top N]` reads the TRX files of a run (a folder, as `dotnet test --logger trx
--results-directory RUN` or `test.sh` writes them) and prints each assembly's wall time and counts, the run's wall time (first start to last
finish, so time spent queued in `limited.sh` is not counted), the N slowest tests and the N classes with the most test time. With `BASE` it
also prints both runs' wall times and counts side by side, as [docs/process/test-performance-plan.md](../docs/process/test-performance-plan.md) reports
each phase. `--top 0` prints the assemblies alone.

## content.sh

Runs the language core's content tool on a game content bundle (default `content/game-v1`):

- `validate` checks every file's hash and the cross-references, and prints the bundle hash.
- `hash` rewrites `manifest.json` from the files on disk, the step after any edit. Options after the bundle directory are passed on, so a
  new bundle id is set with `hash content/game-v1 --bundle-id game-v1`; without the option the manifest keeps its id.
  `scripts/rehash-bundle.sh` runs `hash` and then `validate` through `limited.sh` and prints the new hash (B29c).
- `cefr-check` lists dialogue-template and prompt-text lemmas missing from a CEFR list (`cefr-check content/game-v1 --level a2`). It only
  prints (it exits 0 whatever it lists); `refresh` and the merge gate fail on a lemma it lists at A2.
- `lint`, `grammar`, `pack` and `verify-pack` are passed through as [docs/nlp/tools.md](../docs/nlp/tools.md) describes.
- `refresh` is the whole loop after an edit of the game's content ([content/README.md](../content/README.md), "The edit loop"). Its lint
  and CEFR checks are the gate's: the bundle's ceiling is A2 (village-plan 3.9, D-game-22 (b), V08), so the lint runs with
  `--strict --level a2 --fail-on-level --host-pools 'guide.*'` and `cefr-check --level a2` fails on any lemma it lists, while
  `cefr-check --level a1` is a report of what is above A1 and never fails. Each lesson's own level is the host's `--validate-content`.
  `content-loop.sh` (below) runs it as its first step.

The tool is the `Vocaterra.Nlp.ContentBuilder` project under `tools/`, run from source with `dotnet run`, so it is always the core at the
current commit. Needs bash and the .NET SDK (`~/.dotnet` is used when present).

## content-loop.sh

`scripts/content-loop.sh [--grammar] [--speech] [--record] [--tier fast|full|none]` is the whole loop after an edit of the game's content in one
command (V18): a content milestone's brief says "run `scripts/content-loop.sh --grammar --speech`" after each edit. **Run it directly, never
under `limited.sh`**: it calls `limited.sh` itself for each heavy step (each `dotnet`, `content.sh` and prerender call) and `test.sh` directly,
and the one lock `limited.sh` holds would make the loop wait for ever on its own first step. It refuses to start under `limited.sh` (exit 3):
`limited.sh` sets `VOCATERRA_LIMITED=1` for its command, and the loop also sees the lock inherited as an open file.

In order, it runs: 1 `content.sh refresh`; 2 with `--grammar`, the LanguageTool steps (start the server, check the strings the cache lacks with
`--prune`, check that a cache rebuilt from nothing equals it as CI's `languagetool` job does, print every new finding, stop the server, even on a
failure); 3 the renderer's `--check` and, with `--speech`, the render of the new or changed lines and a second refresh; 4 nothing to write
(B29c: no test pins the bundle hash); 5 the build with `-warnaserror -m:4`; 6 the end-to-end goldens and the
identity baseline, checked, or with `--record` (off by default) recorded, with the `git diff --stat` and the changed entries per file printed;
7 the fuzz suite's `FuzzLanguageStreamTests`, whose digests run on frozen inputs since the core's M57, so a content edit does not move them: the
step checks that they did not move (a move means a generator or the fixture changed; it prints the new digests and the constants they differ
from, and the script edits no C#); 8 the tier: `fast` (the default) is `test.sh fast`, then the fuzz suite's grammar-sensitive classes at their full depth
(`CorrectSentenceFuzzTests`, `MutationTagFuzzTests`, `RecastLoopFuzzTests`); `full` is the whole suite, as the gate; `none` runs no test.

It stops at the first failure with the next thing to do and ends with one summary of at most 30 lines: each step, passed, failed, skipped or not
run, its time, what it changed and its log. Each step's whole output is in `.claude/content-loop/<stamp>/` (git-ignored; the ten newest runs are
kept), never on the terminal. Exit 0 when every step passed, 1 when one failed or the stream digests are left to set, 2 on a usage error. It runs
the gate's content checks unchanged, and on an unedited tree it changes no file. The header lists the options and the steps.

## merge-gate.sh

The orchestrator's gate, run from a feature branch's checkout (never `main`, which it refuses):
`GATE_TARGET=<feature branch> scripts/merge-gate.sh "<label>:<branch>" ...` merges each branch into it with `--no-ff` (aborting that
merge on a conflict), then runs on `Vocaterra.sln` the build with warnings as errors, the whole test suite (core and game; model-backed
tests when `VOCATERRA_MODELS_DIR` is set or the shared models folder exists), the format check, `content.sh validate`, the game bundle's
content gate, its grammar check, and the core's `license-scan.sh --offline`. The content gate (since V08) is the lint at A2, the bundle's
ceiling, with `--fail-on-level`, then `cefr-check --level a2`, which fails the gate on any lemma it lists, then `cefr-check --level a1` as a
report that never fails, then the host's `--validate-content`, which holds each lesson to its own level; its log line says which is which.
Each check's log is kept under `.claude/gate-logs/`.
It prints the counts and `result: PASS` or `result: FAIL`, and exits non-zero on a failure.

## fetch-models.sh

Downloads the models listed in `models/manifest.json` into `${VOCATERRA_MODELS_DIR:-models}/<id>/`, checks the archive SHA-256, extracts it and verifies every listed file against its pinned hash. `--list` reports which models are present, missing, mismatching or unpinned without downloading anything, and model ids on the command line limit the work to those models. Entries without an archive are skipped with a warning because their ONNX export is manual. Needs bash, python3, curl, sha256sum (or shasum) and unzip or tar with bzip2.

## fetch-lexicon-sources.sh

Downloads the two data sets the lexicon import reads into `${VOCATERRA_LEXICON_SOURCES_DIR:-lexicon-sources}/` (git-ignored): the kaikki.org Spanish extract of English Wiktionary (about 1 GB of JSONL) and `es_50k.txt` of hermitdave/FrequencyWords at a pinned commit. The URLs, byte sizes and SHA-256 hashes are pinned in the script; a file that is already there and matches is kept, a download that does not match is deleted and the script stops (the kaikki.org URL is not versioned, so a mismatch means upstream replaced the file; re-pinning is described in [docs/nlp/lexicon.md](../docs/nlp/lexicon.md)). It then writes `sources.json` next to the files with URL, hash, size, licence and retrieval date, which `Vocaterra.Nlp.ContentBuilder import-lexicon --sidecar` copies into the sidecar of the generated lexicon. `--check` verifies the files that are present and downloads nothing. Needs bash, python3, curl and sha256sum (or shasum). Downloaded files are data: nothing in them is ever executed.

## fetch-tools.sh and languagetool.sh

`fetch-tools.sh` installs the grammar check's development tools (the Temurin JRE and LanguageTool, pinned in `tools/manifest.json`) under
`~/.local/share/vocaterra/tools`; `languagetool.sh start|stop|status` runs the local LanguageTool server that `content.sh grammar` calls.
Both are described in [docs/nlp/tools.md](../docs/nlp/tools.md).

## license-scan.sh

Checks the licence of every NuGet package in `Vocaterra.Nlp.slnf` (the core projects: the engine `Vocaterra.Nlp`, the Spanish pack `Vocaterra.Nlp.Es`, their tests and the three tools), direct and transitive, as listed by `dotnet list package --include-transitive`. Each package's cached `.nuspec` is read first; packages without an SPDX expression there are looked up in the NuGet registration API unless `--offline` is given, which restricts the scan to the local cache (`NUGET_PACKAGES` or `~/.nuget/packages`). The solution must be restored first. The script prints a table with a status per package and exits 1 when any package is GPL, LGPL, AGPL or SSPL, is not on `license-allowlist.txt`, or has no determinable licence. The allowlist holds one SPDX identifier per line, matched as a whole token and case-insensitively, so `MIT` does not accept `MIT-0`; a line containing spaces is matched as a complete expression. Add an entry only after reading the licence text of the package it would accept. The game's own third-party packages (MonoGame, FontStashSharp) are not in the core filter.

Packages whose metadata names no usable licence can be covered by `license-overrides.txt`, one `<package-id>|<version>|<spdx>|<evidence-url>|<reviewed-on>` line per package version. An override applies only to the exact id (case-insensitive) and version on its line, is used only after the nuspec and the API have failed to name a licence, and must cite an https evidence URL. Its SPDX value is still checked against the allowlist, and the table reports its source as `override`. Malformed lines stop the scan with exit status 2. When a package is updated, its override no longer matches and the scan reports it again.

## Checks that are tests now

The relative-link check of the Markdown documents is `MarkdownLinkTests` (`tests/Vocaterra.Nlp.Tests/Documents/`); the graded lines of Spanish are the `Line*Tests` classes (`tests/Vocaterra.Nlp.Tests/Pipeline/Lines/`, and `tests/Vocaterra.Host.Tests/LineHostPathTests.cs` for the host's path). `dotnet test` runs them (M76). `check-links.sh`, `try-lines.sh` and the Phase A `identity-check.sh` are gone; the identity test itself (`SpanishIdentityTests`) was always in the suite, see `tests/golden/README.md`.

## The game's scripts

- `prerender-speech.sh` renders the NPCs' speakable lines of `content/game-v1` with Kokoro (`tools/Vocaterra.SpeechRender`); `--check` reports what would be rendered and writes nothing.
- `playtest-build.sh <folder>` builds the private linux-x64 playtest folder (the game, the bundle, the Vosk model, the tester's README and launcher from `scripts/playtest/`) and checks it.
- `playtest-report.sh <session folders>` summarises the session logs the playtest writes.
