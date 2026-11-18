You are implementing B02d, "One water level per lake", for the Vocaterra repository (C#, .NET 10; the game is Vocaterra.Game.Core,
Vocaterra.Game, Vocaterra.Host; the core Vocaterra.Nlp* is not yours). Do the work yourself; do not spawn subagents.
Where to work: the worktree /home/james/Dev/Vocaterra/.claude/worktrees/b02d (absolute; cd there first), on the local branch
`B02d-lake-levels` (cut from `feature/biomes` at 21f632e0). Commit after each step that builds and passes, prefix `B02d: `. Do not push; never
push or merge feature/biomes or main. Scratch under /home/james/Dev/Vocaterra/artifacts/tmp/b02d/, removed at the end.
Resources and waiting: every dotnet restore, build, test and format, every game or scripted run through scripts/limited.sh (builds `-m:4`,
tests `-m:2`); one heavy command at a time, no `&`. Run a long command with the Bash tool's `run_in_background: true`, end your turn and
wait for its notification; never poll with `sleep` or `until` loops, never `nohup ... &`. Game runs: a Release build,
`scripts/limited.sh timeout 1200 <command>`, `--far-view off` unless the far view matters. Other agents build at the same time: expect
waits. Iterate with `scripts/test.sh fast` plus the classes you touch; **merge feature/biomes into your branch before your one
`scripts/test.sh full`**, and report only after it finishes. Environment: prefix every command with PATH="$HOME/.dotnet:$PATH"
DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1; game runs need DISPLAY=:0 and VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models. The
sandbox refuses the words `eval` and `hash` in a command line (run `scripts/content.sh hash ...` from a wrapper script).

Why: B02c (rivers that only fall, `src/Vocaterra.Game.Core/World/Drainage/DrainageNetwork.cs`) left lakes as a staircase of levels (seed
2026, round x 5300 to 6900, z 600 to 800: levels 47, 45, 43 and 41 with walls of water between: the dark lines in its far-view picture).
Read .claude/briefs/b02c-report.md, rounds 4 and 5: the cause (`ReadLandmass` gives a lake node max(its flood level, the level of the node
it was found from), and that node can be a higher bank node), and B02c's three attempts and what each broke: a body raised to its
highest node leaked (`HeightModelTests...WaterIsExactlyWhere...`); a plain lowest-node flatten broke the river-never-rises tests; a
lake node taking its own flood level, each body to its lowest node and the bank chain below capped passed the one-level test and the
rivers but failed `TerrainTuningTests...NoPuddleLiesOnTheLand` (seed 7: 9 puddles against a limit of 8). B27 then cached each node's river
reaches in its tile (`DrainageCell.Near`): keep that working. Your row: docs/game/implementation-plan.md section 4.14, B02d.
Rules: a game milestone edits only the game projects, their tests, content/game-v1, content/schema, assets/ and docs/game/ and takes the
core as it is. A regression check is a C# test. Never edit .github/workflows/ (unless named), either implementation-plan.md, either
decisions.md or docs/game/biomes-plan.md (say in the report what they should say). No biome or ore id in source. The goal tonight
(the owner, 2026-11-17): feature/biomes ready to merge into main by morning, so keep the change contained and the suite green. The drainage code stays pure (B24's purity test; one `lock` allowed). **Never loosen a test's limit** (the puddle limit included):
if the lower level adds puddles, find why (first measure the seed-7 puddle count on your base) and remove their cause (banks under the
new level becoming shore or land, pockets the lower level uncovers).

Deliverables:
1. Each lake body is one water level up to its spill point, its outflow never rises above it, and the banks round it are shore (dry land
   at or above the level, or water at the level), with no walls of water and no new puddles. A test: every run of lake along three rows
   across the seed-2026 lake (z 620, 700, 780) is one level, and a test over seeds 42, 7 and 2026 that no lake body holds two levels.
2. The river-never-rises tests, HeightModelTests, the puddle test, B14's `ProceduralWaterSeedsTests` and B11b's entrance-water tests pass.
3. Pins re-recorded **once**, at the end: the procedural goldens that moved (the lake and river columns), the save-v9 fixture's state digest,
   the atmosphere pins; say which moved. B11d (cave mouths) runs at the same time and also moves pins: whoever merges second re-records.
4. A picture (Release, High, settled) of the seed-2026 lake from (5536, 304) as in B02c's `far-view-lake-seed2026.png` (the lines gone),
   and from its shore, in /home/james/Dev/Vocaterra/artifacts/b-series/b02d/ with an index.md. Look at both.
Gate: build -warnaserror (0 warnings); scripts/test.sh full (12,766 passed on feature/biomes with B17c, plus your tests); format clean;
content validate. Commit messages end with
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDbkPTqABsu7sLVD9h6
Report, a few lines: branch, head, worktree; the cause and the fix; the puddle count before and after; pins moved; the pictures; the
suite's count. No file dumps.
