# B42 report (coarse far tiles flicker over fully rendered chunks)

Branch `B42-standin-flicker`, head `21e02452` (feature/world-next at c8715d4e merged, no conflict; four B42 commits on the WIP). Agent's
gate: build 0 warnings; full 12,700 passed, 0 failed, 9 skipped (before a last 3-line using-order commit, after which the build and the 43
far-view tests were re-run); format clean; validate.

Cause: not a z-fight with the chunks (the far pass clears depth before the chunks draw). In `FarView.Assemble` a stand-in (B35's level-4
tile, 512 blocks) was drawn whole and hid every fine leaf under it already on the card, and stayed in the draw list while any one leaf under
it waited (B38 makes leaves wait up to 240 s; walking, flying or ring ingest keep adding waiting leaves). Finished level-0 ground swapped to
level 4 and back as leaves came and went. Evidence from a new test (fresh fill, eye walking 0.5 block a frame, the level each 32-block
ground cell shows): before the fix 610 of 1,216 watched cells flip fine > coarse > fine; after, 0 of 1,140. Still screenshots and two
scripted walks did not isolate it. Main not run: stand-ins and ring waiting exist only on this branch.

Fix: each stand-in carries windows (merged rectangles) over the leaves under it that wait, plus the leaves under the chunks (a backdrop
behind water and foliage, kept apart from the chunks by their depth range); a stand-in with no windows draws nothing; on-card fine leaves
draw as they are. GL path: `chunk.frag.glsl` takes `uClip[128]`, `uClipCount` and discards outside; `GlChunkPass.SetFarClip`;
`FarTerrainRenderer.DrawGl` sets it for the opaque, lit and water passes; over 128 windows the stand-in draws whole as before.
**The MonoGame-effects path (shaders off) is not fixed** (no clip in BasicEffect/AlphaTestEffect, no stencil in `Depth24`): it still
flickers. A regression in the WIP (no stand-ins for inner leaves in their 700 ms grace: sky gaps in the early fill) was found and removed.

Test: `FarViewStandInClipTests` (slow): no ground cell with two owners; every fine leaf under an on-card stand-in has a tile or a window; no
cell outside the zone goes fine > coarse > fine; stand-ins are windowed. Existing far-view tests unchanged. Pictures:
artifacts/b-series/b42/start-fill-before-after.png (3, 5, 7, 9 s of a fresh fill; no regression; the flicker itself not pictured).
Left: confirm in the game (flying) in the GL path; whether `--shaders off` deserves a fix (a stencil depth format). For architecture.md:
"Far stand-ins are drawn through `FarTile.Windows` (clipped by `uClip` in the chunk fragment shader); a whole stand-in is used when
MonoGame's effects draw or a stand-in has more than 128 windows."
