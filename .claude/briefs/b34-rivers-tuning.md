You are implementing B34, "More and bigger rivers, kept logical", for the Vocaterra repository (C#, .NET 10; the game is
Vocaterra.Game.Core, Vocaterra.Game, Vocaterra.Host; the core Vocaterra.Nlp* is not yours). Do the work yourself; do not spawn subagents.
Where to work: the worktree /home/james/Dev/Vocaterra/.claude/worktrees/b34 (absolute; cd there first), on the local branch
`B34-rivers-tuning` (cut from `feature/biomes` at 06964347). Commit after each step that builds and passes, prefix `B34: `. Do not push; never
push or merge feature/biomes or main. Scratch under /home/james/Dev/Vocaterra/artifacts/tmp/b34/, removed at the end.
Resources and waiting: every dotnet restore, build, test and format, every game or WorldPreview run through scripts/limited.sh (builds
`-m:4`, tests `-m:2`); one heavy command at a time, no `&`; long commands with the Bash tool's `run_in_background: true`, waiting for the
notification; never `sleep`/`until` polls or `nohup ... &`. Game runs: a Release build, `scripts/limited.sh timeout 1200 <command>`.
Iterate with `scripts/test.sh fast` plus the classes you touch; merge feature/biomes before your one `scripts/test.sh full`. Environment:
prefix every command with PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1; game runs need DISPLAY=:0 and
VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models. The sandbox refuses the words `eval` and `hash` in a command line (wrapper script
for `scripts/content.sh hash`). **The owner is playing the game on this machine: keep game windows for the final pictures only** (a few,
short), and use `tools/Vocaterra.WorldPreview` (no window) for maps and measurements.

Why: the owner, 2026-11-18: "we seem to also have fewer rivers now? im testing seed 42, and it used to have a large river", then, offered
more rivers, bigger rivers, a major river near the start, or all three: **"more rivers and bigger rivers, but they need to be logical"**
(D-game-34 item 55). B02c replaced the old climate-noise river with a drainage network (`src/Vocaterra.Game.Core/World/Drainage/DrainageNetwork.cs`:
a priority flood over 32-block nodes; a river past `sourceArea` 400 nodes; width and depth by the square root of the area; rivers only
fall); B02d made each lake one level; B27 cached each node's river reaches (`DrainageCell.Near`). Read .claude/briefs/b02c-report.md,
b02d-report.md, b27-report.md, and docs/game/biomes-plan.md section 3 (water). The land round the valley is low and flat, so catchments
there stay small. Your row: docs/game/implementation-plan.md section 4.14, B34.

"Logical" means: every river runs downhill to a lake or the sea; tributaries join and the river widens and deepens downstream, never
narrows; a river sits in a valley of its own, not on a ridge or a slope's face; no two rivers run side by side a few blocks apart; banks are
exitable (D-game-26); lakes stay one level (B02d). Rivers follow the land, with no channel painted over it. Do not force a river near the
start (the owner did not choose that).

Rules: a game milestone edits only the game projects, their tests, content/game-v1, content/schema, assets/ and docs/game/ and takes the
core as it is. A regression check is a C# test. Never edit .github/workflows/, either implementation-plan.md, either decisions.md or
docs/game/biomes-plan.md (say in the report what they should say). The drainage code stays pure (IntMath; one allowed `lock`), allocation-
free per frame (B21's and B02c's tests), and B27's cache keeps working; keep the per-chunk cost within about 20% of today's (B24's CPU
budgets must pass; say the cost before and after). **Never loosen a test's limit** (the puddle limit, the river-never-rises tests,
`ProceduralWaterSeedsTests`, B11b's entrance-water rules, B02d's `LakeLevelsTests`).

Deliverables:
1. **Measure first** (WorldPreview, seeds 42, 7, 2026, a 16 km window and a 2 km window round the valley): river count, total river
   length, the share of columns that are river, widths (median, 90th percentile, max), where the widest river is; and a drainage map.
2. **More rivers:** a lower source threshold (data: put `sourceArea` and the width and depth factors in `world.json` or the drainage data with
   schema and validator ranges, if they are code constants today), so small streams appear and join, without noise rivers on flat ground
   (a minimum slope or catchment for a source).
3. **Bigger rivers:** width and depth that grow with the catchment so that a major river (a large catchment) is about 12 to 40 blocks wide
   and a few blocks deep, carving a valley proportional to it; small streams stay 2 to 4 wide.
4. Tests: the measures above within stated ranges per seed (more rivers than today by a stated factor; the widest river on each seed at
   least about 12 blocks); widths never shrink downstream; every river reaches a lake or the sea; no river within a few blocks of another
   unless they join; the existing water and lake tests pass.
5. Pins re-recorded once at the end (the goldens, the save-v9 fixture's state digest if a world file changes, the atmosphere pins);
   `ProceduralGenerator.Version` bumped (a fresh far-store folder); the scripted tours' river and lake stops (r51, r52, r53 in
   scripts/scripted-runs.sh) re-picked if they move, so `BiomeTourTests` and `BiomeTourSeedsTests` pass.
6. Pictures in /home/james/Dev/Vocaterra/artifacts/b-series/b34/ with an index.md: the drainage maps before and after (three seeds), and
   in-game (Release, High, midday, `--far-view` on) a big river from a hill and a stream joining it, on seed 42. Look at each.
Gate: build -warnaserror (0 warnings); scripts/test.sh full (the count on feature/biomes at your base, plus your tests); format clean;
content validate. Commit messages end with
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDbkPTqABsu7sLVD9h6
Report, half a page: branch, head, worktree; the measures before and after; the parameters chosen; the tests; the per-chunk cost; pins
moved; the pictures; anything left. No file dumps.
