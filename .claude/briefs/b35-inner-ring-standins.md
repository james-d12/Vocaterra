You are implementing B35, "Chunk speed 1: stand-ins for the inner ring", for the Vocaterra repository (C#, .NET 10; the game is
Vocaterra.Game.Core, Vocaterra.Game, Vocaterra.Host; the core Vocaterra.Nlp* is not yours). Do the work yourself; do not spawn subagents.
Where to work: the worktree /home/james/Dev/Vocaterra/.claude/worktrees/b35 (absolute; cd there first), on the local branch
`B35-inner-ring-standins` (cut from `feature/world-next`). Commit after each step that builds and passes, prefix `B35: `. Do not push;
never push or merge feature/world-next or main. Scratch under /home/james/Dev/Vocaterra/artifacts/tmp/b35/, removed at the end.
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

Why: the owner's top priority is chunk generation's speed (D-game-34 items 52 and 61: "this is the biggest issue right now and we need to
get it right"). On a fresh world the far view shows a sky-coloured ring between the near zone and the distant hills for most of a 2 to 3
minute fill. Read docs/research/2026-11-18-chunk-generation-speed.md sections 1, 3 (#2) and 4 ("The sky gap"), .claude/briefs/b07e-report.md
and b07d-report.md, and your row: docs/game/implementation-plan.md section 4.14, B35. The cause: coarse-first ordering builds the horizon
(level 4 and up) first; levels 0 to 3 (about 384 to 1,800 blocks at High) come last at 1 to 2.2 s per cold tile; B07e's stand-ins
(src/Vocaterra.Game/Graphics/Far/FarView.cs, `FallbackSteps`) draw only ancestors already built, and on a fresh world nothing builds the
ancestors of those inner leaves.

Deliverables:
1. For each far leaf with no built ancestor within `FallbackSteps`, request its level-4 ancestor (or the nearest coarser level that
   covers it, whichever the code's structure makes natural; say which) at coarse priority, so the stand-in exists within seconds. Keep it
   rendering only: no change to the world, the far store's section data or any pin. Requested stand-ins that are no longer needed are
   dropped like other tiles (no leak; the tile count and memory stay bounded).
2. Tests (C#, in the far view's test classes): on a fresh store every visible leaf is covered by a drawn tile or a requested ancestor
   from the first frames; the stand-in requests come ahead of fine tiles; nothing is requested twice.
3. Measure (Release, High, 16 km, a fresh far store in a temporary folder, seed 42, `--stream-stats` or the code's own counters): the time
   from start until no visible leaf is uncovered, before and after; the whole fill time before and after (it should not grow by more than
   a few per cent). Two pictures in /home/james/Dev/Vocaterra/artifacts/b-series/b35/ (index.md): about 5 s after a fresh start, before
   and after. Look at them.
Report, half a page: branch, head, worktree; what changed; the tests; the times before and after; the pictures; anything left. No file dumps.
