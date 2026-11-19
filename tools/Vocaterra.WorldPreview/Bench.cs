using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Vocaterra.Game.Core.Content;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World.Far;
using Vocaterra.Game.Core.World.Generation.Procedural;
using Vocaterra.Game.Core.World.Lod;
using Vocaterra.Game.Core.World.Regions;

namespace Vocaterra.WorldPreview;

/// <summary>
/// The generation bench (B36): how long the world's generation takes, measured the same way every time so a speed milestone can be compared with the one before. Seed 42 over the shipped world files, one thread, the fastest of N passes
/// of each row. A tool and not a test: it asserts nothing and is not in the suite. The rows are those of docs/research/2026-11-18-chunk-generation-speed.md section 1 that Game.Core can produce (the tile mesher and the ring's ingest
/// are in Game, so the ring row is the bands and the generation of the footprint's four chunk columns).
/// </summary>
internal static class Bench
{
    private const string Usage = """
        usage: WorldPreview bench [--seed <n>] [--passes <n>] [--columns <n>] [--json <file>] [--quick] [--content <dir>]
          --seed <n>      the world seed (default 42)
          --passes <n>    each row is the fastest of this many passes (default 3; 2 with --quick)
          --columns <n>   land chunk columns in the chunk rows (default 24; 6 with --quick)
          --json <file>   where the JSON goes (default bench-seed<seed>.json in the current folder)
          --quick         a short run (one land and one sea position for the far stacks, 6 columns, 2 passes): under 30 s
        """;

    private const int Footprint = 2; // the ring's footprint: 32 x 32 blocks, 2 x 2 chunk columns

    public static int Run(string[] args, TextWriter output, TextWriter error)
    {
        ulong seed = 42;
        var passes = -1;
        var columns = -1;
        var quick = false;
        string? json = null;
        string? content = null;
        try
        {
            for (var i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--quick":
                        quick = true;
                        break;
                    case "--seed" when i + 1 < args.Length:
                        seed = ulong.Parse(args[++i], CultureInfo.InvariantCulture);
                        break;
                    case "--passes" when i + 1 < args.Length:
                        passes = int.Parse(args[++i], CultureInfo.InvariantCulture);
                        break;
                    case "--columns" when i + 1 < args.Length:
                        columns = int.Parse(args[++i], CultureInfo.InvariantCulture);
                        break;
                    case "--json" when i + 1 < args.Length:
                        json = args[++i];
                        break;
                    case "--content" when i + 1 < args.Length:
                        content = args[++i];
                        break;
                    default:
                        error.WriteLine($"Unknown option or missing value: '{args[i]}'.");
                        error.WriteLine(Usage);
                        return 2;
                }
            }

            passes = passes > 0 ? passes : quick ? 2 : 3;
            columns = columns > 0 ? columns : quick ? 6 : 24;
            var data = Cli.Load(content ?? Cli.FindBundle());
            var bench = new Rows(data, seed, passes, columns, quick, output);
            bench.Execute();
            var path = json ?? $"bench-seed{seed}.json";
            File.WriteAllText(path, bench.ToJson(seed, passes, columns, quick));
            output.WriteLine($"json: {path}");
            return 0;
        }
        catch (Exception e) when (e is FormatException or OverflowException or ArgumentException or IOException)
        {
            error.WriteLine(e.Message);
            return 2;
        }
    }

    private sealed record Row(string Name, double Ms, string Unit, int Count);

    private sealed class Rows
    {
        private readonly ProceduralChunkSource _source;
        private readonly GenerationWorld _world;
        private readonly FarTerrainSampler _far;
        private readonly int _passes;
        private readonly int _columnCount;
        private readonly bool _quick;
        private readonly TextWriter _out;
        private readonly List<Row> _rows = [];

        public Rows(GameData data, ulong seed, int passes, int columns, bool quick, TextWriter output)
        {
            _source = new ProceduralChunkSource(data.WorldGen, data.Biomes, seed, null, data.Underground);
            _world = _source.World;
            _far = new FarTerrainSampler(_world, RegionCatalog.Empty);
            _passes = passes;
            _columnCount = columns;
            _quick = quick;
            _out = output;
        }

        public void Execute()
        {
            var land = LandColumns(_columnCount);
            var sea = SeaColumn();
            _out.WriteLine($"generation bench: {land.Count} land chunk columns, {_passes} pass(es), single thread, Release={!IsDebug()}");
            Warm(land);
            ChunkRows(land);
            StageRows(land);
            FarRows(land, sea);
            Print();
        }

        private static bool IsDebug()
        {
#if DEBUG
            return true;
#else
            return false;
#endif
        }

        // ---- positions

        private ColumnShape ShapeAt(int x, int z) => _world.Height.Shape(x, z, _world.Climate.At(x, z));

        // Land chunk columns, spread out: a spiral of chunk columns seven apart from the origin, dry ground over the sea and under the snow.
        private List<(int Cx, int Cz)> LandColumns(int count)
        {
            var found = new List<(int, int)>();
            for (var ring = 0; ring < 60 && found.Count < count; ring++)
            {
                for (var gz = -ring; gz <= ring && found.Count < count; gz++)
                {
                    for (var gx = -ring; gx <= ring && found.Count < count; gx++)
                    {
                        if (Math.Max(Math.Abs(gx), Math.Abs(gz)) != ring)
                        {
                            continue;
                        }

                        var (cx, cz) = (gx * 7, gz * 7);
                        var shape = ShapeAt((cx << 4) + 8, (cz << 4) + 8);
                        if (!shape.Water && shape.Height > _world.SeaLevel + 2 && shape.Height < 150)
                        {
                            found.Add((cx, cz));
                        }
                    }
                }
            }

            return found;
        }

        private (int Cx, int Cz) SeaColumn()
        {
            for (var ring = 0; ring < 200; ring++)
            {
                for (var gz = -ring; gz <= ring; gz++)
                {
                    for (var gx = -ring; gx <= ring; gx++)
                    {
                        if (Math.Max(Math.Abs(gx), Math.Abs(gz)) != ring)
                        {
                            continue;
                        }

                        var (cx, cz) = (gx * 7, gz * 7);
                        var shape = ShapeAt((cx << 4) + 8, (cz << 4) + 8);
                        if (shape.Water && shape.WaterLevel == _world.SeaLevel && shape.Height < _world.SeaLevel - 8)
                        {
                            return (cx, cz);
                        }
                    }
                }
            }

            throw new InvalidOperationException("no open sea found");
        }

        // ---- timing

        private static double Ms(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;

        // The fastest of the passes of a piece of work, in ms.
        private double Fastest(Action work)
        {
            var best = double.MaxValue;
            for (var p = 0; p < _passes; p++)
            {
                var t = Stopwatch.GetTimestamp();
                work();
                best = Math.Min(best, Ms(Stopwatch.GetTimestamp() - t));
            }

            return best;
        }

        private void Add(string name, double ms, string unit, int count) => _rows.Add(new Row(name, ms, unit, count));

        private void Warm(List<(int Cx, int Cz)> land)
        {
            foreach (var (cx, cz) in land.Take(2))
            {
                _ = _source.GenerateColumn(cx, cz);
            }

            _ = _far.At(land[0].Cx << 4, land[0].Cz << 4);
        }

        // ---- the chunk rows

        private void ChunkRows(List<(int Cx, int Cz)> land)
        {
            var n = land.Count;
            var top = _source.MaxChunkY - 1;
            var deepLayer = Math.Max(_source.MinChunkY, -4);
            var surfaceLayers = land.Select(c => ShapeAt((c.Cx << 4) + 8, (c.Cz << 4) + 8).Height >> VoxelChunk.Shift).ToArray();
            Add("surface chunk, Generate(coord)", Fastest(() =>
            {
                for (var i = 0; i < n; i++)
                {
                    _ = _source.Generate(new VoxelChunkCoord(land[i].Cx, surfaceLayers[i], land[i].Cz));
                }
            }) / n, "ms per chunk", n);
            Add($"deep chunk (layer {deepLayer})", Fastest(() =>
            {
                foreach (var (cx, cz) in land)
                {
                    _ = _source.Generate(new VoxelChunkCoord(cx, deepLayer, cz));
                }
            }) / n, "ms per chunk", n);
            Add($"air chunk (layer {top})", Fastest(() =>
            {
                foreach (var (cx, cz) in land)
                {
                    _ = _source.Generate(new VoxelChunkCoord(cx, top, cz));
                }
            }) / n, "ms per chunk", n);
            Add("whole column, GenerateColumn", Fastest(() =>
            {
                foreach (var (cx, cz) in land)
                {
                    _ = _source.GenerateColumn(cx, cz);
                }
            }) / n, "ms per column", n);

            // The residency's column: its band, then the layers of the band and one each side.
            var bandMs = 0.0;
            var layerMs = 0.0;
            var best = double.MaxValue;
            for (var p = 0; p < _passes; p++)
            {
                double b = 0, l = 0;
                foreach (var (cx, cz) in land)
                {
                    var (band, generated) = Residency(cx, cz);
                    b += band;
                    l += generated;
                }

                if (b + l < best)
                {
                    best = b + l;
                    (bandMs, layerMs) = (b, l);
                }
            }

            Add("residency column: SurfaceBand", bandMs / n, "ms per column", n);
            Add("residency column: GenerateLayers (band +-1)", layerMs / n, "ms per column", n);
            Add("residency column, total", (bandMs + layerMs) / n, "ms per column", n);

            // The ring's footprint: 2 x 2 chunk columns, each one's band and then its layers.
            var footprints = land.Select(c => (c.Cx & ~1, c.Cz & ~1)).Distinct().ToList();
            double bands = 0, generate = 0, ringBest = double.MaxValue;
            for (var p = 0; p < _passes; p++)
            {
                double b = 0, l = 0;
                foreach (var (fx, fz) in footprints)
                {
                    for (var dz = 0; dz < Footprint; dz++)
                    {
                        for (var dx = 0; dx < Footprint; dx++)
                        {
                            var (band, generated) = Residency(fx + dx, fz + dz);
                            b += band;
                            l += generated;
                        }
                    }
                }

                if (b + l < ringBest)
                {
                    ringBest = b + l;
                    (bands, generate) = (b, l);
                }
            }

            var f = footprints.Count;
            Add("ring footprint 32 x 32: bands", bands / f, "ms per footprint", f);
            Add("ring footprint 32 x 32: generate", generate / f, "ms per footprint", f);
            Add("ring footprint 32 x 32, total (no ingest)", (bands + generate) / f, "ms per footprint", f);
        }

        private (double Band, double Layers) Residency(int cx, int cz)
        {
            var t0 = Stopwatch.GetTimestamp();
            var band = _source.SurfaceBand(cx, cz);
            var t1 = Stopwatch.GetTimestamp();
            if (!band.IsEmpty)
            {
                _ = _source.GenerateLayers(cx, cz, band.MinLayer - 1, band.MaxLayer + 1);
            }

            return (Ms(t1 - t0), Ms(Stopwatch.GetTimestamp() - t1));
        }

        // ---- the stages of a column

        private void StageRows(List<(int Cx, int Cz)> land)
        {
            var n = land.Count;
            var best = new Dictionary<string, double>(StringComparer.Ordinal);
            var order = new List<string>();
            for (var p = 0; p < _passes; p++)
            {
                var stages = new Dictionary<string, double>(StringComparer.Ordinal);
                foreach (var (cx, cz) in land)
                {
                    Stages(cx, cz, stages, p == 0 ? order : null);
                }

                foreach (var (name, ms) in stages)
                {
                    best[name] = best.TryGetValue(name, out var old) ? Math.Min(old, ms) : ms;
                }
            }

            foreach (var name in order)
            {
                Add("stage: " + name, best[name] / n, "ms per column", n);
            }
        }

        private static void Tick(Dictionary<string, double> into, List<string>? order, string name, long from)
        {
            if (!into.ContainsKey(name))
            {
                order?.Add(name);
                into[name] = 0;
            }

            into[name] += Ms(Stopwatch.GetTimestamp() - from);
        }

        private void Stages(int cx, int cz, Dictionary<string, double> into, List<string>? order)
        {
            var world = _world;
            var size = VoxelChunk.Size + (2 * world.Margin);
            var (ox, oz) = ((cx << VoxelChunk.Shift) - world.Margin, (cz << VoxelChunk.Shift) - world.Margin);
            var climate = new Vocaterra.Game.Core.World.Climate.ClimateSample[size * size];
            var t = Stopwatch.GetTimestamp();
            world.Climate.Fill(ox, oz, size, size, climate);
            Tick(into, order, "window: climate Fill", t);
            t = Stopwatch.GetTimestamp();
            for (var j = 0; j < size; j++)
            {
                for (var i = 0; i < size; i++)
                {
                    _ = world.Height.Shape(ox + i, oz + j, climate[(j * size) + i]);
                }
            }

            Tick(into, order, "window: height model Shape (with drainage)", t);

            var ctx = new ColumnContext(world, cx, cz);
            t = Stopwatch.GetTimestamp();
            foreach (var shaper in _source.Pipeline.Shapers)
            {
                shaper.Shape(ctx);
            }

            Tick(into, order, "shapers", t);
            t = Stopwatch.GetTimestamp();
            _ = ctx.Trees;
            _ = ctx.CavePlan;
            _ = ctx.Ramps;
            Tick(into, order, "plans (trees, caves, entrances)", t);

            var band = _source.SurfaceBand(cx, cz);
            var highest = Math.Max(ctx.HighestInWindow(), world.SeaLevel) + world.Strategies.MaxTreeHeight;
            for (var layer = _source.MinChunkY; layer < _source.MaxChunkY; layer++)
            {
                if ((layer << VoxelChunk.Shift) > highest)
                {
                    continue; // an air layer: the generator makes no chunk of it (see the air chunk row)
                }

                var kind = !band.IsEmpty && band.Contains(layer) ? "band" : "deep";
                var coord = new VoxelChunkCoord(cx, layer, cz);
                var builder = new ChunkBuilder(coord);
                foreach (var decorator in _source.Pipeline.Decorators)
                {
                    t = Stopwatch.GetTimestamp();
                    decorator.Decorate(ctx, builder);
                    Tick(into, order, $"{kind} layers: {decorator.PassName}", t);
                }

                t = Stopwatch.GetTimestamp();
                _ = builder.ToData();
                Tick(into, order, $"{kind} layers: ToData", t);
            }
        }

        // ---- the far view

        private void FarRows(List<(int Cx, int Cz)> land, (int Cx, int Cz) sea)
        {
            // Per column: the sampler asked one column at a time, then a grid filled (the far tile's way; the same numbers).
            var spots = FarLand(land);
            var samples = 0;
            var one = Fastest(() =>
            {
                samples = 0;
                foreach (var (cx, cz) in spots)
                {
                    for (var z = 0; z < 16; z++)
                    {
                        for (var x = 0; x < 16; x++)
                        {
                            _ = _far.At((cx << 4) + x, (cz << 4) + z);
                            samples++;
                        }
                    }
                }
            });
            Add("FarTerrainSampler.At, one column", one * 1000 / samples, "us per column", samples);
            var grid = new FarSample[32 * 32];
            var filled = Fastest(() =>
            {
                foreach (var (cx, cz) in spots)
                {
                    _far.Fill(cx << 4, cz << 4, 1, 32, 32, grid);
                }
            });
            Add("FarTerrainSampler.Fill 32 x 32, step 1", filled * 1000 / (spots.Count * 1024), "us per column", spots.Count * 1024);

            var sections = new LodModelSections(_far.Lod(4), _world.MinY, _world.MaxY);
            for (var level = 0; level <= 7; level++)
            {
                var (syMin, syMax) = LodWorld.SyRange(level, _world.MinY, _world.MaxY);
                int Side(int chunk) => (chunk << VoxelChunk.Shift) >> (LodSection.Shift + level);
                var landMs = Fastest(() =>
                {
                    foreach (var (cx, cz) in spots)
                    {
                        _ = sections.Stack(level, Side(cx), Side(cz), syMin, syMax);
                    }
                }) / spots.Count;
                Add($"far model stack L{level}, land", landMs, "ms per stack", spots.Count);
                var seaMs = Fastest(() => _ = sections.Stack(level, Side(sea.Cx), Side(sea.Cz), syMin, syMax));
                Add($"far model stack L{level}, sea", seaMs, "ms per stack", 1);
            }

            // Where level 3's time goes (B37): the same stack with its forests as layers (no crowns), and the tree pass alone over the stack's footprint with the crowns' blocks.
            {
                var (syMin, syMax) = LodWorld.SyRange(3, _world.MinY, _world.MaxY);
                int Side3(int chunk) => (chunk << VoxelChunk.Shift) >> (LodSection.Shift + 3);
                var layered = new LodModelSections(_far.Lod(3), _world.MinY, _world.MaxY);
                var noCrowns = Fastest(() =>
                {
                    foreach (var (cx, cz) in spots)
                    {
                        _ = layered.Stack(3, Side3(cx), Side3(cz), syMin, syMax);
                    }
                }) / spots.Count;
                Add("far model stack L3, land, forests as layers (no crowns)", noCrowns, "ms per stack", spots.Count);
                var side = 1 << (LodSection.Shift + 3);
                var plant = Fastest(() =>
                {
                    foreach (var (cx, cz) in spots)
                    {
                        var scope = _far.NewScope(side * side, false);
                        _ = _far.PlantTrees(Side3(cx) * side, Side3(cz) * side, side, side, scope);
                    }
                }) / spots.Count;
                Add("far tree plan over an L3 stack's footprint", plant, "ms per stack", spots.Count);

                // The coarse levels with one column at the cell's centre and no merge of four (MergeLevels 3).
                var single = new LodModelSections(_far.Lod(4, 3), _world.MinY, _world.MaxY);
                for (var level = 4; level <= 7; level++)
                {
                    var (lMin, lMax) = LodWorld.SyRange(level, _world.MinY, _world.MaxY);
                    var at = level;
                    int SideL(int chunk) => (chunk << VoxelChunk.Shift) >> (LodSection.Shift + at);
                    var ms = Fastest(() =>
                    {
                        foreach (var (cx, cz) in spots)
                        {
                            _ = single.Stack(at, SideL(cx), SideL(cz), lMin, lMax);
                        }
                    }) / spots.Count;
                    Add($"far model stack L{level}, land, one column a cell (MergeLevels 3)", ms, "ms per stack", spots.Count);
                }
            }
        }

        private List<(int Cx, int Cz)> FarLand(List<(int Cx, int Cz)> land)
        {
            var take = _quick ? 1 : 4;
            var spread = new List<(int, int)>();
            for (var i = 0; i < take; i++)
            {
                spread.Add(land[Math.Min(land.Count - 1, i * Math.Max(1, land.Count / take))]);
            }

            return spread;
        }

        // ---- the report

        private void Print()
        {
            var width = _rows.Max(static r => r.Name.Length);
            foreach (var row in _rows)
            {
                _out.WriteLine($"{row.Name.PadRight(width)}  {row.Ms.ToString("0.000", CultureInfo.InvariantCulture),10}  {row.Unit} (n={row.Count})");
            }
        }

        public string ToJson(ulong seed, int passes, int columns, bool quick)
        {
            var rows = _rows.Select(static r => new { name = r.Name, value = Math.Round(r.Ms, 4), unit = r.Unit, n = r.Count });
            return JsonSerializer.Serialize(new { seed, passes, columns, quick, release = !IsDebug(), rows }, new JsonSerializerOptions { WriteIndented = true });
        }
    }
}
