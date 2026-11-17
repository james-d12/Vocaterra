You are implementing B03b, "Pictures of every biome on three seeds", for the Vocaterra repository (C#, .NET 10). The game is
Vocaterra.Game.Core (the engine-free logic), Vocaterra.Game (MonoGame) and Vocaterra.Host; the language core (Vocaterra.Nlp*) is not yours.
Where to work: the worktree /home/james/Dev/Vocaterra/.claude/worktrees/b03b2 (an absolute path; cd there first), on the local branch
`B03b-biome-pictures` (cut from `feature/biomes` at 66d9b263). Commit after each step that builds and passes its tests, with the prefix
`B03b: `. Do not push. Never push or merge feature/biomes or main. Leave the worktree clean on its branch. (An older, stopped attempt is the
branch `B03b-sample-world`, commit 0f0a621e, from 2026-11-15, in `.claude/worktrees/b03b`: read it with `git show 0f0a621e` for ideas; do
not merge or edit it.)
Resources and waiting: every dotnet restore, build, test and format, every game and scripted run through scripts/limited.sh (builds `-m:4`,
tests `-m:2`); one heavy command at a time, no `&`. Run a long command with the Bash tool's `run_in_background: true`, end your turn and
wait for its notification; never poll with `sleep` or `until` loops, never `nohup ... &`. Game runs: a Release build,
`scripts/limited.sh timeout 1200 <command>` (timeout inside), one batch script for the pictures. Other agents build at the same time:
expect waits. Iterate with `scripts/test.sh fast` plus the classes you touch; merge feature/biomes before your one `scripts/test.sh full`.
Scratch under /home/james/Dev/Vocaterra/artifacts/tmp/b03b/ (not /tmp), removed at the end. Environment: prefix every command with
PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1; game runs need DISPLAY=:0 and
VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models; scripted runs use
`VOCATERRA_RUNS_WORK=/home/james/Dev/Vocaterra/artifacts/tmp/b03b/runs`. Game windows are allowed; look at every picture with the Read tool.

Why: the owner, 2026-11-17: "For number six, yeah, run that, it's fine" (D-game-34 item 47; B03b had been stopped on 2026-11-15). Since
then B14 added `r51-biome-tour` (seed 42: every biome of `biomes.json`, a cave, the far land; .claude/briefs/b14-report.md; pictures in
artifacts/b-series/b14/), so B03b finishes only what the tour lacks: **every biome on seeds 7 and 2026 too**, and a seed-42 retake of the
stops B02c moved. Your row: docs/game/implementation-plan.md section 4.14, B03b.

Read first: CLAUDE.md; docs/game/scripted-runs.md (r51); docs/game/running.md (`--seed`, `--teleport`, `--far-view`); docs/game/biomes-plan.md
section 3 (the biomes); `tools/Vocaterra.WorldPreview` (its biome maps: it can find where each biome lies for a seed without a window);
`BiomeTourTests` (how r51's stops are checked).

Rules: a game milestone edits only the game projects, their tests, content/game-v1, content/schema, assets/ and docs/game/ and takes the
core as it is. Never edit .github/workflows/, either implementation-plan.md, either decisions.md or docs/game/biomes-plan.md. The world does
not change (goldens and fixture stay). No biome id in source (`NoBiomeIdsInSource`): stops are data in the scripted run, not code.

Deliverables:
1. **Stops for seeds 7 and 2026:** for each seed, one stop per biome of `biomes.json` (found with WorldPreview or a small tool: the nearest
   column well inside that biome, standing on ground), a cave entrance, and a far view; as scripted runs (`r52-biome-tour-seed7`,
   `r53-biome-tour-seed2026`, or one run taking the seed), with tests like `BiomeTourTests` (every biome has a stop, each stop in or beside
   its biome, ground at each teleport).
2. **`--world sample`** (the old attempt's bounded box with no valley): add it only if the pictures need it (for example if the valley's
   region gets in the way of a seed's stops); otherwise say why it is not needed and leave it out.
3. **B14's stall:** a full re-run of r51 after its stops were re-picked stalled at the beach stop past 30 minutes. Re-run r51 (Release,
   High, midday) and find out why if it stalls (the teleport into water or sand, the far view at that spot, a settle that never ends);
   fix the cause if it is small, or report it with the smallest reproducing command line. Retake its pictures 07 (river), 12 (dense
   woodland) and 14 (lake).
4. **Pictures** (Release, High, midday) in /home/james/Dev/Vocaterra/artifacts/b-series/b03b/ with an index.md: each biome on seeds 7 and
   2026, one line each on what it shows and anything wrong (holes, floating water, odd colours, seams, misplaced trees). Look at each.

Gate: build with -warnaserror (0 warnings); scripts/test.sh full (12,714 passed on feature/biomes at your base, plus your new tests);
format clean; content validate. A timing test that fails under load: rerun it alone and say so. Commit messages end with
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDbkPTqABsu7sLVD9h6

Report, half a page: branch, head commit, worktree; the stops per seed; `--world sample` or why not; the stall's cause; the pictures with
anything wrong; the suite's count; anything left. No file dumps or long logs.
