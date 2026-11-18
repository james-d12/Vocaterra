You are implementing B07d, "The far view's remainder", for the Vocaterra repository (C#, .NET 10). The game is Vocaterra.Game.Core (the
engine-free logic), Vocaterra.Game (MonoGame DesktopGL 3.8.6) and Vocaterra.Host; the language core (Vocaterra.Nlp*) is not yours.
Where to work: the worktree /home/james/Dev/Vocaterra/.claude/worktrees/b07d, already on the local branch `B07d-far-remainder` (cut from
`feature/biomes` at a8560f35). Run every command from that directory. Commit after each step that builds and passes its tests, with the
prefix `B07d: `. Do not push. Never push or merge feature/biomes or main. Leave the worktree clean on its branch.
Resources: every dotnet restore, build, test and format, every game run and screenshot through scripts/limited.sh (builds `-m:4`, tests
`-m:2`; the script's caps); one heavy command at a time, no `&`, no wait loops on `pgrep -f` or on a file (a loop left behind runs forever:
two agents left them today). Other agents build at the same time: expect waits. Iterate with a project or `scripts/test.sh fast`;
`scripts/test.sh full` once at the end. Scratch under /home/james/Dev/Vocaterra/artifacts/tmp/b07d/ (not /tmp), removed at the end.
Environment: prefix every command with PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1; game runs need DISPLAY=:0
and VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models; scripted runs use
`VOCATERRA_RUNS_WORK=/home/james/Dev/Vocaterra/artifacts/tmp/b07d/runs`. Game windows are allowed (the owner is not at the screen); look
at every picture with the Read tool. Measure and take pictures in a Release build.

Why: the owner wants the far view to work like the Voxy mod (D-game-34 item 29; Voxy's code is All Rights Reserved: ideas only, never copy
it or fetch its source), asked for all the review fixes (item 37), and on 2026-11-17 decided the game's worker threads (item 42): **"Yeah,
auto is fine. I mean, perhaps we should allow that to be configured in the game as well."** And every graphics setting goes in a Graphics
group of the settings page (item 43). Your row: docs/game/implementation-plan.md section 4.14, B07d.

Read first: CLAUDE.md; docs/game/decisions.md D-game-34 items 29, 37, 42 and 43; docs/game/biomes-plan.md sections 3.7 and 3.10;
docs/research/2026-11-16-rendering-and-distance-review.md (H5, R1, the near planes, D1) and docs/research/2026-11-16-codebase-review.md
section 4 (P3, P4; line numbers have moved, find the code by name); docs/game/architecture.md (threads, streaming, the far view);
.claude/briefs/b07c-report.md, b07e-report.md, b07f-report.md, b21-report.md (the job pool's lanes: residency, mesh, tiles, ring).

Rules: a game milestone edits only the game projects, their tests, content/game-v1, content/schema, assets/ and docs/game/ and takes the
core as it is. A regression check is a C# test. Never edit .github/workflows/, either implementation-plan.md, either decisions.md or
docs/game/biomes-plan.md (say in the report what they should say). The world does not change (the goldens and the fixture stay); the
far view's picture for the same settings stays the same. Game.Core stays pure (no threads, no file IO beyond what is already allowed: B24
is adding an architecture test for that at the same time). Another agent (B17b) is adding a "Shaders" row and creating the Graphics group
on the settings page now: put your rows in a Graphics group too; whoever merges second joins the two.

Deliverables:
1. **Worker threads (item 42):** a "Worker threads" setting, Auto or 1 to 8, on the settings page's Graphics group, saved, and a launch
   option (`--workers auto|N`); Auto is the default for play: processor count minus 4, at least 1, at most 8. `JobPool.MaxWorkers` becomes
   the setting's bound (8). Scripted runs, tests and `--prefetch` keep their current small counts (scripted runs and tests stay at 2 or
   inline, so their results and the machine's limits do not change). A change in play takes effect without losing chunks (rebuild the
   pool between frames, or say it needs a restart). Tests: Auto's arithmetic, the clamp, scripted runs unchanged, the world identical
   at 1, 2 and 8 workers.
2. **The detail settings** on the settings page (the far view's distance and detail, whatever `FarViewSettings` and the presets expose),
   in the Graphics group, saved and as launch options.
3. **A world pre-generation command:** `--pregen <radius>` (or a host or tool command) that fills the far store for a seed round a point
   using all the workers, with progress, and exits; a run after it opens with the far view complete. Say the time for 4 km and 16 km.
4. **The far store's memory and IO** (P3, P4, H5, R1): a bounded LRU of real sections with a cap, reloaded from disk when needed; the
   ring's disk checks off the game thread; fsync on a timer, not whenever the write queue runs dry; region compaction; pooled tile arrays;
   no put-then-get round trip; a surface-only ring if it holds the picture; the near-plane fixes. Tests for the LRU's cap and reload, the
   ring planning on a worker, the fsync timer, compaction keeping every section.
5. **Measured** (Release, High, `--stream-stats`, interleaved A/B runs because other agents load the machine): fill time of the 16 km
   far view after a start and after a teleport at 2, 4 and 8 workers; the game thread's p50, p95, p99; memory after a long walk (before:
   grows; after: flat under the cap); hitches from disk reads. Pictures in /home/james/Dev/Vocaterra/artifacts/b-series/b07d/ with an
   index.md: the same far view at 2 and 8 workers after the same wait, and the settings page's Graphics group.
6. **Documents:** docs/game/running.md (the settings, `--workers`, `--pregen`), docs/game/architecture.md (the store, the threads), the
   Game README.

Gate: build with -warnaserror (0 warnings); scripts/test.sh full (12,531 passed on feature/biomes before, plus your new tests); format
clean; content validate. A timing test that fails under load: rerun it alone and say so. Commit messages end with
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDbkPTqABsu7sLVD9h6

Report, half a page: branch, head commit, worktree; each deliverable and how; the measurements (fill time by worker count); the
pictures; what the plans should say; anything left. No file dumps or long logs.
