You are implementing B36, "Chunk speed 2: the far sampler shares its work, and a generation bench", for the Vocaterra repository (C#,
.NET 10; the game is Vocaterra.Game.Core, Vocaterra.Game, Vocaterra.Host; the core Vocaterra.Nlp* is not yours). Do the work yourself; do
not spawn subagents. Where to work: the worktree /home/james/Dev/Vocaterra/.claude/worktrees/b36 (absolute; cd there first), on the local
branch `B36-far-sampler-sharing` (cut from `feature/world-next`). Commit after each step that builds and passes, prefix `B36: `. Do not
push; never push or merge feature/world-next or main. Scratch under /home/james/Dev/Vocaterra/artifacts/tmp/b36/, removed at the end.
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

Why: the owner's top priority is chunk generation's speed (D-game-34 items 52 and 61). A review measured it with a throwaway program, since
deleted: docs/research/2026-11-18-chunk-generation-speed.md. Read its sections 0, 1, 2 (item 1), 3 (#1) and 5, and your row:
docs/game/implementation-plan.md section 4.14, B36. Model stacks are about 90% of the far view's CPU. `FarTerrainSampler.At`
(src/Vocaterra.Game.Core/World/Far/FarTerrainSampler.cs) costs 50.9 µs a column: climate from four lattice corners with nothing reused
(`Climate.At` is 34 times `Climate.Fill` per column), `SurfaceRock.Adjust` reads 8 neighbours each through a new `FarGround` that
recomputes climate and shape, `TryRiver` samples the centre again plus 3 `Climate.At`, and `Even` adds 8 more over water. At levels 1
and 2 samples are 1 to 2 blocks apart, so each lattice point is evaluated dozens of times. The review's prototype (dictionaries cleared per
stack) measured 8 to 14 times on levels 0 to 2 and 3.3 on level 3 with byte-identical sections.

Deliverables, in this order:
1. **A committed generation bench**, so this and the later speed milestones (B37 to B40) are measured the same way: a `bench` command in
   tools/Vocaterra.WorldPreview (preferred; or a small tool project beside it if that is cleaner; say why). Seed 42 by default, the
   shipped world files, fastest of N passes (default 3), single-threaded, text plus a JSON file. It reports the rows of the review's
   section 1 that Game.Core can produce: surface, deep and air chunk; whole column; residency column (band, then layers); ring footprint
   (32x32 blocks); far model stack per level 0 to 7 (land and sea positions); the per-stage split of a column (climate fill, shape,
   shapers, plans, band and deep passes, strata, caves); and FarTerrainSampler.At per column. A `--quick` mode under about 30 s. It
   is a tool, not a test: no timing assert, not in the suite (a smoke test that it runs in quick mode with tiny N is fine). Run it through
   limited.sh. Record the baseline on your base commit before item 2.
2. **Digests first:** a test that records digests of packed far sections (several positions on land, sea, river, cliffs; levels 0 to 7; at
   least seeds 42 and 2026) from the current code, committed before the change, so the change is proved byte-identical.
3. **The sharing:** per stack (or per tile), climate from a lattice table (`Fill`), and each column's climate and shape computed once and
   reused by the cliff test, `TryRiver` and `Even`, in per-call arrays (caller-owned scratch is fine; no static or `[ThreadStatic]`
   cache). Same integer arithmetic; the digests from item 2 unchanged; every existing far and world test passes unchanged. Keep changes
   to shared types (`Climate`, `HeightModel`, `FarGround`) additive: B39 works on the chunk window beside you.
4. Measure with the bench: before and after per level; the share of far CPU left at levels 3 to 7 (B37's target). If you can, one Release
   game run with a fresh far store at High (16 km, seed 42, `--stream-stats`): the fill time before and after.
Report, half a page: branch, head, worktree; the bench's command line and its output before and after (the table, not the JSON); the
digest test; the design of the sharing; anything left. The text for docs/game/running.md on the bench command. No file dumps.
