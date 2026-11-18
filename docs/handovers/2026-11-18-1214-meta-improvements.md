# Handover: the throughput changes (feature/meta_improvements)

Created: 2026-11-18T12:14Z (UTC)
Updated: 2026-11-18T12:40Z (UTC)
Workstream: making development with agents faster: the throughput study's remaining changes, re-ranked by the review the owner approved (D-game-34 items 57 to 59)
Branch: `feature/meta_improvements`, cut from `main` at `bb89521b` (the merge of PR #69). HEAD: the commit that records B29a's launch. Pushed; **draft pull request #70** into `main` (item 59), so every push runs CI.
Worktree: `/home/james/Dev/Vocaterra/.claude/worktrees/meta-improvements`
Previous handover: none. B29 was split out of [2026-11-18-0409-world-next](2026-11-18-0409-world-next.md), which keeps the chunk-speed series and the rest of the world work.

## Outcome so far

- **The evidence:** the study [2026-11-17-ai-development-throughput.md](../research/2026-11-17-ai-development-throughput.md) (with a status note) and the review [2026-11-18-throughput-review.md](../research/2026-11-18-throughput-review.md). Read the review's sections 4 and 5 first: they are the plan.
- **The owner's decisions** (docs/game/decisions.md, D-game-34): item 57 (the review's changes approved; B29 split into B29a and B29b; the token policy and stamp skip deferred; **the chunk-speed milestones 0a to 0f do not run on this branch**), item 58 (timing tests in the gate and CI only; the fuzz suite in CI only), item 59 (keep a pull request open so CI runs on every push; the orchestrator reads CI with `gh run list`).
- **The plan rows:** docs/game/implementation-plan.md section 4.14, B29 (split), B29a and B29b.
- **Already applied in this branch's first commits:** the orchestrator's CI section now says to read CI with `gh run list --branch <branch>` and `gh run view <id> --log-failed` (the token has the `repo` and `workflow` scopes; `gh run list` worked on 2026-11-18).
- **B29a is running** (launched 2026-11-18 about 12:35 UTC): a Sonnet 5.5 agent at medium effort, worktree `/home/james/Dev/Vocaterra/.claude/worktrees/b29a`, local branch `B29a-throughput` cut from `aac46848`, brief [.claude/briefs/b29a-throughput.md](../../.claude/briefs/b29a-throughput.md). Its report reaches only the launching session. **If that report is lost:** inspect the worktree (`git -C .claude/worktrees/b29a log --oneline aac46848..`, `git status`), commit any uncommitted work as a WIP snapshot, and relaunch a fresh agent on the same brief with "continue from the commits already on the branch". It may leave a scratch branch `b29a-gate-scratch` and a scratch worktree for its gate dry run (deliverable 7); remove both if left.
- No gate has run on this branch yet.
- CI on `main` for the PR #69 merge (run `44298795061`) was still in progress at 12:21; check it (step 2 below).

## Start here

1. Run the start-of-session checks in `.claude/agents/orchestrator.md`. Check this handover's branch and HEAD against git (`git -C /home/james/Dev/Vocaterra/.claude/worktrees/meta-improvements log --oneline -3`).
2. Check CI on `main` for the PR #69 merge: `gh run list --branch main --limit 3`. Run `44298795061` was in progress at 12:14. If it failed, tell the owner and fix that first, on its own branch.
3. Done: the branch is pushed and draft PR #70 is open.
4. B29a is running (above). When it reports: verify, gate (`GATE_TARGET=feature/meta_improvements scripts/merge-gate.sh "B29a:B29a-throughput"` from this worktree), apply the replacement texts its report gives (CLAUDE.md, `.claude/agents/orchestrator.md`, `docs/process/milestone-briefs.md`), record, push, and read CI on PR #70 (`gh run list --branch feature/meta_improvements`). Then B29b, then re-measure.

## Milestones (all authorised by the owner, items 57 to 59, unless marked)

| Order | Milestone | What | Size, model | Brief |
| --- | --- | --- | --- | --- |
| 1 | **B29a** | The review's items 1, 2, 3, 5 and 7a: Release tests, measured first; `merge-gate.sh` holds one `limited.sh` slot for the whole run; failed tests re-run alone and labelled FLAKY or FAILED; a test-time report; timing tests tagged, left out of `scripts/test.sh` (a `timing` mode) and run by the gate as their own step after the suite; the fuzz suite out of the gate and `test.sh`, in CI only | S, Sonnet 5.5 medium | **running**; [b29a-throughput.md](../../.claude/briefs/b29a-throughput.md), written from `.claude/briefs/b29-throughput.md` (keep deliverables 1, 4 and 7; drop 2 and 3, now deferred; move 5 to B29b; drop 6, done by B30) and add the gate's single slot, the re-run, the timing step and the fuzz move. Set BASE to this branch's head. |
| 2 | **B29b** | The review's items 4, 6 and 7: every wall-clock assert moved to a CPU-time budget in a timing collection, plus an architecture test that rejects new ones; the save fixture checks the bundle hash against `manifest.json`; a re-hash script; a PreToolUse hook that rejects long `sleep` polls | S, Sonnet 5.5 medium | to write from the review and the plan row |
| 3 | Re-measure | Time the suite, the gate and the slot queue after B29a and B29b, and compare with the review's section 5 table. Then ask the owner about the deferred items: B29's token policy (item 50 B) and stamp skip (item 50 C) | orchestrator, no agent | — |
| 4 | Long single tests | Only if a Game.Core class is still over 5 minutes in Release: fewer samples or split theory data, with no assertion weakened (the review's item 9) | S, Sonnet 5.5 medium | to write | 

### Points for B29a's brief

(Applied in the brief; kept for B29b's and the re-measure's context.)

- **Measure Release on a quiet machine.** Item 50 asked for the measurement with nothing else running. The world-next workstream (the chunk-speed series) may run at the same time from its own branch, so agree a quiet window with the owner or with that session before the Release-against-Debug run.
- **A FLAKY test does not fail the gate.** It is listed with the result. The orchestrator records it as a finding (docs/game/findings.md, or docs/nlp/findings.md for a core test), and a test that flakes twice goes to B29b's treatment (item 57).
- **Timing tests.** The two `TimingCollection`s (Game.Core.Tests and Nlp.Tests) hold 10 classes today; give them a trait so `test.sh` can filter them, and keep the `Speed=Slow` trait where it is.
- **The fuzz suite.** `Vocaterra.Nlp.Fuzzing.Tests` carries an assembly trait already (`Speed=Slow`). It leaves the gate and `test.sh` but stays in CI's `build-and-test`. Its oracles are never weakened.
- **CI.** It runs with `CI=true`, which multiplies the CPU budgets by 4 (B30), and has a 120-minute timeout. Say what Release does to CI's time.
- **The documents the agent may not edit.** The agent must not edit CLAUDE.md or `.claude/agents/`, so its report gives the replacement text and the orchestrator applies it:
  - CLAUDE.md: "The fuzz suite is a gate; never weaken an oracle to make it pass" becomes "The fuzz suite runs in CI; never weaken an oracle to make it pass". Its build-and-test line names Release.
  - `orchestrator.md`, line 105: "(the gate and CI always run the full suite)" no longer holds.
  - `docs/process/milestone-briefs.md`: the template's test rules (no fuzz suite or timing tests in agents' runs; `test.sh timing` when the milestone changes performance).

## Checks run

- None of the build, test or gate. This branch so far changes only documents and `.claude/agents/orchestrator.md`'s CI section.
- The doc-link test (`MarkdownLinkTests`) passed on 2026-11-18 at 12:20, with this handover in place. Run it again before each push.

## Coordination with the other workstream

At B29a's launch the world-next branch had not been cut and nothing else ran. B29a's Debug-against-Release measurement wants a quiet machine; if that workstream starts agents meanwhile, the agent notes the load in its report.

The world-next handover's chunk-speed series, B32, B33 and B34 run on their own branch from `main`. Both workstreams share the machine's limits: five agents and three `limited.sh` slots in all. B29a changes `limited.sh`, `merge-gate.sh` and `test.sh` only on this branch; other branches get them only after the owner merges this branch's pull request into `main`.
