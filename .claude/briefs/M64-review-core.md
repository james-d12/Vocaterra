You are implementing the core milestone **M64, the core findings of the review of `feature/village`**, of the Vocaterra repository (C#, .NET 10). The language core is the `Vocaterra.Nlp` engine (`src/Vocaterra.Nlp`), the Spanish pack `src/Vocaterra.Nlp.Es` and the `Vocaterra.Nlp.*` test and tool projects; the game is `Vocaterra.{Game.Core,Game,Host,SpeechRender}`. **This milestone's work is in the core.** The owner asked on 2026-11-01 for every finding of the review (`docs/qa-reports/2026-11-review-feature-village.md`, read its R1, R2, R5, R6 and "Cleanups") to be fixed on `feature/village` before PR #20 is merged. The game's findings (R4, R7 to R10) are a separate milestone, V19, after this one; do not touch them.

Do the work yourself; do not spawn subagents.

## Where to work, and pushing
Work only in the existing git worktree `/home/user/Dev/Vocaterra/.claude/worktrees/village`: cd there first, and never edit `/home/user/Dev/Vocaterra` itself or any other worktree. It is on branch `feature/village` (at `9745d30`).
- Create your branch with `git checkout -b M64-review-core feature/village`.
- **The owner wants work pushed often.** Commit after each coherent step that builds and passes `scripts/test.sh fast`, with the prefix `M64: `. Push after every commit: `git push -u origin M64-review-core` the first time, then `git push origin M64-review-core`.
- Never push `main` or `feature/village`, and do not merge.

## The milestone
### (a) R1: a subject joined by `y` is read as its last pronoun (the main fix; a false alarm on correct Spanish)
`SpanishAgreementChecker.CheckSubjectVerb` (`src/Vocaterra.Nlp.Es/Analysis/SpanishAgreementChecker.cs`, about line 160) takes the subject pronoun right before the verb as the subject, with no look at a `y`/`e` before it. M62's `PredicateSubjectOf` (`SpanishAgreementChecker.Predicate.cs`, step 1) does the same for a predicate adjective. Session 8's lesson models `Por la mañana, Luz y yo fuimos a la tienda.` and asks `¿Y Mar y tú? ¿Adónde fueron ayer?`; the natural answer is graded wrong, and the NPC recasts it into wrong Spanish (`«Mar y yo fui al río»`).
- Wrongly flagged today, must be clean:
  - `mar y yo fuimos al río`, `luz y yo fuimos al río`, `tú y yo fuimos al río`, `mis amigos y yo fuimos al río` (at `lesson-we-went`; today `conj.person`, expected `fui`);
  - `tú y yo somos amigos` (the game's own line `guide.lesson.villager.be.1`; today `soy`);
  - `él y yo somos altos`, `ella y yo estamos cansadas` (today `conj.person` and, from M62, `noun.number`);
  - `ella y él son altos` (today `es` and `alto`);
  - `tú y ella son altas` and `tú y ella sois altas` (the bundle's dialects are es-MX, es-CO and es-AR, where `ustedes` is the plural of `tú`: accept both);
  - `usted y yo vamos al río`, `luz y yo compramos pan`.
- The person of a coordinated subject: any first person (`yo`, `nosotros`) gives the first person plural; else any second person (`tú`, `vos`, `vosotros`) gives the second plural **or** the third plural (`ustedes`), both correct; else the third plural. A predicate adjective after it is plural, and its gender is known only when every member has one (`ella y él` masculine, `ella y ella` feminine; `yo`, `tú`, a name give none, so only the number is checked).
- **Clause coordination must not change.** `y` also joins clauses, and then the pronoun after it is the subject of its own clause: `luz hizo pan y yo fui al río`, `comí pan y tú compraste fruta`, `mar pescó y yo comí pescado` stay clean, and `luz hizo pan y yo fuimos al río` stays flagged (`fui`). Use M59's notion of a phrase that opens its clause (`docs/nlp/API.md`, "A noun phrase before its verb") for the member before `y`.
- Decide, and say why, whether a verb that agrees with no accepted person is flagged with the coordinated form (`tú y yo fui` gives `fuimos`) or left unchecked. Flagging is a widening; leaving it is acceptable. Never flag correct Spanish.
- The fuzz suite never saw this case: the sentence realiser re-rolls every coordinated subject (`skip:coordinated-subject`, `tests/Vocaterra.Nlp.Fuzzing.Tests/Generation/SentenceRealizer.cs` about line 879). Where the realiser can model a coordinated subject with a pronoun, let it (a widening of O3, and of O4/O5 if you flag); otherwise say why not.

### (b) R5: an amount before a singular `ser` (a false alarm, M59)
`diez monedas es mucho dinero` and `dos panes es suficiente` give `conj.person` (expected `son`; the second also `noun.number` `suficientes`). An amount read as a unit takes a singular verb; both `es` and `son` are correct here. Clean after the fix: those two, `dos panes es poco`, `quince monedas es mucho`, and `dos panes son suficientes`. Still flagged: `las manzanas es roja` (`son`; no number in the phrase), `los niños come pan`, `dos niños come pan` (not `ser`).
- **The one fuzz exception the owner's request allows:** if this fix needs M59's `conj.person` mutation after a noun subject to leave out exactly this case (a phrase with a cardinal number before `ser` with a singular predicate), that guard is allowed. Make it as narrow as possible, document it in the fuzz README's narrowed oracles with the date 2026-11-01 and "the owner's request to fix the review's findings", and report it. No other narrowing of any oracle.

### (c) R6: `cerca del` with a time (a false alarm, M56)
`es cerca del mediodía` gives `ser_estar` (expected `está`): `IsApproximateTime` (`SpanishAgreementChecker.Postverbal.cs`) tests the word `de` and misses `del`. Clean: `es cerca del mediodía`, `son cerca de las tres`, `es cerca de la una`. Still flagged: `la tienda es cerca del río` (`está`: `del` with a place is still a place), `es cerca` (`está`).

### (d) R2: the boundary record
- `docs/nlp/adr/0009-core-and-game-boundary.md`: add one dated note, in the style of its M32, M34, M52 and M53 notes, for the public surface the branch adds. It covers:
  - `IAnalysisRules`: `IsElapsedTimePreposition` and `IsElapsedTimeNoun` (M59), `IsPostnominalAdjective` and `IsDegreeAdverb` (M62); members with no default, so breaking for a host that implements the interface;
  - `IMorphology`: `TryGetRegularizationOfWord` and `TryGetPluralSlip`, default members, and the public record `PluralSlip` (M58);
  - the public static class `SpanishTimeMarkers` (M57);
  - anything public this milestone adds.
  The boundary is unchanged in kind (no game word in the core); say so.
- `docs/nlp/API.md`:
  - It says `IsVerbSeekingQuestionWord` was added by M56; it came with M34 (`678d9ad`). Correct that.
  - Add (a) to (c) where it lists what `conj.person`, `ser_estar`, `noun.number` and `gender.agree` report.

### (e) The cleanup
`SpanishTimeMarkers` exposes collection-expression arrays as `IReadOnlyList<string>`, so a caller can cast and write them. Make them read-only (for example `Array.AsReadOnly`) and keep the property types, so the public surface does not change.

Regression tests in the owning module (`tests/Vocaterra.Nlp.Es.Tests`, beside `PostverbalSubjectTests` and M59's and M62's tests), each failing before the fix, with the clean and still-flagged neighbours above as tests too.

## Read first (the sections named, not whole files)
- `CLAUDE.md`.
- `docs/qa-reports/2026-11-review-feature-village.md`: R1, R2, R5, R6, "Risks" (the fuzz blind spots) and "Cleanups".
- `docs/nlp/IMPLEMENTATION_PLAN.md`: §3, §6, the M56 to M63 rows of §5 (lines 241 to 248), and the M59 and M62 entries of §5b (`grep -n '^- \*\*M59\|^- \*\*M62'`).
- `docs/nlp/API.md`: "Person and copula errors" and "Adjectives".
- `tests/Vocaterra.Nlp.Fuzzing.Tests/README.md`: grep for `coordinated-subject`, `M59`, `M62` and the narrowed oracles. Do not read it whole.
- The code: `SpanishAgreementChecker.cs` (`CheckSubjectVerb`), `.Preverbal.cs` (`CheckPreverbalSubject`, `PluralPredicateFollows`, the clause-start test), `.Predicate.cs` (`PredicateSubjectOf`), `.Postverbal.cs` (`IsApproximateTime`), `SpanishTimeMarkers.cs`, and their tests.

## Rules
- A core milestone edits only the `Vocaterra.Nlp` and `Vocaterra.Nlp.*` projects, their tests, `tests/golden` and `docs/nlp/`. It does not edit the game's projects, `content/game-v1`, `content/locale`, `content/PHRASES.md`, `docs/game/` or `docs/qa-reports/`. If a core test on `content/game-v1` breaks because the content relied on the old grade, stop and report it.
- Fix bugs at the source, with a regression test in the owning module. **Never weaken or narrow a fuzz oracle**, except the one guard (b) allows; widen or leave. If the fuzz suite finds a new real bug, fix it or report it, and add it to the fuzz README's list. A gate run that fails only on the three known open seeds (`egVgLELrxyL4`, `2ldq2rIKLrG2`, `b3HSKSLCsL_1`) is rerun once after checking the seed; say so in the report.
- Determinism: no `System.Random`, no clock, ordinal comparisons.
- Do not edit either `IMPLEMENTATION_PLAN.md` or either `DECISIONS.md`. Never create or edit anything under `.github/workflows/`.
- The end-to-end goldens may move: re-record them (`VOCATERRA_RECORD_GOLDEN=1`, `tests/Vocaterra.Nlp.Golden.Tests`) in their own commit, and list in the report every entry that moved and why it is now right.

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
- The sandbox refuses a command line that contains the words `eval` or `hash`.
- **The review's wrappers are ready.** They run the built Debug binaries, so build first.
  - `/tmp/claude-1000/review/batch.sh <file>` runs the CLI on each `prompt|line` of a file and prints the intent and the errors, one line each.
  - `/tmp/claude-1000/review/host1.sh <file>` does the same through the host (the game's path: the greeting and the listener's name taken out first).
  - Run both through `scripts/limited.sh`.
  - `/tmp/claude-1000/review/lines1.txt` and `lines2.txt` are the review's 99 lines; `h2.txt` and `h3.txt` are the `lesson-we-went` lines.
  - Write new line files beside them.
- Model-gated tests read `VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models`.

## Gate (from the worktree root, each through scripts/limited.sh)
- `dotnet build Vocaterra.sln -warnaserror -m:4`, with 0 warnings.
- `dotnet test Vocaterra.sln -m:2` all passing (it was 7259 passed and 3 skipped, with models); report the new count.
- `dotnet format Vocaterra.sln --verify-no-changes`.
- `bash scripts/license-scan.sh --offline` PASSED.
- The core demo: `yo quiere dos espada` at `villager-open` still yields exactly `conj.person` and `noun.number` (`batch.sh`).
- The lines of (a) to (c) give the stated results through `batch.sh`, and the `lesson-we-went` lines through `host1.sh` give no `conj.person` and no recast of `fuimos`.
- **Correct learner lines stay clean:**
  - `lines1.txt` and `lines2.txt` through `batch.sh`, compared with the run before your change. Only the lines this milestone fixes may change, apart from the known `vocab.wrong` at constrained prompts and the known `puede usted vender pan`.
  - A dozen clause coordinations of your own.
  - List any other change in the report.
- `scripts/content.sh validate` passes (the bundle is unchanged).
- Do not commit `bin/`, `obj/` or `artifacts/`.

## Commit
Each message ends with:
```
Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
```

## Report (one page, under 400 words)
- The branch and the final commit SHA, pushed.
- What was built for (a) to (e), and the choices for (a): the person rule, flag or leave, the clause test, the realiser.
- The regression tests and what each gave before the fix.
- The gate lines' results, and the diff of the 99 lines' runs before and after.
- Fuzz: oracles widened, the one guard of (b) if used (where, how narrow), new findings.
- The test count before and after; goldens re-recorded and why each entry moved.
- Anything left undone, and why.
