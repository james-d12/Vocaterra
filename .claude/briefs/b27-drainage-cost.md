You are implementing B27, "The drainage network's cost", for the Vocaterra repository (C#, .NET 10). The game is Vocaterra.Game.Core (the
engine-free logic), Vocaterra.Game (MonoGame) and Vocaterra.Host; the language core (Vocaterra.Nlp*) is not yours. A performance milestone:
make world generation cheaper again **without changing a single block**.
Where to work: the worktree /home/james/Dev/Vocaterra/.claude/worktrees/b27 (an absolute path; cd there first), already on the local branch
`B27-drainage-cost` (cut from `feature/biomes` at 673fbbf2). Commit after each step that builds and passes its tests, with the prefix `B27: `.
Do not push. Never push or merge feature/biomes or main. Leave the worktree clean on its branch.
Resources and waiting: every dotnet restore, build, test and format through scripts/limited.sh (builds `-m:4`, tests `-m:2`); one heavy
command at a time, no `&`. Run a long command with the Bash tool's `run_in_background: true`, end your turn and wait for its notification;
never poll with `sleep` or `until` loops, never `nohup ... &`. Other agents build at the same time: expect waits. Iterate with
`scripts/test.sh fast` plus the classes you touch; merge feature/biomes into your branch before your one `scripts/test.sh full`, and report
only after it finishes. Scratch under /home/james/Dev/Vocaterra/artifacts/tmp/b27/ (not /tmp), removed at the end. Environment: prefix every
command with PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1. No game windows are needed (a Release `--stream-stats`
run is optional: `scripts/limited.sh timeout 900 ...`).

Why: B24's CPU timing tests found that B02c (rivers that only fall: `src/Vocaterra.Game.Core/World/Drainage/DrainageNetwork.cs`, its use
in `HeightModel` and `WorldQuery`) made chunk generation about 2.5 to 3 times dearer: a surface chunk 6.4 to 6.9 ms of CPU before, 17 to 21
after; the valley snapshot 2.7 to 5.9 s; it also made the test suite much longer (docs/research/2026-11-17-ai-development-throughput.md,
section 2.3). Your row: docs/game/implementation-plan.md section 4.14, B27. Read .claude/briefs/b02c-report.md (the design: a priority
flood over 32-block nodes, each landmass read whole on first touch, 0.25 to 0.9 s; `IsRead`, `IsWarm`, the dry scope, the lock) and
b24-report.md (the CPU budgets).

Rules: a game milestone edits only the game projects, their tests, content/game-v1, content/schema, assets/ and docs/game/ and takes the
core as it is. Never edit .github/workflows/, either implementation-plan.md, either decisions.md or docs/game/biomes-plan.md. **The world
does not change:** the 18 procedural goldens, `SceneTests.GoldenDigest`, the save-v9 fixture's state digest and the atmosphere pins must not
move; if one does, the change is wrong. The drainage code stays pure (B24's purity test: IntMath only, no floats, clocks or randomness; its
one `lock` allowed by name); no new threads. Keep the per-frame look-up allocation-free (B21's test, B02c's test).

Deliverables:
1. **Profile:** where the extra cost goes, per surface chunk and per landmass read (a CPU-time breakdown: the per-column calls into the
   network, `IsRead`'s checks, `Shape`, node look-ups, the flood itself, dictionary or lock overhead). Say the numbers.
2. **Fix it:** cache or restructure so the per-chunk cost comes back near B02c's base (for example: resolve the network's nodes for a chunk
   once and reuse them for its 256 columns; replace a dictionary on the hot path with an array per landmass; avoid repeated `IsRead`
   checks; precompute per-node values the column shape needs). Byte-identical results.
3. **Budgets:** lower B24's CPU budgets (the procedural chunk, streamed-world and snapshot timing tests) to twice the new measured cost, so a
   later regression is caught.
4. **Measured:** the procedural chunk, column-of-24, band and snapshot CPU costs before and after; the full suite's Game.Core.Tests and
   Game.Tests times before and after (note the load).

Gate: build with -warnaserror (0 warnings); scripts/test.sh full (12,568 passed on feature/biomes at your base, plus your new tests); format
clean; content validate. Commit messages end with
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDbkPTqABsu7sLVD9h6

Report, a few lines: branch, head commit, worktree; where the time went; the fix; the costs before and after; the new budgets; the suite's
count and times; anything left. No file dumps or long logs.
