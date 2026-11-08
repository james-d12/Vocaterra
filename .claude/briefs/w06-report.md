# W06 report (Sonnet 5.5, 2026-11-06)

Branch `W06-procedure-runner` (local), final commit 13dee319. Agent gate: build 0 warnings; full 9645 passed, 3 skipped (+31); format and
validate clean; scripted runs skipped (no VoxelSession change; the save gained an optional entry only).

Built (Game.Core): `Activities/ProcedureProgress` (runs, completed, clean sittings, first-time steps, mastered, heard/read evidence per row;
`InstructionPresentation` Heard|Read; `InstructionEvidencePolicy`, decision 19); `PhraseProgress.RecordAct` (`ActQuality`), `RecordHelp`,
`StartAt`, `DuePhrases` skips `does` rows; `Activities/ProcedureRunner` (Start, Act, Collect, Repeat, Slower, Help, Leave, Abandon, View,
DrainEvents, Capture, Restore; drawn order, folding, reward and bonus, mastery, atomic exchange); the four events in `WorldEvents.All` (no
schema lists event names); `ProceduresSave` in `SystemsSave.Procedures` (absent when empty); `Session/GuideSession.Activities.cs` with
`BakeMastered` through `CraftingRules.Craft` (logs `item_crafted`). Tests: ProcedureRunnerTests (20), ProcedureSessionTests (11, save v1 to v6),
TestSupport/ProcedureRig.

Values chosen: decision 19, rungs/folding/mastery ignore presentation, heard and read counted apart (`RequirePresentation=true` the
alternative); at rung 1 any right unassisted act moves up, even after a slip; a rung-4 act earns nothing; the third slip's highlight counts as
help without a rung move; `anyOrder` steps follow the drawn order strictly (a group-mate acted early is out of order); order drawn with
`WorldRng.For(seed, "proc." + id, bake)`; mastery counts distinct play sessions (`PhraseProgress.Session`); a full bag leaves
`ReadyToCollect` with nothing consumed, `Collect` retries; a missing ingredient reopens its step without further credit; chunk and goal rows
start at rung 2; the one-click bake gives the base count, no bonus; `Abandon` added beside `Leave`.

Design vs code: save fields `runs`, `completed`, `cleanSittings`, `firstTimeSteps` (design: `bakes`, `cleanSittings`); no `OnEvent` (events
drained); `ActionTable` unused by the runner; the per-noun read count once per bake (10.10) left to W07.

API for W07 (on GuideSession): StartProcedure(procedureId, stationId, teacherPresent) -> StartOutcome; ActInProcedure(use, on, verb, presented)
-> ActResult(Outcome, Response, State, Missing, Made); CollectProcedure() -> ActResult; HelpInProcedure() -> ProcedureView;
Procedures.Repeat/Slower/Leave/Abandon; Procedures.View() -> ProcedureView (unit, row, rung, ShowEnglish, ShowText, highlight, steps done);
Procedures.Progress (RecordOf, EvidenceOf, IsMastered, Known); BakeMastered(procedureId) -> CraftResult.
