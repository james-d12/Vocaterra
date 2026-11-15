You are implementing B02, "The world preview tool and the first maps", for the Vocaterra repository (C#, .NET 10). The language core
is the engine Vocaterra.Nlp, the Spanish pack Vocaterra.Nlp.Es and the Vocaterra.Nlp.* test and tool projects; the game is
Vocaterra.Game.Core (the engine-free logic), Vocaterra.Game (MonoGame), Vocaterra.Host and tools/Vocaterra.SpeechRender. This milestone's
work is in the game: a new tool project, tools/Vocaterra.WorldPreview, and its tests.
Where to work: the worktree /home/james/Dev/Vocaterra/.claude/worktrees/b02, already on the local branch `B02-world-preview` (cut from
`feature/biomes` at 94bea060, which holds B01). Run every command from that directory. Commit after each item that builds and passes its
tests, with the prefix `B02: `. Do not push your branch: it stays local and the gate merges it from there. Never push or merge
feature/biomes or main. When you finish, leave the worktree on your branch, clean, and remove any extra worktree you made.
Resources (the machine has crashed from overload): run every dotnet restore, build, test, format and run through scripts/limited.sh (for
example `scripts/limited.sh dotnet build Vocaterra.sln -warnaserror -m:4`; tests with `-m:2`; the tool itself with
`scripts/limited.sh dotnet run --project tools/Vocaterra.WorldPreview -- ...`). scripts/test.sh calls it itself: run it directly. Never
start two heavy commands at once (no `&`, no background builds, no parallel test runs), and write no shell wait loop on `pgrep -f`. Other
agents build in their own worktrees at the same time; limited.sh queues you all, so expect waits. While iterating run
`scripts/test.sh fast` or `scripts/test.sh project <your test project>`; run `scripts/test.sh full` (the whole suite, as the gate) once at
the end, after your last change. Exit 137 means the 6 GB cap killed it: lower the parallelism, do not work around the cap.
Environment: prefix every command inline with PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1 (the sandbox may
reject a bare `export`, and it refuses a command line containing the shell builtin words `eval` or `hash`).

Read first: CLAUDE.md; docs/game/implementation-plan.md sections 1, 3, 4.14 (your row) and 6; docs/game/biomes-plan.md sections 3.2, 3.4
and 4 (your row, and "Pictures for the owner"); D-game-34 items 12 and 13 in docs/game/decisions.md; docs/process/code-guidelines.md;
docs/game/architecture.md. B01's work, which the tool reads: src/Vocaterra.Game.Core/World/Noise/, World/Climate/ (`IClimateSampler.Fill`
for areas; a single `At` per column is slow), World/Biomes/ (`BiomeTable.Classify`, the rows), World/Generation/Procedural/ (`HeightModel`,
`ColumnShape`), World/WorldGenData.cs, src/Vocaterra.Game.Core/README.md (B01's section), and how `GameData` loads the bundle
(src/Vocaterra.Game.Core/Content/GameData.cs). For the project's shape, look at how tools/Vocaterra.SpeechRender is set up (its csproj,
how it loads content, how it is listed in Vocaterra.sln and in the licence scan). Read the sections named, never whole long files.

Rules: a game milestone edits only the game projects, their tests, content/game-v1, assets/ and docs/game/ and takes the core as it is.
A regression check is a C# test. Never create or edit anything under .github/workflows/; do not edit either implementation-plan.md,
either decisions.md or docs/game/biomes-plan.md (the orchestrator does); no new package (Directory.Packages.props stays as it is): write
the PNGs with the BCL's `ZLibStream` and a hand-written CRC32. The tool only reads: it does not change Vocaterra.Game.Core, the content
or the generator, and it does no tuning (the owner reviews the maps before any tuning). Do not overlap the other agents' files
(src/Vocaterra.Game.Core/Voxel/, Save/, World/, src/Vocaterra.Game, content/).

Deliverables:
1. tools/Vocaterra.WorldPreview, a console program in Vocaterra.sln (and wherever the other tools are registered, for example the
   licence scan), that loads content/game-v1 through `GameData` and writes PNG files:
   - `biomes`: each column coloured by its biome row, with a legend (a small PNG strip or a text file listing each biome id, its colour
     and its share of the area). The palette lives in the tool, keyed by the row's position or a hash of its id, so no biome id is written
     in code (the no-biome-id test scans src/ only, but keep the tool clean too).
   - `heights`: a hypsometric ramp (sea blue by depth, then lowland green to upland brown to grey stone to white above the snow line),
     with light hill-shading so the relief reads.
   - `slice`: a vertical section along a line (from x0,z0 to x1,z1), heights on the y axis from -64 to 320: ground coloured by the
     surface biome, water blue, the snow line and the tree line drawn as thin lines, and the sea level marked.
   Options: `--seed`, `--x`, `--z` (the area's centre), `--size` (blocks), `--scale` (blocks per pixel), `--out`. Use the climate's `Fill`
   over the area; a map of 4096 x 4096 blocks at 4 blocks per pixel should take seconds, not minutes.
2. Tests (a small test project for the tool, or the nearest existing one; follow how the repository tests its tools): the CRC32 against
   known vectors; a written PNG decodes (signature, chunk CRCs, inflated size); the same arguments write byte-identical files; a biome
   map's colours match `BiomeTable.Classify` at sampled columns.
3. The maps for the owner (D-game-34 item 12), saved in /home/james/Dev/Vocaterra/artifacts/b-series/b02/ (outside git; create the
   folder), for three seeds (42, 7 and 2026): for each seed a biome map and a heights map of 16,384 x 16,384 blocks at 16 blocks per
   pixel, the same pair of 4,096 x 4,096 at 4 blocks per pixel centred on a range, and three slices (across a range, across a coast and
   across a river). Also a one-page `index.md` in that folder listing each picture with a line saying what it shows and what to look
   for (biome borders, rivers, coasts, the ranges' height), plus the biome shares per seed.
4. Documents: a short README in the tool's folder (what it is for, the commands), and a line in docs/game/running.md or
   docs/building-and-testing.md, whichever lists the tools.

Gate (from the worktree root): scripts/limited.sh dotnet build Vocaterra.sln -warnaserror -m:4 (0 warnings); scripts/test.sh full
(11,597 passed before your change, plus your new tests) run after your last change; scripts/limited.sh dotnet format Vocaterra.sln
--verify-no-changes clean; the licence scan `bash scripts/license-scan.sh --offline` PASSED. Do not commit bin/, obj/ or artifacts/.
Commit messages end with
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDbkPTqABsu7sLVD9h6

Report, one page: branch, commit SHA, worktree path; what was built (the commands and options); test counts; the time to draw each map;
the pictures' folder and the biome shares per seed; anything in the maps that looks wrong to you (cliffs, missing biomes, rivers that
stop, coasts at the wrong place), described plainly for the owner, without changing the tuning; bugs found and fixed; files touched
outside the milestone's area; anything left undone and why. Leave out file dumps and long logs.
