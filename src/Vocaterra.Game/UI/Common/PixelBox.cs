namespace Vocaterra.Game.UI.Common;

/// <summary>A rectangle in window pixels.</summary>
/// <param name="X">The left edge.</param>
/// <param name="Y">The top edge.</param>
/// <param name="Width">The width.</param>
/// <param name="Height">The height.</param>
public readonly record struct PixelBox(int X, int Y, int Width, int Height)
{
    /// <summary>The right edge (exclusive).</summary>
    public int Right => X + Width;

    /// <summary>The bottom edge (exclusive).</summary>
    public int Bottom => Y + Height;

    /// <summary>Whether the point is inside the box.</summary>
    public bool Contains(int x, int y) => x >= X && x < Right && y >= Y && y < Bottom;
}
