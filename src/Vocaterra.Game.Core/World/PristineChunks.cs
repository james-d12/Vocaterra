using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World.Streaming;

namespace Vocaterra.Game.Core.World;

/// <summary>
/// The chunks of a streamed world as the generator made them, read without installing anything (B07): what the guide's facts look at when a chunk is not resident. A read of a non-resident chunk through
/// the grid would generate it and keep it, and a window of the guide's search holds thousands of chunks (B06's finding: a few seconds, on the game loop's thread, for a snapshot outside the valley);
/// this reads a region's own chunk straight, answers a chunk above a column's surface band as air (the generator's own shortcut), generates only the chunks of the band, and does not see under it: the
/// guide's world is the surface (the region's chunks, the surface band of every other column) and whatever the player changed, wherever it is, because the rock under the ground (the strata, the ores and the
/// caves of the underground) is not what a question about the land is about and a world that looked there would answer by what happened to be resident (<see cref="Sees"/> says which resident chunks count).
/// Not thread safe: one reader serves one thread (the game loop's), and remembers the bands it has asked for.
/// </summary>
public sealed class PristineChunks
{
    private readonly StreamedWorld _world;
    private readonly Dictionary<long, ColumnBand> _bands = [];
    private readonly Dictionary<long, IReadOnlyList<ChunkData>> _layers = [];

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

        var band = BandOf(coord);
        if (!band.IsEmpty)
        {
            if (coord.Y > band.MaxLayer || coord.Y < band.MinLayer)
            {
                return null; // air above the ground; under it, the rock is not part of the guide's world
            }

            // The band's layers of a column are made together from one window (about what one chunk costs alone), the first time one of them is asked for.
            var key = ((long)coord.X << 32) | (uint)coord.Z;
            if (!_layers.TryGetValue(key, out var layers))
            {
                _layers[key] = layers = _world.GenerateLayers(coord.X, coord.Z, band.MinLayer, band.MaxLayer);
            }

            return Make(coord, GridResidency.ContentOf(layers[coord.Y - band.MinLayer]));
        }

        return Make(coord, GridResidency.ContentOf(_world.Source.Generate(coord)));
    }

    /// <summary>
    /// Whether a resident chunk is part of the guide's world: one the player changed, one of a region, or one in the surface band of its column. A resident chunk under the band that nobody changed is there
    /// by the residency's choice (the near zone, the padding), not by the world's, so it does not count.
    /// </summary>
    /// <param name="resident">A resident chunk.</param>
    public bool Sees(VoxelChunk resident)
    {
        ArgumentNullException.ThrowIfNull(resident);
        if (resident.IsChanged || _world.Regions.Covering(resident.Coord) is not null)
        {
            return true;
        }

        var band = BandOf(resident.Coord);
        return band.IsEmpty || resident.Coord.Y >= band.MinLayer;
    }

    private ColumnBand BandOf(VoxelChunkCoord coord)
    {
        var key = ((long)coord.X << 32) | (uint)coord.Z;
        if (!_bands.TryGetValue(key, out var band))
        {
            _bands[key] = band = _world.Bands.Of(coord.X, coord.Z);
        }

        return band;
    }

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
