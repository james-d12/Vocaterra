You are implementing B14, "Integration", for the Vocaterra repository (C#, .NET 10). The game is Vocaterra.Game.Core (the engine-free
logic), Vocaterra.Game (MonoGame DesktopGL 3.8.6) and Vocaterra.Host; the language core (Vocaterra.Nlp*) is not yours.
Where to work: the worktree /home/james/Dev/Vocaterra/.claude/worktrees/b14, already on the local branch `B14-integration` (cut from
`feature/biomes` at 46c05e0f). Run every command from that directory. Commit after each step that builds and passes its tests, with the
prefix `B14: `. Do not push. Never push or merge feature/biomes or main. Leave the worktree clean on its branch.
Resources: every dotnet restore, build, test and format, every game run, scripted run and screenshot through scripts/limited.sh (builds
`-m:4`, tests `-m:2`; the script's caps: 10 GB a command, 16 GB in all, three commands machine-wide); one heavy command at a time, no `&`,
no wait loops on `pgrep -f`. Four other agents build at the same time: expect waits. Iterate with a project or `scripts/test.sh fast`;
`scripts/test.sh full` once at the end. Scratch under /home/james/Dev/Vocaterra/artifacts/tmp/b14/ (not /tmp), removed at the end.
Environment: prefix every command with PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1; game runs need DISPLAY=:0
and VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models; scripted runs use
`VOCATERRA_RUNS_WORK=/home/james/Dev/Vocaterra/artifacts/tmp/b14/runs`. Game windows are allowed (the owner is asleep); look at every
picture with the Read tool; pictures in a Release build. The sandbox refuses the words `eval` and `hash` in a command line: run
`scripts/content.sh hash ...` from a small wrapper script under your scratch folder.

Why: B14 is the B series' integration step (docs/game/biomes-plan.md, its row B14 in section 7 and "The end" in section 4: "B14 ends with
the screenshot tour and the water and cave checks over 3 seeds"); your row in docs/game/implementation-plan.md section 4.14.

Read first: CLAUDE.md; docs/game/implementation-plan.md sections 1, 3, 4.14 and 6; docs/game/biomes-plan.md sections 3 (water, the
underground, the cloud layer note at "Its height becomes data in B14"), 4 and 7; docs/game/architecture.md; docs/game/scripted-runs.md;
docs/game/sound-and-display.md; content/README.md; the reports .claude/briefs/b12-report.md and b13-report.md (the cave tour and the
valley's underground).

Rules: a game milestone edits only the game projects, their tests, content/game-v1, content/schema, assets/ and docs/game/ and takes the
core as it is. A regression check is a C# test. Never edit .github/workflows/, either implementation-plan.md, either decisions.md or
docs/game/biomes-plan.md (say in the report what they should say). Assets are CC0 only (D-game-5): a new sound needs its source and
licence in `content/game-v1/ATTRIBUTION.md` or assets' attribution, as the existing ones do; if no fitting CC0 sound is already in the
repository and none can be fetched with a clear CC0 licence, reuse existing beds and say so. A biome or ore is a row of data: no file under
`src/` names a biome's or an ore's id (the existing `NoBiomeIdsInSource`/`NoOreIdsInSourceTests` stand). The world's blocks do not change:
the 18 procedural goldens, `SceneTests.GoldenDigest` and the save-v9 fixture's state digest must not move (a content edit changes only the
bundle hash: put the new one in `tests/Vocaterra.Game.Core.Tests/Fixtures/save-v9.json`, and run the whole suite, core included, after it).
Other agents are changing nearby code now: B02c (rivers and the drainage network; it will move the world's pins), B07f (the far store,
`StreamedWorldHost`), B21 (the remesh path, `ChunkResidency`, `VoxelGrid.HighestBlock`), M98 (the core's fuzz tests). Keep off their code.

Deliverables:
1. **Ambient sound by place:** the ambient beds chosen from `IPlaceInfo` (src/Vocaterra.Game.Core/World/IPlaceInfo.cs; the sound in
   src/Vocaterra.Game.Core/Sound/AmbientSound.cs, SoundFrontend) by the biome's data (a field in the biome's row naming its bed, not a
   biome id in code) and underground (the depth below the surface or a cave cell: a cave bed, dripping or wind, quieter), with a smooth
   cross-fade and the hand-crafted regions' existing beds kept. Tests: the bed chosen for a place in each biome of `biomes.json` and
   underground; no biome id in source.
2. **Water and edge checks over three seeds** (seeds 42 and two others; say which): every procedural water block at rest when its chunk
   loads (no flow tick changes it), and every water edge exitable (`BankBesideWater`, D-game-26), over a sample of chunks round the pass,
   a river, a lake and the sea for each seed. B03 has these for one seed: widen them, keep them fast (mark `[Trait(TestSpeed.Trait,
   TestSpeed.Slow)]` if over a few seconds). If one fails, find whether the generator or the check is wrong; fix the generator only if the
   fix keeps the goldens, else report it with a picture (B02c is changing the rivers now; a river failure may be its).
3. **Signed coordinates in scripted runs** (`Scripted/VoxelStep.cs`, now `NumberStyles.None`): steps accept negative coordinates; a test.
4. **The cloud layer's height as data:** `SkyModel.Altitude` (168) read from the sky or weather data in content/game-v1 (a field with a
   schema entry and a validator range), default 168; a test that the value read is the value drawn. Bundle re-hashed, fixture updated.
5. **A scripted tour** (a new run under the scripted runs, docs/game/scripted-runs.md): from the valley through the pass into each biome
   of `biomes.json` that can be reached in a few minutes' walk or by a teleport step, down a cave entrance to a cavern with a torch, ending
   on a view of the far land; pictures at each stop, Release, High, midday. **Before the final tour and the full suite, merge feature/biomes
   into your branch** (B02c's rivers and B21 may be in by then) and take the tour on the merged code; say which merges it includes.
   Pictures and an index.md in /home/james/Dev/Vocaterra/artifacts/b-series/b14/ (outside git): one line each on what it shows and
   anything wrong (holes, floating water, odd colours, a bed that does not match).
6. **Documents:** docs/game/architecture.md, docs/game/saves.md, docs/game/running.md (anything new), and "Adding a biome" and "Adding an
   ore" sections in content/README.md (the files, the fields, the validator, the tests that guard it, a worked example from an existing row).

Gate: build with -warnaserror (0 warnings); scripts/test.sh full (12,483 passed on feature/biomes before, plus your new tests); format
clean; `scripts/content.sh validate`; the content lint from CLAUDE.md clean. A timing test that fails under load: rerun it alone and say
so. Commit messages end with
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDbkPTqABsu7sLVD9h6

Report, half a page: branch, head commit, worktree; each deliverable and how; the seeds and what the checks found; the sounds used and
their licences; the tour's pictures with anything wrong; the merges taken; the suite's count; what the plans should say; anything left. No
file dumps or long logs.
