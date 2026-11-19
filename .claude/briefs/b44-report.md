# B44 report (the sky tile off the game thread; B43's fix 1)

Branch `B44-sky-tile-off-thread`, head `97c7951e` (feature/world-next at 8555ffcf merged; B44 commit `604ab176`). Agent's gate: build 0
warnings; full 12,706 passed, 0 failed, 9 skipped; timing 23 of 23; format clean; validate.

Change. Game.Core (workers): `ColumnContext` keeps the height model's raw heights before the shapers when it shapes a group window;
`ColumnGroup.NaturalTile(cx, cz)` cuts the 18x18 tile (margin at least 1, so no neighbour lookups); `ProceduralChunkSource.NaturalTile(cx,
cz, group)` falls back to one climate fill plus `Height.Shape` when no group holds the column (also through `CompositeChunkSource` and
`StreamedWorld`). Game: `Streaming/ColumnNaturalHeights` (game thread only, owned by `ChunkResidency`, exposed as
`StreamedWorldHost.NaturalHeights`); the residency's worker job returns the tile with the column; `Install` stores it before the chunks;
eviction removes it with the column; the inline path stores it too; `SceneDrawer` hands the store to `VoxelRenderer.SetNaturalHeights` and
on to `SkyExposure`; `SkyExposure.Tile` builds from the cached tile plus the unchanged walk down the grid, falling back to the old
game-thread computation for a column without a tile (0 times in the runs). `--stream-stats` ends with a `sky tiles` line. Note:
`WorldQuery.NaturalSurfaceAt` returns the dry answer until the worker has read the landmass; mesh values cannot differ once chunks are
resident.

Identity: `NaturalHeightsTests` (slow, 4): 32 sampled chunk columns on seeds 42 and 2026, all 324 block columns each against
`NaturalSurfaceAt` (valley, its edge, group edges, far land, sea, mountains; over 60 blocks of height spread), group cut and standalone tile;
sky tiles and meshes (Opaque and Alpha, vertex for vertex) identical from cached and old heights, before and after edits (edits never move
natural heights); residency with 2 workers: every resident column has its tile, equal to the height model; after moving, held tiles match
the plan.

Memory: about 1.32 KB a column; 2,453 columns and 3.1 MB measured at High; hard cap 16,384 columns (about 21 MB), past which the store clears
and the fallback runs; each `ColumnGroup` gains one int per window column (about 32 KB a group, about 4 MB at the 128-group cap).

Measured (Release, High, `--world streamed --pass open --no-settle --workers 8 --stream-stats`, fresh seeds 7501-7503 start and 7511-7513
teleport to (3155, 3115) after 35 s, fresh `VOCATERRA_LOD_DIR` each, before c8715d4e and after interleaved, medians of 3 (jump n=2), load
4.7 to 16.5, Draw capped at 60 fps for both by a temporary patch):

| Case | build | meshed 50/90/100% s | visible 50/90/100% s | meshes/s | game-thread Remesh total |
|---|---|---|---|---|---|
| Start | before | 10.9 / 23.0 / 26.8 | 11.3 / 21.2 / 24.9 | 259 | 8.5 s |
| Start | after | 6.6 / 10.7 / 11.7 | 6.6 / 10.1 / 11.3 | 583 | 2.5 s |
| Teleport run, start phase | before | 12.9 / 25.1 / 28.7 | 13.2 / 23.3 / 26.7 | 227 | 16.0 s (whole run) |
| Teleport run, start phase | after | 8.3 / 12.3 / 13.6 | 8.2 / 11.9 / 12.9 | 479 | 3.5 s (whole run) |
| 3 km jump | before | 15.2 / 26.4 / 29.4 | 13.0 / 24.6 / 27.2 | 179 | n/a |
| 3 km jump | after | 4.2 / 7.9 / 9.0 | 3.6 / 7.3 / 8.4 | 569 | n/a |

Near view (64 blocks) complete: start 4.2 to 2.8 s; jump 3.1 to 0.6 s. Generation and residency unchanged (100% at about 7 to 11 s).

Next: meshing no longer lags residency (meshed 100% about 2 to 3 s after resident 100%), so residency is the limit, then B43's fixes 3 and 5,
then 4. 30 to 60% of columns are made alone (the 128-group cache evicts groups before generation reaches them, B39's limit too); for those
the tile costs about 3 ms on the worker; raising the group cache limit would help both. The downward walk per tile stays on the game thread.
B43's branch-against-main 3 s (B38's ring copy) untouched.

For architecture.md: "Since B44 the sky's exposure no longer evaluates the height model on the game thread. The residency's column job (a
worker) cuts the 18x18 natural surface heights of the column and its one-block ring from the group window it generated the column from
(`ColumnGroup.NaturalTile`, kept from the window before the shapers run), or makes them alone (`ProceduralChunkSource.NaturalTile`), and
returns them with the column's chunks. `ChunkResidency.Install` puts them in `ColumnNaturalHeights` (game thread, about 1.3 KB a column,
removed with the column), `StreamedWorldHost.NaturalHeights` hands the store to `VoxelRenderer.SetNaturalHeights` and `SkyExposure.Tile`
builds a tile from lookups plus the walk down the blocks; a column without heights falls back to computing them on the game thread. Edits
never move the heights; they invalidate tiles only." For running.md: "`--stream-stats` ends with a `sky tiles` line: tiles built from the
workers' heights, tiles that computed them on the game thread, the columns and MB held, and how many columns' heights were cut from a group
window or made alone."
