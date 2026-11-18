using Vocaterra.Game.Core.Mathematics;

namespace Vocaterra.Game.Core.World.Noise;

/// <summary>The conversion between the data's thousandths (a field's range runs from -1 000 to 1 000 in the files) and the fixed point the generation computes in (<see cref="IntMath.One"/> = 4 096).</summary>
internal static class FieldScale
{
    /// <summary>A number of thousandths as fixed point, rounded towards zero.</summary>
    /// <param name="thousandths">The value in thousandths.</param>
    public static int FromThousandths(int thousandths) => (int)((long)thousandths * IntMath.One / 1000);
}
