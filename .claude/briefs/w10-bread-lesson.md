You are implementing the game milestone **W10, session 11 "En la panadería"**, of the Vocaterra repository (C#, .NET 10, a MonoGame first-person voxel game that teaches Spanish). The language core is the `Vocaterra.Nlp` engine and the Spanish pack `src/Vocaterra.Nlp.Es`; the game is `Vocaterra.{Game.Core,Game,Host,SpeechRender}`. **This milestone is mostly content** (`content/game-v1`), with the code the content needs: bread with Luz end to end, from her offer or the optional class, through the six-action recipe at the bakery's station, with flour sold at the stall.

Do the work yourself; do not spawn subagents.

## Two phases, with a review between them
- **Phase A (draft and try):** write every new Spanish line and row (the recipe's `does` rows, the learner's `says` rows, the `hears` rows, the lesson's text, Luz's new lines, the shopkeeper's flour lines), try the lesson's production lines and error examples in the core's CLI, and commit the draft content **without recordings** (no `--speech` yet; run the content loop with `--grammar` only). Then **end your turn** with the Phase A report below. The orchestrator reviews the Spanish (the design's "Opus pass") and sends you the corrections.
- **Phase B (build and record):** apply the corrections, then the code, the recordings, the checks, the run and the gate, and end with the final report.

## Where to work, and committing
Work only in the git worktree `/home/user/Dev/Vocaterra/.claude/worktrees/w10`, already on the branch `W10-bread-lesson` (from `feature/bread-pilot` at its tip, with W05 to W08 merged). cd there first; never edit `/home/user/Dev/Vocaterra` or any other worktree (`.claude/worktrees/adaptive` and `.claude/worktrees/german` are not yours: never touch them); do not switch branches.
- **Commit after each item that builds and passes its tests**, with the prefix `W10: `. Never end your turn, or wait on anything, with uncommitted work.
- **Do not push**: your branch stays local and the gate merges it from there. Never push or merge `feature/bread-pilot`, `feature/adaptive_design` or `main`. When you finish, leave the worktree on your branch, clean.
- **Run everything in the foreground and finish each phase in one turn.** Do not start background commands or monitors and end your turn to wait for them. The content loop and a scripted run take minutes in the foreground; wait for them.

## The rules, environment, resources and gate
Read `/home/user/Dev/Vocaterra/.claude/briefs/w07-station-view.md`, the sections **Output and context**, **Resources**, **Environment**, **Rules** and **Gate**: they apply to you unchanged, with `/tmp/claude-1000/w10/` for your logs and session folders, about 12 GB of disk free, and **9794 passed and 3 skipped** as the count before you. This milestone edits `content/game-v1`, so the Rules' last point applies: `scripts/content-loop.sh` after each content edit (run directly, never under limited.sh; `--grammar` in Phase A, `--speech` in Phase B) and `scripts/content-loop.sh --speech --tier full` once before the final report instead of `scripts/test.sh full`. Read `content/README.md` for how to edit content and `docs/nlp/changing-the-core.md`'s part on the shared bundle (the goldens, including the identity baseline, and `save-v1.json`'s bundle hash after a re-hash). The content gate is the A2 ceiling: `scripts/content.sh lint content/game-v1 --strict --level a2 --fail-on-level --host-pools 'guide.*'` clean, and the host's `--validate-content` holds each lesson to its own `level`.

**The scripted runs:** capture `scripts/scripted-runs.sh` before your first edit (Phase A) into `/tmp/claude-1000/w10/runs-before`, and after (Phase B). With a `pan` procedure in the bundle, W08's offer becomes live, so runs that meet Luz may change: explain every difference in the report.

## The specification
- `docs/game/activities-plan.md`: section 6's paragraph **W10** (`sed -n 294,301p`), section 7's content table (`sed -n 444,460p`), section 9's W10 row (`sed -n 485p`), and section 4.4 to 4.7 for what W05 to W08 built (`sed -n 99,158p`). That paragraph is the milestone.
- The owner's design document, extracted one paragraph per line at `/tmp/claude-1000/orch/adaptation-design.txt`. Read only these ranges: **10.4** the pilot example (`sed -n 718,738p`); **10.6** the optional class (`sed -n 742,792p`); **10.9** step by step, the six actions (`sed -n 843,894p`); **10.12** ingredients and the economy (`sed -n 948,955p`); **10.16** content, the draft rows (`sed -n 1002,1033p`); **10.18** tests and runs (`sed -n 1038,1043p`); section 18.2's decisions **5** (the recipe) and **6** (session 11 and flour) (`sed -n 1424,1429p`) and **20** (ingredient grants, `sed -n 1469,1471p`). Decisions 5, 6 and 20 are adopted provisionally (`docs/game/decisions.md` D-game-23, the answers of 2026-11-04); where one leaves a value open, take the smallest choice that keeps the no-dead-end rule and saves every persistent state, and list it in the report.
- The bakery's stall answers are fixed host events (D-game-23, 2026-11-03, answer 3: the stall buys bread for one coin): flour sold for one coin, bread bought for one coin. Check that V21b's `instead` can send the shopkeeper's answer to a request for `harina` to the flour sale. **No W04 price list**: fixed events only.
- **What W05 to W08 built** (reports in `.claude/briefs/`: `w05-report.md`, `w06-report.md`, `w07-report.md`, `w07b-report.md`, `w08-report.md`, `w08-report-2.md`): procedures, stations and actions as data and their validator; `ProcedureRunner` (the ladder, folding, the goal, the reward, mastery, `systems.procedures`); the station view and its fixture icons `bowl`, `spoon`, `oven` with a fallback tile; the teacher in the loop (Luz's `offering_bake` and `teaching` states, `baker-proc`, the host kinds, the daily grant). **W08 left W10 a seam:** `GuideSession.GrantRequiredByLesson` (overridden in `VoxelSession`), which opens the grant while the lesson's do step waits for `procedure_done`; use it for the class's do step. W08's throwaway screenshot bundle needed fixes to pass the checks (its report, item 4: provenance for `pan.json`, the nouns `mesa`, `horno`, `mano`, `echar` replaced by `poner` at A2); learn from them.
- **The lesson** is a file like `game/lessons/el-mercado.json`, `npc baker`, `level a2`, placed after `ayer-y-manana` (10.6). It stays optional (D-game-25): only the introduction is required, and sessions 1 to 10 must be unchanged.
- **Flour** takes the next free item enum ID (check which is free: the plan says 34 if N07's roast apple took 33), with its noun, gender, icon, lexicon entry and any justified world-word waiver. **bol or tazón:** pick one in Phase A, say why, and use it everywhere.
- Where the design and the code disagree on a file or a name, the code wins; say so in the report. List every value you choose where the design leaves one open.

## What to build (each committed)
1. **Phase A, the draft:** the `pan` procedure (six actions, two chunk rows, one goal row, per 10.9 and decision 5) and the bakery's station and fixtures as data; the phrase sheet's rows (9 `does`, 4 `says`, 5 `hears`, per 10.16); flour; the lesson file; Luz's and the shopkeeper's new lines; English support text. Try each production line and each error example of the lesson in the CLI (`scripts/limited.sh dotnet run --project tools/Vocaterra.Nlp.Cli -- eval --transcript "<line>" --prompt <prompt> --content content/game-v1`, through a wrapper script because of the word `eval`); a line the core misreads is reworded or reported, never patched in the core.
2. **Phase B, the code the content needs:** the flour sale and the bread purchase at the stall as fixed host events, with `instead` for `harina`; the lesson's do step opening the grant through `GrantRequiredByLesson`; the buying and selling turns in the lesson's flow; anything else the procedure or the lesson needs that W05 to W08 did not build (say what and why).
3. **Recordings:** normal and slow for every new spoken line, through the content loop's `--speech`.
4. **Checks:** the lesson's comprehension checks, filtered to the lesson as the other lessons' are.
5. **Tests:** the bread procedure loads and validates; a full run of it through the runner (rung 1 to done, the reward, `procedure_done`); the flour sale and the bread purchase; `instead` for `harina`; the grant open during the class's do step and closed otherwise; the lesson's flow with typed lines; Luz's offer live now that the procedure exists; sessions 1 to 10 unchanged.
6. **The scripted run `r27-bread`** (`docs/game/scripted-runs.md`: add its row as the other runs have one): from the offer, or the class, to the first bread, with captures at the station. Look at its PNGs; they also check W07's station view, which has only been checked on throwaway bundles so far.

Out of scope: the session summary, telemetry and `r28` (W11); the recipe book (W30); price lists (W04); fail-forward patience (W03); any core intent (a core milestone, reported if needed); the owner's listening.

## Commit
Each message ends with:
```
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
```

## Phase A report (end your turn with it)
- The branch and the commit SHA.
- **Every new Spanish line or row**, one per line, with its id, its kind (`does`/`says`/`hears`, dialogue, lesson text, check), its English, and its CEFR level from the lint; the bol-or-tazón choice and why.
- The CLI verdict for each production line and each error example (intent, `communicated`, error tags), and any you reworded.
- The values you chose where the design leaves one open, and any place the design and the code disagree.

## Final report (one page, under 600 words)
- The branch and the final commit SHA (local, not pushed).
- Each deliverable: what you built (file and type), its tests.
- The corrections from the review and how you applied them; every Spanish line added or changed after Phase A.
- Values chosen, design versus code.
- The scripted runs' differences, each explained; `r27-bread`'s PNG paths (under `/tmp/claude-1000/w10/`) and what each shows.
- The goldens re-recorded and why, the test count before and after, the lint and grammar results, anything left undone and why.

Before each report, make sure no process you started is still running (`pgrep -fa 'dotnet|Vocaterra|scripted-runs|content-loop'`, kill only your own).
