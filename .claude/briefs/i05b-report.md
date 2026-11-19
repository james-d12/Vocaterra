# I05b report (light round the player, a spike)

The agent's report, saved by the orchestrator (2026-11-18). Sonnet 5.5 at high effort; brief `i05b-player-light.md`. Gated at `f0b6a882`
(after the orchestrator merged feature/inventory into the branch and rehashed the manifest, eb58627b): 13,359 passed, 0 failed, 9 skipped;
timing 23; format clean.

Branch `I05b-player-light`: 8d2c7c3d (the light stat, `game/light.json`, a term in the chunk shader), eda4a5e3 (tuning, docs), a277a4a8.

**How the world is lit today:** the brief's `LightMap` is not in this tree. The mesher bakes sky light (`SkyDepthLight`/`SkyExposure`: 1 in
the open, 0.28 deep underground), block light (`ChunkLightIndex`: torches, lanterns, lava within `GlowRadius`) and corner occlusion into a
per-vertex word; the game's own GLSL program (`chunk.vert.glsl`/`chunk.frag.glsl`, on by default) multiplies by the day's sun colour and adds
amber block light. The MonoGame effects path is a fallback only.

**Chosen:** `VoxelGame` calls `VoxelRenderer.SetPlayerLight(data, light sum, eye)` each frame; `PlayerLight.Of` (Core, pure) gives reach and
strength from the stat and the daylight at the eye (the sun light's luminance x `EyeSky`: full at or under 0.4, off at or over 0.8, linear
between, so noon in a cave is dark); two uniforms (`uPlayerLight`, `uPlayerLightColour`) add `texel x colour x smoothstep(1 - d/reach) x
facing` (0.3 to 1, 0 for faces turned away) in `chunk.frag.glsl`; a branch skips it when off. No remesh, no simulation state, no save.
**Rejected:** an additive glow mesh (geometry per frame, depth fights, no falloff on the faces). **Limits:** no light on the effects
fallback (below OpenGL 3.3); thin walls pass light (no occlusion); one global colour.

**Data** (`game/light.json`, schema, validated, `GameData.Light`): radius 2 + 0.6 x light; strength min(1, 0.08 x light); colour (1.0, 0.78,
0.45); darkAtDaylight 0.4, offAtDaylight 0.8. The `light` stat: a sum, 0 to 16 per item, the total clamped to 16. Light 12: reach 9.2,
strength 0.96; light 3: reach 3.8, strength 0.24. For I08b: lantern about 10 to 12, torch 8 to 10 if worn, amulet 2 to 3; rows only.

**Frame times** (low preset, 1280x720, Release, seed 42, RTX 3070, 900 frames, back to back, load 6 to 23): GPU time median 0.56 ms open
without light, 0.38 with the lantern; cave 0.21 without, 0.23 with; frame p50 22.5 to 24.4 ms in the clean pairs; the two slower lantern runs
coincide with load spikes of 15 and 23. No measurable cost.

**Screenshots:** `scratchpad/i05b-shots/` (open/cave x none/amulet/lantern x first/third); six sent to the owner (stop 5).

**Tests:** +29 (`Light/PlayerLightTests`, `Graphics/World/PlayerLightGlowTests`). Changed on purpose: `StatRulesTests` (`light` has a rule),
`NoLiteralTextTests` (allow-list `Light/PlayerLightData.cs`), the `GearItems` test catalogue (Lantern 215, Amulet 216). No digest, golden or
fixture moved.
