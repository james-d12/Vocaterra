B12 report (agent a3940fd1ac62ebf2a, 2026-11-16 about 09:15 UTC; first pass, sent back for three fixes)

Branch `B12-underground-in-play` (local), head `1d151128` (3 commits on 898a95e0), worktree .claude/worktrees/b12, clean.

Built:
- Darkening by depth: `SkyDepthLight` (constants beside the other light numbers: 6-block falloff, linear, to a cave minimum of 0.28 of the sun; 0.12 and 0.18 were near-black). `SkyExposure`/`SkySurface` take the surface from the pure height model (`NaturalSurfaceAt`) and let the sky down open shafts and cut banks to the first opaque block (up to 48). The mesher darkens each face corner from the open cells in front of it; a face whose front cell is open to the sky is unchanged, as are lava and everything above ground; forest floors and houses untouched (the natural surface ignores trees and roofs). No propagation.
- Lights: `BlockId.Torch` is a `Glows` kind (lantern, torch, post torch, lava), so `ChunkLightIndex` follows a placed or removed torch and re-meshes the chunks within 9 blocks at once. Glow strength 1 underground at any hour (`VoxelRenderer.GlowStrength`); fog and clear colour darken with the eye's sky share (`EyeSky`).
- Near zone: B07's `BandStreamPolicy` (near 4, underground 6, from `IPlaceInfo.IsUnderground`) unchanged; a test that it widens underground and narrows at the surface; 6 columns (96 blocks) enough across a cavern.
- Cave tour: `scripts/scripted-runs.sh ... r50-cave-tour` (seed 42, `--pass open`): the pass, the nearest entrance (a pit at 488..496, 181..190, floor y 70 under ground 80), a cave at y -1, a cavern (504, -34, 176), a lava lake (528, -53, 190), a placed torch. New steps: `--teleport-to x,y,z`; signed `--teleport`/`--place`/`--pour` (part of B14's signed coordinates).

Tests: SkyDepthLightTests 7, UndergroundLightTests 8 (with the valley regression), CaveTourTests 2, TeleportToStepTests 6, one launch-options test, one residency test; `UndergroundBlockTests` updated (four glow kinds). Game.Tests 1,225, Game.Core.Tests 4,218 (two halves), Host 189, Golden 17, WorldPreview 31; Nlp and Nlp.Es in the fast run. Not seen: the fuzz assembly (oom-killed twice in the shared 8 GB slice) and one single full run. `NamePlatesTests` failed once under load, passed on rerun. Build 0 warnings, format clean, content valid.

Measurements (Release, RTX 3070): underground p50/p95/p99 low 2.0/9.6/14.6, medium 2.4/10.6/12.0, high 3.1/10.1/11.8 ms (load 2.4 to 2.7; start frames included); resident 6.4k/10.0k/15.3k chunks (surface 5.5k/9.2k/14.4k); mesh about 1.2 ms a chunk. Surface 230-block walk: 1.5/9.3/10.7, 2.0/10.4/11.9, 2.9/10.0/11.6. p95s above B07's (5.7/7.6/7.8) on a different walk with start frames and a shared machine; no A/B against the base.

Scripted runs (`--world valley`, 32): identical or order-only except r01 and r42 pictures lit by the now-lit torch (expected), r01's sounds.log timing, stdout timing lines; a one-pixel difference in four pictures fixed; one r01 run segfaulted at shutdown under load (two reruns exit 0).

Pictures (artifacts/b-series/b12/): 00 the pass; 01 the cave mouth (a dirt-rimmed hollow, not legible as a cave mouth); 02 the pit with a black opening in daylight; 03 the tunnel at y -1; 04 with a placed torch; 05 a dark cavern; 06 and 07 a lava lake lighting the walls orange.

Limits: shots right after a teleport show the old view (B07b); B11's entrances are open pits about 10 deep not joined to the cave below by a walkable path (the tour teleports); far from light the cave is nearly black at 0.28; a placed block that changes the surface does not re-mesh the chunks beneath. Files outside the light code: `BlockDefinition.cs`, `Scripted/VoxelStep*.cs`, `VoxelScript.cs`, `Navigation/GroundFinder.cs`, `Startup/LaunchOptions.cs`, `SceneDrawer.cs`, `scripts/scripted-runs.sh`, docs. Not touched: VoxelGame.cs, ChunkResidency.

Orchestrator's review (about 09:20 UTC): sent back for (1) vertical stripes in the pit's walls (per-column exposure steps; smooth per vertex), (2) the re-mesh beneath a surface edit, (3) a torch that lights its surroundings visibly; then one full suite with one slot.
