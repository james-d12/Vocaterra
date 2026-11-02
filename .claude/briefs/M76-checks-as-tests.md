You are implementing the core milestone **M76, checks as C# tests**, of the Vocaterra repository (C#, .NET 10). The language core is the `Vocaterra.Nlp` engine (`src/Vocaterra.Nlp`), the Spanish pack `src/Vocaterra.Nlp.Es` and their tests; the game is `Vocaterra.{Game.Core,Game,Host}` and `tools/Vocaterra.SpeechRender`. This milestone touches tests and `scripts/`, not product code.

Do the work yourself; do not spawn subagents.

## Why (the owner, 2026-11-02)
> "I would prefer things like this to be a csharp unit test, not some throwaway script or junk text file. This just adds to the general tech debt."

`scripts/try-lines.sh` grades `prompt|line` rows through the core's CLI (`tools/Vocaterra.Nlp.Cli`, its eval subcommand, on `content/game-v1`) or, with `--host`, through the game's host (`src/Vocaterra.Host --transcript`, which first takes out a greeting and the listener's name). It compares each row with committed text baselines.
- **Its files:** `scripts/try-lines/`, 9 line files (`review1`, `review2`, `m64`, `m66`, `m67`, `m69`, `m70`, `m72`, `m73`), 540 rows in all, each file with a header naming its items. Each has a `.expected` baseline (the CLI) and a `.host.expected` baseline (the host).
- **The trouble:** neither the gate nor CI runs it. The owner wants such checks to be tests that `dotnet test` runs, and the script and its files gone.

## Where to work, and pushing
Work only in the git worktree `/home/user/Dev/Vocaterra/.claude/worktrees/village`: cd there first, then `git checkout -b M76-checks-as-tests @@BASE@@` (the tip of `feature/village-followups`). Never edit `/home/user/Dev/Vocaterra` or any other worktree.
- **The owner wants work pushed often.** Commit after each step that builds and passes its tests, with the prefix `M76: `. Push after every commit: `git push -u origin M76-checks-as-tests` the first time, then `git push origin M76-checks-as-tests`.
- Never push `main` or `feature/village-followups`, and do not merge. When you finish, leave the worktree on your branch, clean.
- **Disk is short (about 11 GB free).** Build only the projects you need while iterating.

## (a) The try-lines rows as tests
1. **Read the rows and baselines first** (`head` each file, then a short script of your own under `/tmp/claude-1000/m76/` to merge rows with their two baselines). Count:
   - the unique (prompt, line) pairs;
   - the rows whose CLI and host results are the same;
   - the rows already asserted by an existing test with the same expectation (grep the line's text in `tests/`).
2. **Core cases** go in the core's test projects, beside the tests that already grade a line at an NPC's prompt on `content/game-v1`:
   - see `tests/Vocaterra.Nlp.Tests/Pipeline/` (`OffPromptLineTests.cs`, `VillageQaLineTests.cs`, `ReportedVerbReadingTests.cs`, `TestSupport.cs`);
   - follow their fixture and their way of loading the bundle and building the pipeline (the same path as the CLI's eval);
   - load the bundle once per class or collection, not per case.
3. **Host cases** go in `tests/Vocaterra.Host.Tests`, through the code behind `--transcript`, with that project's helpers (`GameContent.cs`, `OffPromptTests.cs`, `GameTurnTests.cs`).
   - Keep a host case only where the host's path matters: a line with a greeting or the listener's name, or a row whose host result differs from the CLI's.
   - A row that is the same through both paths is a core case only.
4. **The shape:**
   - One assertion per row: the intent, communicated or not, and each error tag with its expected word, exactly what the baseline says (`-` means no error).
   - **Group by topic, never by milestone.** For example: prices and amounts; person agreement; gender and number; word order; questions; greetings and names; tenses; lines a prompt does not take (off-prompt); near misses. Each class name says what it guards.
   - Each case shows the prompt and the line in its display name, so a failure names the line.
   - Typed expectations in C# (`TheoryData` or `MemberData` with a small record; your design), no data files.
   - A one-line comment on a group where the old file's header explains why the lines are there (for example "correct Spanish: must give no error").
5. **Deduplicate:**
   - one case per (prompt, line, path);
   - drop a row an existing test already asserts with the same expectation, and count the drops;
   - a row whose baseline disagrees with an existing test is a finding: report it, do not pick one silently.
6. **Speed:** time each new class (`scripts/test.sh project …`). A class that takes more than a few seconds alone is marked `[Trait(TestSpeed.Trait, TestSpeed.Slow)]` (`docs/TEST_PERFORMANCE_PLAN.md`, grep `Slow`). No models are needed.
7. **Prove the tests bite:** change one expected word and one intent temporarily, see each fail with a message that names the line, and revert. Do not commit that.
8. **Then delete** `scripts/try-lines.sh` and `scripts/try-lines/` (`git rm`). Make every document that tells a reader to use them say what to do now:
   - `docs/nlp/CONTRIBUTING.md` (about lines 228 and 229);
   - `scripts/README.md`, if it lists the script;
   - `grep -rn try-lines docs/ scripts/ *.md`.
   - Leave the history in the findings logs, `.claude/briefs/` and `docs/qa-reports/` as it is: they record what happened.

## (b) An audit of the other scripts
For each script in `scripts/` that is a check or a one-off analysis rather than tooling:
- **What it does,** and **who uses it:** grep the docs, `.claude/agents/`, `.claude/briefs/`, `scripts/merge-gate.sh`, `.github/workflows/ci.yml` and the other scripts.
- **One verdict:**
  - **test:** it becomes a C# test in the owning project;
  - **keep:** real tooling (say why);
  - **delete:** obsolete, nothing uses it.

At least these:
- `scripted-runs.sh` with `compare-runs.py`;
- `core-move-scan.py`, `audit-usings.py`;
- `identity-check.sh`, `check-links.sh`, `test-timings.py`;
- `playtest-report.sh` and `scripts/playtest/`.

Leave these alone as tooling: `limited.sh`, `test.sh`, `merge-gate.sh`, `content.sh`, `content-loop.sh`, `fetch-*.sh`, `license-*`, `languagetool.sh`, `prerender-speech.sh`, `playtest-build.sh`.

**What to do with each verdict:**
- **Do** the straightforward ones:
  - delete a one-off nothing uses, with the docs that mention it;
  - turn a small check into a test when it needs no game window, no models and no network, and takes under about an hour.
- **Propose only, with an estimate, and do not build:**
  - anything large, such as the scripted runs, which launch the game and compare screenshots;
  - anything `scripts/merge-gate.sh` or `.github/workflows/ci.yml` calls;
  - anything a document the QA agent follows (`.claude/agents/qa.md`) or a game document requires.

## Rules
- This brief lets M76 edit the core's and the game's test projects (`tests/Vocaterra.Nlp*.Tests`, `tests/Vocaterra.Host.Tests`, `tests/Vocaterra.Game*.Tests` only if (b) needs it), `scripts/`, `docs/nlp/CONTRIBUTING.md`, `scripts/README.md`, and documents that name a script you delete.
- **No product code:** nothing under `src/` or `tools/`, no content. A test that cannot be written without a product change is a finding: report it.
- Do not edit `scripts/merge-gate.sh`, either `IMPLEMENTATION_PLAN.md`, either `DECISIONS.md`, `docs/HANDOVER_*.md`, `.claude/agents/`, or anything under `.github/workflows/`.
- Every new test passes on the current code. The baselines matched the code at V28's gate (`81023b2`): a row that no longer matches is a finding to report, not a test to bend.
- Determinism: no `System.Random`, no clock, ordinal comparisons.

## Output and context (the cost of a milestone is its turns times its context)
- Read the sections named, not whole files. Prefer `grep -n`, and keep `sed -n` ranges under about 150 lines. Generate the case tables with a script rather than by hand, then read and tidy what it wrote.
- Send every heavy command's output to a log file under `/tmp/claude-1000/m76/` and print at most 30 lines of it.
- Wait for a long command by running it in the foreground, not by polling a log. **Never leave a wait loop or a background process behind.**

## Resources (the desktop has crashed from overload)
- Run every `dotnet restore|build|test|format|run` through `scripts/limited.sh` (builds `-m:4`, tests `-m:2`). Never start two heavy commands at once: no `&`, no background builds.
- **Never run `scripts/test.sh`, `scripts/try-lines.sh` or `scripts/content-loop.sh` under `scripts/limited.sh`:** they call it themselves.
- While iterating, use `scripts/test.sh project <test project>`. Run `scripts/test.sh full` once before reporting.
- Exit code 137 means the 6 GB cap killed the command: lower the parallelism, and never work around the cap.

## Environment
- Prefix every command inline with `PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1`.
- The sandbox refuses a command line that contains the words `eval` or `hash`. Use a wrapper script under `/tmp/claude-1000/m76/` for those.
- Model-gated tests read `VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models`.

## Read first
- `CLAUDE.md`.
- `scripts/try-lines.sh` (its header and how it turns output into a result line).
- The test files named in (a), and `docs/TEST_PERFORMANCE_PLAN.md` by `grep -n Slow`.

## Your checks before reporting (from the worktree root)
- `scripts/limited.sh dotnet build Vocaterra.sln -warnaserror -m:4`, 0 warnings.
- The full suite, all passing (it was 8286 passed and 3 skipped).
- `scripts/limited.sh dotnet format Vocaterra.sln --verify-no-changes`.
- `bash scripts/check-links.sh` clean, if it still exists.
- `grep -rn 'try-lines' docs/ scripts/ CLAUDE.md` shows no instruction to use the deleted script.
- Do not commit `bin/`, `obj/` or `artifacts/`.

## Commit
Each message ends with:
```
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDfGVmxEaTFXNb87eD1
```

## Report (one page, under 450 words)
- The branch and the final commit SHA, pushed.
- **(a):**
  - the counts: rows read, unique pairs, core cases, host cases, dropped as duplicates or already tested;
  - the classes, by topic, with their case counts and times;
  - the rows that disagreed or no longer matched (findings);
  - the documents changed.
- **(b):** a table of script, what it does, who uses it, verdict, and done or proposed (with an estimate).
- The test count before and after; anything left undone, and why.

Before your final message, make sure no process you started is still running (`pgrep -fa 'dotnet|limited.sh'`, kill only your own) and that the worktree is clean and pushed.
