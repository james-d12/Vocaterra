You are implementing B10, "The underground blocks", for the Vocaterra repository (C#, .NET 10). The language core is the engine
Vocaterra.Nlp, the Spanish pack Vocaterra.Nlp.Es and the Vocaterra.Nlp.* test and tool projects; the game is Vocaterra.Game.Core (the
engine-free logic), Vocaterra.Game (MonoGame), Vocaterra.Host and tools/Vocaterra.SpeechRender. This milestone's work is in the game:
Vocaterra.Game.Core (Voxel/ and the body's collision), the block art in Vocaterra.Game, and their tests.
Where to work: the worktree /home/james/Dev/Vocaterra/.claude/worktrees/b10, already on the local branch `B10-underground-blocks` (cut from
`feature/biomes` at d6bacf4c, which holds B01 to B06, B04b, B08a and B09's 128-bit kind masks). Run every command from that directory.
Commit after each item that builds and passes its tests, with the prefix `B10: `. Do not push your branch. Never push or merge
feature/biomes or main. When you finish, leave the worktree on your branch, clean, and remove any extra worktree you made.
Resources (the machine has crashed from overload): run every dotnet restore, build, test, format and run through scripts/limited.sh (for
example `scripts/limited.sh dotnet build Vocaterra.sln -warnaserror -m:4`; tests with `-m:2`). scripts/test.sh calls it itself: run it
directly. Never start two heavy commands at once (no `&`, no background builds, no parallel test runs), and write no shell wait loop on
`pgrep -f` (wait for your command's own completion). Up to three other agents build at the same time; limited.sh queues you all, so
expect waits. While iterating run `scripts/test.sh fast` or `scripts/test.sh project tests/Vocaterra.Game.Core.Tests`; run
`scripts/test.sh full` once at the end, after your last change. Exit 137 means the 6 GB cap killed it: lower the parallelism. No game
windows: do not run the game, screenshots or scripts/scripted-runs.sh (the owner may be using the screen).
Environment: prefix every command inline with PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1 (the sandbox may
reject a bare `export` and refuses a command line containing the words `eval` or `hash`). Model-gated tests need
VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models.

Read first: CLAUDE.md (assets are CC0 only, D-game-5; game words live in content); docs/game/implementation-plan.md sections 1, 3, 4.14
(your row) and 6; docs/game/biomes-plan.md section 3.5 ("New blocks" and "In play") and section 4 (your row); D-game-34 items 8 and 9
in docs/game/decisions.md (nothing new can be gathered; the ores have no items and no nouns in this series; lava blocks the body, glows
and does no harm; bedrock can never be removed); docs/process/code-guidelines.md; .claude/briefs/b09-report.md (the 128-bit
`BlockKinds`, `BlockKinds.Accumulator`, the guard test; the mesher's tables are indexed by block id). The code: src/Vocaterra.Game.Core/
Voxel/BlockId.cs and BlockDefinition.cs (how a block is declared: solid, gatherable, light, drop), `VoxelGrid.IsSolid` and how a block
that stops the body is handled (issue #33's two-block-high collision), src/Vocaterra.Game/Graphics/Blocks/ (BlockAtlasLayout: the art
drawn in code), Graphics/World/ChunkLightIndex.cs (the light kinds: Lantern and PostTorch glow), Language/Words/WordColours.cs and
GreyscaleWorld.cs (each block's colour in the grey world), and any test that requires every block to have a noun, an item or a drop. Read
the sections named, never whole long files.

Rules: a game milestone edits only the game projects, their tests, content/game-v1, assets/ and docs/game/ and takes the core as it is. A
regression check is a C# test. Never create or edit anything under .github/workflows/; do not edit either implementation-plan.md, either
decisions.md or docs/game/biomes-plan.md (the orchestrator does); engine code only in src/Vocaterra.Game. Art drawn in code only (no image
files). No new content words and no items or nouns for the new blocks. Nothing places the new blocks in the world yet (B11 generates
them): the valley stays byte-identical (`SceneTests.GoldenDigest` does not move) and the scripted runs are untouched. Keep off the files
other agents are changing now: content/game-v1 (B02b, B08b), World/Climate, World/Generation and World/Regions (B02b, B15), Menu/ and the
settings menu (B08b).

Deliverables:
1. Nine blocks: slate, granite, limestone, gravel, lava, bedrock, iron ore, tin ore, copper ore, with ids after today's last (all under
   128; B09's guard holds), each declared with its properties: rock, gravel and the ores solid and not gatherable (no drop, no item);
   bedrock solid, not gatherable and never removable by any edit path (a guard where blocks are removed); lava a fluid that stops the
   body (it cannot be walked into or fallen into), is not solid for sight, glows (a light kind, like the lantern) and does no harm.
2. Their art drawn in code, in the style of today's stone, cobble and water: slate darker and banded, granite speckled pink-grey,
   limestone pale, gravel loose grey pebbles, bedrock near-black and rough, the ores stone with coloured flecks (iron tan, tin pale grey,
   copper orange-green), lava bright orange with darker veins (its glow from the light index). Their grey-world colours.
3. Tests: each block's properties; bedrock refused by every removal path; the body cannot enter lava (from the side and from above);
   lava is a light kind and lights its neighbours like a lantern; each block meshes (a small test grid with each new block beside air and
   beside stone gives faces; this also covers the mesher above id 63, which B09 could not); not gatherable and no noun, item or drop;
   B09's guard; `SceneTests.GoldenDigest` unchanged.
4. Pictures (D-game-34 item 12): if the atlas is drawn as pixel data without a graphics device, save a PNG of the nine new tiles at 8x
   beside stone, cobble and water to /home/james/Dev/Vocaterra/artifacts/b-series/b10/ (outside git) with an index.md; otherwise say so,
   and the orchestrator takes a shot later.
5. Documents: docs/game/ where blocks are listed (if a block list exists) and the Game.Core README's voxel section.

Gate (from the worktree root): scripts/limited.sh dotnet build Vocaterra.sln -warnaserror -m:4 (0 warnings); scripts/test.sh full
(11,885 passed before your change, plus your new tests) run after your last change; scripts/limited.sh dotnet format Vocaterra.sln
--verify-no-changes clean; scripts/limited.sh scripts/content.sh validate passes; `SceneTests.GoldenDigest` unchanged. Do not commit bin/,
obj/ or artifacts/.
Commit messages end with
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDbkPTqABsu7sLVD9h6

Report, one page: branch, commit SHA, worktree path; the blocks and their ids and properties; how lava stops the body and glows; how
bedrock is guarded; test counts; the pictures (or why not); bugs found and fixed; known-not-fixed with reasoning; files touched outside
the milestone's area; anything left undone and why. Leave out file dumps and long logs.
