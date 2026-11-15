using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.Tests.Voxel;

/// <summary>Streamed grids and generators for the sparse-storage and residency tests.</summary>
internal static class StreamedGridSupport
{
    /// <summary>A generator that serves the content of a function and counts its calls.</summary>
    public sealed class CountingGenerator(Func<VoxelChunkCoord, ChunkContent?> make) : IChunkGenerator
    {
        public List<VoxelChunkCoord> Calls { get; } = [];

        public ChunkContent? Generate(VoxelChunkCoord coord)
        {
            Calls.Add(coord);
            return make(coord);
        }
    }

    /// <summary>A residency listener that records what it is told.</summary>
    public sealed class ResidencyRecorder : IChunkResidencyListener
    {
        public List<VoxelChunkCoord> Installed { get; } = [];

        public List<VoxelChunkCoord> Evicted { get; } = [];

        public void OnChunkInstalled(VoxelChunkCoord coord) => Installed.Add(coord);

        public void OnChunkEvicted(VoxelChunkCoord coord) => Evicted.Add(coord);
    }

    /// <summary>A change listener that records what it is told.</summary>
    public sealed class ChangeRecorder : IBlockChangeListener
    {
        public List<BlockChange> Changes { get; } = [];

        public void OnBlockChanged(BlockChange change) => Changes.Add(change);
    }

    /// <summary>The default streamed limits (x and z plus and minus 30 000, y -64 to 320) with no generator.</summary>
    public static VoxelGrid Default() => new(WorldLimits.Default);

    /// <summary>Content of one chunk filled with a block from its local y 0 up to <paramref name="topLocalY"/> inclusive.</summary>
    public static ChunkContent Floor(BlockId block, int topLocalY = 0)
    {
        var blocks = new BlockId[VoxelChunk.Volume];
        for (var i = 0; i < blocks.Length; i++)
        {
            if ((i >> (2 * VoxelChunk.Shift)) <= topLocalY)
            {
                blocks[i] = block;
            }
        }

        return new ChunkContent(blocks);
    }
}
