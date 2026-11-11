namespace Vocaterra.Game.Core.Voxel;

/// <summary>
/// What cover means for the rain (E06): the highest block of a column (<see cref="VoxelGrid.HighestBlock"/>, trees and roofs count) is the cover, and its top face is where a drop stops.
/// One definition for the drops that are drawn and for the sound of the rain on a listener, so the two never disagree about a roof.
/// </summary>
public static class OverheadCover
{
    /// <summary>How far above the feet the top of the cover must be for a body to stand under it (blocks): the head is 1.8 high, and the ground the feet stand on is below them.</summary>
    public const double Headroom = 1.5;

    /// <summary>The height of the top face of the cover of a column, in blocks (the y a drop stops at); 0 for an empty column.</summary>
    /// <param name="highestBlock">The column's highest block (<see cref="VoxelGrid.HighestBlock"/>), -1 when it is empty.</param>
    public static int Top(int highestBlock) => highestBlock + 1;

    /// <summary>Whether a body whose feet are at <paramref name="feetY"/> stands under the cover of a column: its top is above the head, not at the ground, the plants or the fence at the feet.</summary>
    /// <param name="highestBlock">The column's highest block.</param>
    /// <param name="feetY">The feet's height in blocks.</param>
    public static bool Covers(int highestBlock, double feetY) => Top(highestBlock) - feetY > Headroom;
}
