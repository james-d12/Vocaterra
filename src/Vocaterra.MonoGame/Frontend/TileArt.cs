using Vocaterra.Simulation.Items;
using Vocaterra.Simulation.Tiles;

namespace Vocaterra.MonoGame.Frontend;

/// <summary>
/// Placeholder tile art drawn in code (D-game-5: no third-party art yet): flat colours with a deterministic speckle, as RGBA bytes. The
/// engine layer uploads these into a texture; P4 replaces them with a real atlas and its grey copy. Transparent pixels have alpha 0.
/// </summary>
public static class TileArt
{
    /// <summary>The width and height of one tile's art in pixels.</summary>
    public const int Size = 16;

    /// <summary>The number of tile ids the atlas holds (ids are consecutive from <see cref="TileId.Air"/>).</summary>
    public static int TileCount => TileDefinition.All.Count;

    /// <summary>The number of tool icons after the tiles in the atlas: the axe, the pickaxe, then the sword.</summary>
    public const int ToolCount = 6;

    private static readonly (byte R, byte G, byte B, byte A) Transparent = (0, 0, 0, 0);
    private static readonly (byte R, byte G, byte B, byte A) Metal = (168, 170, 180, 255);

    /// <summary>The tile whose art an item shows as its icon, or <see langword="null"/> for a tool (which has its own icon, <see cref="ToolPixels"/>).</summary>
    public static TileId? TileOfItem(ItemId item) => item switch
    {
        ItemId.Wood => TileId.Planks,
        ItemId.Stone => TileId.Stone,
        ItemId.Dirt => TileId.Dirt,
        ItemId.Sand => TileId.Sand,
        ItemId.Flower => TileId.Flower,
        ItemId.Mushroom => TileId.Mushroom,
        ItemId.Door => TileId.Door,
        ItemId.Table => TileId.Table,
        ItemId.Chair => TileId.Chair,
        ItemId.Bed => TileId.Bed,
        ItemId.Chest => TileId.Chest,
        ItemId.Furnace => TileId.Furnace,
        ItemId.Glass => TileId.Glass,
        ItemId.Brick => TileId.Brick,
        ItemId.Coal => TileId.CoalOre,
        ItemId.Copper => TileId.CopperOre,
        ItemId.Iron => TileId.IronOre,
        ItemId.Gold => TileId.GoldOre,
        ItemId.Torch => TileId.Torch,
        _ => null,
    };

    /// <summary>The index of an item's icon among the tool icons (<see cref="ToolPixels"/>): the axe 0, the pickaxe 1, the sword 2, the lamp 3, the bucket 4, the ring 5.</summary>
    public static int ToolIndexOf(ItemId item) => item switch
    {
        ItemId.Axe => 0,
        ItemId.Pickaxe => 1,
        ItemId.Lamp => 3,
        ItemId.Bucket => 4,
        ItemId.Ring => 5,
        _ => 2,
    };

    /// <summary>The RGBA bytes of a tool icon: <c>0</c> is the axe, <c>1</c> the pickaxe, <c>2</c> the sword, <c>3</c> the lamp, <c>4</c> the bucket, <c>5</c> the ring.</summary>
    public static byte[] ToolPixels(int tool)
    {
        var data = new byte[Size * Size * 4];
        for (var y = 0; y < Size; y++)
        {
            for (var x = 0; x < Size; x++)
            {
                var (r, g, b, a) = tool switch
                {
                    0 => AxePixel(x, y),
                    1 => PickaxePixel(x, y),
                    2 => SwordPixel(x, y),
                    3 => LampPixel(x, y),
                    4 => BucketPixel(x, y),
                    _ => RingPixel(x, y),
                };
                var i = ((y * Size) + x) * 4;
                data[i] = r;
                data[i + 1] = g;
                data[i + 2] = b;
                data[i + 3] = a;
            }
        }

        return data;
    }

    private static (byte R, byte G, byte B, byte A) AxePixel(int x, int y)
    {
        if (x + y == 16 && y is >= 4 and <= 14)
        {
            return (120, 80, 40, 255);
        }

        return x is >= 10 and <= 14 && y is >= 1 and <= 5 ? Metal : Transparent;
    }

    private static (byte R, byte G, byte B, byte A) PickaxePixel(int x, int y)
    {
        if (x is 7 or 8 && y is >= 4 and <= 14)
        {
            return (120, 80, 40, 255);
        }

        var head = (y is 3 or 4 && x is >= 2 and <= 13) || (x is 2 or 3 or 12 or 13 && y is >= 3 and <= 6);
        return head ? Metal : Transparent;
    }

    private static (byte R, byte G, byte B, byte A) SwordPixel(int x, int y)
    {
        // A blade along the diagonal from the top right, a cross guard and a hilt at the bottom left.
        if (x + y == 15 && y is >= 1 and <= 10)
        {
            return Metal;
        }

        if (x + y is 14 or 16 && y is >= 2 and <= 9)
        {
            return Shade(205, 207, 216, 0);
        }

        if (x + y is >= 14 and <= 16 && y is >= 10 and <= 11)
        {
            return Shade(200, 160, 50, 0);
        }

        return x + y == 15 && y is >= 12 and <= 14 ? Shade(120, 80, 40, 0) : Transparent;
    }

    private static (byte R, byte G, byte B, byte A) LampPixel(int x, int y)
    {
        // A round lamp: a dark frame, a yellow glass and a handle on top.
        if (y == 1 && x is >= 6 and <= 9)
        {
            return Shade(80, 82, 90, 0);
        }

        var dx = (2 * x) - 15;
        var dy = (2 * y) - 17;
        var distance = (dx * dx) + (dy * dy);
        if (distance <= 13 * 13)
        {
            return distance >= 10 * 10 ? Shade(80, 82, 90, 0) : Shade(250, 220, 90, 0);
        }

        return Transparent;
    }

    private static (byte R, byte G, byte B, byte A) BucketPixel(int x, int y)
    {
        // An open bucket: a rim on top, a tapering body and a handle arc.
        if (y == 3 && x is >= 2 and <= 13)
        {
            return Metal;
        }

        if (y is >= 4 and <= 13 && x >= 3 + ((y - 4) / 3) && x <= 12 - ((y - 4) / 3))
        {
            return Shade(168, 170, 180, 0);
        }

        return y == 1 && x is >= 4 and <= 11 ? Shade(168, 170, 180, 0) : Transparent;
    }

    private static (byte R, byte G, byte B, byte A) RingPixel(int x, int y)
    {
        // A gold ring seen from the front: a thick circle with a hole in the middle.
        var dx = (2 * x) - 15;
        var dy = (2 * y) - 15;
        var distance = (dx * dx) + (dy * dy);
        return distance is <= 14 * 14 and >= 8 * 8 ? Shade(240, 190, 50, 0) : Transparent;
    }

    /// <summary>
    /// Whether a cell is part of an ore nugget: the cells group into 2 x 2 blocks, and about 45 % of the blocks are nuggets (a hash of the block,
    /// so the pattern is the same every run).
    /// </summary>
    private static bool Speck(int x, int y) => Mix(0x51, (uint)(x >> 1), (uint)(y >> 1)) % 100 < 45;

    private static (byte R, byte G, byte B, byte A) FlowerPixel(int x, int y, uint noise)
    {
        if (x is 7 or 8 && y is >= 7 and <= 15)
        {
            return Shade(60, 140, 60, noise);
        }

        if (y is 11 or 12 && x is >= 4 and <= 6)
        {
            return Shade(60, 140, 60, noise);
        }

        // Petals: a disc of radius 3 around (7.5, 4), in doubled coordinates so the centre is an integer.
        var dx = (2 * x) - 15;
        var dy = (2 * y) - 8;
        var distance = (dx * dx) + (dy * dy);
        if (distance <= 8)
        {
            return Shade(250, 210, 40, noise);
        }

        return distance <= 36 ? Shade(240, 80, 120, noise) : Transparent;
    }

    private static (byte R, byte G, byte B, byte A) MushroomPixel(int x, int y, uint noise)
    {
        if (x is >= 6 and <= 9 && y is >= 9 and <= 15)
        {
            return Shade(230, 222, 198, noise);
        }

        if (y is >= 3 and <= 8 && x is >= 2 and <= 13 && !(y == 3 && x is < 4 or > 11))
        {
            var spot = (x is 4 or 5 && y == 5) || (x is 10 or 11 && y == 6);
            return spot ? Shade(230, 230, 230, noise) : Shade(200, 50, 50, noise);
        }

        return Transparent;
    }

    /// <summary>The RGBA bytes (row by row, <see cref="Size"/> x <see cref="Size"/> x 4) of the art for <paramref name="id"/>.</summary>
    public static byte[] Pixels(TileId id)
    {
        var data = new byte[Size * Size * 4];
        for (var y = 0; y < Size; y++)
        {
            for (var x = 0; x < Size; x++)
            {
                var (r, g, b, a) = Pixel(id, x, y);
                var i = ((y * Size) + x) * 4;
                data[i] = r;
                data[i + 1] = g;
                data[i + 2] = b;
                data[i + 3] = a;
            }
        }

        return data;
    }

    private static (byte R, byte G, byte B, byte A) Pixel(TileId id, int x, int y)
    {
        var noise = Mix((uint)id, (uint)x, (uint)y) % 5; // 0..4: a small, deterministic speckle
        switch (id)
        {
            case TileId.Dirt:
                return Shade(120, 80, 48, noise);
            case TileId.Grass:
                return y < 4 ? Shade(78, 160, 56, noise) : Shade(120, 80, 48, noise);
            case TileId.Stone:
                return Shade(120, 124, 132, noise);
            case TileId.Trunk:
                return x is >= 5 and <= 10 ? Shade(102, 66, 34, noise) : Transparent;
            case TileId.Leaves:
                return Mix(7, (uint)x, (uint)y) % 6 == 0 ? Transparent : Shade(46, 128, 52, noise);
            case TileId.Sand:
                return Shade(222, 196, 120, noise);
            case TileId.Flower:
                return FlowerPixel(x, y, noise);
            case TileId.Mushroom:
                return MushroomPixel(x, y, noise);
            case TileId.Planks:
                return y % 4 == 3 ? Shade(140, 96, 52, noise) : Shade(190, 140, 80, noise);
            case TileId.Door:
                if (x is 1 or 14 || y is 0 or 15)
                {
                    return Shade(96, 60, 30, noise);
                }

                return x is 11 && y is 8 or 9 ? Shade(230, 190, 60, 0) : Shade(150, 98, 46, noise);
            case TileId.Table:
                if (y is >= 4 and <= 6)
                {
                    return Shade(170, 120, 70, noise);
                }

                return x is 2 or 3 or 12 or 13 && y >= 7 ? Shade(110, 74, 40, noise) : Transparent;
            case TileId.Chair:
                if ((x is 4 or 5 && y is >= 2 and <= 8) || (y is 9 or 10 && x is >= 3 and <= 12) || (x is 3 or 4 or 11 or 12 && y >= 11))
                {
                    return Shade(150, 98, 46, noise);
                }

                return Transparent;
            case TileId.Bed:
                if (y >= 13)
                {
                    return x is 1 or 2 or 13 or 14 ? Shade(96, 60, 30, noise) : Transparent;
                }

                if (y is >= 9 and <= 12)
                {
                    return Shade(140, 90, 45, noise);
                }

                if (y is >= 5 and <= 8 && x >= 4)
                {
                    return Shade(200, 60, 60, noise);
                }

                return y is 4 or 5 && x is >= 1 and <= 4 ? Shade(230, 230, 230, noise) : Transparent;
            case TileId.Chest:
                if (y is >= 6 and <= 14 && x is >= 2 and <= 13)
                {
                    if (y == 6)
                    {
                        return Shade(100, 66, 30, noise);
                    }

                    return x is 7 or 8 && y is 8 or 9 ? Shade(230, 190, 60, 0) : Shade(150, 100, 50, noise);
                }

                return Transparent;
            case TileId.Furnace:
                if (x is >= 6 and <= 9 && y is >= 7 and <= 12)
                {
                    return y >= 10 ? Shade(250, 190, 40, 0) : Shade(235, 110, 30, 0);
                }

                return y == 0 || y == 15 || x == 0 || x == 15 ? Shade(80, 82, 90, noise) : Shade(130, 134, 142, noise);
            case TileId.Glass:
                if (x is 0 or 15 || y is 0 or 15)
                {
                    return Shade(150, 205, 225, noise);
                }

                return x + y is 6 or 7 or 8 or 20 ? Shade(235, 248, 255, 0) : Shade(175, 220, 240, noise);
            case TileId.Brick:
                var mortar = y % 4 == 3 || ((y / 4 % 2 == 0 ? x : x + 4) % 8 == 7);
                return mortar ? Shade(200, 196, 190, noise) : Shade(170, 70, 50, noise);
            case TileId.CoalOre:
                return Speck(x, y) ? Shade(44, 44, 50, 0) : Shade(120, 124, 132, noise);
            case TileId.CopperOre:
                return Speck(x, y) ? Shade(214, 120, 62, 0) : Shade(120, 124, 132, noise);
            case TileId.IronOre:
                return Speck(x, y) ? Shade(232, 200, 170, 0) : Shade(120, 124, 132, noise);
            case TileId.GoldOre:
                return Speck(x, y) ? Shade(255, 214, 70, 0) : Shade(120, 124, 132, noise);
            case TileId.Torch:
                if (x is >= 6 and <= 9 && y is >= 2 and <= 5)
                {
                    return x is 7 or 8 && y is 3 or 4 ? Shade(255, 240, 130, 0) : Shade(250, 160, 40, 0);
                }

                return x is 7 or 8 && y is >= 6 and <= 15 ? Shade(120, 80, 40, noise) : Transparent;
            case TileId.TallGrass:
                if (x is 3 or 6 or 9 or 12 && y >= 8 + (x % 3))
                {
                    return Shade(70, 150, 50, noise);
                }

                return x is 4 or 7 or 10 or 13 && y >= 11 ? Shade(60, 130, 44, noise) : Transparent;
            default:
                return Transparent;
        }
    }

    private static (byte R, byte G, byte B, byte A) Shade(int r, int g, int b, uint noise)
    {
        var d = (int)noise * 5;
        return ((byte)(r + d), (byte)(g + d), (byte)(b + d), 255);
    }

    /// <summary>An integer hash of three values (a murmur-style finaliser), so the speckle needs neither a random generator nor the clock.</summary>
    private static uint Mix(uint a, uint b, uint c)
    {
        var h = unchecked((a * 0x9E3779B1) ^ (b * 0x85EBCA6B) ^ (c * 0xC2B2AE35));
        h ^= h >> 15;
        h = unchecked(h * 0x2C1B3C6D);
        h ^= h >> 12;
        h = unchecked(h * 0x297A2D39);
        return h ^ (h >> 15);
    }
}
