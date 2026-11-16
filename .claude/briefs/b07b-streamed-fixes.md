You are implementing B07b, "Streamed-world fixes from the world QA pass", for the Vocaterra repository (C#, .NET 10). The game is
Vocaterra.Game.Core (the engine-free logic), Vocaterra.Game (MonoGame) and Vocaterra.Host; the language core (Vocaterra.Nlp*) is not yours.
This milestone's work is in the game: the scripted steps and the teleport in src/Vocaterra.Game (VoxelGame.cs, Scripted/), the streamed
world's first install round a new place (src/Vocaterra.Game/Streaming, Vocaterra.Game.Core World/Streaming), the respawn's place, and
their tests.
Where to work: the worktree /home/james/Dev/Vocaterra/.claude/worktrees/b07b, already on the local branch `B07b-streamed-fixes` (cut from
`feature/biomes` at 2ec710fd, which holds the streamed world (B07) and the final terrain (B02b)). Run every command from that directory.
Commit after each item that builds and passes its tests, with the prefix `B07b: `. Do not push. Never push or merge feature/biomes or
main. Leave the worktree clean on its branch.
Resources: every dotnet restore, build, test, format and run, every game run, screenshot and scripted run through scripts/limited.sh
(builds `-m:4`, tests `-m:2`); one heavy command at a time, no `&`, no wait loops on `pgrep -f`. Another agent (B12, the underground's
light and a cave tour) builds and runs the game at the same time: expect waits, and say the load beside any frame time. Iterate with
`scripts/test.sh fast` or a project; `scripts/test.sh full` once at the end. Exit 137: lower the parallelism. Environment: prefix every
command with PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1; game runs need DISPLAY=:0 and
VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models; scripted runs use `VOCATERRA_RUNS_WORK=/tmp/claude-1000/b07b-runs`. **Game
windows are allowed (the owner is asleep); look at every screenshot you take with the Read tool.** Quality before speed: do not call a fix
done before you have seen it work in a picture.

The bugs (docs/qa-reports/2026-11-16-world.md, QA-2026-11-16-world-1 and 2; pictures under /tmp/vocaterra-qa/ if they still exist):
1. **A shot right after a teleport shows the view from before it.** `--teleport 212,256 --look 180,-8 --shot a.png` with no wait: a.png
   (/tmp/vocaterra-qa/w13/still-a.png) is the view from the start point, not from (212, 256) by the lake (w14/a.png, taken after
   `--wait 2`, is right). The orchestrator's reading: since B07 part 2a the simulation is held after a start or jump until the columns
   round the body are resident (`ChunkResidency.IsNearReady`, `StreamedWorldHost.IsReadyAround`, the gate in `VoxelGame.Update`), but
   the scripted steps go on while it is held, so the shot runs before the teleport has taken effect. Confirm the cause before you fix it.
2. **A far teleport shows only sky for seconds.** On the live path (`--no-settle`) a teleport to (700, 700) with `--pass open` shows sky
   only at 209 ticks (the body held at y 98) and the whole world after about 1,200 ticks (/tmp/vocaterra-qa/w22/shot.png,
   w23/after20.png). A start does better (one frame of about 630 ms, the synchronous near zone). The same must hold for every far move in
   play: the menu's teleport (`TeleportToPoint`), the respawn at the camp after a death far away (Session/VoxelSession.Death.cs), and a
   load of a save far out.
3. **A teleport onto water stands the body on the bed.** `--teleport 258,118` prints `standing at 258,38,118` and the picture is under
   water in the waterfall's pool (w7/foot-up.png).

Read first: CLAUDE.md; docs/game/implementation-plan.md sections 1, 3, 4.14 (your row, B07b) and 6; docs/game/running.md and
docs/game/scripted-runs.md (the steps, the settle); .claude/briefs/b07-phase-b2a-report.md and b07-phase-b2b-report.md (the hold, the
synchronous near zone at a start, the budgets, `--stream-stats` and its `worst frames` line). Read the sections named, never whole long
files.

Rules: a game milestone edits only the game projects, their tests, content/game-v1, content/schema, assets/ and docs/game/ and takes the
core as it is. A regression check is a C# test. Never edit .github/workflows/, either implementation-plan.md, either decisions.md or
docs/game/biomes-plan.md. Engine code only in src/Vocaterra.Game; Core stays pure. Keep off what B12 changes now: the mesher's light and
`ChunkLightIndex` (Graphics/World), and the residency's underground radius (B12 widens the near zone underground); if you must touch
`ChunkResidency`, keep your change small and say so. The `--world valley` scripted runs must stay identical apart from the known noise.

Deliverables:
1. Scripted steps wait while the body is held: a step after a `--teleport` (a shot, a walk, a look) runs only once the teleport has
   taken effect and the world round the new place is resident; a test that a shot step after a teleport is not taken while held. State
   the hold's length in ticks for a near and a far teleport.
2. A far move in play makes its near zone the way a start does: the menu's teleport, the respawn at the camp from far away and a load far
   out each show the world round the body within about a second (a bounded synchronous near zone, or a drawn loading view while held:
   never a frame of seconds, never seconds of empty sky). State the time to a complete near view and the longest frame for each, before
   and after (Release, High, `--no-settle`, with `--stream-stats`).
3. A teleport onto water stands the body on the water's surface (or on the nearest dry ground within a few blocks: choose, and say why);
   the same rule for the menu's teleport and any other "stand at the ground of this column" helper; a test with a pool and a lake.
4. Pictures (outside git) in /home/james/Dev/Vocaterra/artifacts/b-series/b07b/ with an index.md: before and after for each bug (the shot
   after a teleport; a far teleport at 1 s on the live path; the teleport onto the pool).
5. Documents: docs/game/running.md and docs/game/scripted-runs.md where the steps' timing or the teleport's rule changes.

Gate: build with -warnaserror (0 warnings); scripts/test.sh full (12,267 passed before, plus your new tests); format clean; content
validate. Commit messages end with
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDbkPTqABsu7sLVD9h6

Report, half a page: branch, head commit, worktree; each bug's cause and fix; the timings before and after; tests; the scripted-run
comparison; the pictures; anything left. No file dumps or long logs.
