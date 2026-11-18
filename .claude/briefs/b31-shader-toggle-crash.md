You are implementing B31, "The crash when Shaders is switched in play", for the Vocaterra repository (C#, .NET 10; the game is
Vocaterra.Game.Core, Vocaterra.Game (MonoGame), Vocaterra.Host; the core Vocaterra.Nlp* is not yours). A small, urgent bug fix: it blocks the
owner's merge of feature/biomes into main (PR #69). Do the work yourself; do not spawn subagents.
Where to work: the worktree /home/james/Dev/Vocaterra/.claude/worktrees/b31 (absolute; cd there first), on the local branch
`B31-shader-toggle-crash` (cut from `feature/biomes` at 4ac7d4b1). Commit after each step that builds and passes, prefix `B31: `. Do not push;
never push or merge feature/biomes or main.
Resources and waiting: every dotnet command through scripts/limited.sh (builds `-m:4`, tests `-m:2`); one heavy command at a time, no `&`;
long commands with the Bash tool's `run_in_background: true`, waiting for the notification; never `sleep`/`until` polls or `nohup ... &`.
Environment: prefix every command with PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1. **The owner is playing the
game on this machine now: open no game window and run no live-GL test (`VOCATERRA_GL_TESTS` unset) unless the orchestrator says so.**

The crash (the owner, 2026-11-18 08:29, switching Shaders on and off in play; report
/home/james/.local/share/vocaterra/sessions/20261118-082911-s1/reports/crash-s1-t3113.md):
`NotSupportedException: Calling GetData on a resource that was created with BufferUsage.WriteOnly is not supported.` at
`VertexBuffer.GetData` in `FarTerrainRenderer.Convert(FarTile tile, GlChunkPass pass)` (src/Vocaterra.Game/Graphics/Far/FarTerrainRenderer.cs,
about line 298), from `FarTerrainRenderer.Draw` / `VoxelRenderer.DrawWorld`. `Convert` carries a far tile built for one path (MonoGame's
effects or the GL program) to the other by reading its buffers back from the card, and the effect path's buffers are created
`BufferUsage.WriteOnly` (lines about 321 to 330 and 404 to 416), so a read-back throws. B17b made the Shaders switch live (the near chunks
re-mesh); B17c and B07d moved the far tiles onto the GL program with pooled arrays (`GlChunkPass.UploadFar`); read their reports
(.claude/briefs/b17b-report.md, b17c-report.md, b07d-report.md).

Rules: a game milestone edits only the game projects and their tests (and docs/game/). The world and saves do not change. Keep the fix small.
Deliverables:
1. Fix: no read-back from the card. On a Shaders change, rebuild the far tiles from their data for the new path (as `FarView.Reconfigure`
   already does for the climate toggle and a change of levels), or keep each tile's CPU-side arrays until it is on the card in both forms
   if that is simpler and bounded in memory; say which. Check both directions (on to off, off to on), and any other read-back of a
   `WriteOnly` buffer in the game (search `GetData`).
2. A test that fails before the fix and passes after, without a game window: for example the far renderer on MonoGame's headless or fake
   device path the tests already use, or a test that switching the path asks for rebuilds and never calls `Convert`'s read-back (if the
   device cannot be made in a test, test the decision logic and say so).
3. Gate: build -warnaserror (0 warnings); `scripts/test.sh fast` plus the far-view and settings test classes; merge feature/biomes if it
   moved; then `scripts/test.sh full` once; format clean. Commit messages end with
   Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
   Claude-Session: https://claude.ai/code/session_01F9jKDbkPTqABsu7sLVD9h6
Report in a few lines: head, the cause, the fix chosen, other read-backs found, the test, the suite count.
