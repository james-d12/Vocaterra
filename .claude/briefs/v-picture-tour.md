You are taking pictures of the Vocaterra game for its owner (C#, .NET 10; a Spanish-learning voxel game on MonoGame). This is not a
milestone: **change no source, test, content or document in the repository and commit nothing.** You take in-game screenshots, look at each,
and write an index outside git.

The owner (2026-11-16): "once that B07c agent is done, I need you to send screenshots of how the world looks with different seed
generations. Also get an image of a mountain and an ocean and a cave as well." `feature/biomes` now holds the streamed world (B07), the far
view built like Voxy (B07c: 3D block detail out to 16 km at High), the terrain (B02, B02b), the underground with rock variety and natural
cave entrances (B11, B11b), the light underground (B12) and the pass (B08a to B08c).

Where to work: the worktree /home/james/Dev/Vocaterra/.claude/worktrees/tour (detached at `feature/biomes` e9798005; yours alone). Build it
once (`scripts/limited.sh dotnet build Vocaterra.sln -c Release -m:4`) and run the game from that build (a Release build: the far view
builds much faster). Every build and game run through scripts/limited.sh; one heavy command at a time, no `&`, no wait loops on `pgrep -f`.
Other agents build at the same time: expect waits. Environment: prefix every command with PATH="$HOME/.dotnet:$PATH"
DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1; game runs need DISPLAY=:0 and VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models; use a
session folder of your own under /home/james/Dev/Vocaterra/artifacts/tmp/tour/ (on disk; /tmp is a small shared RAM disk) and remove it
at the end. Game windows are allowed.

Read first: docs/game/running.md (launch options: `--seed`, `--quality high`, `--time 12`, `--weather clear` or the sunny id, `--pass
open`, `--teleport x,z`, `--teleport-to x,y,z`, `--look yaw,pitch`, `--wait`, `--shot`, `--screenshot`, `--stream-stats`, `--world`);
docs/game/scripted-runs.md (how a settled shot waits for the far view); the preview tool's README (tools/Vocaterra.WorldPreview:
`heights`, `biomes`, `stats`, `entrances`, `river`: use it to find good places on each seed before you run the game).

Take, at High, midday, clear weather, settled (the stream line shows 0 far tiles waiting before the shot), 1280 x 720 or larger:
1. **Different seeds:** for seeds 42, 7, 2026, 1 and 99999, one wide view each from a high, open viewpoint (a hilltop or a summit, with
   nothing in front of the camera) that shows the land to the horizon: ranges, forests, rivers, lakes or the sea, so the owner can compare
   how each seed generates. Use the preview tool's maps to choose; say in the index what the picture looks at.
2. **A mountain range:** a chain of peaks with snow seen from a distance (from a foothill or across a valley), so the range fills the
   frame; and one from on a summit looking along the spine.
3. **An ocean:** a coast with the sea to the horizon, from a cliff or a hill above the shore; and one at the beach at eye level.
4. **A cave (wait for the orchestrator's message before this item: B11b, the natural entrances and rock variety, is merging; take items 1 to 3 first, then rebuild after the message):** one natural entrance from outside (a hillside mouth if you can find one, else a flat-ground one, from about 8 blocks), the
   walk inside the passage, and a cavern lit by a lava lake (B11b's tour stops in docs/game/scripted-runs.md, `r50-cave-tour`, are known
   good places on seed 42: the mouth 475,186; the landing 468,62,153; the cavern 504,-34,176; the lava lake 528,-53,190). A placed torch is
   fine where it is dark.
Look at every picture with the Read tool; retake any where a tree, a wall or a hill blocks the view, the far land has not filled in, or the
picture is dull; pick the best one or two of each kind.

Write the pictures and an index.md (file, seed, place x,y,z, yaw and pitch, what it shows, the frame time from `--stream-stats`) into
/home/james/Dev/Vocaterra/artifacts/b-series/tour/ (outside git). Report (half a page): the list of pictures with one line each, anything
that looked wrong in the game (with how to reproduce it), and the folder. Do not remove the worktree (the orchestrator does).
