# Y08 report: the mountain, the strings, the rebaseline (and two-block beds, and Y07's leftovers)

(The agent's report, 2026-11-12, condensed by the orchestrator.)

Branch `Y08-mountain`, HEAD `b470d3ec` (six Y08 commits and a merge of `feature/settlement` with Y10); worktree
`/home/james/Dev/Vocaterra/.claude/worktrees/y08`.

## Merge of feature/settlement (Y10)
Manifests regenerated (bundle `f43f27fc…`); `ui.json` and `LocaleText.cs` kept both sides (Y08's `ended*` keys and death lines, Y10's
`plot*`); `save-v8.json` kept Y08's re-recorded fixture with the new hash. Digests pinned again once, after the merge. Y09's and Y10's
seed-42 coordinates (261,160; `--mark-plot 266,165`) still pass: the scene edit does not touch the north meadow.

## Removed and changed
- Scene: the tent at `camp.ledge`, `chest.player`, its crate and `firering.player` gone; the `tent` stamp kept for Y12; `camp.player`
  keeps its site, blend and spawn, loses «mi campamento»; `bed.inn` labelled «cama». `chest.player` was used by nothing (the death's
  chest is made where the player falls).
- Counter: a new block `BlockId.Counter = 56`, solid and `Tall` like a fence, a plank top over crate sides; not climbable (StepHeight is
  one block, so a slab or crate would be), not `table`. A test: a crate is climbed, the counter is not.
- Beds two cells long: `Bed` is the head, `BedFoot = 54` new; `Bedroll` head, `BedrollFoot = 55`. `ChunkMesher.AddBed` finds the foot
  from the neighbour; the mattress runs over both cells with no seam: top 0.55, headboard 1.0 down to the floor with a 3D pillow, footboard
  0.7, a plank frame under the mattress. The bedroll: a mat 0.2 high with a rolled pillow. A lone head is a short bed with a footboard; a
  lone foot a half bed. Three new atlas cells (`BedFoot`, `BedSide`, `BedrollFoot`). Beds still not solid. The inn's row 5 `" BF   FB "`;
  `bed.inn` moved to 279,231 (the west bed's foot end), labelled «cama».
- Sleep places: `camp.fire` gone; `guide.fire` (point `firering`, fire, always, radius 5, height 6) new, and `death.wakeAt` names it. A
  death before any sleep wakes two blocks from Luna's ring.
- Code: `PlayerFireRing`, `_playerFireRing`, `HasPlayerFireRing`, `AtPlayerFireRing`, `ScanPlayerFireRing` removed. The fire's ambient
  bed (`AmbientPlaces.Fire`) is read from the prop `firering` and plays at Luna's ring once `FireLit`. Kept for Y12: `FireLit`, `fire_lit`
  (now with detail `firering`), `LightFire`, `RingHasTorch(IReadOnlyList<BlockPos>)` (any ring's stones), `AtRing(ring, cell)`.
- Strings: the console's sleep line names the place (`SleepOffer.EndedWhere`; `ui.endedAtInn`, `ui.endedAtFire`, `ui.endedWhereFell`,
  `ui.endedAnywhere`); `tips.json`, `controls.json`, `ui.json` (death lines, `ui.control.talk`, `hud.camp`); help for `--sleep` and
  `--skip-to-session`; docs (running, lessons, playtest, saves, scripted-runs, architecture, `scripts/playtest/tester-readme.md`).
- Rosa after lodging given elsewhere: `NpcService.FollowLodging()` moves her to `given`, from `GrantLodging()` (the skip, `--lodging`)
  and `RestoreStage()` (a load, a new session); she greets «Buenas noches.» with no offer.

## New Spanish
`hud.camp` (the death screen, `words.json`): «Estás en tu campamento.» → «Estás de vuelta.» (You are back.). Nothing else; grammar 0 new
findings; goldens and stream digests unmoved.

## Digests pinned again
Scene `GoldenDigest` `213e6370…` → `ab403535…` (the bare ledge, the beds, the counter); `VillageStructureDigest` `1349a7e5…` →
`44d687ce…`; `CampsStructureDigest` ledge half `ed42e2a6…` → `98bb81aa…`; `AtmosphereSimulationTests` `8213aec4…` and `c0222a77…`;
`save-v8.json` re-recorded with a new command (the camp route, then Luna's fire at 176,188; tick 4258; `saves.md` updated), so an
`ObjectiveBookTests` test expects wood 3. Block table: 57 kinds.

## Tests
With models 11,347 passed, 3 skipped; without models 11,300 passed, 50 skipped; build, format, validate, A2 lint clean. Added
`Session/MountainRemovedTests` (9), rewritten `ChunkMesherBedTests` and `BedArtTests`, the counter test in `BedBlocksTests`, `InnTests`,
`AmbientBedsTests`; changed the tests naming the mountain ring and `camp.fire`, the label and ring counts (27 → 26, 3 → 2).

## Runs in the game
Screenshots (`artifacts/y08-check/` in the settlement worktree): the bare ledge, Luna's fire, the counter, the guest room from the door
and close. A death before any sleep wakes at 174,43,188 beside Luna's fire; a sleep there with no lodging prints `session 1 ends at the
fire`.

## Scripted runs (baseline `feature/settlement` at `c2c6acc8`, built in a throwaway worktree; after = the branch with Y10 merged)
Same on order: r02 to r06, r11, r16 to r19, r22 to r24. Intended: the scene digest line (r07, r08, r12, r14, r15, r21, r25, r31), r27's
mesh counts, r09 and r21's save digests; r01, r10, r20, r30 the extra trunks, `madera 5/5`, `fire_lit (firering)`, the checklist and
Luna's line; r10 now asks Rosa and reaches the ending and post-check (exit 0, was 2); r20 sleeps in the bed (`ends at the inn`, `last`
`inn.bed`); r30 sleeps at Luna's fire and its second sleep says `wherever the player stood`; r26 wakes at Luna's fire (the walk to the
chest 1,355 ticks, was 94). Re-baselined: `docs/game/scripted-runs.md` (a Y08 paragraph, the r10, r20, r30 rows); the repository keeps no
capture files.

## For Y12 and Y13
Y12 can place the `tent` stamp and the bedroll (`Bedroll` then `BedrollFoot`) and point the torch hook and `RingHasTorch` at its ring;
the fire bed reads the static layout, so a placed ring needs its own point. `--sleep` away from any place records no `last` by design
(an existing test). The guest room from the door shows one bed's end (narrow room); the close shot is clearer.
