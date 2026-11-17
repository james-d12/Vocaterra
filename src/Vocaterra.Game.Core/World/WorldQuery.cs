using System.Collections.Concurrent;
using Vocaterra.Game.Core.World.Biomes;
using Vocaterra.Game.Core.World.Climate;
using Vocaterra.Game.Core.World.Drainage;
using Vocaterra.Game.Core.World.Generation.Procedural;
using Vocaterra.Game.Core.World.Regions;

namespace Vocaterra.Game.Core.World;

/// <summary>
/// The place query of a streamed or bounded world (B06): the climate, the height model and the biome table of the world's generation, with its regions. It holds nothing that changes after
/// construction and builds everything a call needs inside the call, so any thread may call it at any time.
/// </summary>
public sealed class WorldQuery : IPlaceInfo
{
    private readonly GenerationWorld _world;
    private readonly RegionCatalog _regions;
    private readonly ConcurrentDictionary<long, byte> _warming = new();

    internal WorldQuery(GenerationWorld world, RegionCatalog regions)
    {
        _world = world;
        _regions = regions;
    }

    /// <inheritdoc />
    public ClimateSample ClimateAt(int x, int z) => _world.Climate.At(x, z);

    /// <inheritdoc />
    public int TemperatureAt(int x, int y, int z) => _world.Climate.TemperatureAt(x, y, z);

    /// <inheritdoc />
    public BiomeRow BiomeAt(int x, int z)
    {
        var climate = _world.Climate.At(x, z);
        var shape = ShapeAt(x, z, climate);
        return _world.Table.Classify(climate, shape.Height, x, z, shape.Water, shape.RiverWeight).Row;
    }

    /// <inheritdoc />
    public IRegion? RegionAt(int x, int z) => _regions.RegionAt(x, z);

    /// <inheritdoc />
    public int NaturalSurfaceAt(int x, int z) => ShapeAt(x, z, _world.Climate.At(x, z)).Height;

    /// <inheritdoc />
    public bool IsUnderground(int x, int y, int z) => y < NaturalSurfaceAt(x, z);

    // The column's shape, for a caller that may be the game loop (the sky's exposure and the residency read it every frame): where the drainage network has not read the landmass round the column yet the answer is the ground without rivers and lakes (the
    // loop never waits for a landmass: up to a second) and a pool thread reads it, once for each 4 096-block square; the next call has the water.
    private ColumnShape ShapeAt(int x, int z, ClimateSample climate)
    {
        if (_world.Ground.IsWarm(x, z))
        {
            return _world.Height.Shape(x, z, climate);
        }

        var key = ((long)(x >> 12) << 32) ^ (uint)(z >> 12);
        if (_warming.TryAdd(key, 0))
        {
            ThreadPool.QueueUserWorkItem(_ =>
            {
                _world.Ground.Warm(x, z);
                _warming.TryRemove(key, out byte _);
            });
        }

        using var dry = DrainageNetwork.WithoutReading();
        return _world.Height.Shape(x, z, climate);
    }
}
