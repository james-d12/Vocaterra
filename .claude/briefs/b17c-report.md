B17c report (agent adad109e156f24543, Sonnet 5.5 high, 2026-11-17 about 20:15 UTC)

Branch `B17c-light-in-shaders`, head `e893dac8` (4 commits on 25d8c494), worktree `/home/james/Dev/Vocaterra/.claude/worktrees/b17c`.
Full suite 12,590 passed, 0 failed; build, format, content, licence clean. Pictures in artifacts/b-series/b17c/ (75 files, index.md).
1. Light as data: a 4-byte light word per vertex in a second stream (`MeshOptions.Data`): sky (byte 0), block (byte 1), occlusion 2 bits |
   face 3 bits | flags 3 bits (byte 2), extra (byte 3: a flat face's grey, water depth); level L = L*17. `MeshVertex` (24 bytes) is the
   material. The shader: material * faceShade(sun) * occlusion * sky * sunColour + amber * glow * block. B22's baked bands, tints and
   glow pass gone from the GL path; `SunModel` follows the hour. The mesher a pure function of its snapshot (byte-identical worker and inline).
2. Fog by height (closed-form exponential along the ray, the look's haze colour, near and far). 3. Water shader (depth tint, movement,
   grazing-angle edge). 4. The far view through the same program in its own GL scope, mipped; its baked Day colours relit per face.
5. Texture array not adopted (`AtlasMipBleedTests`: mips cell-pure to level 4). 6. Toggles under "Shaders and effects": sun follows the
   hour, climate colours, water shader, fog by height, far view light, plus Antialiasing, with Grass moved in; each on by default, saved
   only when off, a launch option, set by the quality level (Low turns off the water shader and fog by height).
7. Fallback (Shaders off, no GL 3.3, compile failure): the mesher bakes today's colours; `BakedLightGoldenTests` holds a hash from the base.
Comparison against the base (High, 1280x720, >8/255): within 0.1%: meadow 7 px, cave 0, village 1, rain 6, dusk meadow 0.0085%, dusk wood
0.013%, night meadow 7, night village with torches 2; outside by design: lake shore 0.83% and under water 0.52% (water shader), far views
5.1% and 6.8% (fog by height; off: 14 px), shore at 20 h 37.9% (sun follows the hour). Shaders off: shore 7 px (1/255), torch view 0.
An hour change: the base rebuilt 5,829 meshes (19 s of worker time, longest frame 4.6 s); B17c none (longest 45 ms). A torch: the same 12
chunks re-mesh (12.8 ms baked, 12.5 as data). Card time equal or lower in every clean pair.
B23 inherits the sky and block channels (`ChunkMesher.CornerWord`, `Open`, `GrassMesher.AddTuft`) and `LightModel.Corner` for the fallback.
Left: B22's effects off give a pre-B22 look; the far water has no water shader; the climate toggle rebuilds all far tiles; torch runs not
compared (its base lacked B07f's fix). Sent back (about 20:20) to merge 66d9b263 (13 conflicts), run the full suite, compare the torch runs.

Round 2 (about 23:10 UTC), head f34f644e (merge 10a45fc7 of 66d9b263, then a docs commit): 13 conflicts resolved; the Graphics group: Quality, Draw distance, Field of view, Underwater blur, Screen effects, then under "Shaders and effects": Shaders, Grass, Antialiasing, Sun follows the hour, Climate colours, Water shader, Fog by height, Far view light, then B07d's far view rows and Worker threads; SkyLook keeps B14's CloudAltitude; B07d's pooled tile arrays feed GlChunkPass.UploadFar. Full suite with VOCATERRA_GL_TESTS=1: 12,766 passed, 0 failed. Torch runs (r42, r01, r40) finish on both paths with effects on and off; simulation identical; with effects off the pictures match within 8 pixels (largest 21/255 on a flame edge).
