You are implementing milestone **V13, session 7 "¿Cómo es?"**, of the Vocaterra repository (C#, .NET 10: a Spanish-learning first-person voxel game on MonoGame, plus a language core). It is the first A2 lesson of phase B of `docs/game/VILLAGE_PLAN.md`:
- the farmer León and the shopkeeper;
- adjectives after the noun, with agreement (`la manzana es roja`, `quiero tres manzanas rojas`);
- `me gusta` / `me gustan`, and **`gustar` in the past** (`me gustó el pan de Luz`);
- the water order `water-for-plants`, with its plan and report reviewed (`voy a buscar agua`, `fui al río`);
- apples for sale.

Do the work yourself; do not spawn subagents.

## Where to work, and pushing
Work only in the existing git worktree `/home/user/Dev/Vocaterra/.claude/worktrees/village`: cd there first, and never edit `/home/user/Dev/Vocaterra` itself or any other worktree. It is on branch `feature/village` (at the HEAD `git log -1` shows).
- Create your branch with `git checkout -b V13-como-es feature/village`.
- **The owner wants work pushed often.** Commit after each coherent step that builds and passes `scripts/test.sh fast`, with the prefix `V13: `.
- Push after every commit: `git push -u origin V13-como-es` the first time, then `git push origin V13-como-es`.
- Never push `main` or `feature/village`, and do not merge.

## What exists (read the V01 to V12c and V08 entries at the end of `docs/game/IMPLEMENTATION_PLAN.md` section 5 first)
- **Phase A (sessions 4 to 6, all `a1`):**
  - The fisher Mar, with `el-pueblo` and the order `fruit-for-mar`.
  - The baker Luz, with `numeros-y-pan`, the order `oven-wood` and the bread sale (`sell`, 2 coins a loaf).
  - The smith Domingo and the farmer León, with `gente-del-pueblo` and the order `axe-handle` (2 wood and 2 coins, which gives the axe).
  - **V10, V11 and V12 are the models to follow.** Their lessons, NPC files, sheet rows, intents, orders, tests (`VillageLessonTests`, `BakeryLessonTests`, the session-6 tests and the `*SpokenTests`), locale text, checks and commits (`git log --oneline -40 feature/village`). Read them before you design yours.
- **The farmer exists** (V12): `game/npcs/farmer.json`, `farmer-open` (names, jobs, where he lives), the figure and the voice `pm_santa` (speaker 44, provisional). He has no sale and no order yet; both are yours.
- **V12b:** each village NPC has a `focus` (the rows at home at its open prompt). There is a validator rule: a prompt that is the home of practised rows names one in its NPC's focus.
- **V12c:**
  - A session advances by sleeping at the player's own fire, once the session's work is done; a sleep is exactly a load.
  - The objective names the next lesson and its NPC, and the book's Lessons page says when and where each lesson opens.
  - `--skip-to-session n` starts at session n with the earlier sessions counted as done. It is built from the data, so it must work for `--skip-to-session 7` with your lesson; test it.
- **V08:** every lesson declares `level` (`a1` | `a2`). The game's validator checks a lesson's Spanish against its level's cumulative list, and its `verbs` tenses against its level. The gate's lint is A2 with fail-on-level, and `cefr-check --level a1` is a report only. Your lesson is the first `a2` one if it uses an A2 word; let the validator decide, and say which level you chose and why.

## Read first
- `CLAUDE.md`.
- `docs/game/VILLAGE_PLAN.md`, all of it. Your milestone is V13:
  - section 3.5: the arc table's session-7 row, the ladder (session 7: `me gusta`, `me gustan`, `es` + adjective; `me gustó`, `me gustaron`; `voy a buscar agua`, reviewed), and "Sessions 7 to 10";
  - 3.3 (the farmer's role), 3.4 (the order `water-for-plants`: 1 `agua` gives 3 `manzanas`; an apple costs 1 coin at the farmer);
  - 3.6 (`describe`, `state_like`, `[ADJ]` in `request_item`, and their confusion checks);
  - 3.7, 3.9, 4 ("Every content milestone") and 5 (the places the core must be tried first).
- `docs/game/DECISIONS.md`: D-game-10, D-game-12 and D-game-22.
- `content/README.md`, `content/PHRASES.md`, the lessons `numeros-y-pan` and `gente-del-pueblo` with their locale text, `docs/nlp/API.md` "Tense errors", `docs/game/README.md` "Refreshing the cache", and `docs/game/SPEECH_CHECKLIST.md`.
- **Known core gaps you must not rely on** (M56 to M59 in `docs/nlp/IMPLEMENTATION_PLAN.md`):
  - `ser` with location adverbs;
  - a plural verb with a singular subject gives no `conj.person`;
  - the fuzz generator's future markers;
  - `cuánto costa`;
  - `hay pans`;
  - `conj.person` after a name subject or a reflexive clitic;
  - `va a hace`.

## What to build
1. **The lesson `como-es`** (session 7, `after: gente-del-pueblo`, with its `level`), its locale text, and `lesson.como-es.objective`. Base it on VILLAGE_PLAN 3.5's session-7 design. Write the final rows yourself (proposals, the sheet's columns, D-game-10).
   - **The learner says:**
     - `la-manzana-es-roja` (`describe`; error example `la manzana es rojo`, `gender.agree`);
     - `quiero-tres-manzanas-rojas` (`request_item` with `[ADJ]`; error example `quiero tres rojas manzanas`, `word_order`, if the core grades it, else one it does);
     - `me-gusta-el-pan` and `me-gustan-las-manzanas` (`state_like`);
     - `me-gusto-el-pan`, "Me gustó el pan de Luz.", to the farmer's `¿Te gustó el pan?` (the step expects `pret`; error example `ayer me gusta el pan`, `conj.tense`);
     - `es-grande` or `es-pequeno` (`describe`);
     - the water order's plan `voy a buscar agua` (`ir-a`) and its report `fui al río` (`pret`), both reviewed;
     - two `review` steps.
   - **The learner hears:** the farmer's questions (`¿Cómo es la manzana?`, `¿Te gusta el pan?`, `¿Te gustó el pan de Luz?`), his description of the field or the apples, and the shopkeeper's line about the bread being Luz's (`el pan es de Luz`), if it fits.
   - **The NPCs:** the farmer and the shopkeeper (VILLAGE_PLAN 3.3). Every `hears` line is a `guide.lesson.<npc>.*` pool, with the NPC as its speaker (V01's rule).
   - **The lesson's `verbs` for the book:** `gustar` (`me gusta`, `me gustan`, `me gustó`; the forms the core's morphology knows), `ser` (`es`), and `buscar` and `ir` reviewed.
2. **Intents** (VILLAGE_PLAN 3.6), each with error examples:
   - `describe`: `[ART]? [NOUN:object]? [VERB:ser] [ADJ]`, `(esto|eso) [VERB:ser] [ADJ]`; no keyword rule.
   - `state_like`: `me [VERB:gustar] [ART] [NOUN:item]`, `(sí|no)? me [VERB:gustar]`; keyword `gustar`. It takes any tense of `gustar`.
   - `request_item` gains `[ADJ]?` after the noun.
   - Run the lint's confusion check: `describe` against `name_object` (a noun against an adjective), and `state_like` against `state_want`. The prompts' `allowedIntentIds` are the first lever, as today.
   - **If the fuzz suite fails on a new grammar, change the grammar, never the oracle**, and report it.
3. **The farmer's sale and the water order.**
   - **Apples:** `request_item` with `qty` at a farmer prompt whose host event's action is `sell` (1 coin an apple), with `farmer.cant_pay` when the coins are too few. V11's bread sale is the model.
   - **The order `water-for-plants`:** session 7, `deliverTo: farmer`, 1 water bottle, giving 3 apples through `farmer.delivered`. Its HUD text goes in the locale. Its plan (`voy a buscar agua`) comes before the delivery and its report (`fui al río`) after, as V12 ordered the axe order's three times. Test a learner who arrives already carrying the water.
   - **The coin path.** A learner who followed sessions 4 to 6 reaches session 7 with **0 coins**: +2, +4, −4 for the bread, −2 for the axe. Check this with the data and `--skip-to-session 7`. So a sale that needs coins cannot be the lesson's only route.
     - Design the lesson so that it can be finished: for example, `quiero tres manzanas rojas` asked as the order's reward, or the farmer pays coins for the water as well, or a step that does not need the purchase.
     - Keep the apple sale for a learner who has coins.
     - Report the final terms, which the orchestrator records as a deviation if they differ from 3.4.
4. **The F1 controls help** (left undone by V12c, because its lines are in `content/game-v1`): add a line about sleeping at the fire to start the next session, in its existing style.
5. **Checks:** about 8 items with `lesson: como-es`.
6. **Tests**, in the style of V10 to V12:
   - the lesson loads and validates at its level;
   - every prompt and phrase resolves;
   - the sale takes coins and gives apples, and with too few coins gives nothing (`cant_pay`);
   - the order takes the water and gives apples;
   - a learner who arrives with the water early still meets the plan step before the delivery;
   - the tense steps credit and miss as designed (`ayer me gusta el pan` is the core's `conj.tense`; `me gusta el pan` at the `pret` step is the lesson's Miss with `tenseRetry`);
   - `--skip-to-session 7` opens `como-es`, with the objective and the beacon on the farmer.

## The content loop (as V10 to V12 did it; D-game-10 and D-game-12)
- **Sheet rows first** (`content/PHRASES.md`, `game/phrases.json`, the locale glosses). Each said row has a graded learner-error example, and every new row is `llm-opus (provisional)`.
- **Try the core first**, on every canonical text and error example, before relying on a grade. Use the CLI (`dotnet run --project tools/Vocaterra.Nlp.Cli -- eval --transcript "<text>" --prompt <prompt> --content content/game-v1`) through a wrapper script under `/tmp/claude-1000/`, because the sandbox refuses the word `eval`. In particular (VILLAGE_PLAN section 5):
  - `me gustan las manzanas`: a postverbal plural subject, where `conj.person` must stay silent;
  - `quiero tres manzanas rojas` clean, while `rojos` (`gender.agree`) and a misplaced adjective (`word_order`) fire;
  - `la manzana es roja` clean, and `la manzana es rojo` `gender.agree`;
  - `me gustó el pan de Luz` clean;
  - `ayer me gusta el pan` `conj.tense`;
  - `fui al río` clean.

  A wrong grade is a core gap. Change the example or the row, do not patch the core, and report the exact sentence and what it gave; the orchestrator files core milestones.
- **An Opus pass** over every new Spanish string before any WAV: naturalness, a Latin American neutral register, words within the level, accents and `¿¡`. List the changes in the report.
- **Words:** the gate's lint (A2, fail-on-level) is clean, and the validator passes the lesson at its level. Add world words above A1 to the marked block of `cefr/a1.txt` only if the lesson is `a1`. Otherwise an `a2` lesson may use A2 words, and a word above A2 still fails.
- **WAVs:** `scripts/prerender-speech.sh` (through `scripts/limited.sh`) for every new line, at both speeds; the checklist rows for the owner.
- **Grammar:** refresh the strings file and the cache with the local LanguageTool server (`scripts/languagetool.sh start`, then `stop`), with `--prune` if the tool offers it there. Check that the cache rebuilt from nothing equals the committed one; CI's `languagetool` job deletes it and compares. The strict offline check passes.
- **Hash and suite:**
  - `scripts/content.sh refresh`, or validate, then hash through the wrapper, then validate again.
  - Then the whole suite.
  - Re-record the goldens the edit moved: `VOCATERRA_RECORD_GOLDEN=1`, with the identity baseline in its own commit.
  - Re-record the fuzz suite's `FuzzLanguageStreamTests` digests if the content moved them, with the generators unchanged; say so in the commit and the report.
  - Put the new hash into the save fixtures (`save-v1.json`, `save-v4.json`, and any other that pins it).
  - Never weaken a fuzz oracle.
- **With models:** the spoken round trip (as the `*SpokenTests` do) over the new said rows. `gusta`, `gustan`, `gustó`, `manzanas`, `rojas` and the colours must be heard. A word the recognizer cannot hear goes to the heard side, or to a typed-only variant; report it.
- **Timing:** the scripted lesson run (`--skip-to-session 7` with `--lesson-run como-es`, or as V12 did), then your estimate in minutes, counting reading, audio, turns and walking (target 5 to 10).
- **Scripted runs:** none of the 20 should change, apart from what the new lesson adds (for example, the Lessons page's line count in r16 and r20). Run `scripts/scripted-runs.sh` before and after and explain every difference.

## Resources (the desktop has crashed from overload)
- Run every `dotnet restore|build|test|format|run`, every `scripts/content.sh` call, every speech render and every screenshot run through `scripts/limited.sh`.
- Never start two heavy commands at once: no `&`, no background builds.
- While iterating, use `scripts/test.sh fast` or `scripts/test.sh project <test project>`. Run `scripts/test.sh full` once before reporting.
- Exit code 137 means the 6 GB cap killed the command: lower the parallelism, and never work around the cap.

## Environment
- Prefix every command inline with `PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1`.
- The sandbox refuses a command line that contains the words `eval` or `hash`. Use a wrapper script under `/tmp/claude-1000/` for those.
- Model-gated tests read `VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models`.
- Game runs need `DISPLAY=:0`.

## Rules
- **What a game milestone edits:**
  - the game projects (`src/Vocaterra.Game.Core`, `src/Vocaterra.Game`, `src/Vocaterra.Host`, `tools/Vocaterra.SpeechRender`) and their tests;
  - `content/game-v1`, `content/locale`, `content/schema`, `content/PHRASES.md`, `content/README.md`, `content/grammar` and `content/speech-index.json`;
  - `assets/` and `docs/game/`.
- It takes the core (`src/Vocaterra.Nlp*`, `tests/Vocaterra.Nlp.*`, `tests/golden`, `docs/nlp/`) as it is, except for re-recording goldens and digests a content edit moved.
- Engine code goes only in `src/Vocaterra.Game`. No `System.Random` and no clock in the evaluation path.
- Do not edit `docs/game/IMPLEMENTATION_PLAN.md`, `docs/game/VILLAGE_PLAN.md`, `docs/nlp/IMPLEMENTATION_PLAN.md` or either `DECISIONS.md`.
- Write code and content that read like the surrounding ones.

## Gate (from the worktree root, each through scripts/limited.sh)
- `dotnet build Vocaterra.sln -warnaserror -m:4`, with 0 warnings.
- `dotnet test Vocaterra.sln -m:2` all passing. It was 6087 passed and 3 skipped; report the new count.
- `dotnet format Vocaterra.sln --verify-no-changes`.
- `scripts/content.sh validate`.
- The content gate as V08 left it in `scripts/merge-gate.sh`: the lint at A2 with fail-on-level and `--host-pools 'guide.*'`, and the cefr-checks.
- The host's `--validate-content`.
- `scripts/content.sh grammar content/game-v1 --strings content/grammar/game-v1-strings.jsonl --strings-only --offline --strict`.
- **The host demos:**
  - `dotnet run --project src/Vocaterra.Host -- --transcript "quiero agua, por favor" --prompt stall-need` still prints `request_item`, communicated;
  - `--transcript "¿cómo te llamas?" --prompt smith-open` prints `ask_name`;
  - `--transcript "me gustan las manzanas" --prompt farmer-open` (or the prompt where the row lives) prints `state_like`, communicated.
- Do not commit `bin/`, `obj/` or `artifacts/`.

## Commit
Each message ends with:
```
Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDeR3htNRFCtjYM6mbq
```

## Report (under 500 words)
- The branch and the final commit SHA, pushed.
- What was built, by deliverable.
- The lesson's level, and why.
- The final said and heard rows (id and Spanish), and any dropped or added.
- The Opus pass's changes.
- The core tries and their results. A wrong grade is a core gap: give the exact sentence and what it gave.
- The coin path, and the order's and the sale's final terms.
- Words added to the CEFR lists.
- Recognizer results.
- The lesson's estimated minutes.
- The test count before and after.
- The new bundle hash; the goldens and digests re-recorded.
- The scripted-run differences.
- Bugs found and fixed.
- Anything left undone, and why.
