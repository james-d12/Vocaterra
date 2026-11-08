using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.Water.Flow;

/// <summary>A cell waiting for a flow update, and the tick it falls due.</summary>
/// <param name="Due">The tick from which the cell may be updated.</param>
/// <param name="Position">The cell.</param>
public readonly record struct FlowEntry(long Due, BlockPos Position)
{
    /// <summary>The order the queue serves entries in: due tick, then y, then z, then x.</summary>
    public static IComparer<FlowEntry> Order { get; } = Comparer<FlowEntry>.Create(static (a, b) =>
    {
        var byDue = a.Due.CompareTo(b.Due);
        if (byDue != 0)
        {
            return byDue;
        }

        var byY = a.Position.Y.CompareTo(b.Position.Y);
        if (byY != 0)
        {
            return byY;
        }

        var byZ = a.Position.Z.CompareTo(b.Position.Z);
        return byZ != 0 ? byZ : a.Position.X.CompareTo(b.Position.X);
    });
}
