You are implementing milestone **V09 again, the documents and the scripted runs for phase B**, of the Vocaterra repository (C#, .NET 10: a Spanish-learning first-person voxel game on MonoGame, plus a language core). Phase B of the village series (`docs/game/VILLAGE_PLAN.md`, D-game-22) is built: sessions 7 to 10, all `a2` (V08, V13, V14, V16, V17, V15, V18, and the core's M56 to M62). V09 is run once per phase (VILLAGE_PLAN section 4, its row); phase A's V09 is the model. It is the last milestone of phase B: after it, the owner reviews the whole branch.

Do the work yourself; do not spawn subagents.

## Where to work, and pushing
Work only in the existing git worktree `/home/user/Dev/Vocaterra/.claude/worktrees/village`: cd there first, and never edit `/home/user/Dev/Vocaterra` itself or any other worktree. It is on branch `feature/village` (at the HEAD `git log -1` shows, `@@HEAD@@`).
- Create your branch with `git checkout -b V09-phase-b feature/village`.
- **The owner wants work pushed often.** Commit after each coherent step that builds and passes `scripts/test.sh fast`, with the prefix `V09: `.
- Push after every commit: `git push -u origin V09-phase-b` the first time, then `git push origin V09-phase-b`.
- Never push `main` or `feature/village`, and do not merge.

## What exists (read these first)
- **Phase A's V09** (its entry in `docs/game/IMPLEMENTATION_PLAN.md` section 5: `grep -n '^- \*\*V09'`, then about 30 lines): what it compared, built and found. Do the same for phase B.
- **Phase B's entries** at the end of that section 5: V08, V13, V18, V14, V16, V17, V15 with V15b (and the core's M56 to M62 with M62b in `docs/nlp/IMPLEMENTATION_PLAN.md` §5b, by `grep -n`; only their first lines and "left" points). Collect every point they left open for the documents or the runs.
- `docs/game/VILLAGE_PLAN.md`: section 4 (V09's row, "Every content milestone": "the counts in the documents (V09)") and section 5 ("The playtest's battery", "The session count", "Not overwhelming", "The recognizer").
- `docs/game/SCRIPTED_RUNS.md` (the runs and how they are compared) and `scripts/scripted-runs.sh`.
- `docs/game/README.md` and `docs/game/PLAYTEST.md`: by `grep -n` for counts (lessons, phrases, nouns, speech files, items, sessions, tests skipped without models, `--talk`, `--session`, `--skip-to-session`), then the sections they sit in. `docs/game/SPEECH_CHECKLIST.md`: its preface only (written by the renderer; never read it whole).

## What to build
1. **The scripted runs against the end of phase A.** Capture all of `scripts/scripted-runs.sh` at `ccf8187` (`feature/village` before V08's merge: phase A, V12b and V12c) and at the HEAD, in this one worktree (check out the old commit detached, build, capture, return to your branch, build; phase A's V09 did the same against `main`). Compare them run by run with `scripts/compare-runs.py` (the header of `scripts/scripted-runs.sh` says how), and trace every difference to a phase-B milestone (or to `main`'s merge at `0cf674b`, which brought CI and the QA agent). A difference no milestone explains is a finding.
2. **Two new runs for phase B**, deterministic across two captures, added to `scripts/scripted-runs.sh` and `SCRIPTED_RUNS.md`, in the style of `r18-bakery` and `r19-directions`:
   - `r22-market`: session 10 from `--skip-to-session 10`: sell to an NPC with `buy` ("vendo madera"), buy at the stall or the baker with those coins, ask "¿cuánto es todo?"; the inventory and the coins before and after;
   - `r23-future`: session 9's or 10's future at an NPC in free talk ("mañana compraré pan" at `baker-open`), the reply and the phrase credited, and the book's future column after it (`--book-verb comprar`, S28: the verbs page with that verb's table).
   If V15 named a run of its own, keep it and say so.
3. **The learner's route through phase B, played**: from `--skip-to-session 7`, each lesson in turn by sleeping at the fire (7, 8, 9, 10), as a learner would: the Lessons page's `next:`, the objective and the beacon at the start of each session, the lesson, then free talk once with each NPC of the lesson. Report anything a learner would find wrong or stuck (phase A's lessons were unreachable in practice until V12c, and V12b's wrong credit showed only in free talk). Fix what is small and in the game; list the rest as findings.
4. **The documents**, with every count measured, not copied:
   - `docs/game/README.md`: the village section for sessions 7 to 10 (the four lessons in a line each, the futures, the market, `buy`), the counts (lessons, phrases, nouns, verbs, speech files, check items, the battery by session), `--session` and `--skip-to-session` up to 10, and the tests skipped without models;
   - `docs/game/PLAYTEST.md`: the pilot's battery (does it stay at 31 items? the phase-B items outside it), what a playtest of sessions 7 to 10 would need;
   - `docs/game/SPEECH_CHECKLIST.md`'s preface (written by the renderer: the provisional voices; regenerate it, do not hand-edit it), and the rows phase B added, counted for the owner (the owner has not heard any voice yet);
   - `content/README.md` and the game projects' READMEs where a count or a list of lessons or NPCs is stale.
5. **CI's case:** the full suite once without models (`VOCATERRA_MODELS_DIR=<an empty folder under /tmp/claude-1000/v09/> scripts/test.sh full`), and every test that measures wall-clock time listed with its measure and its budget (the GitHub runner is about four times slower than this machine; three older ones were within four times of their budgets at V16: the bundle load, the scene build and digest, the world snapshot). Say which would likely fail on CI.
6. **The open points of phase B, in one list** for the owner's review, each with where it was found and whether it is a game point, a core gap, or an owner decision (for example the voices not yet heard, name plates through figures, `KeyWordsTaught` as one save flag, `tener que` without a book entry, the NPCs' "¡Muy bien!" in Luna's voice, `vocab.wrong` outside a constrained answer set, `fisher-open` without plans; and V15's: the core gaps `¿puede usted vender manzanas?` (a wrong `conj.person`), `puedo pago con fruta` and `quiero vendo madera` (no error), `¿cuánto cuestan todo?` (no `conj.person`), `ti` not in the lexicon, `¿cuánto son todo?` (intent `unknown`); the host's `--transcript` mode prints a sale's success line whatever the bag holds; the fuzz findings left for the owner, seeds `egVgLELrxyL4`, `2ldq2rIKLrG2`, `b3HSKSLCsL_1`, and the core's M63, filed). Check each against the plans' section 5 entries; drop what a later milestone fixed. Put it at the end of `SCRIPTED_RUNS.md`'s phase-B section or of `docs/game/README.md`'s village section, whichever reads better, and in the report.

## Output and context (the cost of a milestone is its turns times its context)
- Send every heavy command's output (builds, tests, scripted runs) to a log file under `/tmp/claude-1000/v09/`, and print at most 30 lines of it.
- Keep `sed -n` ranges under about 150 lines; prefer `grep -n` to find the place first.
- Wait for a long command by running it in the foreground through `scripts/limited.sh`, not by polling a log. **Never leave a wait loop or a background process behind.**
- Compare captures with `diff`, `cmp` and the run logs' summaries, not by reading whole logs; look at a screenshot only where a difference needs it.
- **Disk is tight (about 10 GB free):** keep only the two captures you compare, and delete them when the report is written. An old checkout's build reuses this worktree's `bin/` and `obj/`; do not create another worktree.

## Resources (the desktop has crashed from overload)
- Run every `dotnet restore|build|test|format|run`, every `scripts/content.sh` call, every speech render and every screenshot or scripted run through `scripts/limited.sh` (`scripts/scripted-runs.sh` does).
- Never start two heavy commands at once: no `&`, no background builds.
- While iterating, use `scripts/test.sh fast` or `scripts/test.sh project <test project>`. Run `scripts/test.sh full` once before reporting.
- Exit code 137 means the 6 GB cap killed the command: lower the parallelism, and never work around the cap.

## Environment
- Prefix every command inline with `PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1`.
- The sandbox refuses a command line that contains the words `eval` or `hash`. Use a wrapper script under `/tmp/claude-1000/v09/` for those.
- Model-gated tests read `VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models`.
- Game runs need `DISPLAY=:0`.

## Rules
- **What a game milestone edits:** the game projects (`src/Vocaterra.Game.Core`, `src/Vocaterra.Game`, `src/Vocaterra.Host`, `tools/Vocaterra.SpeechRender`) and their tests; `content/game-v1`, `content/locale`, `content/README.md`; `assets/` and `docs/game/`; `scripts/scripted-runs.sh` for deliverable 2.
- A content edit (only to fix a finding) runs the content loop: `scripts/content-loop.sh --grammar --speech` directly (never under `limited.sh`), `--record` if the goldens move, `--tier full` once before the report.
- It takes the core (`src/Vocaterra.Nlp*`, `tests/Vocaterra.Nlp.*`, `tests/golden`, `docs/nlp/`) as it is. A core gap is reported, never patched.
- Engine code goes only in `src/Vocaterra.Game`. No `System.Random` and no clock in the evaluation path.
- Do not edit `docs/game/IMPLEMENTATION_PLAN.md`, `docs/game/VILLAGE_PLAN.md`, `docs/nlp/IMPLEMENTATION_PLAN.md`, either `DECISIONS.md`, `scripts/merge-gate.sh` or anything under `.claude/` or `.github/workflows/`.
- Write documents that read like the surrounding ones: short sentences, counts measured.

## Gate (from the worktree root, each through scripts/limited.sh)
- `dotnet build Vocaterra.sln -warnaserror -m:4`, with 0 warnings.
- `dotnet test Vocaterra.sln -m:2` all passing. It was @@COUNT@@; report the new count.
- `dotnet format Vocaterra.sln --verify-no-changes`.
- `scripts/content.sh validate`, the content gate of `scripts/merge-gate.sh` (the lint at A2 with fail-on-level and `--host-pools 'guide.*'`, the cefr-checks) and the host's `--validate-content`.
- The two new runs give the same result twice.
- Do not commit `bin/`, `obj/` or `artifacts/`. When you finish, `git status` is clean and the branch is pushed.

## Commit
Each message ends with:
```
Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDbfCAUCv1Pc1rbMkuD
```

## Report (one page, under 450 words; the details belong in the commits and the documents)
- The branch and the final commit SHA, pushed.
- The scripted runs: every difference since `ccf8187` and the milestone it traces to; any difference no milestone explains.
- The two new runs, and whether they are deterministic.
- The learner's route through sessions 7 to 10: what a learner meets at each session's start, in each lesson and in free talk; bugs found and fixed (where, symptom, fix, test); what is left.
- The documents changed, with the counts measured.
- CI's case: the count without models, and the timed tests with their measures and budgets.
- The open points of phase B, in one list, each marked game, core or owner.
- The test count before and after.
- Anything left undone, and why.
