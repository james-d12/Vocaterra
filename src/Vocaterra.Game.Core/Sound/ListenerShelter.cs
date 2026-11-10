using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.Sound;

/// <summary>
/// Whether the listener stands under cover (E06): the share of the columns round the feet that have a block above the head, read from the world's highest block of each column, the
/// very query the rain's drops use to stop at a roof (<see cref="OverheadCover"/>). It reads and never writes: Sound does not touch the simulation. Nine columns, a block apart, so
/// the rain eases as the player walks out from under an eave and is not a step.
/// </summary>
public static class ListenerShelter
{
    /// <summary>How far (in blocks) the sampled columns reach from the feet on each side.</summary>
    public const int Reach = 1;

    /// <summary>
    /// The share of the columns round the listener that are covered, 0 (open sky) to 1 (a roof over all of them). With no <paramref name="highestBlock"/> the sky is open.
    /// </summary>
    /// <param name="highestBlock">Reads the highest block of a column (<c>VoxelGrid.HighestBlock</c>), or <see langword="null"/> for open ground.</param>
    /// <param name="listener">The listener: the feet's place and height.</param>
    public static double CoveredShare(Func<int, int, int>? highestBlock, Listener listener)
    {
        if (highestBlock is null)
        {
            return 0;
        }

        var x0 = (int)Math.Floor(listener.X);
        var z0 = (int)Math.Floor(listener.Z);
        var covered = 0;
        var total = 0;
        for (var dz = -Reach; dz <= Reach; dz++)
        {
            for (var dx = -Reach; dx <= Reach; dx++)
            {
                total++;
                if (OverheadCover.Covers(highestBlock(x0 + dx, z0 + dz), listener.Y))
                {
                    covered++;
                }
            }
        }

        return covered / (double)total;
    }
}
