You are implementing B13, "The valley's underground", for the Vocaterra repository (C#, .NET 10). The language core is the engine
Vocaterra.Nlp, the Spanish pack Vocaterra.Nlp.Es and the Vocaterra.Nlp.* test and tool projects; the game is Vocaterra.Game.Core (the
engine-free logic), Vocaterra.Game (MonoGame), Vocaterra.Host and tools/Vocaterra.SpeechRender. This milestone's work is in the game:
the valley's build in Vocaterra.Game.Core (World/Generation/SceneBuilder and the region template), World/Underground, and their tests.
Where to work: the worktree /home/james/Dev/Vocaterra/.claude/worktrees/b13, already on the local branch `B13-valley-underground` (cut from
`feature/biomes` at d26c7a45, which holds B01 to B11, B04b, B08a, B08b, B10b, B15 and core M96). Run every command from that directory.
Commit after each item that builds and passes its tests, with the prefix `B13: `. Do not push your branch. Never push or merge
feature/biomes or main. When you finish, leave the worktree on your branch, clean, and remove any extra worktree you made.
Resources (the machine has crashed from overload): run every dotnet restore, build, test, format and run, every scripts/content.sh call
and every run of the preview tool through scripts/limited.sh (builds `-m:4`, tests `-m:2`). scripts/test.sh calls it itself. Never start
two heavy commands at once (no `&`, no background builds), and write no shell wait loop on `pgrep -f` (wait for your command's own
completion). Up to three other agents build at the same time; expect waits. While iterating run `scripts/test.sh fast` or
`scripts/test.sh project tests/Vocaterra.Game.Core.Tests`; run `scripts/test.sh full` once at the end. Exit 137 means the 6 GB cap killed
it: lower the parallelism. No game windows: do not run the game, screenshots or scripts/scripted-runs.sh; the orchestrator re-baselines
the scripted runs with the owner later.
Environment: prefix every command inline with PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1 (the sandbox refuses
the words `eval` and `hash` in a command line: for `scripts/content.sh hash` write a two-line wrapper script in /tmp/claude-1000/). Model-
gated tests and the end-to-end goldens need VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models.

Read first: CLAUDE.md (in particular "Game content bundle" and the end-to-end goldens); docs/game/implementation-plan.md sections 1, 3,
4.14 (your row) and 6; docs/game/biomes-plan.md sections 3.5 ("Under the valley") and 3.6 (the valley as an `AuthoredReliefRegion`; its
goldens hold until B13), and section 4 (your row: the valley's goldens are re-recorded once, with the owner's review); D-game-34 items 8
and 9 (nothing gatherable; lava). The reports you build on, in .claude/briefs/: b11-report.md (underground.json, the passes, the water rule,
the procedural underground under the valley capped at y -9, entrances never in a region or its margin), b06-report.md (the region
template: built once by `SceneBuilder`, frozen; its template digest goes into the state header), b08b-report.md and b08a-report.md (the pass
and the rockfall), b10b-report.md. The code: src/Vocaterra.Game.Core/World/Generation/SceneBuilder.cs (how the valley's stone is laid),
World/Regions/AuthoredReliefRegion.cs, World/Underground/, the valley's tests (SceneTests, VillageTests, PassTests, PassLockTests). Read the
sections named, never whole long files.

Rules: a game milestone edits only the game projects, their tests, content/game-v1, content/schema, assets/ and docs/game/ and takes the
core as it is. A regression check is a C# test. Never create or edit anything under .github/workflows/; do not edit either
implementation-plan.md, either decisions.md or docs/game/biomes-plan.md (the orchestrator does); engine code only in src/Vocaterra.Game
(none needed). Generation stays pure (no floating point, clock or System.Random). Keep off the files other agents are changing now:
world.json and biomes.json, World/Climate, the height model and the river and lake code (B02b is retuning the terrain); src/Vocaterra.Game
(B07); content/game-v1/game/scene.json (no layout change is needed: the valley's surface does not change).

Deliverables:
1. The valley's own stone gets the underground: B11's strata (stone, slate below y 0 with the dither, granite and limestone blobs), its
   ores and its caves, laid into the valley's stone when the valley is built (so the region template carries them and every reader,
   bounded or streamed, sees the same blocks). Where the valley's stone meets the procedural underground below it (y 0 and the -9 cap),
   the caves and strata continue without a seam.
2. **The valley's surface does not change:** no cave, ore or other new block within 8 blocks of any air or water cell of the valley (a
   3D distance, so cliff faces and the lake bed stay as they are), and none within a margin of the valley's sites, paths, props, the
   village, the pass and the rockfall (numbers in underground.json, a new `valley` section with schema and validator). No entrance opens
   in the valley. Everything the player can see, walk on or edit near the surface today stays identical; only buried stone changes.
3. Pins: `SceneTests.GoldenDigest` moves once (say from what to what and why); the region template digest, the fixture's state digest
   (tests/Vocaterra.Game.Core.Tests/Fixtures/save-v9.json), the atmosphere pins and the end-to-end goldens if they move (re-record with
   `VOCATERRA_RECORD_GOLDEN=1` through limited.sh and check the diff moves only digest lines). Re-hash after editing underground.json.
   Never loosen a test: a test that pins the valley's surface must still pass unchanged.
4. Tests: no new block within 8 of air or water anywhere in the valley; none within the margins of sites, paths, props, the village, the
   pass and the rockfall; the valley's surface blocks (every column's top and the faces of its cliffs) identical to before; the strata,
   ores and caves present in the valley's deep stone (counts over the valley); no seam at y 0 between the valley's caves and the
   procedural ones; the bounded and streamed valley still equal; VillageTests, PassTests and PassLockTests pass unchanged; the build time
   of the valley before and after (it must stay within a few tens of milliseconds of today's).
5. Pictures for the owner's review (D-game-34 item 12; this is the series' "valley golden" review stop), with the preview tool's `slice
   --mode blocks` round the valley: slices through the village, the lake, the ring's peaks and the pass, before and after, in
   /home/james/Dev/Vocaterra/artifacts/b-series/b13/ (outside git) with an index.md. The 3D views come later (windows).
6. Documents: the Game.Core README (the valley's underground) and content/README.md (the `valley` section).

Gate (from the worktree root): scripts/limited.sh dotnet build Vocaterra.sln -warnaserror -m:4 (0 warnings); scripts/test.sh full
(12,087 passed before your change, plus your new tests) run after your last change; scripts/limited.sh dotnet format Vocaterra.sln
--verify-no-changes clean; scripts/limited.sh scripts/content.sh validate passes. Do not commit bin/, obj/ or artifacts/.
Commit messages end with
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDbkPTqABsu7sLVD9h6

Report, one page: branch, commit SHA, worktree path; what was built (how the passes reach the valley, the margins and their numbers);
test counts; every pin that moved, old and new value (first 12 characters) and why; the valley's build time before and after; the
pictures' folder; bugs found and fixed; known-not-fixed with reasoning; files touched outside the milestone's area; anything left undone
and why. Leave out file dumps and long logs.
