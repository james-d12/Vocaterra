You are implementing milestone **V14, session 8 "Ayer en el pueblo"**, of the Vocaterra repository (C#, .NET 10: a Spanish-learning first-person voxel game on MonoGame, plus a language core). It is the second A2 lesson of phase B of `docs/game/VILLAGE_PLAN.md`:
- the villager, the smith Domingo and the fisher Mar;
- **the preterite for every person** the series still lacks: él/ella (`fue`, `hizo`, `compró`, `vendió`, `pescó`), nosotros (`fuimos`, `compramos`), ellos (`fueron`, `comieron`), on verbs met before;
- the day of the village, told by the villager and retold by the learner;
- `tener que` + infinitive in the present (`tengo que ir …`), with the new intent `state_obligation`.

Do the work yourself; do not spawn subagents.

## Where to work, and pushing
Work only in the existing git worktree `/home/user/Dev/Vocaterra/.claude/worktrees/village`: cd there first, and never edit `/home/user/Dev/Vocaterra` itself or any other worktree. It is on branch `feature/village` (at the HEAD `git log -1` shows, `12ea9d7`).
- Create your branch with `git checkout -b V14-ayer-en-el-pueblo feature/village`.
- **The owner wants work pushed often.** Commit after each coherent step that builds and passes `scripts/test.sh fast`, with the prefix `V14: `.
- Push after every commit: `git push -u origin V14-ayer-en-el-pueblo` the first time, then `git push origin V14-ayer-en-el-pueblo`.
- Never push `main` or `feature/village`, and do not merge.

## What exists (read the V entries at the end of `docs/game/IMPLEMENTATION_PLAN.md` section 5 first, V01 to V13)
- **Phase A (sessions 4 to 6, all `a1`):** the fisher Mar (`el-pueblo`, the order `fruit-for-mar`), the baker Luz (`numeros-y-pan`, `oven-wood`, the bread sale), the smith Domingo and the farmer León (`gente-del-pueblo`, the order `axe-handle`). V12 taught the third person in the present, `va a hacer` and `hizo`, and gave `state_action` a closed list of names before the verb (`domingo|luz|mar|león`).
- **V13 (session 7, `como-es`, the first `a2` lesson):** the farmer León; `describe`, `state_like` (`me gusta`, `me gustan`, `me gustó`) and `[ADJ]?` after the noun in `request_item`; the apple sale (1 coin) and the order `water-for-plants` (1 bottle of water, 3 apples). **An order may now name its lesson** (`"lesson": "como-es"`, checked by the validator): walking up with the goods delivers nothing until that lesson reaches its delivery step. A learner reaches session 8 with 0 coins and 3 apples. Its findings, deviations and core gaps are the V13 entry of section 5.
- **V10 to V13 are the models to follow.** Their lessons, NPC files, sheet rows, intents, tests (`VillageLessonTests`, `BakeryLessonTests`, the session-6 and session-7 tests, the `*SpokenTests`), locale text, checks and commits (`git log --oneline -60 feature/village`). Read them before you design yours.
- **V12b:** each village NPC has a `focus`; a prompt that is the home of practised rows names one in its NPC's focus (a validator rule).
- **V12c:** a session advances by sleeping at the player's own fire once the session's work is done; `--skip-to-session n` starts at session n with the earlier sessions counted as done, built from the data. It must work for `--skip-to-session 8` with your lesson; test it.
- **V08:** every lesson declares `level`; the validator holds its Spanish to its level's cumulative list and its `verbs` tenses to its level; the gate's lint is A2 with fail-on-level.

## Read first (the sections named, not whole files: every line you read stays in your context for the rest of the run)
- `CLAUDE.md`.
- `docs/game/VILLAGE_PLAN.md` (71 KB; read only these sections, by `grep -n '^##'` and ranges). Your milestone is V14:
  - section 2.9 (verbs in three times);
  - section 3.3 (the people) and 3.5: the arc table's session-8 row, the ladder (session 8: `tengo que` + infinitive; `fue`, `compró`, `vendió`, `pescó`; `fuimos`, `compramos`; `fueron`, `comieron`; `va a` + infinitive reviewed), and the session-8 bullet of "Sessions 7 to 10";
  - 3.6 (the `state_obligation` row, its confusion with `state_have`, and the paragraph on the changes to `state_action`);
  - 3.7, 3.9, 4 ("Every content milestone") and 5 (the places the core must be tried first, the names that are nouns, "A tense is graded only next to a marker", "Not overwhelming": session 8 is the one lesson with three new persons of a known tense).
- `docs/game/DECISIONS.md`: D-game-10, D-game-12 and D-game-22 only.
- `content/README.md`; `content/PHRASES.md` (94 KB): its header and column notes, then only the rows of sessions 6 and 7 (grep them); the lessons `gente-del-pueblo` and `como-es` with their locale text; `docs/nlp/API.md` "Tense errors"; `docs/game/README.md` "Refreshing the cache".
- **Do not read** `docs/game/SPEECH_CHECKLIST.md` (71 KB, a generated table; the renderer writes it), `content/game-v1/manifest.json`, or the goldens, whole: grep for what you need.

## Output and context (the cost of a milestone is its turns times its context)
- Send every heavy command's output (builds, tests, `content.sh`, prerender, scripted runs, LanguageTool) to a log file under `/tmp/claude-1000/`, and print at most 30 lines of it (the summary, `grep` for failures, `tail`).
- Keep `sed -n` ranges under about 150 lines; prefer `grep -n` to find the place first.
- Wait for a long command by running it in the foreground through `scripts/limited.sh`, not by polling a log with repeated `tail`.
- After the first edit of the bundle, run the grammar-sensitive fuzz classes at once (`scripts/test.sh project tests/Vocaterra.Nlp.Fuzzing.Tests`), not only the fast tier, so that a grammar fault shows before the full run.
- **The core's M56 and M59 have just landed** (the owner moved them before V14 on 2026-10-31): M56 at `d87e823`, M59 at `12ea9d7` (their entries in `docs/nlp/IMPLEMENTATION_PLAN.md` §5b). What the core now grades: `ser` where a place takes `estar` (`es lejos`, `¿dónde es la herrería?` → `ser_estar`; an event keeps `ser`); a subject after its verb (`¿dónde están la herrería?`, `¿cuánto cuestan el pan?`, `me gusta las manzanas` → `conj.person`); a determiner phrase or names joined by `y` before a third-person verb of the other number (`los niños come pan`, `las manzanas es roja`, `Luz y Domingo comió pan` → `conj.person`); the reflexive clitic (`cómo te llama` → `llamas`); `va a hace un hacha` (`conj.tense`); and `hace` read as the verb unless a time follows. **What it leaves to the lesson on purpose:** a lone name before a verb is never checked, in any person, because it may be the person spoken to (`Luz, quiero pan`; transcripts carry no commas). So `Luz hice pan` or `Mar pescaste en el río` at a retelling step is the lesson's own person miss (its `personRetry`), not a core error, and no first- or second-person verb is checked after any noun phrase. `Domingo trabajo en la herrería` and `Domingo voy a hacer un hacha` stay clean to the core.
- **Known core gaps you must not rely on** (`docs/nlp/IMPLEMENTATION_PLAN.md` §5): the fuzz generator knows only `mañana` as a future marker (M57, after V16); `cuánto costa` and `hay pans` (M58, before V15); a predicate adjective across `ser` (`la manzana es rojo`) and a colour adjective before its noun (M62, before V15); an object put first with no clitic (`las manzanas vende Luz`) is read as a subject (M59's known limit).

## What to build
1. **The lesson `ayer-en-el-pueblo`** (session 8, `after: como-es`, `level: a2` as the plan says unless the validator shows a reason; say which and why), its locale text and `lesson.ayer-en-el-pueblo.objective`. Base it on VILLAGE_PLAN 3.5's session-8 design. Write the final rows yourself (proposals, the sheet's columns, D-game-10).
   - **The learner hears** (listen steps, at the villager): the day of the village in short lines, for example "Ayer Luz hizo pan. Domingo trabajó en la herrería. Mar pescó en el río.", and a line that gives the plural (`Luz y Domingo comieron pan en la plaza.` or the like) and the nosotros (`¿Adónde fueron?`). The villager models each new form before the learner says it, as V11's Luz modelled `-é`.
   - **The learner says** (proposals; the plan names `luz-hizo-pan`, `mar-pesco-en-el-rio`, `ellos-comieron-pan`, `fuimos-al-rio`, `tengo-que-ir`):
     - `luz-hizo-pan` "Luz hizo pan." (`state_action`, the step expects `pret`, `3sg`; error example with a marker, for instance `ayer Luz hace pan`, `conj.tense` expected `hizo`);
     - `mar-pesco-en-el-rio` "Mar pescó en el río." (`state_action` gains `pescar` if it lacks it; `pret`, `3sg`);
     - `ellos-comieron-pan` "Ellos comieron pan." (`pret`, `3pl`; error example `ellos comió pan`, `conj.person`, if the core grades it);
     - `fuimos-al-rio` "Fuimos al río." (`pret`, `1pl`, to `¿Adónde fueron?`; `conj.irregular` must stay silent on `fuimos`; error example `nosotros fue al río` or another the core grades);
     - `tengo-que-ir` "Tengo que ir a …" (`state_obligation`, the present; error example `yo tiene que ir …`, `conj.person`);
     - optionally a `compramos` or `vendió` row if the lesson has room under its ten-minute ceiling; eight said rows is the most;
     - two `review` steps (rows from sessions 4 to 7).
   - **Truth.** Every line the learner says must be true in the game when it is said (V11 replaced "Tengo madera" for this reason). "Fuimos al río" needs a "we" who went: design the step so that it is true (for example the fisher and the learner walk to the river bank together in a `do` step, or the question names a walk the learner has made with someone); say how you made it true. The same for "Tengo que ir a …": name a place the lesson then sends the learner to.
   - **The NPCs:** the villager, the smith and the fisher. Every `hears` line is a `guide.lesson.<npc>.*` pool with the NPC as its speaker (V01's rule).
   - **The lesson's `verbs` for the book:** the preterite persons it teaches (`hacer`, `trabajar`, `pescar`, `comer`, `ir`, and `comprar` or `vender` if a row uses them) and `tener` (`tengo que`). Check the book shows the new persons (the forms the core's morphology knows).
   - **No order and no sale** (the plan's "Mechanics: none"): the lesson must not need coins or goods. Read V13's coin path and say what a learner holds at session 8.
2. **Intents** (VILLAGE_PLAN 3.6), each with error examples:
   - `state_obligation`: `[VERB:tener] que [VERB:ir] a [ART] [NOUN:place]`, `[VERB:tener] que [VERB:comer] …` (or the verbs your rows need); its keyword rule (`tener` followed by `que`) must win over `state_have`'s `tener` + item. Run the lint's confusion check against `state_have`; the prompts' `allowedIntentIds` are the first lever.
   - `state_action` gains what the rows need: `pescar` (`en [ART] [NOUN:place]`), a plural pronoun subject (`ellos`, `nosotros`), and a subject of two names joined by `y` only if a row needs it. V12 found that an optional pronoun before the verb made the fuzz suite generate `qué tú hace`: keep subjects where they cannot do that.
   - **If the fuzz suite fails on a new grammar, change the grammar, never the oracle**, and report it.
3. **Four open points at the NPCs of this lesson** (the findings log, V12, V12b and M59):
   - **The open prompts and statements.** At the open prompts a correct sentence of another intent is forced into the prompt's intents: `Domingo hizo un hacha` at `smith-open` gives `ask_activity` with `vocab.wrong` expected `''`. A lesson that retells the village's day makes this likely in free talk. Let the open prompts of the NPCs this lesson visits (`smith-open`, `fisher-open`, the villager's) take `state_action`, with a short reply line, if the lint's confusion check stays clean and the fuzz suite passes; test that `Domingo hizo un hacha` at `smith-open` is `state_action` and error-free. If it does not fit, say why and leave it.
   - **The bakery order's timing.** V13 found that a learner who already carries an order's goods hands them over on the first talk, before saying the plan, and gave `water-for-plants` its lesson. `oven-wood` (session 5) has the same hazard if the learner carries 5 wood when first talking to Luz, before `voy a buscar madera`: check it (and `axe-handle`, which V12 ordered by starting at the villager), give the order its lesson if needed, test it, and explain the scripted-run differences (r18).
   - **The villager's credit.** At the villager, "¿dónde está la panadería?" credits `donde-esta-el-rio` (V12b left it: the check that would fix it changes lesson credit). Fix it with V12b's rule (of two rows with one intent, the one naming the place asked) or say why not, with a test either way.
   - **A learner who addresses the NPC by name** (found by the orchestrator at M59): at the open prompts a line that starts with the listener's name resolves no intent and gets `vocab.wrong` (`Luz quiero pan` at `baker-open` → `unknown`; `hola Luz quiero dos panes` → `greet` only; `Domingo necesito un hacha` at `smith-open` → `unknown`). Session 8 teaches the villagers' names, so learners will open with them. Find where it fails (the prompts' grammars, the answer sets, the host's reading of names), and make a leading or trailing vocative name (`Luz, quiero pan`, `quiero pan, Luz`) resolve as the line without it, at every open prompt and the lesson prompts, in the game's data or code; the core since M59 leaves a lone name before a verb unchecked (it may be the listener). Test it on the CLI and in free talk; if it needs the core, report it.
4. **The sheet's stand-in examples.** Phase A's rows use stand-in error examples where the core could not grade the real slip (`grep -n 'meanwhile\|M56\|M59' content/PHRASES.md`). Where M56 and M59 now grade the real slip, put it back as the example (tried on the CLI first) and drop the "meanwhile" note. By their reports: `esta-lejos` (`es lejos`, `ser_estar`; its intent stays `unknown` unless `ask_far` also takes `ser`: decide), `donde-esta-la-herreria` (`¿dónde están la herrería?`), `me-gusta-el-pan` (`me gustan el pan`), `me-gustan-las-manzanas` (`me gusta las manzanas`), `cuanto-cuesta` (`¿cuánto cuestan el pan?`; `cuánto costa` waits for M58), `como-te-llamas` (`¿cómo te llama?`), and V13's `la manzana son roja` / `las manzanas es roja` at `lesson-apple`. Not restorable, by design: `trabaja-en-la-herreria` and `va-a-hacer-un-hacha` keep the pronoun (a lone name is never checked).
5. **Checks:** about 8 items with `lesson: ayer-en-el-pueblo`.
6. **Tests**, in the style of V10 to V13:
   - the lesson loads and validates at its level; every prompt and phrase resolves;
   - the tense and person steps credit and miss as designed (`ayer Luz hace pan` is the core's `conj.tense`; `Luz hace pan` at the `pret` step is the lesson's Miss with `tenseRetry`; a wrong person without a pronoun is the lesson's person Miss, not a core error);
   - `state_obligation` resolves at its prompt and `tengo dos panes` still resolves as `state_have`;
   - the open points of deliverable 3;
   - `--skip-to-session 8` opens `ayer-en-el-pueblo`, with the objective and the beacon on its first NPC.

## The content loop (as V10 to V13 did it; D-game-10 and D-game-12)
- **Sheet rows first** (`content/PHRASES.md`, `game/phrases.json`, the locale glosses). Each said row has a graded learner-error example, and every new row is `llm-opus (provisional)`.
- **Try the core first**, on every canonical text and error example, before relying on a grade. Use the CLI (`dotnet run --project tools/Vocaterra.Nlp.Cli -- eval --transcript "<text>" --prompt <prompt> --content content/game-v1`) through a wrapper script under `/tmp/claude-1000/`, because the sandbox refuses the word `eval`. In particular (VILLAGE_PLAN section 5): `fuimos al río` clean (`conj.irregular` silent on `fuimos`); `Mar pescó en el río`, `Luz hizo pan` and `Domingo trabajó en la herrería` clean, the name read as a third-person subject; `ellos comieron pan` clean and `ellos comió pan` `conj.person`; `ayer Luz hace pan` `conj.tense` expected `hizo`; `tengo que ir a la panadería` clean and `yo tiene que ir a la panadería` `conj.person`; `fueron`, `compramos` and `vendió` read as `ir`, `comprar` and `vender`. A wrong grade is a core gap. Change the example or the row, do not patch the core, and report the exact sentence and what it gave; the orchestrator files core milestones.
- **An Opus pass** over every new Spanish string before any WAV: naturalness, a Latin American neutral register (`ustedes`, not `vosotros`), words within the level, accents and `¿¡`. List the changes in the report.
- **Words:** the gate's lint (A2, fail-on-level) is clean, and the validator passes the lesson at its level. A word above A2 fails.
- **The content loop, one command (V18):** after each round of content edits run `scripts/content-loop.sh --grammar --speech` directly (never under `scripts/limited.sh`: it calls it itself and refuses to run nested). It runs the refresh (manifests, provenance, validate, the gate's lint and cefr-checks, `--validate-content`, the grammar strings and the offline grammar check), the LanguageTool cache with `--prune` and CI's rebuilt-from-nothing check, the prerender of the new lines and the manifest after it, the new `bundleHash` into every save fixture, the build, the goldens and the stream digests (checked), and the fast tier with the grammar-sensitive fuzz classes; it prints a summary of at most 30 lines and stops at the first failure with the next thing to do. Its header (`scripts/content-loop.sh`) and `content/README.md` ("The edit loop") say the rest.
  - **Read every grammar finding it prints**: fix the content or add a waiver with its reason. Names read as other words (`Domingo` as the day, `Mar` as the sea) take text waivers, as V12 did.
  - **WAVs:** use the NPCs' existing voices. The voices are provisional (the owner has not heard them); the checklist rows are for the owner.
  - **When the goldens move,** run it with `--record` and commit the goldens and the identity baseline in a commit of their own, listing in the report which entries moved and why. The fuzz suite's `FuzzLanguageStreamTests` digests are still constants in its C# (until the core's M57): the loop prints the new values and the constants to set; set them with the generators unchanged and say so in the commit and the report.
  - Run it once with `--tier full` before the report (the whole suite, as the gate). Never weaken a fuzz oracle.
- **With models:** the spoken round trip (as the `*SpokenTests` do) over the new said rows. `hizo`, `pescó`, `comieron`, `fuimos`, `fueron` and `tengo que` must be heard. A word the recognizer cannot hear goes to the heard side, or to a typed-only variant; report it.
- **Timing:** the scripted lesson run with `--skip-to-session 8` and `--lesson-run ayer-en-el-pueblo`, then your estimate in minutes, counting reading, audio, turns and walking (target 5 to 10).
- **Scripted runs:** run `scripts/scripted-runs.sh` before and after; none should change apart from what the new lesson adds (for example, the Lessons page's line count). Explain every difference.
- **Test the learner's route,** not only `--lesson-run` (phase A's lessons were unreachable in practice until V12c, and V12b's wrong credit showed only in free talk): from `--skip-to-session 7`, finish session 7, sleep at the fire into session 8, and check the book's Lessons page (`next:`), the objective and the beacon as a learner would find them; play the lesson; then talk freely once with each NPC of the lesson and check the reply and the phrase credited.
- **CI's case:** run the full suite once without models, as CI does: `VOCATERRA_MODELS_DIR=<an empty folder under /tmp/claude-1000/> scripts/test.sh full` (the model-gated tests then skip). Report any test that measures wall-clock time and passes within 4 times of its budget (the GitHub runner is about four times slower than this machine; M60 failed only there).

## Resources (the desktop has crashed from overload)
- Run every `dotnet restore|build|test|format|run`, every `scripts/content.sh` call, every speech render and every screenshot run through `scripts/limited.sh`.
- Never start two heavy commands at once: no `&`, no background builds.
- While iterating, use `scripts/test.sh fast` or `scripts/test.sh project <test project>`. Run `scripts/test.sh full` once before reporting.
- Exit code 137 means the 6 GB cap killed the command: lower the parallelism, and never work around the cap.
- Disk is tight (about 13 GB free): delete capture directories you no longer need.

## Environment
- Prefix every command inline with `PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1`.
- The sandbox refuses a command line that contains the words `eval` or `hash`. Use a wrapper script under `/tmp/claude-1000/` for those.
- Model-gated tests read `VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models`.
- Game runs need `DISPLAY=:0`.

## Rules
- **What a game milestone edits:** the game projects (`src/Vocaterra.Game.Core`, `src/Vocaterra.Game`, `src/Vocaterra.Host`, `tools/Vocaterra.SpeechRender`) and their tests; `content/game-v1`, `content/locale`, `content/schema`, `content/PHRASES.md`, `content/README.md`, `content/grammar` and `content/speech-index.json`; `assets/` and `docs/game/`.
- It takes the core (`src/Vocaterra.Nlp*`, `tests/Vocaterra.Nlp.*`, `tests/golden`, `docs/nlp/`) as it is, except for re-recording goldens and digests a content edit moved.
- Engine code goes only in `src/Vocaterra.Game`. No `System.Random` and no clock in the evaluation path.
- Do not edit `docs/game/IMPLEMENTATION_PLAN.md`, `docs/game/VILLAGE_PLAN.md`, `docs/nlp/IMPLEMENTATION_PLAN.md` or either `DECISIONS.md`.
- Never create or edit anything under `.github/workflows/`.
- Write code and content that read like the surrounding ones.

## Gate (from the worktree root, each through scripts/limited.sh)
- `dotnet build Vocaterra.sln -warnaserror -m:4`, with 0 warnings.
- `dotnet test Vocaterra.sln -m:2` all passing. It was 6431 passed and 3 skipped; report the new count.
- `dotnet format Vocaterra.sln --verify-no-changes`.
- `scripts/content.sh validate`.
- The content gate in `scripts/merge-gate.sh`: the lint at A2 with fail-on-level and `--host-pools 'guide.*'`, and the cefr-checks.
- The host's `--validate-content`.
- `scripts/content.sh grammar content/game-v1 --strings content/grammar/game-v1-strings.jsonl --strings-only --offline --strict`.
- **The host demos:**
  - `dotnet run --project src/Vocaterra.Host -- --transcript "quiero agua, por favor" --prompt stall-need` still prints `request_item`, communicated;
  - `--transcript "me gustan las manzanas" --prompt farmer-open` (or V13's prompt for it) still prints `state_like`, communicated;
  - `--transcript "tengo que ir a la panadería" --prompt <its prompt>` prints `state_obligation`, communicated;
  - `--transcript "Mar pescó en el río" --prompt <its prompt>` prints `state_action`, communicated.
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
- The lesson's level, and why.
- The final said and heard rows (id and Spanish), and any dropped or added; how "Fuimos al río" and "Tengo que ir" are true.
- The Opus pass's changes.
- The core tries and their results. A wrong grade is a core gap: give the exact sentence and what it gave.
- The four open points of deliverable 3: done or not, and why.
- Recognizer results.
- The lesson's estimated minutes.
- The test count before and after.
- The new bundle hash; the goldens and digests re-recorded.
- The scripted-run differences, and the learner's route from session 7.
- Bugs found and fixed.
- Anything left undone, and why.
