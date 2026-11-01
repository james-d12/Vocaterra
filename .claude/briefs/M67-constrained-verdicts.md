You are implementing the core milestone **M67, what a constrained prompt's answer set tells the verdict**, of the Vocaterra repository (C#, .NET 10). The language core is the `Vocaterra.Nlp` engine (`src/Vocaterra.Nlp`), the Spanish pack `src/Vocaterra.Nlp.Es` and the `Vocaterra.Nlp.*` test and tool projects; the game is `Vocaterra.{Game.Core,Game,Host,SpeechRender}`. **This milestone's work is in the core.** It is a follow-up after PR #20's merge (`docs/HANDOVER_VILLAGE_PHASE_B.md` section 9.3 C; read it) with two bugs of the QA pass `docs/qa-reports/2026-11-01-language.md` (QA-language-5 and 10; read them). The game's free-talk milestone V20 waits on it.

Do the work yourself; do not spawn subagents.

## Where to work, and pushing
Work only in the git worktree `/home/user/Dev/Vocaterra/.claude/worktrees/m65` (reused, so its build is reused): cd there first, then `git checkout -b M67-constrained-verdicts @@BASE@@` (M66's final commit; it is being merged into `feature/village-followups`). Never edit `/home/user/Dev/Vocaterra`, the worktree `.claude/worktrees/village` or any other worktree. **Other agents may run at the same time**; every heavy command goes through `scripts/limited.sh`, whose machine-wide lock makes yours wait for theirs.
- **The owner wants work pushed often.** Commit after each coherent step that builds and passes its tests, with the prefix `M67: `. Push after every commit: `git push -u origin M67-constrained-verdicts` the first time, then `git push origin M67-constrained-verdicts`.
- Never push `main` or `feature/village-followups`, and do not merge.
- **Disk is short (about 10 GB free; each core project's build output is about 400 MB).** Build only the projects you need: `tests/Vocaterra.Nlp.Tests`, `tests/Vocaterra.Nlp.Es.Tests`, `tests/Vocaterra.Nlp.Golden.Tests`, `tests/Vocaterra.Nlp.Fuzzing.Tests`, `tools/Vocaterra.Nlp.Cli`, `src/Vocaterra.Host` and its tests, and `tests/Vocaterra.Game.Core.Tests` only if a game test pinned the old score. Never the whole solution: the orchestrator's gate builds and tests it afterwards.

## The milestone
**Measure on the host as well as the CLI** (`scripts/try-lines.sh --host`, the game's path). Put the milestone's lines in `scripts/try-lines/m67.txt` (`prompt|line`) and run them before your first edit, so the report can show before and after.

### (a) `vocab.wrong` only on a line that was not understood (handover 9.3 C)
- **Problem:** correct, understood lines are scored as small mistakes (correctness 0.4), in free talk and at lesson steps: `hola luz quiero dos panes` at `baker-open`, `usted comprará pan` at a lesson step, `buscaré madera para luz` at `smith-open` (a recast and "a small mistake"), `los panes cuestan dos monedas` and `puedo pagar con fruta y con pan` at `farmer-open`, `me llamo domingo` at `smith-open`.
- **Cause:** `VerdictAssembler.DeriveVocabWrong` (`src/Vocaterra.Nlp/Evaluation/VerdictAssembler.cs`, about line 219) adds `vocab.wrong` on the first content word whenever a constrained prompt's answer set has no match, even when a slot grammar understood the line and resolved an intent.
- **Fix:** derive it only when the line resolved no intent, or when it is a near miss of one answer (a fuzzy match below the threshold). Never on a line a grammar understood: the derived tag explains a miss, and an understood line has none.

### (b) A verb's form against the nearest answer (QA-language-5 and 10)
- **Problem:** at a constrained lesson prompt whose answer set holds the target form, a line that differs from the nearest answer only in a verb's form is taken as that answer with no error, so the lesson's own point is praised wrong. Reproduced through the host by the orchestrator (correctness 1, «¡Eso es!»):
  - `Luz compra pescado` at `lesson-luz-bought` (asked «¿Qué compró Luz ayer?», answer `luz compró pescado`);
  - `compro tres manzanas` at `lesson-market` (asked «¿Qué compraste en el mercado?»);
  - `sí fue al río` at `lesson-water-went` (asked «¿Fuiste al río?»; expected `fui`).
- **Fix:** when the nearest answer differs from the line only in verb forms of the same lemma, report `conj.tense` (another tense) or `conj.person` (another person, same tense) on that word with the answer's form as expected, in the tag's usual shape (`docs/nlp/API.md`, "Tense errors", "Person and copula errors"). Find where the answer match accepts the near answer (`src/Vocaterra.Nlp/Language/Matching/AnswerMatcher.cs`, `TextGrader.cs`, `UtterancePipeline.cs`) and decide where the comparison belongs; say why.
- **The owner's rule on accents (D-game-22, 2026-11-01, item 3):** a typed form that differs only by a written accent that marks tense or person (`compre` for `compré`, `compro` for `compró`) is **not** this error: it stays accepted with no deduction (M70 adds the note). So compare the forms with their accents: `compro` against `compró` is an accent, `compra` against `compró` is a tense. Say how you tell them apart.
- **Clean:** every alias of each answer set at those prompts; `ayer luz compró pescado`; `sí, fui al río`; `compré tres manzanas`; and an answer with a different verb that a lesson accepts by its grammar.
- The fuzz suite: a widening where O4 can model it (a constrained prompt's answer with one verb mutated), never a narrowing.

### (c) Tooling: `scripts/limited.sh` re-entrant (this brief allows that one file)
On 2026-11-01 an agent ran `scripts/limited.sh scripts/test.sh …`; `test.sh` calls `limited.sh` itself, so the outer call held the machine-wide lock while the inner one waited for it forever (21 minutes, every other agent's build queued behind it). `limited.sh` already sets `VOCATERRA_LIMITED=1` for its command (its header, step 5). Make it re-entrant: when `VOCATERRA_LIMITED=1` is already set, it runs the command directly (it is already inside the scope and holds the lock) and prints one line saying so. `scripts/content-loop.sh` keeps its own refusal. Test it by hand: `scripts/limited.sh scripts/test.sh project <a small test project>` finishes. One commit, prefix `M67:`.

## Proof
- `scripts/try-lines.sh --check` over every line file: the lines that change are exactly (a)'s understood lines (their `vocab.wrong` gone) and (b)'s; lines that were not understood keep their `vocab.wrong`. Update the `.expected` files in their own commit and list every changed line in the report.
- The goldens and the identity baseline will move: re-record them with the golden project alone (`VOCATERRA_RECORD_GOLDEN=1 VOCATERRA_RECORD_IDENTITY=1 scripts/test.sh project tests/Vocaterra.Nlp.Golden.Tests`, then again without the variables), in their own commit, and list each change.
- **A game test that pinned the 0.4** may be changed, as at M56 and M62: change only the expected number with a one-line comment naming M67, list each in the report, and change nothing else in the game.

## Read first (the sections named, not whole files)
- `CLAUDE.md`.
- `docs/HANDOVER_VILLAGE_PHASE_B.md` section 9.3 C (`grep -n '^\*\*C\. ' docs/HANDOVER_VILLAGE_PHASE_B.md`, about 20 lines).
- `docs/qa-reports/2026-11-01-language.md`: QA-language-5 and 10, and the 9.3 C lines under "Observations".
- `docs/game/DECISIONS.md`: D-game-22's "Owner's answers, 2026-11-01 (on the QA passes …)" (grep it).
- `docs/nlp/IMPLEMENTATION_PLAN.md`: §3, §6, the M67 row of §5.
- `docs/nlp/API.md`: section 5 "Evaluate an utterance and read the verdict", "The words and the answer match", "Tense errors", "Person and copula errors" (`grep -n '^## \|^### '`).
- The code: `VerdictAssembler.cs`, `AnswerMatcher.cs`, `TextGrader.cs`, the pipeline's use of them, and their tests; the fuzz suite's O6 (answer matching) and O10 (verdict assembly) models, which must follow the new rules.

## Rules
- A core milestone edits only the `Vocaterra.Nlp` and `Vocaterra.Nlp.*` projects, their tests, `tests/golden`, `docs/nlp/` and `scripts/try-lines/`, plus `scripts/limited.sh` for (c); the one game exception is above. It does not edit `content/`, `docs/game/` or `docs/qa-reports/`.
- Fix at the source with regression tests in the owning module, each failing before the fix, with the clean neighbours as tests too. **Never weaken or narrow a fuzz oracle**: O6 and O10 change because the documented rules change, so update their models to the new rules and say so; that is not a narrowing.
- **Never flag correct Spanish.** Try a dozen natural, correct lines of your own at the lesson prompts and the NPCs' open prompts, and list them.
- A failure on one of the recorded fuzz seeds alone (`2tpLGUOSqj41`, `8yc522XLB_W2`) is rerun once after checking the seed; any other failure is a finding.
- Determinism: no `System.Random`, no clock, ordinal comparisons.
- Do not edit either `IMPLEMENTATION_PLAN.md` or either `DECISIONS.md`. Never create or edit anything under `.github/workflows/`.
- Update `docs/nlp/API.md` (the derived `vocab.wrong`, the new tags at constrained prompts) and add a dated note to `docs/nlp/adr/0009-core-and-game-boundary.md` only if the public surface changes.

## Output and context (the cost of a milestone is its turns times its context)
- Read the sections named, not whole files; prefer `grep -n`, and keep `sed -n` ranges under about 150 lines.
- Send every heavy command's output to a log file under `/tmp/claude-1000/m67/` and print at most 30 lines of it.
- Wait for a long command by running it in the foreground through `scripts/limited.sh`, not by polling a log.

## Resources (the desktop has crashed from overload)
- Run every `dotnet restore|build|test|format|run` through `scripts/limited.sh` (builds `-m:4`, tests `-m:2`). Never start two heavy commands at once: no `&`, no background builds.
- **Never run `scripts/test.sh` or `scripts/content-loop.sh` under `scripts/limited.sh`:** both call it themselves, and the nested call waits for ever on the lock the outer one holds. Run them directly.
- While iterating, use `scripts/test.sh project <test project>`. Do not run `scripts/test.sh fast` or `full` (they build the whole solution).
- Exit code 137 means the 6 GB cap killed the command: lower the parallelism, and never work around the cap.

## Environment
- Prefix every command inline with `PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1`.
- The sandbox refuses a command line that contains the words `eval` or `hash`; `scripts/try-lines.sh` holds them inside it.
- Model-gated tests read `VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models`.

## Your checks before reporting (from the worktree root, each through scripts/limited.sh)
- Each project above builds with `-warnaserror`, 0 warnings, and its tests pass (`scripts/test.sh project <name>`); the fuzz project three times at the default depth and once at five times (`VOCATERRA_FUZZ_ITERATIONS=15000 VOCATERRA_FUZZ_ITERATIONS_GRAMMAR=10000`).
- `dotnet format --verify-no-changes` on each project you changed.
- The core demo: `yo quiere dos espada` at `villager-open` still yields exactly `conj.person` and `noun.number`.
- `scripts/try-lines.sh --check` as in "Proof".
- Do not commit `bin/`, `obj/` or `artifacts/`. The orchestrator's gate then runs the whole solution.

## Commit
Each message ends with:
```
Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
```

## Report (one page, under 400 words)
- The branch and the final commit SHA, pushed.
- (a) and (b): what was built, where, the rule's limits (the accent rule of (b)), the tests and what each gave before.
- The `try-lines` lines that changed, each explained; your dozen correct lines.
- The goldens and identity entries that moved; the game tests changed (if any).
- Fuzz: O6 and O10's models, any widening, the four runs (time, result), new findings.
- The test counts of the projects you ran, before and after.
- Anything left undone, and why.

Before your final message, make sure no process you started is still running (`pgrep -fa 'dotnet|limited.sh'`, kill only your own) and that the worktree is clean and pushed.
