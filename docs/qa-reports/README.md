# QA reports

Bug reports from QA passes and code reviews. A report is a record: written once, never overwritten, and its bugs are tracked in the
triage table below. Confirmed bugs also go into the findings logs ([game plan section 5](../game/implementation-plan.md),
[core §5b](../nlp/implementation-plan.md)) and are fixed as milestones.

- **QA passes** by the QA agent ([`.claude/agents/qa.md`](../../.claude/agents/qa.md)) test one area of the game (ui, language, speech,
  world, progression, save, content, or all) and write `<yyyy-mm-dd>-<area>.md`, with `-2`, `-3` for another run of the same area on
  the same day.
- **Reviews** of a branch or pull request by the orchestrator write `<yyyy-mm-dd>-review-<branch>.md`.
- **Bug ids** are `QA-<report>-<n>`, where `<report>` is the report's file name without `.md` (`QA-2026-11-05-ui-1`), so an id is
  unique across reports. The reports of 2026-11-01 predate this and use `QA-<area>-<n>` (unique so far) and, in the review, `R<n>`.
- **The triage table** is the orchestrator's: it adds one row per bug when it reads a report, and sets the status when a milestone
  fixes it. Status is `open`, `fixed` (with the milestone), `parked`, `not a bug` or `won't fix`.

## Triage

| Bug | Report | Severity, owner | Summary | Status |
| --- | --- | --- | --- | --- |
| QA-language-1 | [2026-11-01-language](2026-11-01-language.md) | major, core and game | The CLI and the host give different verdicts | not a bug: the host takes out greetings and the listener's name first (V19) |
| QA-language-2 | [2026-11-01-language](2026-11-01-language.md) | major, core | A correct learner name is rejected | fixed (M70) |
| QA-language-3 | [2026-11-01-language](2026-11-01-language.md) | major, core | A plural subject with a singular `costar` is praised | fixed (M66) |
| QA-language-4 | [2026-11-01-language](2026-11-01-language.md) | major, core | A wrong `gustar` form is praised | fixed (M70) |
| QA-language-5 | [2026-11-01-language](2026-11-01-language.md) | major, core | A wrong tense against a past question passes | parked (M67b): at a lesson step the game already makes it a miss with a tense retry |
| QA-language-6 | [2026-11-01-language](2026-11-01-language.md) | major, core and game | Ungrammatical recasts built from the wrong word | fixed (M70) |
| QA-language-7 | [2026-11-01-language](2026-11-01-language.md) | major, core | A dropped `ñ` is a false alarm | fixed (M70) |
| QA-language-8 | [2026-11-01-language](2026-11-01-language.md) | major, core | `cuanto son todo?` gets the wrong intent and praise | fixed (V20) |
| QA-language-9 | [2026-11-01-language](2026-11-01-language.md) | major, core | An unknown wrong form is praised; two contradictory replies | fixed (M70 the grading, V21 the replies) |
| QA-language-10 | [2026-11-01-language](2026-11-01-language.md) | minor, core | A wrong person or number passes | `cuántos cuestan todo` fixed (M69); `sí fue al río` parked with QA-language-5 (M67b) |
| QA-progression-1 | [2026-11-01-progression](2026-11-01-progression.md) | major, game | A load moves the session on | fixed (V25) |
| QA-progression-2 | [2026-11-01-progression](2026-11-01-progression.md) | major, game | A load moves the session on (a skipped session) | fixed (V25) |
| QA-progression-3 | [2026-11-01-progression](2026-11-01-progression.md) | minor, game | Contradictory exchange lines | fixed (V21) |
| QA-progression-4 | [2026-11-01-progression](2026-11-01-progression.md) | minor, game | Contradictory exchange lines (the held order) | fixed (V21) |
| R1 | [2026-11-01-review-feature-village](2026-11-01-review-feature-village.md) | must fix, core | Correct Spanish with a coordinated subject is marked wrong | fixed (M64, V19) |
| R2 | [2026-11-01-review-feature-village](2026-11-01-review-feature-village.md) | must fix, docs | ADR 0009 misses the branch's public surface | fixed (M64) |
| R3 | [2026-11-01-review-feature-village](2026-11-01-review-feature-village.md) | must fix, CI | PR #20's checks could not be read | open at the review: the owner reports CI |
| R4 | [2026-11-01-review-feature-village](2026-11-01-review-feature-village.md) | should fix, game | The listener's name after a greeting other than `hola` | fixed (V19) |
| R5, R6 | [2026-11-01-review-feature-village](2026-11-01-review-feature-village.md) | should fix, core | An amount before a singular `ser`; `es cerca del mediodía` | fixed (M64) |
| R7 to R10 | [2026-11-01-review-feature-village](2026-11-01-review-feature-village.md) | should fix, game | The market's misses: no item, full bag, the cap of 5, zero | fixed (V19) |
| QA-2026-11-06-ui-1 | [2026-11-06-ui](2026-11-06-ui.md) | major, game | The expanded dialogue panel cuts the oldest lines with no hint (only the newest rows that fit) | confirmed; to fix in U02b |
| QA-2026-11-06-ui-2 | [2026-11-06-ui](2026-11-06-ui.md) | major, game | At 800x600 and 150 % text the collapsed panel shows only the coaching line, not the learner's line or the reply | confirmed (screenshot); to fix in U02b |
| QA-2026-11-06-ui-3 | [2026-11-06-ui](2026-11-06-ui.md) | major, game (core unverified) | A turn not understood shows the English "Good. The shopkeeper understood you." | confirmed (screenshot); U02b finds the cause, a core cause becomes a core milestone |
| QA-2026-11-06-ui-4 | [2026-11-06-ui](2026-11-06-ui.md) | minor, game | A typed turn not understood gets the off-prompt coaching line ("The NPC understood, but…") | confirmed (screenshot); to fix in U02b |
| QA-2026-11-06-ui-5 | [2026-11-06-ui](2026-11-06-ui.md) | minor, game | At 800x600 and 150 % text the lesson heading is cut before the step total and kind | to fix in U02b |

## Layout of a QA pass report

```markdown
# QA report: <area>, <yyyy-mm-dd>

Branch `<branch>`, commit `<short sha>`. Covered: <runs, inputs and CLI evaluations>.

## Bugs

### QA-<report>-1 (<crash | blocker | major | minor | cosmetic>, <core | game>)
- Reproduce: `<the shortest command that shows it>`
- Expected: <...>
- Actual: <...>
- Evidence: <file path or log line>; likely file: <path, if known>

## Observations
- <things that are not bugs but would trouble a learner>

## Not covered
- <what was meant to be tested and was not, and why>
```
