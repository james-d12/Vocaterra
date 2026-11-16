using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World.Climate;
using Vocaterra.Game.Core.World.Layout;
using Vocaterra.Game.Core.World.Noise;

namespace Vocaterra.Game.Core.World.Biomes;

/// <summary>The blocks a biome row names, resolved once (B01): what <see cref="BiomeTable.Surface"/> hands the surface passes.</summary>
/// <param name="Top">The top layer's block.</param>
/// <param name="Filler">The block under the top layer.</param>
/// <param name="Underwater">The top layer's block under water.</param>
/// <param name="FillerDepth">How many filler layers lie under the top one.</param>
public readonly record struct BiomeSurface(BlockId Top, BlockId Filler, BlockId Underwater, int FillerDepth);

/// <summary>What the table decides about a column: its row, and the surface temperature the row was chosen by with what follows from it.</summary>
/// <param name="Row">The biome row.</param>
/// <param name="SurfaceTemperature">The climate temperature at the surface (after the jitter and the lapse), fixed point.</param>
/// <param name="Snow">Whether snow covers the column: the row allows it and the surface temperature is below the snow threshold.</param>
/// <param name="TreesGrow">Whether trees may grow: the surface temperature is not below the tree line.</param>
public readonly record struct BiomeColumn(BiomeRow Row, int SurfaceTemperature, bool Snow, bool TreesGrow);

/// <summary>
/// The biome selector over the rows of <c>game/biomes.json</c> (B01): the rows in file order, the first whose conditions hold wins (the validator requires the last to hold for
/// every column). The temperature and the humidity of each column are jittered a little by a hash of the column before the rows are tried, so a border is dithered and not a
/// line; the surface temperature the snow and the tree line follow is the jittered temperature lowered by the lapse at the column's height, the same lapse
/// <see cref="IClimateSampler.TemperatureAt"/> uses. Pure and immutable.
/// </summary>
public sealed class BiomeTable : IBiomeSelector
{
    private readonly BiomeData _data;
    private readonly int _seaLevel;
    private readonly int _lapse;
    private readonly int _snowBelow;
    private readonly int _treeLineBelow;
    private readonly int _ditherTemperature;
    private readonly int _ditherHumidity;
    private readonly Compiled[] _rows;
    private readonly BiomeSurface[] _surfaces;
    private readonly TerrainNoise _dither;

    /// <summary>Creates the table of one world seed.</summary>
    /// <param name="data">The biomes.</param>
    /// <param name="world">The world's numbers (the sea level, the lapse and the snow and tree-line thresholds).</param>
    /// <param name="seed">The world seed (it keys the border jitter).</param>
    /// <exception cref="ArgumentException">A row names a block the game does not have.</exception>
    public BiomeTable(BiomeData data, WorldGenData world, ulong seed)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(world);
        _data = data;
        _seaLevel = world.Vertical.SeaLevel;
        _lapse = world.Climate.LapsePer100Blocks;
        _snowBelow = FieldScale.FromThousandths(world.Altitude.SnowBelowTemperature);
        _treeLineBelow = FieldScale.FromThousandths(world.Altitude.TreeLineBelowTemperature);
        _ditherTemperature = FieldScale.FromThousandths(data.DitherTemperature);
        _ditherHumidity = FieldScale.FromThousandths(data.DitherHumidity);
        _dither = new TerrainNoise(seed, "biome/dither");
        _rows = [.. data.Rows.Select(static r => new Compiled(r))];
        _surfaces = [.. data.Rows.Select(Resolve)];
    }

    /// <summary>The rows, in file order.</summary>
    public IReadOnlyList<BiomeRow> Rows => _data.Rows;

    /// <inheritdoc />
    public BiomeRow Select(in ClimateSample c, int height, int x, int z) => Classify(c, height, x, z).Row;

    /// <summary>The biome of a column with the surface temperature it was chosen by and what follows from it (the snow, the tree line).</summary>
    /// <param name="c">The column's climate.</param>
    /// <param name="height">The column's surface height in blocks.</param>
    /// <param name="x">The block's x.</param>
    /// <param name="z">The block's z.</param>
    /// <param name="water">Whether water covers the column (the height model's say, since a river or a lake has a level of its own); not given, the ground is below the sea.</param>
    public BiomeColumn Classify(in ClimateSample c, int height, int x, int z, bool? water = null)
    {
        var temperature = c.Temperature + Jitter(_dither.Column(0, x, z), _ditherTemperature);
        var humidity = c.Humidity + Jitter(_dither.Column(1, x, z), _ditherHumidity);
        var surfaceTemperature = ClimateLapse.Apply(temperature, height, _seaLevel, _lapse);
        var wet = water ?? height < _seaLevel;
        var row = _rows[^1].Row;
        foreach (var compiled in _rows)
        {
            if (compiled.Matches(wet, c, temperature, humidity, height, surfaceTemperature))
            {
                row = compiled.Row;
                break;
            }
        }

        return new BiomeColumn(row, surfaceTemperature, row.SnowCap && !wet && surfaceTemperature < _snowBelow, surfaceTemperature >= _treeLineBelow);
    }

    /// <summary>The blocks a row names, resolved.</summary>
    /// <param name="row">A row of this table's data.</param>
    public BiomeSurface Surface(BiomeRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return _surfaces[row.Index];
    }

    private static int Jitter(int hash, int amplitude) => (int)((long)(hash - 2048) * amplitude / 2048);

    private static BiomeSurface Resolve(BiomeRow row)
    {
        var where = $"biome '{row.Id}'";
        var top = SceneLayoutParser.BlockNamed(row.Top, where);
        return new BiomeSurface(top, SceneLayoutParser.BlockNamed(row.Filler, where), row.Underwater is null ? top : SceneLayoutParser.BlockNamed(row.Underwater, where), row.FillerDepth);
    }

    // A row's conditions with the thousandths turned into fixed point once.
    private sealed class Compiled
    {
        private readonly bool? _water;
        private readonly (int Min, int Max) _continentalness;
        private readonly (int Min, int Max) _erosion;
        private readonly (int Min, int Max) _ridges;
        private readonly (int Min, int Max) _temperature;
        private readonly (int Min, int Max) _humidity;
        private readonly (int Min, int Max) _river;
        private readonly IntRange _height;
        private readonly (int Min, int Max) _surfaceTemperature;

        public Compiled(BiomeRow row)
        {
            Row = row;
            var w = row.When;
            _water = w.Water;
            _continentalness = Scale(w.Continentalness);
            _erosion = Scale(w.Erosion);
            _ridges = Scale(w.Ridges);
            _temperature = Scale(w.Temperature);
            _humidity = Scale(w.Humidity);
            _river = Scale(w.River);
            _height = w.Height;
            _surfaceTemperature = Scale(w.SurfaceTemperature);
        }

        public BiomeRow Row { get; }

        public bool Matches(bool water, in ClimateSample c, int temperature, int humidity, int height, int surfaceTemperature) =>
            (_water is null || _water == water)
            && In(_continentalness, c.Continentalness)
            && In(_erosion, c.Erosion)
            && In(_ridges, c.Ridges)
            && In(_temperature, temperature)
            && In(_humidity, humidity)
            && In(_river, Math.Abs(c.River))
            && _height.Contains(height)
            && In(_surfaceTemperature, surfaceTemperature);

        private static bool In((int Min, int Max) range, int value) => value >= range.Min && value <= range.Max;

        private static (int Min, int Max) Scale(IntRange range) =>
            (range.Min == int.MinValue ? int.MinValue : FieldScale.FromThousandths(range.Min), range.Max == int.MaxValue ? int.MaxValue : FieldScale.FromThousandths(range.Max));
    }
}
