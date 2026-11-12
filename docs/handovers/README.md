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
- **The open handover of a running workstream is kept current** (owner, 2026-11-05): the orchestrator rewrites it in place, with its
  `Updated:` line, after every gate, agent launch or stop and owner decision, so a crash or a fresh session loses nothing. A handover
  in `done/` is never edited, except to repair a link after a move. Later progress goes in a new handover that
  links the previous one. Milestone statuses stay in the implementation plans, owner decisions in the decision registers, and bugs in
  [the QA reports](../qa-reports/README.md); a handover links them rather than copying them.

## Naming

`YYYY-MM-DD-HHMM-<slug>.md`, all lowercase kebab-case: the creation time in UTC (`date -u +%Y-%m-%d-%H%M`; never a commit's date or an
example), then a short slug that names the workstream, for example `2026-11-03-1430-village-decisions.md`. If the name is taken, add
`-2`, `-3`. The handovers written before 2026-11-03 have a known date but no recorded time, so they are named `YYYY-MM-DD-<slug>.md`;
several of them were moved out of the orchestrator, the game plan and a research report, each with a note saying where from.

## Index

**Open**

| Handover | Workstream | What it waits for |
| --- | --- | --- |
| [2026-11-10-1943-weather](2026-11-10-1943-weather.md) | The E series (day, night and weather) on `feature/weather` | PR #64 merged into `main` (2026-11-11); the handover is its session's to close |
| [2026-11-11-1850-settlement](2026-11-11-1850-settlement.md) | The inn, bought land and the campsite (the Y series, D-game-32) on `feature/settlement` | The owner's review and merge of the pull request (the series is done) and the pull request; then Y07 to Y13 and the pull request |
| [2026-11-09-2332-npc-lines](2026-11-09-2332-npc-lines.md) | Issue #53 (NPC lines not spoken) on `fix/issue-53-npc-lines` | The owner's check in the game and the merge of PR #59 |

The open pull request #51 (`feature/german`) carries its own state on its branch.

**Done**

| Handover | Workstream | Closed |
| --- | --- | --- |
| [2026-11-10-2004-settlement](done/2026-11-10-2004-settlement.md) | The Y series, Y00 to Y04, on `feature/settlement` | Picked up by a new session 2026-11-11 (the handover 2026-11-11-1850-settlement) |
| [2026-11-08-2233-ui-issues](done/2026-11-08-2233-ui-issues.md) | The UI issues #5, #6 and #7 (U05 to U08, M91) on `feature/ui-issues` | PR #47 merged into `main` 2026-11-09 |
| [2026-11-07-1345-bread-pilot](done/2026-11-07-1345-bread-pilot.md) | The bread pilot on `feature/bread-pilot`: W05 to W10, T8, W02 to W04, M80, V31, water merged in and the fixes M83, T9, M84, T10, W04b, H11, H12, M85 | PR #40 merged into `main` 2026-11-08; M86, launched at the end, did not land (core plan §5) |
| [2026-11-05-2031-adaptive-design](done/2026-11-05-2031-adaptive-design.md) | The adaptation on `feature/adaptive_design`: the needs, lessons as classes, the presentation track | Its work reached `main` through `feature/bread-pilot` (PR #40); the needs check answered 2026-11-07 |
| [2026-11-07-1846-water](done/2026-11-07-1846-water.md) | The H series (water) on `feature/water`, then H11 on `feature/bread-pilot` | `feature/water` merged into `main` (PR #45); H11 gated on `feature/bread-pilot` 2026-11-08 |
| [2026-11-06-2322-bread-pilot](done/2026-11-06-2322-bread-pilot.md) | The bread pilot, W08 and W10 merged, then W02, W03, M80 and the queue | Queue complete 2026-11-07; replaced by [2026-11-07-1345-bread-pilot](done/2026-11-07-1345-bread-pilot.md) |
| [2026-11-06-2035-bread-pilot](done/2026-11-06-2035-bread-pilot.md) | The bread pilot, W05 to W07b and T8 merged, W08 stopped partway | Session ended 2026-11-06; replaced by [2026-11-06-2322-bread-pilot](done/2026-11-06-2322-bread-pilot.md) |
| [2026-11-06-0928-bread-pilot](done/2026-11-06-0928-bread-pilot.md) | The bread pilot's machinery, W05 to W07, on `feature/bread-pilot` | Session ended 2026-11-06; replaced by [2026-11-06-2035-bread-pilot](done/2026-11-06-2035-bread-pilot.md) |
| [2026-11-03-2114-adaptation-design](done/2026-11-03-2114-adaptation-design.md) | The Adaptation Design Document, revision 3 (PR #30) | PR #30 merged; replaced by [2026-11-05-2031-adaptive-design](done/2026-11-05-2031-adaptive-design.md) |
| [2026-11-03-1133-docs-cleanup](done/2026-11-03-1133-docs-cleanup.md) | The documentation cleanup | `docs_cleanup` merged into `main` |
| [2026-11-02-after-followups](done/2026-11-02-after-followups.md) | The owner's decisions after the village follow-ups | M74, M75, V29, M78 and the study merged into `feature/adaptive_design` |
| [2026-11-03-1019-docs-structure](done/2026-11-03-1019-docs-structure.md) | The documentation layout, before the READMEs and names | Replaced by [2026-11-03-1133-docs-cleanup](done/2026-11-03-1133-docs-cleanup.md) |
| [2026-11-07-1504-water](done/2026-11-07-1504-water.md) | The H series (water), H01 running | Replaced by [2026-11-07-1846-water](done/2026-11-07-1846-water.md) |
| [2026-11-03-0952-docs-organisation](done/2026-11-03-0952-docs-organisation.md) | The first documentation review and its proposed layout | Replaced by [2026-11-03-1019-docs-structure](done/2026-11-03-1019-docs-structure.md) |
| [2026-11-01-village-phase-b](done/2026-11-01-village-phase-b.md) | The village, phase B, its review and the follow-up queue | Replaced by 2026-11-02-after-followups; the follow-ups merged in PR #28 |
| [2026-10-26-engine-and-language-packs](done/2026-10-26-engine-and-language-packs.md) | D14 phase A: one engine assembly and language packs | `feature/language-packs` merged into `main` |
| [2026-10-23-experiment-paused](done/2026-10-23-experiment-paused.md) | The X series on `feature/experiment` | `feature/experiment` and `X06-review-deck` merged into `main` |
| [2026-10-22-s-series-done](done/2026-10-22-s-series-done.md) | The S series (the prototype scenario) | Merged into `main` |
| [2026-10-21-one-repository](done/2026-10-21-one-repository.md) | The game before the one repository; the prototype brief adopted | Done: one repository (D-game-7, R01) |

## Template

```markdown
# Handover: <readable name>

Created: <YYYY-MM-DDTHH:MMZ> (UTC)
Updated: <YYYY-MM-DDTHH:MMZ> (UTC), after <the gate, launch or decision>
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
