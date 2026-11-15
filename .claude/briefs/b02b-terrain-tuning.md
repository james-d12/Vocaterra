You are implementing B02b, "Terrain tuning after the owner's map review", for the Vocaterra repository (C#, .NET 10). The language core
is the engine Vocaterra.Nlp, the Spanish pack Vocaterra.Nlp.Es and the Vocaterra.Nlp.* test and tool projects; the game is
Vocaterra.Game.Core (the engine-free logic), Vocaterra.Game (MonoGame), Vocaterra.Host and tools/Vocaterra.SpeechRender. This milestone's
work is in the game: content/game-v1/game/world.json and biomes.json, the height model and climate in Vocaterra.Game.Core, and their tests.
Where to work: the worktree /home/james/Dev/Vocaterra/.claude/worktrees/b02b, already on the local branch `B02b-terrain-tuning` (cut from
`feature/biomes` at @@BASE@@, which holds B01, B03, B04a, B05 and the preview tool B02). Run every command from that directory. Commit after
each step that builds and passes its tests, with the prefix `B02b: `. Do not push your branch. Never push or merge feature/biomes or main.
When you finish, leave the worktree on your branch, clean, and remove any extra worktree you made.
Resources (the machine has crashed from overload): run every dotnet restore, build, test, format and run, every scripts/content.sh call
and every run of the preview tool through scripts/limited.sh (for example `scripts/limited.sh dotnet build Vocaterra.sln -warnaserror
-m:4`; tests with `-m:2`; `scripts/limited.sh dotnet run -c Release --project tools/Vocaterra.WorldPreview -- ...`). scripts/test.sh
calls it itself: run it directly. Never start two heavy commands at once (no `&`, no background builds, no parallel test runs), and write
no shell wait loop on `pgrep -f`. Other agents build at the same time; limited.sh queues you all. While iterating run
`scripts/test.sh project tests/Vocaterra.Game.Core.Tests` and the preview tool; run `scripts/test.sh full` once at the end, after your last
change. Exit 137 means the 6 GB cap killed it: lower the parallelism.
Environment: prefix every command inline with PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1 (the sandbox may
reject a bare `export`, and it refuses a command line containing the shell builtin words `eval` or `hash`: for `scripts/content.sh hash`
use a wrapper script outside the repository).

**The owner's review of B02's maps (2026-11-15, D-game-34 item 16), which this milestone carries out:**
1. **Mountains: long ranges**, like Minecraft 1.18: mountain chains hundreds of blocks wide, with foothills and high meadows rising to a
   spine of peaks at 200 to 300; rarer, but big when found. Today they are needles about 50 blocks wide shooting to about 290 straight out
   of flat land at 40 to 60 (only 5% of land above 150, 2% above 200, 0.3% above 250), with lone spires and long steep plateau rims.
2. **Seas: big seas and continents**: large connected oceans with continents thousands of blocks across, gentle coastal shelves and wide
   beaches, and inland basins become ordinary lakes (lake-basin), not "ocean". Today about 14% of the map is ocean, on seeds 42 and 7
   nearly all of it scattered inland basins, coasts drop 15 to 20 blocks within 10 blocks of the shore, sea floors are flat at y 10 to 22
   (no deep ocean) and the beach is a strip a few blocks wide.
3. **Snow only up high**: cold lowland stays green spruce taiga; snow lies only on the mountains above the snow line. Today snow covers
   cold lowland near the sea.
Also fix what B02 found and the owner did not need to rule on: abrupt cliffs between lowlands and upland shelves; rivers that start and
stop in lowland without reaching a sea or lake, and closed river loops (seed 42 near x 5780, z 1424); B01's known gap that peaks are not
faded over the sea (rugged islands). Rivers should read as long rivers that run to a lake or the sea.

Read first: CLAUDE.md (in particular "Game content bundle"); docs/game/implementation-plan.md sections 1, 3, 4.14 (your row) and 6;
docs/game/biomes-plan.md sections 3.4 (the surface model) and 7 (risk 9); D-game-34 items 13 and 16 in docs/game/decisions.md;
artifacts/b-series/b02/index.md (outside git: /home/james/Dev/Vocaterra/artifacts/b-series/b02/index.md) and B02's maps there;
.claude/briefs/b01-report.md and b03-report.md (the design choices and gaps). The code and data: content/game-v1/game/world.json and
biomes.json (their `about` comments explain every number), src/Vocaterra.Game.Core/World/Climate/, World/Generation/Procedural/
(`HeightModel`, `ColumnShape`, the passes), World/Biomes/, World/WorldGenData.cs and their schemas in content/schema;
tools/Vocaterra.WorldPreview/README.md (the commands). Read the sections named, never whole long files.

Rules: a game milestone edits only the game projects, their tests, content/game-v1, content/schema, assets/ and docs/game/ and takes the
core as it is. A regression check is a C# test. Never create or edit anything under .github/workflows/; do not edit either
implementation-plan.md, either decisions.md or docs/game/biomes-plan.md (the orchestrator does). No floating point, no clock, no
System.Random in generation (the purity tests). Prefer changing numbers in world.json and biomes.json; change the height model or the
climate's code only where a number cannot express the shape (for example a coastal shelf term in the base spline, peaks faded by
continentalness, a wider ridge field with a foothill falloff), and keep every new parameter in world.json with its schema and validator.
No biome id in src (the existing test). The valley stays byte-identical (`SceneTests.GoldenDigest` does not move). Do not overlap the
other agents: do not edit src/Vocaterra.Game (B03b, B07), src/Vocaterra.Game.Core/Voxel/, Save/, World/Streaming or World/Regions
(B06), or content/game-v1/game/scene.json.

Deliverables:
1. The tuned world: world.json and biomes.json (and code where needed) so the three points above hold for seeds 42, 7 and 2026. Targets
   to aim for and report (judge by the maps, not only the numbers): oceans connected, each seed's largest sea well over half of all ocean
   columns; inland water classed lake-basin; land above 150 several times today's 5%, in chains rather than spots; the steepest step
   between neighbouring columns no worse than today's test bound (9) outside terraces and crags, and plateau rims gone or softened; a
   coastal shelf (land falls to the sea over tens of blocks; the sea floor deepens offshore to y 0 or below far from land); snow only
   where the ground is above the snow line on a mountain; rivers that end in a lake or the sea in most of a sample.
2. Tests: the existing ones pass (re-pin B01's and B03's shape and golden tests where the tuning moves them, never loosening a bound
   to pass); new tests for the targets you can state as numbers over a sample (ocean connectivity, share above 150, no snow below a
   stated height at the equator of the temperature field, rivers ending in water).
3. Content: re-hash after the JSON edits as CLAUDE.md says, validate, and put the new bundle hash into
   tests/Vocaterra.Game.Core.Tests/Fixtures/save-v9.json (B05 renamed the fixture; the state digest there depends only on world.json,
   biomes.json and underground.json, so re-read it with a one-time digest read-out if your edit moved it).
4. **Pictures for the owner** (D-game-34 item 12): the same maps B02 drew (seeds 42, 7, 2026: biome and heights at 16,384 blocks and 16
   per pixel, the pair at 4,096 and 4 per pixel centred on the highest range, and slices across a range, a coast and a river), with the
   preview tool, in /home/james/Dev/Vocaterra/artifacts/b-series/b02b/ (outside git), and an `index.md` there that sets each new map
   beside B02's old one (by file name) and says in a line what changed, plus the before and after numbers (biome shares, share above
   150/200/250, ocean connectivity, coast slope).

Gate (from the worktree root): scripts/limited.sh dotnet build Vocaterra.sln -warnaserror -m:4 (0 warnings); scripts/test.sh full
(@@COUNT@@ passed before your change, plus your new tests) run after your last change; scripts/limited.sh dotnet format Vocaterra.sln
--verify-no-changes clean; scripts/limited.sh scripts/content.sh validate passes; `SceneTests.GoldenDigest` unchanged. Do not commit bin/,
obj/ or artifacts/.
Commit messages end with
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDbkPTqABsu7sLVD9h6

Report, one page: branch, commit SHA, worktree path; what changed (numbers and code) and why; the before and after figures; test counts;
the pins that moved; the pictures' folder; what still looks wrong, described plainly; files touched outside the milestone's area;
anything left undone and why. Leave out file dumps and long logs.
