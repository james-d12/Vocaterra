You are implementing B09, "Widen block kinds from 64 to 128 bits", for the Vocaterra repository (C#, .NET 10). The language core is the
engine Vocaterra.Nlp, the Spanish pack Vocaterra.Nlp.Es and the Vocaterra.Nlp.* test and tool projects; the game is Vocaterra.Game.Core
(the engine-free logic), Vocaterra.Game (MonoGame), Vocaterra.Host and tools/Vocaterra.SpeechRender. This milestone's work is in the game:
Vocaterra.Game.Core, the mesher and light index in Vocaterra.Game, and their tests.
Where to work: the worktree /home/james/Dev/Vocaterra/.claude/worktrees/b09, already on the local branch `B09-kind-masks` (cut from
`feature/biomes` at 06e018a5, which holds B01 to B06, B04b and B08a). Run every command from that directory. Commit after each item that
builds and passes its tests, with the prefix `B09: `. Do not push your branch. Never push or merge feature/biomes or main. When you
finish, leave the worktree on your branch, clean, and remove any extra worktree you made.
Resources (the machine has crashed from overload): run every dotnet restore, build, test, format and run through scripts/limited.sh (for
example `scripts/limited.sh dotnet build Vocaterra.sln -warnaserror -m:4`; tests with `-m:2`). scripts/test.sh calls it itself: run it
directly. Never start two heavy commands at once (no `&`, no background builds, no parallel test runs), and write no shell wait loop on
`pgrep -f`. Up to three other agents build at the same time; limited.sh queues you all, so expect waits. While iterating run
`scripts/test.sh fast` or `scripts/test.sh project tests/Vocaterra.Game.Core.Tests`; run `scripts/test.sh full` once at the end, after
your last change. Exit 137 means the 6 GB cap killed it: lower the parallelism. No game windows: do not run the game, screenshots or
scripts/scripted-runs.sh (the owner may be using the screen); the orchestrator runs them later.
Environment: prefix every command inline with PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1 (the sandbox may
reject a bare `export` and refuses a command line containing the words `eval` or `hash`). Model-gated tests need
VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models.

Read first: CLAUDE.md; docs/game/implementation-plan.md sections 1, 3, 4.14 (your row) and 6; docs/game/biomes-plan.md section 2 (the
`KindMask` paragraph: 16-bit block ids, a lazy mask of one bit per kind, which caps block ids at 64; 57 are used) and sections 3.5 and 4
(your row and B10's: B10 adds slate, granite, limestone, gravel, lava, bedrock and three ores, which overflow 64);
docs/process/code-guidelines.md; .claude/briefs/b04b-report.md (the renderer, `ChunkLightIndex`, the tracker and view set keyed by
coordinate) and b06-report.md (`TemplateBlockIndex`, `Scene.CurrentBlocksOfKind`). The code: grep `KindMask` (Voxel/VoxelChunk.cs,
Voxel/VoxelGrid.cs, Npc/WorldFacts.cs, World/Scene.cs, World/Regions/TemplateBlockIndex.cs, src/Vocaterra.Game/Graphics/World/ChunkMesher.cs,
ChunkLightIndex.cs, VoxelRenderer.cs) and the block id table (Voxel/BlockId and its registry). Read the sections named, never whole long
files.

Rules: a game milestone edits only the game projects, their tests, content/game-v1, assets/ and docs/game/ and takes the core as it is. A
regression check is a C# test. Never create or edit anything under .github/workflows/; do not edit either implementation-plan.md, either
decisions.md or docs/game/biomes-plan.md (the orchestrator does); engine code only in src/Vocaterra.Game. No new block kinds (B10 adds
them) and no content changes. The valley stays byte-identical (`SceneTests.GoldenDigest` does not move) and the pictures identical (the
mesher's vertex-identity and the light index tests). Keep off the files other agents are changing now: content/game-v1 (B02b, B08b),
World/Climate, World/Generation (B02b, B15), World/Regions except `TemplateBlockIndex`'s mask type (B02b, B15), Menu/ and the settings
menu (B08b). In shared files (WorldFacts, Scene) change only the mask's type and its uses.

Deliverables:
1. A 128-bit kind mask (one type used everywhere a set of block kinds is held: the chunk's lazy mask, the mesher's grey masks, the light
   index, `WorldFacts`'s masks, the tracker, `TemplateBlockIndex`, `Scene`), so block ids 0 to 127 work. Choose the representation (two
   `ulong`s or `UInt128`) by measurement; keep the chunk's memory small and the hot paths free of allocation.
2. A guard: a test that fails when the block id table outgrows the mask, and a test with a block id above 63 (a test-only id or a
   fake table) that every mask user handles (membership, union, "any of", iteration in order).
3. Measurements (Debug, one thread, a fixed sample): meshing the valley's chunks and building the light index before and after; the
   chunk's memory before and after.
4. Documents: the Game.Core README's voxel section (the mask's width and where it is used).

Gate (from the worktree root): scripts/limited.sh dotnet build Vocaterra.sln -warnaserror -m:4 (0 warnings); scripts/test.sh full
(11,879 passed before your change, plus your new tests) run after your last change; scripts/limited.sh dotnet format Vocaterra.sln
--verify-no-changes clean; `SceneTests.GoldenDigest` unchanged. Do not commit bin/, obj/ or artifacts/.
Commit messages end with
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDbkPTqABsu7sLVD9h6

Report, one page: branch, commit SHA, worktree path; the representation chosen and why; every file that changed type; test counts; the
measurements; bugs found and fixed; known-not-fixed with reasoning; files touched outside the milestone's area; anything left undone and
why. Leave out file dumps and long logs.
