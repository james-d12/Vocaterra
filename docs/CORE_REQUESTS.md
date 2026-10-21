# Requests to the language core

The game (this repository) takes the language core only as packages, and the core is the submodule `external/Vocaterra.NLP`. A missing
API, a bundle-format need or a core bug is written here as a row, not fixed by copying core source. The core's orchestrator reads this
file at the start of each session (its role file names this path), accepts each open row as a core milestone or answers in the row why
not, and marks a row done with the core commit once the change is merged and pushed. The game's orchestrator then bumps the submodule
(procedure in `.claude/agents/orchestrator.md`) and the row's status is kept as the record.

Columns: **id** (`CR-` and a number, never reused), **date** (the day the row was written), **request** (what is needed, in the core's
terms: a call, a field, a tool), **why** (the game milestone or the failure that needs it), **status** (`open`, `accepted` with the core
milestone, `done`, or `declined` with the core's reason), **core answer/commit** (the core commit that delivers it, or the answer when
declined).

| id | date | request | why | status | core answer/commit |
| --- | --- | --- | --- | --- | --- |
| CR-001 | 2026-10-21 | Repeat-count helper (`RepeatCounts.Next`), prompt text (`PromptSpec.Text`) and the content tool as a dotnet tool (`vocaterra-content`) | The M19c host re-implemented the repeat-count rule (it can overflow), had no line for what an agent says before evaluation, and the content script needed the core checkout's ContentBuilder | done | core `8ce91bc` (M19d, merged in `b87743e`); docs/API.md sections 4 and 5 |
