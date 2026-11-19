You are implementing B38, "Chunk speed 4: no double builds", for the Vocaterra repository (C#, .NET 10; the game is
Vocaterra.Game.Core, Vocaterra.Game, Vocaterra.Host; the core Vocaterra.Nlp* is not yours). Do the work yourself; do not spawn subagents.
Where to work: the worktree /home/james/Dev/Vocaterra/.claude/worktrees/b38 (absolute; cd there first), on the local branch
`B38-no-double-builds` (already created there, cut from `feature/world-next` at 62e3f192). Commit after each step that builds and passes, prefix `B38: `. Do not push;
never push or merge feature/world-next or main. Scratch under /home/james/Dev/Vocaterra/artifacts/tmp/b38/, removed at the end.
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
sections 1, 2 (items 2 and 7) and 3 (#4, #7), .claude/briefs/b35-report.md, b36-report.md and b39-report.md (all short), the B35, B36 and
B39 entries in docs/game/findings.md, and your row: docs/game/implementation-plan.md section 4.14, B38. Two wastes: far tiles at levels 0
to 2 inside the real radius (about 610 tiles at High) are built from the model and then built again when the ring makes their footprints
real; and the ring regenerates the chunks the voxel zone already holds (about 16% of footprints at High). Files: src/Vocaterra.Game/Graphics/Far/FarView.cs
(B35 added the stand-ins and coarse-before-fine ordering there), src/Vocaterra.Game/Streaming/LodRing.cs (B39 gave the ring one window per
footprint). B40 runs beside you in src/Vocaterra.Game/Streaming and the generator (pooled window arrays, worker priority, drainage,
`HighestBlock`): keep your LodRing changes to scheduling and ingest, and leave window allocation to B40.

Deliverables:
1. **No model-built fine tiles inside the real radius:** a level 0 to 2 leaf whose footprint the ring will make real shows its stand-in
   (B35) until the ring's data lands, then is built once from the real data. Ring footprints get the priority of the tiles that wait for
   them (today the ring gets 1 job in 5 while tiles queue), so the detail arrives nearest first. Outside the real radius nothing changes.
2. **The ring ingests resident chunks:** a footprint whose chunks are already resident in the voxel zone takes them from the zone instead of
   generating them again (same bytes; the ring's store data identical to what it would have made, proved by a test).
3. Tests: inside the real radius each fine tile is built exactly once and only from real data; footprints are ordered by the waiting
   tiles' distance; ingesting resident chunks gives the same stored sections as generating them; no leaf is ever uncovered (B35's tests
   keep passing).
4. **The blue line:** B35's after picture (/home/james/Dev/Vocaterra/artifacts/b-series/b35/after-b.png, top left) shows a thin blue vertical
   line over a stand-in's edge that the before picture lacks. Find whether it is a crack between a stand-in and a neighbour (sky showing
   through) and fix it if it is in the tiles you touch; otherwise say what it is.
5. Measure (Release, High, 16 km, seed 42, 8 workers, a fresh far store in a temporary folder, `--stream-stats`, which prints a
   `far coverage:` line since B35): coverage and full fill time, and the far tiles built, before and after; run before and after back to back.
   Two pictures in /home/james/Dev/Vocaterra/artifacts/b-series/b38/ (index.md) at about 10 and 30 s after a fresh start. Look at them.
Report, half a page: branch, head, worktree; what changed; tiles built and times before and after; the blue line; the tests; anything left.
No file dumps.
