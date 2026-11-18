# I-merge-fix report (finishing the merge of main into feature/inventory)

The agent's report, saved by the orchestrator (2026-11-18). Sonnet 5.5 at high effort; brief `i-merge-fix.md`. Merged into
`feature/inventory` as `510f4dcd` without the merge gate (the owner said to hold work as it finished).

Branch `I-merge-fix`, commits b5c23498 (the plan documents and content README name save-v10.json) and 09ce8547 (two tests meet the merged
state), worktree `.claude/worktrees/i-merge-fix`, clean.

## Review

No conflict markers remain; the first agent's resolutions needed no correction beyond these. Checked closely: saves.md, CLAUDE.md,
docs/game README, handovers README, open-items, decisions, findings, implementation-plan (4.14 is B, 4.15 is I), architecture, content
README, the Game.Core README, each a union with no decision reworded. Code spot-checked against both sides: SaveGame, SaveDocument,
SaveMigration, SaveService, GameData, VoxelSession, VoxelSimulation (digest: WorldStateDigest plus the active slots and the worn items),
VoxelRenderer (I09's gear and main's rewrite), LaunchOptions (`--teleport-to`, `--temperature`, `--equip`, `--move`; `--move` has no help
line on either side), the step kinds.

## Corrected

- content/README.md, the activities, village and content plans still named save-v9.json: now save-v10.json. `docs/nlp/changing-the-core.md`
  (core territory) still says save-v9.json.
- `SleepEntrySaveTests` (line 285): main expected "loads is 9"; the oldest readable here is 10.
- `AtmosphereSimulationTests` `PinnedFresh` and `PinnedAfterWalking` re-pinned (525b6cd5e087…, 78b1bc038629…): both sides moved them (main
  the world, I03b the 30 active slots); the shares could not be separated.

## Collisions

None: `SimulationEventKind` ends at MoveRefused = 21, `VoxelCommandKind` at SlotMove = 10, main added neither; D-game-34 and D-game-35
distinct; save version 10 with main's optional `passOpen`.

## Bundle

`scripts/content-loop.sh --grammar --speech --tier none` passed; both manifests already right; bundle hash 029e51f8… already in
save-v10.json; 0 of 1,680 speech files to render; 0 new grammar findings. No end-to-end or procedural golden moved (Nlp.Golden 17/17);
save-v10.json's state digest verifies.

## Checks

Build -warnaserror 0 warnings; format clean; validate passes; A2 gate 0 errors, 0 warnings. `scripts/test.sh full` before the two test
fixes: 13,388 passed, 3 failed, 9 skipped (two fixed above; the third, `ProceduralReliefRegionTests` "builds again in a few hundred
milliseconds", a timing assertion that failed at 5 s on a loaded machine and passes alone). After the fixes only those three classes were
rerun: 47/47. Expected full count 13,391 passed, 9 skipped. Per assembly: Game.Core 4,933, Game 1,585 (+6 skipped), Nlp 4,178 (+3
skipped), Fuzzing 141, Nlp.Es 2,317, Golden 17, Host 189, WorldPreview 31. Files outside the game's area: none.

## Left undone

The full rerun and the merge gate; the timing test may flake on a loaded machine.
