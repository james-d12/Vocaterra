You are implementing milestone **V24, the documents and the scripted runs after the follow-ups**, of the Vocaterra repository (C#, .NET 10: a Spanish-learning first-person voxel game on MonoGame, plus a language core). The village series (`docs/game/VILLAGE_PLAN.md`, D-game-22) was merged into `main` at `1dbb0e5` (PR #20); the follow-ups (`docs/HANDOVER_VILLAGE_PHASE_B.md` section 9) are built on `feature/village-followups`: V22, V25, V23a, V20, V21, V21b in the game and M65, M71, M66, M67, M70, M69 in the core. V24 is "V09 again" (handover 9.3 J): the last milestone before the owner's pull request into `main`.

Do the work yourself; do not spawn subagents.

## Where to work, and pushing
Work only in the existing git worktree `/home/user/Dev/Vocaterra/.claude/worktrees/village`: cd there first, and never edit `/home/user/Dev/Vocaterra` itself or any other worktree. Create your branch with `git checkout -b V24-documents-and-runs @@BASE@@` (the tip of `feature/village-followups`).
- **The owner wants work pushed often.** Commit after each coherent step that builds and passes `scripts/test.sh fast`, with the prefix `V24: `. Push after every commit: `git push -u origin V24-documents-and-runs` the first time, then `git push origin V24-documents-and-runs`.
- Never push `main` or `feature/village-followups`, and do not merge. When you finish, leave the worktree on your branch, clean.

## What exists (read these first, by `grep -n`, not whole files)
- **Phase B's V09** (the model for this milestone): its entry in `docs/game/IMPLEMENTATION_PLAN.md` section 5 (`grep -n '^- \*\*V09 again'`, then about 30 lines).
- **The follow-ups' entries** at the end of that section 5 (the QA passes, V22, V25, V23a, V20, V21; `grep -n '^- \*\*V2\|^- \*\*QA'`) and in `docs/nlp/IMPLEMENTATION_PLAN.md` §5b (M65, M71, M66, M67, M70, M69; `grep -n '^- \*\*M6[5-9]\|^- \*\*M7[01]'`): their first lines and their "Left" points. V21b's entry is not written yet: its commits (`git log --oneline 407d2f2..@@BASE@@ -- src content`) and this summary stand in for it: the stall's lines («Hay agua y pan.», «No tengo eso. Tengo agua y pan.», `quiero pan` gives the bread), León's free-talk total «Una manzana, una moneda.», the shared feedback lines rendered per NPC voice (60 lines, 120 WAVs), `tener que` as the book's fifth column, the Lessons page opening at `next:`, the accent note under the reply (`ayer compre pan`), five world words moved in `a1.txt`, and the host's replies from the game's turn code (`NpcTurnRules`).
- `docs/game/README.md`, "Open points after phase B" (V09's list) and the counts around it; `docs/game/PLAYTEST.md`; `docs/game/SCRIPTED_RUNS.md` and `scripts/scripted-runs.sh` (its header says how runs are compared with `scripts/compare-runs.py`); `docs/game/SPEECH_CHECKLIST.md`'s preface only (the renderer writes it; never read it whole).

## What to build
1. **The scripted runs against `1dbb0e5`** (the merge of PR #20, where the follow-ups start). Capture all of `scripts/scripted-runs.sh` at `1dbb0e5` and at your branch, in this one worktree (check out the old commit detached, build, capture, return to your branch, build). Compare them run by run and trace every difference to a follow-up milestone. A difference no milestone explains is a finding.
2. **One new run, `r24-followups`**, deterministic across two captures, added to `scripts/scripted-runs.sh` and `SCRIPTED_RUNS.md` in the style of `r22-market`: from `--skip-to-session 10`, a lesson's line said in free talk to the NPC who taught it (V20, for example `Mañana compraré pan.` to Luz), `quiero madera` at the stall (V21 and V21b: «No tengo eso. Tengo agua y pan.» and no bottle), and `ayer compre pan` at a step that takes the past (the accent note, V21b).
3. **The learner's route through sessions 4 to 10, played**: from a new world, sleep into each session in turn (`--skip-to-session 4`, then the fire), as a learner would: the Lessons page's `next:` (it now opens there), the objective and the beacon, the lesson, then free talk once with each NPC of the lesson (say one line the lesson taught and one it did not). Report anything a learner would find wrong or stuck. Fix what is small and in the game's documents; list the rest as findings (do not change game code or content in this milestone unless a finding is a one-line fix, and then run the content loop below).
4. **The documents**, every count measured, not copied:
   - `docs/game/README.md`: the counts (lessons, phrases, nouns, verbs, speech files, check items, the battery by session, the tests skipped without models), and the follow-ups in a short paragraph (free talk, the stall and the market's replies, the accent note, the per-NPC feedback voices, the name plates, the two session counters);
   - `docs/game/PLAYTEST.md`: what changed for a playtest (free talk takes the lessons' lines, the Lessons page, the session counters, the accent note);
   - `docs/game/SPEECH_CHECKLIST.md`: regenerate its preface with the renderer (never hand-edit it) and count the rows the follow-ups added, for the owner (no voice has been heard yet);
   - `content/README.md` and the game projects' READMEs where a count or a list is stale.
5. **CI's case:** the full suite once without models (`VOCATERRA_MODELS_DIR=<an empty folder under /tmp/claude-1000/v24/> scripts/test.sh full`), and every test that measures wall-clock time listed with its measure and its budget (the GitHub runner is about four times slower than this machine; at V09 the bundle load was 1438 of 3000 ms, the world snapshot 0.50 of 1.5 s, the scene build and digest 708 of 3000 ms). Say which would likely fail on CI.
6. **The open points, rewritten:** replace `docs/game/README.md`'s "Open points after phase B" with "Open points after the follow-ups": drop every point a follow-up fixed (check each against the entries above, and say which milestone fixed it in your report, not in the list), keep the rest, and add the new ones from the follow-ups' "Left" lines. Mark each owner, game or core. Known to carry, at least: the voices not yet heard (owner); at an NPC's open prompt a correct line with a word outside the prompt's answer set gets `vocab.wrong` and "No te entendí" (`la tienda está en el pueblo` at `farmer-open`; core and game, V17 and M69); `cuántas cuesta todo` read as a noun phrase (core, M69); the recorded fuzz families `2tpLGUOSqj41` (with `fSMdah3kOSd7`) and `8yc522XLB_W2` (core, rerun once); M67b parked (core; the game already retries a tense miss); the timed tests on CI (M68 only if one fails); the merged milestone branches still on the remote (owner).

## Output and context (the cost of a milestone is its turns times its context)
- Send every heavy command's output (builds, tests, scripted runs) to a log file under `/tmp/claude-1000/v24/`, and print at most 30 lines of it.
- Keep `sed -n` ranges under about 150 lines; prefer `grep -n` to find the place first.
- Wait for a long command by running it in the foreground (through `scripts/limited.sh` where the rules below say so), not by polling a log. **Never leave a wait loop or a background process behind** (a `while pgrep -f …` loop matches its own command line and never ends).
- Compare captures with `diff`, `cmp` and the run logs' summaries, not by reading whole logs; look at a screenshot only where a difference needs it.
- **Disk:** about 12 GB free. Keep only the two captures you compare, and delete them when the report is written. The old checkout's build reuses this worktree's `bin/` and `obj/`; do not create another worktree.

## Resources (the desktop has crashed from overload)
- Run every `dotnet restore|build|test|format|run`, every `scripts/content.sh` call, every speech render and every screenshot through `scripts/limited.sh` (`scripts/scripted-runs.sh` does it itself). `scripts/test.sh` and `scripts/content-loop.sh` run directly and must **never** be wrapped in `scripts/limited.sh`.
- Never start two heavy commands at once: no `&`, no background builds.
- While iterating, use `scripts/test.sh fast`. Run `scripts/test.sh full` once before reporting.
- Exit code 137 means the 6 GB cap killed the command: lower the parallelism, and never work around the cap.

## Environment
- Prefix every command inline with `PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1`.
- The sandbox refuses a command line that contains the words `eval` or `hash`. Use a wrapper script under `/tmp/claude-1000/v24/` for those.
- Model-gated tests read `VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models`. Game runs need `DISPLAY=:0`; give each its own `--session-dir` under `/tmp/claude-1000/v24/`.

## Rules
- **What this milestone edits:** `docs/game/` (except the two below), `content/README.md`, the game projects' READMEs, `scripts/scripted-runs.sh` for deliverable 2, and a one-line game fix if item 3 finds one (with a test, and for content the loop: `scripts/content-loop.sh --grammar --speech` directly, `--record` if the goldens move, `--tier full` once).
- It takes the core (`src/Vocaterra.Nlp*`, `tests/Vocaterra.Nlp.*`, `tests/golden`, `docs/nlp/`) as it is. A core gap is reported, never patched.
- Do not edit `docs/game/IMPLEMENTATION_PLAN.md`, `docs/game/VILLAGE_PLAN.md`, `docs/nlp/IMPLEMENTATION_PLAN.md`, either `DECISIONS.md`, `docs/HANDOVER_VILLAGE_PHASE_B.md`, `scripts/merge-gate.sh` or anything under `.claude/` or `.github/workflows/`.
- Write documents that read like the surrounding ones: short sentences, counts measured.

## Gate (yours, before reporting)
- `scripts/limited.sh dotnet build Vocaterra.sln -warnaserror -m:4`, 0 warnings.
- `scripts/test.sh full`, all passing. It was @@COUNT@@ passed and 3 skipped; report the new count.
- `scripts/limited.sh dotnet format Vocaterra.sln --verify-no-changes`.
- `scripts/limited.sh scripts/content.sh validate`.
- The new run gives the same result twice.
- Do not commit `bin/`, `obj/` or `artifacts/`. When you finish, `git status` is clean and the branch is pushed.

## Commit
Each message ends with:
```
Co-Authored-By: Claude Haiku 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDfGVmxEaTFXNb87eD1
```

## Report (one page, under 450 words; the details belong in the commits and the documents)
- The branch and the final commit SHA, pushed.
- The scripted runs: every difference since `1dbb0e5` and the milestone it traces to; any difference no milestone explains.
- The new run, and whether it is deterministic.
- The learner's route through sessions 4 to 10: what a learner meets at each session's start, in each lesson and in free talk; what you fixed; what is left.
- The documents changed, with the counts measured.
- CI's case: the count without models, and the timed tests with their measures and budgets.
- The open points: what you dropped (and the milestone that fixed each), what you kept and added.
- The test count before and after; anything left undone, and why.

Before your final message, make sure no process you started is still running (`pgrep -fa 'dotnet|Vocaterra|content-loop|scripted-runs'`, kill only your own).
