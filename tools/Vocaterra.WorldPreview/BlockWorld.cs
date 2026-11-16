using Vocaterra.Game.Core.Content;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World;
using Vocaterra.Game.Core.World.Generation.Procedural;
using Vocaterra.Game.Core.World.Streaming;

namespace Vocaterra.WorldPreview;

/// <summary>
/// The real blocks of the world, for the underground pictures (B11): the procedural source of a seed (the free world), or the composite source of the valley world the streamed game builds, read by
/// whole chunk columns that are kept for the length of one picture. It only reads.
/// </summary>
internal sealed class BlockWorld
{
    private readonly IChunkSource _source;
    private readonly Dictionary<(int X, int Z), IReadOnlyList<ChunkData>> _columns = [];

    /// <summary>Opens the blocks of a seed.</summary>
    /// <param name="data">The bundle's content.</param>
    /// <param name="seed">The world seed.</param>
    /// <param name="valley">Whether the world has the valley as its region (the streamed game's world) instead of being free.</param>
    public BlockWorld(GameData data, ulong seed, bool valley)
    {
        ArgumentNullException.ThrowIfNull(data);
        _source = valley
            ? WorldFactory.Create(data, seed, WorldMode.Streamed).ChunkSource ?? throw new InvalidOperationException("The streamed world has no chunk source.")
            : new ProceduralChunkSource(data.WorldGen, data.Biomes, seed, null, data.Underground);
        MinY = data.WorldGen.Vertical.MinY;
        MaxY = data.WorldGen.Vertical.MaxY;
        SeaLevel = data.WorldGen.Vertical.SeaLevel;
    }

    /// <summary>The lowest block row of the world.</summary>
    public int MinY { get; }

    /// <summary>One above the highest block row.</summary>
    public int MaxY { get; }

    /// <summary>The sea level.</summary>
    public int SeaLevel { get; }

    /// <summary>The block at a position.</summary>
    /// <param name="x">The block's x.</param>
    /// <param name="y">The block's y.</param>
    /// <param name="z">The block's z.</param>
    public BlockId At(int x, int y, int z)
    {
        if (y < MinY || y >= MaxY)
        {
            return BlockId.Air;
        }

        var chunk = Column(x >> 4, z >> 4)[(y >> 4) - (MinY >> 4)];
        return chunk.IsEmpty ? BlockId.Air : chunk.Blocks[ChunkData.Index(x & 15, y & 15, z & 15)];
    }

    /// <summary>The y of the highest block of the column that is not air or water, or <see cref="MinY"/> - 1 when it has none.</summary>
    /// <param name="x">The block's x.</param>
    /// <param name="z">The block's z.</param>
    public int TopSolid(int x, int z)
    {
        var column = Column(x >> 4, z >> 4);
        for (var layer = column.Count - 1; layer >= 0; layer--)
        {
            var chunk = column[layer];
            if (chunk.IsEmpty)
            {
                continue;
            }

            for (var ly = VoxelChunk.Size - 1; ly >= 0; ly--)
            {
                if (chunk.Blocks[ChunkData.Index(x & 15, ly, z & 15)] is not (BlockId.Air or BlockId.Water))
                {
                    return MinY + (layer << VoxelChunk.Shift) + ly;
                }
            }
        }

        return MinY - 1;
    }

    private IReadOnlyList<ChunkData> Column(int chunkX, int chunkZ)
    {
        if (!_columns.TryGetValue((chunkX, chunkZ), out var column))
        {
            column = _source switch
            {
                ProceduralChunkSource procedural => procedural.GenerateColumn(chunkX, chunkZ),
                CompositeChunkSource composite => composite.GenerateColumn(chunkX, chunkZ),
                _ => [.. Enumerable.Range(MinY >> 4, (MaxY - MinY) >> 4).Select(y => _source.Generate(new VoxelChunkCoord(chunkX, y, chunkZ)))],
            };
            _columns[(chunkX, chunkZ)] = column;
        }

        return column;
    }
}

/// <summary>The colour of each block in the underground pictures: by the block's own name in the game's table (a tool's palette, not a rule of the world), a hue from the id for a block it has no colour for.</summary>
internal static class BlockColours
{
    private static readonly Dictionary<string, Rgb> ByName = new(StringComparer.Ordinal)
    {
        ["stone"] = new Rgb(128, 128, 132),
        ["slate"] = new Rgb(62, 66, 84),
        ["granite"] = new Rgb(172, 118, 112),
        ["limestone"] = new Rgb(214, 206, 170),
        ["bedrock"] = new Rgb(14, 14, 18),
        ["lava"] = new Rgb(255, 120, 24),
        ["water"] = new Rgb(54, 110, 200),
        ["gravel"] = new Rgb(150, 146, 140),
        ["iron-ore"] = new Rgb(232, 196, 160),
        ["tin-ore"] = new Rgb(210, 230, 236),
        ["copper-ore"] = new Rgb(40, 190, 140),
        ["grass"] = new Rgb(92, 160, 70),
        ["dirt"] = new Rgb(124, 88, 56),
        ["sand"] = new Rgb(218, 204, 140),
        ["snow"] = new Rgb(246, 249, 253),
        ["trunk"] = new Rgb(98, 70, 40),
        ["leaves"] = new Rgb(44, 112, 50),
        ["path"] = new Rgb(170, 146, 100),
    };

    /// <summary>The colour of a block; air is the picture's own background (the sky or an open cave).</summary>
    /// <param name="block">The block.</param>
    public static Rgb Of(BlockId block)
    {
        if (block == BlockId.Air)
        {
            return new Rgb(10, 12, 18);
        }

        var name = BlockDefinition.Get(block).Name;
        if (ByName.TryGetValue(name, out var colour))
        {
            return colour;
        }

        var hue = (int)block * 47 % 255;
        return new Rgb((byte)(80 + (hue / 2)), (byte)(120 + (((int)block * 31) % 100)), (byte)(160 - (hue / 3)));
    }
}
