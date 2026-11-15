You are implementing B01, "Noise primitives, climate with altitude, the tall height model and the biome table", for the Vocaterra
repository (C#, .NET 10). The language core is the engine Vocaterra.Nlp, the Spanish pack Vocaterra.Nlp.Es and the Vocaterra.Nlp.* test
and tool projects; the game is Vocaterra.Game.Core (the engine-free logic), Vocaterra.Game (MonoGame), Vocaterra.Host and
tools/Vocaterra.SpeechRender. This milestone's work is in the game, in Vocaterra.Game.Core, its tests and content/game-v1.
Where to work: the worktree /home/james/Dev/Vocaterra/.claude/worktrees/b01, already on the local branch `B01-noise-climate-biomes` (cut
from `feature/biomes` at @@BASE@@). Run every command from that directory. Commit after each item that builds and passes its tests, with
the prefix `B01: `. Do not push your branch: it stays local and the gate merges it from there (owner, 2026-11-05). Never push or merge
feature/biomes or main. When you finish, leave the worktree on your branch, clean.
Resources (the machine has crashed from overload): run every dotnet restore, build, test, format and run, every scripts/content.sh call,
speech prerender and screenshot run through scripts/limited.sh (for example `scripts/limited.sh dotnet build Vocaterra.sln -warnaserror
-m:4`; tests with `-m:2`). scripts/test.sh and scripts/content-loop.sh call it themselves: run them directly. Never start two heavy
commands at once (no `&`, no background builds, no parallel test runs), and write no shell wait loop on `pgrep -f` (it matches its own
command line and never ends). Another agent (B04a) builds in its own worktree at the same time; limited.sh queues you both. While
iterating run `scripts/test.sh fast` or `scripts/test.sh project tests/Vocaterra.Game.Core.Tests`; run `scripts/test.sh full` (the whole
suite, as the gate) once before reporting. Exit 137 means the 6 GB cap killed it: lower the parallelism, do not work around the cap.
Environment: prefix every command inline with PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1 (the sandbox may
reject a bare `export`, and it refuses a command line containing the shell builtin words `eval` or `hash`: for `scripts/content.sh hash`
use a wrapper script outside the repository, for example in /tmp). Model-gated tests need
VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models.

Read first: CLAUDE.md; docs/game/implementation-plan.md sections 1, 3, 4.14 (your row) and 6; docs/game/biomes-plan.md sections 2, 3.1 to
3.4 (the design you build; 3.2's table and seams and 3.4 in full) and 4 (your row); docs/process/code-guidelines.md;
docs/game/architecture.md (the Core folders); D-game-34 in docs/game/decisions.md. The model to follow for "a kind is a row of data" is
the weather series' E04: src/Vocaterra.Game.Core/Atmosphere/WeatherData.cs and WeatherValidator.cs, content/game-v1/game/weather.json,
content/schema/weather.schema.json, how src/Vocaterra.Game.Core/Content/GameData.cs loads it, tests/Vocaterra.Game.Core.Tests/Atmosphere/
WeatherDataTests.cs and tests/Vocaterra.Game.Core.Tests/Architecture/NoWeatherKindIdsInSourceTests.cs. Existing primitives:
src/Vocaterra.Game.Core/World/Generation/TerrainNoise.cs, src/Vocaterra.Game.Core/Mathematics/IntMath.cs (fixed point, One = 4096) and
WorldRng.cs. Block names in data: reuse `SceneLayoutParser.BlockNamed` (src/Vocaterra.Game.Core/World/Layout/SceneLayoutParser.cs), do
not add a second name table. Read the sections named, never whole long files (SceneBuilder.cs and TerrainGenerator.cs are long: grep).

Rules: a game milestone edits only the game projects, their tests, content/game-v1, content/schema, assets/ and docs/game/ and takes the
core as it is (a missing core call stops that part: report it, do not copy core source or edit a Vocaterra.Nlp project or docs/nlp/). A
regression check is a C# test, never a script with text baselines. Never create or edit anything under .github/workflows/; do not edit
either implementation-plan.md, either decisions.md or docs/game/biomes-plan.md (the orchestrator does); engine code only in
src/Vocaterra.Game (D-game-1); no System.Random, no clock and no floating point in generation; all generation maths in fixed point.
Tests are Given_When_Then in folders that mirror the source.
Do not overlap B04a, which runs at the same time: do not edit src/Vocaterra.Game.Core/Voxel/, src/Vocaterra.Game.Core/Save/,
src/Vocaterra.Game.Core/Npc/WorldFacts.cs or anything in src/Vocaterra.Game. B01 generates no chunks and writes no blocks: no
`GenerationPipeline`, no `ProceduralChunkSource` (B03), no preview tool (B02), no wiring into the game (B06, B07).

Deliverables (all in Vocaterra.Game.Core unless named; the folder names and seams are the plan's section 3.2):
1. `World/Noise/`: move `TerrainNoise` here with its algorithm and pass names unchanged (the valley must stay byte-identical:
   `SceneTests.GoldenDigest` does not move); integer `GradientNoise` in 2D and 3D with a quintic fade; `NoiseStack` (`Fbm`, `Ridged`,
   `Warp`); `Spline` whose control points come from data; `LatticeSampler3` (a coarse 3D lattice, interpolated, for B11's caves) and the
   2D lattice the climate is sampled on (4 blocks, interpolated). Tests: determinism, negative coordinates, value ranges, continuity
   (bounded change between neighbouring samples), coordinates near plus and minus 29,000.
2. `World/Climate/`: `ClimateSample`, `IClimateSampler`, `ClimateSampler` with the fields of section 3.4's table (continentalness,
   erosion, ridges, temperature, humidity, river; lakes either here or in the height model, your choice, recorded in the report), and
   `TemperatureAt(x, y, z)`: the climate temperature falling with altitude by a data lapse rate (section 3.3: the future temperature
   system and the snow line read the same value).
3. `World/Generation/Procedural/`: `IHeightModel`, `ColumnShape` and `HeightModel`: section 3.4's three data splines (base from C, range
   factor from E, peaks from R), height = base + factor x peaks + detail, the soft cap above 280 at half rate, the clamp at 310, terraces
   on the steep middle slopes and crags only on the highest ridges; rivers carved where |V| is small to a bed below the sea level 32 and
   faded out above about 72 and towards the sea; lakes pulled towards 29 in low calm basins. `ColumnShape` carries at least the surface
   height and whether water covers the column (every column below 32 fills to 32), so B02's maps and B03's passes read it. If the height
   model needs `Terrace` (TerrainGenerator.cs, near line 195), move it to a shared helper unchanged; the valley golden must not move.
4. `World/Biomes/`: `BiomeRow`, `BiomeData` (loaded from `game/biomes.json`), `BiomeTable : IBiomeSelector` (ordered rows, the first
   match wins, a data row and never an enum), border dithering by a small jitter of temperature and humidity, and `BiomeValidator`
   (ranges sane, a final catch-all row required, block and tree-species names known). The rows of section 3.4 ("Biomes"): ocean, beach,
   river; jagged peaks, stony peaks, snowy slopes, grove, meadow; then taiga, plains, forest, birch forest, dense forest by temperature and
   humidity; all from existing blocks. Snow and the tree line follow `TemperatureAt` against data thresholds. Tree species and crown
   shapes stay code strategies named by the data (only their names in data).
5. `game/world.json` (the vertical range y -64 to 320 and the sea level 32, the climate fields' scales, the splines, the lapse rate and
   thresholds) and `game/biomes.json`, with JSON Schemas in content/schema, their loaders and validators, loaded once into `GameData`
   (properties such as `WorldGen` and `Biomes`) and checked where the other game data is validated. A comment in each JSON file says what
   it is for, as the other game files do.
6. Tests: `NoBiomeIdsInSourceTests`, mirroring `NoWeatherKindIdsInSourceTests` (no biome id from the shipped data as a string literal in
   any file under `src/`). Watch for clashes: the valley's pass names (`terrain`, `forest`, `trees`, `treeshape`, `undergrowth`) are
   string literals in src, so name the biomes so they do not collide (for example `woodland` rather than `forest`), never allow-list a
   clash away. A biome defined only in test data must select and validate with no code change. Purity tests: a reflection test that the
   seam implementations hold only readonly fields and no static mutable state; a token ban that keeps `Thread`, `Task`, `lock`,
   `Interlocked`, `System.Random`, `DateTime`, `float` and `double` out of `World/Noise`, `World/Climate`, `World/Biomes` and
   `World/Generation/Procedural`.
7. Content: after the JSON edits run `scripts/content.sh hash content/game-v1 --bundle-id game-v1` from a wrapper script, then
   `scripts/limited.sh scripts/content.sh validate`, put the new bundle hash into tests/Vocaterra.Game.Core.Tests/Fixtures/save-v8.json,
   and re-record the end-to-end goldens only if the edit moved them (CLAUDE.md, "Game content bundle"). world.json and biomes.json hold
   no Spanish.

Measure and report: the time to sample climate, height and biome for a 256 x 256 area, and for one 16 x 16 chunk column.

Gate (from the worktree root): scripts/limited.sh dotnet build Vocaterra.sln -warnaserror -m:4 (0 warnings); scripts/test.sh full
(@@COUNT@@ passed before your change, plus your new tests); scripts/limited.sh dotnet format Vocaterra.sln --verify-no-changes clean;
scripts/limited.sh scripts/content.sh validate passes; `SceneTests.GoldenDigest` unchanged. Do not commit bin/, obj/ or artifacts/.
Commit messages end with
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDbkPTqABsu7sLVD9h6

Report, one page: branch, commit SHA, worktree path; what was built (the types and the data files); test counts; the measurements;
bugs found and fixed (where, symptom, fix, test); design choices the plan left open (lakes, ColumnShape's fields, the spline form, the
biome ids); known-not-fixed with reasoning; files touched outside the milestone's area; anything left undone and why. Leave out file
dumps and long logs.
