namespace Vocaterra.Game.Graphics.Far;

/// <summary>
/// Which far-view tiles are drawn from where the eye stands (B07): a quadtree over the top level's tiles. A tile beyond the range, or wholly inside the inner radius where the voxel chunks stand,
/// is left out; a tile above level 0 that is nearer than <see cref="FarViewSettings.SplitDistance"/> of its level is replaced by its four children; the rest are leaves. The leaves cover the ground
/// between the inner radius and the range exactly once, so there is no gap and no overlap between levels, and the choice depends on the eye's place alone (the tiles are cached by key, so walking
/// only builds the tiles that newly become leaves). Pure, with no graphics type.
/// </summary>
public static class FarTileSelector
{
    /// <summary>The leaves, nearest first (ties by level, row, column).</summary>
    /// <param name="settings">The far view's settings.</param>
    /// <param name="eyeX">The eye's x.</param>
    /// <param name="eyeZ">The eye's z.</param>
    /// <param name="voxelViewDistance">The voxel zone's radius in blocks: the inner radius is <see cref="FarViewSettings.InnerFraction"/> of it.</param>
    /// <param name="includeInner">Whether the tiles wholly inside the inner radius are leaves too (the far view then fills a part of the voxel zone until its chunks are drawn, and filters them itself).</param>
    /// <param name="reachScale">What the range is multiplied by (1 or more): the fog is planar and the choice radial, so a tile at the screen's side needs a reach of range over the cosine of its angle to end in full fog.</param>
    public static IReadOnlyList<FarTileKey> Select(FarViewSettings settings, double eyeX, double eyeZ, int voxelViewDistance, bool includeInner = false, double reachScale = 1.0)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var leaves = new List<(double Distance, FarTileKey Key)>();
        if (!settings.Enabled)
        {
            return [];
        }

        var inner = includeInner ? -1 : settings.InnerRadius(voxelViewDistance);
        var reach = settings.Range * Math.Max(1.0, reachScale);
        var top = settings.TopLevel;
        var size = settings.SizeOf(top);
        var x0 = (int)Math.Floor((eyeX - reach) / size);
        var x1 = (int)Math.Floor((eyeX + reach) / size);
        var z0 = (int)Math.Floor((eyeZ - reach) / size);
        var z1 = (int)Math.Floor((eyeZ + reach) / size);
        for (var tz = z0; tz <= z1; tz++)
        {
            for (var tx = x0; tx <= x1; tx++)
            {
                Visit(settings, new FarTileKey(top, tx, tz), eyeX, eyeZ, inner, reach, voxelViewDistance, leaves);
            }
        }

        leaves.Sort(static (a, b) => a.Distance != b.Distance ? a.Distance.CompareTo(b.Distance)
            : a.Key.Level != b.Key.Level ? a.Key.Level.CompareTo(b.Key.Level)
            : a.Key.Tz != b.Key.Tz ? a.Key.Tz.CompareTo(b.Key.Tz) : a.Key.Tx.CompareTo(b.Key.Tx));
        return [.. leaves.Select(static l => l.Key)];
    }

    private static void Visit(FarViewSettings settings, FarTileKey key, double x, double z, double inner, double reach, int voxel, List<(double, FarTileKey)> leaves)
    {
        var nearest = key.NearestDistance(settings, x, z);
        if (nearest > reach || key.FarthestDistance(settings, x, z) < inner)
        {
            return;
        }

        if (key.Level > 0 && nearest < settings.SplitDistance(key.Level, voxel))
        {
            foreach (var child in key.Children())
            {
                Visit(settings, child, x, z, inner, reach, voxel, leaves);
            }

            return;
        }

        leaves.Add((nearest, key));
    }
}
