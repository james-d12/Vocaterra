# B45 report (near-first gate for far work; B43's fix 4; item 64)

Branch `B45-near-first-gate`, head `2f1f7ee5` (feature/world-next at ffc1baf1 merged). Agent's gate: build 0 warnings; full 12,718 passed,
1 failed (`NoLiteralTextTests`: the new console line needed an entry; added, the class passes 11/11; the full suite not re-run after);
timing 23/23; format clean; validate. B35, B38 and B42's far-view tests and `LodRingResidentTests` unchanged.

Gate: `src/Vocaterra.Game/Streaming/NearFirstGate.cs`, owned by `StreamedWorldHost.NearGate`; starts open (tests, `--pregen` unaffected);
`SceneDrawer` closes it when `PipelineCounters.Track` starts an episode (the start, or an eye move over 64 blocks) and opens it after a
frame's `Remesh` when every planned column within 8 chunks (128 blocks) is resident (`ChunkResidency.IsResidentAround`) and
`VoxelRenderer.IsMeshedAround(eye, 8)`; a 20 s timeout; `Settle` opens it. Default `All`: no ring plan or submit, no far tile of any level,
no stand-ins (`RequestStandIns` held), no drainage warm-up; afterwards B35's and B38's order. Knob `VOCATERRA_NEAR_GATE=off|fine|all`
(`fine` lets level 4 and up and stand-ins through; measured no gain, since coarse tiles and stand-ins are most of the start's far work; the
sky gap about 2 s longer at a start). `--stream-stats` prints a `near gate:` line and one `far coverage (episode n)` line per start and jump.

Copy off the game thread: `LodRing.CaptureResident` (game thread) takes chunk references only (about 160 lookups a footprint);
`CopyResident` (the ring's worker job) copies; `TakeResident` stays as the two together. `VoxelChunk.HasBeenWritten` (Game.Core) is a
fenced flag set in `RecordPristineNow` before an edit's first write; the worker reads it, copies, fences, reads again, and drops a marked
chunk (generated instead: same blocks), so stored sections stay byte-identical and edited chunks are never taken.

Tests: `NearFirstGateTests` (11): open when unarmed, open on done, timeout, re-arm on a jump, modes; the ring makes no plan or job while
held and runs after; the far view asks for no tile while held and fills after; a worker's copy equals generating; a worker copying while
the game thread edits every chunk keeps no edited chunk.

Measured (Release, High, `--world streamed --pass open --workers 8 --no-settle --stream-stats`, fresh seed and `VOCATERRA_LOD_DIR` per run,
base 8a16edf7 against B45, interleaved, 3 runs each, seeds 7721-7723, start then a 3 km teleport; load 3.6 to 7.5; medians, seconds):

| | base | B45 |
|---|---|---|
| Near view (64-block probe) | 5.2 | 3.3 |
| Gate held (128-block view done) | n/a | 4.6, 3.6, 5.1 |
| Generated/resident 50/90/100% | 5.4 / 7.4 / 8.2 | 3.0 / 4.9 / 5.3 |
| Meshed 50/90/100% | 7.9 / 10.9 / 11.5 | 5.8 / 8.8 / 9.4 |
| Whole view resident and meshed | 11.5 | 9.3 |
| Far covered (no uncovered leaf) | 11.1 | 12.6 |
| Far filled (every leaf on the card) | 26.2 | 26.0 |

Per seed whole view: 11.5/9.3, 8.2/12.9 (load 7.1, noise), 12.1/9.3. Jump: whole view 2.9 against 3.2 and 3.3 s; far covered 3.6 and 5.4 s,
filled 17.8 and 21.4 s. Game-thread streaming p99 6.8 to 7.5 ms against 9.6 to 12.4. The copy move alone shows no separable change (small
by design). origin/main (one run): near view 3.7 s, whole view 20.6 s; the jump's whole view 13.2 s.

Left: far work later by design (sky gap about 2 s longer at a start; inner-zone far leaves beyond 128 blocks show sky until the gate
opens); residency now ends about 5 s and meshing about 9 s, so the mesh budget and B43's items 2, 3, 5 are next (meshed trails resident by
about 4 s). Jump runs vary with load.

For architecture.md: "Since B45 far work waits for the near view. At a start and after an eye jump over 64 blocks, `SceneDrawer` closes
`StreamedWorldHost.NearGate` (`NearFirstGate`); while it is closed the `LodRing` makes no plan and submits no footprint, and the `FarView`
asks for no tile (levels 0 to 3, coarse tiles, B35's stand-ins) and no drainage warm-up, so the workers take only residency and mesh jobs.
The gate opens when every planned column within 128 blocks is resident (`ChunkResidency.IsResidentAround`) and every chunk in view within
128 blocks has its mesh (`VoxelRenderer.IsMeshedAround`), or after 20 s; a settled run opens it. Then far work resumes in B35's and B38's
order. The ring no longer copies resident chunks on the game thread: `LodRing.CaptureResident` (game thread) takes references to chunks
nobody has written, `CopyResident` (the ring's worker job) copies them and drops any chunk `VoxelChunk.HasBeenWritten` marks before or
during the copy." For running.md: "`--stream-stats` prints a `near gate:` line (the mode, how many times it closed, how long each hold
lasted, timeouts) and one `far coverage (episode n)` line per start or jump; `VOCATERRA_NEAR_GATE=off|fine|all` sets what the gate holds
(default `all`)."
