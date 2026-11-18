# B08b report (Sonnet 5.5, medium; 2026-11-15)

Branch `B08b-pass-lock`, head `7e6af56b` (main work c6c6a4ed). Agent's suite: 11,895 passed, 0 failed, 3 skipped (16 new); build 0
warnings; format clean; validate passes; the content lint line not run (two English locale labels only).

- Data: a top-level `pass` section in world.json (`requiredLessons`: all 12 of today's lessons; `rockfall`: the prop id; `edge`:
  `paso.edge`), with schema; `World/Pass/PassData.cs`, `PassValidator.cs` in `GameDataValidator` (refuses an unknown or repeated lesson,
  an unknown prop or site); `GameData.Pass` lazy.
- Rule: `World/Pass/PassState.cs`: `ShouldBeOpen(data, isDone, cheat)` pure; `Update` opens once and raises `Opened`; `Restore(open)`
  without the event. `VoxelSession.Pass.cs`: `Pass`, `PassCheat`; `Step` asks once a tick; setting the cheat opens at once.
- Removal: every cell of `Scene.PropCells[rockfall]` set to Air with `SetBlock` (the stamp stands above the ground); saved as changed
  chunks; world event `pass_opened` (detail `lessons` or `cheat`). Save: optional `PassOpen`, written only when true.
- Setting: `SettingId.PassCheat`, a toggle under a new "Cheats" heading, "Open the pass (cheat)", saved with the player's settings
  (written only when true); `VoxelGame` applies it at start and on each menu change; launch flag `--pass open`.
- For B07: `session.Pass.IsOpen` (or `Pass.Opened`) and `session.Data.Pass.Edge` (`paso.edge`) to lift the edge wall.
- Tests: `PassLockTests` (14), `NoLessonIdsInSourceTests` (2), one `--pass open` launch test.
- Pins: bundle 375729f3... to 290a4119...; save-v9.json bundle hash and state digest (d543beb4... to ae875482...); the locale manifest
  re-hashed. `SceneTests.GoldenDigest` and the end-to-end goldens unchanged.
- Shots for the orchestrator (later, with windows): the rockfall from the road at about x 335 to 340, z 208, looking east to (355, 200),
  closed; the same with `--pass open`; the road at about (380, 208), y 58, looking east, with `--pass open`.
- Known-not-fixed: `LessonRunner.Classes.cs` names two lesson ids (`first-words`, `first-class`) for the old-save split; the no-lesson-id
  test skips that file and says so. The edge still stops the body when open (B07). No sound or message when it opens.
- Outside the area: `NoLiteralTextTests` (an entry for `PassValidator`); `PassTests` helpers made internal; small edits to `GameData`,
  `GameDataValidator`, `WorldEvent`, `SaveGame`, `SaveDocument` and the Core menu files; world.json and its schema (one new section
  each, one sentence in `about`).
