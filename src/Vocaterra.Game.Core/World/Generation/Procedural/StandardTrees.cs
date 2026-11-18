using Vocaterra.Game.Core.Mathematics;
using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.World.Generation.Procedural;

/// <summary>A tree species made of plain values: the three the valley has, and any a test defines.</summary>
/// <param name="Name">The species' name.</param>
/// <param name="DefaultCrown">The crown it has when its row names none.</param>
/// <param name="Trunk">The block of the trunk.</param>
/// <param name="Leaves">The block of the leaves.</param>
/// <param name="MinTrunk">The shortest trunk in blocks.</param>
/// <param name="MaxTrunk">The longest trunk in blocks.</param>
public sealed record TreeSpecies(string Name, string DefaultCrown, BlockId Trunk, BlockId Leaves, int MinTrunk, int MaxTrunk) : ITreeSpecies;

/// <summary>The species the code knows (B03), ported from the valley's forest: an oak, a spruce (a much taller trunk) and a birch (a little taller). The valley draws a trunk of 4 or 5 blocks and adds 6 for a spruce and 2 for a birch.</summary>
internal static class StandardTrees
{
    /// <summary>The oak, birch and spruce.</summary>
    public static IReadOnlyList<ITreeSpecies> Species { get; } =
    [
        new TreeSpecies("oak", "round", BlockId.Trunk, BlockId.Leaves, 4, 5),
        new TreeSpecies("spruce", "cone", BlockId.SpruceTrunk, BlockId.SpruceLeaves, 10, 11),
        new TreeSpecies("birch", "narrow", BlockId.BirchTrunk, BlockId.BirchLeaves, 6, 7),
    ];

    /// <summary>The round, small-round, narrow and cone crowns.</summary>
    public static IReadOnlyList<ICrownShape> Crowns { get; } = [new RoundCrown(), new FixedCrown("small-round", [0, 2, 5, 5, 2], 2), new FixedCrown("narrow", [0, 2, 5, 5, 2], 2), new ConeCrown()];

    // The farthest a layer with this squared limit reaches: the valley's table.
    internal static int ReachOf(int limit) => limit == 0 ? 0 : limit <= 2 ? 1 : limit <= 5 ? 2 : 3;

    // The oak's big crown, and its small one (a quarter of the oaks, by the tree's own bits).
    private sealed class RoundCrown : ICrownShape
    {
        private static readonly int[] Big = [1, 5, 10, 10, 5];
        private static readonly int[] Small = [0, 2, 5, 5, 2];

        public string Name => "round";

        public int Reach => 3;

        public int Rise => 3;

        public CrownProfile Profile(int trunkLength, ulong shape) => new(3, (shape & 3) == 0 ? Small : Big);
    }

    private sealed class FixedCrown(string name, int[] limits, int reach) : ICrownShape
    {
        public string Name { get; } = name;

        public int Reach { get; } = reach;

        public int Rise => 3;

        public CrownProfile Profile(int trunkLength, ulong shape) => new(3, Limits);

        private int[] Limits { get; } = limits;
    }

    // The spruce's tiers: a layer for every block of trunk but one, widening from the top.
    private sealed class ConeCrown : ICrownShape
    {
        private static readonly int[] Cone = [0, 1, 2, 2, 5, 5, 5, 10, 10, 10];

        public string Name => "cone";

        public int Reach => 3;

        public int Rise => 1;

        public CrownProfile Profile(int trunkLength, ulong shape)
        {
            var layers = new int[Math.Max(0, trunkLength - 1)];
            for (var k = 0; k < layers.Length; k++)
            {
                layers[k] = Cone[Math.Min(k, Cone.Length - 1)];
            }

            return new CrownProfile(1, layers);
        }
    }

    /// <summary>Whether a ragged rim drops the leaf at the offset: the outermost blocks of a layer go now and then, never the ones next to the trunk.</summary>
    internal static bool DropsRimLeaf(ulong shape, int dx, int dz, int layer, int limit, int d2) =>
        d2 >= limit - 1 && d2 > 1 && (WorldRng.Mix(shape ^ (ulong)((dx + 8) | ((dz + 8) << 4) | (layer << 8))) & 3) == 0;
}
