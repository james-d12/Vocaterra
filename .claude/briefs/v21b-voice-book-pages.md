You are implementing the game milestone **V21b, voice, book and pages**, of the Vocaterra repository (C#, .NET 10, a MonoGame first-person voxel game that teaches Spanish). The language core is the `Vocaterra.Nlp` engine and the Spanish pack `src/Vocaterra.Nlp.Es`; the game is `Vocaterra.{Game.Core,Game,Host,SpeechRender}`. **This milestone's work is in the game: `src/Vocaterra.Game.Core`, `src/Vocaterra.Game`, `src/Vocaterra.Host`, `tools/Vocaterra.SpeechRender`, `content/` and their tests.** It gathers the follow-ups of `docs/HANDOVER_VILLAGE_PHASE_B.md` section 9.3 E (its last three bullets) and G (two bullets), the display of the core's M70 accent note, and the orchestrator's Spanish pass over V21's lines (the V21b row of `docs/game/VILLAGE_PLAN.md` section 4).

Do the work yourself; do not spawn subagents.

## Where to work, and pushing
Work only in the git worktree `/home/user/Dev/Vocaterra/.claude/worktrees/village` (the orchestrator's gate checkout, lent to you because it holds the full build and disk is short). cd there first and run `git checkout -b V21b-voice-book-pages @@BASE@@` (the tip of `feature/village-followups`). Never edit `/home/user/Dev/Vocaterra` or any other worktree, and never commit to `feature/village-followups`. When you finish, **leave the worktree on your branch, clean**; the orchestrator switches it back.
- **The owner wants work pushed often.** Commit after each item that builds and passes `scripts/test.sh fast`, with the prefix `V21b: `. Push after every commit: `git push -u origin V21b-voice-book-pages` the first time, then `git push origin V21b-voice-book-pages`.
- Never push `main` or `feature/village-followups`, and do not merge.
- **Other agents may run at the same time** (the core's M69 in `.claude/worktrees/m65`); every heavy command goes through `scripts/limited.sh`, whose machine-wide lock makes yours wait for theirs.
- **Disk is short (about 8 GB free).** Delete capture folders you no longer need; keep scripted-run captures to the two you compare.

## The items (each with a test that fails before the fix; one commit each)
1. **The Spanish pass over V21's lines (the orchestrator's, D-game-12).** V21 made the stall refuse what it does have: it gives the water bottle, then offers bread (`shop_offer`, `stall.give_bread`), and in the lesson says «Sí, hay pan. Es pan de Luz.», yet now answers `quiero pan` with «Solo tengo agua.» (`shop_wont_sell.1`) and `¿hay pan?` with «Hay agua.» (`shop_hay.1`).
   - `shop_hay.1` goes back to «Hay agua y pan.» (it was true).
   - `shop_wont_sell.1` becomes «No tengo eso. Tengo agua y pan.» and is said only for an item the stall has neither of (wood, apples, …).
   - A request for `pan` at the stall never gets a refusal: give the bread (`stall.give_bread`, `shop_give_bread`) or a true line, your choice, with a test; session 1's route (ask for water, get the bottle, the offer of bread, `sí` or `no`) is unchanged, and the scripted runs show it.
   - León's free-talk answer to `¿Cuánto es todo?` (`farmer_total`, «Quince manzanas, quince monedas.», whatever the bag holds): outside the lesson, answer with the unit price, «Una manzana, una moneda.» (check the price in `content/game-v1/game/` first), or a line that follows what the learner holds; the lesson row `quince-monedas` keeps its line.
   - Keep `villager_wont_sell.1` «Solo tengo pan.», `farmer_wont_buy.1` «No compro eso. Compro fruta del bosque.» and `baker_wait.1` «Primero, la lección. Luego, las monedas.» as they are.
2. **The shared feedback lines in each NPC's voice** (9.3 E; the open point "the NPCs' «¡Muy bien!» plays in Luna's voice"). Render the shared feedback pools (`feedback.*` and the other lines every NPC says through the guide's pools; find them in `content/speech-index.json` and `tools/Vocaterra.SpeechRender`) once per NPC voice, with the index keyed by speaker; the game plays the speaking NPC's file and falls back to the guide's. **Count first:** the lines × the NPCs × 2 speeds, and the disk cost from the current files' sizes. If it is under 100 MB, render and report the numbers; if over, render none, build the code path with a test, and report the count for the owner.
3. **`tener que` in the book** (9.3 E): an entry where the book lists `ir a`, with the example `tengo que ir al río`; a test that the book shows it once the lesson that teaches it is done.
4. **The Lessons page opens at `next:`** (9.3 E): it runs to about 192 lines, and in sessions 8 to 10 the `next:` lesson is far down. Open it scrolled so the `next:` lesson is in view; a test of the scroll offset and a screenshot of the page at session 9 (`--skip-to-session 9`).
5. **The accent note (the core's M70, `docs/nlp/API.md`, "Verbs written without the accent of their tense: `result.Notes`").** A verb typed without the accent of its tense (`ayer compre pan`) is accepted with no deduction; the core gives one `AccentNote` per such word. Read `result.Notes ?? []` (and `UtteranceAnalysis.Notes` where the game evaluates in two phases) and show `SpanishAccentNotes.Describe(note)` («compré», con tilde: es pasado) in the feedback panel under the reply, and in the host's `--transcript` output. Tests in `Game.Core` and `Host`; a screenshot of `ayer compre pan` at a lesson step that takes the past (find one, for example session 8's).
6. **The five old world words** (9.3 G): `madera`, `antorcha`, `botella`, `fuego` and `despacio` sit in the body of `content/game-v1/cefr/a1.txt`, not its marked block: move them into the block; the lint's and cefr-check's counts at A1 and A2 do not move (say so with the numbers).
7. **The book counts a correct use wherever it was understood** (9.3 G, following V20 (b)): today a use counts as correct only when it was understood at its own prompt; count it wherever it was understood (free talk included), with a test, and a save from before still loads (X01).
8. **The host's replies follow the game's turn** (found by the orchestrator's spot-check of V21). `src/Vocaterra.Host/ConversationSession.cs` copies the game's miss handling (`NpcService.AnswerMisses`) instead of calling it, so its `--transcript` output shows replies the game never says: with `--bag "coin:0"`, `quiero madera` at `stall-need` gives «¿Quieres pan?» and then the refusal (the game drops the offer after a missed gift), and without a bag it gives the bottle; `compré three manzanas` at `farmer-open` gives the recast's praise and «¡Qué bien!», and `buscaré madeda` at `lesson-will-fetch` the praise and «No te entendí» (the game gives one reply, V21's rule). Make the host's replies come from the game's own turn code (one path, not a copy), so `scripts/try-lines.sh --host` and the QA agent see what the game says; a Host test for each of the three lines. If the host cannot call the game's turn without a session it does not have, say why and apply V21's two rules (the dropped offer, one reply per turn) through the shared code instead of a second copy.

## The content loop
- **Try the core first** on any new line a learner might say: `scripts/try-lines.sh` and `--host` (the game's path), with your lines in a scratch file. A wrong grade is a core gap: report it, do not patch the core.
- **After each round of content edits** run `scripts/content-loop.sh --grammar --speech` directly. **Never run it, or `scripts/test.sh`, under `scripts/limited.sh`.** It refreshes, checks the grammar cache (with CI's rebuilt-from-nothing check), renders new lines' WAVs in the NPCs' existing voices, puts the new `bundleHash` in the save fixtures, builds, checks the goldens and runs the fast tier. Read every grammar finding: fix the content or add a waiver with its reason. Add `--record` when the goldens or the identity baseline move (a commit of their own, every moved entry listed). Once with `--tier full` before the report.
- After it finishes, check that no `content-loop.sh` or wait loop of yours is still running (`pgrep -fa content-loop`); never leave a `while pgrep …` loop behind (it matches its own command line and never ends).
- Every new or changed Spanish line: short, A1, natural, Latin American neutral, with accents and `¿¡`; list each with its pool id in the report.

## Proof
- **The host:** the stall's and León's lines at their prompts (`scripts/try-lines.sh --host` with a scratch file, the host's `--bag`); `ayer compre pan` showing the note.
- **The game:** screenshots of the Lessons page at session 9, the accent note, and one NPC's «¡Muy bien!» (stdout naming the speaker's WAV). Look at each.
- **The scripted runs:** `scripts/scripted-runs.sh` before your first edit and after, compared with `python3 -I scripts/compare-runs.py`. Explain every difference.
- **The host demos still hold:** `--transcript "quiero agua, por favor" --prompt stall-need` gives `request_item`, communicated; `--transcript "hola, quiero dos panes" --prompt baker-open` gives the sale; `--transcript "vendo madera" --prompt smith-open` gives `offer_item`; `--transcript "mañana compraré pan" --prompt baker-open` gives `state_action`.

## Read first (the sections named, not whole files)
- `CLAUDE.md`; `docs/code_guidelines.md`; `content/README.md` ("The edit loop"); the header of `scripts/content-loop.sh` (lines 1 to 45).
- `docs/HANDOVER_VILLAGE_PHASE_B.md` section 9.3 E and G (`grep -n '^\*\*E\. \|^\*\*G\. '`, about 35 lines).
- `docs/game/IMPLEMENTATION_PLAN.md` section 5, the V20 and V21 entries (`grep -n '^- \*\*V20\|^- \*\*V21'`).
- `docs/nlp/API.md`, the section on `result.Notes` (`grep -n 'result.Notes'`, about 40 lines).
- The code you change: the speech index and renderer, the book, the Lessons page, the feedback panel, the stall's agent (`content/game-v1/agents/shopkeeper.json`), `NpcService.cs`'s exchange miss.
- Never read `docs/game/SPEECH_CHECKLIST.md`, `content/game-v1/manifest.json` or the whole of `content/PHRASES.md`; grep their rows.

## Rules
- **What a game milestone edits:** the game projects and their tests, `tools/Vocaterra.SpeechRender`; `content/game-v1`, `content/locale`, `content/schema`, `content/PHRASES.md`, `content/README.md`, `content/grammar` and `content/speech-index.json`; `assets/` and `docs/game/`. It takes the core as it is, except for re-recording goldens and the identity baseline that a content edit moved.
- Engine code only in `src/Vocaterra.Game`; `Game.Core` uses no `System.Random`, no `Guid` and no clock.
- A save must still load an older save (X01).
- Do not edit `scripts/`, `docs/game/IMPLEMENTATION_PLAN.md`, `docs/game/VILLAGE_PLAN.md`, `docs/nlp/`, `docs/qa-reports/` or either `DECISIONS.md`. Never create or edit anything under `.github/workflows/`.
- `content/PHRASES.md` is the source of truth for phrases (D-game-10).

## Output, context and resources
- Read the sections named, not whole files; prefer `grep -n`, and keep `sed -n` ranges under about 150 lines. Heavy output to log files under `/tmp/claude-1000/v21b/`, at most 30 lines printed.
- Run every `dotnet restore|build|test|format|run`, every `scripts/content.sh` call, every speech render and every game or screenshot run through `scripts/limited.sh` (builds `-m:4`, tests `-m:2`); `scripts/test.sh` and `scripts/content-loop.sh` run directly. Never start two heavy commands at once: no `&`, no background builds. Exit code 137 means the 6 GB cap killed the command: lower the parallelism.
- Prefix every command inline with `PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1`. The sandbox refuses a command line containing `eval` or `hash`. Game runs need `DISPLAY=:0` and `VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models`; give each its own `--session-dir` under `/tmp/claude-1000/v21b/`.

## Gate (yours, before reporting)
- `dotnet build Vocaterra.sln -warnaserror -m:4`, 0 warnings; the full suite passing (@@COUNT@@ passed and 3 skipped before, with models; the loop's `--tier full`); report the new count.
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
- For 1 to 8: what was built, the test, the result before and after.
- The WAV count and disk cost of item 2, and what was rendered.
- Every new or changed Spanish line with its pool id.
- The scripted runs' differences, each explained.
- The goldens and identity entries that moved; the test count before and after.
- Core gaps found (the exact line and what it gave); anything left undone, and why.

Before your final message, make sure no process you started is still running (`pgrep -fa 'dotnet|Vocaterra|content-loop'`, kill only your own).
