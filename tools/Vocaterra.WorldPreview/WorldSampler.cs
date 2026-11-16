using Vocaterra.Game.Core.Content;
using Vocaterra.Game.Core.World;
using Vocaterra.Game.Core.World.Biomes;
using Vocaterra.Game.Core.World.Climate;
using Vocaterra.Game.Core.World.Generation.Procedural;

namespace Vocaterra.WorldPreview;

/// <summary>What the model says about one column: the ground, the water over it, the biome row and the lines of snow and trees.</summary>
/// <param name="Ground">The y of the topmost solid block (the bed under water).</param>
/// <param name="Water">Whether water covers the column.</param>
/// <param name="WaterLevel">The y of the water's top, or -1.</param>
/// <param name="Biome">The biome row's position in the file.</param>
/// <param name="Snow">Whether the ground is above the snow line (temperature at the ground below the snow threshold), on land.</param>
/// <param name="SnowLine">The lowest y at which snow lies in this column, or null when it is never cold enough.</param>
/// <param name="TreeLine">The lowest y above which no tree grows in this column, or null when it is never cold enough.</param>
/// <param name="RiverWeight">How much a river carved the column, 0 to 4 096 (0 in the valley world, which has no weights).</param>
/// <param name="Natural">The ground before the rivers and lakes carved it.</param>
internal readonly record struct Column(int Ground, bool Water, int WaterLevel, int Biome, bool Snow, int? SnowLine, int? TreeLine, int RiverWeight = 0, int Natural = 0);

/// <summary>The columns of a square area, one for each pixel.</summary>
internal sealed class WorldMap
{
    public WorldMap(int left, int top, int scale, int width, int height)
    {
        Left = left;
        Top = top;
        Scale = scale;
        Width = width;
        Height = height;
        Ground = new short[width * height];
        Water = new bool[width * height];
        Snow = new bool[width * height];
        Biome = new short[width * height];
        River = new short[width * height];
        Natural = new short[width * height];
        WaterLevel = new short[width * height];
    }

    /// <summary>The block x of the first pixel.</summary>
    public int Left { get; }

    /// <summary>The block z of the first pixel row.</summary>
    public int Top { get; }

    /// <summary>The blocks a pixel stands for, across and down.</summary>
    public int Scale { get; }

    public int Width { get; }

    public int Height { get; }

    public short[] Ground { get; }

    public bool[] Water { get; }

    public bool[] Snow { get; }

    public short[] Biome { get; }

    /// <summary>How much a river carved each column, 0 to 4 096.</summary>
    public short[] River { get; }

    /// <summary>The ground before the rivers and lakes carved it.</summary>
    public short[] Natural { get; }

    /// <summary>The y of the water's top block in each column, or -1.</summary>
    public short[] WaterLevel { get; }

    /// <summary>The block x of a pixel column.</summary>
    public int BlockX(int px) => Left + (px * Scale);

    /// <summary>The block z of a pixel row.</summary>
    public int BlockZ(int py) => Top + (py * Scale);
}

/// <summary>Reads the climate, the height model and the biome table of one seed, as the generator will, and samples them for the maps. It only reads.</summary>
internal sealed class WorldSampler
{
    private readonly WorldGenData _world;
    private readonly ClimateSampler _climate;
    private readonly HeightModel _height;
    private readonly BiomeTable _biomes;
    private readonly int _snowBelow;
    private readonly int _treeLineBelow;
    private readonly int _maxThreads;
    private readonly IPlaceInfo? _places;

    // The generation's fixed point (the core's IntMath.One, which is not public): 4 096 is the top of a field's range.
    private const int FixedOne = 4096;

    /// <summary>
    /// Creates the sampler of a seed over the bundle's data. With <paramref name="places"/> (a scene's <see cref="IPlaceInfo"/>, B06) the climate and the ground are the world's own round its
    /// regions: the continentalness pulled towards land, the valley's own ground inside it and the land eased into its edge outside; without it, the free procedural world.
    /// </summary>
    public WorldSampler(GameData data, ulong seed, int maxThreads = 4, IPlaceInfo? places = null)
    {
        ArgumentNullException.ThrowIfNull(data);
        _world = data.WorldGen;
        _climate = new ClimateSampler(_world, seed);
        _height = new HeightModel(_world, seed);
        _biomes = new BiomeTable(data.Biomes, _world, seed);
        _snowBelow = Fixed(_world.Altitude.SnowBelowTemperature);
        _treeLineBelow = Fixed(_world.Altitude.TreeLineBelowTemperature);
        _maxThreads = Math.Max(1, maxThreads);
        _places = places;
        Palette = new Palette(_biomes.Rows);
    }

    public Palette Palette { get; }

    public IReadOnlyList<BiomeRow> Rows => _biomes.Rows;

    public int SeaLevel => _world.Vertical.SeaLevel;

    /// <summary>The height from which the rivers fade out (their sources).</summary>
    public int RiverFadeFrom => _world.Height.Rivers.FadeFromHeight;

    public int MinY => _world.Vertical.MinY;

    public int MaxY => _world.Vertical.MaxY;

    /// <summary>The river field at a block (4 096 is the top of its range): a river runs where it is near zero.</summary>
    public int RiverField(int x, int z) => ClimateAt(x, z).River;

    /// <summary>The column (x, z), one climate sample at a time.</summary>
    public Column At(int x, int z) => Evaluate(x, z, ClimateAt(x, z));

    /// <summary>
    /// The map of a square area centred on (centreX, centreZ): one column every <paramref name="scale"/> blocks. At a scale of the climate's lattice or less the climate is filled
    /// in strips with <see cref="IClimateSampler.Fill"/> (the lattice is evaluated once for each strip); above it the area is sampled one column at a time, which costs less than
    /// filling every block between.
    /// </summary>
    public WorldMap Map(int centreX, int centreZ, int size, int scale)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(scale);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);
        if (size % scale != 0)
        {
            throw new ArgumentException("The size must be a whole number of pixels: a multiple of the scale.", nameof(size));
        }

        var pixels = size / scale;
        var map = new WorldMap(centreX - (size / 2), centreZ - (size / 2), scale, pixels, pixels);
        var fill = scale <= _world.Climate.LatticeBlocks && _places is null;
        const int stripRows = 16;
        var strips = (pixels + stripRows - 1) / stripRows;
        Parallel.For(0, strips, new ParallelOptions { MaxDegreeOfParallelism = _maxThreads }, strip =>
        {
            var py0 = strip * stripRows;
            var rows = Math.Min(stripRows, pixels - py0);
            ClimateSample[]? filled = null;
            if (fill)
            {
                filled = new ClimateSample[size * rows * scale];
                _climate.Fill(map.Left, map.BlockZ(py0), size, rows * scale, filled);
            }

            for (var r = 0; r < rows; r++)
            {
                var py = py0 + r;
                var z = map.BlockZ(py);
                for (var px = 0; px < pixels; px++)
                {
                    var x = map.BlockX(px);
                    var c = filled is null ? ClimateAt(x, z) : filled[(r * scale * size) + (px * scale)];
                    var column = Evaluate(x, z, c);
                    var i = (py * pixels) + px;
                    map.Ground[i] = (short)column.Ground;
                    map.Water[i] = column.Water;
                    map.Snow[i] = column.Snow;
                    map.Biome[i] = (short)column.Biome;
                    map.River[i] = (short)column.RiverWeight;
                    map.Natural[i] = (short)column.Natural;
                    map.WaterLevel[i] = (short)column.WaterLevel;
                }
            }
        });
        return map;
    }

    /// <summary>The share of the map's columns in each biome row, in file order (they add up to 1).</summary>
    public double[] Shares(WorldMap map)
    {
        var counts = new long[Rows.Count];
        foreach (var b in map.Biome)
        {
            counts[b]++;
        }

        return [.. counts.Select(c => (double)c / map.Biome.Length)];
    }

    private Column Evaluate(int x, int z, in ClimateSample c)
    {
        var shape = ShapeAt(x, z, c);
        var column = _biomes.Classify(c, shape.Height, x, z, shape.Water);
        var snow = column.Snow;
        return new Column(shape.Height, shape.Water, shape.WaterLevel, column.Row.Index, snow, LineAt(c.Temperature, _snowBelow), LineAt(c.Temperature, _treeLineBelow), shape.RiverWeight, shape.NaturalHeight);
    }

    private ClimateSample ClimateAt(int x, int z) => _places is null ? _climate.At(x, z) : _places.ClimateAt(x, z);

    // The ground of a column: the height model's, or the world's own round its regions (the natural surface; it is under water where it is below the sea).
    private ColumnShape ShapeAt(int x, int z, in ClimateSample c)
    {
        if (_places is null)
        {
            return _height.Shape(x, z, c);
        }

        var height = _places.NaturalSurfaceAt(x, z);
        var water = height < SeaLevel;
        return new ColumnShape(height, height, water, water ? SeaLevel : -1, 0, 0);
    }

    // The lowest y at which the temperature is below the threshold (the sea level when it already is there), or null when even the top of the world is warmer.
    private int? LineAt(int seaTemperature, int below)
    {
        int Cold(int y) => ClimateLapse.Apply(seaTemperature, y, SeaLevel, _world.Climate.LapsePer100Blocks);
        if (Cold(SeaLevel) < below)
        {
            return SeaLevel;
        }

        var top = MaxY + 1;
        if (Cold(top) >= below)
        {
            return null;
        }

        var lo = SeaLevel;
        var hi = top;
        while (hi - lo > 1)
        {
            var mid = (lo + hi) / 2;
            if (Cold(mid) < below)
            {
                hi = mid;
            }
            else
            {
                lo = mid;
            }
        }

        return hi;
    }

    // The data's thousandths of a field's range as the fixed point the generation computes in (the core's FieldScale, which is not public).
    private static int Fixed(int thousandths) => (int)((long)thousandths * FixedOne / 1000);
}
