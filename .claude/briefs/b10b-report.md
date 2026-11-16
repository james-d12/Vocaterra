# B10b report (Sonnet 5.5, medium; 2026-11-16)

Branch `B10b-lava-sink`, head `f87207ee` (one commit on e0b8c168). Agent's suite: 11,985 passed, 0 failed, 3 skipped (8 new, 3 removed
that encoded B10's stand-on-lava rule, replaced by `LavaBodyTests`); build 0 warnings; format clean; GoldenDigest unmoved.

- Lava: a moving body no longer collides with it (`VoxelCollider.Collides`); constants in `Physics/LavaMotion.cs` as water's are in
  `SwimMotion`: sink 0.5 blocks/s (water 1.5), swim-up 1.2 (2.5), swimming 0.25 of walk (0.6), wading 0.35 (0.8), vertical drag 0.15
  (0.07); climbs out at an edge like water; no harm; not solid for sight; `BlocksBody` unchanged, so the planners avoid it.
  `SwimMotion.NextVerticalSpeed` takes the liquid's numbers; `StepInWater` has a `lava` flag.
- View: a deep orange tint (strength 0.6, the water view's easing) when the eye is in lava (`LavaVolume.Contains`,
  `UnderwaterPass.DrawLavaTint`); not yet seen in a window.
- Ores (`BlockArtNature.Clusters`): five clusters of 3 to 6 pixels with a rim and a light top-left pixel: iron #D8A27A (brown rim), tin
  #E6EEF5 (dark grey rim), copper #E87A2C (brown rim) with three #3FB59A patina pixels.
- Pictures: /home/james/Dev/Vocaterra/artifacts/b-series/b10b/before.png, after.png, index.md; the writer is a permanent test
  (`OreSheetTests`, only when `VOCATERRA_ORE_SHEET` names a file).
- Known-not-fixed: no harm (later); footsteps, landing sounds and the breath meter unchanged; lava above a fence not handled; a likely
  merge with B07 in `VoxelCollider.cs` (only the `Collides` helper).
