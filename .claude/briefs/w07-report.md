# W07 report (Sonnet 5.5, 2026-11-06)

Branch `W07-station-view` (local), final commit 28efed22. Agent gate: build 0 warnings; format and validate clean; full 9736 passed,
3 skipped before its last test (the atlas-mip test, run alone): 9737 expected (+92 on 9645).

Built. Core `Activities/`: `StationScreen` (state, the view), `StationView`/`StationCell`, `InstructionStrip` (a model of its own from
`ProcedureView`, for W30), `StationPanel` (the IScreen, 10.8's keys), `IInstructionAudio` (Game `Sound/NpcInstructionAudio` through
`NpcVoice`/`BookSpeaker`, slow file for S; `NpcVoice.CanPlay`), `StationProps` (E lookup; `Scene.PropCells`/`PropAt` from
`SceneBuilder.BuildStamp`); `GameCommands.OpenStation`, `KeyBindings.World(atStation)` (E priority NPC, station, workbench); scripted
steps Station, Act, ProcHelp. Game: `UI/Station/StationLayer`, `StationLayout`, `InstructionStripLayer`, `InstructionStripLayout`;
VoxelGame wiring, `PlayPause.Station`; `--station`, `--act`, `--proc-help` (scripted-runs.md, running.md, architecture.md); bowl, spoon,
oven and a plain tile in the atlas (`BlockAtlasLayout.IconForKey`, `IItemIcons.DrawKey`); 16 `ui.station*` strings in content/locale.
content/game-v1 untouched. Tests: StationPanelTests, InstructionStripTests, StationPropsTests, KeyBindingsTests (E priority),
ScenarioScriptTests; Game: StationLayoutTests (six canvases), StationIconTests, StationLaunchOptionsTests.

Values chosen: no controls.json entry (keys in the footer and running.md); reach as the workbench's; the station's first procedure opens,
`teacherPresent` false (W08); rung 4 plays audio (`ProcedureView.PlayAudio`); muted or unrecorded rows show the Spanish card, act `Read`;
a fixture used `with` is a tool, otherwise a target; intermediates exist from the last `with` step before first use to their last use;
held list grouped by noun, 9 a page, empty hand on 0; a wrong act keeps the selection, a right one clears it; Done: Enter bakes again;
ReadyToCollect: Enter retries; per-noun read count `MarkRead` once per bake (the hover English needs only `WordRecord.Known`); the
mastered one-click bake is not offered.

Design vs code: hover English per noun (`NounSupport`); save `runs`, not `bakes`; ui `btn*` English, the Spanish button labels from
`GameWords.Buttons`.

Hooks: W08 `StationPanel.OnTalk` (`Action<StationTalk>`: Speak for V/Space, Type for W; null until W08). W30 `InstructionStrip` and
`InstructionStripLayer.Draw(batch, view, strip, InstructionStripLayout)`.

Screenshots (throwaway bundle copy, deleted): /tmp/claude-1000/w07/first.png, slip.png, help.png, done.png.
Scripted runs: the new atlas cells first moved 11 runs' far horizon (top mips average the whole atlas); interface-only cells are now
excluded from the mips (`IsUiOnly`, tested) and every run matches the baseline, but `r30-no-classes` `declined.png`, which differs
between two captures of the same build (as in W05). The locale edit landed during the baseline capture; r25, r26 and r30 re-taken.
Left undone: talking to Luz from the station (W08), the one-click bake offer, the recipe book (W30).
