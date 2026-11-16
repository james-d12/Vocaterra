using System.IO.Compression;
using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.World.Lod;

/// <summary>Where a section of the far view's store is: its level (a voxel is 2^level blocks a side) and its place on that level's grid of sections (each 32 voxels a side), in all three axes.</summary>
/// <param name="Level">The level: 0 is the world block by block.</param>
/// <param name="X">The section's x, in sections of the level.</param>
/// <param name="Y">The section's y.</param>
/// <param name="Z">The section's z.</param>
public readonly record struct LodSectionKey(int Level, int X, int Y, int Z)
{
    /// <summary>The section one level up that holds this one (and seven others).</summary>
    public LodSectionKey Parent => new(Level + 1, X >> 1, Y >> 1, Z >> 1);

    /// <summary>The child at (<paramref name="dx"/>, <paramref name="dy"/>, <paramref name="dz"/>), each 0 or 1, one level down.</summary>
    /// <param name="dx">The child's x within its parent.</param>
    /// <param name="dy">The child's y.</param>
    /// <param name="dz">The child's z.</param>
    public LodSectionKey Child(int dx, int dy, int dz) => new(Level - 1, (X * 2) + dx, (Y * 2) + dy, (Z * 2) + dz);
}

/// <summary>Where a stored section's data came from; a better origin is never replaced by a worse one.</summary>
public enum LodOrigin : byte
{
    /// <summary>Made from the generator's own model (the height model, the biome table, the tree placement, the regions' blocks): an approximation at level 0 and above.</summary>
    Model = 0,

    /// <summary>A level-0 section made from real chunks (the generator's chunks, or the player's edits), wholly or in part (see the entry's mask).</summary>
    Real = 1,

    /// <summary>A section above level 0 made by mipping eight real sections.</summary>
    RealMip = 2,
}

/// <summary>A section as the store keeps it: packed bytes, where it came from and, for a level-0 section, which of its eight chunks are real (bit (dy &lt;&lt; 2) | (dz &lt;&lt; 1) | dx).</summary>
/// <param name="Packed">The section, <see cref="LodSection.Pack"/>.</param>
/// <param name="Origin">The origin.</param>
/// <param name="RealMask">The chunks that are real (all eight for a wholly real section).</param>
public sealed record LodEntry(byte[] Packed, LodOrigin Origin, byte RealMask)
{
    /// <summary>Whether every voxel of the section is real data.</summary>
    public bool IsWhollyReal => Origin == LodOrigin.RealMip || (Origin == LodOrigin.Real && RealMask == 0xFF);
}

/// <summary>
/// A cube of 32 x 32 x 32 voxels of the far view's store (B07c, the way the Voxy mod keeps its far world: true three-dimensional levels of detail, so a distant tree, an overhang or an arch keeps its shape, only coarser). Each voxel is a block
/// and a light byte (sky light in the high four bits, block light in the low four): the light of a voxel is what reaches that cell, so for the air in front of a face it is the light the face is lit by. Index = (y * 32 + z) * 32 + x. A
/// section that is one block and one light all through (the sky, the stone under the ground) holds no array at all.
/// </summary>
public sealed class LodSection
{
    /// <summary>The voxels along a side.</summary>
    public const int Size = 32;

    /// <summary>log2 of <see cref="Size"/>.</summary>
    public const int Shift = 5;

    /// <summary>The voxels in a section.</summary>
    public const int Volume = Size * Size * Size;

    /// <summary>The light byte of open sky: full sky light, no block light.</summary>
    public const byte SkyFull = 0xF0;

    private BlockId[]? _blocks;
    private byte[]? _light;
    private BlockId _fill;
    private byte _fillLight;

    /// <summary>Creates a section that is one block and one light all through.</summary>
    /// <param name="fill">The block.</param>
    /// <param name="fillLight">The light byte.</param>
    public LodSection(BlockId fill, byte fillLight)
    {
        _fill = fill;
        _fillLight = fillLight;
    }

    /// <summary>The index of the voxel at (x, y, z), each 0 to 31.</summary>
    /// <param name="x">The x.</param>
    /// <param name="y">The y.</param>
    /// <param name="z">The z.</param>
    public static int Index(int x, int y, int z) => (y << 10) | (z << 5) | x;

    /// <summary>A section of open air.</summary>
    public static LodSection Air() => new(BlockId.Air, SkyFull);

    /// <summary>Whether it is one block and one light all through.</summary>
    public bool IsUniform => _blocks is null && _light is null;

    /// <summary>The block it is made of when it is uniform.</summary>
    public BlockId FillBlock => _fill;

    /// <summary>Whether it is nothing but air.</summary>
    public bool IsEmpty => _blocks is null && _fill == BlockId.Air;

    /// <summary>The block of a voxel.</summary>
    /// <param name="index">The voxel's index.</param>
    public BlockId BlockAt(int index) => _blocks is null ? _fill : _blocks[index];

    /// <summary>The light byte of a voxel.</summary>
    /// <param name="index">The voxel's index.</param>
    public byte LightAt(int index) => _light is null ? _fillLight : _light[index];

    /// <summary>Sets a voxel.</summary>
    /// <param name="index">The voxel's index.</param>
    /// <param name="block">The block.</param>
    /// <param name="light">The light byte.</param>
    public void Set(int index, BlockId block, byte light)
    {
        Materialise();
        _blocks![index] = block;
        _light![index] = light;
    }

    /// <summary>Sets a block, leaving the light.</summary>
    /// <param name="index">The voxel's index.</param>
    /// <param name="block">The block.</param>
    public void SetBlock(int index, BlockId block)
    {
        Materialise();
        _blocks![index] = block;
    }

    /// <summary>Sets a light byte, leaving the block.</summary>
    /// <param name="index">The voxel's index.</param>
    /// <param name="light">The light byte.</param>
    public void SetLight(int index, byte light)
    {
        Materialise();
        _light![index] = light;
    }

    private void Materialise()
    {
        if (_blocks is not null)
        {
            return;
        }

        _blocks = new BlockId[Volume];
        _light = new byte[Volume];
        if (_fill != BlockId.Air)
        {
            Array.Fill(_blocks, _fill);
        }

        if (_fillLight != 0)
        {
            Array.Fill(_light, _fillLight);
        }
    }

    /// <summary>Drops the arrays when every voxel is the same block and light.</summary>
    public void Simplify()
    {
        if (_blocks is null || _light is null)
        {
            return;
        }

        var block = _blocks[0];
        var light = _light[0];
        for (var i = 1; i < Volume; i++)
        {
            if (_blocks[i] != block || _light[i] != light)
            {
                return;
            }
        }

        _fill = block;
        _fillLight = light;
        _blocks = null;
        _light = null;
    }

    /// <summary>A copy that can be changed without changing this one.</summary>
    public LodSection Copy()
    {
        var copy = new LodSection(_fill, _fillLight);
        if (_blocks is not null)
        {
            copy._blocks = (BlockId[])_blocks.Clone();
            copy._light = (byte[])_light!.Clone();
        }

        return copy;
    }

    /// <summary>The section as bytes: a uniform one is four bytes, any other is its blocks and lights deflated.</summary>
    public byte[] Pack()
    {
        if (_blocks is null)
        {
            return [0, (byte)((ushort)_fill & 0xFF), (byte)((ushort)_fill >> 8), _fillLight];
        }

        using var memory = new MemoryStream();
        memory.WriteByte(1);
        using (var deflate = new DeflateStream(memory, CompressionLevel.Fastest, leaveOpen: true))
        {
            var raw = new byte[Volume * 3];
            for (var i = 0; i < Volume; i++)
            {
                raw[i] = (byte)((ushort)_blocks[i] & 0xFF);
                raw[Volume + i] = (byte)((ushort)_blocks[i] >> 8);
                raw[(2 * Volume) + i] = _light![i];
            }

            deflate.Write(raw);
        }

        return memory.ToArray();
    }

    /// <summary>The section a packed array holds.</summary>
    /// <param name="packed">The bytes <see cref="Pack"/> made.</param>
    public static LodSection Unpack(ReadOnlySpan<byte> packed)
    {
        if (packed[0] == 0)
        {
            return new LodSection((BlockId)(packed[1] | (packed[2] << 8)), packed[3]);
        }

        var raw = new byte[Volume * 3];
        using (var input = new MemoryStream(packed[1..].ToArray()))
        using (var deflate = new DeflateStream(input, CompressionMode.Decompress))
        {
            deflate.ReadExactly(raw);
        }

        var section = new LodSection(BlockId.Air, 0) { _blocks = new BlockId[Volume], _light = new byte[Volume] };
        for (var i = 0; i < Volume; i++)
        {
            section._blocks![i] = (BlockId)(raw[i] | (raw[Volume + i] << 8));
            section._light![i] = raw[(2 * Volume) + i];
        }

        return section;
    }
}
