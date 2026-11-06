T8 done. Branch T8-r30-determinism (local), final commit 84d8be6f.

Differing region: declined.png, box x 384..428, y 556..565: the "10.0 ms" latency on the dialogue's developer note line
("ask_directions, understood, 100 %, 10.0 ms"), printed by DialogueModel.cs:639 from LastTurnMilliseconds, the turn worker's wall time
(MonotonicClock.Milliseconds passed to GuideSession.StartWorker in VoxelGame.cs).

Fix: LaunchOptions.TurnClock returns null for a scripted run (IsScreenshotRun && Script.Count > 0) and the monotonic clock otherwise;
VoxelGame passes it to StartWorker. A null clock gives zero timings, so scripted runs read 0.0 ms. Play unchanged. No mask or tolerance.

Test: AScriptedRunTimesItsTurnsWithNoClockSoItsLatencyLabelsDoNotMove (tests/Vocaterra.Game.Tests/Startup/VoxelLaunchOptionsTests.cs).

Captures: before the fix, three r30 captures, all pairs differ in declined.png only; after, three captures, all pairs same. Whole set
before vs after: 25 runs same, r30 differs in declined.png only, confined to the latency box.

Gate: build 0 warnings; test.sh full 9759 passed, 3 skipped (+1); format clean; content validate 0 errors.

Left: MonotonicClock is also used by CheckBattery ("check: picked ... ms") and SessionLog timings; not in r30, unchanged, could move a
future scripted run that uses checks. docs/game/scripted-runs.md gained a paragraph.
