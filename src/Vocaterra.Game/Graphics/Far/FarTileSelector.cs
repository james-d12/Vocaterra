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
    public static IReadOnlyList<FarTileKey> Select(FarViewSettings settings, double eyeX, double eyeZ, int voxelViewDistance)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var leaves = new List<(double Distance, FarTileKey Key)>();
        if (!settings.Enabled)
        {
            return [];
        }

        var inner = settings.InnerFraction * voxelViewDistance;
        var top = settings.TopLevel;
        var size = settings.SizeOf(top);
        var x0 = (int)Math.Floor((eyeX - settings.Range) / size);
        var x1 = (int)Math.Floor((eyeX + settings.Range) / size);
        var z0 = (int)Math.Floor((eyeZ - settings.Range) / size);
        var z1 = (int)Math.Floor((eyeZ + settings.Range) / size);
        for (var tz = z0; tz <= z1; tz++)
        {
            for (var tx = x0; tx <= x1; tx++)
            {
                Visit(settings, new FarTileKey(top, tx, tz), eyeX, eyeZ, inner, voxelViewDistance, leaves);
            }
        }

        leaves.Sort(static (a, b) => a.Distance != b.Distance ? a.Distance.CompareTo(b.Distance)
            : a.Key.Level != b.Key.Level ? a.Key.Level.CompareTo(b.Key.Level)
            : a.Key.Tz != b.Key.Tz ? a.Key.Tz.CompareTo(b.Key.Tz) : a.Key.Tx.CompareTo(b.Key.Tx));
        return [.. leaves.Select(static l => l.Key)];
    }

    private static void Visit(FarViewSettings settings, FarTileKey key, double x, double z, double inner, int voxel, List<(double, FarTileKey)> leaves)
    {
        var nearest = key.NearestDistance(settings, x, z);
        if (nearest > settings.Range || key.FarthestDistance(settings, x, z) < inner)
        {
            return;
        }

        if (key.Level > 0 && nearest < settings.SplitDistance(key.Level, voxel))
        {
            foreach (var child in key.Children())
            {
                Visit(settings, child, x, z, inner, voxel, leaves);
            }

            return;
        }

        leaves.Add((nearest, key));
    }
}
