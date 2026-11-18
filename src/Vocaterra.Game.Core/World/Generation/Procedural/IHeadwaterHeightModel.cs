using Vocaterra.Game.Core.World.Climate;

namespace Vocaterra.Game.Core.World.Generation.Procedural;

/// <summary>A height model whose rivers and lakes can be scaled down (B02b): the blend near a region asks for a column with only a share of its water, so a river becomes a stream and a lake a pond.</summary>
internal interface IHeadwaterHeightModel : IHeightModel
{
    /// <summary>The shape of the column (x, z) with the river's width and depth and the lake's size scaled.</summary>
    /// <param name="x">The block's x.</param>
    /// <param name="z">The block's z.</param>
    /// <param name="c">The climate of the column.</param>
    /// <param name="water">The share of the water, 0 (none) to <see cref="Mathematics.IntMath.One"/> (the free world's).</param>
    ColumnShape Shape(int x, int z, in ClimateSample c, int water);
}
