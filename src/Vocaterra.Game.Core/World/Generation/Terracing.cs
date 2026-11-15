using Vocaterra.Game.Core.Mathematics;

namespace Vocaterra.Game.Core.World.Generation;

/// <summary>
/// The stepped faces both generators share (moved out of the valley's <c>TerrainGenerator</c> unchanged, B01): above a foot height the ground climbs in flat shelves of
/// a few blocks joined by short risers, so a steep slope becomes a stepped cliff. Pure integer maths on heights in sixteenths of a block.
/// </summary>
internal static class Terracing
{
    /// <summary>
    /// The height with shelves of <paramref name="terraceBlocks"/> blocks, blended in from <paramref name="foot16"/> to <paramref name="full16"/> (both in sixteenths) so the
    /// low ground stays as it was. A step of one block or less leaves the height alone.
    /// </summary>
    /// <param name="h16">The height in sixteenths of a block.</param>
    /// <param name="terraceBlocks">The shelf height in blocks.</param>
    /// <param name="foot16">The height where the shelves start to show, in sixteenths.</param>
    /// <param name="full16">The height where they are fully formed, in sixteenths.</param>
    public static int Apply(int h16, int terraceBlocks, int foot16, int full16) => Apply(h16, terraceBlocks, IntMath.Ramp(h16, foot16, full16));

    /// <summary>The height with shelves of <paramref name="terraceBlocks"/> blocks blended in by a weight (0 to <see cref="IntMath.One"/>).</summary>
    /// <param name="h16">The height in sixteenths of a block.</param>
    /// <param name="terraceBlocks">The shelf height in blocks.</param>
    /// <param name="strength">How much of the stepped height shows.</param>
    public static int Apply(int h16, int terraceBlocks, int strength)
    {
        if (terraceBlocks <= 1 || strength == 0)
        {
            return h16;
        }

        var step = terraceBlocks * 16;
        var baseLevel = h16 / step * step;
        var f = (h16 - baseLevel) * IntMath.One / step;
        var g = IntMath.Ramp(f, 1500, 2700);
        var stepped = baseLevel + (int)((long)g * step / IntMath.One);
        return IntMath.Lerp(h16, stepped, strength);
    }
}
