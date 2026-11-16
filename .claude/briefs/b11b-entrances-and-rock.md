You are implementing B11b, "Cave entrances you can walk into, and more rock variety", for the Vocaterra repository (C#, .NET 10). The
game is Vocaterra.Game.Core (the engine-free logic), Vocaterra.Game (MonoGame) and Vocaterra.Host; the language core (Vocaterra.Nlp*) is
not yours. This milestone's work is in the game: the underground's generation (src/Vocaterra.Game.Core/World/Underground, its passes in
World/Generation/Procedural), content/game-v1/game/underground.json with its schema and validator, the surface pass where bare stone is
laid (cliffs, steep ground, scree) and its data, new blocks if needed (Voxel/BlockId, BlockDefinition, Graphics/Blocks), the preview
tool's views of them, and their tests.
Where to work: the worktree /home/james/Dev/Vocaterra/.claude/worktrees/b11b, already on the local branch `B11b-entrances-and-rock` (cut
from `feature/biomes` at @@BASE@@, which holds the underground (B11), the valley's underground (B13), the final terrain (B02b) and the
streamed world (B07)). Run every command from that directory. Commit after each item that builds and passes its tests, with the prefix
`B11b: `. Do not push. Never push or merge feature/biomes or main. Leave the worktree clean on its branch.
Resources: every dotnet restore, build, test, format and run, every scripts/content.sh call, every preview-tool run, game run, screenshot
and scripted run through scripts/limited.sh (builds `-m:4`, tests `-m:2`); one heavy command at a time, no `&`, no wait loops on
`pgrep -f`. Three other agents build and run the game at the same time: expect waits. Iterate with `scripts/test.sh project
tests/Vocaterra.Game.Core.Tests` and the preview tool; run `scripts/test.sh full` once at the end with `VOCATERRA_HEAVY_SLOTS=1` set inline
(other agents' builds share limited.sh's 8 GB slice; one slot avoids oom kills). Exit 137: lower the parallelism. Environment: prefix
every command with PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1 (the sandbox refuses the words `eval` and `hash`
in a command line: for `scripts/content.sh hash` use a two-line wrapper script in /tmp/claude-1000/); game runs need DISPLAY=:0 and
VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models; scripted runs use `VOCATERRA_RUNS_WORK=/tmp/claude-1000/b11b-runs`. **Game
windows are allowed; look at every screenshot you take with the Read tool and fix what looks wrong.** Quality before speed: the owner
will judge this by its pictures.

Why (two findings, 2026-11-16):
1. B12 (the underground's light, .claude/briefs/b12-report.md, pictures in /home/james/Dev/Vocaterra/artifacts/b-series/b12/): B11's
   entrances are open pits about 10 blocks deep (for example at 488..496, 181..190, seed 42, floor y 70 under ground 80) with no walkable
   way into the cave below (its tour teleports), and the mouth reads as a dirt-rimmed hollow in a meadow (01-cave-mouth-from-outside.png,
   02-pit-and-dark-opening.png).
2. The owner (D-game-34 item 27 in docs/game/decisions.md): "We probably need some more rock variation, like gravel, sand stuff. So, for
   example, in a cave, rather than just all being stone, it should have different mixtures of stone to break things up ... perhaps some
   segments of gravel in there or something to break it up." Today the strata are stone, slate below y 0 with a dither, and granite and
   limestone blobs (underground.json `strata.blobs`, threshold 330); gravel exists as a block (B10) but nothing lays it; the pit's walls
   and the caves read as one grey.

Read first: CLAUDE.md ("Game content bundle"); docs/game/implementation-plan.md sections 1, 3, 4.14 (your row, B11b) and 6;
docs/game/biomes-plan.md sections 3.5 and 4 (your row); D-game-34 items 8, 9, 12 and 27; .claude/briefs/b11-report.md (the passes, the
lattice, the water rule, entrances never in a region or its margin), b13-report.md (the valley's underground and its margins: the valley's
surface must not change), b10-report.md (how blocks and their textures were added), b02b-round3-report.md (water per column: the cave
water rule reads it), b12-report.md; the code named above. Read the sections named, never whole long files.

Rules: a game milestone edits only the game projects, their tests, content/game-v1, content/schema, assets/ and docs/game/ and takes the
core as it is. A regression check is a C# test. Never edit .github/workflows/, either implementation-plan.md, either decisions.md or
docs/game/biomes-plan.md. Engine code only in src/Vocaterra.Game; Core stays pure (no floating point, clock or System.Random in
generation; column- and chunk-local as B11's purity and locality tests require). Every new parameter in underground.json (or the surface
data) with its schema and validator; no block id hard-coded where data can name it. **The valley's surface stays identical**
(`SceneTests.GoldenDigest` may move only from buried blocks, as B13's rule: nothing within its margins of air, water, sites, paths and
props); the 18 `ProceduralGoldenTests`, the fixture save-v9.json's state digest and the atmosphere pins move (re-record them, say why).
Keep off what the other agents change now: B12 (Graphics/World's mesher light, `ChunkLightIndex`), B07b (VoxelGame.cs's scripted steps and
teleport, Streaming/), B07c (Graphics/Far and World/Far; it reads the generator's surface blocks, so name any new surface block in your
report for its palette). No harm from lava; nothing gatherable changes (item 8).

Deliverables:
1. **Entrances you can walk into.** Each entrance leads from the surface down into the cave network on foot: a sloping or winding passage
   (or a spiral round a shaft) whose steps are never higher than one block, wide enough to walk (2 or more), reaching a cave B11 carves;
   none in a region or its margin, none that floods (B11's water rule with the per-column water). Mouths that read as caves: in a hillside
   or cut bank where there is one (an opening under an overhang with a stone rim), and on flat ground a rimmed hole in stone, not a dirt
   hollow. Test: from every entrance in a sample (three seeds), a walk (the game's own route finder or a flood of standable cells with
   one-block steps) reaches a cave cell at least 20 blocks below the mouth; no step over one block along it.
2. **Rock variety underground** (as data): pockets and bands of gravel, sand and dirt in the upper underground; larger and more frequent
   granite and limestone; slate and a darker stone deeper; mossy stone near cave water and near entrances; gravel and loose stone on
   cave floors; sand and gravel where caves meet water. Add new stone kinds only if the existing ones do not break up the grey enough
   (textures drawn the way B10 drew its blocks; name them in Spanish and English like the other blocks). State a measure and test it:
   over cave walls and floors in a sample of chunks above y 0, plain stone at most about 60 to 70% of the exposed faces, at least four
   kinds present, no kind in a single huge blob.
3. **Rock variety on bare stone at the surface** (as data): exposed stone on cliffs, steep mountainsides, pits and cut banks gets the
   same mixtures (bands of granite and limestone across a cliff face, gravel scree on steep slopes and at a cliff's foot, gravel and sand
   along streambeds where the bank pass allows); snow and grass rules unchanged. The valley's own surface unchanged.
4. **Pins:** re-hash the bundle; the fixture's bundle hash and state digest; the procedural goldens (columns searched again if needed);
   `GeneratorVersion` if the generator's output changes (B11 set 2); the atmosphere pins and end-to-end goldens only if they move.
5. **Pictures for the owner** (an owner-review stop), in /home/james/Dev/Vocaterra/artifacts/b-series/b11b/ with an index.md, before
   (feature/biomes at the base) and after from the same places: an entrance from outside (a hillside mouth and a flat-ground one); the walk
   down (two or three stops); a cave wall and floor up close; a cavern; a cliff face and a scree slope in daylight; and the preview tool's
   `slice --mode blocks` through a cave and through a cliff. Pick viewpoints with nothing in front of the camera.
6. **Tests:** the entrance walk above; the variety measure; the valley's surface unchanged; B11's purity, locality, water and region
   tests; the new data's schema and validator.
7. **Documents:** content/README.md (the new underground.json and surface data), the Game.Core README (the underground), docs/game/running.md
   if a flag changes.

Gate: build with -warnaserror (0 warnings); scripts/test.sh full (@@COUNT@@ passed before, plus your new tests); format clean; content
validate. Commit messages end with
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDbkPTqABsu7sLVD9h6

Report, one page: branch, head commit, worktree; how entrances are built and walked; the rock variety (kinds, where, the measure's
figures before and after); new blocks; test counts; pins that moved (old and new, first 12 characters) and why; the pictures and what they
show, plainly; bugs found and fixed; known-not-fixed; files touched outside the milestone's area; anything left undone and why. No file
dumps or long logs.
