You are implementing the core milestone **M58, two learner slips the core misreads, and the `word_order` mutation's guard**, of the Vocaterra repository (C#, .NET 10). The language core is the `Vocaterra.Nlp` engine (`src/Vocaterra.Nlp`), the Spanish pack `src/Vocaterra.Nlp.Es` and the `Vocaterra.Nlp.*` test and tool projects; the game is `Vocaterra.{Game.Core,Game,Host,SpeechRender}`. **This milestone's work is in the core.** The owner asked on 2026-10-31 for M58 to land before the game's V15 (session 10, "El mercado"), whose prices and quantities a learner will ask with exactly these slips.

Do the work yourself; do not spawn subagents.

## Where to work, and pushing
Work only in the existing git worktree `/home/user/Dev/Vocaterra/.claude/worktrees/village`: cd there first, and never edit `/home/user/Dev/Vocaterra` itself or any other worktree. It is on branch `feature/village` (at the HEAD `git log -1` shows, `@@HEAD@@`).
- Create your branch with `git checkout -b M58-misreadings feature/village`.
- **The owner wants work pushed often.** Commit after each coherent step that builds and passes `scripts/test.sh fast`, with the prefix `M58: `. Push after every commit: `git push -u origin M58-misreadings` the first time, then `git push origin M58-misreadings`.
- Never push `main` or `feature/village`, and do not merge.

## The milestone (its row in `docs/nlp/IMPLEMENTATION_PLAN.md` §5, M58)
Found by the game's V11 (2026-10-31), each tried on the CLI at a prompt of the game bundle:
- **(a) `cuánto costa`** (the stem change missed) is read as the noun `costa`: it gives `gender.agree` (expected `cuánta`) and no intent, where `conj.irregular` expected `cuesta` is the slip (`cuánto costan los panes` is graded `conj.irregular` correctly). Read `costa` after `cuánto` (and wherever a verb is expected: `el pan costa dos monedas`) as the missed stem change of `costar`, so the intent resolves with `conj.irregular`. Keep the noun where it is one (`la costa`, `en la costa`, if the lexicon has it). Look for the same misreading in the other stem-changing verbs the bundle uses (`podo`/`pode`, `quero`, `tenes`, `dormo`, `jugo`…): fix those the same way if they collide with a word, or list them in the report.
- **(b) `hay pans`** gives `vocab.unknown` and no intent, where `noun.number` expected `panes` would let the intent resolve. A plural made with `-s` on a noun that takes `-es` (`pans`, `flors`, `relojs`) is a number slip of a known noun, not an unknown word; the intent resolves. Keep `vocab.unknown` for a real unknown word.
- **(c) The `word_order` mutation's guard, by the owner's decision (2026-10-31, "go with option 1"; the only narrowing this brief allows).** M57 found an older O5 failure (CsCheck seed `ff2aNwHuJBk8`, `MutationCase { Kind = WordOrder, Seed = 64247025744 }`, about one full fuzz run in eight; the fuzz README's "Found at M57 and left for a decision" entry). The guard reads the pattern's roles, not the words' readings, so it moves an adjective after a word that can itself be an adjective (`suelta plantar satisfecha`: `plantar` is a real adjective) and expects a `word_order` the core rightly does not give. Make the guard read the words' readings (no move when a word before the adjective can be a noun or an adjective by its readings, as the analyzer sees it), so the mutation never builds a sentence the core may read as correct. This narrows O4 and O5 for exactly that case and no other: say so in the README's list of narrowed oracles, with the owner's decision and date, and move the M57 entry there. Add a regression case on that seed (the mutation is now skipped or still flagged correctly), and run the fuzz project at five times its usual depth once to show nothing else changed.
- Regression tests in the owning module (the Spanish pack's analysis, `src/Vocaterra.Nlp.Es/Analysis/`, or the morphology/lexicon stage that owns the reading), each failing before the fix, with the clean neighbours (`cuánto cuesta el pan`, `cuánto cuestan los panes`, `hay panes`, `hay dos panes`, `hay un pan`) as tests too. The fuzz oracles unchanged or widened, never narrowed.
- `docs/nlp/API.md` stays true: where it lists what `conj.irregular`, `noun.number` and `vocab.unknown` report, add the new cases.

## Read first (the sections named, not whole files)
- `CLAUDE.md`.
- `docs/nlp/IMPLEMENTATION_PLAN.md` §3, §4, §6, the M56 to M62 rows of §5 and the M60 and M61 entries of §5b (models of a core milestone found by the game; `grep -n` for them).
- `docs/nlp/CONTRIBUTING.md`, `docs/nlp/API.md` (the error tags `ser_estar`, `conj.person`; "Tense errors" for the style), `docs/nlp/FUZZING_PLAN.md` (the oracles), `tests/Vocaterra.Nlp.Fuzzing.Tests/README.md` (bugs found, known limits, narrowed oracles).
- `docs/nlp/adr/0009-core-and-game-boundary.md` and ADR 0012 (the engine and the Spanish pack; `LayeringTests`).
- The code: whatever builds a word's readings (the lexicon lookup, the misspelling and stem-change candidates) and ranks them, `src/Vocaterra.Nlp.Es/Analysis/` (`SpanishAgreementChecker.cs`, `SpanishAnalysisTables.cs`), the stage that reports `vocab.unknown`, and their tests.
- The game's notes on these slips, so you know the sentences the content wants graded: `grep -n 'M58\|meanwhile' content/PHRASES.md` (do not read it whole; do not edit it).

## Rules
- A core milestone edits only the `Vocaterra.Nlp` and `Vocaterra.Nlp.*` projects, their tests, `tests/golden` and `docs/nlp/`. It does not edit the game's projects, `content/game-v1`, `content/locale`, `content/PHRASES.md` or `docs/game/`. If a core test on `content/game-v1` breaks because the content relied on the old grade, stop and report it.
- Fix bugs at the source, with a regression test in the owning module. **Never weaken or narrow a fuzz oracle**; widen it or leave it. The one exception is part (c), by the owner's decision, and only for the case it names. If the fuzz suite finds a new real bug, fix it or report it, and add it to the fuzz README's list.
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
- The CLI at the game's prompts: `¿cuánto costa el pan?` at `baker-open` gives `conj.irregular` with the price intent; `hay pans` at the baker's prompt gives `noun.number` with its intent; `¿cuánto cuesta el pan?`, `cuánto cuestan los panes` and `hay dos panes` stay clean, and `cuánto costan los panes` still gives `conj.irregular` (expected `cuestan`). (If a prompt id differs, find it with `grep`.)
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
- What was built for (a) and (b), and the other stem-change collisions found.
- The regression tests and what each gave before the fix.
- The CLI results of the gate's lines.
- Fuzz findings, if any; oracles changed (only widened, apart from (c)) or not.
- For (c): the guard's new rule, the regression case on seed `ff2aNwHuJBk8`, the README's narrowed-oracle entry, and the five-times-depth fuzz run's result.
- The test count before and after; goldens re-recorded and why each entry moved.
- Which of the game's "meanwhile" examples in `content/PHRASES.md` the core now grades, so the game can restore them.
- Anything left undone, and why.
