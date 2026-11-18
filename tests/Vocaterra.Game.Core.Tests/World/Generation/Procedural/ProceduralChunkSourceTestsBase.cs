using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.Tests.World.Generation.Procedural;

/// <summary>The shared parts of the test classes split out of ProceduralChunkSourceTests (B28: xUnit runs the tests of one class one after another, so the slow ones are in classes of their own): the fields and helpers.</summary>
public abstract class ProceduralChunkSourceTestsBase
{
    public static TheoryData<ulong, Feature> Places => new()
    {
        { 42, Feature.Forest },
        { 7, Feature.Coast },
        { 2026, Feature.River },
        { 42, Feature.Mountain },
    };

    internal static List<VoxelChunkCoord> Neighbourhood(int cx, int cz, int firstLayer, int lastLayer)
    {
        var coords = new List<VoxelChunkCoord>();
        for (var z = cz - 1; z <= cz + 1; z++)
        {
            for (var x = cx - 1; x <= cx + 1; x++)
            {
                for (var y = firstLayer; y <= lastLayer; y++)
                {
                    coords.Add(new VoxelChunkCoord(x, y, z));
                }
            }
        }

        return coords;
    }

    // A fixed shuffle: the order is a function of the seed, so a failure is the same failure on the next run.
    internal static List<VoxelChunkCoord> Shuffled(List<VoxelChunkCoord> coords, ulong seed)
    {
        var rng = new Vocaterra.Game.Core.Mathematics.WorldRng(seed);
        var list = new List<VoxelChunkCoord>(coords);
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = rng.NextInt(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }

        return list;
    }
}
