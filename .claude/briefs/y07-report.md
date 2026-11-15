# Y07 report: the opening's logic

(The agent's report, 2026-11-12, condensed by the orchestrator.)

Branch `Y07-opening`, HEAD `7bb6451f`; commits `6b4f5d0b` (the milestone), `3b37fe12` (merge of `feature/settlement` with Y09,
`fe56c91c`), `7bb6451f` (manifests and the fixture's hash regenerated). Worktree `/home/james/Dev/Vocaterra/.claude/worktrees/y07`.

## Built
- Objective `inn-room`: one step on `lodging_granted`, restore hook `lodging-given` (the save's lodging); it replaces `light-fire` in
  `session.json`. `light-fire.json`, its rows and `ui.light-fire.hud` deleted; `ui.spareTorch` gone.
- Hooks `spare-torch`, `point-own-camp`, `scan-fire-ring` retired (ObjectiveBook, VoxelSession, both schemas, the validator);
  `GiveSpareTorch`, `PointAtOwnCamp`, `WorldEvents.SpareTorch` removed. New completion hook `point-inn` on `camp.json`; the place is
  content (`session.json`'s optional `innBeacon`, value `inn`, label «posada»; required when the hook is used, checked against the
  scene); it skips when lodging is already given.
- Luna's line: `VoxelSession.Speech.cs` (`TakeSpoken`, `SpokenLine`, `InnLinePool`) queues it with an English hint;
  `SessionEndingFlow` plays it with `voice.Append` and prints `guide says: …`.
- The closing pool is Y06's `innkeeper_night` (variant 1 «Buenas noches. Tu cama está allí.»); `guide.lesson.end` gone. The ending's
  speaker is `VoxelSession.ClosingSpeaker` (the pool's NPC); `SessionEnding.Update` waits while `GuideSession.TalkOpen`;
  `SessionEndingFlow` appends, never cutting her room line. `SessionComplete` has no inn special case.
- Skip: `SkipToEnding` calls `GrantLodging()` and no longer places the mountain torch.
- Kept for Y08 and Y12: `firering.player`, the torch hook, `FireLit`, `ScanPlayerFireRing`, `fire_lit` (the sound beds read the latch;
  `RestoreStage` calls the scan).
- Texts: `ui.objectiveComplete` "Session complete: you have a room at the inn"; `ui.sleepHud` "…go to bed to start session N";
  `objective.inn-room.hud` "Go to the inn (la posada) and ask for a room"; `ui.sessionCompleteLine` "Good night. Your bed is over
  there."; PlaytestFlow and PlaytestEnd strings. Docs: lessons, README, running, playtest, content/README, the Core and Game READMEs.

## New Spanish
`guide.camp.inn.1` «¡Muy bien! Ahora ve a la posada.» (Very good! Now go to the inn.), Luna's voice, two files rendered, the two
`guide.lesson.end` files deleted; grammar 0 new findings. The identity baseline: +1, −1; end-to-end goldens and digests unmoved.

## Tests
After the merge of Y09: with models 11,304 passed, 3 skipped; without models 11,257 passed, 50 skipped. Build, format, validate, A2
gate clean. A content-loop `--tier full` before the merge failed once on `PipelineFuzzTests.OpenFallback…` ("generator no longer
reaches fallback:second-chosen"), not in either `test.sh full` run: the known coverage floor flake. New tests in
`OpeningAndEndingTests`, `ObjectiveBookTests`, `SessionSkipTests` (the beacon at the inn and Luna's line; none if the room was taken
first; the room completes `inn-room` and the session; the ending waits for the talk and is Rosa's; a save after the grant restores
`inn-room` without the event; retired hooks and a bad `innBeacon` fail validation; `light-fire` gone; the skip gives lodging). Changed:
`OpeningAndEndingTests`, `ObjectiveBookTests`, `NextSessionTests`, `SleepTests` (its `EndedFirstSession` lights the mountain ring
itself, for Y08 to remove), `SessionSkipTests`, `OpeningFrontendTests`, `FourthNpcTests`, `NpcRosterTests`, `NoLiteralTextTests`.

## Runs in the game (seed 42, `--dev`)
Day one from a new world (the CAMP script needed four more trunks: crafting the torch drops wood below 5): `beacon_set … posada at
280,37,229`; `guide says: ¡Muy bien! Ahora ve a la posada.`; «sí» at Rosa → `innkeeper.room`, `lodging_granted`; `session_complete`;
`beacon_set (bed.inn …)`; after `--leave`, `session complete: innkeeper says: Buenas noches. Tu cama está allí.`; the sleep; session 2.
`--skip-to-session 2` and a sleep in the bed with no `--lodging` works. Screenshots in `artifacts/y07/` of the y07 worktree.

## Scripted runs (own folders `y07-before`, `y07-after`; the first captures collided with Y09's and were redone)
r01, r20: only sounds, pictures, r20's save. r10: "the room at the inn is taken" (intended), voice timing. r21 to r24, r26, r27:
`lodging_granted` added, faces 808,248 → 808,236 (no mountain torch), saves and pictures (intended). r30: Luna's line and the beacon
to «posada» instead of the spare torch (intended). Not re-baselined. r10 exits 139 at exit, as before.

## Merge of feature/settlement (Y09)
Conflicts only in the two manifests and `save-v8.json`; regenerated (bundle hash `91f6bebc…`). The rest merged on its own.

## For Y08, Y12, Y13
The console still prints "session 1 ends at the fire"; the beacon to the bed shows the raw label `bed.inn` (the point has no label);
`SleepTests.EndedFirstSession` lights the mountain ring; `ui.control.talk` says "sleep at your fire"; `scripts/playtest/tester-readme.md`
line 64 describes the torch ending (outside its paths); running.md lines 95, 243, 364 describe the mountain fire; r10 never visits
the innkeeper, so it no longer reaches the ending. The `--sleep` aid's save shows no `last` sleep place (not investigated).
