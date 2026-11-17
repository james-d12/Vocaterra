You are implementing B21, "Meshing off the game thread", for the Vocaterra repository (C#, .NET 10). The game is Vocaterra.Game.Core
(the engine-free logic), Vocaterra.Game (MonoGame DesktopGL 3.8.6) and Vocaterra.Host; the language core (Vocaterra.Nlp*) is not yours.
This milestone's work is the near world's remesh path and the streaming round it: src/Vocaterra.Game/Graphics/World (`VoxelRenderer`'s
remesh, `ChunkMesher.Build` and its neighbourhood snapshot, `ChunkViewSet`, `GrassMesher`'s call site), src/Vocaterra.Game/Streaming
(`ChunkResidency`, `StreamedWorldHost.ApplyPreset`, `JobPool`), src/Vocaterra.Game.Core/Voxel/VoxelGrid.cs (`HighestBlock`) and its callers
(`Land/LandRules.cs`, `Land/CampPlan.cs`), `World/Streaming/StreamedWorld.cs` and `World/PristineChunks.cs` (`ContentOf`), and their tests.
Where to work: the worktree /home/james/Dev/Vocaterra/.claude/worktrees/b21, already on the local branch `B21-meshing-off-thread` (cut from
`feature/biomes` at __BASE__). Run every command from that directory. Commit after each step that builds and passes its tests, with the
prefix `B21: `. Do not push. Never push or merge feature/biomes or main. Leave the worktree clean on its branch.
Resources: every dotnet restore, build, test and format, every game run and screenshot through scripts/limited.sh (builds `-m:4`, tests
`-m:2`; the caps are the script's defaults: 10 GB a command, 16 GB in all, three commands machine-wide); one heavy command at a time, no
`&`, no wait loops on `pgrep -f`. Four other agents build at the same time: expect waits. Iterate with a project or `scripts/test.sh fast`;
`scripts/test.sh full` once at the end. Scratch on disk under /home/james/Dev/Vocaterra/artifacts/tmp/b21/ (not /tmp, a small tmpfs),
removed at the end. Environment: prefix every command with PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1; game
runs need DISPLAY=:0 and VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models; scripted runs use
`VOCATERRA_RUNS_WORK=/home/james/Dev/Vocaterra/artifacts/tmp/b21/runs`. Game windows are allowed (the owner is asleep); look at every
picture with the Read tool. Measure and take pictures in a Release build.

Why: the owner asked for an Opus review of the codebase and performance, then said "go ahead with all the fixes recommended from review"
(D-game-34 item 37). Read docs/research/2026-11-16-codebase-review.md sections 1 and 4. Its findings for you (the line numbers were taken at
`30f104cb` and have moved; find the code by name):
- **P1 (major). Chunk meshing runs on the game thread:** `VoxelRenderer`'s remesh calls `ChunkMesher.Build` (and `GrassMesher.Build`)
  inline, 3 to 5 meshes a frame within a 4 to 6 ms budget, so a High view of up to 24k meshes fills over minutes. The mesher already reads
  through an 18^3 neighbourhood snapshot: take the snapshot on the game thread (or from the pure source on a worker, if the chunk and its
  neighbours are unchanged since it was queued), mesh on the job pool, upload the vertex and index buffers on the game thread within a
  budget. Lights (`_lights.Near`), sky exposure and the grey kinds that `Build` takes must be captured with the snapshot, so a worker reads
  no mutable game state.
- **P2 (major). Every edit rescans everything:** `ChunkViewSet.Update` re-sorts all ready chunks, and the remesh re-checks every wanted
  chunk's key (seven lookups each) on each block change and each water-flow tick (an estimated 3 to 10 ms a frame at High). Mark dirty from
  a block-change listener (the chunk and the neighbours a border cell touches); re-sort only when the eye changes chunk or the view changes.
- **P5. Per-frame allocations** in residency and remesh (closures, lists, a boxed `First()`, a listener `ToArray()` per install or evict,
  chunk copies in `ContentOf`): about 1 to 2 MB/s of short-lived garbage. Remove them from the per-frame paths (pooled or reused buffers,
  no LINQ in the frame loop); show the allocation rate before and after (`GC.GetAllocatedBytesForCurrentThread` or a counter in
  `--stream-stats`).
- **C4.** A jump (teleport) clears `ChunkResidency._inFlight` while old packages are still flying, so a late package can install a chunk
  the new position no longer wants or double-count. Old packages must be recognised (an epoch per jump) and dropped or used safely.
- **C5.** `StreamedWorldHost.ApplyPreset` replaces the residency while jobs are in flight; a quality change must not lose, duplicate or
  half-install chunks. Keep this change small: another agent (B07f) is changing `StreamedWorldHost`, `LodEdits` and `LodRing` now.
- **C11.** `VoxelGrid.HighestBlock` installs air chunks on the game thread on a streamed grid (callers `LandRules`, `CampPlan`); it must
  read without installing (the pure source for unresident columns, or the resident chunks only, whichever the callers need: say which).
  A related suspicion in the review: simulation reads outside the resident set generate chunks synchronously; check and report.

Read first: CLAUDE.md; docs/game/implementation-plan.md sections 1, 3, 4.14 (your row, B21) and 6; docs/game/architecture.md (threads and
streaming); the review above; .claude/briefs/b07-streamed-world.md and b07-phase-b2b-report.md (the job pool's two workers, the band
residency and why workers touch only the pure source); .claude/briefs/b07e-report.md (the far view's undrawn-column hand-off, which reads
what the near renderer drew: keep it right when meshing becomes asynchronous). Other agents are changing nearby code now: B22 (ChunkMesher's
face light and colours, GrassMesher's tint, VoxelRenderer's constructor), B07f (Streaming's LOD parts, StreamedWorldHost), B17a
(VoxelRenderer's opaque pass behind `--gl-shaders`), B02c (rivers), B20 (the valley's snow). Keep the mesher's output unchanged (same
vertices for the same input); before you report, merge feature/biomes into your branch (it will hold some of theirs) and resolve.

Rules: a game milestone edits only the game projects, their tests, content/game-v1, content/schema, assets/ and docs/game/ and takes the
core as it is. A regression check is a C# test. Never edit .github/workflows/, either implementation-plan.md, either decisions.md or
docs/game/biomes-plan.md. Engine code only in src/Vocaterra.Game; Game.Core stays pure (no threads there; the job pool is the game's). The
world does not change: the 18 procedural goldens, `SceneTests.GoldenDigest` and the fixture's state digest must not move, and the scripted
runs' pictures should not change (say if one does, and why). The two-worker rule stands (the pool's two workers; no `Parallel.For`, no new
threads). The bounded valley world (`--world valley`) must work the same way.

Deliverables:
1. P1: meshing on the job pool with uploads on the game thread under a per-frame budget; a mesh built from an older snapshot than the
   chunk's current version is refused and requeued (a version per chunk, bumped by edits and by neighbour edits on borders); the nearest
   dirty chunks first; an edit near the player is visible next frame or the one after (an edit's own chunk may mesh synchronously if that is
   what keeps it immediate: say what you chose). Tests: the same input gives byte-identical meshes on a worker and inline; a mesh made
   stale by an edit mid-build is refused; many edits while jobs fly end with every chunk's mesh matching its blocks.
2. P2: dirty marking from block changes and flow ticks; no full re-sort or full key scan per edit (a test that counts the work, or a
   bound on calls per edit).
3. P5: no per-frame allocation in the steady state of residency and remesh (a test with an allocation counter over a stretch of frames on
   a fake device, if possible; else the measured rate).
4. C4, C5, C11 as above, each with a test (a jump with packages in flight; a preset change with jobs in flight; `HighestBlock` on a
   streamed grid installs nothing).
5. Measurements (Release, the B07 walk along the pass road and a teleport to a far point, at Medium and High, with `--stream-stats`, before
   on your base and after): frame p50, p95, p99; the game thread's remesh time per frame; the time to fill the view after a teleport; the
   allocation rate; say the machine's load at the time (other agents run).
6. Pictures in /home/james/Dev/Vocaterra/artifacts/b-series/b21/ with an index.md: the same views before and after (a village street, the
   streamed world from a hill, a cave with a torch, an edit: a block broken and placed), showing no holes, no stale chunks and no flicker
   after edits; look at each.
7. Documents: docs/game/architecture.md (the threads), the Game README (the remesh path), docs/game/running.md if a setting or stat is added.

Gate: build with -warnaserror (0 warnings); scripts/test.sh full (12,451 passed on feature/biomes before, plus your new tests); format
clean; content validate. A timing test that fails under load: rerun it alone and say so. Commit messages end with
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDbkPTqABsu7sLVD9h6

Report, half a page: branch, head commit, worktree; each finding fixed and how; the tests; the measurements; the pictures; anything left.
No file dumps or long logs.
