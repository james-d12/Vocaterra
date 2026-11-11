# Y04, the inn building and the meadows: the agent's report (2026-11-10), as received

Saved by the orchestrator because the milestone is built but **not gated and not merged** (see the open handover,
`docs/handovers/2026-11-10-2004-settlement.md`). The work is on the local branch `Y04-inn-building` at `cfeea239`, in the worktree
`/home/james/Dev/Vocaterra/.claude/worktrees/y04` (clean, two commits: `369c08cf`, which lacks the attribution trailer, and `cfeea239`).
The brief is `.claude/briefs/y04-inn-building.md`.

## The agent's own checks
- The build with warnings as errors: no warning. The format check: clean.
- `content-loop.sh --grammar --speech --tier full` passed; the full suite gave 10,749 passed, 0 failed, 3 skipped (10,734 before).
- `content.sh validate` passes; the strict A2 lint gives no error and no warning.
- The end-to-end goldens and the stream digests did not move. The fuzz test and the timing test did not fail in its run.

## What it built
- **The inn:** `{"type":"house","site":"village","dx":-16,"dz":8,"width":9,"depth":7,"height":3,"facing":"north","name":"inn","label":"posada"}`,
  then `{"type":"stamp","stamp":"inn_rooms","site":"village","dx":-16,"dz":8,"facing":"north","name":"inn.rooms"}`. The stamp is section
  9.1's layers with `T` as `crate` and `B` as `bed`.
- **`house3`:** now `dx 9, dz 13`, 5 x 5, facing north, its name and its label `casa` kept. It is in the south-east of the levelled
  disc (its far corner at about 21.4 of 24), clear of every prop, the paving, the road, the fences and the well; dz 12 left only two
  free blocks to `house2`, dz 13 leaves three. A walk test reaches its door from the square. No lesson, line or recording changed.
- **`lamp.inn`** at `dx -10, dz 7`, in place of `lamp.house3`.
- **The meadows:** sites `meadow.north` (270, 180) and `meadow.south` (264, 258), with no radius (they level nothing);
  `trees.clearings` has both at radius 22, fade 12, keep 0.
- **Points:** `bed.inn` (278, 231) and `npc.innkeeper` (282, 227).
- **`needs.json`:** the sleep place `inn.bed` (point `bed.inn`, kind `bed`, radius 2, height 2, access `lodging`), so nobody can
  use it yet.

## Where the cells differ from section 9.1
- **The counter is a row of `crate`, not `table`.** A table within reach is a workbench (`StationInReach(Table)` opened the
  crafting panel inside the inn), and a `stall` block draws the market's red and white awning. The counter keeps its cells
  (column 5, rows 1 and 2).
- **Eight windows,** not the six that 9.1's text says (the stamp as drafted cuts eight).
- Unchanged: the door (280, 226), the talk cell, the keeper's cell, the partition's gap, the beds and the stand cells.

## The bed
- `ChunkMesher.AddBed` draws a mattress 0.5 high and a planks headboard (0.85 high) at the head; the head turns to lie against a
  wall beside the cell (north, west, east, then south). One block per bed.
- The picture: a plump pillow with a shadow under it, the sheet turned down, a blanket with a lit ridge over a dark crease.
- `BedArtTests` and `ChunkMesherBedTests` follow the new picture and mesh; `BedBlocksTests` is unchanged.

## The scene's numbers
- Build time 678 ms (the limit is 3 s).
- `SceneTests.GoldenDigest` is `213e63701b1d392ec308c222584056135b2e2975253420100152f925c09b20ca` (was `b699324f…`): the inn and
  its inside, the moved house, the trees cleared from both meadows (1,959 trees became 1,906).
- `VillageTests`' structure digest: `a03be5dc938ea1d6` became `1349a7e570ce5167`. Other pinned counts: planks gain the inn's 63,
  doors are 12, labels are 27.
- `Fixtures/save-v8.json` stopped loading (the world's digest) and was recorded again with the command in `docs/game/saves.md`;
  its tick is 4260, as before.

## Tests
- **New, `World/Generation/InnTests` (11 cases):** the door; the counter, the partition, the gap, the lintel, the beds and the
  windows; the keeper's cell enclosed; the keeper's cell as a valid standing cell; sight (clear from the talk cell to the keeper;
  blocked from the street, from the guest room west of the gap and from the gap's far side); a walk from the street through the
  door and the gap to the bed's stand cell, by path search and by an actual walk; three `casa` houses with `house3`'s door
  reachable; no trunk or leaves within 20 blocks of either meadow on seeds 42, 7, 1234 and 2025; the meadows' sites and clearings.
- **Changed:** `VillageTests` takes `Bed` back into the block-kinds check (`Bedroll` stays out until Y12) and accepts the
  chimney's cobble at the 3 x 3 layer's corners (the chimney lands at another corner in the house's new place); `SleepPlacesTests`,
  `NeedsDataTests` and `SleepEntrySaveTests` expect `camp.fire` and `inn.bed`; the pinned counts (`SceneTests`, `GameWordsTests`);
  a string in `UndergrowthTests`.
- **Not asserted:** the line from the bed's stand cell to the keeper (that diagonal passes through the partition's gap; the test
  uses the guest room west of the gap). No separate "`inn.bed` is not usable" test: `SleepPlacesTests` checks it is listed.

## The word `posada`
- In `lexicon/game.json` (feminine, singular and plural) and in the marked block of world words at the end of `cefr/a1.txt`, with
  its one-line reason: the model `content/README.md` gives. The lists are cumulative, so the A2 gate covers it. (The owner's
  answer named the A2 list; the orchestrator tells the owner.)
- The core's CLI reads «¿Dónde está la posada?» at `villager-open` as `ask_directions` with `place: posada`. In the game
  `--talk villager --say "¿Dónde está la posada?"` logs `place_asked (inn)` and `beacon_set (posada at 280,37,229)`.

## The scripted runs, before and after
- Same: r02 to r06, r11, r16, r18, r19, r22 to r24 (r10 and r17 differ only in order and in stdout's digest lines).
- Differ, all as expected: the scene's digest and the counts of trees, faces and vertices in stdout; the world digest in saves,
  `replay.jsonl` and the autosave; the pictures of r01, r07, r08, r09, r12, r14, r15, r20 (`session2`), r21 (`beacon`), r26, r27
  and r30, where the new trees and buildings are in view.
- Not this change: r01's `sounds.log` differs between two runs of one build (voice-end ticks, footstep gains). r20 and r30 still
  stop before their sleep.

## Screenshots
In `/home/james/Dev/Vocaterra/.claude/worktrees/settlement/artifacts/y04/` (git-ignored): `inn-outside.png`, `inn-counter.png`,
`inn-counter-down.png`, `inn-bed-west.png`, `inn-bed-east.png`, `inn-guest-room.png`, `house3.png`, `meadow-north.png`,
`meadow-south.png`, `village-from-bridge.png`. `--teleport` stands on the highest surface, a roof for a building, and `--walk-to`
stops about half a block short, so the interior shots were framed by hand. The orchestrator looked at six: `inn-bed-west.png`,
`meadow-north.png` and `house3.png` are usable; `inn-outside.png` (a view from the roof) and `inn-guest-room.png` (a wall) are not.

## For the milestones after it
- **Y05:** the sleep place is `inn.bed` at the point `bed.inn`; the stand cell is (278, 231), beside the west bed at (277, 231).
- **Y06:** the keeper's room is 2 x 2 (x 282 to 283, z 227 to 228) and an NPC stands there at feet y 38. A player can probably
  step up onto the one-block counter into it; a taller counter or a block that collides two high (as the fence does) would stop
  that, and it is a scene edit.
- **Y09:** `meadow.north` and `meadow.south` exist as sites with their clearings.
- **Documents it changed:** the `--beacon` help in `LaunchOptions.cs`, `src/Vocaterra.Game/README.md`, `docs/game/running.md`.
- Core gaps: none. Left undone: nothing in the brief.
