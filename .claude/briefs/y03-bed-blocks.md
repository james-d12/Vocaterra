You are implementing Y03, "Bed and bedroll blocks", for the Vocaterra repository (C#, .NET 10). The language core is the engine
Vocaterra.Nlp, the Spanish pack Vocaterra.Nlp.Es and the Vocaterra.Nlp.* test and tool projects; the game is Vocaterra.Game.Core (the
engine-free logic), Vocaterra.Game (MonoGame), Vocaterra.Host and tools/Vocaterra.SpeechRender. This milestone's work is in the game.

Where to work: /home/james/Dev/Vocaterra/.claude/worktrees/y03, which already exists on the branch `Y03-bed-blocks` (cut from
`feature/settlement`). Start every command with `cd /home/james/Dev/Vocaterra/.claude/worktrees/y03 &&` and use absolute paths for file
tools; your shell may start in another checkout, which you must not edit. Commit after each item that builds and passes its tests, with
the prefix `Y03: `. Do not push your branch: it stays local and the gate merges it from there. Never push or merge `feature/settlement`
or `main`. When you finish, leave the worktree on your branch, clean.

Resources (the machine has crashed from overload): run every dotnet restore, build, test, format and run and every screenshot run
through scripts/limited.sh (for example `scripts/limited.sh dotnet build Vocaterra.sln -warnaserror -m:4`; tests with `-m:2`).
scripts/test.sh calls it itself: run it directly. Never start two heavy commands at once (no `&`, no background builds, no parallel
test runs), and write no shell wait loop on `pgrep -f`. Other agents build on this machine too, so a heavy command will often wait for
a free slot: that is normal, let it wait, and build and test in few, well-chosen runs. While iterating run `scripts/test.sh fast` or
`scripts/test.sh project <test project>`; run `scripts/test.sh full` once before reporting. Exit 137 means the 6 GB cap killed it:
lower the parallelism, do not work round the cap.

Environment: prefix every command inline with PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1 (the sandbox may
reject a bare `export`).

Read first: CLAUDE.md; docs/process/code-guidelines.md; in docs/game/settlement-plan.md the paragraph "Y03" of section 8.3 and, in
section 9.1, the interior stamp (it shows where a bed will stand: the `crate` cells). Then the code:
- src/Vocaterra.Game.Core/Voxel/BlockId.cs (the end of the enum and its header comment) and Voxel/BlockDefinition.cs (the table and
  what `Solid`, `Transparent` and `Decor` mean; the rows of `Loaves`, `Apples`, `Bottles`, `Door` and `Torch` are the nearest models).
- Voxel/BlockRay.cs (what the ray passes through).
- src/Vocaterra.Game/Graphics/World/ChunkMesher.cs: `BlockShape`, the function that gives a block its shape, and the `Pile` case.
- src/Vocaterra.Game/Graphics/Blocks/BlockAtlasLayout.cs and BlockArt.cs: how the loaves and the apples get their cells and their art.
- The tests that count or list blocks and atlas cells (grep `BlockDefinition.All`, `PostTorch`, `Loaves` under tests/).
Read the parts named, never whole long files. Heavy output goes to log files; print at most 30 lines.

Rules: a game milestone edits only the game projects, their tests and docs/game/ and takes the core as it is; a regression check is a
C# test, never a script with text baselines; tests are named Given_When_Then in folders that mirror the source; do not edit either
implementation-plan.md, either decisions.md or docs/game/settlement-plan.md (the orchestrator does); engine code only in
src/Vocaterra.Game; Vocaterra.Game.Core references no package (`ArchitectureRulesTests`); art is drawn in code as the other blocks'
is, or CC0; nothing under .github/workflows/. The weather series is editing ChunkMesher.cs's light and `MeshOptions` on another
branch: touch only the shape of the two new blocks there.

What this milestone is for. The series puts a bed in an inn and a bedroll in a tent. Every block today is a full cube, a crossed
plant, a torch or a "pile" (a low box: the loaves, the apples and the bottles on a crate). A bed and a bedroll are two new blocks
drawn with the pile shape. No one sleeps in them yet: Y05 and Y12 do that.

Deliverables:

1. **Two blocks.** `Bed` and `Bedroll`, appended after `PostTorch` in `BlockId` (ids are written to saves by name and hashed into the
   world digest, so nothing before them moves), with rows named `bed` and `bedroll` in `BlockDefinition`'s table: not solid (a body
   walks through, as through a door), not decor (so the crosshair's ray stops on them and nothing can be placed into their cell), not
   liquid, never gathered, no drop.
2. **The shape and the art.** Both use the pile shape. Look at the box the pile draws: if a bed needs a lower or wider box to read as
   a bed from eye height, give the shape a size per block rather than a new shape, and say what you chose. The bed's top shows a
   pillow at one end and a blanket; the bedroll is a roll of cloth. Atlas cells and art after the model of the loaves.
3. **Tests.** The table's count and the two names (`BlockDefinition.FromName`); the ray stops on a bed; a body walks through one; a
   cell holding one is not free ground to wake on (`IsFreeGround` in Session/VoxelSession.Death.cs: test it through what is public);
   the atlas tests. Fix every test that pins the number of blocks or cells.
4. **Two screenshots** from a scratch copy of the scene that is not committed: a bed and a bedroll set by `block` props on flat
   ground by the village, seen from eye height (`--scene <file>`, `--teleport`, `--look`, `--screenshot`), saved as `bed.png` and
   `bedroll.png` in /home/james/Dev/Vocaterra/.claude/worktrees/settlement/artifacts/y03/ (git-ignored; the one write outside your
   worktree that is asked for). If the scene's schema lists the block names a `block` prop or a stamp's legend may use
   (content/schema/scene.schema.json), add the two names there.
5. **Documents.** src/Vocaterra.Game.Core/README.md and src/Vocaterra.Game/README.md where they list blocks or shapes, and
   content/README.md if it lists the block names a scene may use.

Not in this milestone: sleeping, any item, any change to the shipped scene (`content/game-v1/game/scene.json`), any change to the
save. The shipped world has neither block, so the scene's digest and the goldens must not move: if one moves, stop and say why.

Gate (from your worktree's root): scripts/limited.sh dotnet build Vocaterra.sln -warnaserror -m:4 (0 warnings); scripts/test.sh full
(report the count); scripts/limited.sh dotnet format Vocaterra.sln --verify-no-changes clean. Do not commit bin/, obj/ or artifacts/.

Commit messages end with
Co-Authored-By: Claude Haiku 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDfPceqswnMMBmTQzg9

Report, one page: branch, commit SHA, worktree path; what was built; the box you chose for each block; test counts and the tests you
changed; the screenshots' paths; whether any digest, golden or screenshot baseline moved; anything left undone and why. Leave out how
you searched.
