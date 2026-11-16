using Vocaterra.Game.Core.World.Biomes;
using Vocaterra.Game.Core.World.Climate;
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
        var shape = _world.Height.Shape(x, z, climate);
        return _world.Table.Classify(climate, shape.Height, x, z, shape.Water, shape.RiverWeight).Row;
    }

    /// <inheritdoc />
    public IRegion? RegionAt(int x, int z) => _regions.RegionAt(x, z);

    /// <inheritdoc />
    public int NaturalSurfaceAt(int x, int z) => _world.Height.Shape(x, z, _world.Climate.At(x, z)).Height;

    /// <inheritdoc />
    public bool IsUnderground(int x, int y, int z) => y < NaturalSurfaceAt(x, z);
}
