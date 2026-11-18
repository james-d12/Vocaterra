B22 report (agent aa7d5db64ccc8a1a2, 2026-11-17 about 01:00 UTC; sent back for a merge with B18 and the dusk crowns)

Branch `B22-fidelity-bundle`, head `fb88c4cc` (two commits on edd60cc6), worktree clean. Build 0 warnings; full run 12,423 with one failure (`NoLiteralTextTests` for the colour validator's messages, fixed and re-run alone); format clean; content valid; 24 new tests; the world's blocks, goldens and fixture untouched.

- G4 MSAA: `PreferMultiSampling` and `MultiSampleCount` in `PreparingDeviceSettings`; `QualityPreset.MultiSamples` 4 at every level; `--msaa 0|2|4|8`; the underwater target takes the device's count; it takes effect (the tree-line band's distinct colours 9,153 without, 17,054 with); fixed at window open. Its cost not measurable on the RTX 3070 at 1280 x 720.
- G3: `LightBand.Dawn` appended (4 bands); `LightBands.SunDirectionOf`: Day as before, Dusk the sky's sun at 20.2 h (west), Dawn at 5.8 h (east), Night the moon at midnight; `SunShare(face, band)` lights a low sun's own side fully; `DuskLight` (0.88, 0.56, 0.46), was (0.84, 0.66, 0.60); dusk ground luma 0.62 of midday (was 0.70). The far view and figures keep the Day band's directions (the far land is baked once).
- G2: `Graphics/Colour/land-colours.json` (embedded): a 5 x 5 climate map (temperature by humidity) of grass and oak-leaf colours, bilinear, no biome ids; `LandColours` validates it; the grass top, rim, oak leaves, tall grass and fern art made the pale end of the green (a vertex colour only darkens); birch, spruce and bush art slightly desaturated and untinted; `MeshOptions.Ground` from `IPlaceInfo` (temperature at the block, the column's humidity), memoised per column; tops take the full tint, sides 0.55; the far view the same map (`FarPalette.LandTint`); the valley's grass its climate's colour. Midday ground saturation about 0.56 (was 0.66).
- G5: `CloudBottom` (150, 168, 204), was (204, 216, 238); sides darker; boxes 30-50 by 18-30 (were 30-64 by 18-40); the height unchanged (B14's data).
- G6: `WaterDepthTint`, per vertex by the four corner cells' mean depth (up to 8): white at the shore, (0.56, 0.70, 0.90) from 6 down.
Tests: `BandSunTests`, `WaterDepthTintTests`, `LandColoursTests`, an `--msaa` parse test; changed for the intended look: `LightBandsTests`, `SkyLookTests`, `ChunkMesherLightBandTests`, `BlockAtlasTests`, `LookTests`, `SkyWeatherLookTests` (the clear-sky tint ceiling 1.43 to 1.50), `NoLiteralTextTests` (allow-list). Measurements at load 7 to 20: no change seen beyond noise; MSAA free.
Pictures (artifacts/b-series/b22/, 11 pairs and a no-MSAA copy): meadow at midday, dawn, dusk; the valley wood at midday and dusk; the village; the river; a pool; the sky; two far views. Dusk crowns go olive (for the owner). No deep lake shore found. Outside the brief: `FarTileMesher.cs` (a context field and colour lines), `FarViewSettings`, small wiring in `SceneDrawer`, `StreamedWorldHost`, `VoxelRenderer`; docs (Game README, running.md `--msaa`).
Orchestrator (about 01:05): sent back to merge B18 and tint its tufts with the same land colour, keep dusk leaves from going yellow, run the full suite.

Follow-up 1 (about 02:55 UTC), head 179b906f: merged feature/biomes c0f0ac10 (B18): conflicts in QualityPreset, VoxelRenderer,
SceneDrawer, LaunchOptions (--msaa and --grass), all kept. `GrassMesher` takes the column's `LandColours` grass tint (tufts match the
tops). Dusk crowns: oak-leaf map reds cut 28% (land-colours.json), no longer yellow-olive; `DuskLight` unchanged. Pictures
forest-dusk-after2, meadow-dusk-after2, meadow-midday-merged-grass60/-grass0. Full run: two timing tests failed at load 16 to 19
(ValleyUnderground, StreamedWorldTimeTests), passing alone. Measurements not retaken after the merge.
Follow-up 2 (about 04:15 UTC), head ea5f6053: merged B07e; conflict only in FarTileMesher's Context construction (B07e's neighbours and
B22's Ground both kept); B07e's border and skirt faces go through FaceAt's land tint. Full run at load 11 to 18: two timing failures
(ValleyUnderground, GeneratedLexicon), passing alone. Picture far-a-flower-meadow-merged-b07e.png: near and far the same green, no seam.
