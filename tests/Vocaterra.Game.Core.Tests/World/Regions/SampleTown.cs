using System.Collections.Concurrent;
using Vocaterra.Game.Core.Content;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World.Climate;
using Vocaterra.Game.Core.World.Generation;
using Vocaterra.Game.Core.World.Generation.Procedural;
using Vocaterra.Game.Core.World.Layout;
using Vocaterra.Game.Core.World.Regions;

namespace Vocaterra.Game.Core.Tests.World.Regions;

/// <summary>Shared set-up of the town tests (B15): the sample town's layout (test data, no words) and its region on the procedural ground of seed 42 at two places away from the valley.</summary>
internal static class SampleTown
{
    /// <summary>The seed of the town tests.</summary>
    public const ulong Seed = 42;

    /// <summary>The chunk column and row of the first town (the block 1264, 96): rolling land east of the valley, 880 blocks from its edge.</summary>
    public static readonly (int ChunkX, int ChunkZ) First = (79, 6);

    /// <summary>The chunk column and row of the second town (the block 896, -400): flat land north-east of the valley.</summary>
    public static readonly (int ChunkX, int ChunkZ) Second = (56, -25);

    private static readonly ConcurrentDictionary<(int ChunkX, int ChunkZ), Lazy<ProceduralReliefRegion>> Regions = new();

    /// <summary>The shipped data.</summary>
    public static GameData Data => TestGame.Data;

    /// <summary>The layout file's text.</summary>
    public static string Json => File.ReadAllText(Repo.Path("tests", "Vocaterra.Game.Core.Tests", "Fixtures", "sample-town.json"));

    /// <summary>The sample town's layout.</summary>
    public static SceneLayout Layout => SceneLayout.Parse(Json);

    /// <summary>The town at the first place, on the layer <see cref="ProceduralReliefRegion.Fit"/> chooses.</summary>
    public static ProceduralReliefRegion Town() => At(First);

    /// <summary>The town at another place.</summary>
    public static ProceduralReliefRegion At((int ChunkX, int ChunkZ) chunk) =>
        Regions.GetOrAdd(chunk, static c => new Lazy<ProceduralReliefRegion>(() =>
        {
            var layout = Layout;
            var placement = ProceduralReliefRegion.Fit(layout, c.ChunkX, c.ChunkZ, Data.WorldGen, Seed);
            return ProceduralReliefRegion.Create(SampleTown.Id, layout, placement, Data.WorldGen, Seed);
        })).Value;

    /// <summary>The region's name.</summary>
    public const string Id = "sample-town";

    /// <summary>
    /// The procedural world's ground at a column of the region's footprint as the town's own ground is made from it: the height model over the climate with the land pulled towards the region.
    /// The reference the tests compare the region's ground with.
    /// </summary>
    public static Func<int, int, int> FreeGround(RegionBounds footprint, ulong seed = Seed)
    {
        var world = Data.WorldGen;
        var blend = new RegionBlend(new RegionCatalog([new BoxRegion(footprint)]), world.Blend, world.Vertical.SeaLevel);
        var climate = new BlendedClimateSampler(new ClimateSampler(world, seed), blend);
        var height = new HeightModel(world, seed);
        return (x, z) => height.Shape(x, z, climate.At(x, z)).Height;
    }

    /// <summary>The block at a world position of a region, air outside it.</summary>
    public static BlockId BlockAt(IRegion region, int x, int y, int z)
    {
        var coord = new VoxelChunkCoord(x >> 4, y >> 4, z >> 4);
        return region.Covers(coord) ? region.Copy(coord)[x & 15, y & 15, z & 15] : BlockId.Air;
    }

    /// <summary>A region that is only a box (the blend reads nothing else of it for the land bias).</summary>
    private sealed class BoxRegion(RegionBounds bounds) : IRegion
    {
        public string Id => "box";

        public RegionBounds Bounds { get; } = bounds;

        public bool Covers(VoxelChunkCoord chunk) => Bounds.Covers(chunk);

        public Vocaterra.Game.Core.World.Streaming.ChunkData Copy(VoxelChunkCoord chunk) => throw new NotSupportedException();

        public int EdgeHeight(int x, int z) => throw new NotSupportedException();

        public int SmoothedEdgeHeight(int x, int z) => throw new NotSupportedException();
    }

    /// <summary>A ground that is the same shape wherever the layout stands: <paramref name="relative"/> of the layout's own column and row, lifted to the placement's floor plus <paramref name="above"/>.</summary>
    public sealed class RelativeGround(RegionPlacement placement, int above, Func<int, int, int> relative) : IGroundHeights
    {
        public int HeightAt(int x, int z) => placement.OriginY + above + relative(x - placement.OriginX, z - placement.OriginZ);
    }
}
