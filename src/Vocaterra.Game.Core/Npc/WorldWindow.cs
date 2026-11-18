using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World;
using Vocaterra.Game.Core.World.Regions;

namespace Vocaterra.Game.Core.Npc;

/// <summary>
/// The box of the world the guide's facts look at (B06, biomes plan section 3.7), in blocks and whole chunks: the world is too large to search, so a question is answered from a window. Inside
/// an authored region the window is the region (the valley's answers stay what they were); elsewhere it is a box of <see cref="Radius"/> blocks each side of the player, from
/// <see cref="Below"/> blocks under to <see cref="Above"/> above, grown out to whole chunks and cut to the world's limits.
/// </summary>
/// <param name="MinX">The first block column.</param>
/// <param name="MinY">The first block layer.</param>
/// <param name="MinZ">The first block row.</param>
/// <param name="EndX">One past the last block column.</param>
/// <param name="EndY">One past the last block layer.</param>
/// <param name="EndZ">One past the last block row.</param>
public readonly record struct WorldWindow(int MinX, int MinY, int MinZ, int EndX, int EndY, int EndZ)
{
    /// <summary>The blocks each side of the player, across, that the window reaches outside a region.</summary>
    public const int Radius = 128;

    /// <summary>The blocks under the player the window reaches outside a region.</summary>
    public const int Below = 64;

    /// <summary>The blocks over the player the window reaches outside a region.</summary>
    public const int Above = 64;

    /// <summary>The box of a grid's limits.</summary>
    /// <param name="limits">The limits (a bounded grid's start at the origin).</param>
    public static WorldWindow Of(WorldLimits limits) => new(limits.MinX, limits.MinY, limits.MinZ, limits.EndX, limits.EndY, limits.EndZ);

    /// <summary>The box of a region's chunks.</summary>
    /// <param name="bounds">The region's bounds.</param>
    public static WorldWindow Of(RegionBounds bounds) => new(bounds.MinX, bounds.MinY, bounds.MinZ, bounds.MaxX + 1, bounds.MaxY + 1, bounds.MaxZ + 1);

    /// <summary>
    /// The window for a player at a place: the whole grid when it is bounded; the region's box when the place is inside a region of the scene's place query; otherwise the box round the place,
    /// in whole chunks, cut to the limits.
    /// </summary>
    /// <param name="scene">The scene.</param>
    /// <param name="x">The player's x.</param>
    /// <param name="y">The player's y.</param>
    /// <param name="z">The player's z.</param>
    public static WorldWindow For(Scene scene, int x, int y, int z)
    {
        ArgumentNullException.ThrowIfNull(scene);
        var limits = scene.Grid.Limits;
        if (!scene.Grid.IsStreamed)
        {
            return Of(limits);
        }

        if (scene.Places?.RegionAt(x, z) is { } region)
        {
            return Of(region.Bounds);
        }

        var min = Chunked(x - Radius, y - Below, z - Radius, up: false);
        var end = Chunked(x + Radius + 1, y + Above + 1, z + Radius + 1, up: true);
        return new WorldWindow(
            Math.Max(min.X, limits.MinX),
            Math.Max(min.Y, limits.MinY),
            Math.Max(min.Z, limits.MinZ),
            Math.Min(end.X, limits.EndX),
            Math.Min(end.Y, limits.EndY),
            Math.Min(end.Z, limits.EndZ));
    }

    /// <summary>Whether the block lies inside.</summary>
    /// <param name="x">The block's x.</param>
    /// <param name="y">The block's y.</param>
    /// <param name="z">The block's z.</param>
    public bool Contains(int x, int y, int z) => x >= MinX && y >= MinY && z >= MinZ && x < EndX && y < EndY && z < EndZ;

    private static (int X, int Y, int Z) Chunked(int x, int y, int z, bool up)
    {
        const int Mask = VoxelChunk.Size - 1;
        return up ? ((x + Mask) & ~Mask, (y + Mask) & ~Mask, (z + Mask) & ~Mask) : (x & ~Mask, y & ~Mask, z & ~Mask);
    }
}
