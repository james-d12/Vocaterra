# The codebase so far: a review, with performance and graphics fidelity (2026-11-16)

Asked for by the owner ("can we get an agent of opus 5.5 high to review the codebase so far?", then "also make it review performance and
the graphics fidelity"). Done by an Opus 5.5 agent at high effort, read-only, on `feature/biomes` at `30f104cb` (the c7124db3..30f104cb
diff first, then the code round it, the reports in `.claude/briefs/` and the pictures in `artifacts/b-series/`). Nothing was built or
run; every finding was checked against the code unless marked a suspicion or an estimate. It builds on the day's two earlier reviews
without repeating them: RR, [the rendering and distance review](2026-11-16-rendering-and-distance-review.md), and FR, the far view's review
(`.claude/briefs/b07c-review.md`).

**Verdict.** Disciplined and in good health. Generation is genuinely pure (no float, double, clock or Random anywhere under
`Game.Core/World`), with many order- and thread-independence tests; the streaming design is sound (workers touch only the pure source;
installing or evicting a chunk is not an edit). The weak spot is the far view's store (`World/Lod`): its edit layer and concurrency. The
performance architecture still meshes chunks on the game thread. For looks, the biggest gaps are light and colour, not geometry.

## 1. Correctness

- **C1 (major). `LodWorld.IngestChunk` relights from a stale snapshot and overwrites real sections** (`World/Lod/LodStore.cs:406,
  432-452`): it reads the stack without a lock and writes the rebuilt sections back as Real with mask 0xFF; `LodRing.Ingest`
  (`LodRing.cs:81-94, 138-155`) and `LodEdits` (`StreamedWorldHost.cs:297`) both run on the Ring lane, so with two workers an edit's
  relight can write model blocks over the ring's real sections, persisted, and the ring then treats the stack as finished for good. Fix:
  serialise work per footprint, or let the light pass change only light bytes under the stripe lock; a two-thread race test.
- **C2 (major). The far store's edit layer is not tied to the save.** (a) Edits loaded from a save never reach the far view: `SaveGame.Restore`
  runs (`Program.cs:97`) before the host exists (`SceneDrawer.cs:140`), `LodEdits` only hears later installs and changes, and changed
  chunks are never evicted; it works today only because the folder on disk still holds them. (b) The folder is keyed by seed, generator and
  data hash (`SceneDrawer.cs:111`), not by save: another save or a new game with the same seed shows the other timeline's buildings for good.
  Fix (M): queue `grid.ChangedChunks()` at host creation; generator sections per world, edit overlays per save; on load drop edited sections
  the save does not hold.
- **C3 (major). The store's model-byte count races with `Trim`** (`LodStore.cs:178-189, 207-229`), so the 192 MB cap stops holding; a
  corrupt slot's bytes are not subtracted (:115-118).
- Minor: C4 a jump clears `_inFlight` while old packages fly (`ChunkResidency.cs:161, 327`); C5 `ApplyPreset` replaces the residency with
  jobs in flight (`StreamedWorldHost.cs:162-166`); C6 `FarView.DropUnwantedPending` discards a rebuilt mesh without re-marking it dirty
  (`FarView.cs:527`); C7 a job with old settings can be accepted after `Reconfigure` (`FarView.cs:247`); C8 the resident cap does not evict as
  documented (`QualityPreset.cs:22`, `ChunkResidency.cs:375-378`); C9 `ValleyUnderground.Lay` runs `Parallel.For` on up to 8 threads
  (`ValleyUnderground.cs:96`), against the two-worker rule; C10 shutdown can hang on the disk writer (`FileLodPersistence.cs:173-179`,
  `StreamedWorldHost.cs:278-285`); C11 `VoxelGrid.HighestBlock` installs air chunks on the game thread on a streamed grid
  (`VoxelGrid.cs:253-277`; callers `LandRules.cs:200`, `CampPlan.cs:233`). Suspicion: simulation reads outside the resident set generate
  chunks synchronously.
- Saves and the state digest are sound (exactly the changed chunks, bounded and streamed alike).

## 2. Architecture

- **A1.** The purity guards have gaps: `ProceduralWorldPurityTests.PureFolders` leaves out `World/Generation` outside `Procedural/`,
  `World/Layout`, `World/Pass` and `Voxel`; `ArchitectureRulesTests` does not ban `Parallel.`, threads, `Task.Run`, file IO or environment
  reads in Game.Core. Clean today, unguarded.
- **A2.** Dead code: `World/Streaming/ChunkStreamer.cs` and `VoxelGrid.ResidentCoordinates` (tests only); biomes-plan.md names a
  `ChunkPrefetcher` that no longer exists.
- **A3.** Large files: `GuideSession` 1,676 lines, `VoxelGame` 1,541, `LaunchOptions` 1,535, `SceneBuilder` 1,446, `ChunkMesher` 1,255,
  `VoxelRenderer` 1,189 (split a remesh scheduler out of it first).
- The core/game boundary and the engine boundary hold; the bounded and streamed worlds share the region copy without duplication.

## 3. Tests

- **T1.** Gaps in risky code: the two ingests under concurrency, save-loaded edits reaching the far store, reconfiguring with jobs in
  flight, a persistence read during a write, `Dispose` with jobs in flight; most residency tests use 0 workers.
- **T2.** Timing tests with bounds hundreds of times the measured cost (`ProceduralChunkTimeTests.cs:79-82`, `StreamedWorldTimeTests.cs:67-69`)
  no longer guard anything.
- **T3.** The fuzz suite's generator holds 54 re-roll rules (`SentenceRealizer.cs`, 2,348 lines); M97's O5 fix is one more (the generator
  avoids an input the analyzer misreads). No gate tracks the realisation rate or the skip reasons, so the suite can lose power silently.
  Fix (S): pin realisation rates per pattern family and print counts per skip reason.

## 4. Performance across the game

Context (reports): p50 about 2 to 3 ms, p95 8 to 10, p99 11 to 20; a chunk mesh about 1.2 ms; a far tile about 33 ms of worker time.

- **P1 (major). Chunk meshing on the game thread** (`VoxelRenderer.Remesh` :546-640, `ChunkMesher.Build` :375): 3 to 5 meshes a frame
  within a 4 to 6 ms budget; a High view of up to 24k meshes fills over minutes. The mesher already reads through an 18^3 neighbourhood
  snapshot (:336-367): take it on the game thread, mesh on the job pool, upload on the game thread. Estimate: game-thread meshing under
  0.5 ms a frame, fill 3 to 5x faster. M to L.
- **P2 (major). Every edit rescans everything:** `ChunkViewSet.Update` (:132, 166, 215) re-sorts all ready chunks and `Remesh` re-checks
  every wanted chunk's key (:579; 7 lookups each) on each block change and flow tick (estimate 3 to 10 ms a frame at High). Fix: dirty
  marking from a block-change listener; re-sort only when the eye changes chunk. M.
- **P3 (major, hitches). Disk reads on the game thread** in `LodRing.Plan` (:119): a file open and read per footprint (4,225 in the first
  plan at High). Move the check to the worker or keep an index read off-thread. S.
- **P4. An fsync storm:** up to 48 region files synced whenever the write queue runs dry (`FileLodPersistence.cs:196-199`), plus two
  flushes per section (:232-236). Sync on a timer, on save and at shutdown. S.
- **P5.** Per-frame allocations (closures, lists, a boxed `First()`, listener `ToArray()` per install or evict, chunk copies in
  `GridResidency.ContentOf`): about 1 to 2 MB/s of short-lived garbage (estimate). S.
- **P6** start-up (8 threads in `ValleyUnderground`, the LOD folder deleted on the game thread, the ring's first plan); **P7** memory over a
  long session (real LOD sections never evicted, RR H5; regions append only); **P8** the language core runs off the game thread (not
  measured).
- Order: P3, P4, P2, P1, P5.

## 5. Graphics fidelity

Judged from `artifacts/b-series/b07c/`, `b12/` and the owner's Voxy references (shader effects excluded).

- **G1. No light propagation:** sky light is a function of depth under the height model's surface (`SkyDepthLight.cs`, `SkySurface.cs`):
  no shade under tree crowns, in houses or under overhangs; caves a flat 28%; torches an additive glow. Minecraft-style sky and block light
  at 0 to 15, flood-filled per chunk across borders, with smooth per-vertex light, all in vertex colours: possible **now** with the
  built-in effects. The largest single visual gain. L.
- **G2. One saturated lime green everywhere** (mean ground about RGB 89,179,49): biome colour maps for grass and leaves from temperature
  and humidity, less saturation, the far palette from the same map. Possible now. S to M.
- **G3. The sun on the blocks never moves:** faces shaded from a constant `ChunkMesher.SunDirection` (:145) while the sky's sun moves
  (`SkyRenderer.cs:283`); dusk darkens the ground only about 17%. Bake each light band with its own sun azimuth; deepen `DuskLight`
  (:539). Possible now. S to M.
- **G4. No antialiasing** (`VoxelGame.cs:414-421` sets no `PreferMultiSampling`): 4x MSAA. S.
- **G5. Clouds** read as near-white low slabs: darker bluer bottoms, higher and smaller. S.
- **G6. Water** is a flat sheet: depth tint per vertex and an edge texture now (S to M); reflections need B17.
- **G7.** Radial and height fog need B17. **G8.** Near/far transitions: RR H3, H4. **G9.** Shadows, bloom, SSAO: B17 only, not the target.
- Good already: per-vertex corner occlusion with the diagonal flip, point-sampled mipmaps, dense flowers and grass, the horizon's haze.
- Order: G4, G3, G2, G5, G6 (all with built-in effects), then G1, then B17.

## 6. The core (a sample)

M96 is sound and narrow (`AccentVariantChoice.cs:137-157`); M97's near-miss change is sound (`AnswerNearMisses.cs:174-178`); M97's
realiser change narrows the fuzz generator (T3).

## 7. Docs and data

`architecture.md:174, 328` (worker threads), the `LodEdits` summary (`StreamedWorldHost.cs:233`), `QualityPreset.cs:22`, biomes-plan.md's
`ChunkPrefetcher`; every world-generation file has a schema and a validator (not audited field by field).

## The five most valuable improvements next

1. The far store's edit layer: C1 and C2 with a race test (M).
2. Meshing off the game thread and rechecking only dirty chunks: P1, P2 (M to L).
3. A fidelity bundle with no shaders: MSAA, a moving sun and deeper dusk, biome colour maps, cloud and water tints: G4, G3, G2, G5, G6 (S to M).
4. Sky and block light propagation with smooth light: G1 (L).
5. The far store's memory and IO: LRU-evict real sections, the ring's disk probes off the game thread, fsync on a timer, compaction: P3,
   P4, RR H5 (M).

Then: the guards (A1, a fuzz realisation-rate gate, the T1 concurrency tests).

Not covered: UI, audio and speech, NPCs, lessons and dialogue; the maths of blending, biomes, caves and rivers; the save fixture's contents;
the analyzer beyond M96 and M97; the far mesher and renderer internals (FR, RR); WorldPreview, Host, CI. Performance figures not from the
reports are estimates.
