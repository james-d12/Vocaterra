You are implementing B12, "The underground in play", for the Vocaterra repository (C#, .NET 10). The game is Vocaterra.Game.Core (the
engine-free logic), Vocaterra.Game (MonoGame) and Vocaterra.Host; the language core (Vocaterra.Nlp*) is not yours. This milestone's work
is in the game: the renderer's light and darkening in Vocaterra.Game, the light index, the residency's underground near zone, a scripted
cave tour, and their tests.
Where to work: the worktree /home/james/Dev/Vocaterra/.claude/worktrees/b12, already on the local branch `B12-underground-in-play` (cut from
`feature/biomes` at 898a95e0, which holds the streamed world and far view (B07), the underground (B11), the valley's underground (B13),
lava that sinks the body (B10b) and the final terrain (B02b)). Run every command from that directory. Commit after each item that builds
and passes its tests, with the prefix `B12: `. Do not push. Never push or merge feature/biomes or main. Leave the worktree clean.
Resources: every dotnet restore, build, test, format and run, every game run, screenshot and scripted run through scripts/limited.sh
(builds `-m:4`, tests `-m:2`); one heavy command at a time, no `&`, no wait loops on `pgrep -f`. Other agents build at the same time.
Iterate with `scripts/test.sh fast` or a project; `scripts/test.sh full` once at the end. Exit 137: lower the parallelism. Environment:
prefix every command with PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1; game runs need DISPLAY=:0 and
VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models; scripted runs use `VOCATERRA_RUNS_WORK=/tmp/claude-1000/b12-runs`. **Game
windows are allowed unless the orchestrator says otherwise; look at every screenshot you take with the Read tool and fix what looks
wrong.** Quality before speed: don't call anything done that you have not seen working in a picture.

Read first: CLAUDE.md; docs/game/implementation-plan.md sections 1, 3, 4.14 (your row) and 6; docs/game/biomes-plan.md section 3.5
("Light underground", "In play") and section 4 (your row, "Pictures for the owner", "Measurement"); D-game-34 items 9, 12, 21 and 22 in
docs/game/decisions.md. The reports in .claude/briefs/: b07-phase-a-report.md, b07-phase-b1-report.md, b07-phase-b2a-report.md and b07-phase-b2b-report.md (residency: the band
streamer keeps everything under the player within 4 chunks, 6 underground; the body held until the world round it is resident; the far
view; `--stream-stats` and its `worst frames` line; the Release figures you compare against), b02b-round3-report.md (rivers and lakes
at their own levels), b11-report.md (caves, entrances, lava at
-54), b13-report.md, b10-report.md and b10b-report.md (lava glows through the light index; `Glows`; the placed torch `antorcha`,
`BlockId.Torch`, is not a light kind yet: B04b's finding), b04b-report.md (`ChunkLightIndex`). The code: src/Vocaterra.Game/Graphics/World/
(the mesher's light and ambient occlusion, `ChunkLightIndex`, VoxelRenderer), the residency in src/Vocaterra.Game/Streaming and
World/Streaming, the scripted-run steps (Scripted/), docs/game/scripted-runs.md. Read the sections named, never whole long files.

Rules: a game milestone edits only the game projects, their tests, content/game-v1, content/schema, assets/ and docs/game/ and takes the
core as it is. A regression check is a C# test. Never edit .github/workflows/, either implementation-plan.md, either decisions.md or
docs/game/biomes-plan.md. Engine code only in src/Vocaterra.Game; Core stays pure. The valley's look by day above ground must not change
(the `--world valley` scripted runs compare exactly, apart from what a placed torch now lights). No harm from lava (a later series).

Deliverables:
1. Darkening by depth: a face below its column's surface darkens with depth (sky exposure from the pure height model and the column's
   highest block), down to a cave minimum within about 6 blocks of the open sky; faces open to the sky unchanged. No light propagation
   (later work). Numbers as data if the renderer's light numbers are data, else constants beside the existing ones.
2. Light underground: lava, torches and lanterns light their surroundings; the placed torch (`BlockId.Torch`) becomes a light kind, and the
   light index updates when a torch is placed or removed, so a placed torch lights a cave at once.
3. The near zone underground: when the player is underground (below the column's surface), the residency keeps a wider radius of
   underground chunks (B07 set 6; check it is enough to see across a cavern) and caves near the player mesh fully; caves inside distant
   mountains are never meshed.
4. A scripted cave tour (docs/game/scripted-runs.md and the runs' script): from the valley through the pass to the nearest cave entrance
   B11 opened, down into a cavern and to a lava lake, with screenshots at each stop.
5. Measurements on this machine (an RTX 3070) at each preset underground and at the surface, with `--stream-stats`: frame medians, meshing
   time, resident chunks; report against B07's figures (B13 and B11 added buried cave faces).
6. Tests: darkening values by depth and the cave minimum; a face open to the sky unchanged; a placed torch is a light kind and changes the
   light index; lava lights like a lantern; the near zone widens underground and narrows at the surface; the cave tour's steps parse and
   run; the `--world valley` scripted runs unchanged apart from torch light.
7. Pictures for the owner (D-game-34 item 12; an owner-review stop): a cave mouth from outside; inside a tunnel with and without a placed
   torch; a cavern lit by lava; the darkening from an entrance into the dark, in /home/james/Dev/Vocaterra/artifacts/b-series/b12/ (outside
   git) with an index.md and the frame times.
8. Documents: docs/game/running.md, the Game README (light), docs/game/scripted-runs.md (the cave tour).

Gate: build with -warnaserror (0 warnings); scripts/test.sh full (12,267 passed before, plus your new tests); format clean; content
validate. Commit messages end with
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDbkPTqABsu7sLVD9h6

Report, one page: branch, head commit, worktree; what was built (darkening, lights, the near zone, the tour); test counts; measurements;
the scripted-run comparison; the pictures and what they show, plainly; bugs found and fixed; known-not-fixed; files touched outside the
milestone's area; anything left undone and why. No file dumps or long logs.
