You are implementing B05, "Saves version 9 and the changed-chunk state digest", for the Vocaterra repository (C#, .NET 10). The
language core is the engine Vocaterra.Nlp, the Spanish pack Vocaterra.Nlp.Es and the Vocaterra.Nlp.* test and tool projects; the game is
Vocaterra.Game.Core (the engine-free logic), Vocaterra.Game (MonoGame), Vocaterra.Host and tools/Vocaterra.SpeechRender. This milestone's
work is in the game, in Vocaterra.Game.Core (Save/, the state digest) and its tests.
Where to work: the worktree /home/james/Dev/Vocaterra/.claude/worktrees/b05, already on the local branch `B05-saves-v9` (cut from
`feature/biomes` at @@BASE@@, which holds B01 and B04a). Run every command from that directory. Commit after each item that builds and
passes its tests, with the prefix `B05: `. Do not push your branch: it stays local and the gate merges it from there. Never push or merge
feature/biomes or main. When you finish, leave the worktree on your branch, clean, and remove any extra worktree you made.
Resources (the machine has crashed from overload): run every dotnet restore, build, test, format and run, every scripts/content.sh call
and every scripted run through scripts/limited.sh (for example `scripts/limited.sh dotnet build Vocaterra.sln -warnaserror -m:4`; tests
with `-m:2`). scripts/test.sh, scripts/content-loop.sh and scripts/scripted-runs.sh call it themselves: run them directly. Never start two
heavy commands at once (no `&`, no background builds, no parallel test runs), and write no shell wait loop on `pgrep -f`. Other agents
build in their own worktrees at the same time; limited.sh queues you all, so expect waits. While iterating run `scripts/test.sh fast` or
`scripts/test.sh project tests/Vocaterra.Game.Core.Tests`; run `scripts/test.sh full` (the whole suite, as the gate) once at the end,
after your last change. Exit 137 means the 6 GB cap killed it: lower the parallelism, do not work around the cap.
Environment: prefix every command inline with PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1 (the sandbox may
reject a bare `export`, and it refuses a command line containing the shell builtin words `eval` or `hash`: for `scripts/content.sh hash`
use a wrapper script outside the repository). Model-gated tests and the scripted runs need
VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models; the scripted runs also need DISPLAY=:0.

Read first: CLAUDE.md (in particular "Game content bundle"); docs/game/implementation-plan.md sections 1, 3, 4.14 (your row) and 6;
docs/game/biomes-plan.md sections 2 ("Saves diff the whole world", "The state digest hashes the whole grid") and 3.8 (your design);
docs/game/saves.md; the owner's rule of no save backward compatibility (an older save stops loading; no migration code);
docs/process/code-guidelines.md. B04a's work, which you build on: src/Vocaterra.Game.Core/Voxel/ (`VoxelChunk.PristineDigest` and
`IsChanged`, recorded lazily before a chunk's first write; `VoxelGrid.SealPristine()`, which nothing calls yet; `ResidentCoordinates`;
`WorldLimits`) and tests/Vocaterra.Game.Core.Tests/Architecture/DenseGridMembersAllowListTests.cs (its B05 entries: Save/SaveGame.cs,
Save/SaveMigration.cs, Save/VoxelGridDiff.cs, Simulation/VoxelSimulation.cs's `ComputeDigest`, and VoxelGame.cs's `ComputeDigestHex`).
The code: src/Vocaterra.Game.Core/Save/ (SaveGame.cs `CurrentVersion` and `OldestReadableVersion`, SaveDocument.cs, ChunkEdit.cs,
VoxelGridDiff.cs, SaveService.cs, SaveMigration.cs), the state digest's users (grep `ComputeDigest`, `hostStateHash`), and the fixture
tests/Vocaterra.Game.Core.Tests/Fixtures/save-v8.json. Read the sections named, never whole long files.

Rules: a game milestone edits only the game projects, their tests, content/game-v1, assets/ and docs/game/ and takes the core as it is
(a missing core call stops that part: report it). A regression check is a C# test, never a script with text baselines. Never create or
edit anything under .github/workflows/; do not edit either implementation-plan.md, either decisions.md, docs/game/biomes-plan.md or
CLAUDE.md (the orchestrator does; name what CLAUDE.md should say in your report); engine code only in src/Vocaterra.Game; no
System.Random and no clock in the simulation. Tests are Given_When_Then in folders that mirror the source.
Do not overlap the other agents: do not edit src/Vocaterra.Game/Graphics/ (B04b), src/Vocaterra.Game.Core/World/Noise, Climate, Biomes,
Generation/Procedural or Streaming (B03), or content/game-v1/game/scene.json (B08a). You may add one call to `SealPristine()` at the end
of the valley's build (src/Vocaterra.Game.Core/World/Generation/SceneBuilder.cs), so the valley's own chunks count as unchanged; keep the
valley byte-identical (`SceneTests.GoldenDigest` does not move). Not in scope: the procedural world's restore path through a chunk source
(B06 wires it; design the save so B06 only has to supply "regenerate this chunk").

Deliverables (the plan's section 3.8):
1. The world's state digest: a header (the seed, the region template's digest, a `ProceduralGenerator.Version` constant, and a hash of
   the world-generation data only: world.json and biomes.json now, underground.json when it exists, so editing a phrase does not move
   it), then the changed chunks sorted by coordinate, each written as its coordinates and its digest. Its cost grows with the number of
   changed chunks, not the size of the world. It works on bounded and streamed grids. Everything that hashed the whole grid for the
   state (`hostStateHash`, the save's check, `VoxelGame`'s use) uses it.
2. Saves version 9: `ChunkEdit(X, Y, Z, Runs, Water)` keyed by chunk coordinates (Y may be negative) replaces the dense index;
   `WorldDims` goes; the pristine world copy goes (and with it the first save's hitch and its memory); restoring a save rebuilds the
   world as a new game does, then overwrites each edited chunk. X01's migration by position stays. `CurrentVersion` and
   `OldestReadableVersion` both become 9; a version-8 save is refused with the existing message.
3. The fixture: tests/Vocaterra.Game.Core.Tests/Fixtures/save-v9.json replaces save-v8.json (with the current bundle hash), and every
   test and document that names save-v8 follows.
4. The pins that move: the state digests (`AtmosphereSimulationTests` and any other), the end-to-end goldens if `hostStateHash` moved
   them (`VOCATERRA_RECORD_GOLDEN=1 dotnet test tests/Vocaterra.Nlp.Golden.Tests`, through limited.sh), never weakening a test.
5. Each B05 entry leaves the dense-member allow-list as its file stops using the dense members.
6. Tests: save and load round-trips on the valley (blocks, water, edits undone counting as unchanged, the inventory and the rest of the
   save as today); the digest is the same for a world before an edit and after the edit is undone; the digest ignores residency (the
   same edits give the same digest whatever chunks are resident, on a small streamed grid with a test `IChunkGenerator`); a version-8
   save is refused; the first save allocates no world copy; time to save and load the valley before and after.
7. Documents: docs/game/saves.md (version 9, what a save holds now).

Checks beyond the suite: capture the scripted runs before your first change and after (`scripts/scripted-runs.sh`, then
`python3 -I scripts/compare-runs.py`); the state-digest lines are masked by the comparison, so the rest must match. Known exception:
r01-route's `sounds.log` voice-end tick varies between captures under load (B04a's finding); report it if it is the only difference.

Gate (from the worktree root): scripts/limited.sh dotnet build Vocaterra.sln -warnaserror -m:4 (0 warnings); scripts/test.sh full
(@@COUNT@@ passed before your change, plus your new tests) run after your last change; scripts/limited.sh dotnet format Vocaterra.sln
--verify-no-changes clean; scripts/limited.sh scripts/content.sh validate passes. Do not commit bin/, obj/ or artifacts/.
Commit messages end with
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDbkPTqABsu7sLVD9h6

Report, one page: branch, commit SHA, worktree path; what was built (the digest's layout, the save's layout, the restore path and the
hook B06 must fill); test counts; the pins that moved; the scripted-run comparison's result; save and load times before and after; the
allow-list entries left; what CLAUDE.md should now say about the fixture; bugs found and fixed; known-not-fixed with reasoning; files
touched outside the milestone's area; anything left undone and why. Leave out file dumps and long logs.
