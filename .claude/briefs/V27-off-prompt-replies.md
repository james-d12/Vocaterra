You are implementing the game milestone **V27, the off-prompt lines in the game**, of the Vocaterra repository (C#, .NET 10, a MonoGame first-person voxel game that teaches Spanish). The language core is the `Vocaterra.Nlp` engine and the Spanish pack `src/Vocaterra.Nlp.Es`; the game is `Vocaterra.{Game.Core,Game,Host,SpeechRender}`. **This milestone's work is in the game** (`src/Vocaterra.Game.Core`, `src/Vocaterra.Host`, their tests, and `content/game-v1`). The owner chose this design on 2026-11-02 (D-game-22, "Owner's answers, 2026-11-02 (the off-prompt lines …)"); the core's M72 has built its half.

Do the work yourself; do not spawn subagents.

## Where to work, and pushing
Work only in the git worktree `/home/user/Dev/Vocaterra/.claude/worktrees/village`: cd there first, and never edit `/home/user/Dev/Vocaterra` or any other worktree. Create your branch with `git checkout -b V27-off-prompt-replies @@TIP@@` (the tip of `feature/village-followups`, with M72). Do not switch branches once you have started.
- **The owner wants work pushed often.** Commit after each item that builds and passes its tests, with the prefix `V27: `. Push after every commit: `git push -u origin V27-off-prompt-replies` the first time, then `git push origin V27-off-prompt-replies`.
- Never push `main` or `feature/village-followups`, and do not merge. When you finish, leave the worktree on your branch, clean.

## What the core gives you (M72)
At a prompt that lists allowed intents, a line none of them resolves may still be one the intent catalog understands. The core then sets `Verdict.OffPromptIntent` (`IntentId?`; also `IntentResolution.OffPromptIntent`): the intent it has over the whole catalog, by a slot grammar or a keyword rule. It is set only when `Intent` is unknown and no answer matched, so the line is never communicated, and it changes nothing else in the core: the core's repair ladder still counts it as `not_communicated`. Since M72 such a line also has no derived `vocab.wrong` (it was 0.4 with an empty expected word); a near miss of an answer still has it. Read `docs/nlp/API.md`, "A line the prompt does not take: `Verdict.OffPromptIntent` (M72)" (`grep -n 'OffPromptIntent' docs/nlp/API.md`), with its table: `me gusta el río` at `smith-open` gives `state_like`, `tengo dos manzanas` at `fisher-open` `state_have`, `el río es grande` at `baker-open` `describe`, `la tienda está en el pueblo` at `farmer-open` nothing. Today the game answers all of these «No te entendí. ¿Me lo dices otra vez?».

## The items
1. **The reply.** In free talk (no lesson step: `scene.PromptOverride is null` in `NpcService`), a line with `OffPromptIntent` set gets one reply, the NPC's own off-prompt line, and nothing else: no «No te entendí», no repair line of any kind (not «¿Cómo?», not the slow repeat, the choices or the give-up), no correction. One pool per NPC in `content/game-v1/dialogue/dialogue.json` beside its `*_nice` pools, one line each, chosen through the NPC's data the way V20's and V21's pools are (`NpcTurnRules.Compose`, `NpcEntry.FreeTalkPools`, `content/game-v1/game/` by `grep -n freeTalkPools`). The lines (the owner's, verbatim):
   - Domingo (smith) «Te entiendo. Yo hago hachas.»
   - Luz (baker) «Te entiendo. ¿Quieres pan?»
   - León (farmer) «Te entiendo. ¿Quieres manzanas?»
   - Mar (fisher) «Te entiendo. ¿Buscas un lugar?»
   - the villager «Te entiendo. ¿Qué buscas?»
   - the guide, Luna, «Te entiendo. ¿Qué quieres aprender?» and the shopkeeper «Te entiendo. ¿Quieres comprar algo?» (the orchestrator's, in the owner's form: «Te entiendo.» and the NPC's topic).
   Each gets a normal and a slow WAV in its NPC's voice (14 WAVs) through the content loop. Keep the text exactly; if the lint or `cefr-check` refuses a word, stop that line and report it rather than rewording it.
2. **The ladder does not climb.** An off-prompt line is not a failed attempt. After it, the NPC's repeat counts are put back as they were before the turn (`utterance.RepeatCountsByTag`; see how `NpcService` restores the learner for an uncertain line, about line 680), so the next line the NPC does not understand gets the first step again, not the slow repeat. The learner model is not marked down for it either. Test: two off-prompt lines then a line not understood gives «No te entendí…» with no slow repeat or choices.
3. **The book credits it** as free talk does since V20 (b): a use of its nouns and of any phrase of a lesson begun or done (`LineReading.PhraseSaid`, `PhraseSkillsOf`, `WordSkillsOf`; today they run only when `verdict.Communicated`). No NPC action: no item given, no order, no host event. Read V20's entry first (`grep -n '^- \*\*V20' docs/game/IMPLEMENTATION_PLAN.md`, then about 25 lines) and V26's (`grep -n '^- \*\*V26'`), whose rule for a refused request (the item must match the phrase row's noun) still holds. Test: `me gusta el río` at Domingo after its lesson is begun counts as a use of its phrase, and `río` as a noun used.
4. **Lesson steps are unchanged.** At a lesson step (`scene.PromptOverride` set) a line the step does not take keeps today's reply and repair, and its counts climb as today. Test it.
5. **The host shows it.** `Vocaterra.Host --transcript "<line>" --prompt <id>` shares `NpcTurnRules` (`src/Vocaterra.Host/ConversationSession.cs`, about line 155): it prints the NPC's off-prompt line as its `agent:` line, and one `off-prompt: <intent>` line (a diagnostic, like `repair:`). Re-record the try-lines host baselines this moves and say which rows moved.
6. **The spoken path.** A spoken line the recognizer was unsure of keeps the uncertain reply («No te entendí bien…», `UncertaintyRule`) even when the core found an off-prompt intent; say what you chose if a case does not fit. Spoken off-topic lines at constrained prompts are decoded against the answer set only (a separate owner decision): out of scope.
7. **Two test names.** M72 changed the expected values of `LearnerRecordTests.AnErrorSpanMarksAWordUsedButNotCorrect` and `SpeechUxTests.ATypedLineTheCoreDidNotUnderstand…`, so their names no longer say what they assert. Rename them, or give them a line that still has the old case, whichever is truer.
8. **The README.** `docs/game/README.md`, "Open points after the follow-ups" (`grep -n 'Open points after the follow-ups'`): drop the points V27 closes (the «No te entendí» for a correct off-prompt line), and add any it leaves. Update the counts it states for lines and speech files. Do not edit the plans or the decisions.

## Read first (the sections named, not whole files)
- `CLAUDE.md`; `docs/code_guidelines.md`; `docs/game/ARCHITECTURE.md` (by `grep -n`); `content/README.md` (how to edit content).
- V20's, V21's and V26's entries in `docs/game/IMPLEMENTATION_PLAN.md` section 5 (`grep -n '^- \*\*V2[016]'`, about 25 lines each).
- `src/Vocaterra.Game.Core/Npc/NpcService.cs` about lines 600 to 740 (the turn), `src/Vocaterra.Game.Core/Npc/NpcTurnRules.cs`.
- Never read `docs/game/SPEECH_CHECKLIST.md`, `manifest.json` or the whole of `content/PHRASES.md` (grep the rows you need).

## The content loop (V18)
After every content edit run `scripts/content-loop.sh --grammar --speech` directly (never under `scripts/limited.sh`: it refuses, exit 3). It refreshes the hash, the fixtures, the LanguageTool cache and the WAVs. Add `--record` when the goldens move, and run it once with `--tier full` before the report (that is your full-suite run). After a grammar refresh, check the cache rebuilt from nothing equals the committed one: `scripts/languagetool.sh start`; `rm content/grammar/game-v1-cache.json`; `scripts/content.sh grammar content/game-v1 --strings content/grammar/game-v1-strings.jsonl --strings-only --strict`; `git diff --exit-code content/grammar/`; stop the server.

## Output and context (the cost of a milestone is its turns times its context)
- Send every heavy command's output to a log file under `/tmp/claude-1000/v27/`, and print at most 30 lines of it.
- Keep `sed -n` ranges under about 150 lines; prefer `grep -n` to find the place first.
- Wait for a long command by running it in the foreground, not by polling a log. **Never leave a wait loop or a background process behind** (a `while pgrep -f …` loop matches its own command line and never ends).
- **Disk:** about 12 GB free. While iterating, build only the projects you need; build the whole solution once at the end.

## Resources (the desktop has crashed from overload)
- Run every `dotnet restore|build|test|format|run`, every `scripts/content.sh` call and every game run through `scripts/limited.sh` (`scripts/limited.sh dotnet build <project> -m:4`; tests with `-m:2`). `scripts/test.sh`, `scripts/try-lines.sh` and `scripts/content-loop.sh` run directly and are **never** wrapped in `scripts/limited.sh`.
- Never start two heavy commands at once: no `&`, no background builds.
- While iterating use `scripts/test.sh fast` or `scripts/test.sh project <test project>`.
- Exit code 137 means the 6 GB cap killed the command: lower the parallelism, and never work around the cap.

## Environment
- Prefix every command inline with `PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1`.
- The sandbox refuses a command line that contains the words `eval` or `hash`. Use a wrapper script under `/tmp/claude-1000/v27/` for those.
- Model-gated tests and game runs read `VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models`. Game runs need `DISPLAY=:0`; give each its own `--session-dir` under `/tmp/claude-1000/v27/`, and scripted steps need `--screenshot`. The host: `dotnet src/Vocaterra.Host/bin/Debug/net10.0/Vocaterra.Host.dll --transcript "<line>" --prompt <id>`.

## Rules
- **What this milestone edits:** the game's code and tests, `content/game-v1` (the seven pools, the NPCs' data, the WAVs and what the content loop refreshes), `docs/game/README.md`, and the try-lines baselines it moves.
- It takes the core (`src/Vocaterra.Nlp*`, `tests/Vocaterra.Nlp.*`, `tests/golden`, `docs/nlp/`) as it is. A core gap is reported, never patched; a core test that fails after a content edit is wanted feedback: report it.
- Engine code only in `src/Vocaterra.Game`; `Vocaterra.Game.Core` references no package (`ArchitectureRulesTests`).
- Do not edit either `IMPLEMENTATION_PLAN.md`, `docs/game/VILLAGE_PLAN.md`, either `DECISIONS.md`, `docs/HANDOVER_VILLAGE_PHASE_B.md`, `scripts/merge-gate.sh`, or anything under `.claude/` or `.github/workflows/`.
- No `System.Random` and no clock in the evaluation path.

## Behaviour checks (before the report; show the lines)
- The game: `--skip-to-session 10 --talk smith --say "me gusta el río"` gives «Te entiendo. Yo hago hachas.» alone; `--talk fisher --say "tengo dos manzanas"` gives Mar's line; `--talk farmer --say "la tienda está en el pueblo"` still gives «No te entendí…» (no off-prompt intent); one lesson step with an off-prompt line unchanged. The session summary credits what item 3 says. Look at one screenshot.
- The host, each of the API table's lines at its prompt, before and after.
- `scripts/try-lines.sh --check` and `--host --check`: list the rows that moved and why.
- **The scripted runs:** `scripts/scripted-runs.sh` before your first edit and after, compared with `python3 -I scripts/compare-runs.py`; run them one at a time through `scripts/limited.sh`. Explain every difference.
- **The learner's route:** sleep into session 10, then free talk once with each NPC with one line it does not take.
- **CI's case:** the full suite once without models (`VOCATERRA_MODELS_DIR` an empty folder); report any wall-clock test within four times of its budget.

## Gate (yours, before reporting)
- `scripts/limited.sh dotnet build Vocaterra.sln -warnaserror -m:4`, 0 warnings.
- `scripts/content-loop.sh --grammar --speech --tier full`, all passing. It was 8199 passed and 3 skipped after M72; report the new count.
- `scripts/limited.sh dotnet format Vocaterra.sln --verify-no-changes`.
- `scripts/limited.sh scripts/content.sh validate`; the lint at A2 clean (`scripts/content.sh lint content/game-v1 --strict --level a2 --fail-on-level --host-pools 'guide.*'`); `scripts/try-lines.sh --check` clean (after any re-record you justify).
- Do not commit `bin/`, `obj/` or `artifacts/`. When you finish, `git status` is clean and the branch is pushed.

## Commit
Each message ends with:
```
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDfGVmxEaTFXNb87eD1
```

## Report (one page, under 450 words)
- The branch and the final commit SHA, pushed.
- Each item: what you changed (file and member), the test; the seven pool ids and their lines; the new bundle hash.
- The behaviour checks' lines; the scripted runs' differences; the try-lines rows re-recorded.
- The test count before and after; CI's case; anything left undone, and why.

Before your final message, make sure no process you started is still running (`pgrep -fa 'dotnet|Vocaterra|content-loop|scripted-runs'`, kill only your own).
