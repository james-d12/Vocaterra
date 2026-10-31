You are implementing the core milestone **M56, three agreement checks that are silent where a learner errs**, of the Vocaterra repository (C#, .NET 10). The language core is the `Vocaterra.Nlp` engine (`src/Vocaterra.Nlp`), the Spanish pack `src/Vocaterra.Nlp.Es` and the `Vocaterra.Nlp.*` test and tool projects; the game is `Vocaterra.{Game.Core,Game,Host,SpeechRender}`. **This milestone's work is in the core.** The owner asked on 2026-10-31 for M56 and M59 to land before the game's V14 (session 8, the preterite for every person, with named villagers as subjects) and M58 before V15 (session 10, the market); M59 depends on M56.

Do the work yourself; do not spawn subagents.

## Where to work, and pushing
Work only in the existing git worktree `/home/user/Dev/Vocaterra/.claude/worktrees/village`: cd there first, and never edit `/home/user/Dev/Vocaterra` itself or any other worktree. It is on branch `feature/village` (at the HEAD `git log -1` shows, `@@HEAD@@`).
- Create your branch with `git checkout -b M56-agreement feature/village`.
- **The owner wants work pushed often.** Commit after each coherent step that builds and passes `scripts/test.sh fast`, with the prefix `M56: `. Push after every commit: `git push -u origin M56-agreement` the first time, then `git push origin M56-agreement`.
- Never push `main` or `feature/village`, and do not merge.

## The milestone (its row in `docs/nlp/IMPLEMENTATION_PLAN.md` §5, M56)
Found by the game's V10 and V11 (2026-10-30 and 31), each tried on the CLI at a prompt of the game bundle:
- **(a) `ser` with a location adverb or a location question:** `es lejos`, `es cerca`, `¿dónde es la herrería?` give no `ser_estar` (expected `está`). Keep `ser` correct where it is (`la fiesta es en la plaza`, an event's place, if the core distinguishes it; `es lejano` an adjective; `¿dónde es?` of an event). Say what you decided for the event reading and why.
- **(b) a plural verb before a singular subject, and the reverse:** `¿dónde están la herrería?` gives no `conj.person` (expected `está`); `¿cuánto cuestan el pan?` and `cuánto cuesta los panes` give none either (expected `cuesta` and `cuestan`); and V13 found the same with `gustar`, whose subject follows it: `me gusta las manzanas` (expected `gustan`) and `me gustan el pan` (expected `gusta`) give nothing. A postverbal subject must be matched to the verb: `me gustan las manzanas`, `me gusta el pan`, `¿dónde están las casas?`, `cuestan dos monedas` (no subject) and `hay dos panes` (`haber` is impersonal) stay clean.
- Regression tests in the owning module (the Spanish pack's analysis, `src/Vocaterra.Nlp.Es/Analysis/`), each failing before the fix, with the clean neighbours above as tests too. The fuzz oracles unchanged or widened, never narrowed.
- `docs/nlp/API.md` stays true: where it lists what `ser_estar` and `conj.person` report, add the new cases.

## Read first (the sections named, not whole files)
- `CLAUDE.md`.
- `docs/nlp/IMPLEMENTATION_PLAN.md` §3, §4, §6, the M56 to M61 rows of §5 and the M60 and M61 entries of §5b (models of a core milestone found by the game; `grep -n` for them).
- `docs/nlp/CONTRIBUTING.md`, `docs/nlp/API.md` (the error tags `ser_estar`, `conj.person`; "Tense errors" for the style), `docs/nlp/FUZZING_PLAN.md` (the oracles), `tests/Vocaterra.Nlp.Fuzzing.Tests/README.md` (bugs found, known limits, narrowed oracles).
- `docs/nlp/adr/0009-core-and-game-boundary.md` and ADR 0012 (the engine and the Spanish pack; `LayeringTests`).
- The code: `src/Vocaterra.Nlp.Es/Analysis/SpanishAgreementChecker.cs`, `SpanishAnalysisTables.cs` and whatever else reports `ser_estar` and `conj.person`; their tests.
- The game's notes on these slips, so you know the sentences the content wants graded: `grep -n 'M56\|meanwhile' content/PHRASES.md` (do not read it whole; do not edit it).

## Rules
- A core milestone edits only the `Vocaterra.Nlp` and `Vocaterra.Nlp.*` projects, their tests, `tests/golden` and `docs/nlp/`. It does not edit the game's projects, `content/game-v1`, `content/locale`, `content/PHRASES.md` or `docs/game/`. If a core test on `content/game-v1` breaks because the content relied on the old grade, stop and report it.
- Fix bugs at the source, with a regression test in the owning module. **Never weaken or narrow a fuzz oracle**; widen it or leave it. If the fuzz suite finds a new real bug, fix it or report it, and add it to the fuzz README's list.
- Determinism: no `System.Random`, no clock.
- Do not edit either `IMPLEMENTATION_PLAN.md` or either `DECISIONS.md`. Never create or edit anything under `.github/workflows/`.
- The end-to-end goldens may move because the core now grades a learner line differently: re-record them (`VOCATERRA_RECORD_GOLDEN=1`, `tests/Vocaterra.Nlp.Golden.Tests`) in their own commit, and list in the report every entry that moved and why it is now right.

## Output and context (the cost of a milestone is its turns times its context)
- Read the sections named, not whole files; prefer `grep -n` to find a place, and keep `sed -n` ranges under about 150 lines.
- Send every heavy command's output to a log file under `/tmp/claude-1000/` and print at most 30 lines of it (the summary, `grep` for failures).
- Wait for a long command by running it in the foreground through `scripts/limited.sh`, not by polling a log.
- Run the fuzz suite's project (`scripts/test.sh project tests/Vocaterra.Nlp.Fuzzing.Tests`) as soon as the checker changes, not only at the full run.

## Resources (the desktop has crashed from overload)
- Run every `dotnet restore|build|test|format|run` through `scripts/limited.sh` (builds `-m:4`, tests `-m:2`). Never start two heavy commands at once: no `&`, no background builds.
- While iterating, use `scripts/test.sh project <test project>` or `scripts/test.sh fast`. Run `scripts/test.sh full` once before reporting.
- Exit code 137 means the 6 GB cap killed the command: lower the parallelism, and never work around the cap.

## Environment
- Prefix every command inline with `PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1`.
- The sandbox refuses a command line that contains the words `eval` or `hash`. Use a wrapper script under `/tmp/claude-1000/` for a CLI `eval` run (`dotnet run --project tools/Vocaterra.Nlp.Cli -- eval --transcript "<text>" --prompt <prompt> --content content/game-v1`).
- Model-gated tests read `VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models`.

## Gate (from the worktree root, each through scripts/limited.sh)
- `dotnet build Vocaterra.sln -warnaserror -m:4`, with 0 warnings.
- `dotnet test Vocaterra.sln -m:2` all passing (it was @@COUNT@@); report the new count.
- `dotnet format Vocaterra.sln --verify-no-changes`.
- `bash scripts/license-scan.sh --offline` PASSED.
- The core demo: `dotnet run --project tools/Vocaterra.Nlp.Cli -- eval --transcript "yo quiere dos espada" --prompt villager-open --content content/game-v1` still yields exactly `conj.person` and `noun.number` (through the wrapper).
- The CLI at the game's prompts: `es lejos` at `lesson-far` gives `ser_estar`; `¿dónde están la herrería?` at `fisher-open` and `¿cuánto cuestan el pan?` at `baker-open` give `conj.person`; `me gustan las manzanas` at the farmer's prompt and `¿dónde está la panadería?` at `fisher-open` stay clean. (If a prompt id differs, find it with `grep`.)
- `scripts/content.sh validate` passes (the bundle is unchanged).
- Do not commit `bin/`, `obj/` or `artifacts/`.

## Commit
Each message ends with:
```
Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDdyPc3eAsDgGyBVKv6
```

## Report (one page, under 400 words)
- The branch and the final commit SHA, pushed.
- What was built for (a) and (b), and the event-place decision.
- The regression tests and what each gave before the fix.
- The CLI results of the gate's lines.
- Fuzz findings, if any; oracles changed (only widened) or not.
- The test count before and after; goldens re-recorded and why each entry moved.
- Which of the game's "meanwhile" examples in `content/PHRASES.md` the core now grades, so the game can restore them.
- Anything left undone, and why.
