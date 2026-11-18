using Vocaterra.Game.Core.World;
using Vocaterra.Game.Core.World.Biomes;
using Vocaterra.Game.Core.World.Climate;
using Vocaterra.Game.Core.World.Regions;

namespace Vocaterra.Game.Core.Tests.Temperature;

/// <summary>A place query whose climate temperature is a function the test gives: only <see cref="TemperatureAt"/> answers (the climate term asks nothing else).</summary>
internal sealed class StubPlaces(Func<int, int, int, int> temperature) : IPlaceInfo
{
    public int TemperatureAt(int x, int y, int z) => temperature(x, y, z);

    public ClimateSample ClimateAt(int x, int z) => throw new NotSupportedException();

    public BiomeRow BiomeAt(int x, int z) => throw new NotSupportedException();

    public IRegion? RegionAt(int x, int z) => throw new NotSupportedException();

    public int NaturalSurfaceAt(int x, int z) => throw new NotSupportedException();

    public bool IsUnderground(int x, int y, int z) => throw new NotSupportedException();
}
