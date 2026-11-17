You are implementing B11c, "Caves breaking the surface", for the Vocaterra repository (C#, .NET 10). The game is Vocaterra.Game.Core (the
engine-free logic), Vocaterra.Game (MonoGame DesktopGL 3.8.6) and Vocaterra.Host; the language core (Vocaterra.Nlp*) is not yours.
Where to work: the worktree /home/james/Dev/Vocaterra/.claude/worktrees/b11c, already on the local branch `B11c-caves-surface` (cut from
`feature/biomes` at BASE). Run every command from that directory. Commit after each step that builds and passes its tests, with the
prefix `B11c: `. Do not push. Never push or merge feature/biomes or main. Leave the worktree clean on its branch.
Resources: every dotnet restore, build, test and format, every game run and screenshot through scripts/limited.sh (builds `-m:4`, tests
`-m:2`; the script's caps); one heavy command at a time, no `&`, no wait loops on `pgrep -f` or on a file (a loop left behind runs forever).
Other agents build at the same time: expect waits. Iterate with a project or `scripts/test.sh fast`; `scripts/test.sh full` once at the
end. Scratch under /home/james/Dev/Vocaterra/artifacts/tmp/b11c/ (not /tmp), removed at the end. Environment: prefix every command with
PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1; game runs need DISPLAY=:0 and
VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models. Game windows are allowed; look at every picture with the Read tool; pictures in a
Release build.

Why: the owner's bug 1 (2026-11-16, seed 42; docs/game/findings.md, the entry "The owner's visual bugs in play"; his screenshot in
/home/james/Dev/Vocaterra/artifacts/owner-bugs/2026-11-16/, outside git): three small pits in a diagonal row in a meadow at (857, 71, 227),
dirt walls, stone and gravel at the bottom: caves breaking the surface on their 4-block lattice. Your row: docs/game/implementation-plan.md
section 4.14, B11c. Since the bug was seen, B02c changed the ground (rivers that only fall, generator version 4) and B11b added entrances
and rock variety, so the exact spot may have moved or healed.

Read first: CLAUDE.md; docs/game/biomes-plan.md section 3 (the underground: the cover rule, entrances, "no cave within reach of water");
docs/game/implementation-plan.md section 4.14; .claude/briefs/b11-report.md, b11b-report.md, b02c-report.md; the underground generation
(`World/Generation`, `underground.json` and its validator, `EntranceSites`) and `tools/Vocaterra.WorldPreview` (its `slice` and entrance
views, which show caves without a game window).

Rules: a game milestone edits only the game projects, their tests, content/game-v1, content/schema, assets/ and docs/game/ and takes the
core as it is. A regression check is a C# test. Never edit .github/workflows/, either implementation-plan.md, either decisions.md or
docs/game/biomes-plan.md (say in the report what they should say). No ore or biome id in source. The fix moves the world: re-record the 18
procedural goldens, the save-v9 fixture's state digest and the atmosphere pins **once**, at the end, and say which moved and why; a content
edit means re-hashing the bundle (the sandbox refuses the words `eval` and `hash` in a command line: run `scripts/content.sh hash ...` from a
wrapper script under your scratch folder) and the whole suite after it. Other agents work now on the remesh and GL passes (B17b), the far
store and worker threads (B07d), guards and timing tests (B24) and integration (B14: ambient sound, water checks, a tour); keep off their code.

Deliverables:
1. **Find the cause:** reproduce on the current world (seed 42 round (857, 71, 227), and a search over seeds 42, 7 and 2026 for any cave
   cell open to the sky outside an entrance's footprint, with WorldPreview or a test). Say why the cover rule lets a cave open at the surface
   (for example the cover measured from the ground before a later pass lowers it, the 4-block lattice's interpolation crossing the cover,
   the river carve or B11b's rock variety removing cover, or the surface pass running after the carve).
2. **Fix it at the source,** so that no cave opens at the surface except through an entrance, without losing the entrances or the caves'
   look underground (the caves' share of the underground volume within a few percent of today's: say the numbers before and after).
3. **A test over three seeds** (42, 7, 2026) that no cave cell is open to the sky outside an entrance, over a sample large enough to have
   caught the bug (say how many columns); fast or marked `[Trait(TestSpeed.Trait, TestSpeed.Slow)]`.
4. Pictures in /home/james/Dev/Vocaterra/artifacts/b-series/b11c/ with an index.md: the owner's spot before and after (Release, High,
   midday, the same camera as his screenshot if it can be found), a WorldPreview slice through it, and an entrance that still works.

Gate: build with -warnaserror (0 warnings); scripts/test.sh full (COUNT passed on feature/biomes before, plus your new tests); format clean;
content validate. A timing test that fails under load: rerun it alone and say so. Commit messages end with
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDbkPTqABsu7sLVD9h6

Report, half a page: branch, head commit, worktree; the cause; the fix; the cave volume before and after; the test and its sample; pins
moved; the pictures; anything left. No file dumps or long logs.
