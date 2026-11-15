# Y05a, indoor sleep places: the agent's report (2026-11-11), summarised as received

Branch `Y05a-indoor-sleep-places` (`a887a66b` the fix, `40836a89` README and import order), worktree `.claude/worktrees/y05a`.
Brief: `.claude/briefs/y05a-indoor-sleep-places.md`.

- **`Session/SleepWalk.cs`** (new): a bounded breadth-first walk with `VoxelWalker.FindPath`'s step rules (up one, level, down
  to three; the shaft clear).
- **"At the place":** `SleepPlaces.IsAt` = the radius and height test, then the walk: reachable on foot from the place's cell
  within `radius + 3` steps (`WalkStepsOf`). `UsableAt` and `LockedBedAt` use it; `IsWithinReach` unchanged. A walk rather than
  line of sight: a wall adds far more than 3 steps, and it handles doors and stairs.
- **Wake cell:** `NearestFreeGround` takes an optional reach set (a 12-step walk from the place); `WakeAtSleepPlace` searches it
  first with the same 2-to-5 band, and falls back to the straight search when the place has no standing room or nothing is found.
  The chest search is unchanged.
- **Cost:** the walk runs only for a place that passes the radius test: at most 5 steps for a bed (radius 2), 8 for the camp fire
  (radius 5); no cache, no clock, no random. Not timed.
- **Tests:** `Session/IndoorSleepPlacesTests.cs`, 4 tests on game-v1's inn (a death after a night at `inn.bed` wakes inside, the
  path staying inside and at most 12 steps; two south of the stand cell `IsWithinReach` is true but `AtSleepPlace` and
  `CurrentSleepPlace` are not; `AtLockedBed` outside false, inside true; a fire's wake cell as the old search picks). No test
  changed. Full suite 10,763 passed, 3 skipped. Build, format, validate clean.
- **In the game:** the brief's run wakes at (280, 38, 231), inside the guest room (before: 278, 38, 233, outside); the HUD offers
  sleep there. `artifacts/y05a/wake.png`.
- **Scripted runs:** `r21-skip`, `r26-death`, `r03-npcs` the same (`r03` order only); `r26-death` wakes at (152, 73, 106).
- **For later:** a place made in play (Y12) uses the same rules; a place with no standing room in its column (y-1 to y+2) keeps the
  radius-only test and the old wake search; a door block counts as solid for the walk only if it is solid (the inn's door is not);
  the 12 steps and `radius + 3` are constants in code, not in `needs.json`.
