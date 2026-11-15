using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World.Streaming;

namespace Vocaterra.Game.Core.World;

/// <summary>
/// The chunks of a streamed world as the generator made them, read without installing anything (B07): what the guide's facts look at when a chunk is not resident. A read of a non-resident chunk through
/// the grid would generate it and keep it, and a window of the guide's search holds thousands of chunks (B06's finding: a few seconds, on the game loop's thread, for a snapshot outside the valley);
/// this reads a region's own chunk straight, answers a chunk above a column's surface band as air and one under it as the one shared array of stone (the generator's own shortcuts, so the answer is
/// exactly what a generation would give), and generates only the chunks that meet the ground. A chunk that was changed is resident and is read from the grid, never from here. Not thread safe: one
/// reader serves one thread (the game loop's), and remembers the bands it has asked for.
/// </summary>
public sealed class PristineChunks
{
    // The blocks of a chunk of stone, shared by every transient chunk of stone and never written (a transient chunk is only read).
    private static readonly BlockId[] StoneBlocks = Enumerable.Repeat(BlockId.Stone, VoxelChunk.Volume).ToArray();

    private readonly StreamedWorld _world;
    private readonly Dictionary<long, Column> _columns = [];

    /// <summary>Creates a reader over a streamed world.</summary>
    /// <param name="world">The pure parts of the world.</param>
    public PristineChunks(StreamedWorld world)
    {
        ArgumentNullException.ThrowIfNull(world);
        _world = world;
    }

    /// <summary>The chunk as generated, or <see langword="null"/> for a chunk of air. The chunk is not part of any grid: it is read, never written, and nothing keeps it.</summary>
    /// <param name="coord">The chunk (inside the world's limits).</param>
    public VoxelChunk? Read(VoxelChunkCoord coord)
    {
        if (_world.Regions.Covering(coord) is { } region)
        {
            return Make(coord, GridResidency.ContentOf(region.Copy(coord)));
        }

        var key = ((long)coord.X << 32) | (uint)coord.Z;
        if (!_columns.TryGetValue(key, out var column))
        {
            var band = _world.Bands.Of(coord.X, coord.Z);
            // The band's layers of a column are made together from one window (about what one chunk costs alone), the first time one of them is asked for.
            _columns[key] = column = new Column(band, band.IsEmpty ? null : _world.GenerateLayers(coord.X, coord.Z, band.MinLayer, band.MaxLayer));
        }

        if (!column.Band.IsEmpty)
        {
            if (coord.Y > column.Band.MaxLayer)
            {
                return null;
            }

            if (coord.Y < column.Band.MinLayer)
            {
                return Make(coord, new ChunkContent(StoneBlocks, null));
            }

            return Make(coord, GridResidency.ContentOf(column.Layers![coord.Y - column.Band.MinLayer]));
        }

        return Make(coord, GridResidency.ContentOf(_world.Source.Generate(coord)));
    }

    private sealed record Column(ColumnBand Band, IReadOnlyList<ChunkData>? Layers);

    private static VoxelChunk? Make(VoxelChunkCoord coord, ChunkContent? content)
    {
        if (content is null)
        {
            return null;
        }

        var chunk = new VoxelChunk(coord);
        chunk.Install(content);
        return chunk;
    }
}
