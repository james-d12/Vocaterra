You are implementing B08a, "The pass through the east ring", for the Vocaterra repository (C#, .NET 10). The language core is the engine
Vocaterra.Nlp, the Spanish pack Vocaterra.Nlp.Es and the Vocaterra.Nlp.* test and tool projects; the game is Vocaterra.Game.Core (the
engine-free logic), Vocaterra.Game (MonoGame), Vocaterra.Host and tools/Vocaterra.SpeechRender. This milestone's work is in the game:
content/game-v1/game/scene.json, and the tests, fixtures and documents that pin the valley.
Where to work: the worktree /home/james/Dev/Vocaterra/.claude/worktrees/b08a, already on the local branch `B08a-the-pass` (cut from
`feature/biomes` at 3a56ed18). Run every command from that directory. Commit after each item that builds and passes its tests, with the
prefix `B08a: `. Do not push your branch: it stays local and the gate merges it from there (owner, 2026-11-05). Never push or merge
feature/biomes or main. When you finish, leave the worktree on your branch, clean.
Resources (the machine has crashed from overload): run every dotnet restore, build, test, format and run, every scripts/content.sh call,
speech prerender and screenshot run through scripts/limited.sh (for example `scripts/limited.sh dotnet build Vocaterra.sln -warnaserror
-m:4`; tests with `-m:2`). scripts/test.sh, scripts/content-loop.sh and scripts/scripted-runs.sh call it themselves: run them directly.
Never start two heavy commands at once (no `&`, no background builds, no parallel test runs), and write no shell wait loop on `pgrep -f`
(it matches its own command line and never ends). Two other agents (B01, B04a) build in their own worktrees at the same time; limited.sh
queues you all, so expect waits. While iterating run `scripts/test.sh fast` or `scripts/test.sh project tests/Vocaterra.Game.Core.Tests`;
run `scripts/test.sh full` (the whole suite, as the gate) once before reporting. Exit 137 means the 6 GB cap killed it: lower the
parallelism, do not work around the cap.
Environment: prefix every command inline with PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1 (the sandbox may
reject a bare `export`, and it refuses a command line containing the shell builtin words `eval` or `hash`: for `scripts/content.sh hash`
use a wrapper script outside the repository, for example in /tmp). Model-gated tests and the game's runs need
VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models; runs of the game also need DISPLAY=:0.

Read first: CLAUDE.md (in particular "Game content bundle"); docs/game/implementation-plan.md sections 1, 3, 4.14 (your row) and 6;
docs/game/biomes-plan.md section 3.6 ("The pass") and section 4 (the B08a and B08b rows); D-game-34 in docs/game/decisions.md (items 6
and 10); content/README.md (the edit loop); docs/game/running.md (`--teleport`, `--look`, `--screenshot`); docs/game/scripted-runs.md
("How to run them" and "Comparing"). The data: content/game-v1/game/scene.json (its comments explain each section: `terrain.relief`,
`sites`, `paths`, `stamps`, `props`) and content/game-v1/game/land.json (path blocks are protected as roads, so a road of path blocks is
protected with no change there). Read the sections named, never whole long files (SceneBuilder.cs and TerrainGenerator.cs are long: grep).

Rules: a game milestone edits only the game projects, their tests, content/game-v1, assets/ and docs/game/ and takes the core as it is
(a missing core call stops that part: report it, do not copy core source or edit a Vocaterra.Nlp project or docs/nlp/). A regression
check is a C# test, never a script with text baselines. Never create or edit anything under .github/workflows/; do not edit either
implementation-plan.md, either decisions.md or docs/game/biomes-plan.md (the orchestrator does); engine code only in src/Vocaterra.Game
(D-game-1); no System.Random and no clock in the simulation. Tests are Given_When_Then in folders that mirror the source.
Scope: data, not code. Prefer to build everything with the layout features scene.json already has; if one is missing (for example a
stamp that must sit on sloped ground), report it, and change code only where the change is small and clearly needed. Do not overlap the
other two agents: do not edit src/Vocaterra.Game.Core/Voxel/, src/Vocaterra.Game.Core/Save/ (B04a), src/Vocaterra.Game.Core/World/Noise,
World/Climate, World/Biomes, World/Generation/Procedural, src/Vocaterra.Game.Core/Content/ or content/game-v1/game/world.json and
biomes.json (B01). Not in scope: the unlock rule, the required-lessons list, the cheat setting and removing the rockfall (B08b); the
streamed world, the edge wall and the exit corridor (B06, B07); any Spanish word, label or line (the `paso` site has no `label`).

Deliverables:
1. The pass: lower the east side of the ring where it is already lowest. In `terrain.relief` rows 12 to 14, columns 22 and 23 are `f` and
   `k` (heights 60 and 80; column 21 is already `b`, height 44); lower them to `c` and `d` (heights 48 to 52), adjusting neighbours only as
   far as needed for a walkable slope (the plan's section 3.6). Check the result by walking it, not only by the numbers.
2. The road: a new path of path blocks from the village (about x 316, z 218, where the existing road comes in) east through the pass to
   the east edge at x 383, with the `mode` that keeps it walkable on the slope (`follow` on the floor, `grade` where it climbs).
3. The `paso` site at the top of the pass, with no `label` (no new word in content).
4. The rockfall: a stamp of existing blocks (stone, cobble, mossy stone and the like; a heap, not a wall) placed as a prop across the road
   where it meets the ring, so the pass is closed: the body cannot climb over it or round it at any point, and the edge beyond stays out
   of reach. A comment says that B08b removes it when the pass opens. Give the prop an id B08b can find it by.
5. The valley's pins move: re-record `SceneTests.GoldenDigest` (and its comment, in the style of the entries already there), every other
   digest or golden the edit moves (the state digests, `AtmosphereSimulationTests`, the save fixtures), the bundle hash (run
   `scripts/content.sh hash content/game-v1 --bundle-id game-v1` from a wrapper script, then validate, then put the new hash into
   tests/Vocaterra.Game.Core.Tests/Fixtures/save-v8.json), and the end-to-end goldens only if they moved. Never weaken a test to pass.
6. Tests: the road reaches the east edge from the village with every step walkable; the rockfall blocks the body (no walkable route from
   the road past it to the east edge while it stands); the protected road covers the new path; water at rest and every water edge still
   exitable (the existing checks pass).
7. Scripted runs: capture before your first change (`scripts/scripted-runs.sh <out-before>`) and after (`<out-after>`) with the same work
   folder, compare with `python3 -I scripts/compare-runs.py`, and report which runs moved and why (expected: only views that include the
   east ring or the new road). Do not re-baseline anything in the repository from them; the owner reviews the pass at B08b.
8. Pictures for the owner: three screenshots with `--teleport` and `--look` and `--screenshot` (from the village looking east along the
   road, at the rockfall, and from the top of the pass looking back west over the valley), saved in
   /home/james/Dev/Vocaterra/artifacts/b-series/b08a/ (outside git; create the folder).
9. Documents: the scene's comments; a row in docs/game/scripted-runs.md only if you add a run (not required).

Gate (from the worktree root): scripts/limited.sh dotnet build Vocaterra.sln -warnaserror -m:4 (0 warnings); scripts/test.sh full
(11,498 passed before your change, plus your new tests); scripts/limited.sh dotnet format Vocaterra.sln --verify-no-changes clean;
scripts/limited.sh scripts/content.sh validate passes; the content gate `scripts/limited.sh scripts/content.sh lint content/game-v1
--strict --level a2 --fail-on-level --host-pools 'guide.*'` clean. Do not commit bin/, obj/ or artifacts/.
Commit messages end with
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDbkPTqABsu7sLVD9h6

Report, one page: branch, commit SHA, worktree path; what was built (the relief cells changed, the road, the site, the rockfall and its
prop id); test counts; the pins that moved (old and new golden digest); the scripted runs that moved and why; the screenshots' paths;
bugs found and fixed (where, symptom, fix, test); layout features that were missing; known-not-fixed with reasoning; files touched
outside the milestone's area; anything left undone and why. Leave out file dumps and long logs.
