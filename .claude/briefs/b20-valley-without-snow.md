You are implementing B20, "No snow in the valley", for the Vocaterra repository (C#, .NET 10). The game is Vocaterra.Game.Core (the
engine-free logic), Vocaterra.Game (MonoGame) and Vocaterra.Host; the language core (Vocaterra.Nlp*) is not yours. This milestone's work is
the hand-crafted valley's build: content/game-v1/game/scene.json (the terrain's `snowLine`), src/Vocaterra.Game.Core/World/Generation/
SceneBuilder.cs (the snow rule, about line 175), and their tests and pins.
Where to work: the worktree /home/james/Dev/Vocaterra/.claude/worktrees/b20, already on the local branch `B20-valley-without-snow` (cut from
`feature/biomes` at b4343875). Run every command from that directory. Commit after each step that builds and passes its tests, with the
prefix `B20: `. Do not push. Never push or merge feature/biomes or main. Leave the worktree clean on its branch.
Resources: every dotnet restore, build, test and format, every scripts/content.sh call, every game run and screenshot through
scripts/limited.sh (builds `-m:4`, tests `-m:2`; the script's caps); one heavy command at a time, no `&`, no wait loops on `pgrep -f`. Four
other agents build at the same time: expect waits. Iterate with `scripts/test.sh project tests/Vocaterra.Game.Core.Tests`; `scripts/test.sh
full` once at the end. Scratch on disk under /home/james/Dev/Vocaterra/artifacts/tmp/b20/, removed at the end. Environment: prefix every
command with PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1 (the sandbox refuses the words `eval` and `hash` in a
command line: for `scripts/content.sh hash` use a two-line wrapper script); game runs need DISPLAY=:0 and
VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models. Game windows are allowed; look at every picture with the Read tool. Use a Release
build for pictures.

The owner (2026-11-16, D-game-34 item 36): "can you add a milestone to remove the snow from the valley, it now doesnt make sense to have snow
on the mountain when the valley with the procedural generated area". The valley lays snow on its ring's tops above `snowLine` 100 (with a
noise of a few blocks) where the slope is gentle (`SceneBuilder.cs:175`: `h >= spec.SnowLine + noise - 3 && slope <= 5` gives Snow over
Stone), while the generated land round it has snow only above y 120 on its mountains.

Read first: CLAUDE.md ("Game content bundle"); docs/game/implementation-plan.md sections 1, 3, 4.14 (your row, B20) and 6; the valley's
tests (SceneTests, VillageTests, PassTests, PassLockTests, ValleyUndergroundTests) and `SceneBuilder` round the snow rule.

Rules: a game milestone edits only the game projects, their tests, content/game-v1, content/schema, assets/ and docs/game/ and takes the
core as it is. A regression check is a C# test. Never edit .github/workflows/, either implementation-plan.md, either decisions.md or
docs/game/biomes-plan.md. Snowfall as weather is unchanged (only the snow blocks laid by the valley's build go). Keep off what other agents
change now (Graphics/Far, World/Lod, Streaming, ChunkMesher's light and colours, the rivers).

Deliverables:
1. **No snow blocks in the valley's build:** the tops that were snow take what the slope would otherwise carry (stone above `rockLine` or on
   steep ground, grass or mossy stone below, as the existing rule does); remove the snow rule and the `snowLine` field cleanly (schema and
   validator too), or keep the field with a value that lays none if removal ripples too far (say which and why).
2. **Pins:** `SceneTests.GoldenDigest` moves once (say from what to what); re-hash the bundle; the fixture save-v9.json's bundle hash and
   state digest if they move (the valley's blocks are part of the region template: check); the atmosphere pins and end-to-end goldens only
   if they move. Re-record nothing that does not move. VillageTests, PassTests, PassLockTests and ValleyUndergroundTests pass unchanged.
3. **A test** that the valley's build lays no snow block anywhere.
4. **Pictures** (an owner-review stop for the valley's look) before and after from the same places, in
   /home/james/Dev/Vocaterra/artifacts/b-series/b20/ with an index.md: the ring's peaks from the camp, from the village, and from the pass
   looking back; and the valley seen from outside (from a hill in the streamed world, where the far view draws it). Look at each.
5. The scripted runs (`--world valley`): the pictures with the peaks change (say which); nothing else.

Gate: build with -warnaserror (0 warnings); scripts/test.sh full (12,399 passed before, plus your new tests); format clean; content
validate. Commit messages end with
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDbkPTqABsu7sLVD9h6

Report, half a page: branch, head commit, worktree; what changed; pins (old and new, first 12 characters); tests; pictures; anything left.
No file dumps or long logs.
