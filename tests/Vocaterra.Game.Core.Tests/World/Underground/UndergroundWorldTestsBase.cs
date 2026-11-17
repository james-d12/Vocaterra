using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.World.Underground;

namespace Vocaterra.Game.Core.Tests.World.Underground;

/// <summary>The shared parts of the test classes split out of UndergroundWorldTests (B28: xUnit runs the tests of one class one after another, so the slow ones are in classes of their own): the fields and helpers.</summary>
public abstract class UndergroundWorldTestsBase
{
    public static TheoryData<ulong, Feature> WaterPlaces => new()
    {
        { 42, Feature.River }, { 42, Feature.Lake }, { 42, Feature.Coast },
        { 7, Feature.River }, { 7, Feature.Coast },
        { 2026, Feature.River }, { 2026, Feature.Lake }, { 2026, Feature.Coast },
    };

    internal static UndergroundData Data => TestGame.Data.Underground!;

    internal static SampledArea Area(ulong seed, Feature feature, int span = 5)
    {
        var (cx, cz) = ProceduralTestWorld.Find(seed, feature);
        var source = ProceduralTestWorld.Source(seed);
        return new SampledArea(source, source.GenerateColumn, cx - (span / 2), cz - (span / 2), span, span);
    }
}
