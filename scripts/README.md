# Scripts

Repository helper scripts, for the language core and the game. All are bash and can be run from any directory.

Heavy commands (dotnet restore, build, test, format, run; `content.sh`; the speech prerender) are run through `limited.sh`, which
serialises them machine-wide and caps their memory and CPU; see its header.

## limited.sh

`scripts/limited.sh <command> [args...]` runs one heavy command at a time on the machine (a lock shared by every worktree), waits while
memory is short, and runs it in a systemd user scope with a 6 GB memory cap, no swap and 10 of 16 cores. Exit status 137 means the cap
killed the command. Use `-m:4` for builds and `-m:2` for tests.

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
also prints both runs' wall times and counts side by side, as [docs/TEST_PERFORMANCE_PLAN.md](../docs/TEST_PERFORMANCE_PLAN.md) reports
each phase. `--top 0` prints the assemblies alone.

## content.sh

Runs the language core's content tool on a game content bundle (default `content/game-v1`):

- `validate` checks every file's hash and the cross-references, and prints the bundle hash.
- `hash` rewrites `manifest.json` from the files on disk, the step after any edit. Options after the bundle directory are passed on, so a
  new bundle id is set with `hash content/game-v1 --bundle-id game-v1`; without the option the manifest keeps its id.
- `cefr-check` lists dialogue-template and prompt-text lemmas missing from a CEFR list (`cefr-check content/game-v1 --level a2`). It only
  prints (it exits 0 whatever it lists); `refresh` and the merge gate fail on a lemma it lists at A2.
- `lint`, `grammar`, `pack` and `verify-pack` are passed through as [docs/nlp/TOOLS.md](../docs/nlp/TOOLS.md) describes.
- `refresh` is the whole loop after an edit of the game's content ([content/README.md](../content/README.md), "The edit loop"). Its lint
  and CEFR checks are the gate's: the bundle's ceiling is A2 (VILLAGE_PLAN 3.9, D-game-22 (b), V08), so the lint runs with
  `--strict --level a2 --fail-on-level --host-pools 'guide.*'` and `cefr-check --level a2` fails on any lemma it lists, while
  `cefr-check --level a1` is a report of what is above A1 and never fails. Each lesson's own level is the host's `--validate-content`.

The tool is the `Vocaterra.Nlp.ContentBuilder` project under `tools/`, run from source with `dotnet run`, so it is always the core at the
current commit. Needs bash and the .NET SDK (`~/.dotnet` is used when present).

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

Downloads the two data sets the lexicon import reads into `${VOCATERRA_LEXICON_SOURCES_DIR:-lexicon-sources}/` (git-ignored): the kaikki.org Spanish extract of English Wiktionary (about 1 GB of JSONL) and `es_50k.txt` of hermitdave/FrequencyWords at a pinned commit. The URLs, byte sizes and SHA-256 hashes are pinned in the script; a file that is already there and matches is kept, a download that does not match is deleted and the script stops (the kaikki.org URL is not versioned, so a mismatch means upstream replaced the file; re-pinning is described in [docs/nlp/LEXICON.md](../docs/nlp/LEXICON.md)). It then writes `sources.json` next to the files with URL, hash, size, licence and retrieval date, which `Vocaterra.Nlp.ContentBuilder import-lexicon --sidecar` copies into the sidecar of the generated lexicon. `--check` verifies the files that are present and downloads nothing. Needs bash, python3, curl and sha256sum (or shasum). Downloaded files are data: nothing in them is ever executed.

## fetch-tools.sh and languagetool.sh

`fetch-tools.sh` installs the grammar check's development tools (the Temurin JRE and LanguageTool, pinned in `tools/manifest.json`) under
`~/.local/share/vocaterra/tools`; `languagetool.sh start|stop|status` runs the local LanguageTool server that `content.sh grammar` calls.
Both are described in [docs/nlp/TOOLS.md](../docs/nlp/TOOLS.md).

## license-scan.sh

Checks the licence of every NuGet package in `Vocaterra.Nlp.slnf` (the core projects: the engine `Vocaterra.Nlp`, the Spanish pack `Vocaterra.Nlp.Es`, their tests and the three tools), direct and transitive, as listed by `dotnet list package --include-transitive`. Each package's cached `.nuspec` is read first; packages without an SPDX expression there are looked up in the NuGet registration API unless `--offline` is given, which restricts the scan to the local cache (`NUGET_PACKAGES` or `~/.nuget/packages`). The solution must be restored first. The script prints a table with a status per package and exits 1 when any package is GPL, LGPL, AGPL or SSPL, is not on `license-allowlist.txt`, or has no determinable licence. The allowlist holds one SPDX identifier per line, matched as a whole token and case-insensitively, so `MIT` does not accept `MIT-0`; a line containing spaces is matched as a complete expression. Add an entry only after reading the licence text of the package it would accept. The game's own third-party packages (MonoGame, FontStashSharp) are not in the core filter.

Packages whose metadata names no usable licence can be covered by `license-overrides.txt`, one `<package-id>|<version>|<spdx>|<evidence-url>|<reviewed-on>` line per package version. An override applies only to the exact id (case-insensitive) and version on its line, is used only after the nuspec and the API have failed to name a licence, and must cite an https evidence URL. Its SPDX value is still checked against the allowlist, and the table reports its source as `override`. Malformed lines stop the scan with exit status 2. When a package is updated, its override no longer matches and the scan reports it again.

## check-links.sh

Checks the relative Markdown links in every `.md` file of the repository (build output, fetched models, agent worktrees and gate logs excluded). A link is checked when it has no URL scheme and is not a bare `#anchor`; its target, without any `#fragment`, must exist in the working tree. Links inside code fences and code spans are ignored, and nothing is fetched. It prints each broken link as `file:line: target` and the count of links checked, and exits 1 when any link is broken. Run it as `scripts/check-links.sh` from anywhere in the repository; `--quiet` prints only the broken links and the summary when there are some.

## identity-check.sh

`scripts/identity-check.sh <base-ref>` checks the Phase A gate rules (research section 4.9) without building: the game bundle's
`bundleHash` equals the one at `<base-ref>`, and no file under `tests/*/Data/`, `tests/golden/` or `content/` differs from it, except a
pure rename (R100). Exit 0 when both hold; 1 names each failing rule and file. It runs no build, test or content tool. The Spanish
identity test itself (`SpanishIdentityTests`) is run by the test suite; see `tests/golden/README.md`.

## The game's scripts

- `prerender-speech.sh` renders the NPCs' speakable lines of `content/game-v1` with Kokoro (`tools/Vocaterra.SpeechRender`); `--check` reports what would be rendered and writes nothing.
- `playtest-build.sh <folder>` builds the private linux-x64 playtest folder (the game, the bundle, the Vosk model, the tester's README and launcher from `scripts/playtest/`) and checks it.
- `playtest-report.sh <session folders>` summarises the session logs the playtest writes.
