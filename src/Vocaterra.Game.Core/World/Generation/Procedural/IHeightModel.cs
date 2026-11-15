using Vocaterra.Game.Core.World.Climate;

namespace Vocaterra.Game.Core.World.Generation.Procedural;

/// <summary>The ground of the procedural world as a pure function of the seed, the world data, a column and its climate (B01). Implementations are immutable, integer only and safe to call from any thread.</summary>
public interface IHeightModel
{
    /// <summary>The shape of the column (x, z).</summary>
    /// <param name="x">The block's x.</param>
    /// <param name="z">The block's z.</param>
    /// <param name="c">The climate of the column (<see cref="IClimateSampler.At"/>).</param>
    ColumnShape Shape(int x, int z, in ClimateSample c);
}
