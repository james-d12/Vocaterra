# Rendering and view distance: a review (2026-11-16)

Asked for by the owner ("Kick off an Opus 5.5 high agent to review the rendering and distances"), after he asked what it takes to reach
the render distances of Voxy and Distant Horizons ("that's what we want to achieve"). Done by an Opus 5.5 agent at high effort, read-only,
on `feature/biomes` at `f4daab2b` (the streamed world B07, the far view built like Voxy B07c). No build, test or game was run: every code
finding was checked against the source; figures marked (estimate) are reasoning, not measurement. The owner's reference pictures are in
`artifacts/reference/voxy/` (outside git); his bug screenshots of that day in `artifacts/owner-bugs/2026-11-16/`.

## Summary

Distance is not the gap: at 16 km the far view is already in Voxy's class, and each doubling of the range adds only about 100 coarse
tiles. What separates it from the reference pictures is detail per pixel (a far voxel covers about 4 to 8 pixels at every distance, since
the bands are fixed multiples of the voxel view distance), five confirmed seam and transition defects, two worker threads building
everything, and fat vertices (108 bytes per quad). The owner's Debug-build symptoms have code causes (H1 to H4 below) that show more when
tiles build slowly; the cliff slots (H3) persist in Release.

## 1. What limits the distance today

- **Per frame.** One draw per visible tile per pass, each rebinding texture, effect pass and buffers (`FarTerrainRenderer.cs:195-212`);
  MonoGame 3.8.6 DesktopGL uses no vertex array objects and no base-vertex draws, so every draw re-specifies its attributes; about 300 to
  600 far draws in a walk, and the near zone draws one per chunk per bucket (`VoxelRenderer.cs:690-739`). Frustum culling only
  (`FarTerrainRenderer.cs:136`), no occlusion. Uploads up to 8 MB a frame on the main thread, a new buffer pair per tile; a settled High
  view (about 300 MB) takes 40 or more frames to reach the card. `VertexPositionColorTexture` is 24 bytes, 108 bytes per quad with
  indices; textured levels 0 and 1 hold about 70% of the triangles because textured faces are never merged (`FarTileMesher.cs:600-601`).
- **Per move.** A far tile costs about 33 ms of worker time; about 2 new tiles per block walked (estimate). The model samples 2 x 2
  columns per cell at every level (`LodModelSource.cs:67-96`); every model section is deflated and inflated again (`LodStore.cs:309-310`).
  The ring generates all 24 chunk layers of each footprint (`LodRing.cs:141-146`, about 96 chunk generations each, 5,300 footprints at
  High) while the tile mesher discards anything more than 24 blocks underground (`FarTileMesher.cs:169`): most of it wasted (estimate 5 to
  10x), and likely why the horizon fills in 15 to 30 s. `JobPool.MaxWorkers = 2` (`JobPool.cs:27`) on a 16-core machine.
- **Precision.** Far pass near plane 12 (`FarTerrainRenderer.cs:24`), 24-bit depth: about 1.3 blocks of depth step at 16 km, 5 at 32 km,
  20 at 64 km, harmless for non-overlapping voxels, a problem only where tiles overlap. Near pass near plane 0.05 (`FirstPersonCamera.cs:18`):
  fine at 400 blocks. Absolute float positions: jitter far from the origin (about 0.008 blocks at 100 km), an endless-world issue.
- **Where it breaks first** as distance or detail grows: worker throughput and fill time; then card memory once detail per pixel
  improves (4x tiles, about 1.2 GB at 108 bytes a quad); then draw calls; then fog and edge artefacts. Above about 32 km a 384-block-tall
  world shrinks to a few pixels at the horizon (diminishing returns). Detail per pixel at the start of each band: L1 3.7 px, L2 5.5, L3
  6.6, L4 7.3, L5 7.7, L6 8, L7 8.1 (half that at each band's end); a 5-block crown at 3 km is about 1.4 px but sits inside a 16 to
  32-block voxel: more than kilometres, this is why far land reads as crammed blocks.

## 2. Bugs and risks

- **H1. Stand-in tiles pushed to the far plane.** `FarTerrainRenderer.cs:29` `DepthBias = 2f`; DesktopGL applies `glPolygonOffset(slope,
  DepthBias x 2^23)` for Depth24, so stand-ins clamp to the far value and draw in painter's order or not at all. Fix: never overlap
  levels (draw the parent until all four children are uploaded, then swap).
- **H2. Built but not uploaded leaves holes.** `FarView.cs:58` counts `Pending` as built, so no stand-in is used while the renderer skips
  the tile (`FarTerrainRenderer.cs:131`). Fix: decide on `Buffer is not null`.
- **H3. Tile edges only skirt-deep (persists in Release).** Air outside the tile (`FarTileMesher.cs:369`), outward edge faces dropped
  below the skirt (:553), skirt max(6, 4 x cell) (`FarViewSettings.cs:51`), measured from tree crowns at edges (:388-395): see-through
  slots in cliffs on tile borders; a missing neighbour shows a cross-section with sky under it (arches, grey bands). Fix: neighbour-aware
  meshing; skirts only toward a coarser neighbour, from the ground.
- **H4. Holes in the near zone show the far ring's inner edge as a floating slab** (`FarTileSelector.cs:49`; nothing stands in for
  unmeshed near chunks). Fix: far tiles inside the zone for footprints whose chunks are not all meshed.
- **H5. Real sections never leave memory** (`LodStore.cs:214-216` trims only model entries): long walks grow memory (estimate 1 to 5 GB an
  hour at High). Fix: an LRU over all origins; store buried sections as uniform stone.
- **M1. Planar fog against radial selection** (`VoxelRenderer.cs:402-406`, `FarTileSelector.cs:49`): the land ends at about 63% fog at the
  screen sides. Quick fix: select to Range / cos(half horizontal FOV); proper fix radial fog (B17).
- **M2. Two workers.** **M3. Allocation churn** (`LodSection.cs:204, 233`, `FarTileMesher.cs:513, 653, 714`, `VoxelRenderer.cs:1096`):
  gen-2 collections, likely the p99 tails. **M4. The ring generates whole columns.** **M5. Real data mipped only to L4 and the ring to
  1 km:** built things and edits never show beyond about 3.4 km. **M6. The far pass's depth range** (near plane 12).
- **L1** far water sorted by tile, not within one. **L2** light and look differ near and far (per-face light only; textures end at L1).
  **L3** absolute positions. **L4** clouds always behind land (`SkyRenderer.cs:93`). No resource leaks found.

## 3. The road to Voxy-level distances

From the MonoGame 3.8.6 DesktopGL assembly: present are instanced draws with base instance, attribute divisors, 3D textures and texture
arrays, and occlusion queries; absent are indirect multi-draws, compute dispatch, buffer storage and persistent mapping, depth clip control,
vertex array objects and base-vertex draws. Custom shaders work through `Effect`; only the compiler (mgfxc, Wine on Linux) is the problem.
Reversed depth is not needed: split depth ranges (already used, plus a second far range) hold precision to 64 km. Step 1 within MonoGame can
most likely reach 16 to 32 km at the reference's detail on the RTX 3070; card-driven rendering buys density and low processor cost, not
distance.

Recommended order:
1. **A B19 spike (S):** log the GL version; 32 and 64 km at High (more `BandStarts` and `Levels`); `BandScale` 1, 2 and 4 at 16 km;
   record tiles, worker time, triangles, memory, draws and p95.
2. **B19 step 1 within BasicEffect (L):** a compact 16-byte vertex (short position local to the tile, colour, short UV) with a per-tile
   `World` matrix computed in double relative to the camera (camera-relative rendering); one shared quad index buffer (about -40% memory);
   region batching (16 to 64 tiles a buffer and pass); processor horizon culling (an elevation-angle buffer per azimuth); selection by
   pixels per voxel; atomic parent and child swaps; neighbour-aware meshing; a second depth range; the fog fix.
3. **B17 shaders (L):** the toolchain; instanced quads of 8 to 12 bytes (about 10x less far memory, estimate); light as data; radial fog
   matching near and far; a texture array or atlas tiling so textured faces merge (levels 0 and 1 triangles 3 to 5x fewer); water.
4. **B19 step 2, only if measured necessary (L to XL):** card-driven rendering through raw OpenGL 4.3+ on MonoGame's context (Silk.NET or
   OpenTK; also removes the Wine problem; risks: MonoGame's state cache, drivers, no macOS), a newer MonoGame back end (unverified), or the
   engine change (D-game-1).

## 4. Quick wins

1. H2 (draw on `Buffer`). 2. H1 (parent until children uploaded). 3. The far pass's near plane at about half the voxel view distance
(depth precision about 16x). 4. A Release launcher and a "DEBUG" tag on the screen in Debug builds. 5. The worker count as a setting (about
3 to 4x faster fill; needs the owner). 6. Pool the per-tile arrays; no put-then-get round trip; no `ToArray()` in uploads. 7. Cap real
sections in memory. 8. A surface-only ring (5 to 10x cheaper, estimate). 9. Selection to Range / cos(half FOV). 10. The near pass's near
plane 0.05 to 0.1.

## 5. The architecture

The far store is right (and in one way better than Voxy: the model fills land nobody has visited, so no pre-generation is needed). Before
building more on it: bounded LRU of decompressed sections with deflate only on disk; the visible shell only; neighbour-aware meshing; atomic
level swaps; selection by screen-space error, not tied to the voxel view distance; a compact, batched, camera-relative tile format; more
workers. The near zone is fine at 400 blocks; main-thread meshing would block a larger one, not the far view. The "2 of 8" mip rule
dilates sparse things (forests become canopy slabs from L2); with `TreeLevels` 4 there are no separate crowns beyond about 2 km: a
coverage-preserving foliage rule and exact trees to L4 or L5 move toward the reference look (needs step 1).

## Roadmap

| # | Milestone | Goal | Size | Expected effect |
|---|---|---|---|---|
| R0 | Seam fixes (B07e) | H1 to H4, the fog fix, the far near plane, a Release launcher | M | No holes, arches or floating slabs; correct cliffs |
| R1 | Processor and data (B07d) | workers setting, pooling, an LRU store with a cap, a surface-only ring, a model column cache, near-plane fixes | M | Fill 3 to 6x faster (estimate); flat memory; better p99 |
| R2 | B19 spike | 32 and 64 km, BandScale 1, 2, 4, the GL version | S | The numbers that decide R3 to R5 |
| R3 | B19 step 1 | compact camera-relative vertices, batching, horizon culling, pixel-error selection, a second depth range | L | About 2 to 3 px per voxel at 16 to 32 km in the frame budget; -40% memory |
| R4 | B17 | instanced quads, light as data, radial fog, merged textured faces, water | L | About 10x less far memory (estimate); day and night without re-meshing |
| R5 | B19 step 2 | card-driven rendering if R2 and R3 show the need | L to XL | Voxy density at little processor cost |

## Decisions for the owner

- **D1.** Worker threads in the game (two today; the machine has 16 cores; scripted runs can keep two).
- **D2.** A detail target in pixels per voxel (for example about 2 at High) instead of bands tied to the voxel view distance (about 4x the
  outer tiles; needs R3).
- **D3.** B17's shader toolchain: mgfxc on a Windows CI runner with committed binaries; a pinned Wine; raw GLSL on MonoGame's context (no
  Wine, opens GL 4.x, excludes macOS); or waiting for MonoGame's new back ends.
- **D4.** Platform scope: if macOS does not matter, raw GL 4.6 is a cheap R5 path without an engine change.
- **D5.** The distance target: 16 km is Voxy-class; 24 to 32 km is the useful maximum for a 384-block-tall world; spend on detail per
  pixel rather than kilometres.
- **D6.** Built things far off: real data beyond 1 km and mips beyond L4, or settlements placed by the model as trees are.
- **D7.** The order: R0, then R1; R2 first among the large items; then R3 and R4 in either order (R4 the largest memory gain, R3 needs no
  new tool).

Not verified: the far pass's depth compare (LessEqual from memory); the GL context version; texture arrays end to end on DesktopGL; the
exact requirements of Distant Horizons and Voxy; every cost split marked (estimate).
