using Vocaterra.Game.Core.Tests.World.Biomes;
using Vocaterra.Game.Core.World;
using Vocaterra.Game.Core.World.Biomes;
using Vocaterra.Game.Core.World.Climate;
using Vocaterra.Game.Core.World.Regions;

namespace Vocaterra.Game.Core.Tests.Temperature;

/// <summary>A world for the temperature's tests: the climate temperature by place, the natural surface, the biome's id and the snow line, each a function the test gives (a flat surface at y 40 in a biome with no row by default).</summary>
internal sealed class StubPlaces(Func<int, int, int, int> temperature, Func<int, int, int>? surface = null, Func<int, int, string>? biome = null, int snowBelow = int.MinValue) : IPlaceInfo
{
    public int SnowBelowTemperature => snowBelow;

    public int TemperatureAt(int x, int y, int z) => temperature(x, y, z);

    public int NaturalSurfaceAt(int x, int z) => surface?.Invoke(x, z) ?? 40;

    public BiomeRow BiomeAt(int x, int z) => BiomeTestData.Row(biome?.Invoke(x, z) ?? "stub");

    public bool IsUnderground(int x, int y, int z) => y < NaturalSurfaceAt(x, z);

    public ClimateSample ClimateAt(int x, int z) => throw new NotSupportedException();

    public IRegion? RegionAt(int x, int z) => throw new NotSupportedException();
}
