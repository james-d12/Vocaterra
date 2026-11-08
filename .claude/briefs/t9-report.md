T9 report (Haiku 5.5, 2026-11-08). Branch T9-r27-jitter (local) at 630544ba from bc881c58; worktree clean.
Cause: a script's Say step waits for PendingTurns == 0, checked after each frame's collect; the worker's outcome lands before or after the
next frame's collect at random (turn: ms; frame: ~32 ms), so r27-bread's autosave tick is 2460 or 2461 (1 in 9 base runs; inferred from
timings, the late collect not observed directly). Fix: TurnWorker.WaitForOutcomes(timeoutMs) waits on a monitor (pulsed per outcome) until
every submitted turn is queued; VoxelGame.Update calls it at frame end for screenshot runs with a script only (60 s cap); play and
playtest unchanged; no clock read (ArchitectureRulesTests). Test TurnWorkerTests.WaitForOutcomesReturnsWhenEverySubmittedTurnIsReady...
Proof: 20 runs on the fixed builds (10 beside three busy loops): all autosave 2460. Full set base vs final: all 27 same.
Checks: build -warnaserror clean; full suite 10329 passed, 3 skipped; one Game.Core full-run OutOfMemoryException in
GuideTurnTests.TheCursorTileIsNamedWhenTheGuideIsAskedWhatThisIs, the assembly rerun passed 2871/2871 (not explained); format clean;
validate 0 errors. Docs not updated (orchestrator's).
