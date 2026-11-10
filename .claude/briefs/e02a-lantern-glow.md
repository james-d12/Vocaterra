# Brief: E02a, the lanterns' glow as its own layer

Sent 2026-11-10 to a general-purpose agent, Sonnet 5.5 at high effort. Worktree `/home/james/Dev/Vocaterra/.claude/worktrees/e02a`,
branch `E02a-lantern-glow`, cut from `feature/weather` after E05's gate. The shared part is `.claude/briefs/e-series-standard.md`.

```
You are implementing E02a, "the lanterns' glow as its own layer": a fix for the one defect E02 left. Your work is in
src/Vocaterra.Game/Graphics/World (and what calls it) and tests/Vocaterra.Game.Tests.

The defect (docs/game/findings.md, the E02 entry, "Not fixed (E02a)"; src/Vocaterra.Game/README.md, "The day cycle"): the light a
lantern or torch throws on the faces near it is baked into the chunk's vertex colours together with the sun's light
(Graphics/World/ChunkMesher.cs, `Light` and `Glow`, near lines 489-560). E02 draws each chunk with a tint of the look's sun light over
the baked light of the chunk's band (Graphics/World/LightBands.cs, VoxelRenderer.cs), so the baked glow is scaled by that tint too:
it steps at each of the four band changes of a game day (the lantern area's mean red 136 against 88 across the Night and Dusk edge),
shows as a patchwork round lanterns for the 6 to 15 seconds a band change takes, and since E05 it also dims in bad weather, which is
backwards.

The fix: the glow leaves the baked sun light and becomes its own mesh, drawn additively with a strength that is a continuous function
of the look, with no band in it.

Read (the sections named): src/Vocaterra.Game/README.md (the light, the day cycle, the weather); ChunkMesher.cs (MeshOptions,
`Light`, `Glow`, GlowRadius, GlowAdd, GlowCool, how `ChunkMeshes` holds the opaque, plant, water and `LitMesh` meshes); LightBands.cs
(the lantern strength per band); VoxelRenderer.cs (how each mesh kind is drawn per band with its tint, the LitMesh pass, DrawWorld's
order, the blend and depth states of the water pass); Graphics/Sky/SkyLook.cs (the look's sun light and daylight);
Graphics/Figures/HeldArm.cs (ArmLight's lantern warmth, which is already separate); the E02 tests in
tests/Vocaterra.Game.Tests/Graphics/World (ChunkMesherLightBandTests, LightBandsTests, ChunkVersionTrackerBandTests) and
Graphics/Blocks/VillageArtTests.cs (the lantern face counts).

E03 (sound, menu rows, scripted steps) is running in Core and in VoxelGame.cs's testing hooks; E06 (rain) will add a pass to
VoxelRenderer.DrawWorld after the water and new files under Graphics/Weather. Do not edit Core, content or Graphics/Weather, and keep
your DrawWorld change to the one new pass.

Deliverables:

1. The base meshes (opaque, plants, water) bake no glow in any band: a vertex's colour is the face's tint, the ambient occlusion and
   the band's sun multiplier only. The band keeps deciding the sun's baked light, and nothing about the glow.
2. A glow mesh per chunk (beside LitMesh): for every face a lantern or torch reaches, the same quad with the same texture
   coordinates, its vertex colour the glow at that vertex (the falloff, the facing and GlowRadius as today). It is built with the
   chunk and does not depend on the band, so a band change neither rebuilds it differently nor changes it.
3. The glow pass: drawn after the opaque and plant passes, additively, depth read and not written, on exactly the base geometry (no
   z-fighting: the same positions through the same transform; say how you made sure, and show a close screenshot), with a strength
   from the look that is continuous over the day: as strong at night as today's night, about half by day as today's day, moving
   smoothly through dawn and dusk, and not reduced by the weather's sunlight. Cut-out faces (plants, fences) glow only where their
   texture is opaque. Fog still fades the glow with distance.
4. The look. Today's formula also cools the lit face (GlowCool) before adding; an additive pass cannot multiply. Choose the closest
   look an additive, textured pass gives and say what changed: compare the lantern area's mean colour at noon, at a held dusk and at
   midnight before and after, and keep midnight within a small difference of today's. Noon away from any lantern must stay
   pixel-identical to the build before (the default run's camp view at tick 300).
5. No step: at each band edge (hours about 4.24, 5.7, 20.3 and 21.76; LightBands gives the exact ones) the lantern area's mean colour
   a little before and a little after differs by no more than the sun's own smooth change. Measure it as E02 did and report the
   numbers beside E02's (136 against 88, 172 against 149).
6. Tests (tests/Vocaterra.Game.Tests, mirroring the source): the base meshes' vertices are equal with and without lanterns in every
   band; the glow mesh has a quad for each face in reach and none beyond the radius; it is the same in every band; the strength is
   continuous across every band edge and equal by day and at night to the values today's bands use; the existing lantern and torch
   face-count tests moved to the new meshes on purpose.
7. Screenshots to /tmp/claude-1000/-home-james-Dev-Vocaterra/24d882de-ab0e-4e80-85f7-7ad42d8daea9/scratchpad/e02a-shots/ (not into the
   repository), `--quality low`, flattened to opaque RGB: the village view with lanterns in sight at `--time` 0, 13 and 21; the four
   band-edge pairs; one close to a lantern on a wall and on grass at night; one at night with `--weather` set to the bundle's heaviest
   kind (read the ids from content/game-v1/game/weather.json; no kind's id may appear in src/). Look at them and say what is wrong
   with any.
8. Measurements: the camp and the village view's frame time at the low and the default preset before and after; the glow mesh's
   vertex count in the village.
9. Documents: the light and day-cycle sections of src/Vocaterra.Game/README.md (the known limit goes; the glow pass comes).

Out of scope: any change to how far or how strongly a lantern lights compared with today beyond what point 4 forces; coloured or
flickering light; light from the held torch on the world; shadows; rain; Core.
```
