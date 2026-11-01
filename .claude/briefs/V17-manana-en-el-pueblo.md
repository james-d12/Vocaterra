You are implementing milestone **V17, session 9 "Mañana en el pueblo"**, of the Vocaterra repository (C#, .NET 10: a Spanish-learning first-person voxel game on MonoGame, plus a language core). It is the third A2 lesson of phase B of `docs/game/VILLAGE_PLAN.md`, and the series' only new tense:
- the baker Luz, the smith Domingo and the fisher Mar;
- **the simple future** (-é, -ás, -á, -emos, -án; `iré`; `hará`, `tendré` heard) as the second way to say tomorrow, **against `ir a`**: a plan made now, `voy a …`; a promise or a prediction, `-é`;
- the day of tomorrow, told by an NPC and retold by the learner, as session 8 did with yesterday;
- the later time markers (`la semana que viene`, `el próximo día`, `pasado mañana`) in the bundle's `state_action`, now that the core's M57 has landed.

Do the work yourself; do not spawn subagents.

## Where to work, and pushing
Work only in the existing git worktree `/home/user/Dev/Vocaterra/.claude/worktrees/village`: cd there first, and never edit `/home/user/Dev/Vocaterra` itself or any other worktree. It is on branch `feature/village` (at the HEAD `git log -1` shows, `888f914`).
- Create your branch with `git checkout -b V17-manana-en-el-pueblo feature/village`.
- **The owner wants work pushed often.** Commit after each coherent step that builds and passes `scripts/test.sh fast`, with the prefix `V17: `.
- Push after every commit: `git push -u origin V17-manana-en-el-pueblo` the first time, then `git push origin V17-manana-en-el-pueblo`.
- Never push `main` or `feature/village`, and do not merge.

## What exists (read the V entries at the end of `docs/game/IMPLEMENTATION_PLAN.md` section 5 first, V10 to V16, and the core's M56, M57 and M59 entries in `docs/nlp/IMPLEMENTATION_PLAN.md` §5b)
- **Phase A (sessions 4 to 6, `a1`)** and **V13 (session 7, `como-es`)**: as V14's brief said; V13 lets an order name its lesson.
- **V14 (session 8, `ayer-en-el-pueblo`, `a2`):** the villager tells the day of the village and the learner retells it (the preterite in every person, `tener que`, the intent `state_obligation`); **the listener's name as a vocative** is taken out before the core reads the line (`src/Vocaterra.Game.Core/Npc/Vocative.cs`: `Luz, quiero pan`); `smith-open` takes `state_action` and `state_want`; `oven-wood` and `axe-handle` name their lessons. A learner leaves session 8 with 0 coins, 3 apples, 2 loaves and the axe.
- **V16 (the simple future in the runner and the book):** a step's `tense` may be `fut` (only in an `a2` lesson); at a `fut` step the near future (`voy a comprar pan`) and the present with a time word (`mañana compro pan`) are credited with the step's one `futureNote`, and `compré pan` or `compro pan` is a miss with the `tenseRetry`; at an `ir-a` step the simple future is credited with the step's `presentNote`; the book's fourth column, "future" (`ui.bookColumnFuture`); the game's time words as phrases (`nearFuture.timeWords`, `NearFutureWords.Mark`). **Your lesson is the first with `fut` steps:** use V16's machinery as it is, and report anything it lacks.
- **The core:** M56 and M59 (a subject after its verb, the person checks; **a lone name before a verb is never checked**, it may be the listener, so `Luz harás pan` at a step is the lesson's own person miss); M57 (the later markers, below).
- **V10 to V14 are the models to follow**: their lessons, NPC files, sheet rows, intents, tests (`YesterdayLessonTests`, `YesterdaySpokenTests`, `VillageTalkTests` and the earlier ones), locale text, checks and commits (`git log --oneline -60 feature/village`).
- **V12c:** sleeping at the player's fire advances the session; `--skip-to-session 9` must open your lesson; test it.
- **V18:** the content loop is one command (below).

## Read first (the sections named, not whole files: every line you read stays in your context for the rest of the run)
- `CLAUDE.md`.
- `docs/game/VILLAGE_PLAN.md` (read only these sections, by `grep -n '^##'` and ranges). Your milestone is V17:
  - section 2.9 (verbs in three times; "The simple future is half-known");
  - section 3.1 (principles 6 and 7: an order is a plan, a doing and a report), 3.3 (the people) and 3.5: the arc table's session-9 row, the ladder's row 9, and the session-9 bullet of "Sessions 7 to 10";
  - 3.6 (the paragraph on the changes to `state_action`: the time words at either end), 3.7 (`tense: fut`, `futureNote`, `nearFuture.timeWords`), 3.9, 4 ("Every content milestone") and 5 ("A tense is graded only next to a marker", "Not overwhelming": session 9 is the only new tense).
- `docs/game/DECISIONS.md`: D-game-10, D-game-12 and D-game-22 only.
- `content/README.md` (a step's person and tense, the two notes, the time words); `content/PHRASES.md`: its header and column notes, then only the rows of sessions 7 and 8 (grep them); the lessons `como-es` and `ayer-en-el-pueblo` with their locale text; `docs/nlp/API.md` "Tense errors"; `docs/game/README.md`, the V16 section.
- **Do not read** `docs/game/SPEECH_CHECKLIST.md`, `content/game-v1/manifest.json`, or the goldens, whole: grep for what you need.

## Output and context (the cost of a milestone is its turns times its context)
- Send every heavy command's output (builds, tests, `content.sh`, prerender, scripted runs, LanguageTool) to a log file under `/tmp/claude-1000/`, and print at most 30 lines of it (the summary, `grep` for failures, `tail`).
- Keep `sed -n` ranges under about 150 lines; prefer `grep -n` to find the place first.
- Wait for a long command by running it in the foreground through `scripts/limited.sh`, not by polling a log with repeated `tail`.
- After the first edit of the bundle, run the grammar-sensitive fuzz classes at once (`scripts/test.sh project tests/Vocaterra.Nlp.Fuzzing.Tests`), not only the fast tier, so that a grammar fault shows before the full run.
- **The core's M57 has just landed** (at `888f914`; its entry in `docs/nlp/IMPLEMENTATION_PLAN.md` §5b). `state_action`'s time words may now take `pasado mañana`, `la semana que viene` (and `el mes que viene`, `el año que viene`), `la próxima semana`, `el próximo día` (`mes`, `año`) as future markers and `la semana pasada`, `el año pasado` as past ones, written as alternatives in the grammar: the fuzz generator and the core's tense check read the same table (`SpanishTimeMarkers`) and agree on all of them. Until a grammar takes them, `la semana que viene compro pan` binds `semana` as the item. The game's `nearFuture.timeWords` (V16) also has `luego` and `después`, which the core does not count as markers (`luego comí` stays clean): keep them for the runner's credit of a plan said in the present, but do not expect the core to flag a tense next to them. The core also knows `el día que viene`, `la próxima vez`, `el próximo lunes`, the seasons, `anoche` and `anteayer`; add to the game's list only what the lesson uses.
- **Since M57 the fuzz suite's stream digests run on frozen inputs**: a content edit no longer moves them (the loop's step 7 just passes). If one moves, something other than content changed: report it.
- **Known core gaps you must not rely on** (`docs/nlp/IMPLEMENTATION_PLAN.md` §5): `cuánto costa`, `hay pans` (M58) and the predicate adjective (`la manzana es rojo`) and a colour adjective before its noun (M62), both before V15; an object put first with no clitic (`las manzanas vende Luz`) reads as a subject (M59's known limit). The fuzz suite has one rare, older O5 failure on a `word_order` mutation (seed `ff2aNwHuJBk8`, its README); if a full run fails there, rerun once and report it, do not change the oracle.

## What to build (VILLAGE_PLAN 3.5, session 9; the ladder's row 9; 3.6 and 3.7)
1. **The lesson `manana-en-el-pueblo` "Mañana en el pueblo"**, level `a2`, with the baker, the smith and the fisher; reached by sleeping at the player's fire from session 8 (`--skip-to-session 9`). The simple future as the second way to say tomorrow, against `ir a`:
   - an `explain` step that says when each is used: a plan made now, `voy a …`; a promise or a prediction, `-é` (one sentence each, in English support text, with a Spanish example each);
   - heard: `que-haras-manana` "¿Qué harás mañana?"; the day of tomorrow told in three lines ("Mañana Luz hará pan. Domingo trabajará. Mar pescará.", deliverable 6), as session 8 told yesterday; `tendre-pan` "Mañana tendré pan." (the baker);
   - said, each a step with `tense: fut` (V16): `manana-comprare-pan` "Mañana compraré pan." (`state_action`; `mañana voy a comprar pan` is credited with the `futureNote`, as V16 built), `ire-al-rio` "Iré al río.", `buscare-madera` "Buscaré madera para Luz.", `luz-hara-pan` "Luz hará pan." (the third person, heard first in the day of tomorrow);
   - the error examples the core grades, tried on the CLI first: `mañana compré pan` (`conj.tense`), `ayer compraré pan` (`conj.tense`, expected `compré`), `yo comprarás pan` (`conj.person`).
2. **The later markers** in the bundle's `state_action` time words (at either end, as 3.6 says), from M57's list above, and in `nearFuture.timeWords` where the lesson uses one; show on the CLI that `la semana que viene compraré pan` and `pasado mañana iré al río` are `state_action` and clean, and `la semana que viene compré pan` is `conj.tense`; run the fuzz project right after the grammar edit.
3. **The book:** the future column (V16) shows the forms taught: `compraré`, `comprarás`, `comprará`; `iré`; `buscaré`; and the heard `hará`, `tendré`, `trabajará`, `pescará` as V13 listed heard forms. The counts on the Lessons page.
4. **The order of the session** follows principle 7 (VILLAGE_PLAN 3.1): a plan, a doing and a report, in the three times, as V13 did (V14 had none); if the session has no order, say why in the report.
5. **The coin path:** a learner arrives with 0 coins, 3 apples, 2 loaves and the axe (V14); nothing in session 9 may need coins a learner cannot have. Say what the learner leaves with.
6. **The day of tomorrow** is told by one of the three NPCs (your choice; say why), as the villager told yesterday in V14, and is true in the world where it can be (what Luz, Domingo and Mar will do is what session 10's market then shows, if it fits).
7. **Plans in free talk at the lesson's NPCs** (found by the orchestrator at M57): `mañana voy a comprar pan` at `baker-open` resolves no intent (`unknown`, `vocab.wrong`): the baker's open prompt does not take `state_action`, and V14 gave it only to `smith-open` (the fisher's and the villager's prompts were left, because the lint gave `busqué madera` to `ask_directions` there). A lesson about tomorrow makes plans at the baker and the fisher likely: let their open prompts take `state_action` with a short reply if the lint's confusion check stays clean and the fuzz suite passes, or say why not.
8. **Checks:** about 8 items with `lesson: manana-en-el-pueblo`.
9. **Tests**, in the style of V14 and V16:
   - the lesson loads and validates at `a2` (and a copy at `a1` fails on its `fut` steps); every prompt and phrase resolves;
   - the `fut` steps credit and miss as V16 built (`mañana compraré pan` credited; `mañana voy a comprar pan` and `mañana compro pan` credited with the `futureNote`; `compré pan` and `compro pan` a miss with the `tenseRetry`; `mañana compré pan` the core's `conj.tense`; `yo comprarás pan` the core's `conj.person`; `Luz harás pan` the lesson's own person miss);
   - the book's future column shows the forms taught and heard after the lesson;
   - `--skip-to-session 9` opens `manana-en-el-pueblo`, with the objective and the beacon on its first NPC.
10. **M57's loose ends** (it could not edit them, a core milestone): `scripts/content-loop.sh` (its header and step 7), `scripts/README.md`, `content/README.md` and `docs/DEVELOPMENT_BOTTLENECKS.md` still say the stream digests move with the content; say instead that they run on frozen inputs since M57 and that step 7 checks they did not move.

## The content loop (as V10 to V13 did it; D-game-10 and D-game-12)
- **Sheet rows first** (`content/PHRASES.md`, `game/phrases.json`, the locale glosses). Each said row has a graded learner-error example, and every new row is `llm-opus (provisional)`.
- **Try the core first**, on every canonical text and error example, before relying on a grade. Use the CLI (`dotnet run --project tools/Vocaterra.Nlp.Cli -- eval --transcript "<text>" --prompt <prompt> --content content/game-v1`) through a wrapper script under `/tmp/claude-1000/`, because the sandbox refuses the word `eval`. In particular: `mañana compraré pan`, `iré al río`, `buscaré madera para Luz`, `Luz hará pan`, `mañana tendré pan` and `mañana voy a comprar pan` clean; `mañana compré pan` and `la semana que viene compré pan` `conj.tense`; `ayer compraré pan` `conj.tense` expected `compré`; `yo comprarás pan` `conj.person`; `compraré`, `iré`, `hará`, `tendré` read as `comprar`, `ir`, `hacer`, `tener` with tense `fut`. Also a few natural, correct lines a learner may say at these NPCs (with a name, `usted`, `ustedes`) to check nothing correct is flagged. A wrong grade is a core gap. Change the example or the row, do not patch the core, and report the exact sentence and what it gave; the orchestrator files core milestones.
- **An Opus pass** over every new Spanish string before any WAV: naturalness, a Latin American neutral register (`ustedes`, not `vosotros`), words within the level, accents and `¿¡`. List the changes in the report.
- **Words:** the gate's lint (A2, fail-on-level) is clean, and the validator passes the lesson at its level. A word above A2 fails.
- **The content loop, one command (V18):** after each round of content edits run `scripts/content-loop.sh --grammar --speech` directly (never under `scripts/limited.sh`: it calls it itself and refuses to run nested). It runs the refresh (manifests, provenance, validate, the gate's lint and cefr-checks, `--validate-content`, the grammar strings and the offline grammar check), the LanguageTool cache with `--prune` and CI's rebuilt-from-nothing check, the prerender of the new lines and the manifest after it, the new `bundleHash` into every save fixture, the build, the goldens and the stream digests (checked), and the fast tier with the grammar-sensitive fuzz classes; it prints a summary of at most 30 lines and stops at the first failure with the next thing to do. Its header (`scripts/content-loop.sh`) and `content/README.md` ("The edit loop") say the rest.
  - **Read every grammar finding it prints**: fix the content or add a waiver with its reason. Names read as other words (`Domingo` as the day, `Mar` as the sea) take text waivers, as V12 did.
  - **WAVs:** use the NPCs' existing voices. The voices are provisional (the owner has not heard them); the checklist rows are for the owner.
  - **When the goldens move,** run it with `--record` and commit the goldens and the identity baseline in a commit of their own, listing in the report which entries moved and why. The fuzz suite's `FuzzLanguageStreamTests` digests are still constants in its C# (until the core's M57): the loop prints the new values and the constants to set; set them with the generators unchanged and say so in the commit and the report.
  - Run it once with `--tier full` before the report (the whole suite, as the gate). Never weaken a fuzz oracle.
- **With models:** the spoken round trip (as the `*SpokenTests` do) over the new said rows. `compraré`, `iré`, `buscaré` and `hará` must be heard. A word the recognizer cannot hear goes to the heard side, or to a typed-only variant; report it.
- **Timing:** the scripted lesson run with `--skip-to-session 9` and `--lesson-run manana-en-el-pueblo`, then your estimate in minutes, counting reading, audio, turns and walking (target 5 to 10).
- **Scripted runs:** run `scripts/scripted-runs.sh` before and after; none should change apart from what the new lesson adds (for example, the Lessons page's line count). Explain every difference.
- **Test the learner's route,** not only `--lesson-run` (phase A's lessons were unreachable in practice until V12c, and V12b's wrong credit showed only in free talk): from `--skip-to-session 8`, finish session 8, sleep at the fire into session 9, and check the book's Lessons page (`next:`), the objective and the beacon as a learner would find them; play the lesson; then talk freely once with each NPC of the lesson and check the reply and the phrase credited.
- **CI's case:** run the full suite once without models, as CI does: `VOCATERRA_MODELS_DIR=<an empty folder under /tmp/claude-1000/> scripts/test.sh full` (the model-gated tests then skip). Report any test that measures wall-clock time and passes within 4 times of its budget (the GitHub runner is about four times slower than this machine; M60 failed only there).

## Resources (the desktop has crashed from overload)
- Run every `dotnet restore|build|test|format|run`, every `scripts/content.sh` call, every speech render and every screenshot run through `scripts/limited.sh`.
- Never start two heavy commands at once: no `&`, no background builds.
- While iterating, use `scripts/test.sh fast` or `scripts/test.sh project <test project>`. Run `scripts/test.sh full` once before reporting.
- Exit code 137 means the 6 GB cap killed the command: lower the parallelism, and never work around the cap.
- Disk is tight (about 11 GB free): delete capture directories you no longer need.

## Environment
- Prefix every command inline with `PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1`.
- The sandbox refuses a command line that contains the words `eval` or `hash`. Use a wrapper script under `/tmp/claude-1000/` for those.
- Model-gated tests read `VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models`.
- Game runs need `DISPLAY=:0`.

## Rules
- **What a game milestone edits:** the game projects (`src/Vocaterra.Game.Core`, `src/Vocaterra.Game`, `src/Vocaterra.Host`, `tools/Vocaterra.SpeechRender`) and their tests; `content/game-v1`, `content/locale`, `content/schema`, `content/PHRASES.md`, `content/README.md`, `content/grammar` and `content/speech-index.json`; `assets/` and `docs/game/`.
- It takes the core (`src/Vocaterra.Nlp*`, `tests/Vocaterra.Nlp.*`, `tests/golden`, `docs/nlp/`) as it is, except for re-recording goldens and digests a content edit moved.
- Engine code goes only in `src/Vocaterra.Game`. No `System.Random` and no clock in the evaluation path.
- `scripts/` (not `merge-gate.sh`) and `docs/DEVELOPMENT_BOTTLENECKS.md` for deliverable 10 only.
- Do not edit `docs/game/IMPLEMENTATION_PLAN.md`, `docs/game/VILLAGE_PLAN.md`, `docs/nlp/IMPLEMENTATION_PLAN.md` or either `DECISIONS.md`.
- Never create or edit anything under `.github/workflows/`.
- Write code and content that read like the surrounding ones.

## Gate (from the worktree root, each through scripts/limited.sh)
- `dotnet build Vocaterra.sln -warnaserror -m:4`, with 0 warnings.
- `dotnet test Vocaterra.sln -m:2` all passing. It was 6659 passed and 3 skipped; report the new count.
- `dotnet format Vocaterra.sln --verify-no-changes`.
- `scripts/content.sh validate`.
- The content gate in `scripts/merge-gate.sh`: the lint at A2 with fail-on-level and `--host-pools 'guide.*'`, and the cefr-checks.
- The host's `--validate-content`.
- `scripts/content.sh grammar content/game-v1 --strings content/grammar/game-v1-strings.jsonl --strings-only --offline --strict`.
- **The host demos:**
  - `dotnet run --project src/Vocaterra.Host -- --transcript "quiero agua, por favor" --prompt stall-need` still prints `request_item`, communicated;
  - `--transcript "tengo que ir a la panadería" --prompt <V14's prompt for it>` still prints `state_obligation`, communicated;
  - `--transcript "mañana compraré pan" --prompt <its prompt>` and `--transcript "la semana que viene iré al río" --prompt <its prompt>` print `state_action`, communicated, with no error;
  - `--transcript "Luz, mañana compraré pan" --prompt baker-open` (or the baker's prompt that takes it) is read without the name, as V14 built.
- Do not commit `bin/`, `obj/` or `artifacts/`.

## Commit
Each message ends with:
```
Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDdyPc3eAsDgGyBVKv6
```

## Report (one page, under 400 words; the details belong in the commits)
- The branch and the final commit SHA, pushed.
- What was built, by deliverable.
- The final said and heard rows (id and Spanish), and any dropped or added; who tells the day of tomorrow, and why.
- The Opus pass's changes.
- The core tries and their results. A wrong grade is a core gap: give the exact sentence and what it gave.
- The markers the grammars now take, and the fuzz results after the grammar edit.
- What V16's machinery lacked, if anything.
- Recognizer results.
- The lesson's estimated minutes, and the coin path.
- The test count before and after (with models, and without: CI's case), and any timed test within 4 times of its budget.
- The new bundle hash; the goldens re-recorded, if any.
- The scripted-run differences, and the learner's route from session 8.
- Bugs found and fixed.
- Anything left undone, and why.
