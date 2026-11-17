You are implementing B24, "Guards and cleanups", for the Vocaterra repository (C#, .NET 10). The game is Vocaterra.Game.Core (the
engine-free logic), Vocaterra.Game (MonoGame DesktopGL 3.8.6) and Vocaterra.Host; the language core (Vocaterra.Nlp*) is not yours.
Where to work: the worktree /home/james/Dev/Vocaterra/.claude/worktrees/b24, already on the local branch `B24-guards-cleanups` (cut from
`feature/biomes` at BASE). Run every command from that directory. Commit after each step that builds and passes its tests, with the
prefix `B24: `. Do not push. Never push or merge feature/biomes or main. Leave the worktree clean on its branch.
Resources: every dotnet restore, build, test and format through scripts/limited.sh (builds `-m:4`, tests `-m:2`; the script's caps); one
heavy command at a time, no `&`, no wait loops on `pgrep -f` or on a file (a loop left behind runs forever: two agents left them today).
Other agents build at the same time: expect waits. Iterate with a project or `scripts/test.sh fast`; `scripts/test.sh full` once at the
end. Scratch under /home/james/Dev/Vocaterra/artifacts/tmp/b24/ (not /tmp), removed at the end. Environment: prefix every command with
PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1. No game windows are needed.

Why: the owner said "go ahead with all the fixes recommended from review" (D-game-34 item 37). Read docs/research/2026-11-16-codebase-review.md
sections 1 (C8), 2 (A1, A2), 3 (T1, T2) and 7 (docs and data); line numbers there were taken at `30f104cb` and have moved: find the code
by name. Since the review: B21 moved meshing onto the job pool, B24a took the valley underground to two threads and its timing test onto
CPU time, B07f added the far store's edit layer with race tests, B02c added the drainage network (its `WorldQuery` answers dry until a
landmass is read; `HeightModel.Warm` is no longer called from `src`). Your row: docs/game/implementation-plan.md section 4.14, B24.

Read first: CLAUDE.md; docs/game/architecture.md; docs/process/code-guidelines.md; docs/process/test-performance-plan.md (`TestSpeed`);
the review sections above; tests/Vocaterra.Game.Core.Tests' `ArchitectureRulesTests` and `ProceduralWorldPurityTests`; the reports
.claude/briefs/b21-report.md, b24a-report.md, b07f-report.md.

Rules: a game milestone edits only the game projects, their tests, content/game-v1, content/schema, assets/ and docs/game/ and takes the
core as it is. A regression check is a C# test. Never edit .github/workflows/, either implementation-plan.md, either decisions.md or
docs/game/biomes-plan.md (say in the report what they should say). The world does not change: the 18 procedural goldens,
`SceneTests.GoldenDigest` and the save-v9 fixture's state digest stay. **Never weaken a test to make it pass**: a timing test made robust
must still catch a real slowdown (say what it catches).

Deliverables:
1. **A1, purity guards:** `ProceduralWorldPurityTests` (or its successor) covers every folder that feeds the world's blocks (all of
   `World/Generation`, `World/Drainage`, the underground, the regions' layouts, whatever the generator reads), not only `Procedural/`.
   Game.Core is banned from threads, `Parallel`, `ThreadPool`, `Task.Run`, timers, file IO and environment reads by an architecture test
   with an explicit allow-list (today's legitimate users, each with a one-line reason: for example the turn worker's thread, the save's
   file IO if it lives there; B24a's two-thread `Lay` if it stays). Fix any use that is not legitimate rather than allow-listing it.
2. **A2, dead code:** remove `World/Streaming/ChunkStreamer.cs`, `VoxelGrid.ResidentCoordinates` (tests only) and whatever else the review
   names, if still unused (check with a search); `HeightModel.Warm` if nothing but tests call it and the tests do not need it. Say what
   you removed.
3. **C8, the resident cap:** make the resident cap behave as documented (evict as the review says it should), with a test; or, if the
   documented behaviour is wrong, fix the documents and say why.
4. **T1, concurrency tests** for the gaps the review lists that B07f and B21 did not already fill (check their tests first): the two
   ingests under concurrency, save-loaded edits reaching the far store, reconfiguring with jobs in flight. Deterministic where possible
   (run inline, or a controlled scheduler), never sleeping to make a race likely.
5. **T2, timing tests that guard something:** the review's bounds hundreds of times the measured cost (`ProceduralChunkTimeTests`,
   `StreamedWorldTimeTests`) set from the measured cost with a stated margin, measured the way B24a's is (CPU time or work counted, fastest
   of several, in the timing collection that runs alone after the assembly), so they hold under load and still catch a doubling. The same
   for the game tests that failed in today's gates under load: `StreamedWorldTimeTests`' per-chunk budget, `PristineChunksTests`'
   snapshot test (21.6 s at load 17), and B07f's settle tests' hang guards (30 and 60 minutes: shorten to something a hang shows quickly
   without failing under load). `GeneratedLexiconTests.LoadingTheBundleIsFastEnough` is the core's: do not touch it; say what it needs.
6. **Docs and data (section 7):** fix the contradictions the review lists in docs/game/ and the code's comments; list any in
   docs/game/biomes-plan.md for me to apply.

Gate: build with -warnaserror (0 warnings); scripts/test.sh full (COUNT passed on feature/biomes before, plus your new tests, less any
removed with dead code: say how many); format clean; content validate. Commit messages end with
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDbkPTqABsu7sLVD9h6

Report, half a page: branch, head commit, worktree; each deliverable and how; the allow-list; what was removed; each timing test's new
check and numbers; the suite's count; what the plans should say; anything left. No file dumps or long logs.
