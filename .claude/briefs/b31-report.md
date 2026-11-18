B31 report (agent a4cff8004a49d8754, Sonnet 5.5 medium, 2026-11-18 about 09:25 UTC)

Branch `B31-shader-toggle-crash`, head a141e91d (fix 38b62ed1, then feature/biomes merged), worktree `/home/james/Dev/Vocaterra/.claude/worktrees/b31`.
Cause: `FarTerrainRenderer.Convert` carried a far tile between the effect path and the GL path by reading its buffers back from the card;
the effect path's buffers are `BufferUsage.WriteOnly`, so `GetData` threw when Shaders went from off to on with tiles uploaded (the owner's
crash, 2026-11-18 08:29). Fix: `FarView.UsePath(bool gl, dispose)` drops every tile and rebuilds from data when the path changes after one
was set (as `Reconfigure` does for a change of levels; the drop shared as `DropAll`); `FarTerrainRenderer.Draw` calls it each frame;
`Convert`, its byte budget and `GlFarBuffers.Download` removed; both directions rebuild. Other read-backs: none on a WriteOnly buffer
(the remaining `GetData` calls are a texture in `GlChunkPass` and the microphone). Test: `FarViewReshapeTests` (the first path drops
nothing, the same path keeps the tiles, off to on and on to off each drop and rebuild), the decision logic only (no device in a test).
Build 0 warnings; far-view and settings classes 77 passed; format clean; full suite 12,788 passed, 0 failed. No game window or GL test run.
Gate (about 09:35 UTC): `277443de`, 12,787 passed, 1 failed (the sample-town wall-clock test, passing alone in 5 s): kept.
