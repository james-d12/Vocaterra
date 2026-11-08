# U01 report (Sonnet 5.5, 2026-11-05), saved by the orchestrator

Branch `U01-clean-defaults`, final agent commit `5fa9a175` (the first two commits were pushed before the owner's no-push rule, the rest
local). The orchestrator merged `feature/adaptive_design` (N07) into it: `running.md` kept both sides, the locale manifest regenerated.

- `--dev` (`LaunchOptions.Dev`): the debug overlay and the dialogue's developer note (`DialogueModel.ShowVerdictNotes`, now settable) start
  on only with it; F3 toggles both; the overlay's default is off (`VoxelDebugOverlay.PanelFor`). The "Heard: … · Expected: …" line stays.
- The tip box sits under the overlay when F3 is on (`HudLayout.TipBlock`, `FrameView.DebugOverlayBottom`).
- One speak hint: `tip.speak` already fired once (`TipFeed`); the dialogue footer drops "hold V: speak" once that tip has shown
  (`DialoguePanel.HintsFor(..., speakHintShown)`, keys `ui.dialogueKeyHintsShort`, `ui.dialogueButtonHintsShort`); the hold-to-speak sentence
  left `lesson.first-words.u1-say-hola.text` (English); the key stays in the F1 help.
- Tracker: `VoxelSession.ObjectiveTracker`, `TrackerView`, `Objective.AdvancedAt`, `HudLayout.TrackerRows`: one line per objective, three at
  most, the last advanced first, the rest "+n"; a lesson `do` step is left out while the lesson strip is up; the rest line keeps its place.
  The tracker moves under the tip block when they would touch, and hides in a conversation.
- Tests: `ObjectiveTrackerTests`, a `TenseLessonTests` case, `TopHudLayoutTests` (tracker, tip, overlay, needs, hotbar at both windows and
  three scales), `DevLaunchTests`. D-game-9 tests unedited and passing.
- Values chosen: F3 stays off in `--playtest` (the brief said any mode; the existing test kept); a single-line tracker wraps to 3 rows, a
  too-wide checklist shrinks to "Campamento: 2/4"; the recency order is not saved (a load falls back to the session's order); the short
  footer appears once the speak tip has been offered.
- Scripted runs: `scripted-runs.sh` passes `--dev` to every run except `r10-playtest`; r01 to r26 identical (8 order-only); r30 differs by
  a toast behind the ending card (its baseline lost its stdout: a timing artefact). No re-baseline.
- Counts: 9451 → 9466 passed, 3 skipped. Without models: one failure under load, `GeneratedLexiconTests.LoadingTheBundleIsFastEnough`
  (9 s against 3 s; passes alone). Build, format, validate, A2 lint clean.
