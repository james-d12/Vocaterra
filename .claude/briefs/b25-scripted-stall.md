You are implementing B25, "Scripted runs stall after a torch or a guide dialogue", for the Vocaterra repository (C#, .NET 10). The game is
Vocaterra.Game.Core (the engine-free logic), Vocaterra.Game (MonoGame DesktopGL 3.8.6) and Vocaterra.Host; the language core
(Vocaterra.Nlp*) is not yours. This is a debugging milestone: find the cause, fix it, guard it with a test.
Where to work: the worktree /home/james/Dev/Vocaterra/.claude/worktrees/b25 (an absolute path; cd there first), already on the local branch
`B25-scripted-stall` (cut from `feature/biomes` at 0d72b26b). Commit after each step that builds and passes its tests, with the prefix
`B25: `. Do not push. Never push or merge feature/biomes or main. Leave the worktree clean on its branch.
Resources: every dotnet restore, build, test and format, every game run and scripted run through scripts/limited.sh (builds `-m:4`, tests
`-m:2`; the script's caps); one heavy command at a time, no `&`, no wait loops on `pgrep -f` or on a file (a loop left behind runs forever:
three agents left them today). Bound every game run with `timeout` (for example `timeout 900`) so a stall ends. Other agents build at the
same time: expect waits. Iterate with a project or `scripts/test.sh fast`; `scripts/test.sh full` once at the end. Scratch under
/home/james/Dev/Vocaterra/artifacts/tmp/b25/ (not /tmp), removed at the end. Environment: prefix every command with
PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1; game runs need DISPLAY=:0 and
VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models; scripted runs use
`VOCATERRA_RUNS_WORK=/home/james/Dev/Vocaterra/artifacts/tmp/b25/runs`. Game windows are allowed (the owner is not at the screen); look at
every picture with the Read tool.

The bug (seen by two agents today, reports in .claude/briefs/b21-report.md "Left" and b17b-report.md "Risk"):
- B21: in the streamed world a scripted `--place antorcha@467,61,153` shows no torch; and after a guide dialogue opens, `--wait` never
  returns.
- B17b: B17a's cave view (`scripts/gl-compare.sh`'s cave, which places two torches beside the camera and then waits; it worked when B17a
  was gated at `1fe71da5`) now stalls at `--wait`, with or without `--gl-shaders`: the cave run ran over 20 minutes, r42-camp 8 minutes
  before it was killed. r42-camp, r01-route and r40-inn could not be compared.
Suspects merged between `1fe71da5` (B17a, worked) and `0d72b26b` (now): B22 (`19d21747`), B24a (`ef932990`), M98 (core only), B21
(`28d0478c`: meshing on the job pool; a scripted run's `Remesh` with no budget waits for the workers; an edited chunk within 40 blocks
meshes in its own frame) and B07f (`2f995552`: `--shot` waits for `FarOnTheCard` up to 120 s; `StreamedWorldHost.Settle` limits; `Stale`
counts only needed tiles).

Read first: CLAUDE.md; docs/game/scripted-runs.md; docs/game/running.md (the scripted options); src/Vocaterra.Game/Scripted (the steps,
`ScriptedRunEnd`, the wait); the remesh pipeline (`ChunkMeshPipeline`, `VoxelRenderer`); `StreamedWorldHost.Settle`; the reports
.claude/briefs/b21-report.md, b07f-report.md, b17b-report.md.

Rules: a game milestone edits only the game projects, their tests, content/game-v1, content/schema, assets/ and docs/game/ and takes the
core as it is. A regression check is a C# test. Never edit .github/workflows/, either implementation-plan.md, either decisions.md or
docs/game/biomes-plan.md. The world does not change (goldens and fixture stay). Other agents work now on integration and a tour (B14,
which uses scripted runs), the far store and worker threads (B07d); B17b (GL passes) and B24 (guards; it added `SettleGuard` to tests) are
being merged. Keep your change small and in the scripted-run or remesh code that causes the stall.

Deliverables:
1. Reproduce both stalls on `0d72b26b` with the smallest scripted command lines (bounded by `timeout`), then find the first bad merge by
   running the same lines on the merges above (a detached `git worktree` per commit under your scratch folder, removed after; or
   `git bisect` on a scratch worktree). Say which merge and which code.
2. Fix the cause: a torch placed by a scripted step shows (its block and its glow) and every `--wait` returns; a guide dialogue opening
   does not block `--wait`. Do not paper over it with a shorter timeout.
3. A test that fails before the fix and passes after (a scripted host run on a fake device or the inline job pool, if possible; it must
   finish in seconds, or be marked slow).
4. Re-run r42-camp, r01-route, r40-inn and the gl-compare cave view with torches: each ends, and its pictures look right (say so).

Gate: build with -warnaserror (0 warnings); scripts/test.sh full (12,531 passed on feature/biomes at your base, plus your new tests);
format clean; content validate. A timing test that fails under load: rerun it alone and say so. Commit messages end with
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDbkPTqABsu7sLVD9h6

Report, a few lines: branch, head commit, worktree; the first bad merge and the cause; the fix; the test; the runs re-taken; anything
left. No file dumps or long logs.
