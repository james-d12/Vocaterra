You are implementing B10b, "The body sinks into lava; the ores easier to see", for the Vocaterra repository (C#, .NET 10). The language core is the engine
Vocaterra.Nlp, the Spanish pack Vocaterra.Nlp.Es and the Vocaterra.Nlp.* test and tool projects; the game is Vocaterra.Game.Core (the
engine-free logic), Vocaterra.Game (MonoGame), Vocaterra.Host and tools/Vocaterra.SpeechRender. This milestone's work is in the game: the
body's movement in Vocaterra.Game.Core, any view effect and the ores' art in Vocaterra.Game, and their tests.
Where to work: the worktree /home/james/Dev/Vocaterra/.claude/worktrees/b10b, already on the local branch `B10b-lava-sink` (cut from
`feature/biomes` at @@BASE@@, which holds B10's lava). Run every command from that directory. Commit after each item that builds and passes
its tests, with the prefix `B10b: `. Do not push your branch. Never push or merge feature/biomes or main. When you finish, leave the
worktree on your branch, clean, and remove any extra worktree you made.
Resources (the machine has crashed from overload): run every dotnet restore, build, test, format and run through scripts/limited.sh
(builds `-m:4`, tests `-m:2`). scripts/test.sh calls it itself. Never start two heavy commands at once (no `&`, no background builds), and
write no shell wait loop on `pgrep -f`. Up to three other agents build at the same time; expect waits. While iterating run
`scripts/test.sh fast` or `scripts/test.sh project <project>`; run `scripts/test.sh full` once at the end. Exit 137 means the 6 GB cap
killed it: lower the parallelism. No game windows: do not run the game, screenshots or scripted runs (the owner may be using the screen).
Environment: prefix every command inline with PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1 (the sandbox refuses
the words `eval` and `hash` in a command line). Model-gated tests need VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models.

**The owner's decision (2026-11-15, D-game-34 item 22):** "You should sink into it. Damage doesn't need to be done in this pr." B10 made
lava stop the body like a solid block, so a body can stand on a lava surface; instead, the body sinks into lava.
**And (2026-11-15, D-game-34 item 23):** "I think the ores should be more obvious. In terms of their colour." B10's ore tiles
(artifacts/b-series/b10/tiles.png, the last three of the second row) have small, faint flecks: tin's pale grey is nearly invisible on
stone, iron's tan is faint, copper's orange is small.

Read first: CLAUDE.md; docs/game/implementation-plan.md sections 1, 3 and 6; .claude/briefs/b10-report.md (lava 61: `Solid=false`,
`Liquid=true`, `StopsBody`, `Glows`; `VoxelGrid.BlocksBody`, SleepWalk's and the mesher's copies honour `StopsBody`); how the body moves
in water today (swimming, sinking, buoyancy, the view under water: grep the body's physics and `WaterLevel`, `Liquid`); docs/game/running.md
on movement; docs/process/code-guidelines.md. Read the sections named, never whole long files.

Rules: a game milestone edits only the game projects, their tests, content/game-v1, assets/ and docs/game/ and takes the core as it is. A
regression check is a C# test. Never create or edit anything under .github/workflows/; do not edit either implementation-plan.md, either
decisions.md or docs/game/biomes-plan.md (the orchestrator does); engine code only in src/Vocaterra.Game. No harm or damage from lava (a
later series). The valley stays byte-identical (`SceneTests.GoldenDigest` does not move; no lava stands in it). Keep off the files other
agents are changing now: src/Vocaterra.Game/Program.cs, VoxelGame.cs's start-up, Graphics/World and the edge wall (B07, which also
touches the body's collision at the valley's edge: keep your change to how a liquid cell acts on the body, and expect a small merge),
World/Generation and World/Underground (B11), world.json and biomes.json (B02b). In Graphics/Blocks change only the three ores' art.

Deliverables:
1. Lava acts on the body as a thick liquid: the body enters it and sinks slowly, moves slowly through it (well below its speed in
   water), can swim up slowly and climb out at an edge one block above the surface, as in water; it never stands on a lava surface. The
   numbers (sink speed, move factor, swim-up speed) live where the water's live, as data if the water's are data. Lava stays not
   solid for sight, as B10 made it.
2. The walk planner and SleepWalk do not route a body through lava (they treat it as blocked for planning, as today), so NPCs and
   scripted walks are unchanged.
3. If the game tints the view when the eye is under water, tint it deep orange when the eye is in lava (Game project).
4. Tests: a body dropped onto a lava pool sinks into it (not resting on top); it moves slower in lava than in water; it can swim up and
   climb out at an edge; it takes no harm; the walk planner still avoids lava; `SceneTests.GoldenDigest` unchanged.
5. The ores easier to see (src/Vocaterra.Game/Graphics/Blocks, `BlockArtNature`): on the same stone, larger clusters of strong colour
   (several clusters of 3 to 6 pixels each per tile face), each with a darker rim and a lighter highlight so it stands out at a distance
   and in shade: iron a warm peach-tan (about #D8A27A) with a brown rim, tin a bright silver-white with a cool blue tint (about #E6EEF5)
   and a dark grey rim, copper a bright orange (about #E87A2C) with a few teal-green patina pixels (about #3FB59A). The three must be
   told apart at a glance from each other and from granite. Their grey-world colours follow as today. Redraw the tile sheet B10 made
   (stone, cobble, water and the nine tiles at 8x) before and after, to /home/james/Dev/Vocaterra/artifacts/b-series/b10b/ (outside git)
   with an index.md; the PNG writer may stay a throwaway test as in B10, or become a small tool if it is cheap.
6. Documents: docs/game/running.md (or where movement in water is described).

Gate (from the worktree root): scripts/limited.sh dotnet build Vocaterra.sln -warnaserror -m:4 (0 warnings); scripts/test.sh full
(@@COUNT@@ passed before your change, plus your new tests) run after your last change; scripts/limited.sh dotnet format Vocaterra.sln
--verify-no-changes clean; `SceneTests.GoldenDigest` unchanged. Do not commit bin/, obj/ or artifacts/.
Commit messages end with
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDbkPTqABsu7sLVD9h6

Report, half a page: branch, commit SHA, worktree path; how lava acts on the body now (the numbers); the view tint; the ores' new
colours and the pictures; test counts; bugs
found and fixed; known-not-fixed; files touched outside the milestone's area. Leave out file dumps and long logs.
