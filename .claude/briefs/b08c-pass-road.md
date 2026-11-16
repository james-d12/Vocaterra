You are implementing B08c, "The pass road from the village; the run comparison's speed", for the Vocaterra repository (C#, .NET 10).
The game is Vocaterra.Game.Core (the engine-free logic), Vocaterra.Game (MonoGame) and Vocaterra.Host; the language core (Vocaterra.Nlp*)
is not yours. This milestone's work is in the game: content/game-v1/game/scene.json (the pass road), the scene's build if a stamp or path
rule needs it, their tests, and scripts/compare-runs.py.
Where to work: the worktree /home/james/Dev/Vocaterra/.claude/worktrees/b08c, already on the local branch `B08c-pass-road` (cut from
`feature/biomes` at @@BASE@@, which holds B08a's pass, B08b's lock and B13's valley underground). Run every command from that directory.
Commit after each step that builds and passes its tests, with the prefix `B08c: `. Do not push. Never push or merge feature/biomes or main.
Leave the worktree clean on its branch.
Resources: every dotnet restore, build, test, format and run, every scripts/content.sh call, every game run and screenshot through
scripts/limited.sh (builds `-m:4`, tests `-m:2`); one heavy command at a time, no `&`, no wait loops on `pgrep -f`. Other agents build at
the same time. Iterate with `scripts/test.sh project tests/Vocaterra.Game.Core.Tests`; `scripts/test.sh full` once at the end. Exit 137:
lower the parallelism. Environment: prefix every command with PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1 (the
sandbox refuses `eval` and `hash` in a command line: for `scripts/content.sh hash` use a two-line wrapper script in /tmp/claude-1000/);
game runs need DISPLAY=:0 and VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models. **Game windows are allowed** (the owner is asleep):
look at every screenshot you take with the Read tool. Scripted runs use `VOCATERRA_RUNS_WORK=/tmp/claude-1000/b08c-runs`.

The findings (the night's in-game verification, 2026-11-16; pictures in /home/james/Dev/Vocaterra/artifacts/b-series/verify/, index.md):
1. **The road from the village does not reach the pass as a road.** At about (318, 217), looking east (`--teleport 318,217 --look 90,-2`),
   the path ends at a dirt riser 2 to 3 blocks high (b08a-1-road-from-village.png). A walk from (300, 222) to the gate `paso.gate` (316,
   217) takes a 41-turn detour and about 3,000 ticks (`--pass open --teleport 300,222 --walk-to 316,217 --dev`), against 150 to 470 for
   the later legs. Past the riser the cutting reads as a dirt staircase, not a road (b08b-2w-rockfall-open-wide.png).
2. **scripts/compare-runs.py hangs** on the saves' single long lines (70 KB): its `(\S*/)?(content|src|...)` mask is quadratic. The
   verification's working copy, which bounds that part of the regex to 150 characters and changes nothing else, is at
   /tmp/claude-1000/-home-james-Dev-Vocaterra--claude-worktrees-biomes/bf9afb01-586b-4494-be29-1e3e82a69272/scratchpad/cmp.py if it still
   exists (otherwise find the agent's scratch copy, or write the bound yourself).

Read first: CLAUDE.md ("Game content bundle"); docs/game/implementation-plan.md sections 1, 3 and 6; .claude/briefs/b08a-report.md (the
pass: path `pass` from `paso.foot` (340, 212) to `paso.edge` (383, 208, y 58), `pass.rise` across the village's blend slope from
`paso.gate` (316, 217) to `paso.rise` (326, 217), `pass.floor`; the stamp's `floorY`; PassTests), b08b-report.md, b13-report.md (the
valley's underground keeps margins round paths; its tests read the scene); content/README.md (paths, grading, stamps); the scene's path
and grading code in World/Generation (SceneBuilder, the path pass). Read the sections named, never whole long files.

Rules: a game milestone edits only the game projects, their tests, content/game-v1, content/schema, assets/, docs/game/ and here
scripts/compare-runs.py. A regression check is a C# test. Never edit .github/workflows/, either implementation-plan.md, either
decisions.md or docs/game/biomes-plan.md. Keep off World/Climate, World/Generation/Procedural, world.json and biomes.json (B02b),
src/Vocaterra.Game (B07), World/Underground (B11's) beyond what a test needs.

Deliverables:
1. One continuous road from the village to the edge: the village's road joins `pass.rise` with no riser; the surface is the road's path
   block all the way through the rise, the floor, past the rockfall's place and up to `paso.edge`; every step along it is at most one
   block up or down; the road reads as a road in a picture (path blocks, edges cut cleanly into the slope; retaining walls of the valley's
   stone where the cutting is deep, if you like). Keep the rockfall where B08b removes it, and keep the pass closed by it.
2. Tests: with the pass open, a walk from the village square (or from (300, 222)) to `paso.gate` and on to `paso.edge` follows the road:
   no step over one block along the road's cells, the walked path within about 1.3 times the road's own length, and no long detour (a
   tick bound well under today's 3,000 for the first leg); the road's cells are path blocks end to end; PassTests and PassLockTests pass
   (the closed pass still blocks the walk); B13's `ValleyUndergroundTests` pass (its margins follow the new paths).
3. Pins: `SceneTests.GoldenDigest` moves (say from what to what); re-hash the bundle; the fixture `save-v9.json`'s bundle hash (and its
   state digest only if it moves); the tree count line if trees are cleared.
4. scripts/compare-runs.py: the bounded regex, so a comparison of two full captures finishes in seconds; run it on two captures of the
   valley to show it.
5. Pictures in /home/james/Dev/Vocaterra/artifacts/b-series/b08c/ (outside git) with an index.md: the road from the village at (318, 217)
   looking east, before and after; the cutting from the rockfall's place with `--pass open`, before and after; the whole road from the
   rim looking west. Look at each one; fix what looks wrong.

Gate: build with -warnaserror (0 warnings); scripts/test.sh full (@@COUNT@@ passed before, plus your new tests); format clean; content
validate; the content lint line from CLAUDE.md clean if you touched text. Commit messages end with
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDbkPTqABsu7sLVD9h6

Report, half a page: branch, head commit, worktree; what changed in the road and why the riser was there; the walk's ticks and path
length before and after; tests; pins; the comparison script's time; pictures; anything left. No file dumps or long logs.
