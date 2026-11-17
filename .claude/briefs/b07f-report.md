B07f report (agent a655cb5431daed650, 2026-11-17 about 04:35 UTC), first round

Branch `B07f-far-store-edits`, head b95f0524 (dfea1903, 4208c87f, 5e1a0547, b95f0524 on 30f104cb), worktree clean, not pushed.
Gate: build 0 warnings; full suite on 5e1a0547 12,385 passed, 0 failed, 3 skipped; the later host test passes alone; format clean; content
validate OK. No change to generation, goldens, the state digest or the save format; `LodFormat` 3 (LOD folders start again).
C1: `LodWorld.IngestStack` and `IngestChunk` hold one of 64 striped footprint locks for the whole read, light and write; a two-thread
test over 90 rounds with a slow disk (fails at round 36 without the lock). C2: the host queues every chunk the loaded save changed
(`VoxelGrid.ChangedChunkCoords()`) at creation; an edit journal `edits.txt` in the LOD folder, written through the writer queue before each
ingest; on a load the host drops footprints holding edits the world lacks (`LodWorld.ResetFootprint`, `LodStore.Drop`) and the ring remakes
them; tests for a save's edit at start, another save of the seed, quitting without saving. C3: `Keep` and the byte count under the key's
stripe lock, `Trim` and the corrupt-slot drop too; tests. C6, C7 (FarView, small): results carry a settings version, stale ones refused,
`Clear` and a reshape bump it; a dropped pending mesh disposes its (unwanted) tile; tests fail on the old code. C10: `FileLodPersistence`
shutdown bounded at 8 s, the writer drains, syncs and closes its own files; `StreamedWorldHost.Dispose` one bounded wait; tests.
Beyond the review: an edit showed only at level 0 (under about 560 blocks) because `Upgrade` only remade levels where all eight children
were real; `IngestChunk` climbs with `Upgrade(fill: true)` (parents from the model's children, kept partly real); tests for levels 1 to 4
and a build 600 blocks out changing the far tiles.
Game check: four 5x5 houses and a 60x5 wall, saved, reloaded, viewed from 1.0 to 1.8 km: not visible by eye (slivers); the LOD folder
holds planks at levels 0 to 4 (260, 84, 23, 6, 3), the empty save none. Pictures artifacts/b-series/b07f/. Picture 2 (from 1,400 m, y 247)
shows a large pale void under the far land on its base (no B07e).
Sent back (about 04:40) to merge feature/biomes (B07e; one test-file conflict), re-take the high view settled (the void), and a tall bright
tower seen from about 1 km in both saves.
