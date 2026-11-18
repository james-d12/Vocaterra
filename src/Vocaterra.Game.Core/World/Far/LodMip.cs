using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World.Lod;

namespace Vocaterra.Game.Core.World.Far;

/// <summary>What the far view's voxels are made of, as the store sees blocks (B07c).</summary>
public static class LodBlocks
{
    /// <summary>Whether a real chunk's block is a voxel of the far view: the solid blocks (leaves, trunks and every cube), water and the blocks that give light; plants and the like are left out.</summary>
    /// <param name="block">The block.</param>
    public static bool IsFar(BlockId block) => block != BlockId.Air && (block == BlockId.Water || BlockDefinition.IsSolid(block) || BlockDefinition.IsLight(block));

    /// <summary>Whether a voxel hides what is behind it (everything but air and water).</summary>
    /// <param name="block">The block.</param>
    public static bool IsOpaque(BlockId block) => block != BlockId.Air && block != BlockId.Water;

    /// <summary>How much sky light a voxel takes from the light that passes through it: none for air, one for water and leaves, all for any other block.</summary>
    /// <param name="block">The block.</param>
    public static int SkyLoss(BlockId block) => block == BlockId.Air ? 0 : block == BlockId.Water || BlockDefinition.IsLeaves(block) ? 1 : 15;

    /// <summary>The block light a voxel gives itself.</summary>
    /// <param name="block">The block.</param>
    public static int BlockLight(BlockId block) => BlockDefinition.IsLight(block) ? 15 : 0;
}

/// <summary>
/// How a level is made from the one below (B07c): a section of the next level up is the mip of eight sections, each voxel the mip of eight. The rule keeps what is seen from outside. A voxel is water when its upper four hold water and no solid
/// (the water's surface is kept over its bed, one voxel at least). It is solid when at least two of its eight are (a one-block shell fills four of the eight it passes through, a peak's tip with the cell under it two), or one is a trunk (a trunk of one block that
/// carries a crown stays); its block is the one that is exposed, the most common solid block of the upper four if there is any, else of the lower four (so a slope's grass is not outvoted by the dirt under it; a trunk wins in its layer). Otherwise it is water when
/// two are, otherwise air. Light is the brightest of the eight. Pure.
/// </summary>
public static class LodMip
{
    /// <summary>The voxel of the next level up made from eight voxels (the children in the order (dy, dz, dx), dy slowest) and their light bytes.</summary>
    /// <param name="blocks">The eight blocks.</param>
    /// <param name="lights">The eight light bytes.</param>
    /// <param name="light">The light byte of the result.</param>
    public static BlockId Voxel(ReadOnlySpan<BlockId> blocks, ReadOnlySpan<byte> lights, out byte light)
    {
        var sky = 0;
        var lamp = 0;
        var solid = 0;
        var upperSolid = 0;
        var upperWater = 0;
        var water = 0;
        var trunk = false;
        for (var i = 0; i < 8; i++)
        {
            sky = Math.Max(sky, lights[i] >> 4);
            lamp = Math.Max(lamp, lights[i] & 0xF);
            var b = blocks[i];
            if (b == BlockId.Water)
            {
                water++;
                upperWater += i >= 4 ? 1 : 0;
            }
            else if (b != BlockId.Air)
            {
                solid++;
                upperSolid += i >= 4 ? 1 : 0;
                trunk |= BlockDefinition.IsTrunk(b);
            }
        }

        light = (byte)((sky << 4) | lamp);
        if (upperWater > 0 && upperSolid == 0)
        {
            return BlockId.Water;
        }

        if (solid >= 2 || trunk)
        {
            // The exposed block: the upper four's most common solid one, else the lower four's; a trunk of that layer first.
            var from = upperSolid > 0 ? 4 : 0;
            var best = BlockId.Air;
            var bestCount = 0;
            for (var i = from + 3; i >= from; i--)
            {
                var b = blocks[i];
                if (b == BlockId.Air || b == BlockId.Water)
                {
                    continue;
                }

                if (BlockDefinition.IsTrunk(b))
                {
                    return b;
                }

                var count = 0;
                for (var k = from; k < from + 4; k++)
                {
                    count += blocks[k] == b ? 1 : 0;
                }

                if (count > bestCount)
                {
                    best = b;
                    bestCount = count;
                }
            }

            return best;
        }

        return water >= 2 ? BlockId.Water : BlockId.Air;
    }

    /// <summary>The section of the next level up made from the eight sections it holds (the children in the order (dy, dz, dx), dy slowest, see <see cref="LodSectionKey.Child"/>).</summary>
    /// <param name="children">The eight sections.</param>
    public static LodSection Parent(ReadOnlySpan<LodSection> children)
    {
        if (children.Length != 8)
        {
            throw new ArgumentException("A section has eight children.", nameof(children));
        }

        var first = children[0];
        var uniform = first.IsUniform;
        for (var c = 1; c < 8 && uniform; c++)
        {
            uniform = children[c].IsUniform && children[c].FillBlock == first.FillBlock && children[c].LightAt(0) == first.LightAt(0);
        }

        if (uniform)
        {
            return new LodSection(first.FillBlock, first.LightAt(0));
        }

        var parent = new LodSection(BlockId.Air, 0);
        Span<BlockId> b = stackalloc BlockId[8];
        Span<byte> l = stackalloc byte[8];
        for (var y = 0; y < LodSection.Size; y++)
        {
            for (var z = 0; z < LodSection.Size; z++)
            {
                for (var x = 0; x < LodSection.Size; x++)
                {
                    var child = children[((y >> 4) << 2) | ((z >> 4) << 1) | (x >> 4)];
                    var (bx, by, bz) = ((x & 15) * 2, (y & 15) * 2, (z & 15) * 2);
                    for (var k = 0; k < 8; k++)
                    {
                        var i = LodSection.Index(bx + (k & 1), by + (k >> 2), bz + ((k >> 1) & 1));
                        b[k] = child.BlockAt(i);
                        l[k] = child.LightAt(i);
                    }

                    var block = Voxel(b, l, out var light);
                    parent.Set(LodSection.Index(x, y, z), block, light);
                }
            }
        }

        parent.Simplify();
        return parent;
    }

    /// <summary>
    /// Lights a stack of sections from the sky down (a section of the stack at a time, top first, <paramref name="skyAbove"/> the sky light entering the top of the first): the light of a voxel is the sky light that reaches it, and passes through
    /// air whole, through water and leaves a level less each, and not at all through any other block; a block that gives light has its own. Sky light never spreads sideways, so a cliff's foot is as bright as the open: the far view lights faces by
    /// what is in front of them and darkens only under crowns and water.
    /// </summary>
    /// <param name="section">The section.</param>
    /// <param name="skyAbove">The sky light entering the section's top, one value for each of its 32 x 32 columns (z x 32 + x).</param>
    /// <param name="skyBelow">The sky light leaving its bottom.</param>
    public static void Light(LodSection section, ReadOnlySpan<byte> skyAbove, Span<byte> skyBelow)
    {
        if (section.IsUniform && section.FillBlock == BlockId.Air && AllFull(skyAbove))
        {
            skyAbove.CopyTo(skyBelow);
            return;
        }

        if (section.IsUniform && LodBlocks.SkyLoss(section.FillBlock) >= 15)
        {
            skyBelow.Clear(); // a solid mass: no light goes into it, and the light of its own voxels is never read
            return;
        }

        for (var z = 0; z < LodSection.Size; z++)
        {
            for (var x = 0; x < LodSection.Size; x++)
            {
                int sky = skyAbove[(z << 5) | x];
                for (var y = LodSection.Size - 1; y >= 0; y--)
                {
                    var i = LodSection.Index(x, y, z);
                    var block = section.BlockAt(i);
                    section.SetLight(i, (byte)((sky << 4) | LodBlocks.BlockLight(block)));
                    sky = Math.Max(0, sky - LodBlocks.SkyLoss(block));
                }

                skyBelow[(z << 5) | x] = (byte)sky;
            }
        }

        section.Simplify();
    }

    private static bool AllFull(ReadOnlySpan<byte> sky)
    {
        foreach (var s in sky)
        {
            if (s != 15)
            {
                return false;
            }
        }

        return true;
    }
}
