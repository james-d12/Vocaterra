You are implementing milestone **V16, the simple future in the lesson runner and the book**, of the Vocaterra repository (C#, .NET 10: a Spanish-learning first-person voxel game on MonoGame, plus a language core). It is a small code milestone of phase B of `docs/game/VILLAGE_PLAN.md` (section 3.8, the V16 row), and it prepares session 9, "Mañana en el pueblo" (V17), which teaches the simple future (`compraré`, `iré`, `hará`) against `ir a`. V16 adds no lesson and no Spanish line of the bundle.

Do the work yourself; do not spawn subagents.

## Where to work, and pushing
Work only in the existing git worktree `/home/user/Dev/Vocaterra/.claude/worktrees/village`: cd there first, and never edit `/home/user/Dev/Vocaterra` itself or any other worktree. It is on branch `feature/village` (at the HEAD `git log -1` shows, `@@HEAD@@`).
- Create your branch with `git checkout -b V16-simple-future feature/village`.
- **The owner wants work pushed often.** Commit after each coherent step that builds and passes `scripts/test.sh fast`, with the prefix `V16: `.
- Push after every commit: `git push -u origin V16-simple-future` the first time, then `git push origin V16-simple-future`.
- Never push `main` or `feature/village`, and do not merge.

## What exists
- **S27** gave a lesson step a `tense` (`pres`, `pret`, `ir-a`) and the runner its tense check (`LessonRunner.FormMiss`, `WrongTense`): a wrong tense the core cannot see (no time word) is the lesson's Miss with the step's `tenseRetry`; a wrong tense next to a time word is the core's `conj.tense`, a slip that ends the step. The owner's rule (S27): **correct Spanish is never a Miss.** So a plan said in the present with a future time word at an `ir-a` step (`mañana como pan`) is credited, and the next step opens with the step's `presentNote` (`LessonRunner.PresentForPlan`, the time words in `game/words.json`'s `nearFuture.timeWords`).
- **S28** gave the book its conjugation tables (`Language/Book/ConjugationTable.cs`): the columns present, past and future (ir a), each shown once a cell of it is unlocked; `TaughtVerbForm.Text` builds a form from the core's `MorphologyGenerator` (the near future from `ir`'s present, `a` and the infinitive).
- **V08** gave every lesson a `level`; `LessonLevels.TensesAt` already allows `fut` at A2 only, and the validator checks a lesson's `verbs` tenses against its level. But `LessonBook` still refuses `fut` at a step (`"tense" is pres, pret or ir-a`), and V08's findings say "the `fut` check at a step is tested from V16".
- Read the V01 to V13 entries (and V14's if it is there) at the end of `docs/game/IMPLEMENTATION_PLAN.md` section 5 for the series so far.

## Read first
- `CLAUDE.md`.
- `docs/game/VILLAGE_PLAN.md`: section 2.9 (verbs in three times: what exists), 3.5 (the ladder; session 9 and "Sessions 7 to 10", session 9), 3.7 (the `lesson` row: `tense` may be `fut`, with a `futureNote` beside `presentNote`; the `content/locale/en/*` row: `ui.bookColumnFuture`; the `game/words.json` row), 3.8 (the V16 row) and section 5 ("A tense is graded only next to a marker").
- `docs/game/DECISIONS.md`: D-game-22; and S27's and S28's entries in `docs/game/IMPLEMENTATION_PLAN.md` section 5.
- `docs/nlp/API.md` "Tense errors": the core flags a future marker with a past verb (`mañana compré` expects `voy a comprar`) and a past marker with a present or future verb (`ayer compraré` expects `compré`); the present and the simple future next to `mañana` are correct.
- The code: `src/Vocaterra.Game.Core/Language/Lessons/LessonBook.cs` (`LessonLevels`, `TaughtVerbForm`, the step's `tense` parse), `LessonRunner.cs` (`OnTurn`, `FormMiss`, `WrongTense`, `PresentForPlan`, `IsNearFutureAt`), `Language/Book/ConjugationTable.cs`, `Content/GameDataValidator.cs` (`CheckLessonLevels`, the locale fields of a step), `content/schema/lesson.schema.json`, the locale files under `content/locale/en/`, `content/README.md` (the lesson format), and the tests of S27, S28 and V08 (`LessonLevelTests`, the conjugation-table tests, the tense-step tests).

## What to build (VILLAGE_PLAN 3.8, V16)
1. **`tense: fut` on a step.** `LessonBook` accepts `fut` at a `repeat` or `answer` step; the schema's `tense` enums accept it (bump the schema's version only if the repository's convention for an added enum value says so, and keep the schema test in step); the validator refuses a `fut` step in an `a1` lesson (V08's rule, now tested at a step as well as in `verbs`).
2. **The runner at a `fut` step.** A finite simple-future form is the answer. The owner's rule holds: **correct Spanish for the future is never a Miss.**
   - The near future (`voy a comprar pan`, `mañana voy a comprar pan`) at a `fut` step is credited, and the next step opens with the step's `futureNote` (a new locale field, `lesson.<id>.<step>.futureNote`, beside `presentNote`), as the present is credited at an `ir-a` step.
   - A present with a future time word (`mañana compro pan`) at a `fut` step is correct Spanish too: credit it, with the `futureNote` (or the `presentNote` if the step has one; choose, and say which and why).
   - A preterite or a present without a time word at a `fut` step is the lesson's Miss with `tenseRetry`, as at the other tense steps. A wrong tense next to a time word stays the core's `conj.tense` (`mañana compré pan`), a slip.
   - Check the tense label the core's readings give a simple-future form (`GuideWord.Tense` for `compraré`, `iré`, `hará`): use what the core gives, do not add a mapping that guesses.
   - At an `ir-a` step, a simple future (`mañana compraré pan`) is correct Spanish for a plan as well: credit it (with the `presentNote` or no note; say which), so that a learner who has met the future in session 9 is not told off in a later `ir-a` step.
3. **The book's fourth column.** `ConjugationTable.AllColumns` gains the simple future (`fut`, the locale key `ui.bookColumnFuture`, "future" or the label the other three suggest), shown once a lesson teaches a `fut` form, as the others are; its cells' counts come from the learner's word record like the present and the past. Check the order of the columns reads well (present, past, future (ir a), future, or another order you can defend), and that the book's page still fits at the smallest window and the largest text scale (X08) with four columns; if it does not, fix the layout in `src/Vocaterra.Game` and show the screenshot.
4. **A test that the forms exist:** `TaughtVerbForm.Text` finds `compraré`, `comprarás`, `comprará`, `iré`, `buscaré`, `hará`, `tendré`, `trabajará` and `pescará` in the bundle's lexicon (they come from the Wiktionary import; VILLAGE_PLAN 2.9). A verb the import lacks has no `fut` cell (`MorphologyGenerator.ConjugateRegular` builds no simple future): if any of these is missing, report it as a core gap and do not patch the core.
5. **The time words.** VILLAGE_PLAN 3.7 says `nearFuture.timeWords` gains the other future markers so that a plan said in the present with any of them is credited as `mañana` is. The core's markers (`docs/nlp/API.md`) are `mañana`, `pasado mañana`, `próximo/a` + a time noun and a time noun + `que viene`; `luego` and `después` are not markers to the core. Add what the runner can match safely (multi-word markers need a phrase match, not a word match), and leave the bundle's grammars alone: `state_action` takes the later markers only after the core's M57 (V17).
6. **Docs:** `content/README.md` (the lesson format: `fut`, `futureNote`) and any game doc that lists the step tenses or the book's columns.
7. **Tests**, in the style of S27, S28 and V08, on fixture lessons (no lesson of the bundle has a `fut` step yet): a `fut` step loads; an `a1` lesson with a `fut` step fails validation; the runner credits `compraré pan`, credits `voy a comprar pan` with the `futureNote`, credits `mañana compro pan` as you chose, misses `compré pan` and `compro pan` with `tenseRetry`, and leaves `mañana compré pan` to the core; an `ir-a` step credits `mañana compraré pan`; the book shows the fourth column only once a `fut` form is taught, with the counts; the forms of deliverable 4.

## Resources (the desktop has crashed from overload)
- Run every `dotnet restore|build|test|format|run`, every `scripts/content.sh` call and every screenshot run through `scripts/limited.sh`.
- Never start two heavy commands at once: no `&`, no background builds.
- While iterating, use `scripts/test.sh fast` or `scripts/test.sh project <test project>`. Run `scripts/test.sh full` once before reporting.
- Exit code 137 means the 6 GB cap killed the command: lower the parallelism, and never work around the cap.
- Disk is tight (about 13 GB free): delete capture directories you no longer need.

## Output and context (the cost of a milestone is its turns times its context)
- Read the sections named, not whole files (`docs/game/VILLAGE_PLAN.md` is 71 KB; do not read `docs/game/SPEECH_CHECKLIST.md` or `content/PHRASES.md` whole); prefer `grep -n` to find a place, and keep `sed -n` ranges under about 150 lines.
- Send every heavy command's output to a log file under `/tmp/claude-1000/` and print at most 30 lines of it (the summary, `grep` for failures).
- Wait for a long command by running it in the foreground through `scripts/limited.sh`, not by polling a log.
- Run the full suite once without models, as CI does (`VOCATERRA_MODELS_DIR=<an empty folder under /tmp/claude-1000/> scripts/test.sh full`), and report any wall-clock test within 4 times of its budget.

## Environment
- Prefix every command inline with `PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1`.
- The sandbox refuses a command line that contains the words `eval` or `hash`. Use a wrapper script under `/tmp/claude-1000/` for those (`scripts/content.sh hash`, a CLI `eval`).
- Model-gated tests read `VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models`.
- Game runs need `DISPLAY=:0`.

## Rules
- **What a game milestone edits:** the game projects (`src/Vocaterra.Game.Core`, `src/Vocaterra.Game`, `src/Vocaterra.Host`, `tools/Vocaterra.SpeechRender`) and their tests; `content/game-v1`, `content/locale`, `content/schema`, `content/PHRASES.md`, `content/README.md`, `content/grammar` and `content/speech-index.json`; `assets/` and `docs/game/`.
- It takes the core (`src/Vocaterra.Nlp*`, `tests/Vocaterra.Nlp.*`, `tests/golden`, `docs/nlp/`) as it is. A missing core call stops that part: report it.
- If you edit `content/game-v1` (for example `game/words.json`), hash it through the wrapper, validate, run the whole suite, re-record any golden it moved and put the new hash into the save fixtures (`save-v1.json`, `save-v4.json`, any other that pins it).
- Engine code goes only in `src/Vocaterra.Game`. No `System.Random` and no clock in the evaluation path.
- Do not edit `docs/game/IMPLEMENTATION_PLAN.md`, `docs/game/VILLAGE_PLAN.md`, `docs/nlp/IMPLEMENTATION_PLAN.md` or either `DECISIONS.md`.
- Never create or edit anything under `.github/workflows/`.
- Write code that reads like the surrounding code.

## Gate (from the worktree root, each through scripts/limited.sh)
- `dotnet build Vocaterra.sln -warnaserror -m:4`, with 0 warnings.
- `dotnet test Vocaterra.sln -m:2` all passing. It was @@COUNT@@; report the new count.
- `dotnet format Vocaterra.sln --verify-no-changes`.
- `scripts/content.sh validate`, the host's `--validate-content`, and the content gate in `scripts/merge-gate.sh` (the lint at A2 with fail-on-level, the cefr-checks).
- The host demo `dotnet run --project src/Vocaterra.Host -- --transcript "quiero agua, por favor" --prompt stall-need` still prints `request_item`, communicated.
- One game screenshot of the book's conjugation page with the fourth column shown (a fixture or a test aid; say how), looked at.
- Do not commit `bin/`, `obj/` or `artifacts/`.

## Commit
Each message ends with:
```
Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDdyPc3eAsDgGyBVKv6
```

## Report (under 400 words)
- The branch and the final commit SHA, pushed.
- What was built, by deliverable, and the choices of deliverable 2 (which note at which step).
- The core's tense label for the simple future, and the forms of deliverable 4 found or missing.
- The time words added, and those left for V17 and why.
- The book's layout with four columns (the screenshot's path).
- The test count before and after; whether the bundle moved (its hash if so).
- Bugs found and fixed; anything left undone, and why.
