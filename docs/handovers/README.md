# Handovers

A handover is a snapshot of a session: where the work stopped and what comes next, written so that a fresh session can resume
without the conversation. The [orchestrator](../../.claude/agents/orchestrator.md#writing-a-handover) writes one before it ends a
session, pauses work or hands it on, and whenever the owner asks.

## Open or done

- **This folder holds only open handovers:** work with next steps still to do, at most one per workstream. To resume, list
  `docs/handovers/*.md` (leaving out this README) and read the one for your branch and workstream. Check its branch, commits, pending
  changes and next steps against git before acting; a handover does not authorise new work by itself.
- **`done/` holds the rest.** A handover moves there (`git mv`) when a later handover for the same workstream replaces it, or when
  its work is finished: merged, or dropped by the owner. The session that writes the replacement moves the old one in the same change,
  and repairs links and literal paths to it.
- A handover is never edited after it is written, except to repair a link after a move. Later progress goes in a new handover that
  links the previous one. Milestone statuses stay in the implementation plans, owner decisions in the decision registers, and bugs in
  [the QA reports](../qa-reports/README.md); a handover links them rather than copying them.

## Naming

`YYYY-MM-DD-HHMMZ-<slug>.md`: the creation time in UTC (`date -u +%Y-%m-%d-%H%MZ`; never a commit's date or an example), then a short
lowercase kebab-case slug that names the workstream, for example `2026-11-03-1430Z-village-decisions.md`. If the name is taken, add
`-2`, `-3`. The handovers written before 2026-11-03 have a known date but no recorded time, so they are named `YYYY-MM-DD-<slug>.md`;
several of them were moved out of the orchestrator, the game plan and a research report, each with a note saying where from.

## Index

**Open**

| Handover | Workstream | What it waits for |
| --- | --- | --- |
| [2026-11-03-1019Z-docs-structure](2026-11-03-1019Z-docs-structure.md) | The documentation layout | The owner's review and pull request of `feature/docs-structure` |
| [2026-11-02-after-followups](2026-11-02-after-followups.md) | The owner's decisions after the village follow-ups: M74, M75, V29, the study of spoken off-prompt lines, the known fuzz family | A new branch from `origin/main`; PR #28 (`feature/village-followups`) is merged |

**Done**

| Handover | Workstream | Closed |
| --- | --- | --- |
| [2026-11-03-0952Z-docs-organisation](done/2026-11-03-0952Z-docs-organisation.md) | The first documentation review and its proposed layout | Replaced by [2026-11-03-1019Z-docs-structure](2026-11-03-1019Z-docs-structure.md) |
| [2026-11-01-village-phase-b](done/2026-11-01-village-phase-b.md) | The village, phase B, its review and the follow-up queue | Replaced by 2026-11-02-after-followups; the follow-ups merged in PR #28 |
| [2026-10-26-engine-and-language-packs](done/2026-10-26-engine-and-language-packs.md) | D14 phase A: one engine assembly and language packs | `feature/language-packs` merged into `main` |
| [2026-10-23-experiment-paused](done/2026-10-23-experiment-paused.md) | The X series on `feature/experiment` | `feature/experiment` and `X06-review-deck` merged into `main` |
| [2026-10-22-s-series-done](done/2026-10-22-s-series-done.md) | The S series (the prototype scenario) | Merged into `main` |
| [2026-10-21-one-repository](done/2026-10-21-one-repository.md) | The game before the one repository; the prototype brief adopted | Done: one repository (D-game-7, R01) |

## Template

```markdown
# Handover: <readable name>

Created: <YYYY-MM-DDTHH:MMZ> (UTC)
Workstream: <task or milestone series>
Branch: <branch>; HEAD: <commit>
Worktree: <path>
Previous handover: <relative link, or none>

## Outcome
<What this session achieved and where work stopped.>

## Working tree and running work
- <Uncommitted changes, including pre-existing user work; commit, push and merge state.>
- <Agents, processes, worktrees or partial work that need attention; or none.>

## Completed
- <Finished work, with links to the plans, decisions, commits or evidence.>

## Unfinished and blocked
- <Remaining work, dependencies, known failures and owner decisions still needed.>
- <Separate authorised next work from proposals awaiting the owner.>

## Verification
- <Checks actually run, their result and evidence path; name any failures or skipped checks.>

## Next steps
1. <First concrete action, with enough context to resume safely.>
2. <Next action and any gate or owner decision it depends on.>
```
