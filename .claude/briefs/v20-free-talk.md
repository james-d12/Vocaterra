You are implementing the game milestone **V20, free talk: the NPCs understand what they teach**, of the Vocaterra repository (C#, .NET 10, a MonoGame first-person voxel game that teaches Spanish). The language core is the `Vocaterra.Nlp` engine and the Spanish pack `src/Vocaterra.Nlp.Es`; the game is `Vocaterra.{Game.Core,Game,Host,SpeechRender}`. **This milestone's work is in the game: `src/Vocaterra.Game.Core`, `content/game-v1` and its tests.** It is the main game follow-up after PR #20's merge (`docs/HANDOVER_VILLAGE_PHASE_B.md` section 9.3 D; read it, and section 5 item 4, which lists the sentences).

Do the work yourself; do not spawn subagents.

## Where to work, and pushing
Work only in the git worktree `/home/user/Dev/Vocaterra/.claude/worktrees/village` (the orchestrator's gate checkout, lent to you because it holds the full build and disk is short). cd there first and run `git checkout -b V20-free-talk` from its current commit (`@@BASE@@`, the tip of `feature/village-followups`). Never edit `/home/user/Dev/Vocaterra` or any other worktree, and never commit to `feature/village-followups`. When you finish, **leave the worktree on your branch, clean**; the orchestrator switches it back.
- **The owner wants work pushed often.** Commit after each coherent step that builds and passes `scripts/test.sh fast`, with the prefix `V20: `. Push after every commit: `git push -u origin V20-free-talk` the first time, then `git push origin V20-free-talk`.
- Never push `main` or `feature/village-followups`, and do not merge, except the one merge of `origin/feature/village-followups` described under "The core's M67".
- **Other agents run at the same time** (the core's M67 in `.claude/worktrees/m65`); every heavy command goes through `scripts/limited.sh`, whose machine-wide lock makes yours wait for theirs.
- **Disk is short (about 8.5 GB free).** Delete capture folders you no longer need; keep scripted-run captures to the two you compare.

## The core you build on
The core's M66 is merged: `¿puede usted vender…?` is no longer flagged; `puedo pago`, `¿cuánto cuestan todo?`, `las manzanas cuesta` are; `ti` is a word. **The core's M67 is running in parallel:** it removes the `vocab.wrong` (correctness 0.4) that an understood line gets at a constrained prompt, and adds `conj.tense`/`conj.person` when a lesson answer differs from its answer only in a verb's form. Until it lands, understood lines at open prompts may still show that 0.4; judge your work by the intent and by `communicated`, not by the 0.4.

## The milestone (handover 9.3 D, with QA-language-8)
### (a) Each NPC understands what it teaches
In free talk, 9 of phase B's 27 practised sentences get "No te entendí" from the NPC who taught them (handover section 5, item 4, lists them): the villager's, the fisher's and the stall's open prompts take no statements, and Domingo's takes no `tener que`.
- Each NPC's open prompt (`content/game-v1/prompts/prompts.json`, its `allowedIntentIds`; and the answer set or grammars it needs) accepts the intents of the lessons it teaches (`state_action`, `describe`, `state_have`, `state_like`, and `tener que`'s intent; find them in `content/game-v1/intents/intents.json` and the lessons).
- The NPC answers with a short line from its pools that keeps the talk going (a recast, «¡Qué bien!»), through the agent's transitions (`content/game-v1/agents/<npc>.json`); add a pool and a transition where none fits, the way V15 added `wont_buy` (`grep -rn wont_buy content/game-v1/agents content/game-v1/dialogue`).
- **A game test that runs every said row of every lesson through its NPC's open prompt**, so this cannot regress: all 27 of phase B's practised sentences, and phase A's, are understood (intent resolved, communicated) by the NPC who taught them.
- Watch the lint's confusion check: a statement intent added to an open prompt may now compete with the prompt's requests (`buscaré madera` read as `ask_directions` at `fisher-open` was V17's finding). Requests that worked before must still resolve to their old intents (the scripted runs and the host demos prove it).

### (b) Practice is credited
Free talk credits only the rows in an NPC's `focus`. Credit any phrase row of a lesson the learner has started or finished, at any NPC (the book's word records already count them).

### (c) Example cards after their lesson
An NPC shows its focus cards from the first talk (Luz shows `Vendo madera.`, a session 10 row, in session 5; León shows his A2 lines). A card shows only once its row's lesson is the current one or done.

### (d) Names in recasts
Domingo's recast writes `luz` in lower case («buscaré madera para luz»). When the line is shown (the game's text, not the core), capitalise an NPC's name in a recast unless a determiner comes before it (`la luz` stays). Names come from the roster (`content/game-v1/game/npcs/*.json`), not a list in code.

### (e) `¿cuánto son todo?` and `cuanto son todo?`
`¿cuánto son todo?` is understood with intent `unknown`, and `cuanto son todo?` (no accent) is read as `describe` and praised (QA-language-8, `docs/qa-reports/2026-11-01-language.md`). `ask_total`'s grammar takes `son` (and the core, since M66, then grades `son` as `es`), and a question word, accented or not, never reads as `describe`. Check `¿cuánto es todo?` and the market's other questions are unchanged.

### (f) `quiero cero panes`
Keep the credit (the Spanish was right and practised the pattern). Say so in `docs/game/README.md`, in one line.

## The content loop
- **An Opus pass** over every new Spanish string before any WAV: naturalness, a Latin American neutral register, words within the lesson's level (A1 or A2), accents and `¿¡`. List the changes in the report. The orchestrator reads the new lines in the diff too.
- **Try the core first** on any new line a learner might say: `scripts/try-lines.sh` and `--host` (the game's path), with your lines in a scratch file. A wrong grade is a core gap: report it, do not patch the core.
- **After each round of content edits** run `scripts/content-loop.sh --grammar --speech` directly. **Never run it, or `scripts/test.sh`, under `scripts/limited.sh`:** both call it themselves, and a nested call waits for ever on the lock. It refreshes, checks the grammar cache (with CI's rebuilt-from-nothing check), renders the new lines' WAVs in the NPCs' existing provisional voices, puts the new `bundleHash` in the save fixtures, builds, checks the goldens and runs the fast tier; it stops at the first failure with the next thing to do. Read every grammar finding it prints: fix the content, or add a waiver with its reason. Add `--record` when the goldens move (a commit of their own, every moved entry listed in the report). Once with `--tier full` before the report.
- After it finishes, check that no `content-loop.sh` or wait loop of yours is still running (`pgrep -fa content-loop`).

## The core's M67 (merge it in before your final checks)
When `git fetch origin && git log --oneline origin/feature/village-followups` shows `Merge M67`, merge `origin/feature/village-followups` into your branch (`git merge --no-ff origin/feature/village-followups`), resolve any conflict in the goldens or the identity baseline by re-recording them (`scripts/content-loop.sh --grammar --speech --record`), and rerun your tests. If M67 has not landed when you are otherwise done, say so in the report and stop; the orchestrator will tell you when to merge.

## Proof
- **V09's measure again:** all 27 practised sentences understood by the NPC who taught them (the game test of (a)), and a table in the report: sentence, NPC, intent, the NPC's reply.
- **The learner's route through sessions 7 to 10** (`--skip-to-session n`): sleep into the session, the Lessons page's `next:`, the objective and the beacon, the lesson, then free talk once with each NPC of the lesson (`--talk <npc> --say "…"`) with a lesson sentence. Look at the screenshots.
- **The scripted runs:** `scripts/scripted-runs.sh` before your first edit and after, compared with `python3 -I scripts/compare-runs.py`. Explain every difference.
- **The host demos still hold:** `--transcript "quiero agua, por favor" --prompt stall-need` gives `request_item`, communicated; `--transcript "hola, quiero dos panes" --prompt baker-open` gives the sale; `--transcript "vendo madera" --prompt smith-open` gives `offer_item`; `--transcript "mañana compraré pan" --prompt baker-open` gives `state_action`.
- **CI's case:** the full suite once without models (`VOCATERRA_MODELS_DIR=<an empty folder under /tmp/claude-1000/v20/> scripts/test.sh full`); report any wall-clock test within 4 times of its budget.

## Read first (the sections named, not whole files: every line you read stays in your context)
- `CLAUDE.md`; `docs/code_guidelines.md`; `content/README.md` ("The edit loop") and the header of `scripts/content-loop.sh` (lines 1 to 45).
- `docs/HANDOVER_VILLAGE_PHASE_B.md`: section 9.3 D (`grep -n '^\*\*D\. '`, about 35 lines) and section 5 item 4 (lines about 268 to 290).
- `docs/qa-reports/2026-11-01-language.md`: QA-language-8 and the 9.3 D observations.
- `docs/game/VILLAGE_PLAN.md`: section 3.5 (the lesson arc, by `sed -n` ranges) and section 3.6 (intents and prompts).
- `docs/game/IMPLEMENTATION_PLAN.md` section 5: the V09-again entry (grep `V09 again`) and the V19 entry.
- The code: `src/Vocaterra.Game.Core/Npc/NpcService.cs` (free talk, focus, credit), the phrase credit (`grep -rn focus src/Vocaterra.Game.Core --include=*.cs`), the recast text, and their tests under `tests/Vocaterra.Game.Core.Tests/Npc/`.
- Never read `docs/game/SPEECH_CHECKLIST.md`, `content/game-v1/manifest.json` or the whole of `content/PHRASES.md`; grep their rows.

## Rules
- **What a game milestone edits:** the game projects (`src/Vocaterra.Game.Core`, `src/Vocaterra.Game`, `src/Vocaterra.Host`, `tools/Vocaterra.SpeechRender`) and their tests; `content/game-v1`, `content/locale`, `content/schema`, `content/PHRASES.md`, `content/README.md`, `content/grammar` and `content/speech-index.json`; `assets/` and `docs/game/`. It takes the core as it is, except for re-recording goldens and the identity baseline that a content edit moved.
- Engine code only in `src/Vocaterra.Game`; `Game.Core` uses no `System.Random`, no `Guid` and no clock (`ArchitectureRulesTests`).
- A save must still load an older save (X01); a new field gets a default.
- Do not edit `scripts/`, `docs/game/IMPLEMENTATION_PLAN.md`, `docs/game/VILLAGE_PLAN.md`, `docs/nlp/`, `docs/qa-reports/` or either `DECISIONS.md`. Never create or edit anything under `.github/workflows/`.
- `content/PHRASES.md` is the source of truth for phrases (D-game-10): a new NPC line that is a phrase gets its row first.
- Write code and content that read like the surrounding ones.

## Output and context (the cost of a milestone is its turns times its context)
- Read the sections named, not whole files; prefer `grep -n`, and keep `sed -n` ranges under about 150 lines.
- Send every heavy command's output to a log file under `/tmp/claude-1000/v20/` and print at most 30 lines of it.
- Wait for a long command by running it in the foreground, not by polling a log.

## Resources (the desktop has crashed from overload)
- Run every `dotnet restore|build|test|format|run`, every `scripts/content.sh` call, every speech render and every game or screenshot run through `scripts/limited.sh` (builds `-m:4`, tests `-m:2`); `scripts/test.sh` and `scripts/content-loop.sh` run directly (they call it themselves).
- Never start two heavy commands at once: no `&`, no background builds.
- While iterating, use `scripts/test.sh fast` or `scripts/test.sh project <test project>`. Run `scripts/test.sh full` once before reporting (or the loop's `--tier full`).
- Exit code 137 means the 6 GB cap killed the command: lower the parallelism, and never work around the cap.

## Environment
- Prefix every command inline with `PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1`.
- The sandbox refuses a command line that contains the words `eval` or `hash`; `scripts/try-lines.sh` and `scripts/content-loop.sh` hold them inside.
- Model-gated tests read `VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models`. Game runs need `DISPLAY=:0`; give each its own `--session-dir` under `/tmp/claude-1000/v20/`.

## Gate (yours, before reporting)
- `dotnet build Vocaterra.sln -warnaserror -m:4`, 0 warnings; the full suite passing (7603 passed and 3 skipped before, with models); report the new count, with and without models.
- `dotnet format Vocaterra.sln --verify-no-changes`; `scripts/content.sh validate`; the content gate (`scripts/content.sh lint content/game-v1 --strict --level a2 --fail-on-level --host-pools 'guide.*'`, the cefr-checks, the host's `--validate-content`); the offline grammar replay.
- `scripts/try-lines.sh --check` gives every baseline (your milestone changes no core grade).
- Do not commit `bin/`, `obj/` or `artifacts/`.

## Commit
Each message ends with:
```
Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDfGVmxEaTFXNb87eD1
```

## Report (one page, under 500 words; the details belong in the commits)
- The branch and the final commit SHA, pushed; the worktree left on your branch, clean; whether M67 was merged in.
- For (a) to (f): what was built, the test, the result before and after (the 27 sentences' table for (a)).
- The new Spanish lines, the Opus pass's changes, and the WAVs rendered.
- The scripted runs' differences, each explained; the learner's route.
- The goldens and identity entries that moved.
- The test count before and after, with and without models; any timed test within 4 times of its budget.
- Core gaps found (the exact line and what it gave); anything left undone, and why.

Before your final message, make sure no process you started is still running (`pgrep -fa 'dotnet|Vocaterra|content-loop'`, kill only your own).
