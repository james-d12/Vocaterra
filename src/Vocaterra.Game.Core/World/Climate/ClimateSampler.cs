using Vocaterra.Game.Core.World.Noise;

namespace Vocaterra.Game.Core.World.Climate;

/// <summary>
/// The climate fields of <c>game/world.json</c> over gradient noise (B01): continentalness, erosion, ridges, temperature, humidity and the river field, each a fractal (the ridges
/// a ridged one, the continentalness, ridges and river warped) sampled on a lattice of a few blocks and interpolated, so a column costs a fraction of a noise evaluation and a
/// chunk or a map is filled in one pass. Lakes are the height model's (they depend on the land's height). Pure and immutable: the fields are built once from the seed.
/// </summary>
public sealed class ClimateSampler : IClimateSampler
{
    private readonly ClimateData _data;
    private readonly int _seaLevel;
    private readonly LatticeSampler2 _continentalness;
    private readonly LatticeSampler2 _erosion;
    private readonly LatticeSampler2 _ridges;
    private readonly LatticeSampler2 _temperature;
    private readonly LatticeSampler2 _humidity;
    private readonly LatticeSampler2 _river;

    /// <summary>Creates the sampler of one world seed.</summary>
    /// <param name="world">The world's numbers.</param>
    /// <param name="seed">The world seed.</param>
    public ClimateSampler(WorldGenData world, ulong seed)
    {
        ArgumentNullException.ThrowIfNull(world);
        _data = world.Climate;
        _seaLevel = world.Vertical.SeaLevel;
        var lattice = _data.LatticeBlocks;
        _continentalness = Field(seed, "continentalness", _data.Continentalness, lattice, false);
        _erosion = Field(seed, "erosion", _data.Erosion, lattice, false);
        _ridges = Field(seed, "ridges", _data.Ridges, lattice, true);
        _temperature = Field(seed, "temperature", _data.Temperature, lattice, false);
        _humidity = Field(seed, "humidity", _data.Humidity, lattice, false);
        _river = Field(seed, "river", _data.River, lattice, false);
    }

    /// <inheritdoc />
    public ClimateSample At(int x, int z) =>
        new(_temperature.At(x, z), _humidity.At(x, z), _continentalness.At(x, z), _erosion.At(x, z), _ridges.At(x, z), _river.At(x, z));

    /// <inheritdoc />
    public int TemperatureAt(int x, int y, int z) => ClimateLapse.Apply(_temperature.At(x, z), y, _seaLevel, _data.LapsePer100Blocks);

    /// <inheritdoc />
    public void Fill(int x0, int z0, int width, int depth, Span<ClimateSample> into)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(into.Length, width * depth);
        var n = width * depth;
        if (n <= 0)
        {
            return;
        }

        var t = new int[n];
        var h = new int[n];
        var c = new int[n];
        var e = new int[n];
        var r = new int[n];
        var v = new int[n];
        _temperature.Fill(x0, z0, width, depth, t);
        _humidity.Fill(x0, z0, width, depth, h);
        _continentalness.Fill(x0, z0, width, depth, c);
        _erosion.Fill(x0, z0, width, depth, e);
        _ridges.Fill(x0, z0, width, depth, r);
        _river.Fill(x0, z0, width, depth, v);
        for (var i = 0; i < n; i++)
        {
            into[i] = new ClimateSample(t[i], h[i], c[i], e[i], r[i], v[i]);
        }
    }

    private static LatticeSampler2 Field(ulong seed, string name, NoiseSpec spec, int lattice, bool ridged)
    {
        var stack = new NoiseStack(seed, "climate/" + name);
        return new LatticeSampler2(lattice, (x, z) =>
        {
            var (wx, wz) = stack.Warp(x, z, spec.WarpCell, spec.WarpBlocks);
            return ridged ? stack.Ridged(wx, wz, spec.Cell, spec.Octaves) : stack.Fbm(wx, wz, spec.Cell, spec.Octaves);
        });
    }
}
