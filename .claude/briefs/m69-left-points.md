You are implementing the core milestone **M69, the left points of the core's village milestones**, of the Vocaterra repository (C#, .NET 10). The language core is the `Vocaterra.Nlp` engine (`src/Vocaterra.Nlp`), the Spanish pack `src/Vocaterra.Nlp.Es` and the `Vocaterra.Nlp.*` test and tool projects. **This milestone's work is in the core.** It is the last core follow-up after PR #20's merge (`docs/HANDOVER_VILLAGE_PHASE_B.md` section 9.3 B, its last part, and the M69 row of `docs/nlp/IMPLEMENTATION_PLAN.md` §5). Every item is a **missed error** or a fuzz finding, never a false alarm, so the first rule is: **no correct Spanish may become flagged.**

Do the work yourself; do not spawn subagents.

## Where to work, and pushing
Work only in the git worktree `/home/user/Dev/Vocaterra/.claude/worktrees/m65` (it holds a full build): cd there first, then `git checkout -b M69-left-points 6985fae` (the tip of `feature/village-followups`). Never edit `/home/user/Dev/Vocaterra`, the worktree `.claude/worktrees/village` or any other worktree. **Other agents run at the same time** (V21, a game milestone, in the `village` worktree); every heavy command goes through `scripts/limited.sh`, whose machine-wide lock makes yours wait for theirs.
- **The owner wants work pushed often.** Commit after each item that builds and passes its tests, with the prefix `M69: `. Push after every commit: `git push -u origin M69-left-points` the first time, then `git push origin M69-left-points`.
- Never push `main` or `feature/village-followups`, and do not merge. When you finish, leave the worktree on your branch, clean.
- **Disk is short (about 8 GB free).** Iterate with the test projects you change; do not build the whole solution more than once.

## The items (each with regression tests in `tests/Vocaterra.Nlp.Es.Tests` or the owning module, failing before the fix, with clean neighbours as tests too; one commit each)
Read each milestone's "Left" or "Known limit" lines in §5b first (`grep -n '^- \*\*M56\|^- \*\*M58\|^- \*\*M62\|^- \*\*M66\|^- \*\*M71' docs/nlp/IMPLEMENTATION_PLAN.md`), and measure every line on the host as well as the CLI (`scripts/try-lines.sh` and `--host`); put them in `scripts/try-lines/m69.txt` and run them before your first edit.
1. **M56's left points:** `son cerca` and `ser en` with a place (`la tienda es en el pueblo` gives `está`; `la fiesta es en la plaza` stays clean: an event takes `ser`), and the first and second persons after the verb (`¿dónde estás yo?`-type inversions; decide what M56's note means and say so).
2. **M58's left points:** `podo` and `esto` (as `estoy`) are read as the slip only under a grammar; read them as the slip without one where no other reading exists (`esto` is also a demonstrative: `esto es pan` stays clean).
3. **M62's left points:** `él es alta` with no grammar and no `muy` (the adjective that is also a noun after the copula), `son` after a lone name (`luz son alta`), and the gender of a subject with `de …` (`la casa de los niños es bonito`). Only the number of a `de …` subject is checked today.
4. **Question words and quantities** (V20 and QA-language-10): `cuanto son todo?` (the question word without its accent) is understood but `son` is not corrected; `cuántos cuestan todo` does not check the quantity word (`cuánto`). Grade both as `¿cuánto es todo?` and `¿cuánto cuesta todo?` are graded since M66.
5. **Fuzz findings:**
   - the realiser never draws `buenas tardes`, which the analyzer accepts (`tardes` is a form of `tardar`, so `PreverbalPhrase` takes `buenas` as a subject and re-rolls it as `literal-verb-subject`; M71's README entry): let the realiser draw it (a widening of O3);
   - M67b's O5 seed `deXmFwx4GzL4` (the recast `miedo un tu una exposición` keeps a gender error; it reproduces since M70, which recorded it in the fuzz README): find the cause and fix it at the source, or record why it is the recast's limit;
   - the recorded `8yc522XLB_W2` (O4, `word_order`, M65) and `2tpLGUOSqj41` (O5, the owner: leave recorded) stay as they are; a failure on one of them alone is rerun once after checking the seed.

## Fuzz
Widen where the realiser can model the new checks (for example `ser` against `estar` with a place after `es en`, the gender of a `de …` subject), never narrow. Three runs at the default depth and one at five times (`VOCATERRA_FUZZ_ITERATIONS=15000 VOCATERRA_FUZZ_ITERATIONS_GRAMMAR=10000`).

## Proof
- `scripts/try-lines.sh --check`: only the lines this milestone fixes change; update the `.expected` files in their own commit and list every changed line. **Every line of the earlier files that was clean stays clean.**
- A dozen natural, correct lines of your own around each new check (places with `ser` and `estar`, events, names, `de …` subjects, `esto es`, greetings), listed and clean.
- The goldens and the identity baseline: re-record them with the golden project alone if they move, in their own commit, each change listed.
- The core demo: `yo quiere dos espada` at `villager-open` still yields exactly `conj.person` and `noun.number`.

## Read first (the sections named, not whole files)
- `CLAUDE.md`; `docs/nlp/IMPLEMENTATION_PLAN.md` §3, §6, the M69 row of §5 and the §5b entries named above.
- `docs/nlp/API.md`: "Person and copula errors", "Irregular forms, plurals and unknown words", "Adjectives" (`grep -n '^### '`).
- `tests/Vocaterra.Nlp.Fuzzing.Tests/README.md`: grep for `buenas tardes`, `deXmFwx4GzL4`, `M71`; never whole.
- The code: `src/Vocaterra.Nlp.Es/Analysis/SpanishAgreementChecker*.cs`, M58's slip readings, and their tests.

## Rules
- A core milestone edits only the `Vocaterra.Nlp` and `Vocaterra.Nlp.*` projects, their tests, `tests/golden`, `docs/nlp/` and `scripts/try-lines/`. It does not edit `content/`, `docs/game/` or `docs/qa-reports/`.
- Fix at the source with regression tests in the owning module. **Never weaken or narrow a fuzz oracle.** If an item cannot be checked without flagging correct Spanish, leave it, record why in §5b's words in your report, and move on.
- Determinism: no `System.Random`, no clock, ordinal comparisons.
- Do not edit either `IMPLEMENTATION_PLAN.md` or either `DECISIONS.md`. Never create or edit anything under `.github/workflows/`. Update `docs/nlp/API.md` where it lists what each tag reports.

## Output, context and resources
- Read the sections named, not whole files; prefer `grep -n`, and keep `sed -n` ranges under about 150 lines. Heavy output to log files under `/tmp/claude-1000/m69/`, at most 30 lines printed.
- Run every `dotnet restore|build|test|format|run` through `scripts/limited.sh` (builds `-m:4`, tests `-m:2`). `scripts/test.sh` and `scripts/content-loop.sh` run directly (they call it themselves). Never start two heavy commands at once: no `&`, no background builds. Exit code 137 means the 6 GB cap killed the command: lower the parallelism.
- Prefix every command inline with `PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1`. The sandbox refuses a command line containing `eval` or `hash`. Model-gated tests read `VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models`.

## Your checks before reporting
- The projects you changed build with `-warnaserror`, 0 warnings; their tests pass, with `tests/Vocaterra.Game.Core.Tests` and `tests/Vocaterra.Host.Tests` run once at the end (a game test that pinned an old grade is reported, not changed).
- `dotnet format --verify-no-changes` on each project you changed.
- The proof above. The orchestrator's gate then runs the whole solution.

## Commit
Each message ends with:
```
Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDfGVmxEaTFXNb87eD1
```

## Report (one page, under 400 words)
- The branch and the final commit SHA, pushed.
- For 1 to 5: what was built or why it was left, the tests, the result before and after.
- The `try-lines` lines that changed; your correct lines.
- The goldens and identity entries that moved; fuzz widenings, the four runs, findings.
- The test counts before and after.

Before your final message, make sure no process you started is still running (`pgrep -fa 'dotnet|limited.sh'`, kill only your own) and that the worktree is clean and pushed.
