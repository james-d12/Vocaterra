using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World.Far;
using Vocaterra.Game.Core.World.Generation.Procedural;
using Vocaterra.Game.Core.World.Regions;

namespace Vocaterra.Game.Core.World.Streaming;

/// <summary>
/// The pure parts of a streamed world a host drives its residency and its far view from (B07): the composite chunk source, the surface bands that say which layers of a column are worth holding,
/// the sampler of the far land and the regions. Every part is immutable and safe to call from any thread, so a host may call them from worker threads; the grid is the host's own and only the
/// host's game loop touches it, through <see cref="GridResidency"/>.
/// </summary>
public sealed class StreamedWorld
{
    internal StreamedWorld(CompositeChunkSource source, SurfaceBands bands, FarTerrainSampler far, RegionCatalog regions)
    {
        Source = source;
        Bands = bands;
        Far = far;
        Regions = regions;
    }

    /// <summary>The composite source of the world's chunks.</summary>
    public CompositeChunkSource Source { get; }

    /// <summary>Which layers of a chunk column meet the ground.</summary>
    public SurfaceBands Bands { get; }

    /// <summary>The far land's sampler.</summary>
    public FarTerrainSampler Far { get; }

    /// <summary>The authored regions (the valley).</summary>
    public RegionCatalog Regions { get; }

    /// <summary>
    /// The chunks of some layers of one chunk column, from the lowest layer up, made from one shared window, decorating only those layers (the cheap way: the climate and the height are made once, and the layers under the ground, which cost most, only when asked for).
    /// Each equals what <see cref="CompositeChunkSource.Generate"/> gives for its coordinates, so a worker thread may make them and the game loop may install them.
    /// </summary>
    /// <param name="chunkX">The chunk column.</param>
    /// <param name="chunkZ">The chunk row.</param>
    /// <param name="minLayer">The lowest layer wanted (inclusive).</param>
    /// <param name="maxLayer">The highest layer wanted (inclusive).</param>
    public IReadOnlyList<ChunkData> GenerateLayers(int chunkX, int chunkZ, int minLayer, int maxLayer) => Source.GenerateLayers(chunkX, chunkZ, minLayer, maxLayer);

    /// <summary>
    /// The same layers, with the column's window cut from the shaped window of a group that holds it (B39: <see cref="SurfaceBands.ShapeGroup"/>) instead of made again; the chunks are the same bytes. A column the group does not hold
    /// (or <see langword="null"/>) is made from its own window.
    /// </summary>
    /// <param name="chunkX">The chunk column.</param>
    /// <param name="chunkZ">The chunk row.</param>
    /// <param name="minLayer">The lowest layer wanted (inclusive).</param>
    /// <param name="maxLayer">The highest layer wanted (inclusive).</param>
    /// <param name="group">The group's shaped window, or <see langword="null"/>.</param>
    public IReadOnlyList<ChunkData> GenerateLayers(int chunkX, int chunkZ, int minLayer, int maxLayer, ColumnGroup? group) => Source.GenerateLayers(chunkX, chunkZ, minLayer, maxLayer, group);

    /// <summary>The natural surface heights (<c>IPlaceInfo.NaturalSurfaceAt</c>) of the 18 x 18 block columns round a chunk column, row by row from the block column one short of its corner (B44), for the sky's exposure; cut from the group's window when it holds the column.</summary>
    /// <param name="chunkX">The chunk column.</param>
    /// <param name="chunkZ">The chunk row.</param>
    /// <param name="group">The group's shaped window, or <see langword="null"/>.</param>
    public int[] NaturalTile(int chunkX, int chunkZ, ColumnGroup? group) => Source.NaturalTile(chunkX, chunkZ, group);

    /// <summary>Arrays for the window of one chunk column (B40), for the overload that takes them.</summary>
    public ColumnScratch CreateScratch() => Source.CreateScratch();

    /// <summary>The same layers with the column's window arrays taken from the caller's scratch (B40) instead of allocated; the chunks are the same bytes.</summary>
    /// <param name="chunkX">The chunk column.</param>
    /// <param name="chunkZ">The chunk row.</param>
    /// <param name="minLayer">The lowest layer wanted (inclusive).</param>
    /// <param name="maxLayer">The highest layer wanted (inclusive).</param>
    /// <param name="group">The group's shaped window, or <see langword="null"/>.</param>
    /// <param name="scratch">The caller's arrays, or <see langword="null"/>.</param>
    public IReadOnlyList<ChunkData> GenerateLayers(int chunkX, int chunkZ, int minLayer, int maxLayer, ColumnGroup? group, ColumnScratch? scratch) => Source.GenerateLayers(chunkX, chunkZ, minLayer, maxLayer, group, scratch);
}

/// <summary>
/// What a host does to the set of resident chunks of a streamed grid (B07): installs a chunk a worker made, drops one it no longer needs, and lists what is resident. Neither is an edit (they move no
/// edit count and wake no simulation), an install of a chunk that is already resident is dropped, and a changed chunk is state and is never dropped.
/// </summary>
public static class GridResidency
{
    /// <summary>Installs a generated chunk (a duplicate of a resident chunk is dropped).</summary>
    /// <param name="grid">The streamed grid.</param>
    /// <param name="chunk">The chunk as a source made it.</param>
    /// <returns>Whether the chunk was installed (false: it was resident, or lies outside the grid's limits).</returns>
    public static bool Install(VoxelGrid grid, ChunkData chunk)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(chunk);
        return grid.Limits.Contains(chunk.Coord) && grid.InstallChunk(chunk.Coord, ContentOf(chunk));
    }

    /// <summary>Drops a resident chunk when it is unchanged.</summary>
    /// <param name="grid">The streamed grid.</param>
    /// <param name="coord">The chunk.</param>
    /// <returns>Whether the chunk was dropped (false: not resident, or changed).</returns>
    public static bool TryEvict(VoxelGrid grid, VoxelChunkCoord coord)
    {
        ArgumentNullException.ThrowIfNull(grid);
        return grid.TryEvictChunk(coord);
    }

    /// <summary>The coordinates of the resident chunks (a snapshot, in no fixed order).</summary>
    /// <param name="grid">The streamed grid.</param>
    public static IReadOnlyList<VoxelChunkCoord> Resident(VoxelGrid grid)
    {
        ArgumentNullException.ThrowIfNull(grid);
        return grid.ResidentCoordinateList();
    }

    /// <summary>A chunk as installable content: <see langword="null"/> for air.</summary>
    /// <param name="data">The chunk.</param>
    internal static ChunkContent? ContentOf(ChunkData data) =>
        data.IsEmpty ? null : new ChunkContent(data.Blocks.ToArray(), data.HasWaterLevels ? data.WaterLevels.ToArray() : null);
}
