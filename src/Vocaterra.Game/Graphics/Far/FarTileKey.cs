namespace Vocaterra.Game.Graphics.Far;

/// <summary>One far-view tile: its level and its place on that level's grid (tile (tx, tz) covers the blocks from tx x size to the next multiple, so tiles of every level line up on the same lattice).</summary>
/// <param name="Level">The level: 0 is the finest.</param>
/// <param name="Tx">The tile column.</param>
/// <param name="Tz">The tile row.</param>
public readonly record struct FarTileKey(int Level, int Tx, int Tz)
{
    /// <summary>The parent tile one level up, which covers this one and its three siblings.</summary>
    public FarTileKey Parent => new(Level + 1, Tx >> 1, Tz >> 1);

    /// <summary>The first block column of the tile.</summary>
    /// <param name="settings">The settings (the tile size of the level).</param>
    public int MinX(FarViewSettings settings) => Tx * settings.SizeOf(Level);

    /// <summary>The first block row of the tile.</summary>
    /// <param name="settings">The settings.</param>
    public int MinZ(FarViewSettings settings) => Tz * settings.SizeOf(Level);

    /// <summary>The four children one level down.</summary>
    public IEnumerable<FarTileKey> Children()
    {
        yield return new FarTileKey(Level - 1, Tx * 2, Tz * 2);
        yield return new FarTileKey(Level - 1, (Tx * 2) + 1, Tz * 2);
        yield return new FarTileKey(Level - 1, Tx * 2, (Tz * 2) + 1);
        yield return new FarTileKey(Level - 1, (Tx * 2) + 1, (Tz * 2) + 1);
    }

    /// <summary>The ground distance in blocks from a point to the nearest point of the tile's footprint (0 inside it).</summary>
    /// <param name="settings">The settings.</param>
    /// <param name="x">The point's x.</param>
    /// <param name="z">The point's z.</param>
    public double NearestDistance(FarViewSettings settings, double x, double z)
    {
        var size = settings.SizeOf(Level);
        var x0 = (double)Tx * size;
        var z0 = (double)Tz * size;
        var dx = x < x0 ? x0 - x : x > x0 + size ? x - (x0 + size) : 0;
        var dz = z < z0 ? z0 - z : z > z0 + size ? z - (z0 + size) : 0;
        return Math.Sqrt((dx * dx) + (dz * dz));
    }

    /// <summary>The ground distance in blocks from a point to the farthest corner of the tile's footprint.</summary>
    /// <param name="settings">The settings.</param>
    /// <param name="x">The point's x.</param>
    /// <param name="z">The point's z.</param>
    public double FarthestDistance(FarViewSettings settings, double x, double z)
    {
        var size = settings.SizeOf(Level);
        var x0 = (double)Tx * size;
        var z0 = (double)Tz * size;
        var dx = Math.Max(Math.Abs(x - x0), Math.Abs(x - (x0 + size)));
        var dz = Math.Max(Math.Abs(z - z0), Math.Abs(z - (z0 + size)));
        return Math.Sqrt((dx * dx) + (dz * dz));
    }
}
