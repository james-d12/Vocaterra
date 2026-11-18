You are implementing B02b, "Terrain tuning after the owner's map review", for the Vocaterra repository (C#, .NET 10). The language core
is the engine Vocaterra.Nlp, the Spanish pack Vocaterra.Nlp.Es and the Vocaterra.Nlp.* test and tool projects; the game is
Vocaterra.Game.Core (the engine-free logic), Vocaterra.Game (MonoGame), Vocaterra.Host and tools/Vocaterra.SpeechRender. This milestone's
work is in the game: content/game-v1/game/world.json and biomes.json, the height model and climate in Vocaterra.Game.Core, and their tests.
Where to work: the worktree /home/james/Dev/Vocaterra/.claude/worktrees/b02b, already on the local branch `B02b-terrain-tuning` (cut from
`feature/biomes` at 20e75aa5, which holds B01 to B06, B04b and B08a, with the preview tool B02 and the region blend B06). Run every command from that directory. Commit after
each step that builds and passes its tests, with the prefix `B02b: `. Do not push your branch. Never push or merge feature/biomes or main.
When you finish, leave the worktree on your branch, clean, and remove any extra worktree you made.
Resources (the machine has crashed from overload): run every dotnet restore, build, test, format and run, every scripts/content.sh call
and every run of the preview tool through scripts/limited.sh (for example `scripts/limited.sh dotnet build Vocaterra.sln -warnaserror
-m:4`; tests with `-m:2`; `scripts/limited.sh dotnet run -c Release --project tools/Vocaterra.WorldPreview -- ...`). scripts/test.sh
calls it itself: run it directly. Never start two heavy commands at once (no `&`, no background builds, no parallel test runs), and write
no shell wait loop on `pgrep -f`. The owner is using the machine: prefix every heavy command with VOCATERRA_HEAVY_SLOTS=1 (one machine-wide slot, shared with the
orchestrator's merge gate), and expect waits. No game windows: do not run the game or scripts/scripted-runs.sh. While iterating run
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
4. **Rivers near the valley start in the hills** (2026-11-15, D-game-34 item 18). Today B06's blend fades rivers and lakes across its
   64-block margin, so a river heading for the valley stops in a rounded end against the rising ground (see
   /home/james/Dev/Vocaterra/artifacts/b-series/b06/heights-valley-seed42-x192-z192-s768-p1.png). Instead, near a region a river
   becomes a headwater: over a distance set in world.json (a few hundred blocks outside the margin) it narrows and shallows to a
   stream and starts at a spring or a small pond on the slopes round the valley, its channel floor rising to meet the ground so no
   dead-end trench or wall of water is left, and it flows away to a lake or the sea like any other river. No river reaches into the
   margin. A lake the margin would cut is kept whole outside it (shrunk or moved off), never cut by a straight edge (seed 7 has one at
   the valley's south side: `--world valley --seed 7 --x 192 --z 192 --size 1024 --scale 2`). On seed 7 a steep snowy ridge stands at
   the valley's east margin (B06's report): say whether the tuning removes it. All of this must hold for any region's size and
   position (B16 makes regions data), so read the distance to the region from B06's
   blend, never from valley constants.
Also fix what B02 found and the owner did not need to rule on: abrupt cliffs between lowlands and upland shelves; rivers that start and
stop in lowland without reaching a sea or lake, and closed river loops (seed 42 near x 5780, z 1424); B01's known gap that peaks are not
faded over the sea (rugged islands). Rivers should read as long rivers that run to a lake or the sea.

Read first: CLAUDE.md (in particular "Game content bundle"); .claude/briefs/b06-report.md and
/home/james/Dev/Vocaterra/artifacts/b-series/b06/index.md (the region blend, its `regionBlend` numbers and its tests); docs/game/implementation-plan.md sections 1, 3, 4.14 (your row) and 6;
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
No biome id in src (the existing test). The valley stays byte-identical (`SceneTests.GoldenDigest` does not move). Keep off the files
other milestones own: src/Vocaterra.Game (B03b, B07), src/Vocaterra.Game.Core/Voxel/, Save/, World/Streaming and
content/game-v1/game/scene.json. In World/Regions (B06) and world.json's `regionBlend` section change only what the headwaters need,
and keep B06's tests passing (the steps across the border, the land bias, the streamed valley equal to the bounded one). If the
tuning moves the coast so B06's land-bias test fails, nudge its `shoreFloor` (data), as B06's report says. Report the ring walk
(B06's test: start columns that can walk out of the valley, step up 1, down 3) for seeds 42, 7 and 2026; B06 had 192, 181 and 192 of
192 at margin 64. Do not change the margin: if seed 7 stays under 97%, say so (the owner's call).

Deliverables:
1. The tuned world: world.json and biomes.json (and code where needed) so the three points above hold for seeds 42, 7 and 2026. Targets
   to aim for and report (judge by the maps, not only the numbers): oceans connected, each seed's largest sea well over half of all ocean
   columns; inland water classed lake-basin; land above 150 several times today's 5%, in chains rather than spots; the steepest step
   between neighbouring columns no worse than today's test bound (9) outside terraces and crags, and plateau rims gone or softened; a
   coastal shelf (land falls to the sea over tens of blocks; the sea floor deepens offshore to y 0 or below far from land); snow only
   where the ground is above the snow line on a mountain; rivers that end in a lake or the sea in most of a sample.
2. Tests: the existing ones pass (re-pin B01's and B03's shape and golden tests where the tuning moves them, never loosening a bound
   to pass); new tests for the targets you can state as numbers over a sample (ocean connectivity, share above 150, no snow below a
   stated height at the equator of the temperature field, rivers ending in water), and for the headwaters: no river or lake water
   inside a region's margin, and every river near a region starting at a spring or pond whose channel floor meets the ground (no
   trench ending in a wall), for the valley and for a test region of another size and place.
3. Content: re-hash after the JSON edits as CLAUDE.md says, validate, and put the new bundle hash into
   tests/Vocaterra.Game.Core.Tests/Fixtures/save-v9.json (B05 renamed the fixture; the state digest there depends only on world.json,
   biomes.json and underground.json, so re-read it with a one-time digest read-out if your edit moved it).
4. **Pictures for the owner** (D-game-34 item 12): the same maps B02 drew (seeds 42, 7, 2026: biome and heights at 16,384 blocks and 16
   per pixel, the pair at 4,096 and 4 per pixel centred on the highest range, and slices across a range, a coast and a river), with the
   preview tool, in /home/james/Dev/Vocaterra/artifacts/b-series/b02b/ (outside git), and an `index.md` there that sets each new map
   beside B02's old one (by file name) and says in a line what changed, plus the before and after numbers (biome shares, share above
   150/200/250, ocean connectivity, coast slope). Add the valley maps B06 drew (`--world valley`, seed 42, heights and biomes at
   2,048 and 768 blocks round the valley) again, to show the rivers starting in the hills, each beside B06's file.

Gate (from the worktree root): scripts/limited.sh dotnet build Vocaterra.sln -warnaserror -m:4 (0 warnings); scripts/test.sh full
(11,879 passed before your change, plus your new tests) run after your last change; scripts/limited.sh dotnet format Vocaterra.sln
--verify-no-changes clean; scripts/limited.sh scripts/content.sh validate passes; `SceneTests.GoldenDigest` unchanged. Do not commit bin/,
obj/ or artifacts/.
Commit messages end with
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDbkPTqABsu7sLVD9h6

Report, one page: branch, commit SHA, worktree path; what changed (numbers and code) and why; the before and after figures; test counts;
the pins that moved; the pictures' folder; what still looks wrong, described plainly; files touched outside the milestone's area;
anything left undone and why. Leave out file dumps and long logs.
