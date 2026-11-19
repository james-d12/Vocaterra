You are implementing B40, "Chunk speed 6: the small ones", for the Vocaterra repository (C#, .NET 10; the game is
Vocaterra.Game.Core, Vocaterra.Game, Vocaterra.Host; the core Vocaterra.Nlp* is not yours). Do the work yourself; do not spawn subagents.
Where to work: the worktree /home/james/Dev/Vocaterra/.claude/worktrees/b40 (absolute; cd there first), on the local branch
`B40-small-ones` (already created there, cut from `feature/world-next` at @@BASE@@). Commit after each step that builds and passes, prefix `B40: `. Do not push;
never push or merge feature/world-next or main. Scratch under /home/james/Dev/Vocaterra/artifacts/tmp/b40/, removed at the end.
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
sections 1 (allocation, thread scaling) and 3 (#8 to #11), .claude/briefs/b36-report.md and b39-report.md (both short), the B36 and B39
entries in docs/game/findings.md, and your row: docs/game/implementation-plan.md section 4.14, B40. The bench is
`scripts/limited.sh dotnet run -c Release --project tools/Vocaterra.WorldPreview -- bench` (docs/game/running.md).

Deliverables, each its own commit, each measured before and after:
0. **The bench's group rows first.** B39 added "residency column through a 4x4 group" and "ring footprint through one window" to the bench;
   one `--quick` run read about 8 times higher than B39's own scratch measurement (residency 0.62 ms, footprint 5.6 ms). Find out why (warm-up,
   counting a whole group's window per column, quick mode's few passes) and fix the rows so they agree with a careful measurement.
1. **Allocation (#8):** pool the window arrays through caller-owned scratch (B39's `ColumnGroup` row copies and per-column windows;
   and B36's `FarScope` fixed-size tables, 1 to 2 MB a stack on the large-object heap, **only if B37 is on feature/world-next when you get
   there**: B37, beside you, is changing `FarScope` and `LodModelSource`; otherwise leave `FarScope` and say so). No static or `[ThreadStatic]` cache unless the
   `ArchitectureRulesTests` allow-list gains a line with the reason, as `DrainageNetwork` has. Report bytes per column and gen-0
   collections a second at 8 workers before and after. **Do not switch to server GC**: it raises memory and is the owner's choice. A
   gen-0 budget setting (`GCgen0size` in the game's runtimeconfig) is allowed if it measurably helps and memory stays within a stated bound.
2. **Worker priority (#9):** check first, on this machine, whether `ThreadPriority.BelowNormal` (src/Vocaterra.Game/Streaming/JobPool.cs)
   changes the worker threads' nice value on Linux (read /proc/<pid>/task/*/stat during a run). If it does nothing, set nice 5 to 10 on the
   worker threads through `setpriority` (libc P/Invoke, no package; Linux only, a no-op elsewhere), and show the values after.
3. **Drainage warmed ahead (#10):** warm the landmasses within the far range at start and on a teleport, on the workers, and give each
   landmass its own lock instead of one global lock (first reads are 0.12 to 0.53 s each, now serialised). The network must be the same
   whoever reads it first (a test with two orders). The architecture test's lock allowance must cover the new locks with a reason.
4. **`HighestBlock` (#11):** `VoxelGrid.HighestBlock` builds a whole window per layer from the top down on the game thread (up to about 15
   air chunks, 30 to 70 ms per teleport in Release). Answer it from the surface band (or one `GenerateLayers`) instead; same answers (a test
   over a sample of columns on seeds 42 and 2026, with edits).
Report, half a page: branch, head, worktree; per item what changed and the figures before and after; the nice values; the tests; anything
left. Text for docs/game/running.md if a flag or setting changed. No file dumps.
