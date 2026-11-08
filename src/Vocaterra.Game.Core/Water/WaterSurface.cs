using Vocaterra.Game.Core.Physics;
using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.Water;

/// <summary>
/// The height of the water's top inside a cell. A source reaches 14/16 of a block (the renderer's surface constant), flowing water falls
/// linearly from 12/16 at distance 1 to 2/16 at distance 7 (<c>2 + (7 - distance) * 10 / 6</c>, whole sixteenths, rounding down), and a cell that is falling or has water
/// above it is full (16/16), so a column of water has no gaps.
/// </summary>
public static class WaterSurface
{
    /// <summary>The sixteenths of a block in a full cell.</summary>
    public const int Full = 16;

    /// <summary>The height of a source cell's surface, in sixteenths.</summary>
    public const int SourceSixteenths = 14;

    /// <summary>The height of flowing water at distance 1, in sixteenths.</summary>
    public const int FlowingMax = 12;

    /// <summary>The height of flowing water at the furthest distance, in sixteenths.</summary>
    public const int FlowingMin = 2;

    /// <summary>The height of the water's top in sixteenths of a block (2 to 16).</summary>
    public static int Sixteenths(WaterLevel level, bool waterAbove)
    {
        if (waterAbove || level.Falling)
        {
            return Full;
        }

        return level.IsSource ? SourceSixteenths : FlowingMin + ((WaterLevel.MaxDistance - level.Distance) * (FlowingMax - FlowingMin) / (WaterLevel.MaxDistance - 1));
    }

    /// <summary>The height of the water's top in <see cref="VoxelPhysics.UnitsPerBlock"/> units from the cell's floor.</summary>
    public static int Units(WaterLevel level, bool waterAbove) =>
        Sixteenths(level, waterAbove) * (VoxelPhysics.UnitsPerBlock / Full);
}
