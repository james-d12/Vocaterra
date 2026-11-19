You are implementing B39, "Chunk speed 5: shared windows and the strata pass", for the Vocaterra repository (C#, .NET 10; the game is
Vocaterra.Game.Core, Vocaterra.Game, Vocaterra.Host; the core Vocaterra.Nlp* is not yours). Do the work yourself; do not spawn subagents.
Where to work: the worktree /home/james/Dev/Vocaterra/.claude/worktrees/b39 (absolute; cd there first), on the local branch
`B39-shared-windows-strata` (cut from `feature/world-next`). Commit after each step that builds and passes, prefix `B39: `. Do not push;
never push or merge feature/world-next or main. Scratch under /home/james/Dev/Vocaterra/artifacts/tmp/b39/, removed at the end.
Resources and waiting: every dotnet restore, build, test and format, every game or WorldPreview run through scripts/limited.sh (builds
`-m:4`, tests `-m:2`); one heavy command at a time, no `&`; long commands with the Bash tool's `run_in_background: true`, then end your
turn and wait for the notification; never `sleep`/`until` polls, `pgrep -f` loops or `nohup ... &`. Two other agents build beside you
(the slots in limited.sh queue you; that is expected). Game runs: a Release build, `scripts/limited.sh timeout 900 <command>`, one batch
script rather than many small runs, few and short windows (the owner may be using the machine). Iterate with `scripts/test.sh fast` plus
the classes you touch (`scripts/test.sh project <test project> --filter ...`); **merge `feature/world-next` into your branch before your
one `scripts/test.sh full`**, and run `scripts/test.sh timing` too (this milestone changes performance). Exit 137 is the memory cap:
lower the parallelism, never work around the cap. Environment: prefix every command with PATH="$HOME/.dotnet:$PATH"
DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1; game runs need DISPLAY=:0 and VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models. The
sandbox refuses the words `eval` and `hash` in a command line.
Rules: a game milestone edits only the game projects (src/Vocaterra.Game.Core, src/Vocaterra.Game, src/Vocaterra.Host, tools/Vocaterra.WorldPreview),
their tests and docs/game/, and takes the core (Vocaterra.Nlp*) as it is. A regression check is a C# test. Never edit .github/workflows/,
either implementation-plan.md, either decisions.md, docs/game/biomes-plan.md, README.md, docs/game/running.md or docs/game/architecture.md
(give their text in your report). `World/` stays pure (ArchitectureRulesTests: no clock, no System.Random, no `[ThreadStatic]` or static
caches without an allow-list line, IntMath); determinism unchanged; **never loosen a test's limit or re-record a pin unless this brief
says so**. Do not commit bin/, obj/ or artifacts/.
Gate: build -warnaserror (0 warnings); scripts/test.sh full (the count on feature/world-next at your base, plus your tests); timing tests
pass; format clean; scripts/content.sh validate passes. Commit messages end with
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDc2yqvjCy1ntkujnVS

Why: the owner's top priority is chunk generation's speed (D-game-34 items 52 and 61). Read docs/research/2026-11-18-chunk-generation-speed.md
sections 0, 1 (the per-column stage table), 2 (items 4 to 6) and 3 (#5, #6), .claude/briefs/b21-report.md and b07d-report.md, and your row:
docs/game/implementation-plan.md section 4.14, B39. Each chunk column builds a 42x42-column window (margin 13) for its 16x16 own columns,
so neighbours compute the same climate and shape about 7 times; the window is half a residency column's cost. `SurfaceBand` builds and
shapes a whole window, then `GenerateLayers` builds the same one again (about 23% of residency CPU, 26% of a ring footprint).
`StrataPass` (src/Vocaterra.Game.Core/World/Generation/Procedural/StrataPass.cs) is 40% of decoration, 0.17 ms a deep chunk. Files to
start from: ProceduralChunkSource.cs, StrataPass.cs (same folder), src/Vocaterra.Game.Core/World/Streaming/SurfaceBands.cs, and the
far ring's footprint generation (find it from B07d's report).

Deliverables:
1. Measure first (Release, single-threaded, fastest of 3, seed 42): a residency column, a ring footprint, and the stages above. B36,
   running beside you, adds a `bench` command to tools/Vocaterra.WorldPreview; if it is on feature/world-next when you start measuring
   after (merge it), use it for the after figures and say so; otherwise a scratch program under artifacts/tmp/b39/, deleted at the end.
2. **One context per column:** band and layers from the same shaped window (no second build).
3. **Shared windows:** one shaped window for a group of columns (a 4x4-column group for residency; one window per ring footprint over
   the columns its bands read). Design how jobs pick up a group without delaying the first chunk near the player (the nearest column must
   not wait for a whole group's later layers; say how). Memory per job bounded and stated.
4. **StrataPass:** skip rows whose height band misses, use the lattice minimum and maximum before any per-block work, no
   `Array.IndexOf` per block.
5. Byte-identical: by the invariant that a wider window changes no chunk (the `extraMargin` tests); **the goldens, the save fixture's digest
   and every pin must pass unchanged, with no re-recording**. Add tests that a chunk made through a group window equals one made alone,
   for a sample of columns on seeds 42 and 2026 (edges of groups included).
6. Keep changes to shared types (`Climate`, `HeightModel`) additive: B36 works on the far sampler beside you.
Report, half a page: branch, head, worktree; the figures before and after (residency column, ring footprint, strata); the design of the
groups and the first-chunk latency; the tests; anything left. No file dumps.
