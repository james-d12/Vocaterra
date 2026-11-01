You are implementing milestone **V15, session 10 "El mercado"**, of the Vocaterra repository (C#, .NET 10: a Spanish-learning first-person voxel game on MonoGame, plus a language core). It is the last content milestone of phase B of the village series (`docs/game/VILLAGE_PLAN.md`, D-game-22), and the series' end:
- the baker Luz, the farmer León and the shopkeeper (the stall);
- the numbers 11 to 20, the total of a basket (`¿Cuánto es todo?`), comparing prices (`es muy caro`, `más barato que`), `poder` (`¿Puedo pagar con fruta?`) and selling;
- **the three times reviewed**: the market told in the past, the present and the future;
- the new intent `offer_item` and the host's `buy` (the NPC pays for what the player sells): the series' one repeatable source of coins.

Do the work yourself; do not spawn subagents.

## Where to work, and pushing
Work only in the existing git worktree `/home/user/Dev/Vocaterra/.claude/worktrees/village`: cd there first, and never edit `/home/user/Dev/Vocaterra` itself or any other worktree. It is on branch `feature/village` (at the HEAD `git log -1` shows, `@@HEAD@@`).
- Create your branch with `git checkout -b V15-el-mercado feature/village`.
- **The owner wants work pushed often.** Commit after each coherent step that builds and passes `scripts/test.sh fast`, with the prefix `V15: `.
- Push after every commit: `git push -u origin V15-el-mercado` the first time, then `git push origin V15-el-mercado`.
- Never push `main` or `feature/village`, and do not merge.

## What exists (read the V entries at the end of `docs/game/IMPLEMENTATION_PLAN.md` section 5 first, V13 to V17, and the core's M56 to M62 entries in `docs/nlp/IMPLEMENTATION_PLAN.md` §5b)
- **Phase A (sessions 4 to 6, `a1`)**, **V13 (session 7, `como-es`)** and **V14 (session 8, `ayer-en-el-pueblo`)**: as their entries say. V13 lets an order name its lesson; V14 strips the listener's name as a vocative (`src/Vocaterra.Game.Core/Npc/Vocative.cs`: `Luz, quiero pan`).
- **V16:** `tense: fut` at a step (only in an `a2` lesson); at a `fut` step the near future and the present with a time word are credited with the step's `futureNote`; the book's fourth column.
- **V17 (session 9, `manana-en-el-pueblo`, `a2`):** the simple future against `ir a` (`compraré`, `buscaré`, `iré`; `hará`, `tendré`, `trabajará`, `pescará` heard); the day of tomorrow told by Mar; M57's later markers in every `state_action` grammar; `para` + a village name after `buscar`'s item; `baker-open` takes plans (`state_action`), `fisher-open` does not (the lint's confusion check gives `buscaré madera` to `ask_directions` there). No order in session 9.
- **The core:** M56 (`ser` where a place takes `estar`; a subject after its verb), M57 (the later markers), M59 (the person checks; a lone name is never checked), and, landed just before you, **M58 and M62**: their entries in the core plan §5b say what they grade; in short:
  - **M58:** `¿cuánto costa el pan?` is `ask_price` with `conj.irregular` (expected `cuesta`), and `hay pans` is `ask_availability` with `noun.number` (expected `panes`). Restore the stand-ins of rows `cuanto-cuesta` and `hay-pan`. Not graded: `los panes costa`, `azuls`.
  - **M62:** a predicate adjective agrees with its subject across `ser` and `estar`, past `muy`, `más`, `menos` or `tan`, in a comparison and joined by `y`: `la manzana es rojo` gives `gender.agree` (`roja`), `el pan es grandes` `noun.number` (`grande`), `las manzanas son caro` both; `la manzana es muy caro`, `el pan es muy cara` and `la manzana es más barato que el pan` are graded. A lone name is never given a gender (`Luz es alto` is clean). Restore the stand-ins of rows `la-manzana-es-roja` and `es-grande`. A colour before its noun gives `word_order`: `quiero tres rojas manzanas` at `farmer-open` gives `word_order` with intent `unknown`; with `[ADJ]?` before `[NOUN:item]` in `request_item` it would fill the quantity and the item and still give `word_order` (your choice, for row `quiero-tres-manzanas-rojas`; keep it only if the fuzz project passes after it, since the generator builds correct sentences from that grammar). Not graded: an adjective that is also a noun after the copula with no `muy` and no grammar (`él es alta`), and the gender of a subject with `de …`.
  - **The older fuzz findings left for the owner** (seeds `egVgLELrxyL4`, `2ldq2rIKLrG2`, `b3HSKSLCsL_1`) and M63's (`8zKtIhOY3951`, `c5SlpISRmTZ3`): if a run of the fuzz project fails on one of these seeds alone, rerun it once and say so in the report; do not change the oracle.
- **V10 to V17 are the models to follow**: their lessons, NPC files, sheet rows, intents, tests (`TomorrowLessonTests`, `TomorrowSpokenTests`, `YesterdayLessonTests`, `VillageTalkTests` and the earlier ones), locale text, checks and commits (`git log --oneline -80 feature/village`).
- **The stall's purchase** (V03, V11): `request_item` with `qty` at a prompt whose host event's action is `sell` takes `qty x price` coins and gives `qty` items; too few coins raise `<npc>.cant_pay`. Prices: bread 2 at the baker and the stall, an apple 1 at the farmer.
- **V12c:** sleeping at the player's fire advances the session; `--skip-to-session 10` must open your lesson; test it.
- **V18:** the content loop is one command (below).

## Read first (the sections named, not whole files: every line you read stays in your context for the rest of the run)
- `CLAUDE.md`.
- `docs/game/VILLAGE_PLAN.md` (read only these sections, by `grep -n '^##'` and ranges). Your milestone is V15:
  - section 2.9 (verbs in three times);
  - section 3.1 (principles 6 and 7: an order is a plan, a doing and a report), 3.3 (the people), 3.4 (the items, the orders, the prices and the paragraph on `buy`), and 3.5: the arc table's session-10 row, the ladder's row 10, and the session-10 bullet of "Sessions 7 to 10";
  - 3.6 (the `offer_item` row and the paragraph after the table), 3.7, 3.8 (V03's row: `buy`), 3.9, 4 ("Every content milestone") and 5 ("Confusion between intents", "Names that are nouns", "Not overwhelming").
- `docs/game/DECISIONS.md`: D-game-10, D-game-12 and D-game-22 only.
- `content/README.md` (a step's person and tense, the two notes, the time words, "The edit loop"); `content/PHRASES.md`: its header and column notes, then only the rows of sessions 5, 7 and 9 and the "meanwhile" notes (grep them); the lessons `numeros-y-pan`, `como-es` and `manana-en-el-pueblo` with their locale text; `docs/nlp/API.md` "Tense errors" and the tags your error examples use; `docs/game/README.md`, the V16 and V17 sections.
- The code of the stall's purchase and the delivery (`sell`, `cant_pay`, `give_item`, `take_item`; grep `src/Vocaterra.Game.Core` for them) before you design `buy`.
- **Do not read** `docs/game/SPEECH_CHECKLIST.md`, `content/game-v1/manifest.json`, or the goldens, whole: grep for what you need.

## Output and context (the cost of a milestone is its turns times its context)
- Send every heavy command's output (builds, tests, `content.sh`, prerender, scripted runs, LanguageTool) to a log file under `/tmp/claude-1000/`, and print at most 30 lines of it (the summary, `grep` for failures, `tail`).
- Keep `sed -n` ranges under about 150 lines; prefer `grep -n` to find the place first.
- Wait for a long command by running it in the foreground through `scripts/limited.sh`, not by polling a log with repeated `tail`. **Never leave a wait loop behind** (`until ! pgrep -f …` matches its own command line and never ends): run commands in the foreground.
- After the first edit of the bundle, run the grammar-sensitive fuzz classes at once (`scripts/test.sh project tests/Vocaterra.Nlp.Fuzzing.Tests`), not only the fast tier, so that a grammar fault shows before the full run. **`offer_item`'s grammars and any `[ADJ]` or comparison you add to a grammar are what the fuzz generator will build correct sentences from**: the core's checks (M62's predicate agreement, `word_order`) must agree with them.
- **Since M57 the fuzz suite's stream digests run on frozen inputs**: a content edit does not move them. If one moves, something other than content changed: report it.
- **Known core limits you must not rely on:** one intent per line (below, deliverable 6); a word outside a constrained prompt's answer set gets `vocab.wrong` (`usted comprará pan` at a lesson step); an object put first with no clitic (`las manzanas vende Luz`) reads as a subject (M59's known limit). A wrong grade is a core gap: report it, never patch the core.

## What to build (VILLAGE_PLAN 3.4, 3.5 session 10, the ladder's row 10, 3.6 and section 5)
1. **The lesson `el-mercado` "El mercado"**, level `a2`, with the baker, the farmer and the shopkeeper, reached by sleeping at the player's fire from session 9 (`--skip-to-session 10`):
   - the numbers 11 to 20 (heard and said, in a price or a quantity);
   - `cuanto-es-todo` "¿Cuánto es todo?"; `es-muy-caro` "Es muy caro."; `mas-barato-que` (`más barato que`); `puedo-pagar-con-fruta` "¿Puedo pagar con fruta?" (`poder`); `vendo-madera` "Vendo madera.";
   - **the three times reviewed**, the series' end: `compre-tres-manzanas` "Compré tres manzanas." (past), `quiero-vender-madera` "Quiero vender madera." (now), `manana-vendere-fruta` "Mañana venderé fruta." (future, `tense: fut`; `voy a vender fruta` credited with the `futureNote`, as V16 built);
   - an opening review (two candidates from sessions 8 and 9) and a closing review, as V17 did;
   - what the learner says is true in the world (V14's rule): what they bought, what they sell, what they will sell.
2. **The error examples, tried on the CLI first**: `¿cuánto cuestan el pan?` (`conj.person`), `¿cuánto costa el pan?` (`conj.irregular`), `hay pans` (`noun.number`), `la manzana es muy caro` and `la manzana es más barato que el pan` (`gender.agree`), `quiero tres rojas manzanas` (`word_order`), and for the new rows `mañana vendí fruta` (`conj.tense`), `yo venderás fruta` (`conj.person`), `puedo pago con fruta` (if the core grades it; else another). Restore in `content/PHRASES.md` the stand-in examples M58 and M62 now grade (listed under "What exists"), as V14 restored M56's and M59's.
3. **`offer_item` and `buy`** (3.6, 3.4): the new intent `offer_item` (`[VERB:vender] [NOUN:item]`, `[VERB:tener] [NOUN:item] para (ti|usted)`; keyword `vender`; at the baker and the smith, and the farmer if the lesson needs it) and the host's `buy` (the NPC pays for what the player sells, the stall's `sell` turned round): the series' one repeatable coin source. **Design it last**, and check it against `state_want` and `state_have` with the lint's confusion check (VILLAGE_PLAN section 5); the prompt's `allowedIntentIds` is the first lever. Decide and say: what each NPC buys (the goods a learner can gather again: wood, fruit, apples, fish if any), at what price, how often (a cap per session or per day, so that a learner cannot farm coins in one talk, or none, with the reason), what an NPC says when the player has none of the item, and `quiero vender madera` (`state_want` with `vender`) against `vendo madera` (`offer_item`): both must sell, or say why not. Nothing is lost on a miss (the shopkeeper's bottle rule is the model). Tests for each case.
4. **The coin path:** a learner arrives at session 10 with 0 coins, 3 apples, 2 loaves and the axe (V14, V17: unchanged by session 9). Say what the market's purchases cost, how `buy` makes every purchase of the lesson reachable, and what the learner leaves with. **A learner with 0 coins must be able to finish the lesson** (test it from `--skip-to-session 10`). `¿Puedo pagar con fruta?` is a real question in the world: say what the answer is and whether the game does it.
5. **`¿Cuánto es todo?`**: the total of a basket (the stall's `sell` with `qty x price`, V03): say what "todo" sums (the items asked for in this talk, or the basket) and test it; the answer gives a number from 11 to 20 where it can.
6. **One intent per line** (found at V14): `hola Luz quiero dos panes` (and `hola, quiero dos panes`) resolves only the greeting, so nothing is bought; a market makes this likelier. Try it on the host first. Make the purchase happen in the game, as V14 took the vocative out (`Vocative.cs` is the model): for example a greeting that opens a line whose rest resolves an intent the prompt accepts is answered as that intent (say what the greeting credits, if anything). If it cannot be done in the game without the core reading two intents, stop that part and report it (a core gap). Also try `quiero el pan de Luz` at `baker-open` (no intent since V14); fix it in the bundle if a grammar can take it cleanly, or say why not.
7. **The order of the session** follows principle 7 (VILLAGE_PLAN 3.1): a plan, a doing and a report, in the three times. Session 9 had none (a fifth order to Luz would replay her single thanks-and-reward line and pay her 4 coins again). If session 10 has an order, it must not replay an old one; if it has none, say why.
8. **Checks:** about 8 items with `lesson: el-mercado`.
9. **The book:** the forms taught (`compré`, `quiero vender`, `venderé`, `vendo`, `puedo`) in their columns; the counts on the Lessons page. The Lessons page after session 10 says the series is done (no `next:` to a lesson that does not exist).
10. **Tests**, in the style of V14 and V17:
    - the lesson loads and validates at `a2`; every prompt and phrase resolves;
    - the `fut` step credits and misses as V16 built (`mañana venderé fruta` credited; `mañana voy a vender fruta` credited with the `futureNote`; `vendí fruta` a miss with the `tenseRetry`);
    - `offer_item` and `buy`, each case of deliverable 3; the coin path of deliverable 4; the total of deliverable 5; the greeting of deliverable 6;
    - `--skip-to-session 10` opens `el-mercado`, with the objective and the beacon on its first NPC.

## The content loop (as V10 to V17 did it; D-game-10 and D-game-12)
- **Sheet rows first** (`content/PHRASES.md`, `game/phrases.json`, the locale glosses). Each said row has a graded learner-error example, and every new row is `llm-opus (provisional)`.
- **Try the core first**, on every canonical text and error example, before relying on a grade. Use the CLI (`dotnet run --project tools/Vocaterra.Nlp.Cli -- eval --transcript "<text>" --prompt <prompt> --content content/game-v1`) through a wrapper script under `/tmp/claude-1000/`, because the sandbox refuses the word `eval`. Also a few natural, correct lines a learner may say at these NPCs (with a name, `usted`, `ustedes`, a number from 11 to 20, `más caro que`, `tan barato como`) to check nothing correct is flagged. A wrong grade is a core gap. Change the example or the row, do not patch the core, and report the exact sentence and what it gave; the orchestrator files core milestones.
- **An Opus pass** over every new Spanish string before any WAV: naturalness, a Latin American neutral register (`ustedes`, not `vosotros`), words within the level, accents and `¿¡`. List the changes in the report.
- **Words:** the gate's lint (A2, fail-on-level) is clean, and the validator passes the lesson at its level. A word above A2 fails. `barato`, `caro`, `todo`, `poder` and `vender` are in the ladder's row 10.
- **The content loop, one command (V18):** after each round of content edits run `scripts/content-loop.sh --grammar --speech` directly (never under `scripts/limited.sh`: it calls it itself and refuses to run nested). It runs the refresh, the LanguageTool cache with `--prune` and CI's rebuilt-from-nothing check, the prerender of the new lines, the new `bundleHash` into every save fixture, the build, the goldens and the stream digests (checked), and the fast tier with the grammar-sensitive fuzz classes; it prints a summary of at most 30 lines and stops at the first failure with the next thing to do. Its header (`scripts/content-loop.sh`) and `content/README.md` ("The edit loop") say the rest.
  - **Read every grammar finding it prints**: fix the content or add a waiver with its reason. Names read as other words (`Domingo` as the day, `Mar` as the sea, `León` as the animal) take text waivers, as V12 did.
  - **WAVs:** use the NPCs' existing voices. The voices are provisional (the owner has not heard them); the checklist rows are for the owner.
  - **When the goldens move,** run it with `--record` and commit the goldens and the identity baseline in a commit of their own, listing in the report which entries moved and why.
  - Run it once with `--tier full` before the report (the whole suite, as the gate). Never weaken a fuzz oracle.
- **With models:** the spoken round trip (as the `*SpokenTests` do) over the new said rows. The numbers 11 to 20, `venderé`, `puedo` and `barato` must be heard. A word the recognizer cannot hear goes to the heard side, or to a typed-only variant; report it.
- **Timing:** the scripted lesson runs with `--skip-to-session 10 --lesson-run el-mercado` and, for V17 (whose agent was stopped before it), `--skip-to-session 9 --lesson-run manana-en-el-pueblo`; then your estimate in minutes for each, counting reading, audio, turns and walking (target 5 to 10).
- **Scripted runs:** run `scripts/scripted-runs.sh` before your first edit and after; none should change apart from what the new lesson adds (the Lessons page's line count, the speech file count). Explain every difference. V17's agent did not capture an "after": your "before" is the first capture with session 9, so also say whether anything in it looks wrong for V17 (the ninth lesson line, its speech files).
- **Test the learner's route,** not only `--lesson-run` (phase A's lessons were unreachable in practice until V12c, and V12b's wrong credit showed only in free talk): from `--skip-to-session 9`, play session 9's lesson (V17's route was not walked), sleep at the fire into session 10, and check the book's Lessons page (`next:`), the objective and the beacon as a learner would find them; play your lesson with 0 coins; then talk freely once with each NPC of the lesson (sell something, buy something, ask a total) and check the reply, the coins and the phrase credited.
- **CI's case:** run the full suite once without models, as CI does: `VOCATERRA_MODELS_DIR=<an empty folder under /tmp/claude-1000/> scripts/test.sh full` (the model-gated tests then skip). Report any test that measures wall-clock time and passes within 4 times of its budget (the GitHub runner is about four times slower than this machine; three older ones were near it at V16: the bundle load, the scene build and digest, the world snapshot).

## Resources (the desktop has crashed from overload)
- Run every `dotnet restore|build|test|format|run`, every `scripts/content.sh` call, every speech render and every screenshot run through `scripts/limited.sh`.
- Never start two heavy commands at once: no `&`, no background builds.
- While iterating, use `scripts/test.sh fast` or `scripts/test.sh project <test project>`. Run `scripts/test.sh full` once before reporting.
- Exit code 137 means the 6 GB cap killed the command: lower the parallelism, and never work around the cap.
- Disk is tight (about 10 GB free): delete capture directories you no longer need, and keep scripted-run captures to the two you compare.

## Environment
- Prefix every command inline with `PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1`.
- The sandbox refuses a command line that contains the words `eval` or `hash`. Use a wrapper script under `/tmp/claude-1000/` for those.
- Model-gated tests read `VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models`.
- Game runs need `DISPLAY=:0`.

## Rules
- **What a game milestone edits:** the game projects (`src/Vocaterra.Game.Core`, `src/Vocaterra.Game`, `src/Vocaterra.Host`, `tools/Vocaterra.SpeechRender`) and their tests; `content/game-v1`, `content/locale`, `content/schema`, `content/PHRASES.md`, `content/README.md`, `content/grammar` and `content/speech-index.json`; `assets/` and `docs/game/`.
- It takes the core (`src/Vocaterra.Nlp*`, `tests/Vocaterra.Nlp.*`, `tests/golden`, `docs/nlp/`) as it is, except for re-recording goldens and the identity baseline a content edit moved.
- Engine code goes only in `src/Vocaterra.Game`. No `System.Random` and no clock in the evaluation path.
- Do not edit `scripts/`, `docs/game/IMPLEMENTATION_PLAN.md`, `docs/game/VILLAGE_PLAN.md`, `docs/nlp/IMPLEMENTATION_PLAN.md` or either `DECISIONS.md`.
- Never create or edit anything under `.github/workflows/`.
- Write code and content that read like the surrounding ones.

## Gate (from the worktree root, each through scripts/limited.sh)
- `dotnet build Vocaterra.sln -warnaserror -m:4`, with 0 warnings.
- `dotnet test Vocaterra.sln -m:2` all passing. It was @@COUNT@@; report the new count.
- `dotnet format Vocaterra.sln --verify-no-changes`.
- `scripts/content.sh validate`.
- The content gate in `scripts/merge-gate.sh`: the lint at A2 with fail-on-level and `--host-pools 'guide.*'`, and the cefr-checks.
- The host's `--validate-content`.
- `scripts/content.sh grammar content/game-v1 --strings content/grammar/game-v1-strings.jsonl --strings-only --offline --strict`.
- **The host demos:**
  - `dotnet run --project src/Vocaterra.Host -- --transcript "quiero agua, por favor" --prompt stall-need` still prints `request_item`, communicated;
  - `--transcript "mañana compraré pan" --prompt baker-open` still prints `state_action`, communicated, with no error;
  - `--transcript "vendo madera" --prompt <its prompt>` and `--transcript "quiero vender madera" --prompt <its prompt>` print the intents you chose, communicated;
  - `--transcript "hola, quiero dos panes" --prompt <the baker's selling prompt>` buys the bread (or your report says why not);
  - `--transcript "¿cuánto costa el pan?" --prompt <its prompt>` gives `conj.irregular` and the price intent.
- Do not commit `bin/`, `obj/` or `artifacts/`.

## Commit
Each message ends with:
```
Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
```

## Report (one page, under 400 words; the details belong in the commits)
- The branch and the final commit SHA, pushed.
- What was built, by deliverable.
- The final said and heard rows (id and Spanish), and any dropped or added.
- The Opus pass's changes.
- The core tries and their results. A wrong grade is a core gap: give the exact sentence and what it gave.
- `offer_item` and `buy`: the design (what, where, price, how often), the confusion check's result, and the fuzz results after the grammar edit.
- One intent per line: what the game now does with `hola, quiero dos panes`.
- Recognizer results.
- The estimated minutes of sessions 9 and 10, and the coin path.
- The test count before and after (with models, and without: CI's case), and any timed test within 4 times of its budget.
- The new bundle hash; the goldens re-recorded, if any.
- The scripted-run differences, and the learner's route from session 9.
- Bugs found and fixed.
- Anything left undone, and why.
