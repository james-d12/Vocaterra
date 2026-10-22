namespace Vocaterra.MonoGame.Frontend;

/// <summary>
/// The art of the block atlas, drawn in code (no picture file) (D-game-5: nothing third-party, so the pictures
/// are CC0 by construction): flat bright colours with a deterministic speckle, as RGBA bytes. The palette follows the prototype brief: saturated
/// greens for plants, clear blues for water, warm wood and stone accents. Transparent pixels have alpha 0; water has a partial alpha.
/// </summary>
public static class BlockArt
{
    private static readonly (byte R, byte G, byte B, byte A) Clear = (0, 0, 0, 0);

    /// <summary>
    /// The RGBA bytes of the whole atlas, row by row (<see cref="BlockAtlasLayout.Width"/> x <see cref="BlockAtlasLayout.Height"/> x 4): the colour
    /// cells, then the grey copy of each cell (its <see cref="Greyscale"/> luma, alpha kept) in the place <see cref="BlockAtlasLayout.GreyPixelRectangle"/> gives.
    /// </summary>
    public static byte[] AtlasPixels()
    {
        var width = BlockAtlasLayout.Width;
        var data = new byte[width * BlockAtlasLayout.Height * 4];
        foreach (var cell in Enum.GetValues<BlockCell>())
        {
            var art = Pixels(cell);
            Paste(data, width, BlockAtlasLayout.PixelRectangle(cell), art);
            Paste(data, width, BlockAtlasLayout.GreyPixelRectangle(cell), Greyscale.ToLuma(art));
        }

        return data;
    }

    /// <summary>The RGBA bytes of one cell's grey copy (row by row, 16 x 16 x 4): the luma of <see cref="Pixels"/>, alpha kept.</summary>
    public static byte[] GreyPixels(BlockCell cell) => Greyscale.ToLuma(Pixels(cell));

    private static void Paste(byte[] atlas, int width, (int X, int Y, int Width, int Height) rectangle, byte[] art)
    {
        for (var y = 0; y < BlockAtlasLayout.CellSize; y++)
        {
            Array.Copy(art, y * BlockAtlasLayout.CellSize * 4, atlas, (((rectangle.Y + y) * width) + rectangle.X) * 4, BlockAtlasLayout.CellSize * 4);
        }
    }

    /// <summary>The RGBA bytes (row by row, 16 x 16 x 4) of one cell.</summary>
    public static byte[] Pixels(BlockCell cell)
    {
        const int Size = BlockAtlasLayout.CellSize;
        var data = new byte[Size * Size * 4];
        for (var y = 0; y < Size; y++)
        {
            for (var x = 0; x < Size; x++)
            {
                var (r, g, b, a) = Pixel(cell, x, y);
                var i = ((y * Size) + x) * 4;
                data[i] = r;
                data[i + 1] = g;
                data[i + 2] = b;
                data[i + 3] = a;
            }
        }

        return data;
    }

    private static (byte R, byte G, byte B, byte A) Pixel(BlockCell cell, int x, int y)
    {
        var noise = Mix((uint)cell, (uint)x, (uint)y);
        var speckle = noise % 5; // 0..4
        var rare = (noise >> 8) % 11 == 0;
        switch (cell)
        {
            case BlockCell.GrassTop:
                return rare ? Shade(112, 214, 78, 0) : Shade(84, 182, 58, speckle);
            case BlockCell.GrassSide:
                {
                    // A ragged fringe: the grass hangs 3 to 5 pixels down depending on the column.
                    var hang = 3 + (int)(Mix(1, (uint)x, 0) % 3);
                    return y < hang ? Shade(84, 182, 58, speckle) : y == hang ? Shade(70, 150, 48, 0) : Shade(140, 98, 60, speckle);
                }

            case BlockCell.Dirt:
                return rare ? Shade(108, 74, 44, 0) : Shade(142, 100, 62, speckle);
            case BlockCell.Stone:
                {
                    var crack = ((x + (y * 3)) % 11 == 0) || (y == 8 && x is > 2 and < 12);
                    return crack ? Shade(100, 104, 116, 0) : Shade(138, 142, 152, speckle);
                }

            case BlockCell.Snow:
                return rare ? Shade(214, 226, 246, 0) : Shade(244, 248, 255, speckle);
            case BlockCell.Sand:
                return rare ? Shade(206, 180, 110, 0) : Shade(238, 214, 136, speckle);
            case BlockCell.TrunkSide:
                return x % 4 == 1 ? Shade(86, 54, 28, speckle) : Shade(122, 80, 42, speckle);
            case BlockCell.TrunkEnd:
                {
                    var dx = (2 * x) - 15;
                    var dy = (2 * y) - 15;
                    var d2 = (dx * dx) + (dy * dy);
                    return d2 > 196 ? Shade(104, 68, 36, speckle) : d2 % 56 < 14 ? Shade(150, 104, 54, 0) : Shade(196, 150, 86, speckle);
                }

            case BlockCell.Leaves:
                return rare ? Shade(96, 214, 84, 0) : (noise >> 4) % 4 == 0 ? Shade(34, 124, 44, speckle) : Shade(50, 164, 58, speckle);
            case BlockCell.Bush:
                return rare ? Shade(120, 206, 70, 0) : (noise >> 4) % 3 == 0 ? Shade(44, 130, 40, speckle) : Shade(70, 168, 48, speckle);
            case BlockCell.BerryBush:
                {
                    var berry = (x is 3 or 4 && y is 3 or 4) || (x is 10 or 11 && y is 2 or 3) || (x is 7 or 8 && y is 8 or 9) || (x is 2 or 3 && y is 11 or 12) || (x is 12 or 13 && y is 10 or 11);
                    return berry ? Shade(226, 36, 52, 0) : (noise >> 4) % 3 == 0 ? Shade(44, 130, 40, speckle) : Shade(70, 168, 48, speckle);
                }

            case BlockCell.Water:
                {
                    var ripple = (x + (y * 2)) % 9 == 0 || ((x * 3) + y) % 13 == 0;
                    return ripple ? ((byte)120, (byte)190, (byte)255, (byte)170) : ((byte)(36 + (speckle * 3)), (byte)(120 + (speckle * 3)), (byte)(228 + speckle), (byte)150);
                }

            case BlockCell.PathTop:
                return rare ? Shade(176, 138, 86, 0) : (noise >> 4) % 7 == 0 ? Shade(226, 196, 140, 0) : Shade(206, 168, 106, speckle);
            case BlockCell.Planks:
                return PlankPixel(x, y, speckle);
            case BlockCell.DoorLeaf:
                if (x is 0 or 15 || y is 0 or 15)
                {
                    return Shade(96, 58, 28, speckle);
                }

                if (x is 11 or 12 && y is 8 or 9)
                {
                    return Shade(250, 206, 60, 0);
                }

                return x % 5 == 4 ? Shade(122, 78, 38, speckle) : Shade(166, 108, 52, speckle);
            case BlockCell.TableTop:
                {
                    var grid = x % 8 == 0 || y % 8 == 0;
                    var tool = (x is >= 3 and <= 5 && y is >= 3 and <= 12) || (y is 3 or 4 && x is >= 9 and <= 13);
                    return tool ? Shade(168, 172, 184, 0) : grid ? Shade(130, 84, 40, speckle) : Shade(214, 160, 90, speckle);
                }

            case BlockCell.TableSide:
                {
                    if (x is 0 or 15 || y == 0 || y == 15)
                    {
                        return Shade(104, 66, 32, speckle);
                    }

                    var saw = y is 5 or 6 && x is >= 3 and <= 12;
                    var hammer = (x is 5 or 6 && y is >= 8 and <= 13) || (y is 8 or 9 && x is >= 3 and <= 8);
                    return saw ? Shade(188, 192, 204, 0) : hammer ? Shade(98, 108, 140, 0) : Shade(196, 142, 78, speckle);
                }

            case BlockCell.TorchStick:
                return x % 3 == 1 ? Shade(104, 66, 32, speckle) : Shade(136, 90, 44, speckle);
            case BlockCell.TorchFlame:
                return x is >= 6 and <= 9 && y is >= 6 and <= 9 ? Shade(255, 244, 150, 0) : Shade(255, 168, 40, speckle);
            case BlockCell.StallTop:
                return (x / 4) % 2 == 0 ? Shade(232, 62, 56, speckle) : Shade(252, 248, 240, speckle);
            case BlockCell.StallSide:
                if (y is 0 or 15 || x is 0 or 15)
                {
                    return Shade(110, 70, 34, speckle);
                }

                return y % 4 == 3 ? Shade(150, 104, 56, speckle) : Shade(200, 146, 82, speckle);
            case BlockCell.RoofTop:
                {
                    var seam = y % 4 == 3 || ((x + (y / 4 % 2 * 4)) % 8 == 7);
                    return seam ? Shade(168, 66, 40, 0) : Shade(224, 98, 60, speckle);
                }

            case BlockCell.RoofSide:
                return y % 4 == 3 ? Shade(150, 58, 36, 0) : Shade(198, 86, 54, speckle);
            case BlockCell.Fence:
                return x % 6 == 2 ? Shade(122, 80, 40, speckle) : y % 8 == 7 ? Shade(142, 96, 50, 0) : Shade(190, 136, 76, speckle);
            case BlockCell.Wall:
                {
                    var frame = x is 0 or 1 or 14 or 15 || y is 0 or 1 or 14 or 15;
                    return frame ? Shade(128, 84, 44, speckle) : Shade(244, 230, 196, speckle);
                }

            case BlockCell.FireRing:
                return (noise >> 5) % 13 == 0 ? Shade(70, 64, 60, 0) : Shade(158, 158, 168, speckle);
            case BlockCell.SignBoard:
                {
                    if (x is 0 or 15 || y is 0 or 15)
                    {
                        return Shade(104, 66, 32, speckle);
                    }

                    var text = y is 4 or 5 && x is >= 3 and <= 12 || y is 9 or 10 && x is >= 3 and <= 10;
                    return text ? Shade(80, 44, 20, 0) : Shade(218, 168, 94, speckle);
                }

            case BlockCell.BridgeTop:
                return x % 8 == 7 ? Shade(86, 54, 28, 0) : y % 4 == 3 ? Shade(150, 100, 52, speckle) : Shade(196, 140, 78, speckle);
            case BlockCell.BridgeSide:
                return y % 4 == 3 ? Shade(96, 60, 30, speckle) : Shade(150, 100, 52, speckle);
            case BlockCell.White:
                return (255, 255, 255, 255);
            case BlockCell.IconWood:
                return WoodIconPixel(x, y, speckle);
            case BlockCell.IconBerry:
                return BerryIconPixel(x, y);
            case BlockCell.IconBottle:
                return BottleIconPixel(x, y, filled: false);
            case BlockCell.IconWaterBottle:
                return BottleIconPixel(x, y, filled: true);
            case BlockCell.IconBread:
                return BreadIconPixel(x, y);
            default:
                return Clear;
        }
    }

    private static (byte R, byte G, byte B, byte A) PlankPixel(int x, int y, uint speckle)
    {
        if (y % 4 == 3)
        {
            return Shade(140, 94, 48, 0);
        }

        var joint = (y / 4 % 2 == 0 ? x : x + 8) % 16 == 15;
        return joint ? Shade(150, 102, 52, 0) : Shade(210, 154, 86, speckle);
    }

    private static (byte R, byte G, byte B, byte A) WoodIconPixel(int x, int y, uint speckle)
    {
        // A short log lying along the diagonal: bark on the long side, a pale end on the lower left.
        var along = x + y;
        var across = x - y;
        if (along is >= 10 and <= 21 && across is >= -4 and <= 4)
        {
            if (along <= 12 && across is >= -3 and <= 3)
            {
                return Shade(204, 156, 92, speckle);
            }

            return across % 3 == 0 ? Shade(88, 56, 28, speckle) : Shade(126, 82, 42, speckle);
        }

        return Clear;
    }

    private static (byte R, byte G, byte B, byte A) BerryIconPixel(int x, int y)
    {
        // Three red berries on a green stalk with a leaf.
        var stalk = x == 8 && y is >= 2 and <= 5;
        var leaf = (x is 9 or 10 && y == 3) || (x == 11 && y == 2);
        if (stalk || leaf)
        {
            return Shade(60, 160, 52, 0);
        }

        foreach (var (cx, cy) in new[] { (5, 9), (10, 9), (8, 12) })
        {
            var dx = x - cx;
            var dy = y - cy;
            if ((dx * dx) + (dy * dy) <= 6)
            {
                return dx == -1 && dy == -1 ? Shade(255, 150, 150, 0) : Shade(224, 34, 52, 0);
            }
        }

        return Clear;
    }

    // Whether a pixel is inside the bottle's glass: a neck two wide, shoulders, and a round-cornered body.
    private static bool InBottle(int x, int y) => y switch
    {
        3 or 4 or 5 => x is 7 or 8,
        6 => x is >= 6 and <= 9,
        >= 7 and <= 13 => x is >= 5 and <= 10,
        14 => x is >= 6 and <= 9,
        _ => false,
    };

    private static (byte R, byte G, byte B, byte A) BottleIconPixel(int x, int y, bool filled)
    {
        // A cork on top of a pale glass bottle; the water, when there is some, fills the body from the shoulders down.
        if (y is 1 or 2 && x is 7 or 8)
        {
            return Shade(176, 120, 64, 0);
        }

        if (!InBottle(x, y))
        {
            return Clear;
        }

        var edge = !InBottle(x - 1, y) || !InBottle(x + 1, y) || !InBottle(x, y - 1) || !InBottle(x, y + 1);
        if (edge)
        {
            return Shade(104, 150, 186, 0);
        }

        if (filled && y >= 8)
        {
            return x == 6 && y is >= 9 and <= 11 ? Shade(150, 200, 255, 0) : Shade(44, 116, 226, 0);
        }

        return x == 6 && y is >= 8 and <= 11 ? Shade(250, 253, 255, 0) : Shade(206, 232, 246, 0);
    }

    private static (byte R, byte G, byte B, byte A) BreadIconPixel(int x, int y)
    {
        // A golden loaf, wider than tall, with three slashes across the top.
        var dx = (x - 7.5) / 6.5;
        var dy = (y - 9.5) / 4.5;
        var r2 = (dx * dx) + (dy * dy);
        if (r2 > 1.0)
        {
            return Clear;
        }

        if (r2 > 0.72)
        {
            return Shade(150, 92, 38, 0); // the dark crust edge
        }

        var slash = y is >= 7 and <= 10 && (x + y) % 4 == 0 && x is >= 5 and <= 10;
        if (slash)
        {
            return Shade(238, 200, 128, 0);
        }

        return y <= 9 ? Shade(214, 148, 66, 0) : Shade(190, 122, 52, 0);
    }

    private static (byte R, byte G, byte B, byte A) Shade(int r, int g, int b, uint noise)
    {
        var d = (int)noise * 4;
        return ((byte)Math.Clamp(r + d, 0, 255), (byte)Math.Clamp(g + d, 0, 255), (byte)Math.Clamp(b + d, 0, 255), 255);
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
