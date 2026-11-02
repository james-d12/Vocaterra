You are implementing the core milestone **M65, fuzz stability**, of the Vocaterra repository (C#, .NET 10). The language core is the `Vocaterra.Nlp` engine (`src/Vocaterra.Nlp`), the Spanish pack `src/Vocaterra.Nlp.Es` and the `Vocaterra.Nlp.*` test and tool projects; the game is `Vocaterra.{Game.Core,Game,Host,SpeechRender}`. **This milestone's work is in the core's fuzz suite, plus one script under `scripts/`.** It is the first of the follow-ups after PR #20's merge (`docs/HANDOVER_VILLAGE_PHASE_B.md` section 9.3 A, read it). It goes first because each open fuzz failure can turn a gate or CI red at random.

Do the work yourself; do not spawn subagents.

## Where to work, and pushing
Work only in the git worktree `/home/user/Dev/Vocaterra/.claude/worktrees/m65`, already on your branch `M65-fuzz-stability` (from `feature/village-followups` at `9dfa08b`): cd there first, and never edit `/home/user/Dev/Vocaterra`, the worktree `.claude/worktrees/village` or any other worktree. **Other agents run at the same time** (QA testers in the `village` worktree); every heavy command goes through `scripts/limited.sh`, whose machine-wide lock makes yours wait for theirs, so expect a command to wait at times.
- **The owner wants work pushed often.** Commit after each coherent step that builds and passes its tests, with the prefix `M65: `. Push after every commit: `git push -u origin M65-fuzz-stability` the first time, then `git push origin M65-fuzz-stability`.
- Never push `main` or `feature/village-followups`, and do not merge.
- **Disk is short (about 12 GB free) and each core project's build output is about 400 MB.** Build only the projects you need, never the whole solution: `tests/Vocaterra.Nlp.Fuzzing.Tests`, `tools/Vocaterra.Nlp.Cli` and `src/Vocaterra.Host` (for `--host`). The orchestrator's gate builds and tests the whole solution in the `village` worktree afterwards.

## The milestone
The owner answered on 2026-11-01: **yes to the narrowings (a), (b) and (c); (d) stays recorded and is not narrowed.** These are the only narrowings this milestone may make. Each is listed in `tests/Vocaterra.Nlp.Fuzzing.Tests/README.md` under "Known, deliberately not fixed" as "left for a decision" (lines about 665 to 694); move each one you guard to "Where an oracle is narrower than the plan", dated 2026-11-01, "the owner's yes (M65)", in the style of the M26b entries there.

Each guard gets a test that pins how narrow it is, as `AmountMutationGuardTests` does (read it): the guard skips the failing seed's case, and keeps a close neighbour that does not have the property. Make each guard as narrow as the cause allows.

### (a) `egVgLELrxyL4`, O4 (about one fuzz run in six at the default depth)
The `conj.tense` mutation lacks the guard M26b gave the `conj.person` mutation for a grammar that has both an alternation and a name (M26b, seed `1RTZdjQ2tFb2`; README lines about 102 and 665 to 671). Give the `conj.tense` mutation the same guard (share the predicate rather than copying it).

### (b) `b3HSKSLCsL_1` and `4YuZ2yubxdC1`, O5 (now at the default depth too)
M59's `conj.person` mutation after a noun subject swaps `haber`: `unas televisiones han` becomes `ha`, `el subtítulo ha` becomes `han`. The analyzer leaves `haber` out of the person check after a noun subject by design (`hay dos panes`). The mutation takes the same exclusion, so it models what the analyzer claims to check. The generator also builds `haber` with no participle, which is not Spanish; say in the report whether the realiser should stop doing that, and do it only if it is a small change that does not narrow O3 for correct Spanish. Write in the README the later widening (when a lesson teaches the perfect: `ha` and `han` checked against a noun subject when a participle follows, and the realiser gives `haber` its participle).

### (c) `2ldq2rIKLrG2`, O4 (at five times the depth)
The `conj.tense` mutation where a grammar's literal decided a reading: with `ayer` added the grammar no longer matches and `porque` reads as a noun. Keep a mutated case only when it matches the same grammar as the original.

### (d) not narrowed
`2tpLGUOSqj41` (O5, `word_order` beside a `[WORD]` slot) stays under "left for a decision"; add one line there: "the owner, 2026-11-01: leave it recorded".

### (e) A skip budget (not a narrowing)
The sentence realiser re-rolls whole sentence classes (`skip:coordinated-subject`, `literal-verb-subject`, `colour-before-noun`, `noun-reads-as-adjective`, `ser-of-place`, `predicate-subject`, `time-marker-of-generated-words` and the rest counted by `SentenceGen` and `SentenceRealizer`); one of them hid the review's R1 (`tú y yo fuimos` graded wrong). The diagnostics already exist (`Generation/SentenceModel.cs`, `HomographSkipsAreRare`, used by O3 to O5).
- Measure every skip kind's share of the draws over a fixed-seed run at the default depth, and put the table in the README.
- Add a test (not `Speed=Slow` unless it takes more than a few seconds) that fails when any one kind exceeds its budget: 5 % of the draws by default. A kind already above 5 % gets its measured share plus a small margin as its own budget, named in the test with a one-line reason, and is listed in the report for the owner.
- A new skip kind with no budget falls under the default, so a future re-roll cannot silently eat a class of sentence.

### (f) `scripts/try-lines.sh` (tooling; this brief allows `scripts/`)
Every milestone of the review used two throwaway wrappers: `/tmp/claude-1000/review/batch.sh` (the CLI, with `fmt.py`) and `host1.sh` (the host, the game's path, which takes out a greeting and the listener's name first). Turn them into one script in the repository:
- `scripts/try-lines.sh [--host] <file>...`: each file holds `prompt|line` rows (blank lines and `#` comments skipped); one result line per row, printed in a stable form (prompt, line, intent, communicated, error tags with their expected forms), so two runs can be compared with `diff`. `--host` runs the host's path instead of the CLI.
- It runs the built Debug binaries and says clearly, with a non-zero exit, when they are missing. It needs only bash and `python3 -I`. Callers run it through `scripts/limited.sh`.
- The line files go beside it, in `scripts/try-lines/`: the review's correct lines (`/tmp/claude-1000/review/lines1.txt` and `lines2.txt`, 99 rows) and M64's (`lines3.txt`), with a header comment saying they are correct Spanish and must stay clean, apart from the known items listed in the file (the `vocab.wrong` at constrained prompts that M67 removes, `¿puede usted vender…?` that M66 fixes).
- Commit a baseline of each file's output (`scripts/try-lines/*.expected`), so a later brief can say "`scripts/try-lines.sh` gives the baseline, apart from the lines this milestone fixes" with one command.
- The sandbox refuses a command line that contains the word `eval`; a script file may contain it, so the CLI's subcommand lives inside the script.
- Two lines in `docs/nlp/CONTRIBUTING.md` on how to use it.

## Read first (the sections named, not whole files)
- `CLAUDE.md`.
- `docs/HANDOVER_VILLAGE_PHASE_B.md` section 9.3 A (`grep -n '^\*\*A\. Fuzz' docs/HANDOVER_VILLAGE_PHASE_B.md`, about 40 lines).
- `tests/Vocaterra.Nlp.Fuzzing.Tests/README.md`: "Running", "Pushing even deeper for a one-off soak", "Reproducing a failure", the M26b entries of "Where an oracle is narrower than the plan" (lines about 90 to 105), and "Known, deliberately not fixed" from line about 660. Do not read it whole.
- `docs/nlp/IMPLEMENTATION_PLAN.md` §3 and §6, and the M65 row of §5 (`grep -n '^| M65'`).
- The code: the mutation catalogue of O4 and O5 (`grep -rn 'ConjTense\|ConjPerson' tests/Vocaterra.Nlp.Fuzzing.Tests --include=*.cs -l`), M26b's guard, `AmountMutationGuardTests`, `Generation/SentenceModel.cs`, and the skip counts in `Generation/SentenceGen.cs` and `SentenceRealizer.cs`.

## Rules
- A core milestone edits only the `Vocaterra.Nlp` and `Vocaterra.Nlp.*` projects, their tests, `tests/golden` and `docs/nlp/`; this one also adds `scripts/try-lines.sh` and `scripts/try-lines/`. It does not edit the game's projects, `content/`, `docs/game/` or `docs/qa-reports/`.
- **Only the three narrowings the owner allowed.** Never weaken an oracle otherwise. If a fuzz run finds a new real bug, fix it at the source with a regression test in the owning module if it is small, or record it in the README's list with its seed and report it.
- Determinism: no `System.Random`, no clock, ordinal comparisons.
- Do not edit either `IMPLEMENTATION_PLAN.md` or either `DECISIONS.md`. Never create or edit anything under `.github/workflows/`.

## Output and context (the cost of a milestone is its turns times its context)
- Read the sections named, not whole files; prefer `grep -n` to find a place, and keep `sed -n` ranges under about 150 lines.
- Send every heavy command's output to a log file under `/tmp/claude-1000/m65/` and print at most 30 lines of it (the summary, `grep` for failures).
- Wait for a long command by running it in the foreground through `scripts/limited.sh`, not by polling a log.
- Reproduce each seed before the fix and after it (the README's "Reproducing a failure").

## Resources (the desktop has crashed from overload)
- Run every `dotnet restore|build|test|format|run` through `scripts/limited.sh` (builds `-m:4`, tests `-m:2`). Never start two heavy commands at once: no `&`, no background builds.
- While iterating, use `scripts/test.sh project tests/Vocaterra.Nlp.Fuzzing.Tests`. Do not run `scripts/test.sh fast` or `full` (they build the whole solution); the gate runs the full suite.
- Exit code 137 means the 6 GB cap killed the command: lower the parallelism, and never work around the cap.

## Environment
- Prefix every command inline with `PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1`.
- The sandbox refuses a command line that contains the words `eval` or `hash`.
- Model-gated tests read `VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models`.

## Your checks before reporting (from the worktree root, each through scripts/limited.sh)
- `dotnet build tests/Vocaterra.Nlp.Fuzzing.Tests -warnaserror -m:4`, `dotnet build tools/Vocaterra.Nlp.Cli -warnaserror -m:4` and `dotnet build src/Vocaterra.Host -warnaserror -m:4`, each with 0 warnings.
- `dotnet format tests/Vocaterra.Nlp.Fuzzing.Tests/Vocaterra.Nlp.Fuzzing.Tests.csproj --verify-no-changes`.
- **The proof:** the fuzz project passes three runs in a row at its default depth, and one run at five times its default depth (`VOCATERRA_FUZZ_ITERATIONS=15000 VOCATERRA_FUZZ_ITERATIONS_GRAMMAR=10000`, the other variables at their defaults). Report each run's time and test count. A failure on a seed that is none of (a) to (d) is a finding: report its seed and family.
- The core demo: `yo quiere dos espada` at `villager-open` still yields exactly `conj.person` and `noun.number` (through `scripts/try-lines.sh`).
- `scripts/try-lines.sh` over its three line files gives the same results as `/tmp/claude-1000/review/batch.sh` over the same files, and `--host` the same as `host1.sh`. Those two wrappers cd into the `village` worktree and run its binaries; the core's source is the same there (this milestone changes no core source), so the results must match. Do not edit the wrappers.
- `bash scripts/license-scan.sh --offline` PASSED if it does not need the whole solution built; otherwise leave it to the gate and say so.
- Do not commit `bin/`, `obj/` or `artifacts/`.
- The orchestrator's gate then runs the whole solution's build, tests (7420 passed and 3 skipped before this milestone, with models), format, content and licence checks.

## Commit
Each message ends with:
```
Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
```

## Report (one page, under 400 words)
- The branch and the final commit SHA, pushed.
- For (a) to (c): the guard, where it is, how narrow, the test that pins it, the seed before and after.
- (e): the table of skip shares, the budgets above 5 % and why.
- (f): the script's usage line and the files added.
- The four fuzz runs (time, result, count); any new finding.
- The fuzz project's test count before and after.
- Anything left undone, and why.
