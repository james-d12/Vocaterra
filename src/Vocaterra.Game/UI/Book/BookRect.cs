namespace Vocaterra.Game.UI.Book;

/// <summary>A rectangle in pixels (the engine's own type is not used in Frontend/, D-game-1).</summary>
/// <param name="X">The left edge.</param>
/// <param name="Y">The top edge.</param>
/// <param name="Width">The width.</param>
/// <param name="Height">The height.</param>
public readonly record struct BookRect(int X, int Y, int Width, int Height)
{
    /// <summary>The right edge.</summary>
    public int Right => X + Width;

    /// <summary>The bottom edge.</summary>
    public int Bottom => Y + Height;

    /// <summary>Whether a point is inside.</summary>
    /// <param name="x">The x of the point.</param>
    /// <param name="y">The y of the point.</param>
    public bool Contains(int x, int y) => x >= X && x < Right && y >= Y && y < Bottom;
}
