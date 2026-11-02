You are implementing the core milestone **M73, the small core points after the follow-ups**, of the Vocaterra repository (C#, .NET 10). The language core is the `Vocaterra.Nlp` engine (`src/Vocaterra.Nlp`), the Spanish pack `src/Vocaterra.Nlp.Es` and the `Vocaterra.Nlp.*` test and tool projects; the game is `Vocaterra.{Game.Core,Game,Host,SpeechRender}`. **This milestone's work is in the core.** The owner asked on 2026-11-02 for the small open points to be addressed. Each item is small; treat them one at a time, one commit each.

Do the work yourself; do not spawn subagents.

## Where to work, and pushing
Work only in the git worktree `/home/user/Dev/Vocaterra/.claude/worktrees/village`: cd there first, then `git checkout -b M73-small-core-points @@BASE@@` (the tip of `feature/village-followups`). Never edit `/home/user/Dev/Vocaterra` or any other worktree. Every heavy command goes through `scripts/limited.sh`, whose machine-wide lock serialises it.
- **The owner wants work pushed often.** Commit after each item that builds and passes its tests, with the prefix `M73: `. Push after every commit: `git push -u origin M73-small-core-points` the first time, then `git push origin M73-small-core-points`.
- Never push `main` or `feature/village-followups`, and do not merge. When you finish, leave the worktree on your branch, clean.
- **Disk is short (about 11 GB free).** Build only the projects you need: `tests/Vocaterra.Nlp.Tests`, `tests/Vocaterra.Nlp.Es.Tests`, `tests/Vocaterra.Nlp.Golden.Tests`, `tests/Vocaterra.Nlp.Fuzzing.Tests`, `tools/Vocaterra.Nlp.Cli`, `src/Vocaterra.Host`. Build the whole solution once at the end.

## The lines (measured by the orchestrator through the host on 2026-11-02, after M72 and V27)
```
farmer-open   cuántas cuesta todo     ask_total comm=true 0.85  noun.number expected 'cuestas' heard 'cuesta'   (reads cuesta as a noun)
farmer-open   cuántos cuestan todo    ask_total comm=true 0.6   noun.number 'cuánto' (heard cuántos), conj.person 'cuesta' (heard cuestan)  (M69's grading, the model for (a))
smith-open    ustedes trabajan mucho  ask_activity comm=true 1  (a statement read as "what do you do?", the trabajar keyword rule)
villager-open ustedes trabajan mucho  unknown, off-prompt ask_activity
smith-open    hoy hace buen tiempo    unknown, off-prompt ask_craft  (the weather read as "what do you make?")
villager-open cómo está usted         unknown, no off-prompt intent (greet takes neither this nor cómo estás)
villager-open cómo estás              unknown, no off-prompt intent
baker-open    dos panes es caro       unknown, correctness 1, no error  (a numeral subject is not checked against the verb)
baker-open    dos panes son caros     unknown, correctness 1  (the correct neighbour)
```

## The items
- **(a) `cuántas cuesta todo`** (M69's left point; M69's entry: `grep -n '^- \*\*M69' docs/nlp/IMPLEMENTATION_PLAN.md`): `cuesta` is the verb here, not the noun «slope». Grade it as M69 grades `cuántos cuestan todo`: the question word's error on `cuántas`, and no error on `cuesta`. Neighbours stay clean: `cuánto cuesta todo`, `cuánto cuestan las manzanas`, `la cuesta es grande` if the lexicon has the noun.
- **(b) The recorded fuzz families** (the fuzz project's README and the core plan's §5b by `grep -n` on each seed): `2tpLGUOSqj41` with `fSMdah3kOSd7` (O5, a `word_order` recast where a `[WORD]` slot binds the moved adjective), `8yc522XLB_W2` (O4, `word_order`: the mutation breaks the grammar match, `rica` read as an adjective, a false `conj.person`) and `3-VvCuMLFEA2` (O5, a new seed of the word-order family, from M72's run at five times the depth). The owner's word: these are fixed at the source where the analysis or the recast is wrong, **never narrowed**. A cause whose only fix would narrow an oracle is reported with its cause, and the seed stays recorded.
- **(c) M72's findings:**
  - O2 `7HIoK0InC7l7`: the analyzer reports `conj.person` twice on one word. A real bug: fix it with a regression test.
  - O3 slots `92moRWz9NQ54`: the grammar does not yield the expected `item=suyo` slot. Find the cause; fix it or report it.
  - `ustedes trabajan mucho` is a statement, not `ask_activity` (the keyword rule on `trabajar` alone, like M72's `hacer`); `¿trabajas mucho?`, `¿qué haces?` and `¿en qué trabajas?` keep what they have now unless they are wrong too: say which.
  - `hoy hace buen tiempo` is not `ask_craft`; `¿qué haces?` and the craft questions the lessons teach keep theirs.
  - `cómo está usted` and `cómo estás` are understood as `greet` (they are greetings; no lesson teaches them yet). Check what the NPCs' prompts that take `greet` then answer, and report it: the game's reply is the game's (V28), not yours.
  - `dos panes es caro`: a plural subject with a numeral and a singular verb gets `conj.person` (`son`), as a plural subject with an article does today (check what `los panes es caro` gives first); the adjective's number too if the analyzer already checks it after `ser`. `dos panes son caros` stays clean.
- **(d) V27's run without models:** O5 `26dVDqElv1l2`: the realiser draws the noun `son` («a tune») in `estómago son vencido` (pattern `[NOUN:thing] [PRON]? [NOUN:who] [ADJ]`), and the analyzer reads `son` as `ser`, so the recast differs. If the drawn sentence is one a reader would also take as `ser`, the generator re-rolls such a homograph as M62b's realiser re-rolls a name the analyzer reads as a noun (documented in the README as a generator fix with its reason, not an oracle narrowing); otherwise fix the analyzer. Say which and why.

For each line item: a regression test in the owning module that fails before the fix, and its clean neighbours as tests. Put the lines in `scripts/try-lines/m73.txt` (`prompt|line`, no spaces around `|`) with the neighbours, and run `scripts/try-lines.sh --host` on them before your first edit, so the report can show before and after.

## Content
If a fix is in `content/game-v1/intents/intents.json` (a keyword rule, a grammar, a `greet` example), that is the content this milestone may edit; no lesson, prompt text, dialogue or NPC file. Then run `scripts/content-loop.sh --grammar --speech` directly (never under `scripts/limited.sh`), with `--record` if the goldens move, and `--tier full` once. After a grammar refresh, check that the cache rebuilt from nothing equals the committed one: `scripts/languagetool.sh start`; `rm content/grammar/game-v1-cache.json`; `scripts/content.sh grammar content/game-v1 --strings content/grammar/game-v1-strings.jsonl --strings-only --strict`; `git diff --exit-code content/grammar/`; stop the server.

## Proof
- **`scripts/try-lines.sh --check` over every line file.** Update the `.expected` files in their own commit and list each changed row by kind.
- **The identity baseline** may move. Re-record it with the golden project alone, in its own commit, and list the changes by kind: first `VOCATERRA_RECORD_GOLDEN=1 VOCATERRA_RECORD_IDENTITY=1 scripts/test.sh project tests/Vocaterra.Nlp.Golden.Tests`, then the same command again without the variables. Say if a golden moves.
- **A game test that pinned an old value** may be changed (expected value only, with a one-line comment naming M73). List each; change nothing else in the game.

## Fuzz (a widening, never a narrowing)
- Read `docs/nlp/FUZZING_PLAN.md` and the fuzz project's README by `grep -n`. Update the README's bugs-found and known-limits lists for what you fix or leave.
- A failure on a seed this milestone does not fix is rerun once after checking the seed. Any new failure is a finding: fix it if it is this milestone's, otherwise report it.

## Read first (the sections named, not whole files)
- `CLAUDE.md`.
- `docs/nlp/IMPLEMENTATION_PLAN.md`: §3, §6, the M69, M72 and M73 rows of §5, M69's and M72's entries in §5b (`grep -n '^- \*\*M69\|^- \*\*M72'`).
- `docs/nlp/API.md` by `grep -n '^## \|^### '`: the analysis, intent resolution, the verdict.
- The code each item leads you to, and its tests.

## Rules
- A core milestone edits only the `Vocaterra.Nlp` and `Vocaterra.Nlp.*` projects and their tests, `tests/golden`, `docs/nlp/`, `scripts/try-lines/`, the intent catalog as above, and a game test's pinned value as above. It does not edit the game's code, `docs/game/` or `docs/qa-reports/`.
- Fix at the source. **Never weaken or narrow a fuzz oracle.**
- **Never flag correct Spanish.** Try a dozen natural, correct lines of your own at the NPCs' open prompts and at lesson prompts (names, greetings, `usted`/`ustedes`, the weather, prices, plural subjects with numbers), and list them.
- Determinism: no `System.Random`, no clock, ordinal comparisons.
- Do not edit either `IMPLEMENTATION_PLAN.md` or either `DECISIONS.md`. Never create or edit anything under `.github/workflows/`.

## Output and context (the cost of a milestone is its turns times its context)
- Read the sections named, not whole files; prefer `grep -n`, and keep `sed -n` ranges under about 150 lines.
- Send every heavy command's output to a log file under `/tmp/claude-1000/m73/` and print at most 30 lines of it.
- Wait for a long command by running it in the foreground, not by polling a log. **Never leave a wait loop or a background process behind** (a `while pgrep -f …` loop matches its own command line and never ends).

## Resources (the desktop has crashed from overload)
- Run every `dotnet restore|build|test|format|run` through `scripts/limited.sh` (builds `-m:4`, tests `-m:2`). Never start two heavy commands at once: no `&`, no background builds.
- **Never run `scripts/test.sh`, `scripts/try-lines.sh` or `scripts/content-loop.sh` under `scripts/limited.sh`:** they call it themselves. Run them directly.
- While iterating, use `scripts/test.sh project <test project>`. Run `scripts/test.sh full` once before reporting (or the content loop's `--tier full` if you edited content).
- Exit code 137 means the 6 GB cap killed the command: lower the parallelism, and never work around the cap.

## Environment
- Prefix every command inline with `PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1`.
- The sandbox refuses a command line that contains the words `eval` or `hash`. Use a wrapper script under `/tmp/claude-1000/m73/` for those (a CLI `eval` run); `scripts/try-lines.sh` holds them inside it.
- Model-gated tests read `VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models`. The host: `dotnet src/Vocaterra.Host/bin/Debug/net10.0/Vocaterra.Host.dll --transcript "<line>" --prompt <id>`.

## Your checks before reporting (from the worktree root)
- `scripts/limited.sh dotnet build Vocaterra.sln -warnaserror -m:4`, 0 warnings; the full suite, all passing (it was 8212 passed and 3 skipped).
- The fuzz project three times at the default depth and once at five times (`VOCATERRA_FUZZ_ITERATIONS=15000 VOCATERRA_FUZZ_ITERATIONS_GRAMMAR=10000 scripts/test.sh project tests/Vocaterra.Nlp.Fuzzing.Tests`), and each fixed seed alone (`CsCheck_Seed=<seed>`).
- `scripts/limited.sh dotnet format Vocaterra.sln --verify-no-changes`.
- The core demo: `yo quiere dos espada` at `villager-open` still yields exactly `conj.person` and `noun.number`.
- `scripts/try-lines.sh --check` as in "Proof".
- Do not commit `bin/`, `obj/` or `artifacts/`.

## Commit
Each message ends with:
```
Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDfGVmxEaTFXNb87eD1
```

## Report (one page, under 450 words)
- The branch and the final commit SHA, pushed.
- Each item: fixed (where, how, the test, what it gave before) or reported (the cause, and why the only fix would narrow an oracle).
- The `try-lines` rows that changed, by kind; your dozen correct lines.
- The identity entries and goldens that moved; the game tests changed (if any); the bundle hash if content moved.
- Fuzz: the runs (time and result), the seeds fixed and still recorded, new findings.
- The test count before and after; anything left undone, and why.

Before your final message, make sure no process you started is still running (`pgrep -fa 'dotnet|limited.sh|content-loop'`, kill only your own) and that the worktree is clean and pushed.
