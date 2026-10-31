# QA reports

The bug reports of the QA agent ([`.claude/agents/qa.md`](../../.claude/agents/qa.md)). Each run tests one area of the game (ui, language,
speech, world, progression, save, content, or all) and adds one file here: `<yyyy-mm-dd>-<area>.md`, with `-2`, `-3` for a second run of
the same area on the same day. A report is never overwritten. The orchestrator logs confirmed bugs in the findings logs
([game plan section 5](../game/IMPLEMENTATION_PLAN.md), [core §5b](../nlp/IMPLEMENTATION_PLAN.md)) and plans the fixes as milestones.

## Layout of a report

```markdown
# QA report: <area>, <yyyy-mm-dd>

Branch `<branch>`, commit `<short sha>`. Covered: <runs, inputs and CLI evaluations>.

## Bugs

### QA-<area>-1 (<crash | blocker | major | minor | cosmetic>, <core | game>)
- Reproduce: `<the shortest command that shows it>`
- Expected: <...>
- Actual: <...>
- Evidence: <file path or log line>; likely file: <path, if known>

## Observations
- <things that are not bugs but would trouble a learner>

## Not covered
- <what was meant to be tested and was not, and why>
```
