# Handover: the adaptation, phases B to D, on feature/adaptive_design

Created: 2026-11-05T20:31Z (UTC)
Updated: 2026-11-06T01:30Z (UTC), after N08's gate: phases B to D are done; a QA pass on the conversation surface runs (kept
current after every gate, launch and owner decision: the owner, 2026-11-05)
Workstream: the owner's Adaptation Design Document, phases B to E: the needs (N), lessons as classes (L01), the presentation track (U),
then the bread pilot (W)
Branch: `feature/adaptive_design` (pushed; the owner merges it into `main` once, at the end); HEAD: see `git log -1` (the last record
commit is named in "Completed" below)
Worktree: `/home/user/Dev/Vocaterra/.claude/worktrees/adaptive` (the main checkout `/home/user/Dev/Vocaterra` sits on the old
`feature/village-decisions` and is not used)
Previous handovers: [2026-11-03-2114-adaptation-design](done/2026-11-03-2114-adaptation-design.md) (the design's revision 3, PR #30,
merged) and [2026-11-02-after-followups](done/2026-11-02-after-followups.md) (the follow-ups M74, M75, V29, M78 and the spoken-lines
study, all merged into this branch)

## Outcome

The owner's instruction of 2026-11-04 ("implement this design" on `feature/adaptive_design`, Sonnet as the workhorse, Haiku for light work,
Opus sparingly; scope through W10, phases B to E; section 18.2's recommendations adopted provisionally; `docs/game/decisions.md`, D-game-23,
"Owner's answers, 2026-11-04") is being carried out. The follow-ups and phases B and C are merged and gated; phase D (the presentation
track) is half done; phase E (the bread pilot, W05 to W10) waits for the owner's needs playtest. All of it stays on
`feature/adaptive_design` for one merge into `main` at the end (the owner, 2026-11-05).

## Working tree and running work

- **One agent runs:** a QA pass (`subagent_type: qa`, area `ui`, narrowed to the conversation surface U02 and the settings panel N09)
  on the adaptive worktree; it writes `docs/qa-reports/2026-11-06-ui.md` and builds nothing new beyond the game. When it ends: add one
  row per bug to the triage table in `docs/qa-reports/README.md`, confirmed bugs to the game plan's findings, and propose fix milestones.
  - Their reports reach only the session that launched them. **If this session is gone:** check each worktree (`git -C <wt> status`,
    `git -C <wt> log --oneline feature/adaptive_design..`), commit any uncommitted work as a WIP snapshot on its branch, and relaunch a
    fresh agent with a "you continue a first attempt" section, as [`n05b-continue.md`](../../.claude/briefs/n05b-continue.md) did for N05.
- A background memory and load watch may be running from this session (a shell loop; it ends by itself within 6 hours).
- The adaptive worktree is clean apart from this handover's own change; nothing uncommitted elsewhere.
- **Branches:** milestone branches stay local and are never pushed (the owner, 2026-11-05); only `feature/adaptive_design` is pushed. The
  remote still holds older milestone branches, all merged; the owner deletes them (the permission check refuses remote deletes).

## Completed (each gated into feature/adaptive_design; statuses and findings in the plans)

| Milestone | What | Gated at, tests |
| --- | --- | --- |
| M74, M75, V29, M78 | The follow-ups: weather and wellbeing intents and replies, the near-miss rule, core fixes; the grammar cache refreshed | via `feature/village-decisions`, synced in (R) |
| N01 to N04, N06 | Needs model, eating and drinking, night and collapse, death and the chest, regrowth and gifts | `3a63832e` … `468a1b29` |
| M81, M82 | Every intent needs a grammar, a rule or an example (the owner's call) | M82 9363 |
| W01 | `item_crafted` | `56f88b4b`, 9385 |
| N05 | The needs HUD, relaxed needs, the day-1 safety test | `6dfa8566`, 9433 |
| L01 | Lessons as optional classes (D-game-25) | `971b1058`, 9451 |
| N07 | Cooking at the fire (the roast apple) | `20a8da0c`, 9461 |
| U01 | Clean defaults (`--dev`/F3, one speak hint, the tracker) | `dd01a6e9`, 9476 |
| N09 | The drive rate as a setting: the F8 panel's slider ×0.5 to ×4, `--needs-rate` (the owner, 2026-11-05) | `37c7640b`, 9501 |
| N08 | A berry planted at the camp (decision 14): right click on grass within 8 blocks of the camp ring; grows at night | `209049db`, 9561 |
| U02 | The conversation surface: the current exchange, older lines behind PageUp/O, one coaching line, the lesson heading in the panel | `fe986862`, 9546 |

- Plans: [game plan](../game/implementation-plan.md) section 4.9 (the table) and section 5 (one findings entry per milestone);
  [core plan](../nlp/implementation-plan.md) §5 and §5b; [activities plan](../game/activities-plan.md) section 4.7.
- Reports: `.claude/briefs/<id>-report.md` for each milestone above.
- Process changes today: `scripts/limited.sh` runs two heavy commands at once (8 GB and 14 cores together, 6 GB and 10 cores each, the
  peak memory printed); `scripts/merge-gate.sh` skips those peak lines in its summaries; milestone branches are not pushed; this handover
  is kept current. The orchestrator file and `CLAUDE.md` say so.

## Unfinished and blocked

- **Authorised, in progress:** the QA pass (above). Phases B to D are done.
- **Authorised, next:** nothing building. The owner withdrew the wind-down (2026-11-05, late: "continue with the work, I'm not going
  to handover. But keep the document updated just in case") and went to sleep.
- **Proposed, not authorised: T7, the game tests' memory** (brief [`.claude/briefs/t7-test-memory.md`](../../.claude/briefs/t7-test-memory.md),
  Sonnet 5.5). `Vocaterra.Game.Core.Tests` alone peaks at 5.4 GB (2.80 GB at the test-performance plan's T1); beside a second assembly
  under `scripts/test.sh full` (one `limited.sh` command, 6 GB) the OOM killer ends the run (exit 143): twice on N08's merged tree, once
  in N09. Gates pass or fail by how the assemblies overlap. The rule forbids raising the cap; T7 cuts the memory or schedules that
  assembly alone. Until it lands, a gate killed with 143 is rerun once and the kill recorded (cause known, not the milestone's).
- **Blocked on the owner:**
  - **The needs check** (design 9.12): the owner and two or three testers play days 1 to 3 with needs on. Its pass criteria are in the
    game plan's row and the design's 9.12. Its "go" starts the bread pilot W05 to W10 (activities plan section 4.7). The owner may lift
    that gate for W05 to W07 (machinery that does not depend on the needs' tuning); W08 also waits for the spoken-lines answers.
  - **The spoken-lines study's six questions** ([docs/research/2026-11-04-spoken-off-prompt-lines.md](../research/2026-11-04-spoken-off-prompt-lines.md)
    section 7): they decide M80 (core) and V31 (game), and W08 depends on the study.
  - **Section 18.2's recommendations** were adopted provisionally; the owner confirms or changes each at the review. The provisional
    values each milestone chose are in its findings entry.
- **Known points (no action unless the owner asks):** «¿Me enseñas?» takes no core intent (L01 reads the exact forms; a core milestone
  would widen it); the design's Domingo teaches nothing, so the villager offers its class; a wall-clock test
  (`GeneratedLexiconTests.LoadingTheBundleIsFastEnough`) failed once under two agents' load (passed alone and in the gate): watch it;
  the owner kept «Me muero de…» (`morir` above A2) on 2026-11-05.
- **Not planned:** a visual redesign of the look (styling, fonts, the book, inventory and crafting panels); U03 and U04 come after the
  pilot's last recall. The owner asked about UI redesign on 2026-11-05; propose new U milestones if he wants it.

## Verification

- Every milestone above passed `scripts/merge-gate.sh` (build with warnings as errors, the full suite with models, format, the content gate,
  grammar, licences) and an orchestrator spot-check in the game; the evidence is in each findings entry and `.claude/gate-logs/`.
- Not run: the needs playtest; CI on GitHub (the token cannot read it; the owner reports CI failures).

## Next steps

1. When a milestone reports: verify the branch (`git diff --stat <merge-base>..<branch>`, scope), check conflicts with
   `git merge-tree --write-tree --name-only HEAD <branch>`; if only generated files or docs conflict, merge the base into the branch in its
   worktree, resolve, run `scripts/content-loop.sh --speech --tier none` (or `--grammar --speech --tier full` if code or content changed on
   both sides), commit; otherwise brief a Haiku sync as [`n07-sync.md`](../../.claude/briefs/n07-sync.md).
2. Gate from the adaptive worktree: `GATE_TARGET=feature/adaptive_design scripts/merge-gate.sh "<ID>:<branch>"` (in the background, with
   `VOCATERRA_MODELS_DIR` set), spot-check in the game, record the plan row and a findings entry, save the report, commit, push
   `feature/adaptive_design`, remove the worktree and the local branch, **update this handover**.
3. Phases B to D are done (N08 and U02 merged 2026-11-06): give the owner the review notice (what to playtest, with N09's slider:
   the settings panel and `--needs-rate`), and wait for the needs check or the owner's word on W05 to W07.
