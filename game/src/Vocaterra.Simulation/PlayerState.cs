using Vocaterra.Simulation.Physics;
using Vocaterra.Simulation.Tiles;

namespace Vocaterra.Simulation;

/// <summary>
/// A snapshot of the player body. <see cref="X"/> and <see cref="Y"/> are the top-left corner of the box in units of 1/4096 tile (see
/// <see cref="PhysicsConstants"/>); the box is <see cref="PhysicsConstants.PlayerWidth"/> wide and <see cref="PhysicsConstants.PlayerHeight"/> tall.
/// </summary>
/// <param name="X">The left edge in units.</param>
/// <param name="Y">The top edge in units (grows downwards).</param>
/// <param name="VelocityX">Horizontal speed in units per tick.</param>
/// <param name="VelocityY">Vertical speed in units per tick (negative is up).</param>
/// <param name="Grounded">Whether a solid tile is directly under the feet.</param>
/// <param name="Facing">-1 when last moving left, 1 when last moving right.</param>
public readonly record struct PlayerState(int X, int Y, int VelocityX, int VelocityY, bool Grounded, int Facing)
{
    /// <summary>The right edge in units.</summary>
    public int Right => X + PhysicsConstants.PlayerWidth;

    /// <summary>The bottom edge (the feet) in units.</summary>
    public int Bottom => Y + PhysicsConstants.PlayerHeight;

    /// <summary>The left edge in tiles, for drawing.</summary>
    public double LeftTiles => (double)X / PhysicsConstants.UnitsPerTile;

    /// <summary>The top edge in tiles, for drawing.</summary>
    public double TopTiles => (double)Y / PhysicsConstants.UnitsPerTile;

    /// <summary>The tile under the middle of the feet when standing (the tile row of the bottom edge minus one is the body's lowest row).</summary>
    public TilePoint FeetTile => new(
        TileCollider.FloorDiv(X + (PhysicsConstants.PlayerWidth / 2), PhysicsConstants.UnitsPerTile),
        TileCollider.FloorDiv(Y + PhysicsConstants.PlayerHeight - 1, PhysicsConstants.UnitsPerTile));
}
