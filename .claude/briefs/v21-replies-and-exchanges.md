You are implementing the game milestone **V21, replies and exchanges**, of the Vocaterra repository (C#, .NET 10, a MonoGame first-person voxel game that teaches Spanish). The language core is the `Vocaterra.Nlp` engine and the Spanish pack `src/Vocaterra.Nlp.Es`; the game is `Vocaterra.{Game.Core,Game,Host,SpeechRender}`. **This milestone's work is in the game: `src/Vocaterra.Game.Core`, `src/Vocaterra.Host`, `content/game-v1` and their tests.** It gathers what the NPCs say back that is wrong or contradictory, from `docs/HANDOVER_VILLAGE_PHASE_B.md` section 9.3 E, the QA passes (`docs/qa-reports/2026-11-01-language.md` QA-language-9, `docs/qa-reports/2026-11-01-progression.md` QA-progression-3 and 4) and V20's leftovers (`docs/game/IMPLEMENTATION_PLAN.md` section 5, the V20 entry's "Left").

Do the work yourself; do not spawn subagents.

## Where to work, and pushing
Work only in the git worktree `/home/user/Dev/Vocaterra/.claude/worktrees/village` (the orchestrator's gate checkout, lent to you because it holds the full build and disk is short). cd there first and run `git checkout -b V21-replies-and-exchanges` from its current commit (`@@BASE@@`, the tip of `feature/village-followups`). Never edit `/home/user/Dev/Vocaterra` or any other worktree, and never commit to `feature/village-followups`. When you finish, **leave the worktree on your branch, clean**; the orchestrator switches it back.
- **The owner wants work pushed often.** Commit after each item that builds and passes `scripts/test.sh fast`, with the prefix `V21: `. Push after every commit: `git push -u origin V21-replies-and-exchanges` the first time, then `git push origin V21-replies-and-exchanges`.
- Never push `main` or `feature/village-followups`, and do not merge.
- **Other agents run at the same time** (the core's M70 in `.claude/worktrees/m65`); every heavy command goes through `scripts/limited.sh`, whose machine-wide lock makes yours wait for theirs.
- **Disk is short (about 8 GB free).** Delete capture folders you no longer need; keep scripted-run captures to the two you compare.

## The items (each with a test that fails before the fix; one commit each)
1. **The stall and the villager answer by item.** The stall answers any request with the bottle (older, S6), and since V20 the villager hands over bread for any request (`Quiero madera.`). Route both through V19's `ExchangeMiss` (`grep -rn ExchangeMiss src/Vocaterra.Game.Core`): their items go in `content/game-v1/game/host-events.json`, and any other item gets their `wont_sell` line (add a pool and a transition where none exists, as V15 added `wont_buy`). Make `shop_hay` («Hay agua y pan.») agree with what the stall sells. `quiero agua, por favor` at `stall-need` still gives the water.
2. **Exchange lines that contradict the deal.**
   - The farmer refuses apples with «No, gracias. Solo compro fruta.» (apples are fruit; he buys only berries): the line says what he does buy, or why not apples.
   - The baker says «Necesitas más monedas.» while the order that would pay is held back by the lesson (QA-progression-4: the order waits for the lesson's `n4-back` step, V14's design): give the real reason, for example that the lesson comes first, in a short A1 line.
   - `¿Cuánto es todo?` gets «Quince manzanas, quince monedas.» whatever the bag holds: the total follows what is asked or held (find how the line is chosen; a templated count if the dialogue allows it).
3. **One reply per turn** (QA-language-9): `buscé madera` at `lesson-will-fetch` gets «Claro, «buscaré madera». Muy bien.» and then «No te entendí. ¿Me lo dices otra vez?» in the same turn. A turn never gives a recast's praise and "No te entendí" together: decide which one the case calls for (a line that was not understood gets only the retry) and test it.
4. **Intent clashes V20 found** (content: the intents' keyword rules and grammars in `content/game-v1/intents/intents.json`):
   - `quién es grande`, `qué es grande`, `cómo es grande`, `cual es grande` read as `describe` at León's (`farmer-open`) and the stall's prompts: a question word is never `describe`'s subject.
   - `como es la manzana` reads as `state_action` through the `comer` keyword rule, and `ellas adónde van` through the `ir` rule: a question (`cómo`/`como` before `es`, `adónde`/`dónde`) is not a statement. Find the right intent each should get, or none ("No te entendí" is better than a wrong reading).
   - Run the lint's confusion check after each change; requests and lesson rows that resolved before must resolve the same (V20's `TaughtInFreeTalkTests` must stay green).

## The content loop
- **The Opus pass over new Spanish is the orchestrator's** (D-game-12): keep every new string short, A1, natural, Latin American neutral, with accents and `¿¡`, and list each new line with its pool id in the report. If the orchestrator changes a line, it is re-rendered then (a small follow-up), so render as usual.
- **Try the core first** on any new line a learner might say: `scripts/try-lines.sh` and `--host` (the game's path), with your lines in a scratch file. A wrong grade is a core gap: report it, do not patch the core.
- **After each round of content edits** run `scripts/content-loop.sh --grammar --speech` directly. **Never run it, or `scripts/test.sh`, under `scripts/limited.sh`.** It refreshes, checks the grammar cache (with CI's rebuilt-from-nothing check), renders the new lines' WAVs in the NPCs' existing voices, puts the new `bundleHash` in the save fixtures, builds, checks the goldens and runs the fast tier. Read every grammar finding: fix the content or add a waiver with its reason. Add `--record` when the goldens or the identity baseline move (a commit of their own, every moved entry listed). Once with `--tier full` before the report.
- After it finishes, check that no `content-loop.sh` or wait loop of yours is still running (`pgrep -fa content-loop`).

## Proof
- **The host:** each item's lines at their prompts, before and after (`scripts/try-lines.sh --host` with a scratch file; the host's `--bag` for the exchanges, V23a).
- **The game:** `--skip-to-session 10` at the stall, the villager and the farmer with the item lines; `--skip-to-session 5` at the baker with the order held; look at the screenshots and the stdout.
- **The scripted runs:** `scripts/scripted-runs.sh` before your first edit and after, compared with `python3 -I scripts/compare-runs.py`. Explain every difference.
- **The host demos still hold:** `--transcript "quiero agua, por favor" --prompt stall-need` gives `request_item`, communicated; `--transcript "hola, quiero dos panes" --prompt baker-open` gives the sale; `--transcript "vendo madera" --prompt smith-open` gives `offer_item`; `--transcript "mañana compraré pan" --prompt baker-open` gives `state_action`.

## Read first (the sections named, not whole files)
- `CLAUDE.md`; `docs/code_guidelines.md`; `content/README.md` ("The edit loop"); the header of `scripts/content-loop.sh` (lines 1 to 45).
- `docs/HANDOVER_VILLAGE_PHASE_B.md` section 9.3 E (`grep -n '^\*\*E\. '`, about 20 lines).
- The QA reports' items named above; `docs/game/IMPLEMENTATION_PLAN.md` section 5, the V19 and V20 entries (grep `^- \*\*V19\|^- \*\*V20`).
- The code: `src/Vocaterra.Game.Core/Npc/NpcService.cs`, `NpcHostEvents.cs`, the exchange miss, and their tests under `tests/Vocaterra.Game.Core.Tests/Npc/`; `content/game-v1/game/host-events.json`, the agents of the stall, villager, farmer and baker (`content/game-v1/agents/`).
- Never read `docs/game/SPEECH_CHECKLIST.md`, `content/game-v1/manifest.json` or the whole of `content/PHRASES.md`; grep their rows.

## Rules
- **What a game milestone edits:** the game projects and their tests; `content/game-v1`, `content/locale`, `content/schema`, `content/PHRASES.md`, `content/README.md`, `content/grammar` and `content/speech-index.json`; `assets/` and `docs/game/`. It takes the core as it is, except for re-recording goldens and the identity baseline that a content edit moved.
- Engine code only in `src/Vocaterra.Game`; `Game.Core` uses no `System.Random`, no `Guid` and no clock.
- A save must still load an older save (X01).
- Do not edit `scripts/`, `docs/game/IMPLEMENTATION_PLAN.md`, `docs/game/VILLAGE_PLAN.md`, `docs/nlp/`, `docs/qa-reports/` or either `DECISIONS.md`. Never create or edit anything under `.github/workflows/`.
- `content/PHRASES.md` is the source of truth for phrases (D-game-10).

## Output, context and resources
- Read the sections named, not whole files; prefer `grep -n`, and keep `sed -n` ranges under about 150 lines. Heavy output to log files under `/tmp/claude-1000/v21/`, at most 30 lines printed.
- Run every `dotnet restore|build|test|format|run`, every `scripts/content.sh` call, every speech render and every game or screenshot run through `scripts/limited.sh` (builds `-m:4`, tests `-m:2`); `scripts/test.sh` and `scripts/content-loop.sh` run directly. Never start two heavy commands at once: no `&`, no background builds. Exit code 137 means the 6 GB cap killed the command: lower the parallelism.
- Prefix every command inline with `PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1`. The sandbox refuses a command line containing `eval` or `hash`. Game runs need `DISPLAY=:0` and `VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models`; give each its own `--session-dir` under `/tmp/claude-1000/v21/`.

## Gate (yours, before reporting)
- `dotnet build Vocaterra.sln -warnaserror -m:4`, 0 warnings; the full suite passing (7682 passed and 3 skipped before, with models; the loop's `--tier full`); report the new count.
- `dotnet format Vocaterra.sln --verify-no-changes`; `scripts/content.sh validate`; the content gate (the lint at A2 with fail-on-level and `--host-pools 'guide.*'`, the cefr-checks, the host's `--validate-content`); the offline grammar replay.
- Do not commit `bin/`, `obj/` or `artifacts/`.

## Commit
Each message ends with:
```
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDfGVmxEaTFXNb87eD1
```

## Report (one page, under 450 words)
- The branch and the final commit SHA, pushed; the worktree left on your branch, clean.
- For 1 to 4: what was built, the test, the host or game result before and after.
- Every new Spanish line, and the WAVs rendered.
- The scripted runs' differences, each explained.
- The goldens and identity entries that moved; the test count before and after.
- Core gaps found (the exact line and what it gave); anything left undone, and why.

Before your final message, make sure no process you started is still running (`pgrep -fa 'dotnet|Vocaterra|content-loop'`, kill only your own).
